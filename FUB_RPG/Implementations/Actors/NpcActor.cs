using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Actors;

/// <summary>
/// Represents a non-player character (NPC) on the map
/// </summary>
public sealed class NpcActor : ActorBase, INpc
{
    public string Dialogue { get; set; }
    public bool IsHostile { get; set; }
    public bool IsMerchant { get; set; }
    public bool IsQuestGiver { get; set; }
    public List<string> QuestIds { get; } = new();

    public NpcActor(string name, Species species, ActorClass cls, int x, int y, string dialogue = "Hello, traveler!")
        : base(name, species, cls, x, y)
    {
        Dialogue = dialogue;
        IsHostile = false;
        IsMerchant = false;
        IsQuestGiver = false;
    }

    public NpcActor(string name, Species species, ActorClass cls, int x, int y, Dictionary<StatType, double> customStats, string dialogue = "Hello, traveler!")
        : base(name, species, cls, x, y, customStats)
    {
        Dialogue = dialogue;
        IsHostile = false;
        IsMerchant = false;
        IsQuestGiver = false;
    }

    public void SetDialogue(string dialogue)
    {
        Dialogue = dialogue;
    }

    public void AddQuest(string questId)
    {
        if (!QuestIds.Contains(questId))
            QuestIds.Add(questId);
    }

    public void RemoveQuest(string questId)
    {
        QuestIds.Remove(questId);
    }
}

/// <summary>
/// Interface for NPC-specific functionality
/// </summary>
public interface INpc : IActor
{
    string Dialogue { get; }
    bool IsHostile { get; set; }
    bool IsMerchant { get; set; }
    bool IsQuestGiver { get; set; }
    List<string> QuestIds { get; }
    void SetDialogue(string dialogue);
    void AddQuest(string questId);
    void RemoveQuest(string questId);
}
