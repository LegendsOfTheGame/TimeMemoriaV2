using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using TimeMemoria.Models;
using TimeMemoria.Services;

namespace TimeMemoria.UI
{
    public static class NewsPanel
    {
        public static void Draw(NewsService newsService, PlaytimeStatsService playtimeStats)
        {
            newsService.Poll();

            ImGui.BeginChild("##news_tab", ImGuiHelpers.ScaledVector2(0), true);

            DrawPacingSection(playtimeStats);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (newsService.IsLoading && newsService.Latest == null)
            {
                ImGui.TextDisabled("Loading news data...");
            }
            else if (newsService.FetchError != null && newsService.Latest == null)
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Could not load news data.");
                ImGui.TextDisabled(newsService.FetchError);
            }
            else if (newsService.Latest != null)
            {
                DrawMaintenanceSection(newsService.Latest);
                ImGui.Spacing();
                DrawEventsSection(newsService.Latest);
            }
            else
            {
                ImGui.TextDisabled("No news data available yet.");
            }

            ImGui.EndChild();
        }

        private static void DrawPacingSection(PlaytimeStatsService stats)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Quest Pacing");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text($"Session pacing:  {stats.FormattedSessionPacing ?? "—"}");
            ImGui.Text($"Overall pacing:  {stats.FormattedLifetimePacing ?? "—"}");
        }

        private static void DrawMaintenanceSection(NewsEvent data)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Maintenance");
            ImGui.Separator();
            ImGui.Spacing();

            if (data.Maintenance != null)
            {
                var m = data.Maintenance;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                bool isOngoing = m.Start.HasValue && m.Start.Value <= now
                                 && m.End.HasValue && m.End.Value > now;

                string statusLabel = isOngoing ? "Ongoing" : "Upcoming";
                ImGui.Text($"[{statusLabel}]  {m.Title ?? "Maintenance"}");

                if (m.Start.HasValue)
                    ImGui.TextDisabled($"  Starts: {FormatUtcUnix(m.Start.Value)}");
                if (m.End.HasValue)
                {
                    ImGui.TextDisabled($"  Ends:   {FormatUtcUnix(m.End.Value)}");
                    if (!isOngoing && m.End.Value > now)
                    {
                        var remaining = TimeSpan.FromSeconds(m.End.Value - now);
                        ImGui.TextDisabled($"  Time remaining: {FormatSpan(remaining)}");
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("No upcoming maintenance.");
            }

            ImGui.Spacing();

            if (data.LastMaintenance != null)
            {
                var lm = data.LastMaintenance;
                ImGui.TextDisabled($"Last:  {lm.Title ?? "Maintenance"}");
                if (lm.End.HasValue)
                    ImGui.TextDisabled($"  Ended: {FormatUtcUnix(lm.End.Value)}");
            }
        }

        private static void DrawEventsSection(NewsEvent data)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Active Events");
            ImGui.Separator();
            ImGui.Spacing();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool anyActive = false;

            foreach (var ev in data.Events)
            {
                if (ev.Title == null) continue;

                bool active = ev.Start.HasValue && ev.Start.Value <= now
                              && ev.End.HasValue && ev.End.Value > now;
                bool upcoming = ev.Start.HasValue && ev.Start.Value > now;

                if (!active && !upcoming) continue;
                anyActive = true;

                ImGui.BulletText(ev.Title);

                if (active && ev.End.HasValue)
                {
                    var remaining = TimeSpan.FromSeconds(ev.End.Value - now);
                    ImGui.TextDisabled($"    Ends in: {FormatSpan(remaining)}  ({FormatUtcUnix(ev.End.Value)})");
                }
                else if (upcoming && ev.Start.HasValue)
                {
                    ImGui.TextDisabled($"    Starts: {FormatUtcUnix(ev.Start.Value)}");
                }

                ImGui.Spacing();
            }

            if (!anyActive)
                ImGui.TextDisabled("No active or upcoming events.");

            if (data.LastEvent?.Title != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Last Event");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.TextDisabled(data.LastEvent.Title);
                if (data.LastEvent.End.HasValue)
                    ImGui.TextDisabled($"  Ended: {FormatUtcUnix(data.LastEvent.End.Value)}");
            }
        }

        private static string FormatUtcUnix(long unix)
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
            return dt.ToString("MMM d, h:mm tt");
        }

        private static string FormatSpan(TimeSpan span)
        {
            if (span <= TimeSpan.Zero) return "Ended";
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{span.Minutes}m {span.Seconds}s";
        }
    }
}
