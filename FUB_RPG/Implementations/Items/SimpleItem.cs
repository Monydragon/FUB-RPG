using Fub.Enums;
using Fub.Implementations.Items;

namespace Fub.Implementations.Items;

public sealed class SimpleItem : ItemBase
{
    public SimpleItem(string name, string? description = null, RarityTier rarity = RarityTier.Common, bool stackable = false, int maxStack = 1, decimal baseValue = 0m, float weight = 0f)
        : base(name, ItemType.Generic, rarity)
    {
        Description = description;
        Stackable = stackable;
        MaxStackSize = maxStack;
        BaseValue = baseValue;
        Weight = weight;
    }
}
