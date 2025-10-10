using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Inventory;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Inventory;

public sealed class Inventory : IInventory
{
    private readonly List<InventorySlot> _slots;

    public int Capacity => _slots.Count;
    public IReadOnlyList<IInventorySlot> Slots => _slots;

    public Inventory(int capacity)
    {
        _slots = new List<InventorySlot>(capacity);
        for (int i = 0; i < capacity; i++)
            _slots.Add(new InventorySlot(i));
    }

    public bool CanAdd(IItem item, int quantity) => FindSlotInternal(item.Id, out _) || _slots.Any(s => s.IsEmpty);

    public bool TryAdd(IItem item, int quantity)
    {
        if (quantity <= 0) return false;
        if (FindSlotInternal(item.Id, out var slot))
        {
            if (!item.Stackable) return false;
            return slot!.TryAdd(item, quantity);
        }
        var empty = _slots.FirstOrDefault(s => s.IsEmpty);
        if (empty is null) return false;
        return empty.TryAdd(item, quantity);
    }

    public bool TryRemove(Guid itemId, int quantity)
    {
        if (!FindSlotInternal(itemId, out var slot) || slot is null) return false;
        return slot.TryRemove(quantity);
    }

    public IInventorySlot? FindSlot(Guid itemId)
        => FindSlotInternal(itemId, out var slot) ? slot : null;

    private bool FindSlotInternal(Guid itemId, out InventorySlot? slot)
    {
        slot = _slots.FirstOrDefault(s => !s.IsEmpty && s.Item!.Id == itemId);
        return slot != null;
    }
}
