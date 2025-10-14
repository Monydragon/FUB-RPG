using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map.Objects;

public sealed class MapShopObject : EntityBase, IMapObject
{
    public MapObjectKind ObjectKind => MapObjectKind.Interactable; // Treated as an interactable in the map
    public int X { get; }
    public int Y { get; }
    public IItem? Item => null;
    public IActor? Actor => null;

    // Themed shop category
    public ShopTheme Theme { get; }

    // Inventory entries with stock quantities
    public sealed class StockEntry
    {
        public IItem Item { get; }
        public int Price { get; }
        public int Quantity { get; private set; }
        public StockEntry(IItem item, int price, int quantity)
        {
            Item = item;
            Price = price;
            Quantity = quantity;
        }
        public bool TryTake(int amount)
        {
            if (amount <= 0) return false;
            if (Quantity < amount) return false;
            Quantity -= amount;
            return true;
        }
    }

    private readonly List<StockEntry> _inventory;
    public IReadOnlyList<StockEntry> Inventory => _inventory;

    public MapShopObject(string name, ShopTheme theme, IReadOnlyList<(IItem item, int price)> inventory, int x, int y) : base(name)
    {
        Theme = theme;
        // Back-compat: if only (item, price) provided, default quantity 1 per entry
        _inventory = inventory?.Select(e => new StockEntry(e.item, e.price, 1)).ToList() ?? new List<StockEntry>();
        X = x;
        Y = y;
    }

    public MapShopObject(string name, ShopTheme theme, IReadOnlyList<StockEntry> inventoryWithStock, int x, int y) : base(name)
    {
        Theme = theme;
        _inventory = inventoryWithStock?.ToList() ?? new List<StockEntry>();
        X = x;
        Y = y;
    }

    public bool TryTakeItem(string itemName, out (IItem item, int price) entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(itemName)) return false;
        var idx = _inventory.FindIndex(e => e.Item.Name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase) && e.Quantity > 0);
        if (idx < 0) return false;
        var e2 = _inventory[idx];
        if (!e2.TryTake(1)) return false;
        entry = (e2.Item, e2.Price);
        if (e2.Quantity <= 0) _inventory.RemoveAt(idx);
        return true;
    }

    public bool TryTakeItem(string itemName, int amount, out (IItem item, int price) entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0) return false;
        var idx = _inventory.FindIndex(e => e.Item.Name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase) && e.Quantity >= amount);
        if (idx < 0) return false;
        var e2 = _inventory[idx];
        if (!e2.TryTake(amount)) return false;
        entry = (e2.Item, e2.Price);
        if (e2.Quantity <= 0) _inventory.RemoveAt(idx);
        return true;
    }
}
