using Fub.Enums;

namespace Fub.Interfaces.Map;

public interface IMapTile
{
    int X { get; }
    int Y { get; }
    MapTileType TileType { get; }
    MapVisibilityState Visibility { get; }
    bool Walkable { get; }
    bool BlocksLineOfSight { get; }
}

