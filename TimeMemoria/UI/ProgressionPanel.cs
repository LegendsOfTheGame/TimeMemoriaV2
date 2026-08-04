using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using TimeMemoria.Services;

namespace TimeMemoria.UI
{
    /// <summary>
    /// Class and job levelling progress, with a local clipboard export.
    /// Observational only — levels and experience, no performance data.
    /// </summary>
    public static class ProgressionPanel
    {
        private static readonly Vector4 HeaderColour = new(0.5f, 0.8f, 1.0f, 1.0f);

        private static string copyFeedback  = string.Empty;
        private static DateTime copyShownAt = DateTime.MinValue;

        public static void Draw(ClassJobProgressService progressService)
        {
            ImGui.BeginChild("##progression_tab", ImGuiHelpers.ScaledVector2(0), true);

            var progress = progressService.GetProgress();
            var unlocked = progress.Where(p => p.IsUnlocked).ToList();

            if (unlocked.Count == 0)
            {
                ImGui.TextDisabled("No character loaded.");
                ImGui.EndChild();
                return;
            }

            ImGui.TextColored(HeaderColour, "Class & Job Progression");
            ImGui.SameLine();
            ImGui.TextDisabled($"({unlocked.Count} unlocked)");
            ImGui.Separator();
            ImGui.Spacing();

            DrawExportRow(progressService);

            ImGui.Spacing();

            if (ImGui.BeginTable("##progression_table", 4,
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg,
                ImGui.GetContentRegionAvail()))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Job",      ImGuiTableColumnFlags.WidthFixed,   150f);
                ImGui.TableSetupColumn("Level",    ImGuiTableColumnFlags.WidthFixed,    50f);
                ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("EXP",      ImGuiTableColumnFlags.WidthFixed,   170f);
                ImGui.TableHeadersRow();

                foreach (var job in unlocked)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(job.Name);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(job.Level.ToString());

                    ImGui.TableNextColumn();
                    if (job.IsMaxLevel)
                        ImGui.TextDisabled("Max level");
                    else
                        ImGui.ProgressBar(job.Fraction, new Vector2(-1f, 0f), string.Empty);

                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(job.IsMaxLevel
                        ? "—"
                        : $"{job.Experience:N0} / {job.ExperienceToNext:N0}");
                }

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }


        private static void DrawExportRow(ClassJobProgressService progressService)
        {
            if (ImGui.Button("Copy progression to clipboard"))
            {
                try
                {
                    ImGui.SetClipboardText(progressService.ExportJson());
                    copyFeedback = "Copied as JSON.";
                }
                catch (Exception ex)
                {
                    copyFeedback = $"Copy failed: {ex.Message}";
                }

                copyShownAt = DateTime.UtcNow;
            }

            // Fade the confirmation out rather than leaving it on screen forever.
            if (copyFeedback.Length > 0 && DateTime.UtcNow - copyShownAt < TimeSpan.FromSeconds(4))
            {
                ImGui.SameLine();
                ImGui.TextDisabled(copyFeedback);
            }

            ImGui.TextDisabled("Nothing is sent anywhere — the export stays on your clipboard.");
        }
    }
}
