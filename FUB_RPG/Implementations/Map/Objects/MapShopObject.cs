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

    // Inventory entries: item and its price in gold
    private readonly List<(IItem item, int price)> _inventory;
    public IReadOnlyList<(IItem item, int price)> Inventory => _inventory;

    public MapShopObject(string name, ShopTheme theme, IReadOnlyList<(IItem item, int price)> inventory, int x, int y) : base(name)
    {
        Theme = theme;
        _inventory = inventory?.ToList() ?? new List<(IItem, int)>();
        X = x;
        Y = y;
    }

    public bool TryTakeItem(string itemName, out (IItem item, int price) entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(itemName)) return false;
        var idx = _inventory.FindIndex(e => e.item.Name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;
        entry = _inventory[idx];
        _inventory.RemoveAt(idx);
        return true;
    }
}
