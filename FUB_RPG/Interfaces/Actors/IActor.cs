using System;
using Fub.Enums;
using Fub.Interfaces.Combat;
using Fub.Interfaces.Entities;
using Fub.Interfaces.Inventory;
using Fub.Interfaces.Progression;
using Fub.Interfaces.Stats; // Added
// Added

// Added

namespace Fub.Interfaces.Actors;

/// <summary>
/// Any living (or pseudo-living) entity that can act on the map.
/// </summary>
public interface IActor : IEntity, IHasStats, IHasInventory, IHasJobSystem
{
    Species Species { get; }
    ActorClass Class { get; } // Base / chosen class
    ActorClass EffectiveClass { get; } // Added dynamic
    int Level { get; }
    long Experience { get; }

    /// <summary>Current map position (grid based).</summary>
    int X { get; }
    int Y { get; }

    /// <summary>Attempts to move. Returns true if the move request is valid.</summary>
    bool TryMove(int dx, int dy);

    /// <summary>Inflicts damage and returns remaining health.</summary>
    int TakeDamage(IDamagePacket packet);

    /// <summary>Heals a fixed amount (post-modifiers) and returns new health.</summary>
    int Heal(int amount);

    /// <summary>Sets the validator function for movement.</summary>
    void SetMovementValidator(Func<int, int, bool> validator); // Added
}

/// <summary>
/// Entity with job system for FFXIV-style class progression
/// </summary>
public interface IHasJobSystem
{
    IJobSystem JobSystem { get; }
}
