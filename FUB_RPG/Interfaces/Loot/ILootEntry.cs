using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Items;
using Fub.Interfaces.Random;

namespace Fub.Interfaces.Loot;

/// <summary>
/// Represents a single entry in a loot table
/// </summary>
public interface ILootEntry
{
    string ItemId { get; }
    float Weight { get; }
    int MinQuantity { get; }
    int MaxQuantity { get; }
    int MinLevel { get; }
    int MaxLevel { get; }
    Dictionary<string, object> Conditions { get; }
    LootEntryType EntryType { get; }
    LootRollType RollType { get; }
    RarityTier? RarityFilter { get; }
    double Chance { get; }
    IItem? Item { get; }
    string? TableReferenceId { get; }
    (int min, int max)? QuantityRange { get; }

    bool MeetsConditions(int playerLevel);
    int RollQuantity(IRandomSource random);
}
