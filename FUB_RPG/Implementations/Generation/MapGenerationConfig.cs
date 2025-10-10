using Fub.Enums;
using Fub.Interfaces.Generation;

namespace Fub.Implementations.Generation;

public sealed class MapGenerationConfig : IMapGenerationConfig
{
    public MapGenerationAlgorithm Algorithm { get; init; } = MapGenerationAlgorithm.DefaultRooms;
    public int Width { get; init; } = 50;
    public int Height { get; init; } = 20;
    public int RoomAttempts { get; init; } = 25;
    public int MinRooms { get; init; } = 5;
    public int MaxRooms { get; init; } = 15;
    public int MinRoomSize { get; init; } = 4;
    public int MaxRoomSize { get; init; } = 10;
    public int? MaxCorridorLength { get; init; } = 12;
    public int RandomSeed { get; init; } = Environment.TickCount;
    public MapTheme Theme { get; init; } = MapTheme.Dungeon;
    public MapKind Kind { get; init; } = MapKind.Dungeon; // Added
    public int RoomPadding { get; init; } = 1;
    public bool AllowRoomOverlap { get; init; } = false;
    public double CorridorCarveChance { get; init; } = 1.0; // Always carve for now
    public int MaxCorridorAttempts { get; init; } = 64;
    public double DeadEndTrimChance { get; init; } = 0.0; // Off by default
}
