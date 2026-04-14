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
using Lumina.Excel.Sheets;


namespace TimeMemoria
{
    class QuestDataManager
    {
        public IPluginLog pluginLog { get; private set; }

        private readonly Plugin plugin;
        private readonly Configuration configuration;
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly PlaytimeStatsService playtimeStatsService;
        private readonly IDataManager dataManager;
        private readonly LeveQuestDataService leveQuestDataService;
        private readonly Dictionary<string, uint> townMapping;

        private readonly Dictionary<string, List<Quest>> _loadedBuckets = new();
        private string? _activeBucketPath = null;
        private int _previousTotalComplete = 0;
        private bool _initialized = false;

        private readonly Dictionary<string, List<Quest>> _directBucketCache = new();
        private bool _isPreloadingMode = false;


        public QuestDataManager(
            IDalamudPluginInterface pluginInterface,
            IPluginLog pluginLog,
            Plugin plugin,
            Configuration configuration,
            PlaytimeStatsService playtimeStatsService,
            IDataManager dataManager)
        {
            this.pluginInterface = pluginInterface;
            this.pluginLog = pluginLog;
            this.plugin = plugin;
            this.configuration = configuration;
            this.playtimeStatsService = playtimeStatsService;
            this.dataManager = dataManager;
            this.leveQuestDataService = new LeveQuestDataService(dataManager, pluginLog);
            this.townMapping = leveQuestDataService.GetTownMapping();

            LoadLegacyQuestData();
            AddLevequestStubs();
            PreloadSeasonalContent();
            PreloadAllLevequests();
        }

        private void CorrectLeveQuestIds()
        {
            try
            {
                var pluginDir = pluginInterface.AssemblyLocation.DirectoryName!;
                var questsDir = System.IO.Path.Combine(pluginDir, "Quests");
                var correctionService = new LeveQuestIdCorrectionService(dataManager, pluginLog);
                correctionService.CorrectAllLeveQuestIds(questsDir);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "Failed to correct levequest IDs");
            }
        }


        private void PreloadSeasonalContent()
        {
            var seasonalStubs = FindAllStubsWithStrategy(plugin.QuestData, "AlwaysLoad");

            foreach (var stub in seasonalStubs)
            {
                if (stub.BucketPath != null && stub.BucketPath.EndsWith("/seasonal"))
                    LoadBucketIfNeeded(stub, forceLoad: true);
            }

            pluginLog.Info($"Pre-loaded {seasonalStubs.Count} seasonal content buckets");
        }

        private void PreloadAllLevequests()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Find all levequest stubs
            var allLevequestStubs = FindAllLevequestStubs(plugin.QuestData);

            // Enable preload mode to prevent unloading buckets as we load them
            _isPreloadingMode = true;

            foreach (var stub in allLevequestStubs)
            {
                if (stub.BucketPath != null && stub.Categories.Count == 0 && stub.Quests.Count == 0)
                {
                    LoadBucketIfNeeded(stub, forceLoad: false);
                }
            }

            _isPreloadingMode = false;

            sw.Stop();
            pluginLog.Info($"Pre-loaded all {allLevequestStubs.Count} levequest buckets in {sw.ElapsedMilliseconds}ms");
        }

        private List<QuestData> FindAllLevequestStubs(QuestData root)
        {
            var result = new List<QuestData>();

            if (root.BucketPath?.StartsWith("Levequests/") ?? false)
                result.Add(root);

            foreach (var category in root.Categories)
                result.AddRange(FindAllLevequestStubs(category));

            return result;
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
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("TimeMemoria.data.json");

                if (stream == null)
                {
                    pluginLog.Error("Could not load embedded data.json resource");
                    return;
                }

                using var stringStream = new StreamReader(stream);
                var jsonString = stringStream.ReadToEnd();
                var questData  = JsonConvert.DeserializeObject<QuestData>(jsonString);
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

        private void AddLevequestStubs()
        {
            try
            {
                // Create Levequests root category
                var levequestsRoot = new QuestData
                {
                    Title = "Levequests",
                    Categories = new List<QuestData>()
                };

                // A Realm Reborn - split by region
                var arrRoot = new QuestData
                {
                    Title = "A Realm Reborn",
                    Categories = new List<QuestData>()
                };

                var arrRegions = new[]
                {
                    ("Limsa Lominsa", "Levequests/ARR/arr-limsa-lominsa", 240),
                    ("Gridania", "Levequests/ARR/arr-gridania", 258),
                    ("Ul'dah", "Levequests/ARR/arr-uldah", 191),
                    ("Coerthas", "Levequests/ARR/arr-coerthas", 104),
                    ("Mor Dhona", "Levequests/ARR/arr-mordhona", 51)
                };

                foreach (var (region, path, questCount) in arrRegions)
                {
                    arrRoot.Categories.Add(new QuestData
                    {
                        Title = region,
                        BucketPath = path,
                        CompletionStrategy = "SkipIfAllComplete",
                        TotalQuests = questCount
                    });
                }

                levequestsRoot.Categories.Add(arrRoot);

                // Post-ARR expansions - single NPC per expansion
                var expansions = new[]
                {
                    ("Heavensward", "Levequests/hw-levequests", 360),
                    ("Stormblood", "Levequests/sb-levequests", 165),
                    ("Shadowbringers", "Levequests/shb-levequests", 165),
                    ("Endwalker", "Levequests/ew-levequests", 120),
                    ("Dawntrail", "Levequests/dt-levequests", 120)
                };

                foreach (var (expansion, path, questCount) in expansions)
                {
                    levequestsRoot.Categories.Add(new QuestData
                    {
                        Title = expansion,
                        BucketPath = path,
                        CompletionStrategy = "SkipIfAllComplete",
                        TotalQuests = questCount
                    });
                }

                // Add Levequests as top-level category
                plugin.QuestData.Categories.Add(levequestsRoot);
                pluginLog.Info("Levequest stubs added successfully");
            }
            catch (Exception e)
            {
                pluginLog.Error($"Error adding levequest stubs: {e.Message}");
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
            var isLevequestBucket = bucketPath.StartsWith("Levequests/");

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
                "SkipIfAllComplete"  => CheckAllQuestsComplete(stub),
                _                    => false
            };

            if (!forceLoad && isFullyComplete && configuration.DisplayOption == 2)
            {
                stub.Total        = stub.TotalQuests;
                stub.NumComplete  = stub.TotalQuests;
                stub.Hide         = false;
                stub.EmptyMessage = "All quests are complete.";

                configuration.CompletedBuckets[bucketPath] = true;
                configuration.Save();

                pluginLog.Info($"Bucket complete and hidden by filter, skipped load: {bucketPath}");
                return false;
            }

            stub.EmptyMessage = null;

            // Unload previous bucket when loading a new one
            // Skip unload during preload mode to keep all buckets loaded
            if (!_isPreloadingMode && _activeBucketPath != null && _activeBucketPath != bucketPath)
                UnloadBucket(_activeBucketPath);

            if (isLevequestBucket)
            {
                // Load levequest bucket as QuestData (preserves NPC structure)
                // Try game data first, fall back to JSON
                var levequestData = LoadLevequestBucketFromGameData(bucketPath);
                if (levequestData == null)
                {
                    levequestData = LoadLevequestBucketFromDisk(bucketPath);
                }

                if (levequestData == null)
                {
                    pluginLog.Warning($"Failed to load levequest bucket: {bucketPath}");
                    stub.EmptyMessage = "Failed to load quest data. Check plugin logs.";
                    return false;
                }

                // Merge levequest data into stub
                stub.Categories = levequestData.Categories;
                stub.TotalQuests = CountAllQuests(levequestData);

                // Store a marker in _loadedBuckets to track that this bucket is loaded
                _loadedBuckets[bucketPath] = new List<Quest>();
                _activeBucketPath = bucketPath;

                var stubs = FindAllStubsWithBucketPath(plugin.QuestData, bucketPath);
                foreach (var populatedStub in stubs)
                {
                    UpdateQuestData(populatedStub);
                    // Cache completion counts recursively so they persist when buckets are unloaded
                    CacheCompletionData(populatedStub);
                }

                pluginLog.Info($"Loaded levequest bucket: {bucketPath} ({stub.TotalQuests} quests total)");
            }
            else
            {
                // Load regular quest bucket as List<Quest>
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
            }

            return true;
        }


        /// <summary>
        /// Loads a bucket directly by path, applies starting city / grand company /
        /// start class filters, and returns the filtered quest list.
        /// Used by the new Quests tab UI. Results are cached for the session.
        /// Returns an empty list if the file does not exist or fails to load.
        /// </summary>
        public List<Quest> LoadBucketDirect(string bucketPath)
        {
            if (_directBucketCache.TryGetValue(bucketPath, out var cached))
                return cached;

            var raw = LoadBucketFromDisk(bucketPath);
            if (raw == null || raw.Count == 0)
            {
                pluginLog.Debug($"[LoadBucketDirect] No quests found for path: {bucketPath}");
                _directBucketCache[bucketPath] = new List<Quest>();
                return _directBucketCache[bucketPath];
            }

            var filtered = new List<Quest>();
            foreach (var quest in raw)
            {
                // ARR starting city filter — ID-based, no error logging
                if (!configuration.StartArea.IsNullOrEmpty() &&
                    ArrStartingPaths.IsExcludedForStartArea(quest.Id, configuration.StartArea))
                    continue;

                // Grand company filter
                if (!configuration.GrandCompany.IsNullOrEmpty() &&
                    !quest.Gc.IsNullOrEmpty() &&
                    quest.Gc != configuration.GrandCompany)
                    continue;

                // Start class filter
                if (configuration.StartClass != 0 &&
                    quest.Id.Contains(configuration.StartClass))
                    continue;

                // Branching quest mutual exclusion
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
                    continue;

                filtered.Add(quest);
            }

            _directBucketCache[bucketPath] = filtered;
            pluginLog.Debug(
                $"[LoadBucketDirect] Loaded {filtered.Count}/{raw.Count} quests for: {bucketPath}");
            return filtered;
        }


        /// <summary>
        /// Clears the direct bucket cache. Call this when DisplayOption changes
        /// so that filters are recalculated on next render.
        /// </summary>
        public void InvalidateDirectBucketCache()
        {
            _directBucketCache.Clear();
            pluginLog.Debug("[InvalidateDirectBucketCache] Direct bucket cache cleared.");
        }


        private List<Quest>? LoadBucketFromDisk(string bucketPath)
        {
            try
            {
                var pluginDir = pluginInterface.AssemblyLocation.DirectoryName!;
                var questsDir = Path.Combine(pluginDir, "Quests");

                var parts = bucketPath.Split('/');
                if (parts.Length != 3)
                {
                    pluginLog.Error($"Invalid BucketPath format: {bucketPath}");
                    return null;
                }

                var expansion = parts[0];
                var patch     = parts[1];
                var category  = parts[2];

                var fileName = $"{patch.Replace(".", "")}-{category}.json";
                var fullPath = Path.Combine(questsDir, expansion, patch, fileName);

                if (!File.Exists(fullPath))
                {
                    pluginLog.Warning($"Bucket file not found: {fullPath}");
                    return null;
                }

                var json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
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
                pluginLog.Debug(
                    $"Populated {stub.Quests.Count} quests for stub: {stub.Title} (before filtering)");
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
            return stub.LastQuestId != 0 &&
                   QuestManager.IsQuestComplete(stub.LastQuestId);
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
            configuration.StartArea =
                QuestManager.IsQuestComplete(65575) ? "Gridania"      :
                QuestManager.IsQuestComplete(65643) ? "Limsa Lominsa" :
                QuestManager.IsQuestComplete(66130) ? "Ul'dah"        : "";

            pluginLog.Debug($"Start Area {configuration.StartArea}");
        }


        private void DetermineGrandCompany()
        {
            configuration.GrandCompany =
                QuestManager.IsQuestComplete(66216) ? "Twin Adder"      :
                QuestManager.IsQuestComplete(66217) ? "Maelstrom"       :
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
            if (configuration.StartArea    == "") DetermineStartArea();
            if (configuration.GrandCompany == "") DetermineGrandCompany();
            if (configuration.StartClass   == 0)  DetermineStartClass();

            if (questData.Categories.Count > 0)
            {
                questData.Hide = true;
                foreach (var category in questData.Categories)
                {
                    UpdateQuestData(category);
                    questData.NumComplete += category.NumComplete;
                    questData.Total       += category.Total;
                    if (!category.Hide) questData.Hide = false;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(questData.BucketPath) && questData.Quests.Count == 0)
                {
                    questData.Hide  = false;
                    questData.Total = questData.TotalQuests;

                    // Use cached completion count if available, otherwise check config
                    if (questData.CachedNumComplete >= 0)
                    {
                        questData.NumComplete = questData.CachedNumComplete;
                    }
                    else if (configuration.CompletedBuckets.ContainsKey(questData.BucketPath))
                    {
                        questData.NumComplete = questData.TotalQuests;
                        questData.CachedNumComplete = (int)questData.TotalQuests;
                    }
                    else
                    {
                        questData.NumComplete = 0;
                    }

                    return;
                }

                questData.Hide = true;
                foreach (var quest in questData.Quests.ToList())
                {
                    if (!configuration.StartArea.IsNullOrEmpty() &&
                        !quest.Start.IsNullOrEmpty() &&
                        configuration.StartArea != quest.Start)
                    {
                        if (IsQuestComplete(quest))
                            pluginLog.Error(
                                $"Quest {quest.Title} {string.Join(" ", quest.Id)} " +
                                $"is restricted but completed");

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (!configuration.GrandCompany.IsNullOrEmpty() &&
                        !quest.Gc.IsNullOrEmpty() &&
                        configuration.GrandCompany != quest.Gc)
                    {
                        if (IsQuestComplete(quest))
                            pluginLog.Error(
                                $"Quest {quest.Title} {string.Join(" ", quest.Id)} " +
                                $"is restricted but completed");

                        questData.Quests.Remove(quest);
                        continue;
                    }

                    if (configuration.StartClass != 0 &&
                        quest.Id.Contains(configuration.StartClass))
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

                    quest.Hide =
                        (configuration.DisplayOption == 1 && !IsQuestComplete(quest)) ||
                        (configuration.DisplayOption == 2 &&  IsQuestComplete(quest));

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

                pluginLog.Debug(
                    $"[DetectNewCompletions] {newCompletions} new quest(s) detected.");
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

        // Load levequests from game client (pure game data, no JSON IDs)
        private QuestData? LoadLevequestBucketFromGameData(string bucketPath)
        {
            if (!townMapping.TryGetValue(bucketPath, out var townId))
                return null;

            return leveQuestDataService.LoadLeveQuestBucket(bucketPath, townId);
        }

        private QuestData? LoadLevequestBucketFromDisk(string bucketPath)
        {
            try
            {
                var pluginDir = pluginInterface.AssemblyLocation.DirectoryName!;
                var questsDir = Path.Combine(pluginDir, "Quests");

                // Levequest paths: Levequests/ARR/arr-limsa-lominsa or Levequests/hw-levequests
                var fullPath = Path.Combine(questsDir, bucketPath.Replace('/', Path.DirectorySeparatorChar) + ".json");

                if (!File.Exists(fullPath))
                {
                    pluginLog.Warning($"Levequest file not found: {fullPath}");
                    return null;
                }

                var json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                return JsonConvert.DeserializeObject<QuestData>(json);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, $"Failed to load levequest bucket: {bucketPath}");
                return null;
            }
        }

        private int CountAllQuests(QuestData data)
        {
            int count = data.Quests.Count;
            foreach (var category in data.Categories)
                count += CountAllQuests(category);
            return count;
        }

        private void CacheCompletionData(QuestData data)
        {
            // Cache this node's completion count
            data.CachedNumComplete = (int)data.NumComplete;

            // Recursively cache all categories
            foreach (var category in data.Categories)
                CacheCompletionData(category);
        }
    }
}
