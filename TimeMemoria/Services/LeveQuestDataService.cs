using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using TimeMemoria;

namespace TimeMemoria.Services
{
    /// <summary>
    /// Loads levequest data directly from game client (Lumina Excel).
    /// Provides accurate quest IDs and completion status from authoritative game source.
    /// </summary>
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

                // Get city name from bucket path
                var cityName = ExtractCityName(bucketPath);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Filter levequests by town and availability
                var townLeves = leveSheet
                    .Where(leve => leve.LeveClient.RowId != 0 &&
                                   (townId == 0 || leve.Town.RowId == townId))
                    .ToList();

                if (townLeves.Count == 0)
                    return null;

                // Group by NPC/Issuer - use quest name's NPC context from game data
                var root = new QuestData
                {
                    Title = cityName,
                    Categories = new(),
                    BucketPath = bucketPath
                };

                // For now, create a flat structure grouped by level ranges
                // This avoids the NPC matching problem and still organizes quests
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

                    // Calculate completion for this level group
                    levelCategory.Total = levelCategory.Quests.Count;
                    levelCategory.NumComplete = levelCategory.Quests.Count(q => IsLevequestComplete(q));
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

        private unsafe bool IsLevequestComplete(Quest quest)
        {
            try
            {
                foreach (var id in quest.Id)
                {
                    if (QuestManager.Instance()->IsLevequestComplete((ushort)id))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
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
                { "Levequests/ARR/arr-limsa-lominsa", 2 },
                { "Levequests/ARR/arr-gridania", 3 },
                { "Levequests/ARR/arr-uldah", 1 },
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
