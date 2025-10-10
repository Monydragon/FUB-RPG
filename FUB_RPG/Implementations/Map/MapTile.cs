using Fub.Enums;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map;

public sealed class MapTile : IMapTile
{
    public int X { get; }
    public int Y { get; }
    public MapTileType TileType { get; private set; }
    public MapVisibilityState Visibility { get; private set; } = MapVisibilityState.Visible; // Simplified for now
    public bool Walkable => TileType is MapTileType.Floor or MapTileType.DoorOpen or MapTileType.StairsDown or MapTileType.StairsUp;
    public bool BlocksLineOfSight => TileType switch
    {
        MapTileType.Wall or MapTileType.DoorClosed => true,
        _ => false
    };

    public MapTile(int x, int y, MapTileType type)
    {
        X = x;
        Y = y;
        TileType = type;
    }

    public void SetType(MapTileType t) => TileType = t;
    public void Reveal() => Visibility = MapVisibilityState.Visible;
}
