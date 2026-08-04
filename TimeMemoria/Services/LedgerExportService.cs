using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace TimeMemoria.Services
{
    /// <summary>
    /// Builds the clipboard payload consumed by the Adventurer's Ledger tracker.
    /// Shapes data into the ledger's own field names so an import can merge it
    /// directly. Local only — the payload goes to the clipboard and nowhere else.
    /// </summary>
    internal sealed class LedgerExportService
    {
        private readonly IPlayerState             playerState;
        private readonly ClassJobProgressService  classJobProgress;
        private readonly PlaytimeStatsService     playtimeStats;
        private readonly QuestDataManager         questDataManager;
        private readonly IPluginLog               pluginLog;

        /// <summary>Ledger keys for the expansions it breaks the MSQ down by.</summary>
        private static readonly (string Expansion, string Key)[] MsqExpansions =
        {
            ("A Realm Reborn",  "arr"),
            ("Heavensward",     "hw"),
            ("Stormblood",      "stb"),
            ("Shadowbringers",  "shb"),
            ("Endwalker",       "ew"),
            ("Dawntrail",       "dt")
        };

        public LedgerExportService(
            IPlayerState            playerState,
            ClassJobProgressService classJobProgress,
            PlaytimeStatsService    playtimeStats,
            QuestDataManager        questDataManager,
            IPluginLog              pluginLog)
        {
            this.playerState      = playerState;
            this.classJobProgress = classJobProgress;
            this.playtimeStats    = playtimeStats;
            this.questDataManager = questDataManager;
            this.pluginLog        = pluginLog;
        }


        /// <summary>
        /// Produces the ledger payload as indented JSON. Fields the plugin cannot
        /// source are omitted entirely rather than sent as zero, so an importer can
        /// merge without clobbering values it already holds.
        /// </summary>
        public string BuildJson()
        {
            try
            {
                var root = new JsonObject
                {
                    ["source"]   = "time-memoria",
                    ["version"]  = typeof(LedgerExportService).Assembly
                                       .GetName().Version?.ToString() ?? "unknown",
                    ["exported"] = DateTime.UtcNow.ToString("o"),
                    ["name"]     = playerState.IsLoaded ? playerState.CharacterName : string.Empty,
                    ["server"]   = BuildServer()
                };

                if (playerState.IsLoaded)
                    root["comm"] = playerState.PlayerCommendations;

                var playtime = BuildPlaytime();
                if (playtime != null)
                    root["playtime"] = playtime;

                var progress = classJobProgress.GetProgress();
                root["combat"] = BuildLevels(progress, "combat");
                root["craft"]  = BuildLevels(progress, "craft");
                root["gather"] = BuildLevels(progress, "gather");

                var (breakdown, totals) = BuildMsqBreakdown();
                root["msqBreakdown"]       = breakdown;
                root["msqBreakdownTotals"] = totals;

                return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "[LedgerExport] Failed to build payload");
                return "{}";
            }
        }


        private JsonObject BuildServer()
        {
            var server = new JsonObject
            {
                ["pdc"]   = string.Empty,
                ["ldc"]   = string.Empty,
                ["world"] = string.Empty
            };

            if (!playerState.IsLoaded)
                return server;

            var world = playerState.HomeWorld.ValueNullable;
            if (world == null)
                return server;

            server["world"] = world.Value.Name.ToString();

            // Home world -> data centre -> region group, which is what the ledger
            // calls the physical data centre.
            var dc = world.Value.DataCenter.ValueNullable;
            if (dc != null)
            {
                server["ldc"] = dc.Value.Name.ToString();

                var region = dc.Value.Region.ValueNullable;
                if (region != null)
                    server["pdc"] = region.Value.Name.ToString();
            }

            return server;
        }


        /// <summary>
        /// Lifetime playtime as whole days and leftover hours. Returns null when the
        /// player has not run /playtime yet, since the plugin has no other source and
        /// sending zeroes would overwrite a good value in the ledger.
        /// </summary>
        private JsonObject? BuildPlaytime()
        {
            var record = playtimeStats.CurrentRecord;
            if (record == null || record.LifetimePlaytime <= TimeSpan.Zero)
                return null;

            var span = record.LifetimePlaytime;
            var playtime = new JsonObject
            {
                ["days"]  = (int)span.TotalDays,
                ["hours"] = span.Hours
            };

            // Lifetime playtime only moves when the player runs /playtime, so tell
            // the ledger how old this figure is rather than implying it is current.
            if (record.LifetimePlaytimeUpdatedUtc.HasValue)
                playtime["asOf"] = record.LifetimePlaytimeUpdatedUtc.Value.ToString("o");

            return playtime;
        }


        /// <summary>
        /// Levels encoded the way the ledger stores them: level plus the fraction of
        /// the way through it, to three decimals. Max level emits a bare integer.
        /// </summary>
        private static JsonObject BuildLevels(List<ClassJobProgress> progress, string category)
        {
            var obj = new JsonObject();

            foreach (var job in progress.Where(p => p.Category == category))
            {
                if (job.Level == 0 || job.IsMaxLevel)
                {
                    obj[job.Name] = job.Level;
                    continue;
                }

                var encoded = Math.Round(job.Level + job.Fraction, 3, MidpointRounding.AwayFromZero);
                obj[job.Name] = encoded;
            }

            return obj;
        }


        /// <summary>
        /// Completed and total MSQ per expansion. Counts come through the same
        /// filtered path the Quests tab uses, so starting-city and Grand Company
        /// branches are excluded and the totals match what the character can
        /// actually complete.
        /// </summary>
        private (JsonObject Breakdown, JsonObject Totals) BuildMsqBreakdown()
        {
            var breakdown = new JsonObject();
            var totals    = new JsonObject();

            foreach (var (expansion, key) in MsqExpansions)
            {
                var quests = new List<Quest>();

                foreach (var questline in QuestlineRegistry.All.Where(q => q.Expansion == expansion))
                {
                    foreach (var prefix in questline.PatchPrefixes)
                    {
                        var path = $"{prefix / 10}.x/{prefix / 10}.{prefix % 10}/msq";
                        quests.AddRange(questDataManager.LoadBucketDirect(path));
                    }
                }

                breakdown[key] = quests.Count(QuestDataManager.IsQuestComplete);
                totals[key]    = quests.Count;
            }

            return (breakdown, totals);
        }
    }
}
