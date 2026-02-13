using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

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

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;

        public void Save() => pluginInterface?.SavePluginConfig(this);

        public void Reset()
        {
            StartArea = string.Empty;
            GrandCompany = string.Empty;
            StartClass = 0;
            CategorySelection = null;
            SubcategorySelection = null;
            CompletedBuckets.Clear();
            Save();
        }
    }
}
