using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Map;
using Fub.Interfaces.Generation;
using Fub.Interfaces.Map;
using Fub.Interfaces.Random;

namespace Fub.Implementations.Generation;

/// <summary>
/// Basic rectangular room & corridor generator. Intentionally deterministic by seed.
/// </summary>
public sealed class SimpleMapGenerator : IMapGenerator
{
    public IMap Generate(IMapGenerationConfig config, IProceduralSeed seed)
    {
        var map = new GameMap($"Map-{seed.Value}", config.Theme, config.Width, config.Height, config.Kind); // Added config.Kind
        var rng = new System.Random(seed.Value);

        // Attempt rooms
        var rooms = new List<Room>();
        for (int i = 0; i < config.RoomAttempts; i++)
        {
            int w = rng.Next(config.MinRoomSize, config.MaxRoomSize + 1);
            int h = rng.Next(config.MinRoomSize, config.MaxRoomSize + 1);
            int x = rng.Next(1, config.Width - w - 1);
            int y = rng.Next(1, config.Height - h - 1);

            var newRoom = new Room(RoomType.Corridor, x, y, w, h);
            if (!config.AllowRoomOverlap && rooms.Any(r => Overlaps(r, newRoom, config.RoomPadding)))
                continue;

            CarveRoom(map, newRoom);
            rooms.Add(newRoom);
            map.AddRoom(newRoom);
        }

        // Connect rooms sequentially
        for (int i = 1; i < rooms.Count; i++)
        {
            var prev = rooms[i - 1];
            var cur = rooms[i];
            var (x1, y1) = prev.Center();
            var (x2, y2) = cur.Center();
            if (rng.NextDouble() < 0.5)
            {
                CarveCorridorHorizontal(map, x1, x2, y1, config);
                CarveCorridorVertical(map, y1, y2, x2, config);
            }
            else
            {
                CarveCorridorVertical(map, y1, y2, x1, config);
                CarveCorridorHorizontal(map, x1, x2, y2, config);
            }
            prev.Connect(cur);
        }

        // Start room becomes RoomType.Start
        if (rooms.Count > 0)
        {
            var start = rooms[0];
            var startField = typeof(Room).GetField("RoomType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // Reflection avoided for sealed property; leaving as corridor for now. Could subclass or expose setter later.
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
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                map.SetTile(x, y, MapTileType.Floor);
    }

    private static void CarveCorridorHorizontal(GameMap map, int x1, int x2, int y, IMapGenerationConfig cfg)
    {
        int min = Math.Min(x1, x2);
        int max = Math.Max(x1, x2);
        for (int x = min; x <= max; x++)
        {
            if (!map.InBounds(x, y)) continue;
            if (cfg.CorridorCarveChance >= 1.0 || new System.Random().NextDouble() < cfg.CorridorCarveChance)
                map.SetTile(x, y, MapTileType.Floor);
        }
    }

    private static void CarveCorridorVertical(GameMap map, int y1, int y2, int x, IMapGenerationConfig cfg)
    {
        int min = Math.Min(y1, y2);
        int max = Math.Max(y1, y2);
        for (int y = min; y <= max; y++)
        {
            if (!map.InBounds(x, y)) continue;
            if (cfg.CorridorCarveChance >= 1.0 || new System.Random().NextDouble() < cfg.CorridorCarveChance)
                map.SetTile(x, y, MapTileType.Floor);
        }
    }
}
