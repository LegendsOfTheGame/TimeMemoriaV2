using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Dalamud.Plugin.Services;

namespace TimeMemoria.Services
{
    public enum QuestlineUnlockState
    {
        Unlocked,
        SpoilerLocked,
        FreeTrialLocked,
    }

    public class TocEntry
    {
        [JsonProperty("Patch")]
        public string Patch { get; set; } = string.Empty;

        [JsonProperty("Expansion")]
        public string Expansion { get; set; } = string.Empty;

        [JsonProperty("Role")]
        public string Role { get; set; } = string.Empty;

        [JsonProperty("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("Ids")]
        public List<uint> Ids { get; set; } = new();
    }

    public class QuestlineDefinition
    {
        public string Name            { get; init; } = string.Empty;
        public string Expansion       { get; init; } = string.Empty;
        public List<int> PatchPrefixes { get; init; } = new();
        public string? UnlockPatch    { get; init; }
        public bool RequiresFullVersion { get; init; }
    }

    public static class QuestlineRegistry
    {
        public static readonly List<string> Expansions = new()
        {
            "A Realm Reborn",
            "Heavensward",
            "Stormblood",
            "Shadowbringers",
            "Endwalker",
            "Dawntrail",
        };

        public static readonly List<QuestlineDefinition> All = new()
        {
            // A Realm Reborn
            new() { Name = "Seventh Umbral Era",     Expansion = "A Realm Reborn",  PatchPrefixes = new(){20},             UnlockPatch = null,  RequiresFullVersion = false },
            new() { Name = "Seventh Astral Era",     Expansion = "A Realm Reborn",  PatchPrefixes = new(){21,22,23,24,25}, UnlockPatch = "2.1", RequiresFullVersion = false },

            // Heavensward
            new() { Name = "Heavensward",            Expansion = "Heavensward",     PatchPrefixes = new(){30},             UnlockPatch = "3.0", RequiresFullVersion = false },
            new() { Name = "Dragonsong",             Expansion = "Heavensward",     PatchPrefixes = new(){31,32,33},       UnlockPatch = "3.1", RequiresFullVersion = false },
            new() { Name = "Post-Dragonsong",        Expansion = "Heavensward",     PatchPrefixes = new(){34,35},          UnlockPatch = "3.4", RequiresFullVersion = false },

            // Stormblood
            new() { Name = "Stormblood",             Expansion = "Stormblood",      PatchPrefixes = new(){40},             UnlockPatch = "4.0", RequiresFullVersion = false },
            new() { Name = "Post-Stormblood",        Expansion = "Stormblood",      PatchPrefixes = new(){41,42,43,44,45}, UnlockPatch = "4.1", RequiresFullVersion = true  },

            // Shadowbringers
            new() { Name = "Shadowbringers",         Expansion = "Shadowbringers",  PatchPrefixes = new(){50},             UnlockPatch = "5.0", RequiresFullVersion = true  },
            new() { Name = "Post-Shadowbringers",    Expansion = "Shadowbringers",  PatchPrefixes = new(){51,52,53},       UnlockPatch = "5.1", RequiresFullVersion = true  },
            new() { Name = "Post-Shadowbringers II", Expansion = "Shadowbringers",  PatchPrefixes = new(){54,55},          UnlockPatch = "5.4", RequiresFullVersion = true  },

            // Endwalker
            new() { Name = "Endwalker",              Expansion = "Endwalker",       PatchPrefixes = new(){60},             UnlockPatch = "6.0", RequiresFullVersion = true  },
            new() { Name = "Post-Endwalker",         Expansion = "Endwalker",       PatchPrefixes = new(){61,62,63,64,65}, UnlockPatch = "6.1", RequiresFullVersion = true  },

            // Dawntrail
            new() { Name = "Dawntrail",              Expansion = "Dawntrail",       PatchPrefixes = new(){70},             UnlockPatch = "7.0", RequiresFullVersion = true  },
            new() { Name = "Post-Dawntrail",         Expansion = "Dawntrail",       PatchPrefixes = new(){71,72,73},       UnlockPatch = "7.1", RequiresFullVersion = true  },
            new() { Name = "Post-Dawntrail II",      Expansion = "Dawntrail",       PatchPrefixes = new(){74,75},          UnlockPatch = "7.4", RequiresFullVersion = true  },
        };

        public static readonly List<(string Suffix, string Label)> BucketDisplayNames = new()
        {
            ("msq",      "Main Scenario"),
            ("NewEra",   "Chronicles of a New Era"),
            ("Feature",  "Feature Quests"),
            ("Beasts",   "Beast Tribes"),
            ("Class",    "Class & Job Quests"),
            ("Leve",     "Leve Quests"),
            ("Other",    "Other Quests"),
            ("Seasonal", "Seasonal Events"),
        };
    }

    public class TocService
    {
        private readonly Dictionary<string, List<uint>> _startIds = new();
        private readonly Dictionary<string, List<uint>> _finalIds = new();
        private readonly IPluginLog _log;

        public TocService(IPluginLog log, string tocJsonPath)
        {
            _log = log;

            if (!File.Exists(tocJsonPath))
            {
                _log.Warning($"[TocService] toc.json not found at: {tocJsonPath}");
                return;
            }

            try
            {
                var json    = File.ReadAllText(tocJsonPath);
                var entries = JsonConvert.DeserializeObject<List<TocEntry>>(json) ?? new();

                foreach (var entry in entries)
                {
                    if (entry.Role == "Start")
                        _startIds[entry.Patch] = entry.Ids;
                    else if (entry.Role == "Final")
                        _finalIds[entry.Patch] = entry.Ids;
                }

                _log.Info($"[TocService] Loaded {entries.Count} toc entries.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[TocService] Failed to load toc.json");
            }
        }

        /// <summary>
        /// Returns true if the player has completed the Start quest for the
        /// given patch string (e.g. "2.1"), meaning that patch is accessible.
        /// Patch "2.0" is always accessible — no Start quest required.
        /// </summary>
        public bool IsPatchUnlocked(string patch)
        {
            if (patch == "2.0") return true;

            if (!_startIds.TryGetValue(patch, out var ids)) return false;

            foreach (var id in ids)
            {
                if (FFXIVClientStructs.FFXIV.Client.Game.QuestManager.IsQuestComplete(id))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the unlock state for a questline based on the player's
        /// progression, Free Trial setting, and Spoiler Mode setting.
        /// </summary>
        public QuestlineUnlockState GetUnlockState(
            QuestlineDefinition questline,
            bool freeTrialMode,
            bool spoilerMode)
        {
            // Free Trial lock — cannot be bypassed by Spoiler Mode
            if (freeTrialMode && questline.RequiresFullVersion)
                return QuestlineUnlockState.FreeTrialLocked;

            // 2.0 is always unlocked
            if (questline.UnlockPatch == null)
                return QuestlineUnlockState.Unlocked;

            if (IsPatchUnlocked(questline.UnlockPatch))
                return QuestlineUnlockState.Unlocked;

            // Spoiler Mode bypasses progression lock only
            return spoilerMode
                ? QuestlineUnlockState.Unlocked
                : QuestlineUnlockState.SpoilerLocked;
        }
    }
}
