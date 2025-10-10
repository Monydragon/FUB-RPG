using System.Collections.Generic;
using Fub.Interfaces.Items;

namespace Fub.Interfaces.Inventory;

public interface IInventory
{
    int Capacity { get; }
    IReadOnlyList<IInventorySlot> Slots { get; }
    bool CanAdd(IItem item, int quantity);
    bool TryAdd(IItem item, int quantity);
    bool TryRemove(Guid itemId, int quantity);
    IInventorySlot? FindSlot(Guid itemId);
}

