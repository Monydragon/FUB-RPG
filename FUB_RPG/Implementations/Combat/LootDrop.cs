using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Items;
using Fub.Implementations.Items;

namespace Fub.Implementations.Combat;

/// <summary>
/// Represents loot dropped from combat
/// </summary>
public class LootDrop
{
    public List<ItemStack> Items { get; } = new();
    public int ExperienceGained { get; set; }
    public decimal GoldGained { get; set; }
    public string Source { get; set; } = string.Empty;

    public decimal TotalValue => Items.Sum(stack => stack.TotalValue) + GoldGained;
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => !HasItems && ExperienceGained <= 0 && GoldGained <= 0;

    public void AddItem(IItem item, int quantity = 1)
    {
        if (item == null) return;

        // Try to stack with existing items
        if (item.Stackable)
        {
            var existingStack = Items.FirstOrDefault(stack => stack.Item.Id == item.Id);
            if (existingStack != null)
            {
                var added = existingStack.AddItems(quantity);
                quantity -= added;
            }
        }

        // Create new stacks for remaining quantity
        while (quantity > 0)
        {
            var stackSize = Math.Min(quantity, item.MaxStackSize);
            Items.Add(new ItemStack(item, stackSize));
            quantity -= stackSize;
        }
    }

    public void AddGold(decimal amount)
    {
        if (amount > 0)
            GoldGained += amount;
    }

    public void AddExperience(int amount)
    {
        if (amount > 0)
            ExperienceGained += amount;
    }

    public void Merge(LootDrop other)
    {
        if (other == null) return;

        foreach (var stack in other.Items)
        {
            AddItem(stack.Item, stack.Quantity);
        }

        AddGold(other.GoldGained);
        AddExperience(other.ExperienceGained);
    }
}
