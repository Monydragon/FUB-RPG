using Fub.Interfaces.Inventory;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Inventory;

public sealed class InventorySlot : IInventorySlot
{
    public int Index { get; }
    public IItem? Item { get; private set; }
    public int Quantity { get; private set; }
    public bool IsEmpty => Item is null || Quantity <= 0;

    public InventorySlot(int index)
    {
        Index = index;
    }

    public bool TryAdd(IItem item, int quantity)
    {
        if (quantity <= 0) return false;
        if (IsEmpty)
        {
            Item = item;
            Quantity = quantity;
            return true;
        }
        if (Item!.Id == item.Id && item.Stackable)
        {
            Quantity += quantity;
            return true;
        }
        return false;
    }

    public bool TryRemove(int quantity)
    {
        if (IsEmpty || quantity <= 0 || quantity > Quantity) return false;
        Quantity -= quantity;
        if (Quantity == 0)
            Item = null;
        return true;
    }
}
