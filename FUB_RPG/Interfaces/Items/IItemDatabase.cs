using System.Collections.Generic;

namespace Fub.Interfaces.Items;

public interface IItemDatabase
{
    // Registers or upserts an item with a suggested shop price (in gold)
    void Register(IItem item, int price);

    // Tries to get an item and its price by case-insensitive name
    bool TryGet(string name, out IItem item, out int price);

    // Enumerate all items with prices (e.g., for shops)
    IEnumerable<(IItem item, int price)> GetAll();
}
