using Fub.Enums;
using Fub.Interfaces.Items;

namespace Fub.Interfaces.Loot;

public interface ILootEntry
{
    LootEntryType EntryType { get; }
    LootRollType RollType { get; }
    RarityTier? RarityFilter { get; }
    double Weight { get; }
    double Chance { get; }
    IItem? Item { get; }
    string? TableReferenceId { get; }
    (int min, int max)? QuantityRange { get; }
}

