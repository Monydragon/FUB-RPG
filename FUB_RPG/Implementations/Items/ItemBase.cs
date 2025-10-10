using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Items;

public abstract class ItemBase : EntityBase, IItem
{
    public ItemType ItemType { get; protected set; }
    public RarityTier Rarity { get; protected set; }
    public string? Description { get; protected set; }
    public bool Stackable { get; protected set; }
    public int MaxStackSize { get; protected set; }
    public decimal BaseValue { get; protected set; }
    public float Weight { get; protected set; }

    protected ItemBase(string name, ItemType itemType, RarityTier rarity) : base(name)
    {
        ItemType = itemType;
        Rarity = rarity;
        Stackable = false;
        MaxStackSize = 1;
        BaseValue = 0m;
        Weight = 0f;
    }
}
