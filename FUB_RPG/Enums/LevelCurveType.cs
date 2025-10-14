namespace Fub.Enums;

public enum LevelCurveType
{
    Linear,        // Steady +75 XP per level
    Moderate,      // Balanced progression with +50 XP increases
    Steep,         // Exponential growth (old system)
    Exponential,   // Same as Steep
    Accelerating,  // Gradual acceleration
    Custom         // Your custom formula: 100, 175, 250, 325...
}
