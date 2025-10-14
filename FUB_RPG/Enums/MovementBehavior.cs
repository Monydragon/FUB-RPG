namespace Fub.Enums;

/// <summary>
/// Defines how an entity moves on the map
/// </summary>
public enum MovementBehavior
{
    /// <summary>Does not move</summary>
    Stationary,
    
    /// <summary>Wanders randomly within a certain radius</summary>
    Roaming,
    
    /// <summary>Patrols between fixed waypoints</summary>
    Patrol,
    
    /// <summary>Follows a specific target (player/enemy)</summary>
    Chase,
    
    /// <summary>Moves away from a target</summary>
    Flee,
    
    /// <summary>Guards a specific location</summary>
    Guard,
    
    /// <summary>Custom behavior controlled by script</summary>
    Custom
}

