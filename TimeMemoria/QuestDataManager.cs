using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;

namespace TimeMemoria
{
    class QuestDataManager
    {
        public IPluginLog pluginLog { get; private set; }

        private readonly Plugin plugin;
        private readonly Configuration configuration;
        private readonly IDalamudPluginInterface pluginInterface;

        // Cache: "2.x/2.0/other" -> [all quests from that bucket file]
        private readonly Dictionary<string, List<Quest>> _loadedBuckets = new();
        
        // Track currently active bucket for unloading
        private string? _activeBucketPath = null;

        public QuestDataManager(
            IDalamudPluginInterface pluginInterface, 
            IPluginLog pluginLog,
            Plugin plugin, 
            Configuration configuration)
        {
            this.pluginInterface = pluginInterface;
            this.pluginLog = pluginLog;
            this.plugin = plugin;
            this.configuration = configuration;
            
            LoadLegacyQuestData();
            
            // Pre-load all seasonal content (small size, always available)
            PreloadSeasonalContent();
        }

        /// <summary>
        /// Pre-load all seasonal event buckets on startup
        /// </summary>
        private void PreloadSeasonalContent()
        {
            var seasonalStubs = FindAllStubsWithStrategy(plugin.QuestData, "AlwaysLoad");
            
            foreach (var stub in seasonalStubs)
            {
                if (stub.BucketPath != null && stub.BucketPath.EndsWith("/seasonal"))
                {
                    LoadBucketIfNeeded(stub, forceLoad: true);
                }
            }
            
            pluginLog.Info($"Pre-loaded {seasonalStubs.Count} seasonal content buckets");
        }

        /// <summary>
        /// Find all stubs with a specific CompletionStrategy
        /// </summary>
        private List<QuestData> FindAllStubsWithStrategy(QuestData root, string strategy)
        {
            var result = new List<QuestData>();

            if (root.CompletionStrategy == strategy && root.BucketPath != null)
            {
                result.Add(root);
            }

            foreach (var category in root.Categories)
            {
                result.AddRange(FindAllStubsWithStrategy(category, strategy));
            }

            return result;
        }

        /// <summary>
        /// Load quest data from embedded data.json resource
        /// </summary>
        private void LoadLegacyQuestData()
        {
            try
            {
                pluginLog.Debug("Loading QuestData from data.json");
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TimeMemoria.data.json");
if (stream == null)
{
    pluginLog.Error("Could not load embedded data.json resource");
    return;
}

using var stringStream = new StreamReader(stream);
var jsonString = stringStream.ReadToEnd();
var questData = JsonConvert.DeserializeObject<QuestData>(jsonString);
if (questData != null)
{
    plugin.QuestData = questData;
}


                pluginLog.Debug("Legacy quest data loaded successfully");
            }
            catch (Exception e)
            {
                pluginLog.Error("Error loading QuestData from data.json");
                pluginLog.Error(e.Message);
            }
        }

        /// <summary>
        /// Load bucket data on-demand if not already loaded or complete
        /// Returns true if data was loaded, false if skipped/cached
        /// </summary>
        public bool LoadBucketIfNeeded(QuestData stub, bool forceLoad = false)
        {
            if (stub?.BucketPath == null)
            {
                pluginLog.Debug("Stub has no BucketPath, skipping lazy-load");
                return false;
            }

            var bucketPath = stub.BucketPath;

            // Already loaded in this session?
            if (_loadedBuckets.ContainsKey(bucketPath))
            {
                _activeBucketPath = bucketPath;
                stub.EmptyMessage = null; // Clear message if data exists
                pluginLog.Debug($"Bucket already loaded: {bucketPath}");
                return false;
            }

            // Check if fully complete
            bool isFullyComplete = stub.CompletionStrategy switch
            {
                "SkipIfLastComplete" => CheckLastQuestComplete(stub),
                "SkipIfAllComplete" => CheckAllQuestsComplete(stub),
                _ => false
            };

            // Skip load if complete AND user is hiding completed quests (unless forced)
            if (!forceLoad && isFullyComplete && configuration.DisplayOption == 2) // 2 = Show Incomplete Only
            {
                stub.Total = stub.TotalQuests;
                stub.NumComplete = stub.TotalQuests;
                stub.Hide = false; // Keep visible so message shows
                stub.EmptyMessage = "All quests are complete.";
                
                configuration.CompletedBuckets[bucketPath] = true;
                configuration.Save();
                
                pluginLog.Info($"Bucket complete and hidden by filter, skipped load: {bucketPath}");
                return false;
            }

            // Clear any previous empty message
            stub.EmptyMessage = null;

            // Unload previous bucket if different
            if (_activeBucketPath != null && _activeBucketPath != bucketPath)
            {
                UnloadBucket(_activeBucketPath);
            }

            // Load from disk
            var quests = LoadBucketFromDisk(bucketPath);
            if (quests == null || quests.Count == 0)
            {
                pluginLog.Warning($"Failed to load bucket: {bucketPath}");
                stub.EmptyMessage = "Failed to load quest data. Check plugin logs.";
                return false;
            }

            _loadedBuckets[bucketPath] = quests;
            _activeBucketPath = bucketPath;

            // Populate all stubs that share this bucket
            PopulateAllStubsForBucket(bucketPath, quests);

            // Apply filtering logic to each populated stub
            var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);
            foreach (var populatedStub in stubs)
            {
                UpdateQuestData(populatedStub);
            }

            pluginLog.Info($"Loaded bucket: {bucketPath} ({quests.Count} quests total)");
            return true;
        }

        /// <summary>
        /// Load bucket file from disk and return quest array
        /// </summary>
        private List<Quest>? LoadBucketFromDisk(string bucketPath)
        {
            try
            {
                var pluginDir = pluginInterface.AssemblyLocation.DirectoryName!;
                // Go up three levels: Debug -> bin -> TimeMemoria -> repo root
                var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(pluginDir)!)!)!;
                var questsDir = Path.Combine(repoRoot, "Quests");
                
                // Parse bucketPath: "2.x/2.0/msq" -> "Quests/2.x/2.0/20-msq.json"
                var parts = bucketPath.Split('/');
                if (parts.Length != 3)
                {
                    pluginLog.Error($"Invalid BucketPath format: {bucketPath}");
                    return null;
                }

                var expansion = parts[0];  // "2.x"
                var patch = parts[1];      // "2.0"
                var category = parts[2];   // "msq" or "other"

                var fileName = $"{patch.Replace(".", "")}-{category}.json";
                var fullPath = Path.Combine(questsDir, expansion, patch, fileName);

                if (!File.Exists(fullPath))
                {
                    pluginLog.Warning($"Bucket file not found: {fullPath}");
                    return null;
                }

                var json = File.ReadAllText(fullPath);
                var quests = JsonConvert.DeserializeObject<List<Quest>>(json);

                return quests;
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, $"Failed to load bucket: {bucketPath}");
                return null;
            }
        }

        /// <summary>
        /// Populate all stubs that share the same BucketPath
        /// </summary>
        private void PopulateAllStubsForBucket(string bucketPath, List<Quest> quests)
        {
            var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);

            foreach (var stub in stubs)
            {
                // For MSQ/feature quests, assign all quests (filtering happens in UpdateQuestData)
                // For side quests, filter by Area first
                if (bucketPath.EndsWith("/msq") || bucketPath.EndsWith("/feature"))
                {
                    stub.Quests = new List<Quest>(quests); // Create copy to avoid mutation
                }
                else
                {
                    // Filter by Area for side quests
                    stub.Quests = quests.Where(q => q.Area == stub.Title).ToList();
                }
                
                stub.EmptyMessage = null;
                pluginLog.Debug($"Populated {stub.Quests.Count} quests for stub: {stub.Title} (before filtering)");
            }
        }

        /// <summary>
        /// Recursively find all stubs with matching BucketPath
        /// </summary>
        private List<QuestData> FindAllStubsWithBucketPath(QuestData root, string bucketPath)
        {
            var result = new List<QuestData>();

            if (root.BucketPath == bucketPath)
            {
                result.Add(root);
            }

            foreach (var category in root.Categories)
            {
                result.AddRange(FindAllStubsWithBucketPath(category, bucketPath));
            }

            return result;
        }

        /// <summary>
        /// Unload a bucket from memory (unless it's seasonal content)
        /// </summary>
        public void UnloadBucket(string bucketPath)
        {
            // Never unload seasonal content (always kept in memory)
            if (bucketPath.EndsWith("/seasonal"))
            {
                pluginLog.Debug($"Skipping unload for seasonal content: {bucketPath}");
                return;
            }

            if (_loadedBuckets.Remove(bucketPath))
            {
                // Clear quest lists from all stubs pointing to this bucket
                var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);
                foreach (var stub in stubs)
                {
                    stub.Quests = new List<Quest>();
                }

                pluginLog.Info($"Unloaded bucket: {bucketPath}");
            }

            if (_activeBucketPath == bucketPath)
            {
                _activeBucketPath = null;
            }
        }

        /// <summary>
        /// Unload currently active bucket
        /// </summary>
        public void UnloadActiveBucket()
        {
            if (_activeBucketPath != null)
            {
                UnloadBucket(_activeBucketPath);
            }
        }

        /// <summary>
        /// Check if last quest in bucket is complete (for linear content)
        /// </summary>
        private bool CheckLastQuestComplete(QuestData stub)
        {
            return stub.LastQuestId != 0 && QuestManager.IsQuestComplete(stub.LastQuestId);
        }

        /// <summary>
        /// Check if all quests in stub are complete (for non-linear content)
        /// </summary>
        private bool CheckAllQuestsComplete(QuestData stub)
        {
            if (stub.AllQuestIds == null || stub.AllQuestIds.Count == 0)
            {
                return false;
            }

            foreach (var questId in stub.AllQuestIds)
            {
                if (!QuestManager.IsQuestComplete(questId))
                {
                    return false;
                }
            }

            return true;
        }

        private void DetermineStartArea()
        {
            configuration.StartArea = QuestManager.IsQuestComplete(65575) ? "Gridania" :
                                      QuestManager.IsQuestComplete(65643) ? "Limsa Lominsa" :
                                      QuestManager.IsQuestComplete(66130) ? "Ul'dah" : "";
            
            pluginLog.Debug($"Start Area {configuration.StartArea}");
        }

        private void DetermineGrandCompany()
        {
            configuration.GrandCompany = QuestManager.IsQuestComplete(66216) ? "Twin Adder" :
                                         QuestManager.IsQuestComplete(66217) ? "Maelstrom" :
                                         QuestManager.IsQuestComplete(66218) ? "Immortal Flames" : "";
            
            pluginLog.Debug($"Grand Company {configuration.GrandCompany}");
        }

        private void DetermineStartClass()
        {
            configuration.StartClass = (uint) (
                QuestManager.IsQuestComplete(65792) && !QuestManager.IsQuestComplete(65822) ? 65822 :
                QuestManager.IsQuestComplete(66090) && !QuestManager.IsQuestComplete(66089) ? 66089 :
                QuestManager.IsQuestComplete(65849) && !QuestManager.IsQuestComplete(65848) ? 65848 :
                QuestManager.IsQuestComplete(65583) && !QuestManager.IsQuestComplete(65754) ? 65754 :
                QuestManager.IsQuestComplete(65582) && !QuestManager.IsQuestComplete(65755) ? 65755 :
                QuestManager.IsQuestComplete(65640) && !QuestManager.IsQuestComplete(65638) ? 65638 :
                QuestManager.IsQuestComplete(65584) && !QuestManager.IsQuestComplete(65747) ? 65747 :
                QuestManager.IsQuestComplete(65883) && !QuestManager.IsQuestComplete(65882) ? 65882 :
                QuestManager.IsQuestComplete(65991) && !QuestManager.IsQuestComplete(65990) ? 65990 : 0);
            
            pluginLog.Debug($"Start Class {configuration.StartClass}");
        }

        public void UpdateQuestData()
        {
            UpdateQuestData(plugin.QuestData);
        }
        
        private void UpdateQuestData(QuestData questData)
        {
            questData.NumComplete = questData.Total = 0;
            if (configuration.StartArea == "") DetermineStartArea();
            if (configuration.GrandCompany == "") DetermineGrandCompany();
            if (configuration.StartClass == 0) DetermineStartClass();

            if (questData.Categories.Count > 0)
            {
                questData.Hide = true;
                foreach (var category in questData.Categories)
                {
                    UpdateQuestData(category);
                    questData.NumComplete += category.NumComplete;
                    questData.Total += category.Total;
                    if (!category.Hide) questData.Hide = false;
                }
            }
            else
            {
                // Don't hide stubs that need lazy-loading
                if (!string.IsNullOrEmpty(questData.BucketPath) && questData.Quests.Count == 0)
                {
                    questData.Hide = false; // Keep stub visible for lazy-load
                    questData.Total = questData.TotalQuests;
                    
                    // Check if bucket is cached as complete
                    if (configuration.CompletedBuckets.ContainsKey(questData.BucketPath))
                    {
                        questData.NumComplete = questData.TotalQuests;
                    }
                    else
                    {
                        questData.NumComplete = 0; // Will be calculated after load
                    }
                    
                    return; // Don't process empty quest list
                }
                
                questData.Hide = true;
                foreach (var quest in questData.Quests.ToList())
                {
                    if (!configuration.StartArea.IsNullOrEmpty() && !quest.Start.IsNullOrEmpty() && configuration.StartArea != quest.Start)
                    {
                        if (IsQuestComplete(quest))
                        {
                            pluginLog.Error($"Quest {quest.Title} {string.Join(" ", quest.Id)} is restricted but completed");
                        }

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (!configuration.GrandCompany.IsNullOrEmpty() && !quest.Gc.IsNullOrEmpty() && configuration.GrandCompany != quest.Gc)
                    {
                        if (IsQuestComplete(quest))
                        {
                            pluginLog.Error($"Quest {quest.Title} {string.Join(" ", quest.Id)} is restricted but completed");
                        }

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (configuration.StartClass != 0 && quest.Id.Contains(configuration.StartClass))
                    {
                        questData.Quests.Remove(quest);
                        continue;
                    }
                    
                    if ((QuestManager.IsQuestComplete(67001) && (quest.Id.Contains(67002) || quest.Id.Contains(67003))) ||
                        (QuestManager.IsQuestComplete(67002) && (quest.Id.Contains(67001) || quest.Id.Contains(67003))) ||
                        (QuestManager.IsQuestComplete(67003) && (quest.Id.Contains(67001) || quest.Id.Contains(67002))) ||
                        (QuestManager.IsQuestComplete(69256) && quest.Id.Contains(69257)) || 
                        (QuestManager.IsQuestComplete(69257) && quest.Id.Contains(69256)) || 
                        (QuestManager.IsQuestComplete(69336) && quest.Id.Contains(69337)) || 
                        (QuestManager.IsQuestComplete(69337) && quest.Id.Contains(69336)) || 
                        (QuestManager.IsQuestComplete(69338) && quest.Id.Contains(69339)) || 
                        (QuestManager.IsQuestComplete(69339) && quest.Id.Contains(69338)) || 
                        (QuestManager.IsQuestComplete(69340) && quest.Id.Contains(69341)) || 
                        (QuestManager.IsQuestComplete(69341) && quest.Id.Contains(69340))) 
                    {
                        questData.Quests.Remove(quest);
                    }

                    if (IsQuestComplete(quest)) questData.NumComplete++;

                    quest.Hide = (configuration.DisplayOption == 1 && !IsQuestComplete(quest)) ||
                                 (configuration.DisplayOption == 2 && IsQuestComplete(quest));
                    if (!quest.Hide) questData.Hide = false;
                }

                questData.Total += questData.Quests.Count;
            }
        }

        public static bool IsQuestComplete(Quest quest)
        {
            foreach (var id in quest.Id)
            {
                if (QuestManager.IsQuestComplete(id)) return true;   
            }
            return false;
        }
    }
}
