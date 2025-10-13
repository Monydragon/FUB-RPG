using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Generation;
using Fub.Implementations.Map.Objects;
using Fub.Interfaces.Generation;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map;

/// <summary>
/// Registry for managing endless map generation with multiple exits and proper interconnections.
/// Maps are generated on-demand when accessed via portals.
/// </summary>
public sealed class MapRegistry
{
    private readonly Dictionary<string, IMap> _loadedMaps = new();
    private readonly Dictionary<string, MapExitInfo> _exitRegistry = new();
    private readonly IMapGenerator _generator;
    private readonly World _world;
    private readonly System.Random _rng;

    public MapRegistry(World world, IMapGenerator generator)
    {
        _world = world;
        _generator = generator;
        _rng = new System.Random(world.Seed);
    }

    /// <summary>
    /// Get or generate a map by its coordinate identifier
    /// </summary>
    public IMap GetOrGenerateMap(string mapKey)
    {
        if (_loadedMaps.TryGetValue(mapKey, out var existing))
            return existing;

        var map = GenerateMapForKey(mapKey);
        _loadedMaps[mapKey] = map;
        _world.AddMap(map);
        
        // Create exits for this map
        CreateExitsForMap(map, mapKey);
        
        return map;
    }

    /// <summary>
    /// Create a starting town/city map
    /// </summary>
    public IMap CreateStartingCity()
    {
        var mapKey = "city_start";
        var config = new MapGenerationConfig
        {
            Width = 60,
            Height = 40,
            Theme = MapTheme.City,
            Kind = MapKind.Town,
            MinRooms = 8,
            MaxRooms = 15,
            MinRoomSize = 5,
            MaxRoomSize = 12
        };
        
        var map = _generator.Generate(config, new ProceduralSeed(_world.Seed ^ GetHashForKey(mapKey)));
        _loadedMaps[mapKey] = map;
        _world.AddMap(map);
        
        // Add multiple exits to different areas
        CreateExitsForMap(map, mapKey);
        
        return map;
    }

    /// <summary>
    /// Register a portal connection between two maps
    /// </summary>
    public void RegisterPortalConnection(IMap fromMap, string exitName, string toMapKey, int? toX = null, int? toY = null)
    {
        var exitInfo = new MapExitInfo
        {
            FromMapId = fromMap.Id,
            ExitName = exitName,
            ToMapKey = toMapKey,
            TargetX = toX,
            TargetY = toY
        };
        
        _exitRegistry[$"{fromMap.Id}:{exitName}"] = exitInfo;
    }

    /// <summary>
    /// Use a portal and get/generate the destination map
    /// </summary>
    public (IMap map, int x, int y) UsePortal(IMap currentMap, string exitName)
    {
        var key = $"{currentMap.Id}:{exitName}";
        
        if (!_exitRegistry.TryGetValue(key, out var exitInfo))
        {
            // Generate default destination based on exit name
            exitInfo = GenerateDefaultExit(currentMap, exitName);
            _exitRegistry[key] = exitInfo;
        }
        
        var destMap = GetOrGenerateMap(exitInfo.ToMapKey);
        
        // Find spawn position
        int spawnX, spawnY;
        if (exitInfo.TargetX.HasValue && exitInfo.TargetY.HasValue)
        {
            spawnX = exitInfo.TargetX.Value;
            spawnY = exitInfo.TargetY.Value;
        }
        else
        {
            (spawnX, spawnY) = FindSpawnPosition(destMap);
        }
        
        // Create return portal if it doesn't exist
        EnsureReturnPortal(destMap, currentMap, exitName, exitInfo.ToMapKey);
        
        return (destMap, spawnX, spawnY);
    }

    private MapExitInfo GenerateDefaultExit(IMap currentMap, string exitName)
    {
        string toMapKey;
        MapTheme theme;
        MapKind kind;
        
        // Determine destination based on exit name and current map
        if (exitName.Contains("North") || exitName.Contains("South") || 
            exitName.Contains("East") || exitName.Contains("West") || 
            exitName.Contains("Edge"))
        {
            // Overworld connection
            toMapKey = GenerateOverworldKey(currentMap, exitName);
            theme = MapTheme.Forest;
            kind = MapKind.Overworld;
        }
        else if (exitName.Contains("Dungeon") || exitName.Contains("Cave"))
        {
            toMapKey = $"dungeon_{Guid.NewGuid():N}";
            theme = MapTheme.Dungeon;
            kind = MapKind.Dungeon;
        }
        else if (exitName.Contains("Town") || exitName.Contains("City"))
        {
            toMapKey = $"city_{Guid.NewGuid():N}";
            theme = MapTheme.City;
            kind = MapKind.Town;
        }
        else if (exitName.Contains("Desert"))
        {
            toMapKey = $"desert_{Guid.NewGuid():N}";
            theme = MapTheme.Desert;
            kind = MapKind.Overworld;
        }
        else
        {
            // Default to overworld
            toMapKey = $"overworld_{Guid.NewGuid():N}";
            theme = MapTheme.Forest;
            kind = MapKind.Overworld;
        }
        
        return new MapExitInfo
        {
            FromMapId = currentMap.Id,
            ExitName = exitName,
            ToMapKey = toMapKey,
            Theme = theme,
            Kind = kind
        };
    }

    private string GenerateOverworldKey(IMap currentMap, string exitName)
    {
        // Parse current coordinates if possible
        var name = currentMap.Name.ToLowerInvariant();
        int curX = 0, curY = 0;
        
        if (name.Contains("overworld"))
        {
            // Try to extract coordinates
            var parts = name.Split('_');
            if (parts.Length >= 3)
            {
                int.TryParse(parts[1], out curX);
                int.TryParse(parts[2], out curY);
            }
        }
        
        // Calculate new coordinates based on direction
        if (exitName.Contains("North")) curY--;
        else if (exitName.Contains("South")) curY++;
        else if (exitName.Contains("West")) curX--;
        else if (exitName.Contains("East")) curX++;
        
        return $"overworld_{curX}_{curY}";
    }

    private IMap GenerateMapForKey(string mapKey)
    {
        MapTheme theme = MapTheme.Dungeon;
        MapKind kind = MapKind.Dungeon;
        int width = 60;
        int height = 40;
        
        if (mapKey.StartsWith("city"))
        {
            theme = MapTheme.City;
            kind = MapKind.Town;
            width = 60;
            height = 40;
        }
        else if (mapKey.StartsWith("overworld"))
        {
            theme = _rng.NextDouble() < 0.7 ? MapTheme.Forest : MapTheme.Desert;
            kind = MapKind.Overworld;
            width = 60;
            height = 40;
        }
        else if (mapKey.StartsWith("dungeon"))
        {
            theme = _rng.NextDouble() < 0.5 ? MapTheme.Dungeon : MapTheme.Cave;
            kind = MapKind.Dungeon;
            width = 60;
            height = 45;
        }
        else if (mapKey.StartsWith("desert"))
        {
            theme = MapTheme.Desert;
            kind = MapKind.Overworld;
            width = 60;
            height = 40;
        }
        
        var config = new MapGenerationConfig
        {
            Width = width,
            Height = height,
            Theme = theme,
            Kind = kind,
            MinRooms = 6,
            MaxRooms = 15,
            MinRoomSize = 4,
            MaxRoomSize = 12
        };
        
        return _generator.Generate(config, new ProceduralSeed(_world.Seed ^ GetHashForKey(mapKey)));
    }

    private void CreateExitsForMap(IMap map, string mapKey)
    {
        var rooms = map.Rooms.ToList();
        
        int exitCount = map.Kind switch
        {
            MapKind.Town => _rng.Next(4, 8),      // Cities have many exits
            MapKind.Overworld => 4,               // Overworld always has 4 cardinal exits
            MapKind.Dungeon => _rng.Next(2, 5),   // Dungeons have 2-4 exits
            _ => _rng.Next(2, 4)
        };
        
        if (map.Kind == MapKind.Overworld)
        {
            // Always create 4 edge exits for endless exploration
            CreateEdgeExit(map, "North Edge", 0);
            CreateEdgeExit(map, "South Edge", 2);
            CreateEdgeExit(map, "West Edge", 3);
            CreateEdgeExit(map, "East Edge", 1);
            return;
        }
        
        if (rooms.Count == 0) return;
        
        // Create varied exits in rooms
        var usedRooms = new HashSet<IRoom>();
        var exitTypes = new[] { "Exit", "Portal", "Passage", "Doorway", "Gateway" };
        
        int maxExits = System.Math.Min(exitCount, rooms.Count);
        for (int i = 0; i < maxExits; i++)
        {
            var room = rooms[_rng.Next(rooms.Count)];
            while (usedRooms.Contains(room) && usedRooms.Count < rooms.Count)
                room = rooms[_rng.Next(rooms.Count)];
            
            usedRooms.Add(room);
            int x = room.X + room.Width / 2;
            int y = room.Y + room.Height / 2;
            
            string exitName = map.Kind == MapKind.Town 
                ? $"{exitTypes[_rng.Next(exitTypes.Length)]} {i + 1}"
                : $"{exitTypes[_rng.Next(exitTypes.Length)]}";
            
            map.AddObject(new MapPortalObject(exitName, x, y));
        }
    }

    private void CreateEdgeExit(IMap map, string exitName, int edge)
    {
        int x, y;
        
        // Find appropriate position on edge
        switch (edge)
        {
            case 0: // North
                x = map.Width / 2;
                y = FindEdgeFloorY(map, 0, 1, x);
                break;
            case 1: // East
                x = FindEdgeFloorX(map, map.Width - 1, -1, map.Height / 2);
                y = map.Height / 2;
                break;
            case 2: // South
                x = map.Width / 2;
                y = FindEdgeFloorY(map, map.Height - 1, -1, x);
                break;
            default: // West
                x = FindEdgeFloorX(map, 0, 1, map.Height / 2);
                y = map.Height / 2;
                break;
        }
        
        map.AddObject(new MapPortalObject(exitName, x, y));
    }

    private int FindEdgeFloorY(IMap map, int startY, int dir, int x)
    {
        for (int y = startY; y >= 0 && y < map.Height; y += dir)
        {
            if (map.GetTile(x, y).TileType == MapTileType.Floor)
                return y;
        }
        return Math.Clamp(startY, 0, map.Height - 1);
    }

    private int FindEdgeFloorX(IMap map, int startX, int dir, int y)
    {
        for (int x = startX; x >= 0 && x < map.Width; x += dir)
        {
            if (map.GetTile(x, y).TileType == MapTileType.Floor)
                return x;
        }
        return Math.Clamp(startX, 0, map.Width - 1);
    }

    private (int x, int y) FindSpawnPosition(IMap map)
    {
        var rooms = map.Rooms.ToList();
        if (rooms.Count > 0)
        {
            var room = rooms[_rng.Next(rooms.Count)];
            int x = room.X + room.Width / 2;
            int y = room.Y + room.Height / 2;
            return (x, y);
        }
        
        // Find any floor tile
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    return (x, y);
        
        return (map.Width / 2, map.Height / 2);
    }

    private void EnsureReturnPortal(IMap destMap, IMap sourceMap, string originalExitName, string destMapKey)
    {
        // Create return portal if it doesn't exist
        string returnExitName = GetReturnExitName(originalExitName);
        
        if (!destMap.Objects.Any(o => o.ObjectKind == MapObjectKind.Portal && o.Name == returnExitName))
        {
            var (x, y) = FindSpawnPosition(destMap);
            destMap.AddObject(new MapPortalObject(returnExitName, x, y));
        }
        
        // Register the return connection
        var returnKey = $"{destMap.Id}:{returnExitName}";
        if (!_exitRegistry.ContainsKey(returnKey))
        {
            var sourceKey = GetMapKey(sourceMap);
            RegisterPortalConnection(destMap, returnExitName, sourceKey);
        }
    }

    private string GetReturnExitName(string originalName)
    {
        if (originalName.Contains("North")) return "South Edge";
        if (originalName.Contains("South")) return "North Edge";
        if (originalName.Contains("East")) return "West Edge";
        if (originalName.Contains("West")) return "East Edge";
        return "Return";
    }

    private string GetMapKey(IMap map)
    {
        // Try to find existing key
        foreach (var kvp in _loadedMaps)
        {
            if (kvp.Value.Id == map.Id)
                return kvp.Key;
        }
        
        // Generate new key based on map properties
        return $"{map.Kind.ToString().ToLower()}_{map.Id:N}";
    }

    private int GetHashForKey(string key)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in key)
                hash = hash * 31 + c;
            return hash;
        }
    }
}

public class MapExitInfo
{
    public Guid FromMapId { get; set; }
    public string ExitName { get; set; } = string.Empty;
    public string ToMapKey { get; set; } = string.Empty;
    public int? TargetX { get; set; }
    public int? TargetY { get; set; }
    public MapTheme Theme { get; set; }
    public MapKind Kind { get; set; }
}
