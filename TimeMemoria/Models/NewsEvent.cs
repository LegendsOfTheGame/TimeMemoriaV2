// TimeMemoria/Models/NewsEvent.cs
using System;

namespace TimeMemoria.Models
{
    /// <summary>
    /// Represents a single news item fetched from LatestNews.json.
    /// All timestamps are stored as UTC Unix integers and converted locally for display.
    /// </summary>
    public class NewsEvent
    {
        // --- Maintenance ---
        public MaintenanceWindow? Maintenance { get; init; }
        public MaintenanceWindow? LastMaintenance { get; init; }

        // --- Events ---
        public GameEvent[] Events { get; init; } = Array.Empty<GameEvent>();
        public GameEvent? LastEvent { get; init; }

        // --- Meta ---
        public string? Version { get; init; }
        public long LastUpdated { get; init; }   // UTC Unix int
        public string? Source { get; init; }
    }

    public class MaintenanceWindow
    {
        public string? Title { get; init; }
        public long? Start { get; init; }   // UTC Unix int (nullable; may be absent)
        public long? End { get; init; }     // UTC Unix int
        public long? Time { get; init; }    // UTC Unix int (announcement time)
    }

    public class GameEvent
    {
        public string? Title { get; init; }
        public long? Start { get; init; }   // UTC Unix int
        public long? End { get; init; }     // UTC Unix int
    }
}
