using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Inventory;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Inventory;

/// <summary>
/// Enhanced inventory system with item stack support using existing InventorySlot implementation
/// </summary>
public class Inventory : IInventory
{
    private readonly List<InventorySlot> _slots;
    
    public int Capacity { get; }
    public IReadOnlyList<IInventorySlot> Slots => _slots;
    public int UsedSlots => _slots.Count(s => !s.IsEmpty);
    public int FreeSlots => Capacity - UsedSlots;
    public decimal TotalValue => _slots.Where(s => !s.IsEmpty).Sum(s => (s.Item?.BaseValue ?? 0m) * s.Quantity);
    public float TotalWeight => _slots.Where(s => !s.IsEmpty).Sum(s => (s.Item?.Weight ?? 0f) * s.Quantity);

    public Inventory(int capacity)
    {
        if (capacity <= 0) throw new ArgumentException("Capacity must be positive", nameof(capacity));
        
        Capacity = capacity;
        _slots = new List<InventorySlot>(capacity);
        for (int i = 0; i < capacity; i++)
            _slots.Add(new InventorySlot(i));
    }

    public bool CanAdd(IItem item, int quantity)
    {
        if (quantity <= 0) return false;

        var remaining = quantity;

        // Fill existing stacks first
        if (item.Stackable)
        {
            foreach (var slot in _slots.Where(s => !s.IsEmpty && s.Item!.Id == item.Id))
            {
                var available = Math.Max(0, item.MaxStackSize - slot.Quantity);
                if (available <= 0) continue;
                var used = Math.Min(remaining, available);
                remaining -= used;
                if (remaining <= 0) return true;
            }
        }

        // Use empty slots for remaining
        var emptySlots = _slots.Count(s => s.IsEmpty);
        if (!item.Stackable)
        {
            return remaining <= emptySlots;
        }
        else
        {
            var stacksNeeded = (int)Math.Ceiling(remaining / (double)item.MaxStackSize);
            return stacksNeeded <= emptySlots;
        }
    }

    public bool TryAdd(IItem item, int quantity)
    {
        if (!CanAdd(item, quantity)) return false;
        var remaining = quantity;

        // Add to existing stacks
        if (item.Stackable)
        {
            foreach (var slot in _slots.Where(s => !s.IsEmpty && s.Item!.Id == item.Id))
            {
                var available = Math.Max(0, item.MaxStackSize - slot.Quantity);
                if (available <= 0) continue;
                var toAdd = Math.Min(remaining, available);
                if (toAdd > 0)
                {
                    slot.TryAdd(item, toAdd);
                    remaining -= toAdd;
                    if (remaining <= 0) return true;
                }
            }
        }

        // Add to empty slots
        foreach (var slot in _slots.Where(s => s.IsEmpty))
        {
            var toAdd = item.Stackable ? Math.Min(remaining, item.MaxStackSize) : 1;
            if (slot.TryAdd(item, toAdd))
            {
                remaining -= toAdd;
                if (remaining <= 0) return true;
            }
        }

        return remaining == 0;
    }

    public bool TryRemove(Guid itemId, int quantity)
    {
        if (quantity <= 0) return false;
        var available = _slots.Where(s => !s.IsEmpty && s.Item!.Id == itemId).Sum(s => s.Quantity);
        if (available < quantity) return false;

        var remaining = quantity;
        foreach (var slot in _slots.Where(s => !s.IsEmpty && s.Item!.Id == itemId))
        {
            var toRemove = Math.Min(remaining, slot.Quantity);
            if (slot.TryRemove(toRemove))
            {
                remaining -= toRemove;
                if (remaining <= 0) break;
            }
        }
        return remaining == 0;
    }

    public IInventorySlot? FindSlot(Guid itemId)
    {
        return _slots.FirstOrDefault(s => !s.IsEmpty && s.Item!.Id == itemId);
    }

    public int GetTotalQuantity(Guid itemId)
    {
        return _slots.Where(s => !s.IsEmpty && s.Item!.Id == itemId).Sum(s => s.Quantity);
    }

    public List<Fub.Implementations.Items.ItemStack> GetAllItems()
    {
        var list = new List<Fub.Implementations.Items.ItemStack>();
        foreach (var slot in _slots.Where(s => !s.IsEmpty))
        {
            if (slot.Item != null && slot.Quantity > 0)
                list.Add(new Fub.Implementations.Items.ItemStack(slot.Item, slot.Quantity));
        }
        return list;
    }

    public void Clear()
    {
        foreach (var slot in _slots)
        {
            if (!slot.IsEmpty)
                slot.TryRemove(slot.Quantity);
        }
    }

    public bool TryMoveItem(int fromSlotIndex, int toSlotIndex, int quantity = -1)
    {
        if (fromSlotIndex < 0 || fromSlotIndex >= Capacity || toSlotIndex < 0 || toSlotIndex >= Capacity || fromSlotIndex == toSlotIndex)
            return false;

        var from = _slots[fromSlotIndex];
        var to = _slots[toSlotIndex];
        if (from.IsEmpty) return false;

        var moveQty = quantity <= 0 ? from.Quantity : Math.Min(quantity, from.Quantity);
        var item = from.Item!;

        // If destination has incompatible item, fail
        if (!to.IsEmpty && (to.Item!.Id != item.Id || (!item.Stackable)))
            return false;

        // Calculate how many can be moved considering stack limits
        var destAvailable = to.IsEmpty ? (item.Stackable ? item.MaxStackSize : 1) : Math.Max(0, item.MaxStackSize - to.Quantity);
        var toMove = Math.Min(moveQty, destAvailable);
        if (toMove <= 0) return false;

        // Perform move
        if (!to.TryAdd(item, toMove)) return false;
        return from.TryRemove(toMove);
    }
}
