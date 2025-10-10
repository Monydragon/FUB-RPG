using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map.Objects;

public sealed class MapItemObject : EntityBase, IMapObject
{
    public MapObjectKind ObjectKind => MapObjectKind.Item;
    public int X { get; }
    public int Y { get; }
    public IItem? Item { get; }
    public IActor? Actor => null;

    public MapItemObject(string name, IItem item, int x, int y) : base(name)
    {
        Item = item;
        X = x;
        Y = y;
    }
}
