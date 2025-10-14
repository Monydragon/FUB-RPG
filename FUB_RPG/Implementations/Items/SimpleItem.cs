using Fub.Enums;
using Fub.Implementations.Items;

namespace Fub.Implementations.Items;

public sealed class SimpleItem : ItemBase
{
    public SimpleItem(
        string name, 
        ItemType itemType, 
        RarityTier rarity, 
        bool stackable = false, 
        int maxStackSize = 1, 
        decimal baseValue = 0m, 
        float weight = 1f,
        string? description = null) 
        : base(name, itemType, rarity)
    {
        Stackable = stackable;
        MaxStackSize = stackable ? maxStackSize : 1;
        BaseValue = baseValue;
        Weight = weight;
        Description = description;
    }
}
