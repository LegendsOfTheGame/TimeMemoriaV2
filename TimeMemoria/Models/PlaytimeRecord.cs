using System;

namespace TimeMemoria.Models
{
    /// <summary>
    /// Per-character playtime snapshot tracking session and lifetime play statistics.
    /// Used for calculating descriptive quest pacing metrics.
    /// </summary>
    public class PlaytimeRecord
    {
        /// <summary>
        /// Character identifier in format "CharacterName@WorldName".
        /// </summary>
        public string CharacterId { get; set; } = string.Empty;

        /// <summary>
        /// Total playtime for this character across all sessions (lifetime).
        /// </summary>
        public TimeSpan LifetimePlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Playtime accumulated during the current session (since login).
        /// Reset on logout or plugin reload.
        /// </summary>
        public TimeSpan SessionPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// UTC timestamp of the last playtime update.
        /// Used to calculate delta time between Framework updates.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total number of quests completed across all sessions (lifetime).
        /// </summary>
        public int TotalQuestsCompleted { get; set; } = 0;

        /// <summary>
        /// Number of quests completed during the current session.
        /// Reset on logout or plugin reload.
        /// </summary>
        public int SessionQuestsCompleted { get; set; } = 0;
    }
}
