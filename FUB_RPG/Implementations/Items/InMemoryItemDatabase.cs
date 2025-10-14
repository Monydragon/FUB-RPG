using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Items;

public sealed class InMemoryItemDatabase : IItemDatabase
{
    private readonly Dictionary<string, (IItem item, int price)> _byName = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IItem item, int price)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (price < 0) price = 0;
        _byName[item.Name] = (item, price);
    }

    public bool TryGet(string name, out IItem item, out int price)
    {
        if (_byName.TryGetValue(name, out var entry))
        {
            item = entry.item;
            price = entry.price;
            return true;
        }
        item = null!;
        price = 0;
        return false;
    }

    public IEnumerable<(IItem item, int price)> GetAll()
        => _byName.Values.ToList();
}
