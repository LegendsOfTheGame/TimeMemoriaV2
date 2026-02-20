using System;
using System.Linq;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using TimeMemoria.Models;

namespace TimeMemoria.Services
{
    /// <summary>
    /// Tracks per-character playtime and calculates descriptive quest pacing statistics.
    /// Subscribes to IFramework.Update to accumulate time while logged in.
    /// All pacing metrics are observational only—no rankings, thresholds, or judgments.
    /// </summary>
    public class PlaytimeStatsService : IDisposable
    {
        private readonly IFramework framework;
        private readonly IClientState clientState;
        private readonly IPlayerState playerState;
        private readonly IChatGui chatGui;
        private readonly Configuration configuration;

        private string? currentCharacterId;
        private bool disposed;
        private DateTime lastSaveTime = DateTime.UtcNow;

        /// <summary>
        /// Gets the current character's playtime record, or null if not logged in.
        /// </summary>
        public PlaytimeRecord? CurrentRecord { get; private set; }

        /// <summary>
        /// Gets the current session pacing in minutes per quest.
        /// Returns null if no quests have been completed this session.
        /// </summary>
        public double? SessionPacingMinutesPerQuest
        {
            get
            {
                if (CurrentRecord == null || CurrentRecord.SessionQuestsCompleted == 0)
                    return null;

                return CurrentRecord.SessionPlaytime.TotalMinutes / CurrentRecord.SessionQuestsCompleted;
            }
        }

        /// <summary>
        /// Gets the lifetime pacing in minutes per quest.
        /// Returns null if no quests have been completed lifetime.
        /// </summary>
        public double? LifetimePacingMinutesPerQuest
        {
            get
            {
                if (CurrentRecord == null || CurrentRecord.TotalQuestsCompleted == 0)
                    return null;

                return CurrentRecord.LifetimePlaytime.TotalMinutes / CurrentRecord.TotalQuestsCompleted;
            }
        }

        /// <summary>
        /// Gets formatted session pacing string for UI display.
        /// Returns null if no session pacing is available.
        /// </summary>
        public string? FormattedSessionPacing
        {
            get
            {
                var pacing = SessionPacingMinutesPerQuest;
                if (pacing == null)
                    return null;

                return FormatPacing(pacing.Value);
            }
        }

        /// <summary>
        /// Gets formatted lifetime pacing string for UI display.
        /// Returns null if no lifetime pacing is available.
        /// </summary>
        public string? FormattedLifetimePacing
        {
            get
            {
                var pacing = LifetimePacingMinutesPerQuest;
                if (pacing == null)
                    return null;

                return FormatPacing(pacing.Value);
            }
        }

        public PlaytimeStatsService(IFramework framework, IClientState clientState, IPlayerState playerState, IChatGui chatGui, Configuration configuration)
        {
            this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
            this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
            this.playerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
            this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            this.framework.Update += OnFrameworkUpdate;
            this.clientState.Login += OnLogin;
            this.clientState.Logout += OnLogout;
            this.chatGui.ChatMessage += OnChatMessage;

            if (this.clientState.IsLoggedIn)
            {
                InitializeCurrentCharacter();
            }
        }

        /// <summary>
        /// Seeds TotalQuestsCompleted from the current quest data on every login.
        /// Called by QuestDataManager after quest data is ready.
        /// </summary>
        public void SeedLifetimeQuestCount(int totalCompleted)
        {
            if (CurrentRecord == null) return;
            CurrentRecord.TotalQuestsCompleted = totalCompleted;
            SaveCurrentRecord();
        }

        /// <summary>
        /// Increments quest completion counters for the current character.
        /// Call this when a quest is passively detected as completed.
        /// </summary>
        public void IncrementQuestCompletion()
        {
            if (CurrentRecord == null)
                return;

            CurrentRecord.SessionQuestsCompleted++;
            CurrentRecord.TotalQuestsCompleted++;

            SaveCurrentRecord();
        }

        /// <summary>
        /// Formats pacing value into "Xm Ys per quest" format.
        /// </summary>
        private static string FormatPacing(double minutesPerQuest)
        {
            var totalSeconds = (int)(minutesPerQuest * 60);
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;

            return $"{minutes}m {seconds}s per quest";
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (!this.clientState.IsLoggedIn || CurrentRecord == null)
                return;

            var now = DateTime.UtcNow;
            var delta = now - CurrentRecord.LastUpdateUtc;

            if (delta < TimeSpan.Zero || delta > TimeSpan.FromMinutes(5))
            {
                CurrentRecord.LastUpdateUtc = now;
                return;
            }

            CurrentRecord.SessionPlaytime += delta;
            CurrentRecord.LastUpdateUtc = now;

            if ((now - lastSaveTime).TotalMinutes >= 5)
            {
                SaveCurrentRecord();
                lastSaveTime = now;
            }
        }

        private void OnLogin()
        {
            InitializeCurrentCharacter();
        }

        private void OnLogout(int type, int code)
        {
            if (CurrentRecord != null)
            {
                SaveCurrentRecord();
            }

            CurrentRecord = null;
            currentCharacterId = null;
        }

        private void OnChatMessage(
            XivChatType type,
            int timestamp,
            ref SeString sender,
            ref SeString message,
            ref bool isHandled)
        {
            if (type != XivChatType.SystemMessage || CurrentRecord == null)
                return;

            var messageText = message.TextValue;

            var match = System.Text.RegularExpressions.Regex.Match(
                messageText,
                @"Total Play Time:\s*(?:(\d+)\s*days?,\s*)?(?:(\d+)\s*hours?,\s*)?(?:(\d+)\s*minutes?)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                var hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                var minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                CurrentRecord.LifetimePlaytime = TimeSpan.FromDays(days) + TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
                SaveCurrentRecord();
            }
        }

        private void InitializeCurrentCharacter()
        {
            if (!this.playerState.IsLoaded)
                return;

            var worldName = this.playerState.HomeWorld.ValueNullable?.Name.ToString() ?? "Unknown";
            currentCharacterId = $"{this.playerState.CharacterName}@{worldName}";

            if (this.configuration.PlaytimeRecords.TryGetValue(currentCharacterId, out var existingRecord))
            {
                CurrentRecord = existingRecord;
                CurrentRecord.SessionPlaytime = TimeSpan.Zero;
                CurrentRecord.SessionQuestsCompleted = 0;
                CurrentRecord.LastUpdateUtc = DateTime.UtcNow;
            }
            else
            {
                CurrentRecord = new PlaytimeRecord
                {
                    CharacterId = currentCharacterId,
                    LastUpdateUtc = DateTime.UtcNow
                };
                this.configuration.PlaytimeRecords[currentCharacterId] = CurrentRecord;
                SaveCurrentRecord();
            }

            // TotalQuestsCompleted will be seeded by QuestDataManager
            // after quest data is ready via SeedLifetimeQuestCount()
        }

        private void SaveCurrentRecord()
        {
            if (currentCharacterId == null || CurrentRecord == null)
                return;

            this.configuration.PlaytimeRecords[currentCharacterId] = CurrentRecord;
            this.configuration.Save();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (CurrentRecord != null)
            {
                SaveCurrentRecord();
            }

            this.framework.Update -= OnFrameworkUpdate;
            this.clientState.Login -= OnLogin;
            this.clientState.Logout -= OnLogout;
            this.chatGui.ChatMessage -= OnChatMessage;

            disposed = true;
        }
    }
}
