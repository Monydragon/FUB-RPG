using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Entities;
using Fub.Interfaces.Items;

namespace Fub.Interfaces.Map;

public interface IMapObject : IEntity
{
    MapObjectKind ObjectKind { get; }
    int X { get; }
    int Y { get; }
    IItem? Item { get; }
    IActor? Actor { get; }
}

