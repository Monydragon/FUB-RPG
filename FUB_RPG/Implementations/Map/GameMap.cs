using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map;

public sealed class GameMap : IMap
{
    private readonly MapTile[,] _tiles;
    private readonly List<IRoom> _rooms = new();
    private readonly List<IMapObject> _objects = new(); // Added

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public MapTheme Theme { get; }
    public MapKind Kind { get; } // Added
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<IRoom> Rooms => _rooms;
    public IReadOnlyList<IMapObject> Objects => _objects; // Added

    public GameMap(string name, MapTheme theme, int width, int height, MapKind kind = MapKind.Dungeon)
    {
        Name = name;
        Theme = theme;
        Kind = kind;
        Width = width;
        Height = height;
        _tiles = new MapTile[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _tiles[x, y] = new MapTile(x, y, MapTileType.Wall);
    }

    public void AddRoom(IRoom room) => _rooms.Add(room);

    public void AddObject(IMapObject obj) => _objects.Add(obj); // Added

    public void SetTile(int x, int y, MapTileType type)
    {
        if (InBounds(x, y))
            _tiles[x, y].SetType(type);
    }

    public IMapTile GetTile(int x, int y) => _tiles[x, y];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public IReadOnlyList<IMapObject> GetObjectsAt(int x, int y)
        => _objects.Where(o => o.X == x && o.Y == y).ToList();

    public bool RemoveObject(Guid objectId)
    {
        var idx = _objects.FindIndex(o => o.Id == objectId);
        if (idx >= 0) { _objects.RemoveAt(idx); return true; }
        return false;
    }
}
