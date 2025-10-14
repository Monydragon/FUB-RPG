using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Map;
using Fub.Interfaces.Generation;
using Fub.Interfaces.Map;
using Fub.Implementations.Map.Objects; // Added for MapPortalObject

namespace Fub.Implementations.Generation;

/// <summary>
/// Enhanced map generator with support for cities, dungeons, forests, caves, and proper theming.
/// Generates larger, more varied maps with multiple exits.
/// </summary>
public sealed class EnhancedMapGenerator : IMapGenerator
{
    public IMap Generate(IMapGenerationConfig config, IProceduralSeed seed)
    {
        var rng = new System.Random(seed.Value);
        
        var map = config.Theme switch
        {
            MapTheme.City => GenerateCity(config, rng),
            MapTheme.Dungeon => GenerateDungeon(config, rng),
            MapTheme.Forest => GenerateForest(config, rng),
            MapTheme.Cave => GenerateCave(config, rng),
            MapTheme.Desert => GenerateDesert(config, rng),
            _ => GenerateGeneric(config, rng)
        };

        // Place default portals to ensure connectivity
        PlaceDefaultPortals((GameMap)map, rng);
        return map;
    }

    private IMap GenerateCity(IMapGenerationConfig config, System.Random rng)
    {
        var map = new GameMap($"City-{rng.Next(1000, 9999)}", config.Theme, config.Width, config.Height, config.Kind);
        
        // Create city blocks with streets
        int blockSize = rng.Next(8, 15);
        int streetWidth = 2;
        
        // Carve horizontal streets
        for (int y = streetWidth; y < config.Height; y += blockSize + streetWidth)
        {
            for (int x = 0; x < config.Width; x++)
            {
                for (int sw = 0; sw < streetWidth && y + sw < config.Height; sw++)
                    map.SetTile(x, y + sw, MapTileType.Floor);
            }
        }
        
        // Carve vertical streets
        for (int x = streetWidth; x < config.Width; x += blockSize + streetWidth)
        {
            for (int y = 0; y < config.Height; y++)
            {
                for (int sw = 0; sw < streetWidth && x + sw < config.Width; sw++)
                    map.SetTile(x + sw, y, MapTileType.Floor);
            }
        }
        
        // Create buildings (rooms) in blocks
        for (int by = streetWidth; by < config.Height; by += blockSize + streetWidth)
        {
            for (int bx = streetWidth; bx < config.Width; bx += blockSize + streetWidth)
            {
                // Building within block
                int buildingW = Math.Min(blockSize - 2, config.Width - bx - 2);
                int buildingH = Math.Min(blockSize - 2, config.Height - by - 2);
                
                if (buildingW > 3 && buildingH > 3)
                {
                    var room = new Room(RoomType.Shop, bx + 1, by + 1, buildingW, buildingH);
                    CarveRoom(map, room);
                    map.AddRoom(room);
                    
                    // Add door to street
                    int doorX = bx + rng.Next(2, buildingW - 1);
                    if (by > 0) map.SetTile(doorX, by, MapTileType.Floor);
                }
            }
        }
        
        return map;
    }

    private IMap GenerateDungeon(IMapGenerationConfig config, System.Random rng)
    {
        var map = new GameMap($"Dungeon-{rng.Next(1000, 9999)}", config.Theme, config.Width, config.Height, config.Kind);
        var rooms = new List<Room>();
        
        int roomCount = rng.Next(5, 15); // Use hardcoded range since MinRooms/MaxRooms not in interface
        
        // Generate rooms with corridors
        for (int i = 0; i < roomCount * 3; i++) // More attempts
        {
            if (rooms.Count >= roomCount) break;
            
            int w = rng.Next(config.MinRoomSize, config.MaxRoomSize + 1);
            int h = rng.Next(config.MinRoomSize, config.MaxRoomSize + 1);
            int x = rng.Next(2, Math.Max(3, config.Width - w - 2));
            int y = rng.Next(2, Math.Max(3, config.Height - h - 2));
            
            var newRoom = new Room(RoomType.Combat, x, y, w, h);
            
            if (!rooms.Any(r => Overlaps(r, newRoom, 2)))
            {
                CarveRoom(map, newRoom);
                rooms.Add(newRoom);
                map.AddRoom(newRoom);
                
                // Connect to previous room
                if (rooms.Count > 1)
                {
                    var prev = rooms[rooms.Count - 2];
                    ConnectRooms(map, prev, newRoom, rng);
                }
            }
        }
        
        // Add some dead-end corridors for exploration
        for (int i = 0; i < rooms.Count / 2; i++)
        {
            var room = rooms[rng.Next(rooms.Count)];
            var (cx, cy) = room.Center();
            int dir = rng.Next(4);
            int length = rng.Next(3, 8);
            
            switch (dir)
            {
                case 0: CarveCorridorHorizontal(map, cx, cx + length, cy); break;
                case 1: CarveCorridorHorizontal(map, cx, cx - length, cy); break;
                case 2: CarveCorridorVertical(map, cy, cy + length, cx); break;
                case 3: CarveCorridorVertical(map, cy, cy - length, cx); break;
            }
        }
        
        return map;
    }

    private IMap GenerateForest(IMapGenerationConfig config, System.Random rng)
    {
        var map = new GameMap($"Forest-{rng.Next(1000, 9999)}", config.Theme, config.Width, config.Height, config.Kind);
        
        // Use organic cellular automata for forest clearings
        var tiles = new bool[config.Width, config.Height];
        
        // Initialize with random floor/wall
        for (int x = 0; x < config.Width; x++)
            for (int y = 0; y < config.Height; y++)
                tiles[x, y] = rng.NextDouble() > 0.45;
        
        // Run cellular automata
        for (int iteration = 0; iteration < 4; iteration++)
        {
            var newTiles = new bool[config.Width, config.Height];
            for (int x = 1; x < config.Width - 1; x++)
            {
                for (int y = 1; y < config.Height - 1; y++)
                {
                    int neighbors = CountNeighbors(tiles, x, y);
                    newTiles[x, y] = neighbors >= 5 || (neighbors == 4 && tiles[x, y]);
                }
            }
            tiles = newTiles;
        }
        
        // Apply to map
        for (int x = 0; x < config.Width; x++)
            for (int y = 0; y < config.Height; y++)
                if (tiles[x, y])
                    map.SetTile(x, y, MapTileType.Floor);
        
        // Create clearings (rooms)
        int maxRooms = Math.Min(10, 10); // Use hardcoded value
        for (int i = 0; i < maxRooms; i++)
        {
            int w = rng.Next(5, 10);
            int h = rng.Next(5, 10);
            int x = rng.Next(2, Math.Max(3, config.Width - w - 2));
            int y = rng.Next(2, Math.Max(3, config.Height - h - 2));
            
            var room = new Room(RoomType.Rest, x, y, w, h);
            CarveRoom(map, room);
            map.AddRoom(room);
        }
        
        return map;
    }

    private IMap GenerateCave(IMapGenerationConfig config, System.Random rng)
    {
        var map = new GameMap($"Cave-{rng.Next(1000, 9999)}", config.Theme, config.Width, config.Height, config.Kind);
        
        // Cellular automata for organic cave structure
        var tiles = new bool[config.Width, config.Height];
        
        // Initialize
        for (int x = 0; x < config.Width; x++)
            for (int y = 0; y < config.Height; y++)
                tiles[x, y] = rng.NextDouble() > 0.52;
        
        // More iterations for smoother caves
        for (int iteration = 0; iteration < 6; iteration++)
        {
            var newTiles = new bool[config.Width, config.Height];
            for (int x = 1; x < config.Width - 1; x++)
            {
                for (int y = 1; y < config.Height - 1; y++)
                {
                    int neighbors = CountNeighbors(tiles, x, y);
                    newTiles[x, y] = neighbors >= 5;
                }
            }
            tiles = newTiles;
        }
        
        // Apply to map
        var floorTiles = new List<(int x, int y)>();
        for (int x = 0; x < config.Width; x++)
        {
            for (int y = 0; y < config.Height; y++)
            {
                if (tiles[x, y])
                {
                    map.SetTile(x, y, MapTileType.Floor);
                    floorTiles.Add((x, y));
                }
            }
        }
        
        // Ensure connectivity by carving paths between isolated areas
        if (floorTiles.Count > 0)
        {
            var start = floorTiles[rng.Next(floorTiles.Count)];
            for (int i = 0; i < 5; i++)
            {
                var end = floorTiles[rng.Next(floorTiles.Count)];
                ConnectPoints(map, start.x, start.y, end.x, end.y);
                start = end;
            }
        }
        
        // Add cave rooms
        int maxCaveRooms = Math.Min(5, 5); // Use hardcoded value
        for (int i = 0; i < maxCaveRooms; i++)
        {
            if (floorTiles.Count == 0) break;
            var tile = floorTiles[rng.Next(floorTiles.Count)];
            int w = rng.Next(4, 8);
            int h = rng.Next(4, 8);
            var room = new Room(RoomType.Treasure, tile.x - w/2, tile.y - h/2, w, h);
            map.AddRoom(room);
        }
        
        return map;
    }

    private IMap GenerateDesert(IMapGenerationConfig config, System.Random rng)
    {
        var map = new GameMap($"Desert-{rng.Next(1000, 9999)}", config.Theme, config.Width, config.Height, config.Kind);
        
        // Wide open spaces with occasional rocky outcrops
        for (int x = 0; x < config.Width; x++)
            for (int y = 0; y < config.Height; y++)
                map.SetTile(x, y, MapTileType.Floor);
        
        // Add rocky obstacles
        for (int i = 0; i < config.Width * config.Height / 50; i++)
        {
            int cx = rng.Next(config.Width);
            int cy = rng.Next(config.Height);
            int size = rng.Next(2, 6);
            
            for (int dx = -size; dx <= size; dx++)
            {
                for (int dy = -size; dy <= size; dy++)
                {
                    if (dx * dx + dy * dy <= size * size && rng.NextDouble() > 0.3)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (map.InBounds(x, y))
                            map.SetTile(x, y, MapTileType.Wall);
                    }
                }
            }
        }
        
        // Add oasis rooms
        int minOasisRooms = Math.Max(1, 3); // Use hardcoded value
        for (int i = 0; i < minOasisRooms; i++)
        {
            int w = rng.Next(6, 12);
            int h = rng.Next(6, 12);
            int x = rng.Next(2, Math.Max(3, config.Width - w - 2));
            int y = rng.Next(2, Math.Max(3, config.Height - h - 2));
            
            var room = new Room(RoomType.Rest, x, y, w, h);
            CarveRoom(map, room);
            map.AddRoom(room);
        }
        
        return map;
    }

    private IMap GenerateGeneric(IMapGenerationConfig config, System.Random rng)
    {
        var map = new GameMap($"Map-{rng.Next(1000, 9999)}", config.Theme, config.Width, config.Height, config.Kind);
        var rooms = new List<Room>();
        
        int roomCount = rng.Next(5, 15); // Use hardcoded range
        
        for (int i = 0; i < roomCount * 2; i++)
        {
            if (rooms.Count >= roomCount) break;
            
            int w = rng.Next(config.MinRoomSize, config.MaxRoomSize + 1);
            int h = rng.Next(config.MinRoomSize, config.MaxRoomSize + 1);
            int x = rng.Next(1, config.Width - w - 1);
            int y = rng.Next(1, config.Height - h - 1);
            
            var newRoom = new Room(RoomType.Corridor, x, y, w, h);
            
            if (!rooms.Any(r => Overlaps(r, newRoom, 1)))
            {
                CarveRoom(map, newRoom);
                rooms.Add(newRoom);
                map.AddRoom(newRoom);
            }
        }
        
        // Connect rooms
        for (int i = 1; i < rooms.Count; i++)
        {
            ConnectRooms(map, rooms[i - 1], rooms[i], rng);
        }
        
        return map;
    }

    private static bool Overlaps(Room a, Room b, int pad)
    {
        return !(b.X - pad >= a.X + a.Width + pad ||
                 b.X + b.Width + pad <= a.X - pad ||
                 b.Y - pad >= a.Y + a.Height + pad ||
                 b.Y + b.Height + pad <= a.Y - pad);
    }

    private static void CarveRoom(GameMap map, Room room)
    {
        for (int x = room.X; x < room.X + room.Width && x < map.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height && y < map.Height; y++)
                if (map.InBounds(x, y))
                    map.SetTile(x, y, MapTileType.Floor);
    }

    private static void ConnectRooms(GameMap map, Room a, Room b, System.Random rng)
    {
        var (x1, y1) = a.Center();
        var (x2, y2) = b.Center();
        
        if (rng.NextDouble() < 0.5)
        {
            CarveCorridorHorizontal(map, x1, x2, y1);
            CarveCorridorVertical(map, y1, y2, x2);
        }
        else
        {
            CarveCorridorVertical(map, y1, y2, x1);
            CarveCorridorHorizontal(map, x1, x2, y2);
        }
    }

    private static void CarveCorridorHorizontal(GameMap map, int x1, int x2, int y)
    {
        int min = Math.Min(x1, x2);
        int max = Math.Max(x1, x2);
        for (int x = min; x <= max; x++)
            if (map.InBounds(x, y))
                map.SetTile(x, y, MapTileType.Floor);
    }

    private static void CarveCorridorVertical(GameMap map, int y1, int y2, int x)
    {
        int min = Math.Min(y1, y2);
        int max = Math.Max(y1, y2);
        for (int y = min; y <= max; y++)
            if (map.InBounds(x, y))
                map.SetTile(x, y, MapTileType.Floor);
    }

    private static void ConnectPoints(GameMap map, int x1, int y1, int x2, int y2)
    {
        CarveCorridorHorizontal(map, x1, x2, y1);
        CarveCorridorVertical(map, y1, y2, x2);
    }

    private static int CountNeighbors(bool[,] tiles, int x, int y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    if (tiles[x + dx, y + dy])
                        count++;
        return count;
    }

    // Added: place two exits on floor tiles far apart
    private static void PlaceDefaultPortals(GameMap map, System.Random rng)
    {
        var floors = new List<(int x, int y)>();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    floors.Add((x, y));

        if (floors.Count < 2) return;

        // Pick one near top-left and one near bottom-right to maximize separation
        var a = floors
            .OrderBy(p => p.x + p.y)
            .First();
        var b = floors
            .OrderByDescending(p => p.x + p.y)
            .First();

        map.AddObject(new MapPortalObject("Exit-A", a.x, a.y));
        map.AddObject(new MapPortalObject("Exit-B", b.x, b.y));
    }
}
