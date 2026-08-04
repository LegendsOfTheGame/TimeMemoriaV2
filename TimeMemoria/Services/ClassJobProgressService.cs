using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace TimeMemoria.Services
{
    /// <summary>
    /// A single class or job's current progression: level, experience earned toward
    /// the next level, and how much that level requires in total.
    /// Read directly from game memory — nothing here is stored or accumulated.
    /// </summary>
    public sealed class ClassJobProgress
    {
        public string Name         { get; init; } = string.Empty;
        public string Abbreviation { get; init; } = string.Empty;
        public int    Level        { get; init; }
        public int    Experience   { get; init; }

        /// <summary>Total experience the current level requires. Zero at max level.</summary>
        public int ExperienceToNext { get; init; }

        /// <summary>Blue Mage and friends — capped below the normal level ceiling.</summary>
        public bool IsLimitedJob { get; init; }

        public bool IsUnlocked  => Level > 0;
        public bool IsMaxLevel  => IsUnlocked && ExperienceToNext == 0;

        /// <summary>Progress through the current level, 0..1. Reads as full at max level.</summary>
        public float Fraction =>
            ExperienceToNext > 0 ? Math.Clamp(Experience / (float)ExperienceToNext, 0f, 1f) : 1f;
    }


    /// <summary>
    /// Reports the player's class and job levels with experience progress.
    /// Values come from Dalamud's IPlayerState, which reads the game's own
    /// per-character arrays — this service holds no state of its own.
    /// </summary>
    public sealed class ClassJobProgressService
    {
        private readonly IPlayerState playerState;
        private readonly IDataManager dataManager;
        private readonly IPluginLog   pluginLog;

        /// <summary>
        /// One representative ClassJob row per experience slot, resolved once.
        /// The ClassJob sheet lists both the base class and its job (Gladiator and
        /// Paladin, say), and the two share a single ExpArrayIndex — so we keep one
        /// row per slot and prefer the job, which is how players think about it.
        /// </summary>
        private List<ClassJob>? trackedJobs;

        public ClassJobProgressService(
            IPlayerState playerState,
            IDataManager dataManager,
            IPluginLog   pluginLog)
        {
            this.playerState = playerState;
            this.dataManager = dataManager;
            this.pluginLog   = pluginLog;
        }


        /// <summary>
        /// Returns every trackable class and job with its current level and experience.
        /// Returns an empty list when no character is loaded.
        /// </summary>
        public unsafe List<ClassJobProgress> GetProgress()
        {
            if (!playerState.IsLoaded)
                return new List<ClassJobProgress>();

            try
            {
                var jobs      = GetTrackedJobs();
                var paramGrow = dataManager.GetExcelSheet<ParamGrow>();
                var result    = new List<ClassJobProgress>(jobs.Count);

                // The level ceiling for this character, which accounts for which
                // expansions the account actually owns.
                var state    = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
                int maxLevel = state != null ? state->MaxLevel : 0;

                foreach (var job in jobs)
                {
                    var level = playerState.GetClassJobLevel(job);
                    var exp   = playerState.GetClassJobExperience(job);

                    // ParamGrow is indexed by level and holds the experience that
                    // level requires. It keeps returning a value at the ceiling —
                    // the game simply stops awarding experience — so cap detection
                    // has to come from MaxLevel rather than from a zero here.
                    var toNext   = 0;
                    var isLimited = job.IsLimitedJob;
                    var atCeiling = !isLimited && maxLevel > 0 && level >= maxLevel;

                    if (level > 0 && !atCeiling)
                    {
                        var row = paramGrow.GetRowOrDefault((uint)level);
                        if (row.HasValue)
                            toNext = row.Value.ExpToNext;
                    }

                    result.Add(new ClassJobProgress
                    {
                        Name             = ToDisplayName(job.Name.ToString()),
                        Abbreviation     = job.Abbreviation.ToString(),
                        Level            = level,
                        Experience       = atCeiling ? 0 : exp,
                        ExperienceToNext = toNext,
                        IsLimitedJob     = isLimited
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "[ClassJobProgress] Failed to read class/job progression");
                return new List<ClassJobProgress>();
            }
        }


        /// <summary>
        /// Serialises current progression to JSON for the clipboard. Local only —
        /// nothing is transmitted; the player pastes this wherever they want it.
        /// </summary>
        public string ExportJson()
        {
            var payload = new
            {
                character = playerState.IsLoaded ? playerState.CharacterName : string.Empty,
                world     = playerState.IsLoaded
                                ? playerState.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty
                                : string.Empty,
                exportedUtc = DateTime.UtcNow.ToString("o"),
                classJobs = GetProgress()
                            .Where(p => p.IsUnlocked)
                            .Select(p => new
                            {
                                name         = p.Name,
                                abbreviation = p.Abbreviation,
                                level        = p.Level,
                                exp          = p.Experience,
                                expToNext    = p.ExperienceToNext
                            })
            };

            return JsonSerializer.Serialize(
                payload, new JsonSerializerOptions { WriteIndented = true });
        }


        /// <summary>
        /// The ClassJob sheet stores names lowercase ("white mage"), while the game
        /// UI presents them capitalised. Only re-cases strings that arrive entirely
        /// lowercase, so localisations with their own casing rules are left alone.
        /// </summary>
        private static string ToDisplayName(string name)
        {
            if (name.Length == 0 || name.Any(char.IsUpper))
                return name;

            return System.Globalization.CultureInfo.CurrentCulture
                         .TextInfo.ToTitleCase(name);
        }


        /// <summary>
        /// Builds the one-row-per-experience-slot list. Rows with a negative
        /// ExpArrayIndex do not gain experience (Adventurer, and the placeholder
        /// rows), so they are skipped entirely.
        /// </summary>
        private List<ClassJob> GetTrackedJobs()
        {
            if (trackedJobs != null)
                return trackedJobs;

            var sheet = dataManager.GetExcelSheet<ClassJob>();

            trackedJobs = sheet
                .Where(job => job.ExpArrayIndex >= 0 && !job.Name.IsEmpty)
                .GroupBy(job => job.ExpArrayIndex)
                // Within a shared slot the job row carries the higher JobIndex;
                // crafters and gatherers sit alone in their slot and are unaffected.
                .Select(group => group.OrderByDescending(job => job.JobIndex)
                                      .ThenBy(job => job.RowId)
                                      .First())
                .OrderBy(job => job.ExpArrayIndex)
                .ToList();

            pluginLog.Debug($"[ClassJobProgress] Tracking {trackedJobs.Count} class/job slots");
            return trackedJobs;
        }
    }
}
