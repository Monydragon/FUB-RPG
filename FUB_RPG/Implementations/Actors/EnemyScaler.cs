using System;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Implementations.Stats;

namespace Fub.Implementations.Actors;

/// <summary>
/// Scales enemy stats based on player level and difficulty
/// </summary>
public class EnemyScaler
{
    /// <summary>
    /// Scales an enemy's stats based on target level and difficulty
    /// </summary>
    public static void ScaleEnemy(IMonster enemy, int targetLevel, Difficulty difficulty = Difficulty.Normal)
    {
        if (enemy == null) throw new ArgumentNullException(nameof(enemy));

        var currentLevel = enemy.Level;
        if (currentLevel >= targetLevel)
            return; // Don't downscale

        var levelDiff = targetLevel - currentLevel;
        var scaleFactor = 1.0 + (levelDiff * 0.1); // 10% increase per level

        // Apply difficulty multipliers
        var difficultyMultiplier = difficulty switch
        {
            Difficulty.Story => 0.75,
            Difficulty.Normal => 1.0,
            Difficulty.Hard => 1.35,
            Difficulty.Ultra => 1.75,
            Difficulty.Nightmare => 2.5,
            _ => 1.0
        };

        scaleFactor *= difficultyMultiplier;

        // Elite and Boss modifiers
        if (enemy.IsElite)
            scaleFactor *= 1.5;
        if (enemy.IsBoss)
            scaleFactor *= 2.5;

        // Scale primary stats
        ScaleStat(enemy, StatType.Health, scaleFactor);
        ScaleStat(enemy, StatType.Mana, scaleFactor);
        ScaleStat(enemy, StatType.Technical, scaleFactor);
        ScaleStat(enemy, StatType.AttackPower, scaleFactor * 0.8); // Slightly lower damage scaling
        ScaleStat(enemy, StatType.Armor, scaleFactor);
        ScaleStat(enemy, StatType.SpellPower, scaleFactor * 0.8);
        ScaleStat(enemy, StatType.Speed, scaleFactor * 0.5); // Less speed scaling
    }

    /// <summary>
    /// Gets a recommended enemy level based on party composition
    /// </summary>
    public static int GetScaledEnemyLevel(int partyAverageLevel, int partySize, Difficulty difficulty)
    {
        var baseLevel = partyAverageLevel;

        // Scale up for larger parties
        var partySizeBonus = partySize switch
        {
            1 => -1, // Solo is slightly easier
            2 => 0,
            3 => 1,
            4 => 2,
            _ => 3
        };

        // Difficulty adjustment
        var difficultyBonus = difficulty switch
        {
            Difficulty.Story => -2,
            Difficulty.Normal => 0,
            Difficulty.Hard => 2,
            Difficulty.Ultra => 4,
            Difficulty.Nightmare => 6,
            _ => 0
        };

        return Math.Max(1, baseLevel + partySizeBonus + difficultyBonus);
    }

    /// <summary>
    /// Determines how many enemies should spawn based on party size
    /// Returns 1-4 enemies with more balanced scaling
    /// </summary>
    public static int GetEnemyCount(int partySize, System.Random random)
    {
        // More granular control: always 1-4 enemies
        if (partySize == 1)
        {
            // Solo: 1-2 enemies (60% chance of 1, 40% chance of 2)
            return random.NextDouble() < 0.6 ? 1 : 2;
        }
        else if (partySize == 2)
        {
            // Duo: 1-3 enemies (30% chance of 1, 40% chance of 2, 30% chance of 3)
            var roll = random.NextDouble();
            if (roll < 0.3) return 1;
            if (roll < 0.7) return 2;
            return 3;
        }
        else if (partySize == 3)
        {
            // Trio: 2-4 enemies (20% chance of 2, 40% chance of 3, 40% chance of 4)
            var roll = random.NextDouble();
            if (roll < 0.2) return 2;
            if (roll < 0.6) return 3;
            return 4;
        }
        else // partySize >= 4
        {
            // Full party: 2-4 enemies (10% chance of 2, 40% chance of 3, 50% chance of 4)
            var roll = random.NextDouble();
            if (roll < 0.1) return 2;
            if (roll < 0.5) return 3;
            return 4;
        }
    }

    private static void ScaleStat(IActor actor, StatType statType, double scaleFactor)
    {
        if (!actor.TryGetStat(statType, out var stat))
            return;

        // Get the base/max value and scale it
        var baseValue = stat.Base;
        var newBase = baseValue * scaleFactor;

        // Update both base and current (healing to new max essentially)
        var statValue = (StatValue)stat;
        statValue.SetBase(newBase);
        statValue.ApplyDelta(newBase - stat.Current); // Restore to new max
    }
}
