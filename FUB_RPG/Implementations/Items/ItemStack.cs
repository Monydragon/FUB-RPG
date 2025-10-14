using System;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Items;

/// <summary>
/// Represents a stack of items with quantity tracking
/// </summary>
public class ItemStack
{
    public IItem Item { get; }
    public int Quantity { get; private set; }
    public decimal TotalValue => Item.BaseValue * Quantity;
    public float TotalWeight => Item.Weight * Quantity;
    public bool IsEmpty => Quantity <= 0;
    public bool IsFull => Quantity >= Item.MaxStackSize;
    public int RemainingSpace => Item.MaxStackSize - Quantity;

    public ItemStack(IItem item, int quantity = 1)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        if (quantity < 0) throw new ArgumentException("Quantity cannot be negative", nameof(quantity));
        if (!item.Stackable && quantity > 1) 
            throw new ArgumentException("Cannot stack non-stackable items", nameof(quantity));
        
        Quantity = Math.Min(quantity, item.MaxStackSize);
    }

    /// <summary>
    /// Attempts to add items to this stack
    /// </summary>
    /// <param name="amount">Amount to add</param>
    /// <returns>Amount actually added</returns>
    public int AddItems(int amount)
    {
        if (amount <= 0) return 0;
        if (!Item.Stackable) return 0;
        
        var canAdd = Math.Min(amount, RemainingSpace);
        Quantity += canAdd;
        return canAdd;
    }

    /// <summary>
    /// Attempts to remove items from this stack
    /// </summary>
    /// <param name="amount">Amount to remove</param>
    /// <returns>Amount actually removed</returns>
    public int RemoveItems(int amount)
    {
        if (amount <= 0) return 0;
        
        var canRemove = Math.Min(amount, Quantity);
        Quantity -= canRemove;
        return canRemove;
    }

    /// <summary>
    /// Splits this stack into two stacks
    /// </summary>
    /// <param name="splitAmount">Amount to split off</param>
    /// <returns>New stack with split amount, or null if split failed</returns>
    public ItemStack? Split(int splitAmount)
    {
        if (splitAmount <= 0 || splitAmount >= Quantity) return null;
        
        Quantity -= splitAmount;
        return new ItemStack(Item, splitAmount);
    }

    /// <summary>
    /// Attempts to merge another stack into this one
    /// </summary>
    /// <param name="other">Stack to merge</param>
    /// <returns>Amount merged</returns>
    public int Merge(ItemStack other)
    {
        if (other == null || !CanStackWith(other)) return 0;
        
        var merged = AddItems(other.Quantity);
        other.RemoveItems(merged);
        return merged;
    }

    /// <summary>
    /// Checks if this stack can be combined with another
    /// </summary>
    public bool CanStackWith(ItemStack other)
    {
        return other != null && 
               Item.Stackable && 
               Item.Id == other.Item.Id && 
               !IsFull;
    }

    public ItemStack Clone()
    {
        return new ItemStack(Item, Quantity);
    }

    public override string ToString()
    {
        return Quantity > 1 ? $"{Item.Name} x{Quantity}" : Item.Name;
    }
}
