using Fub.Enums;
using Fub.Interfaces.Entities;

namespace Fub.Interfaces.Items;

/// <summary>
/// Base item representation. All balancing / numeric values will be data-driven later.
/// </summary>
public interface IItem : IEntity
{
    ItemType ItemType { get; }
    RarityTier Rarity { get; }
    string? Description { get; }
    bool Stackable { get; }
    int MaxStackSize { get; }
    decimal BaseValue { get; }
    float Weight { get; }
}

