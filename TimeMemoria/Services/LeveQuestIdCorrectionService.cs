using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TimeMemoria.Services
{
    /// <summary>
    /// Corrects levequest JSON files with accurate quest IDs from the game client.
    /// This ensures completion tracking uses the correct quest IDs.
    /// </summary>
    public class LeveQuestIdCorrectionService
    {
        private readonly IDataManager dataManager;
        private readonly IPluginLog pluginLog;

        public LeveQuestIdCorrectionService(IDataManager dataManager, IPluginLog pluginLog)
        {
            this.dataManager = dataManager;
            this.pluginLog = pluginLog;
        }

        public void CorrectAllLeveQuestIds(string questsDirectory)
        {
            try
            {
                pluginLog.Info("Starting levequest ID correction from game data...");

                var leveSheet = dataManager.GetExcelSheet<Leve>();
                if (leveSheet == null)
                {
                    pluginLog.Error("Failed to load Leve sheet from game data");
                    return;
                }

                // Build a map of quest name -> Leve RowId for quick lookup
                var leveMap = leveSheet
                    .Where(leve => leve.LeveClient.RowId != 0)
                    .GroupBy(leve => leve.Name.ToString())
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().RowId);

                pluginLog.Info($"Loaded {leveMap.Count} levequests from game data");

                // Find all JSON files
                var jsonFiles = Directory.GetFiles(questsDirectory, "*.json", SearchOption.AllDirectories)
                    .Where(f => f.Contains("Levequests"))
                    .ToList();

                int totalQuestsUpdated = 0;
                int filesUpdated = 0;

                foreach (var jsonFile in jsonFiles)
                {
                    var (updated, count) = CorrectJsonFile(jsonFile, leveMap);
                    if (updated)
                    {
                        filesUpdated++;
                        totalQuestsUpdated += count;
                        pluginLog.Info($"Corrected {count} quests in {Path.GetFileName(jsonFile)}");
                    }
                }

                pluginLog.Info($"Correction complete: {filesUpdated} files updated, {totalQuestsUpdated} quests corrected");
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "Failed to correct levequest IDs");
            }
        }

        private (bool Updated, int Count) CorrectJsonFile(string filePath, Dictionary<string, uint> leveMap)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var root = JObject.Parse(json);
                int corrected = 0;

                CorrectCategoryIds(root["Categories"] as JArray, leveMap, ref corrected);

                if (corrected > 0)
                {
                    var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Include
                    };
                    var correctedJson = JsonConvert.SerializeObject(root, settings);
                    File.WriteAllText(filePath, correctedJson);
                    return (true, corrected);
                }

                return (false, 0);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, $"Failed to correct JSON file: {filePath}");
                return (false, 0);
            }
        }

        private void CorrectCategoryIds(JArray categories, Dictionary<string, uint> leveMap, ref int corrected)
        {
            if (categories == null) return;

            foreach (var category in categories.OfType<JObject>())
            {
                // Recursively process subcategories
                var subCategories = category["Categories"] as JArray;
                if (subCategories?.Count > 0)
                {
                    CorrectCategoryIds(subCategories, leveMap, ref corrected);
                }

                // Process quests in this category
                var quests = category["Quests"] as JArray;
                if (quests != null)
                {
                    foreach (var quest in quests.OfType<JObject>())
                    {
                        var questTitle = quest["Title"]?.ToString();
                        if (string.IsNullOrEmpty(questTitle))
                            continue;

                        if (leveMap.TryGetValue(questTitle, out var correctId))
                        {
                            var idArray = quest["Id"] as JArray;
                            if (idArray != null && idArray.Count > 0)
                            {
                                var oldId = idArray[0].Value<uint>();
                                if (oldId != correctId)
                                {
                                    idArray[0] = correctId;
                                    corrected++;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
