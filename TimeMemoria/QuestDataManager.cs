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
using TimeMemoria.Services;

namespace TimeMemoria
{
    class QuestDataManager
    {
        public IPluginLog pluginLog { get; private set; }

        private readonly Plugin plugin;
        private readonly Configuration configuration;
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly PlaytimeStatsService playtimeStatsService;

        private readonly Dictionary<string, List<Quest>> _loadedBuckets = new();
        private string? _activeBucketPath = null;
        private int _previousTotalComplete = 0;
        private bool _initialized = false;

        public QuestDataManager(
            IDalamudPluginInterface pluginInterface,
            IPluginLog pluginLog,
            Plugin plugin,
            Configuration configuration,
            PlaytimeStatsService playtimeStatsService)
        {
            this.pluginInterface = pluginInterface;
            this.pluginLog = pluginLog;
            this.plugin = plugin;
            this.configuration = configuration;
            this.playtimeStatsService = playtimeStatsService;

            LoadLegacyQuestData();
            PreloadSeasonalContent();
        }

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

        private List<QuestData> FindAllStubsWithStrategy(QuestData root, string strategy)
        {
            var result = new List<QuestData>();

            if (root.CompletionStrategy == strategy && root.BucketPath != null)
                result.Add(root);

            foreach (var category in root.Categories)
                result.AddRange(FindAllStubsWithStrategy(category, strategy));

            return result;
        }

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
                    plugin.QuestData = questData;

                pluginLog.Debug("Legacy quest data loaded successfully");
            }
            catch (Exception e)
            {
                pluginLog.Error("Error loading QuestData from data.json");
                pluginLog.Error(e.Message);
            }
        }

        public bool LoadBucketIfNeeded(QuestData stub, bool forceLoad = false)
        {
            if (stub?.BucketPath == null)
            {
                pluginLog.Debug("Stub has no BucketPath, skipping lazy-load");
                return false;
            }

            var bucketPath = stub.BucketPath;

            if (_loadedBuckets.ContainsKey(bucketPath))
            {
                _activeBucketPath = bucketPath;
                stub.EmptyMessage = null;
                pluginLog.Debug($"Bucket already loaded: {bucketPath}");
                return false;
            }

            bool isFullyComplete = stub.CompletionStrategy switch
            {
                "SkipIfLastComplete" => CheckLastQuestComplete(stub),
                "SkipIfAllComplete" => CheckAllQuestsComplete(stub),
                _ => false
            };

            if (!forceLoad && isFullyComplete && configuration.DisplayOption == 2)
            {
                stub.Total = stub.TotalQuests;
                stub.NumComplete = stub.TotalQuests;
                stub.Hide = false;
                stub.EmptyMessage = "All quests are complete.";

                configuration.CompletedBuckets[bucketPath] = true;
                configuration.Save();

                pluginLog.Info($"Bucket complete and hidden by filter, skipped load: {bucketPath}");
                return false;
            }

            stub.EmptyMessage = null;

            if (_activeBucketPath != null && _activeBucketPath != bucketPath)
                UnloadBucket(_activeBucketPath);

            var quests = LoadBucketFromDisk(bucketPath);
            if (quests == null || quests.Count == 0)
            {
                pluginLog.Warning($"Failed to load bucket: {bucketPath}");
                stub.EmptyMessage = "Failed to load quest data. Check plugin logs.";
                return false;
            }

            _loadedBuckets[bucketPath] = quests;
            _activeBucketPath = bucketPath;

            PopulateAllStubsForBucket(bucketPath, quests);

            var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);
            foreach (var populatedStub in stubs)
                UpdateQuestData(populatedStub);

            pluginLog.Info($"Loaded bucket: {bucketPath} ({quests.Count} quests total)");
            return true;
        }

        private List<Quest>? LoadBucketFromDisk(string bucketPath)
        {
            try
            {
                var pluginDir = pluginInterface.AssemblyLocation.DirectoryName!;
                var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(pluginDir)!)!)!;
                var questsDir = Path.Combine(repoRoot, "Quests");

                var parts = bucketPath.Split('/');
                if (parts.Length != 3)
                {
                    pluginLog.Error($"Invalid BucketPath format: {bucketPath}");
                    return null;
                }

                var expansion = parts[0];
                var patch = parts[1];
                var category = parts[2];

                var fileName = $"{patch.Replace(".", "")}-{category}.json";
                var fullPath = Path.Combine(questsDir, expansion, patch, fileName);

                if (!File.Exists(fullPath))
                {
                    pluginLog.Warning($"Bucket file not found: {fullPath}");
                    return null;
                }

                var json = File.ReadAllText(fullPath);
                return JsonConvert.DeserializeObject<List<Quest>>(json);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, $"Failed to load bucket: {bucketPath}");
                return null;
            }
        }

        private void PopulateAllStubsForBucket(string bucketPath, List<Quest> quests)
        {
            var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);

            foreach (var stub in stubs)
            {
                if (bucketPath.EndsWith("/msq") || bucketPath.EndsWith("/feature"))
                    stub.Quests = new List<Quest>(quests);
                else
                    stub.Quests = quests.Where(q => q.Area == stub.Title).ToList();

                stub.EmptyMessage = null;
                pluginLog.Debug($"Populated {stub.Quests.Count} quests for stub: {stub.Title} (before filtering)");
            }
        }

        private List<QuestData> FindAllStubsWithBucketPath(QuestData root, string bucketPath)
        {
            var result = new List<QuestData>();

            if (root.BucketPath == bucketPath)
                result.Add(root);

            foreach (var category in root.Categories)
                result.AddRange(FindAllStubsWithBucketPath(category, bucketPath));

            return result;
        }

        public void UnloadBucket(string bucketPath)
        {
            if (bucketPath.EndsWith("/seasonal"))
            {
                pluginLog.Debug($"Skipping unload for seasonal content: {bucketPath}");
                return;
            }

            if (_loadedBuckets.Remove(bucketPath))
            {
                var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);
                foreach (var stub in stubs)
                    stub.Quests = new List<Quest>();

                pluginLog.Info($"Unloaded bucket: {bucketPath}");
            }

            if (_activeBucketPath == bucketPath)
                _activeBucketPath = null;
        }

        public void UnloadActiveBucket()
        {
            if (_activeBucketPath != null)
                UnloadBucket(_activeBucketPath);
        }

        private bool CheckLastQuestComplete(QuestData stub)
        {
            return stub.LastQuestId != 0 && QuestManager.IsQuestComplete(stub.LastQuestId);
        }

        private bool CheckAllQuestsComplete(QuestData stub)
        {
            if (stub.AllQuestIds == null || stub.AllQuestIds.Count == 0)
                return false;

            foreach (var questId in stub.AllQuestIds)
            {
                if (!QuestManager.IsQuestComplete(questId))
                    return false;
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
            configuration.StartClass = (uint)(
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

            if (!_initialized)
                playtimeStatsService.SeedLifetimeQuestCount((int)plugin.QuestData.NumComplete);

            DetectNewCompletions();
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
                if (!string.IsNullOrEmpty(questData.BucketPath) && questData.Quests.Count == 0)
                {
                    questData.Hide = false;
                    questData.Total = questData.TotalQuests;

                    if (configuration.CompletedBuckets.ContainsKey(questData.BucketPath))
                        questData.NumComplete = questData.TotalQuests;
                    else
                        questData.NumComplete = 0;

                    return;
                }

                questData.Hide = true;
                foreach (var quest in questData.Quests.ToList())
                {
                    if (!configuration.StartArea.IsNullOrEmpty() && !quest.Start.IsNullOrEmpty() && configuration.StartArea != quest.Start)
                    {
                        if (IsQuestComplete(quest))
                            pluginLog.Error($"Quest {quest.Title} {string.Join(" ", quest.Id)} is restricted but completed");

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (!configuration.GrandCompany.IsNullOrEmpty() && !quest.Gc.IsNullOrEmpty() && configuration.GrandCompany != quest.Gc)
                    {
                        if (IsQuestComplete(quest))
                            pluginLog.Error($"Quest {quest.Title} {string.Join(" ", quest.Id)} is restricted but completed");

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

        private void DetectNewCompletions()
        {
            var currentTotal = (int)plugin.QuestData.NumComplete;

            if (!_initialized)
            {
                _previousTotalComplete = currentTotal;
                _initialized = true;
                return;
            }

            var newCompletions = currentTotal - _previousTotalComplete;
            if (newCompletions > 0)
            {
                for (int i = 0; i < newCompletions; i++)
                    playtimeStatsService.IncrementQuestCompletion();

                pluginLog.Debug($"[DetectNewCompletions] {newCompletions} new quest(s) detected.");
                _previousTotalComplete = currentTotal;
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
