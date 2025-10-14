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

        // Find the source portal position to compute relative spawn if needed
        var sourcePortal = currentMap.Objects.FirstOrDefault(o => o.ObjectKind == MapObjectKind.Portal && string.Equals(o.Name, exitName, StringComparison.OrdinalIgnoreCase));
        int fromX = sourcePortal?.X ?? currentMap.Width / 2;
        int fromY = sourcePortal?.Y ?? currentMap.Height / 2;

        // Find spawn position
        int spawnX, spawnY;
        if (exitInfo.TargetX.HasValue && exitInfo.TargetY.HasValue)
        {
            spawnX = exitInfo.TargetX.Value;
            spawnY = exitInfo.TargetY.Value;
        }
        else
        {
            // Compute relative spawn based on exit direction keywords
            (spawnX, spawnY) = ComputeRelativeSpawn(currentMap, destMap, exitName, fromX, fromY);
            // Store for next time
            exitInfo.TargetX = spawnX;
            exitInfo.TargetY = spawnY;
            _exitRegistry[key] = exitInfo;
        }

        // Create return portal if it doesn't exist and register return mapping with precise coordinates
        EnsureReturnPortal(destMap, currentMap, exitName, exitInfo.ToMapKey, spawnX, spawnY, fromX, fromY);

        return (destMap, spawnX, spawnY);
    }

    private (int x, int y) ComputeRelativeSpawn(IMap source, IMap dest, string exitName, int fromX, int fromY)
    {
        double px = source.Width > 1 ? (double)fromX / (source.Width - 1) : 0.5;
        double py = source.Height > 1 ? (double)fromY / (source.Height - 1) : 0.5;
        int approxX = (int)Math.Round(px * (dest.Width - 1));
        int approxY = (int)Math.Round(py * (dest.Height - 1));

        // Determine edge to place on (opposite edge in destination)
        if (exitName.Contains("North", StringComparison.OrdinalIgnoreCase))
        {
            int y = FindEdgeFloorY(dest, dest.Height - 1, -1, approxX);
            return (approxX, y);
        }
        if (exitName.Contains("South", StringComparison.OrdinalIgnoreCase))
        {
            int y = FindEdgeFloorY(dest, 0, +1, approxX);
            return (approxX, y);
        }
        if (exitName.Contains("West", StringComparison.OrdinalIgnoreCase))
        {
            int x = FindEdgeFloorX(dest, dest.Width - 1, -1, approxY);
            return (x, approxY);
        }
        if (exitName.Contains("East", StringComparison.OrdinalIgnoreCase))
        {
            int x = FindEdgeFloorX(dest, 0, +1, approxY);
            return (x, approxY);
        }

        // Default: center-ish spawn
        return FindSpawnPosition(dest);
    }

    private void EnsureReturnPortal(IMap destMap, IMap sourceMap, string originalExitName, string destMapKey, int toX, int toY, int fromX, int fromY)
    {
        // Create return portal on the destination map at the arrival point
        string returnExitName = GetReturnExitName(originalExitName);

        int rx = Math.Clamp(toX, 0, destMap.Width - 1);
        int ry = Math.Clamp(toY, 0, destMap.Height - 1);
        if (destMap.GetTile(rx, ry).TileType != MapTileType.Floor)
        {
            // Fallback to nearest spawn tile on destination map if blocked
            var fallback = FindSpawnPosition(destMap);
            rx = fallback.x; ry = fallback.y;
        }

        if (!destMap.Objects.Any(o => o.ObjectKind == MapObjectKind.Portal && o.Name == returnExitName && o.X == rx && o.Y == ry))
        {
            destMap.AddObject(new MapPortalObject(returnExitName, rx, ry));
        }

        // Register the reverse connection: destination return exit -> source map at the source portal location
        var returnKey = $"{destMap.Id}:{returnExitName}";
        if (!_exitRegistry.ContainsKey(returnKey))
        {
            var sourceKey = GetMapKey(sourceMap);
            RegisterPortalConnection(destMap, returnExitName, sourceKey, fromX, fromY);
        }

        // Also ensure forward connection for the original exit is stored with exact arrival coordinates
        var forwardKey = $"{sourceMap.Id}:{originalExitName}";
        if (!_exitRegistry.ContainsKey(forwardKey))
        {
            RegisterPortalConnection(sourceMap, originalExitName, destMapKey, rx, ry);
        }
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
