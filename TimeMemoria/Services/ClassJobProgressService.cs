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

        /// <summary>"combat", "craft", or "gather" — matching the ledger's groupings.</summary>
        public string Category { get; init; } = "combat";

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
                        IsLimitedJob     = isLimited,
                        Category         = GetCategory(job)
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
        /// Splits a class or job into the ledger's three groupings. Combat jobs carry
        /// a non-zero Role. Hand and land both sit at Role 0, and DohDolJobIndex
        /// cannot separate them — it numbers within each group, so the crafters run
        /// 0-7 and the gatherers 0-2. Row IDs do separate them and are stable across
        /// languages: 8-15 are Carpenter through Culinarian, 16-18 the gatherers.
        /// </summary>
        private static string GetCategory(ClassJob job)
        {
            if (job.Role != 0)
                return "combat";

            return job.RowId switch
            {
                >= 16 and <= 18 => "gather",
                >= 8  and <= 15 => "craft",
                _               => "combat"
            };
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
                .SelectMany(group =>
                {
                    // A slot usually holds one base class and its job (Gladiator and
                    // Paladin), where only the job is worth showing. Arcanist is the
                    // exception: Summoner and Scholar both sit on it and the game
                    // lists both, so emit every job on the slot when there is one.
                    var jobs = group.Where(job => job.JobIndex > 0)
                                    .OrderBy(job => job.RowId)
                                    .ToList();

                    return jobs.Count > 0
                        ? jobs
                        : group.OrderBy(job => job.RowId).Take(1);
                })
                // UIPriority is the sheet's own display-order field — the same one
                // the in-game Character panel sorts by — so this matches what the
                // player already sees rather than inventing an order.
                .OrderBy(job => job.UIPriority)
                .ThenBy(job => job.ExpArrayIndex)
                .ToList();

            pluginLog.Debug($"[ClassJobProgress] Tracking {trackedJobs.Count} class/job slots");
            return trackedJobs;
        }
    }
}
