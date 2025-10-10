using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Implementations.Actors;

/// <summary>
/// Provides default stat presets for different actor types.
/// Customize these values when creating actors.
/// </summary>
public static class ActorStatPresets
{
    /// <summary>
    /// Creates a complete stat set with default values.
    /// All stats are initialized to ensure no missing stat errors.
    /// </summary>
    public static Dictionary<StatType, double> Default()
    {
        return new Dictionary<StatType, double>
        {
            // Primary Resources
            { StatType.Health, 100d },
            { StatType.Mana, 50d },
            { StatType.Technical, 50d },
            
            // Core Attributes
            { StatType.Strength, 10d },
            { StatType.Agility, 10d },
            { StatType.Intellect, 10d },
            { StatType.Vitality, 10d },
            { StatType.Spirit, 10d },
            { StatType.Luck, 10d },
            
            // Combat Stats
            { StatType.Armor, 0d },
            { StatType.Evasion, 0d },
            { StatType.CritChance, 5d },
            { StatType.CritDamage, 150d },
            { StatType.AttackPower, 10d },
            { StatType.SpellPower, 10d },
            { StatType.Speed, 100d },
            
            // Resistances
            { StatType.FireResist, 0d },
            { StatType.ColdResist, 0d },
            { StatType.LightningResist, 0d },
            { StatType.PoisonResist, 0d },
            { StatType.ArcaneResist, 0d },
            { StatType.ShadowResist, 0d },
            { StatType.HolyResist, 0d }
        };
    }

    /// <summary>
    /// Creates a warrior-type stat preset with high health and strength.
    /// </summary>
    public static Dictionary<StatType, double> Warrior()
    {
        var stats = Default();
        stats[StatType.Health] = 150d;
        stats[StatType.Strength] = 15d;
        stats[StatType.Vitality] = 15d;
        stats[StatType.Armor] = 10d;
        stats[StatType.AttackPower] = 15d;
        return stats;
    }

    /// <summary>
    /// Creates a mage-type stat preset with high mana and intellect.
    /// </summary>
    public static Dictionary<StatType, double> Mage()
    {
        var stats = Default();
        stats[StatType.Health] = 80d;
        stats[StatType.Mana] = 100d;
        stats[StatType.Intellect] = 15d;
        stats[StatType.Spirit] = 12d;
        stats[StatType.SpellPower] = 15d;
        return stats;
    }

    /// <summary>
    /// Creates a rogue-type stat preset with high agility and crit.
    /// </summary>
    public static Dictionary<StatType, double> Rogue()
    {
        var stats = Default();
        stats[StatType.Health] = 90d;
        stats[StatType.Technical] = 70d;
        stats[StatType.Agility] = 15d;
        stats[StatType.Evasion] = 10d;
        stats[StatType.CritChance] = 15d;
        stats[StatType.Speed] = 120d;
        return stats;
    }

    /// <summary>
    /// Creates a weak enemy stat preset.
    /// </summary>
    public static Dictionary<StatType, double> WeakEnemy()
    {
        var stats = Default();
        stats[StatType.Health] = 50d;
        stats[StatType.Mana] = 20d;
        stats[StatType.Technical] = 20d;
        stats[StatType.Strength] = 5d;
        stats[StatType.Agility] = 5d;
        stats[StatType.Intellect] = 5d;
        stats[StatType.AttackPower] = 5d;
        return stats;
    }

    /// <summary>
    /// Creates a strong enemy stat preset.
    /// </summary>
    public static Dictionary<StatType, double> StrongEnemy()
    {
        var stats = Default();
        stats[StatType.Health] = 200d;
        stats[StatType.Mana] = 80d;
        stats[StatType.Technical] = 80d;
        stats[StatType.Strength] = 20d;
        stats[StatType.Agility] = 15d;
        stats[StatType.Intellect] = 15d;
        stats[StatType.Armor] = 15d;
        stats[StatType.AttackPower] = 20d;
        stats[StatType.SpellPower] = 20d;
        return stats;
    }

    /// <summary>
    /// Creates a boss enemy stat preset.
    /// </summary>
    public static Dictionary<StatType, double> Boss()
    {
        var stats = Default();
        stats[StatType.Health] = 500d;
        stats[StatType.Mana] = 150d;
        stats[StatType.Technical] = 150d;
        stats[StatType.Strength] = 30d;
        stats[StatType.Agility] = 20d;
        stats[StatType.Intellect] = 25d;
        stats[StatType.Vitality] = 25d;
        stats[StatType.Armor] = 25d;
        stats[StatType.AttackPower] = 30d;
        stats[StatType.SpellPower] = 30d;
        stats[StatType.CritChance] = 10d;
        return stats;
    }

    /// <summary>
    /// Modifies a stat dictionary with custom values.
    /// </summary>
    public static Dictionary<StatType, double> Customize(
        Dictionary<StatType, double> baseStats,
        params (StatType stat, double value)[] modifications)
    {
        var stats = new Dictionary<StatType, double>(baseStats);
        foreach (var (stat, value) in modifications)
        {
            stats[stat] = value;
        }
        return stats;
    }
}

