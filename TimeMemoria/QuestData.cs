using System;
using System.Collections.Generic;

namespace TimeMemoria;

public class QuestData
{
    public string Title { get; set; } = string.Empty;
    public List<QuestData> Categories { get; set; } = new();
    public List<Quest> Quests { get; set; } = new();
    public float NumComplete { get; set; }
    public float Total { get; set; }
    public bool Hide { get; set; }
    
    // Lazy-loading metadata
    public string? BucketPath { get; set; }
    public string CompletionStrategy { get; set; } = "AlwaysLoad";
    public uint LastQuestId { get; set; }
    public List<uint>? AllQuestIds { get; set; }
    public int TotalQuests { get; set; }
    
    // UI message for empty buckets
    public string? EmptyMessage { get; set; }
}

[Serializable]
public class Quest
{
    public string Title { get; set; } = string.Empty;
    public List<uint> Id { get; set; } = new();
    public string Area { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string Gc { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool Hide { get; set; }
}
