using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map.Objects;

public sealed class MapEnemyObject : EntityBase, IMapObject
{
    public MapObjectKind ObjectKind => MapObjectKind.Enemy;
    public int X { get; }
    public int Y { get; }
    public IItem? Item => null;
    public IActor? Actor { get; }

    public MapEnemyObject(string name, IActor actor, int x, int y) : base(name)
    {
        Actor = actor;
        X = x;
        Y = y;
    }
}
