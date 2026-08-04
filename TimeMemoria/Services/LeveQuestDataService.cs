using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using TimeMemoria;

namespace TimeMemoria.Services
{
    public class LeveQuestDataService
    {
        private readonly IDataManager dataManager;
        private readonly IPluginLog pluginLog;

        public LeveQuestDataService(IDataManager dataManager, IPluginLog pluginLog)
        {
            this.dataManager = dataManager;
            this.pluginLog = pluginLog;
        }

        public QuestData? LoadLeveQuestBucket(string bucketPath, uint townId)
        {
            try
            {
                var leveSheet = dataManager.GetExcelSheet<Leve>();
                if (leveSheet == null)
                    return null;

                var cityName = ExtractCityName(bucketPath);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Load levequests: filter by town ID for ARR, by level range for expansions
                List<Leve> townLeves;

                if (bucketPath.StartsWith("Levequests/ARR/"))
                {
                    // ARR: filter by town ID
                    townLeves = leveSheet
                        .Where(leve => leve.Town.RowId == townId)
                        .OrderBy(leve => leve.ClassJobLevel)
                        .ToList();
                }
                else
                {
                    // Expansions: filter by level range
                    var (minLevel, maxLevel) = GetLevelRangeForExpansion(bucketPath);
                    townLeves = leveSheet
                        .Where(leve => leve.ClassJobLevel >= minLevel && leve.ClassJobLevel <= maxLevel)
                        .OrderBy(leve => leve.ClassJobLevel)
                        .ToList();
                }

                if (townLeves.Count == 0)
                    return null;

                var root = new QuestData
                {
                    Title = cityName,
                    Categories = new(),
                    BucketPath = bucketPath
                };

                // Group by level range
                var levesByLevel = townLeves
                    .GroupBy(leve => (leve.ClassJobLevel / 10) * 10)
                    .OrderBy(g => g.Key)
                    .ToList();

                int totalQuests = 0;
                foreach (var levelGroup in levesByLevel)
                {
                    var levelCategory = new QuestData
                    {
                        Title = $"Level {levelGroup.Key}-{levelGroup.Key + 9}",
                        Quests = new(),
                        Categories = new()
                    };

                    foreach (var leve in levelGroup)
                    {
                        var quest = new Quest
                        {
                            Title = leve.Name.ToString(),
                            Id = new() { leve.RowId },
                            Area = cityName,
                            Level = (int)leve.ClassJobLevel,
                            Start = "",
                            Gc = "",
                            Chain = null
                        };

                        levelCategory.Quests.Add(quest);
                        totalQuests++;
                    }

                    levelCategory.Total = levelCategory.Quests.Count;
                    levelCategory.NumComplete = levelCategory.Quests.Count(q => IsLevequestComplete(q.Id[0]));
                    root.Categories.Add(levelCategory);
                }

                root.Total = totalQuests;
                root.NumComplete = root.Categories.Sum(c => c.NumComplete);

                sw.Stop();
                pluginLog.Info($"Loaded {cityName} from game data in {sw.ElapsedMilliseconds}ms ({totalQuests} quests, {root.NumComplete} complete)");
                return root;
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, $"Failed to load levequest bucket: {bucketPath}");
                return null;
            }
        }

        private unsafe bool IsLevequestComplete(uint leveId)
        {
            try
            {
                return QuestManager.Instance()->IsLevequestComplete((ushort)leveId);
            }
            catch
            {
                return false;
            }
        }

        private (uint minLevel, uint maxLevel) GetLevelRangeForExpansion(string bucketPath)
        {
            return bucketPath switch
            {
                "Levequests/hw-levequests" => (50, 59),
                "Levequests/sb-levequests" => (60, 69),
                "Levequests/shb-levequests" => (70, 79),
                "Levequests/ew-levequests" => (80, 89),
                "Levequests/dt-levequests" => (90, 99),
                _ => (0, 255)
            };
        }

        private string ExtractCityName(string bucketPath)
        {
            return bucketPath switch
            {
                "Levequests/ARR/arr-limsa-lominsa" => "Limsa Lominsa",
                "Levequests/ARR/arr-gridania" => "Gridania",
                "Levequests/ARR/arr-uldah" => "Ul'dah",
                "Levequests/ARR/arr-coerthas" => "Coerthas",
                "Levequests/ARR/arr-mordhona" => "Mor Dhona",
                "Levequests/hw-levequests" => "Heavensward",
                "Levequests/sb-levequests" => "Stormblood",
                "Levequests/shb-levequests" => "Shadowbringers",
                "Levequests/ew-levequests" => "Endwalker",
                "Levequests/dt-levequests" => "Dawntrail",
                _ => "Levequests"
            };
        }

        public Dictionary<string, uint> GetTownMapping()
        {
            return new()
            {
                { "Levequests/ARR/arr-limsa-lominsa", 1 },
                { "Levequests/ARR/arr-gridania", 2 },
                { "Levequests/ARR/arr-uldah", 3 },
                { "Levequests/ARR/arr-coerthas", 4 },
                { "Levequests/ARR/arr-mordhona", 5 },
                { "Levequests/hw-levequests", 0 },
                { "Levequests/sb-levequests", 0 },
                { "Levequests/shb-levequests", 0 },
                { "Levequests/ew-levequests", 0 },
                { "Levequests/dt-levequests", 0 }
            };
        }
    }
}
