using System;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Progression;

namespace Fub.Implementations.Progression;

/// <summary>
/// Experience calculator with customizable leveling curves
/// </summary>
public class ExperienceCalculator : IExperienceCalculator
{
    /// <summary>
    /// Gets the total experience required to reach a specific level
    /// Level 2 = 100 XP, Level 3 = 175 XP, Level 4 = 250 XP, etc.
    /// Formula: 75 + (level - 1) * 75
    /// </summary>
    public long GetExperienceForLevel(int level, LevelCurveType curveType)
    {
        if (level <= 1) return 0;

        return curveType switch
        {
            LevelCurveType.Linear => CalculateLinear(level),
            LevelCurveType.Moderate => CalculateModerate(level),
            LevelCurveType.Steep => CalculateSteep(level),
            LevelCurveType.Custom => CalculateCustom(level),
            _ => CalculateModerate(level)
        };
    }

    /// <summary>
    /// Adds experience to an actor and returns the number of levels gained
    /// </summary>
    public int AddExperience(IActor actor, long amount, ExperienceSourceType sourceType)
    {
        // Apply source multipliers
        var multiplier = sourceType switch
        {
            ExperienceSourceType.Combat => 1.0,
            ExperienceSourceType.Quest => 1.5,
            ExperienceSourceType.Exploration => 0.8,
            ExperienceSourceType.Crafting => 0.7,
            _ => 1.0
        };

        var adjustedAmount = (long)(amount * multiplier);
        return actor.JobSystem.AddExperience(actor.Class, adjustedAmount) ? 1 : 0;
    }

    /// <summary>
    /// Linear progression: steady increase per level
    /// Level 2: 100, Level 3: 275, Level 4: 525, etc.
    /// </summary>
    private long CalculateLinear(int level)
    {
        // Cumulative XP needed to reach this level
        long totalXp = 0;
        for (int i = 2; i <= level; i++)
        {
            totalXp += 25 + (i * 75);
        }
        return totalXp;
    }

    /// <summary>
    /// Moderate progression: balanced curve
    /// Level 2: 100, Level 3: 250, Level 4: 450, etc.
    /// </summary>
    private long CalculateModerate(int level)
    {
        long totalXp = 0;
        for (int i = 2; i <= level; i++)
        {
            totalXp += 50 + ((i - 1) * 50);
        }
        return totalXp;
    }

    /// <summary>
    /// Steep progression: rapid increase at higher levels
    /// </summary>
    private long CalculateSteep(int level)
    {
        return (long)(100 * Math.Pow(level, 2.5));
    }

    /// <summary>
    /// Custom progression for fine-tuned control
    /// Your exact formula: 100 to reach L2, 175 to reach L3, 250 to reach L4
    /// </summary>
    private long CalculateCustom(int level)
    {
        if (level <= 1) return 0;
        
        // Cumulative XP: 100 + (level - 2) * 75
        return 100 + ((level - 2) * 75);
    }

    /// <summary>
    /// Gets experience needed for next level only (not cumulative)
    /// </summary>
    public long GetExperienceForNextLevel(int currentLevel, LevelCurveType curveType)
    {
        var currentTotal = GetExperienceForLevel(currentLevel, curveType);
        var nextTotal = GetExperienceForLevel(currentLevel + 1, curveType);
        return nextTotal - currentTotal;
    }

    /// <summary>
    /// Calculates how many enemies of a given level you need to defeat to level up
    /// </summary>
    public int GetEnemiesNeededForLevel(int playerLevel, int enemyLevel, LevelCurveType curveType)
    {
        var xpNeeded = GetExperienceForNextLevel(playerLevel, curveType);
        var xpPerEnemy = CalculateEnemyExperience(enemyLevel, playerLevel);
        return Math.Max(1, (int)Math.Ceiling((double)xpNeeded / xpPerEnemy));
    }

    /// <summary>
    /// Calculates experience reward from defeating an enemy
    /// </summary>
    public long CalculateEnemyExperience(int enemyLevel, int playerLevel)
    {
        // Base XP: 25 per enemy level
        var baseXp = enemyLevel * 25;

        // Level difference modifier
        var levelDiff = enemyLevel - playerLevel;
        var modifier = levelDiff switch
        {
            >= 5 => 1.5,  // Much higher level: +50% XP
            >= 3 => 1.25, // Higher level: +25% XP
            >= 1 => 1.1,  // Slightly higher: +10% XP
            0 => 1.0,     // Same level: normal XP
            >= -2 => 0.8, // Slightly lower: -20% XP
            >= -5 => 0.5, // Lower level: -50% XP
            _ => 0.1      // Much lower: -90% XP
        };

        return (long)(baseXp * modifier);
    }
}
