using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using TimeMemoria.Services;
using TimeMemoria.UI;



namespace TimeMemoria
{
    class MainWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly QuestDataManager questDataManager;
        private readonly Configuration configuration;
        private readonly PlaytimeStatsService playtimeStats;
        private readonly NewsService newsService;
        private readonly TocService tocService;
        private readonly ClassJobProgressService classJobProgress;
        private readonly LedgerExportService ledgerExport;

        private string searchText = "";

        private string? _selectedKey        = null;
        private string? _selectedLabel      = null;
        private string? _lockMessage        = null;
        private List<Quest>? _selectedQuests = null;

        private bool _forceOverviewTab = false;
        private bool _wasVisible       = false;

        private float _leftPanelWidth = 282f;


        public MainWindow(
            Plugin plugin,
            QuestDataManager questDataManager,
            Configuration configuration,
            PlaytimeStatsService playtimeStats,
            NewsService newsService,
            TocService tocService,
            ClassJobProgressService classJobProgress,
            LedgerExportService ledgerExport)
            : base("Time Memoria##main_window",
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.plugin           = plugin;
            this.questDataManager = questDataManager;
            this.configuration    = configuration;
            this.playtimeStats    = playtimeStats;
            this.newsService      = newsService;
            this.tocService       = tocService;
            this.classJobProgress = classJobProgress;
            this.ledgerExport     = ledgerExport;

            this.Size          = new Vector2(832, 470);
            this.SizeCondition = ImGuiCond.Appearing;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(700, 470),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }


        public void Dispose() { }


        public override void OnOpen()
        {
            base.OnOpen();
            // Don't pre-load levequests - load on-demand when expanded
        }

        public override void OnClose()
        {
            _wasVisible     = false;
            _selectedKey    = null;
            _selectedLabel  = null;
            _selectedQuests = null;
            _lockMessage    = null;
            searchText      = "";
        }


        public override void Draw()
        {
            questDataManager.UpdateQuestData();

            if (!_wasVisible)
            {
                _forceOverviewTab = true;
                _wasVisible       = true;
            }

            if (ImGui.BeginTabBar("##tab_bar", ImGuiTabBarFlags.None))
            {
                var overviewFlags = _forceOverviewTab
                    ? ImGuiTabItemFlags.SetSelected
                    : ImGuiTabItemFlags.None;
                _forceOverviewTab = false;

                if (ImGui.BeginTabItem("Overview##overview_tab", overviewFlags))
                {
                    DrawOverviewTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Quests##quest_tab"))
                {
                    DrawQuestsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("News##news_tab"))
                {
                    NewsPanel.Draw(newsService, playtimeStats, configuration);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Progression##progression_tab"))
                {
                    ProgressionPanel.Draw(classJobProgress, ledgerExport);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Settings##settings_tab"))
                {
                    DrawSettingsTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }


        // ── Overview Tab ──────────────────────────────────────────────────────


        private void DrawOverviewTab()
        {
            ImGui.BeginChild("##overview_tab", ImGuiHelpers.ScaledVector2(0), true);

            var (overallComplete, overallTotal) = GetOverallStats();
            float overallPct = overallTotal > 0 ? overallComplete / (float)overallTotal * 100f : 0f;

            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Quest Completion Progress");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            RenderExpansionStats("Overall", overallComplete, overallTotal, overallPct);
            ImGui.Spacing();

            RenderExpansionStats("A Realm Reborn", GetExpansionStats("A Realm Reborn"));
            RenderExpansionStats("Heavensward", GetExpansionStats("Heavensward"));
            RenderExpansionStats("Stormblood", GetExpansionStats("Stormblood"));
            RenderExpansionStats("Shadowbringers", GetExpansionStats("Shadowbringers"));
            RenderExpansionStats("Endwalker", GetExpansionStats("Endwalker"));
            RenderExpansionStats("Dawntrail", GetExpansionStats("Dawntrail"));

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.5f, 1.0f), "Suggested Quest");
            ImGui.Spacing();

            var (oldestQuest, questline, bucket) = FindOldestIncompleteQuest();
            if (oldestQuest != null)
            {
                if (questline != null)
                {
                    ImGui.TextDisabled($"  {questline.Expansion}");
                    ImGui.TextDisabled($"    {questline.DisplayName}");
                }
                ImGui.Text($"      {oldestQuest.Title}");
                if (oldestQuest.Id.Count > 0)
                {
                    ImGui.SameLine(400f);
                    ImGui.TextDisabled($"[{oldestQuest.Id[0]}]");
                }
                ImGui.TextDisabled($"      Level {oldestQuest.Level} • {oldestQuest.Area}");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), "  Current MSQ To be continued...");
            }

            ImGui.EndChild();
        }

        private void RenderExpansionStats(string name, (int complete, int total) stats)
        {
            var (complete, total) = stats;
            float pct = total > 0 ? complete / (float)total * 100f : 0f;
            RenderExpansionStats(name, complete, total, pct);
        }

        private void RenderExpansionStats(string name, int complete, int total, float pct)
        {
            ImGui.Text(name);
            ImGui.SameLine(200f);
            ImGui.Text($"{complete}/{total}");
            ImGui.SameLine(280f);
            ImGui.TextColored(
                pct == 100f ? new Vector4(0.4f, 1.0f, 0.4f, 1.0f) : new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                $"{(int)pct}%");
            ImGui.Spacing();
        }

        private (int complete, int total) GetOverallStats()
        {
            int complete = 0, total = 0;
            foreach (var ql in QuestlineRegistry.All)
            {
                var (c, t) = GetQuestlineAggregateStats(ql);
                complete += c;
                total += t;
            }
            return (complete, total);
        }

        private (int complete, int total) GetExpansionStats(string expansionName)
        {
            int complete = 0, total = 0;
            var questlines = QuestlineRegistry.All
                .Where(q => q.Expansion == expansionName).ToList();
            foreach (var ql in questlines)
            {
                var (c, t) = GetQuestlineAggregateStats(ql);
                complete += c;
                total += t;
            }
            return (complete, total);
        }

        private (Quest?, QuestlineDefinition?, string) FindOldestIncompleteQuest()
        {
            foreach (var (suffix, label) in QuestlineRegistry.BucketDisplayNames)
            {
                if (suffix == "Seasonal") continue;

                foreach (var ql in QuestlineRegistry.All)
                {
                    var quests = LoadBucketQuestsDirect(ql, suffix);
                    foreach (var quest in quests)
                    {
                        if (!QuestDataManager.IsQuestComplete(quest))
                            return (quest, ql, label);
                    }
                }
            }

            return (null, null, "");
        }


        // ── Quests Tab ────────────────────────────────────────────────────────


        private void DrawQuestsTab()
        {
            if (_selectedKey == null && _lockMessage == null)
                AutoSelectOldestIncomplete();

            float totalWidth = ImGui.GetContentRegionAvail().X;
            float leftWidth  = _leftPanelWidth;
            float splitterW  = 4f;
            float rightWidth = totalWidth - leftWidth - splitterW - ImGui.GetStyle().ItemSpacing.X * 2;

            if (configuration.DisableLazyLoad)
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##search_input", "Search quests...",
                    ref searchText, 256);
                ImGui.Spacing();
            }
            else
            {
                searchText = "";
            }

            // ── Left panel ────────────────────────────────────────────────
            float panelHeight = ImGui.GetContentRegionAvail().Y;
            ImGui.BeginChild("##ql_left",
                new Vector2(leftWidth, panelHeight), true);

            foreach (var expansion in QuestlineRegistry.Expansions)
            {
                var questlines = QuestlineRegistry.All
                    .Where(q => q.Expansion == expansion).ToList();

                bool expOpen = ImGui.TreeNodeEx(
                    $"{expansion}##exp_{expansion}",
                    ImGuiTreeNodeFlags.SpanAvailWidth);

                if (expOpen)
                {
                    foreach (var ql in questlines)
                    {
                        var state = tocService.GetUnlockState(
                            ql,
                            configuration.FreeTrialMode,
                            configuration.SpoilerMode);

                        bool isLocked = state != QuestlineUnlockState.Unlocked;

                        var (qlComplete, qlTotal) = GetQuestlineAggregateStats(ql);
                        string pctText = qlTotal > 0
                            ? $"{(int)(qlComplete / (float)qlTotal * 100)}%"
                            : "—";
                        float pctColW  = ImGui.CalcTextSize("100%").X + 4f;
                        float fixedPctX = ImGui.GetWindowWidth() - pctColW
                                        - ImGui.GetStyle().WindowPadding.X
                                        - ImGui.GetStyle().ScrollbarSize;

                        if (isLocked)
                            ImGui.PushStyleColor(ImGuiCol.Text,
                                ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);

                        bool qlOpen = ImGui.TreeNodeEx(
                            $"{ql.DisplayName}##qlnode_{ql.Name}",
                            ImGuiTreeNodeFlags.SpanAvailWidth);

                        ImGui.SameLine(fixedPctX);
                        ImGui.TextDisabled(pctText);

                        if (isLocked)
                            ImGui.PopStyleColor();

                        if (qlOpen)
                        {
                            foreach (var (suffix, label) in QuestlineRegistry.BucketDisplayNames)
                            {
                                bool isSeasonal = suffix == "Seasonal";
                                bool canSelect  = isSeasonal ||
                                                  state == QuestlineUnlockState.Unlocked;

                                string bucketKey = $"{ql.Name}|{suffix}";
                                bool   selected  = _selectedKey == bucketKey;

                                string bPctText = "";
                                if (!isSeasonal)
                                {
                                    var (bc, bt) = GetBucketStats(ql, suffix);
                                    bPctText = bt > 0
                                        ? $"{(int)(bc / (float)bt * 100)}%"
                                        : "—";
                                }
                                if (!canSelect)
                                    ImGui.PushStyleColor(ImGuiCol.Text,
                                        ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);

                                if (ImGui.Selectable(
                                    $"  {label}##bkt_{ql.Name}_{suffix}",
                                    selected,
                                    ImGuiSelectableFlags.None,
                                    new Vector2(fixedPctX - ImGui.GetCursorPosX() - 4f, 0)))
                                {
                                    if (canSelect)
                                        SelectBucket(ql, suffix);
                                    else
                                        SetLockMessage(ql, state);
                                }

                                if (!string.IsNullOrEmpty(bPctText))
                                {
                                    ImGui.SameLine(fixedPctX);
                                    ImGui.TextDisabled(bPctText);
                                }

                                if (!canSelect)
                                    ImGui.PopStyleColor();
                            }

                            ImGui.TreePop();
                        }
                    }

                    ImGui.TreePop();
                }
            }

            // ── Levequests Section ────────────────────────────────────────
            var levequestRoot = plugin.QuestData.Categories.FirstOrDefault(c => c.Title == "Levequests");
            if (levequestRoot != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                bool leveOpen = ImGui.TreeNodeEx(
                    $"{levequestRoot.Title}##levequest_root",
                    ImGuiTreeNodeFlags.SpanAvailWidth);

                if (leveOpen)
                {
                    RenderLevequestTree(levequestRoot, "Levequests", fixedPctX: ImGui.GetWindowWidth() - ImGui.CalcTextSize("100%").X - 4f - ImGui.GetStyle().WindowPadding.X - ImGui.GetStyle().ScrollbarSize);
                    ImGui.TreePop();
                }
            }

            ImGui.EndChild();

            // ── Draggable splitter ────────────────────────────────────────
            ImGui.SameLine();
            var splitterPos = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton("##splitter", new Vector2(splitterW, panelHeight));
            bool splitterHovered = ImGui.IsItemHovered();
            bool splitterActive  = ImGui.IsItemActive();
            if (splitterHovered || splitterActive)
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            if (splitterActive)
            {
                _leftPanelWidth += ImGui.GetIO().MouseDelta.X;
                _leftPanelWidth  = Math.Clamp(_leftPanelWidth, 282f, totalWidth - 200f);
            }
            var lineColor = splitterActive  ? ImGui.GetColorU32(ImGuiCol.SeparatorActive)  :
                            splitterHovered ? ImGui.GetColorU32(ImGuiCol.SeparatorHovered) :
                                             ImGui.GetColorU32(ImGuiCol.Separator);
            var drawList  = ImGui.GetWindowDrawList();
            float midX    = splitterPos.X + splitterW / 2f;
            float midY    = splitterPos.Y + panelHeight / 2f;
            var   icon    = "\u2194";
            var   iconSz  = ImGui.CalcTextSize(icon);
            drawList.AddLine(
                new Vector2(midX, splitterPos.Y),
                new Vector2(midX, midY - iconSz.Y / 2f - 2f),
                lineColor, 1f);
            drawList.AddLine(
                new Vector2(midX, midY + iconSz.Y / 2f + 2f),
                new Vector2(midX, splitterPos.Y + panelHeight),
                lineColor, 1f);
            drawList.AddText(
                new Vector2(splitterPos.X + splitterW / 2f - iconSz.X / 2f, midY - iconSz.Y / 2f),
                lineColor, icon);

            // ── Right panel ───────────────────────────────────────────────
            ImGui.SameLine();
            ImGui.BeginChild("##ql_right",
                new Vector2(rightWidth, ImGui.GetContentRegionAvail().Y), true);

            if (_lockMessage != null)
            {
                float cy = ImGui.GetContentRegionAvail().Y / 2f -
                           ImGui.GetTextLineHeightWithSpacing() * 2f;
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(cy, 0f));
                ImGui.TextDisabled(_lockMessage);
            }
            else if (_selectedLabel == null || _selectedQuests == null)
            {
                float cy = ImGui.GetContentRegionAvail().Y / 2f -
                           ImGui.GetTextLineHeight();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(cy, 0f));
                ImGui.TextDisabled("Select a category on the left.");
            }
            else
            {
                var displayQuests = GetDisplayQuests();

                int   rc   = _selectedQuests.Count(q =>
                    _selectedKey?.StartsWith("Leve") == true
                        ? QuestDataManager.IsLevequestComplete(q)
                        : QuestDataManager.IsQuestComplete(q));
                int   rt   = _selectedQuests.Count;
                float rpct = rt > 0 ? rc / (float)rt * 100f : 0f;
                string stats = $"{rc}/{rt} {(int)rpct}%";
                float statsW = ImGui.CalcTextSize(stats).X;
                float hAvail = ImGui.GetContentRegionAvail().X;

                ImGui.Text(_selectedLabel);
                ImGui.SameLine(hAvail - statsW);
                ImGui.TextDisabled(stats);
                ImGui.Separator();
                ImGui.Spacing();

                if (configuration.DisableLazyLoad &&
                    !string.IsNullOrWhiteSpace(searchText))
                {
                    ImGui.TextDisabled(
                        $"{displayQuests.Count} " +
                        $"result{(displayQuests.Count == 1 ? "" : "s")} " +
                        $"for \"{searchText}\"");
                    ImGui.Spacing();
                }

                bool isNewEra = _selectedKey?.EndsWith("|NewEra") == true &&
                               displayQuests.Any(q => !string.IsNullOrEmpty(q.Chain));

                if (isNewEra)
                {
                    var byChain = displayQuests
                        .GroupBy(q => q.Chain ?? "Other")
                        .OrderBy(g => g.Key);

                    int neColCount = configuration.ShowQuestArea ? 4 : 3;
                    if (ImGui.BeginTable("##ql_table", neColCount,
                        ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV,
                        ImGui.GetContentRegionAvail()))
                    {
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableSetupColumn("##chk",  ImGuiTableColumnFlags.WidthFixed,   22f);
                        ImGui.TableSetupColumn("##lvl",  ImGuiTableColumnFlags.WidthFixed,   36f);
                        ImGui.TableSetupColumn("Title",  ImGuiTableColumnFlags.WidthStretch);
                        if (configuration.ShowQuestArea)
                            ImGui.TableSetupColumn("Area", ImGuiTableColumnFlags.WidthFixed, 140f);
                        ImGui.TableHeadersRow();

                        foreach (var group in byChain)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); // chk
                            ImGui.TableNextColumn(); // lvl
                            ImGui.TableNextColumn(); // title — render chain header here
                            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), group.Key);
                            if (configuration.ShowQuestArea)
                                ImGui.TableNextColumn(); // area (blank)

                            foreach (var quest in group)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();

                                bool done = _selectedKey?.StartsWith("Levequests") == true
                                    ? QuestDataManager.IsLevequestComplete(quest)
                                    : QuestDataManager.IsQuestComplete(quest);
                                if (done)
                                {
                                    ImGui.PushFont(UiBuilder.IconFont);
                                    ImGui.TextUnformatted(FontAwesomeIcon.Check.ToIconString());
                                    ImGui.PopFont();
                                }

                                ImGui.TableNextColumn();
                                ImGui.TextDisabled(quest.Level.ToString());

                                ImGui.TableNextColumn();

                                string displayTitle = quest.Title;
                                if (quest.Id.Count > 0)
                                {
                                    displayTitle = $"{quest.Title} [{quest.Id[0]}]";
                                }

                                if (configuration.DisableLazyLoad &&
                                    !string.IsNullOrWhiteSpace(searchText))
                                    RenderHighlightedText(displayTitle, searchText, done);
                                else if (done)
                                    ImGui.TextDisabled(displayTitle);
                                else
                                    ImGui.Text(displayTitle);

                                if (configuration.ShowQuestArea)
                                {
                                    ImGui.TableNextColumn();
                                    ImGui.TextDisabled(quest.Area ?? string.Empty);
                                }
                            }
                        }

                        ImGui.EndTable();
                    }
                }
                else
                {
                    RenderQuestResults(displayQuests);
                }
            }

            ImGui.EndChild();
        }


        // ── Settings Tab ──────────────────────────────────────────────────────


        private void DrawSettingsTab()
        {
            ImGui.BeginChild("##settings_tab", ImGuiHelpers.ScaledVector2(0), true);

            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Display Options");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.SetNextItemWidth(130);
            var displayOption = configuration.DisplayOption;
            string[] displayList = { "Show All", "Show Incomplete", "Show Complete" };
            if (ImGui.BeginCombo("##display_option", displayList[displayOption]))
            {
                for (int i = 0; i < displayList.Length; i++)
                {
                    if (ImGui.Selectable(displayList[i]))
                    {
                        configuration.DisplayOption = i;
                        configuration.Save();
                        questDataManager.UpdateQuestData();
                        questDataManager.InvalidateDirectBucketCache();
                        ResetSelections();
                    }
                    if (displayOption == i) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            var showCount = configuration.ShowCount;
            if (ImGui.Checkbox("Show count \"Main Scenario 502/843\"", ref showCount))
            {
                configuration.ShowCount = showCount;
                configuration.Save();
            }

            ImGui.Spacing();

            var showPercentage = configuration.ShowPercentage;
            if (ImGui.Checkbox("Show percentage \"Tribal Quests 32.13%\"", ref showPercentage))
            {
                configuration.ShowPercentage = showPercentage;
                configuration.Save();
            }

            ImGui.Spacing();

            var excludeOtherQuests = configuration.ExcludeOtherQuests;
            if (ImGui.Checkbox("Exclude 'Other Quests' from Overall", ref excludeOtherQuests))
            {
                configuration.ExcludeOtherQuests = excludeOtherQuests;
                configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Quest Browser");
            ImGui.Separator();
            ImGui.Spacing();

            var disableLazyLoad = configuration.DisableLazyLoad;
            if (ImGui.Checkbox("Disable lazy loading (enables quest search)",
                ref disableLazyLoad))
            {
                configuration.DisableLazyLoad = disableLazyLoad;
                configuration.Save();
                if (!disableLazyLoad) searchText = "";
            }
            ImGui.Spacing();
            ImGui.TextDisabled("Loads all quest data at startup.");
            ImGui.TextDisabled("Required for the search bar in the Quests tab.");

            ImGui.Spacing();

            ImGui.SetNextItemWidth(180);
            var classJobOrderMode = Math.Clamp(configuration.ClassJobOrderMode, 0, 1);
            string[] classJobOrderOptions = { "Merged Order", "Release Order" };
            if (ImGui.BeginCombo("Class & Job order", classJobOrderOptions[classJobOrderMode]))
            {
                for (int i = 0; i < classJobOrderOptions.Length; i++)
                {
                    if (ImGui.Selectable(classJobOrderOptions[i]))
                    {
                        configuration.ClassJobOrderMode = i;
                        configuration.Save();
                        questDataManager.InvalidateDirectBucketCache();
                        RefreshSelectedBucketIfNeeded();
                    }

                    if (classJobOrderMode == i)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }
            ImGui.TextDisabled("Merged Order follows the legacy monolith ordering.");
            ImGui.TextDisabled("Release Order keeps patch/file order.");

            ImGui.Spacing();

            var showQuestArea = configuration.ShowQuestArea;
            if (ImGui.Checkbox("Show area column in quest list", ref showQuestArea))
            {
                configuration.ShowQuestArea = showQuestArea;
                configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Spoiler Settings");
            ImGui.Separator();
            ImGui.Spacing();

            var spoilerMode = configuration.SpoilerMode;
            if (ImGui.Checkbox(
                "Spoiler Mode (show all content regardless of MSQ progress)",
                ref spoilerMode))
            {
                configuration.SpoilerMode = spoilerMode;
                configuration.Save();
            }

            ImGui.Spacing();

            var freeTrialMode = configuration.FreeTrialMode;
            if (ImGui.Checkbox("Free Trial Mode (restrict to 2.0–4.0 content)",
                ref freeTrialMode))
            {
                configuration.FreeTrialMode = freeTrialMode;
                configuration.Save();
            }
            ImGui.Spacing();
            ImGui.TextDisabled("Free Trial Mode cannot be overridden by Spoiler Mode.");

            ImGui.EndChild();
        }


        // ── Quests Tab Helpers ────────────────────────────────────────────────


        private void SelectBucket(QuestlineDefinition ql, string suffix)
        {
            _lockMessage    = null;
            _selectedKey    = $"{ql.Name}|{suffix}";
            string label    = QuestlineRegistry.BucketDisplayNames
                .First(b => b.Suffix == suffix).Label;
            _selectedLabel  = $"{ql.DisplayName} — {label}";
            _selectedQuests = LoadBucketQuestsDirect(ql, suffix);
        }


        private void SetLockMessage(QuestlineDefinition ql, QuestlineUnlockState state)
        {
            _selectedKey    = null;
            _selectedLabel  = null;
            _selectedQuests = null;
            _lockMessage    = state == QuestlineUnlockState.FreeTrialLocked
                ? "This content requires the full version of Final Fantasy XIV.\n" +
                  "Free Trial Mode is enabled in Settings."
                : $"Your character has not progressed to {ql.Name} yet.\n" +
                  "Enable Spoiler Mode in Settings to access this content.";
        }


        private List<Quest> LoadBucketQuestsDirect(QuestlineDefinition ql, string suffix)
        {
            var result = new List<Quest>();
            foreach (int prefix in ql.PatchPrefixes)
            {
                string major = $"{prefix / 10}.x";
                string minor = $"{prefix / 10}.{prefix % 10}";
                string path  = $"{major}/{minor}/{suffix}";
                result.AddRange(questDataManager.LoadBucketDirect(path));
            }

            if (suffix == "Class" && configuration.ClassJobOrderMode == 0)
                return result
                    .OrderBy(questDataManager.GetQuestSortOrder)
                    .ToList();

            return result;
        }


        private List<Quest> GetDisplayQuests()
        {
            if (_selectedQuests == null) return new();

            var quests = _selectedQuests.AsEnumerable();

            if (configuration.DisplayOption == 1)
                quests = quests.Where(q => !QuestDataManager.IsQuestComplete(q));
            else if (configuration.DisplayOption == 2)
                quests = quests.Where(q => QuestDataManager.IsQuestComplete(q));

            if (configuration.DisableLazyLoad && !string.IsNullOrWhiteSpace(searchText))
                quests = quests.Where(q =>
                    q.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            return quests.ToList();
        }


        private (int complete, int total) GetBucketStats(
            QuestlineDefinition ql, string suffix)
        {
            var quests   = LoadBucketQuestsDirect(ql, suffix);
            int total    = quests.Count;
            int complete = quests.Count(q => QuestDataManager.IsQuestComplete(q));
            return (complete, total);
        }


        private (int complete, int total) GetQuestlineAggregateStats(
            QuestlineDefinition ql)
        {
            int complete = 0, total = 0;
            foreach (var (suffix, _) in QuestlineRegistry.BucketDisplayNames)
            {
                if (suffix == "Seasonal") continue;
                var (c, t) = GetBucketStats(ql, suffix);
                complete += c;
                total    += t;
            }
            return (complete, total);
        }


        private void AutoSelectOldestIncomplete()
        {
            foreach (var ql in QuestlineRegistry.All)
            {
                var state = tocService.GetUnlockState(
                    ql,
                    configuration.FreeTrialMode,
                    configuration.SpoilerMode);

                if (state != QuestlineUnlockState.Unlocked) continue;

                foreach (var (suffix, _) in QuestlineRegistry.BucketDisplayNames)
                {
                    if (suffix == "Seasonal") continue;

                    var (c, t) = GetBucketStats(ql, suffix);
                    if (t == 0 || c >= t) continue;

                    SelectBucket(ql, suffix);
                    return;
                }
            }
        }


        private void RenderHighlightedText(string text, string match, bool dimmed)
        {
            int idx = text.IndexOf(match, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                if (dimmed) ImGui.TextDisabled(text);
                else        ImGui.Text(text);
                return;
            }

            var normalCol = ImGui.GetStyle().Colors[
                dimmed ? (int)ImGuiCol.TextDisabled : (int)ImGuiCol.Text];
            var hlCol     = ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered];

            string before = text[..idx];
            string hl     = text.Substring(idx, match.Length);
            string after  = text[(idx + match.Length)..];

            if (!string.IsNullOrEmpty(before))
            {
                ImGui.TextColored(normalCol, before);
                ImGui.SameLine(0, 0);
            }

            ImGui.TextColored(hlCol, hl);

            if (!string.IsNullOrEmpty(after))
            {
                ImGui.SameLine(0, 0);
                ImGui.TextColored(normalCol, after);
            }
        }

        private void RenderQuestResults(List<Quest> displayQuests)
        {
            bool isClassBucket =
                _selectedKey?.EndsWith("|Class", StringComparison.OrdinalIgnoreCase) == true ||
                (_selectedLabel?.Contains("Class & Job Quests", StringComparison.OrdinalIgnoreCase) ?? false);

            if (isClassBucket)
            {
                RenderGroupedClassQuests(displayQuests);
                return;
            }

            RenderQuestTable(displayQuests, "##ql_table");
        }

        private void RenderGroupedClassQuests(List<Quest> displayQuests)
        {
            bool mergedOrder = configuration.ClassJobOrderMode == 0;
            var questEntries = displayQuests
                .Select((quest, sequence) => new
                {
                    Quest = quest,
                    Path = questDataManager.GetQuestCategoryPath(quest),
                    Order = questDataManager.GetQuestSortOrder(quest),
                    Sequence = sequence
                })
                .ToList();

            var parentGroups = questEntries
                .GroupBy(entry => GetQuestCategorySegment(entry.Path, 1, "Other Quests"))
                .OrderBy(group => group.Min(entry => mergedOrder ? entry.Order : entry.Sequence))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var (parentGroup, parentIndex) in parentGroups.Select((group, index) => (group, index)))
            {
                if (parentIndex > 0)
                    ImGui.Spacing();

                ImGui.TextDisabled(parentGroup.Key);
                ImGui.Separator();

                var childGroups = parentGroup
                    .GroupBy(entry => GetQuestCategorySegment(entry.Path, 2, "Other Quests"))
                    .OrderBy(group => group.Min(entry => mergedOrder ? entry.Order : entry.Sequence))
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var (childGroup, childIndex) in childGroups.Select((group, index) => (group, index)))
                {
                    if (childIndex > 0)
                        ImGui.Spacing();

                    ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), childGroup.Key);

                    var childQuests = childGroup
                        .OrderBy(entry => mergedOrder ? entry.Order : entry.Sequence)
                        .Select(entry => entry.Quest)
                        .ToList();

                    RenderQuestTable(
                        childQuests,
                        $"##ql_table_class_{parentIndex}_{childIndex}",
                        scrollY: false);
                }
            }
        }

        private void RenderQuestTable(List<Quest> quests, string tableId, bool scrollY = true)
        {
            int colCount = configuration.ShowQuestArea ? 4 : 3;
            var tableFlags = ImGuiTableFlags.BordersInnerV;
            if (scrollY)
                tableFlags |= ImGuiTableFlags.ScrollY;

            var tableSize = scrollY
                ? ImGui.GetContentRegionAvail()
                : new Vector2(ImGui.GetContentRegionAvail().X, 0f);

            if (ImGui.BeginTable(tableId, colCount,
                tableFlags,
                tableSize))
            {
                if (scrollY)
                    ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("##chk",  ImGuiTableColumnFlags.WidthFixed,   22f);
                ImGui.TableSetupColumn("##lvl",  ImGuiTableColumnFlags.WidthFixed,   36f);
                ImGui.TableSetupColumn("Title",  ImGuiTableColumnFlags.WidthStretch);
                if (configuration.ShowQuestArea)
                    ImGui.TableSetupColumn("Area", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableHeadersRow();

                foreach (var quest in quests)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    bool done = _selectedKey?.StartsWith("Levequests") == true
                        ? QuestDataManager.IsLevequestComplete(quest)
                        : QuestDataManager.IsQuestComplete(quest);
                    if (done)
                    {
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(FontAwesomeIcon.Check.ToIconString());
                        ImGui.PopFont();
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(quest.Level.ToString());

                    ImGui.TableNextColumn();

                    string displayTitle = quest.Title;
                    if (quest.Id.Count > 0)
                    {
                        displayTitle = $"{quest.Title} [{quest.Id[0]}]";
                    }

                    if (configuration.DisableLazyLoad &&
                        !string.IsNullOrWhiteSpace(searchText))
                        RenderHighlightedText(displayTitle, searchText, done);
                    else if (done)
                        ImGui.TextDisabled(displayTitle);
                    else
                        ImGui.Text(displayTitle);

                    if (configuration.ShowQuestArea)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TextDisabled(quest.Area ?? string.Empty);
                    }
                }

                ImGui.EndTable();
            }
        }

        private static string GetQuestCategorySegment(string path, int index, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallback;

            var parts = path.Split(" > ", StringSplitOptions.RemoveEmptyEntries);
            if (index >= 0 && index < parts.Length)
                return parts[index];

            return fallback;
        }


        // ── Legacy Helpers ────────────────────────────────────────────────────


        private void ResetSelections()
        {
            if (configuration.CategorySelection == null ||
                configuration.CategorySelection.Hide)
            {
                configuration.CategorySelection =
                    plugin.QuestData.Categories.Find(c => !c.Hide);
                configuration.SubcategorySelection =
                    configuration.CategorySelection?.Categories.Find(c => !c.Hide);
            }

            if (configuration.SubcategorySelection == null ||
                configuration.SubcategorySelection.Hide)
            {
                configuration.SubcategorySelection =
                    configuration.CategorySelection?.Categories.Find(c => !c.Hide);
            }

            configuration.Save();
        }

        private void RefreshSelectedBucketIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_selectedKey))
                return;

            var parts = _selectedKey.Split('|');
            if (parts.Length != 2)
                return;

            var questline = QuestlineRegistry.All.FirstOrDefault(q => q.Name == parts[0]);
            if (questline == null)
                return;

            SelectBucket(questline, parts[1]);
        }


        private string GetDisplayText(QuestData questData)
        {
            var text = $"{questData.Title}";
            if (configuration.ShowCount)
                text += $" {questData.NumComplete}/{questData.Total}";
            if (configuration.ShowPercentage && questData.Total > 0)
                text += $" {questData.NumComplete / questData.Total:P2}";
            return text;
        }


        private void OpenAreaMap(Quest quest)
        {
            var questEnumerable = plugin.DataManager
                .GetExcelSheet<Lumina.Excel.Sheets.Quest>()
                .Where(q => quest.Id.Contains(q.RowId) &&
                            q.IssuerLocation.Value.RowId != 0);

            if (!questEnumerable.Any()) return;

            Level level = questEnumerable.First().IssuerLocation.Value;

            var mapLink = new MapLinkPayload(
                level.Territory.RowId,
                level.Map.RowId,
                (int)(level.X * 1_000f),
                (int)(level.Z * 1_000f));

            plugin.GameGui.OpenMapWithMapLink(mapLink);
        }


        private string GetStartClassName()
        {
            return configuration.StartClass switch
            {
                65822 => "Gladiator",
                66089 => "Pugilist",
                65848 => "Marauder",
                65754 => "Lancer",
                65755 => "Archer",
                65638 => "Rogue",
                65747 => "Conjurer",
                65882 => "Thaumaturge",
                65990 => "Arcanist",
                _     => "Unknown"
            };
        }


        private void RenderLevequestTree(QuestData node, string parentPath, float fixedPctX)
        {
            foreach (var child in node.Categories)
            {
                string childPath = string.IsNullOrEmpty(parentPath)
                    ? child.Title
                    : $"{parentPath}/{child.Title}";

                bool hasSubcategories = child.Categories.Count > 0;
                bool hasQuests = child.Quests.Count > 0;
                bool hasBucketToLoad = !string.IsNullOrEmpty(child.BucketPath) && !hasSubcategories && !hasQuests;
                bool hasChildren = hasSubcategories || hasQuests || hasBucketToLoad;

                string pctText = child.Total > 0
                    ? $"{(int)(child.NumComplete / (float)child.Total * 100)}%"
                    : "—";

                // Check if this node is selected
                bool isSelected = _selectedKey == childPath;

                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.SpanAvailWidth;
                if (!hasChildren)
                    flags |= ImGuiTreeNodeFlags.Leaf;
                if (isSelected)
                    flags |= ImGuiTreeNodeFlags.Selected;

                bool isOpen = ImGui.TreeNodeEx(
                    $"{child.Title}##leve_{childPath}",
                    flags);

                // Check click on tree node BEFORE drawing other items
                // Detect clicks on leaf nodes (NPCs with quests) or city nodes with buckets to load
                bool isClicked = (hasBucketToLoad || hasQuests) && !hasSubcategories && ImGui.IsItemClicked();

                ImGui.SameLine(fixedPctX);
                ImGui.TextDisabled(pctText);

                // Handle click/selection for NPC or city nodes
                if (isClicked)
                {
                    // If this is a city node with a bucket, load it first
                    if (hasBucketToLoad && !string.IsNullOrEmpty(child.BucketPath))
                    {
                        questDataManager.LoadBucketIfNeeded(child, forceLoad: true);
                    }
                    // If this is an NPC node in an unloaded city, load the parent bucket
                    else if (child.Quests.Count == 0 && !string.IsNullOrEmpty(node.BucketPath))
                    {
                        questDataManager.LoadBucketIfNeeded(node, forceLoad: true);
                    }

                    // After loading, check if we have quests to display
                    if (child.Quests.Count > 0)
                    {
                        _lockMessage = null;
                        _selectedKey = childPath;
                        _selectedLabel = child.Title;
                        _selectedQuests = child.Quests;
                    }
                    else
                    {
                        // No quests loaded - show error
                        _lockMessage = "Failed to load quests for this NPC.";
                    }
                }

                if (isOpen)
                {
                    // Don't auto-load buckets here - only load when user clicks on an NPC
                    // (buckets load on-demand when NPC is clicked via isClicked check above)

                    if (hasSubcategories)
                    {
                        RenderLevequestTree(child, childPath, fixedPctX);
                    }

                    // Don't render quests in the tree - they'll show on the right panel when selected

                    ImGui.TreePop();
                }
            }
        }
    }
}
