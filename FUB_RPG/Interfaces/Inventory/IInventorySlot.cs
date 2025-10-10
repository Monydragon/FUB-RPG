using Fub.Interfaces.Items;

namespace Fub.Interfaces.Inventory;

public interface IInventorySlot
{
    int Index { get; }
    IItem? Item { get; }
    int Quantity { get; }
    bool IsEmpty { get; }
}

