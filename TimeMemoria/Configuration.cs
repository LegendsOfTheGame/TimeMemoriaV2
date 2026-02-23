using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using TimeMemoria.Models;

namespace TimeMemoria
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool ShowCount { get; set; } = true;
        public bool ShowPercentage { get; set; }
        public bool ExcludeOtherQuests { get; set; }
        public int DisplayOption { get; set; }
        public string StartArea { get; set; } = string.Empty;
        public string GrandCompany { get; set; } = string.Empty;
        public uint StartClass { get; set; }
        public QuestData? CategorySelection { get; set; }
        public QuestData? SubcategorySelection { get; set; }

        // Lazy-loading: cache of fully completed buckets
        public Dictionary<string, bool> CompletedBuckets { get; set; } = new();

        // Quest browser options
        public bool DisableLazyLoad { get; set; } = false;

        // Spoiler / access control
        public bool SpoilerMode   { get; set; } = false;
        public bool FreeTrialMode { get; set; } = false;

        /// <summary>
        /// Per-character playtime records, keyed by "CharacterName@WorldName".
        /// Persists session and lifetime playtime, quest completion counts.
        /// </summary>
        public Dictionary<string, PlaytimeRecord> PlaytimeRecords { get; set; } = new();

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface) =>
            this.pluginInterface = pluginInterface;

        public void Save() => pluginInterface?.SavePluginConfig(this);

        public void Reset()
        {
            StartArea            = string.Empty;
            GrandCompany         = string.Empty;
            StartClass           = 0;
            CategorySelection    = null;
            SubcategorySelection = null;
            CompletedBuckets.Clear();
            Save();
        }
    }
}
