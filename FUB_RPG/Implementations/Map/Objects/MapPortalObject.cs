using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map.Objects;

public sealed class MapPortalObject : EntityBase, IMapObject
{
    public MapObjectKind ObjectKind => MapObjectKind.Portal;
    public int X { get; }
    public int Y { get; }
    public IItem? Item => null;
    public IActor? Actor => null;

    /// <summary>
    /// Logical exit name that links to world connection.
    /// Equals Name for convenience.
    /// </summary>
    public string ExitName => Name;

    public MapPortalObject(string exitName, int x, int y) : base(exitName)
    {
        X = x;
        Y = y;
    }
}

