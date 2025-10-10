using Fub.Enums;

namespace Fub.Interfaces.Generation;

public interface IMapGenerationConfig
{
    MapGenerationAlgorithm Algorithm { get; }
    int Width { get; }
    int Height { get; }
    int RoomAttempts { get; }
    int MinRoomSize { get; }
    int MaxRoomSize { get; }
    int? MaxCorridorLength { get; }
    int RandomSeed { get; }
    MapTheme Theme { get; }
    MapKind Kind { get; } // Added
    int RoomPadding { get; }          // Space kept between rooms (minimum tiles of separation)
    bool AllowRoomOverlap { get; }    // If true, overlapping attempts permitted (favor density)
    double CorridorCarveChance { get; } // Chance [0,1] to carve a corridor cell when linking rooms
    int MaxCorridorAttempts { get; }  // Attempts to connect disjoint rooms
    double DeadEndTrimChance { get; } // Chance to trim dead-end corridors
}
