using Fub.Enums;

namespace Fub.Implementations.Actors;

/// <summary>
/// Provides default stat presets and per-species/class base modifiers.
/// </summary>
public static class ActorStatPresets
{
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

    // ---- Species base modifiers (additive deltas over Default) ----
    public static void ApplySpecies(ref Dictionary<StatType,double> stats, Species s)
    {
        switch (s)
        {
            case Species.Human:
                stats[StatType.Luck] += 2; stats[StatType.Spirit] += 1; break;
            case Species.Catkin:
                stats[StatType.Agility] += 3; stats[StatType.Evasion] += 5; break;
            case Species.Dogkin:
                stats[StatType.Vitality] += 3; stats[StatType.Health] += 10; break;
            case Species.Squirrelkin:
                stats[StatType.Agility] += 4; stats[StatType.Luck] += 3; break;
            case Species.LandShark:
                stats[StatType.Strength] += 5; stats[StatType.Health] += 20; break;
            case Species.Elf:
                stats[StatType.Intellect] += 4; stats[StatType.Spirit] += 4; break;
            case Species.Dwarf:
                stats[StatType.Vitality] += 5; stats[StatType.Armor] += 5; break;
            case Species.Orc:
                stats[StatType.Strength] += 6; stats[StatType.AttackPower] += 5; break;
            case Species.Goblin:
                stats[StatType.Luck] += 4; stats[StatType.Agility] += 2; break;
            case Species.Undead:
                stats[StatType.ShadowResist] += 15; stats[StatType.Spirit] += 2; break;
            case Species.Demon:
                stats[StatType.ShadowResist] += 10; stats[StatType.Intellect] += 3; stats[StatType.Strength] += 3; break;
            case Species.Construct:
                stats[StatType.Armor] += 10; stats[StatType.PoisonResist] += 10; break;
            case Species.Beast:
                stats[StatType.Strength] += 3; stats[StatType.Agility] += 3; break;
            case Species.Dragonkin:
                stats[StatType.FireResist] += 10; stats[StatType.Strength] += 4; break;
            case Species.Fae:
                stats[StatType.ArcaneResist] += 10; stats[StatType.Luck] += 4; stats[StatType.Intellect] += 2; break;
            case Species.Vampire:
                stats[StatType.ShadowResist] += 10; stats[StatType.Spirit] += 3; break;
            case Species.Warewolf:
                stats[StatType.Strength] += 4; stats[StatType.Speed] += 10; break;
            case Species.Merfolk:
                stats[StatType.ColdResist] += 10; stats[StatType.Spirit] += 3; break;
            case Species.Giant:
                stats[StatType.Health] += 40; stats[StatType.Strength] += 8; break;
            case Species.Android:
                stats[StatType.LightningResist] += 10; stats[StatType.Armor] += 5; stats[StatType.Intellect] += 3; break;
        }
    }

    // ---- Class base modifiers (additive deltas over species-adjusted stats) ----
    public static void ApplyClass(ref Dictionary<StatType,double> stats, ActorClass c)
    {
        switch (c)
        {
            // General
            case ActorClass.Adventurer:
                stats[StatType.Luck] += 2; break;

            // Melee
            case ActorClass.Warrior:
                stats[StatType.Health] += 50; stats[StatType.Strength] += 5; stats[StatType.Vitality] += 5; stats[StatType.Armor] += 10; stats[StatType.AttackPower] += 5; break;
            case ActorClass.Cleric:
                stats[StatType.Spirit] += 8; stats[StatType.Mana] += 20; stats[StatType.HolyResist] += 15; break;
            case ActorClass.Paladin:
                stats[StatType.Health] += 40; stats[StatType.Vitality] += 6; stats[StatType.Spirit] += 3; stats[StatType.Armor] += 12; stats[StatType.HolyResist] += 10; break;
            case ActorClass.DarkKnight:
                stats[StatType.Health] += 45; stats[StatType.Strength] += 6; stats[StatType.CritDamage] += 10; stats[StatType.ShadowResist] += 10; break;
            case ActorClass.Gunbreaker:
                stats[StatType.Health] += 30; stats[StatType.Strength] += 4; stats[StatType.Armor] += 8; stats[StatType.AttackPower] += 4; break;
            case ActorClass.Barbarian:
                stats[StatType.Health] += 60; stats[StatType.Strength] += 7; stats[StatType.CritChance] += 3; break;
            case ActorClass.Monk:
                stats[StatType.Agility] += 6; stats[StatType.Speed] += 15; stats[StatType.Evasion] += 8; break;
            case ActorClass.Samurai:
                stats[StatType.Agility] += 5; stats[StatType.AttackPower] += 6; stats[StatType.CritChance] += 5; break;
            case ActorClass.Dragoon:
                stats[StatType.Strength] += 6; stats[StatType.Agility] += 3; stats[StatType.AttackPower] += 6; break;
            case ActorClass.Ninja:
                stats[StatType.Agility] += 8; stats[StatType.Evasion] += 10; stats[StatType.CritChance] += 7; break;
            case ActorClass.Reaper:
                stats[StatType.Strength] += 5; stats[StatType.ShadowResist] += 5; stats[StatType.CritDamage] += 15; break;
            case ActorClass.Rogue:
                stats[StatType.Agility] += 7; stats[StatType.CritChance] += 10; stats[StatType.Speed] += 10; break;

            // Ranged
            case ActorClass.Ranger:
                stats[StatType.Agility] += 5; stats[StatType.AttackPower] += 5; stats[StatType.Evasion] += 5; break;
            case ActorClass.Machinist:
                stats[StatType.Intellect] += 3; stats[StatType.AttackPower] += 6; stats[StatType.Speed] += 8; break;
            case ActorClass.Dancer:
                stats[StatType.Agility] += 6; stats[StatType.Luck] += 6; stats[StatType.Evasion] += 6; break;
            case ActorClass.Bard:
                stats[StatType.Spirit] += 5; stats[StatType.Luck] += 5; stats[StatType.Mana] += 10; break;

            // Magic / Support
            case ActorClass.Druid:
                stats[StatType.Spirit] += 6; stats[StatType.Mana] += 10; stats[StatType.ArcaneResist] += 5; stats[StatType.SpellPower] += 4; break;
            case ActorClass.Wizard:
                stats[StatType.Intellect] += 8; stats[StatType.Mana] += 30; stats[StatType.SpellPower] += 8; break;
            case ActorClass.Sorcerer:
                stats[StatType.Intellect] += 7; stats[StatType.Mana] += 25; stats[StatType.CritChance] += 5; stats[StatType.SpellPower] += 7; break;
            case ActorClass.Warlock:
                stats[StatType.Intellect] += 6; stats[StatType.ShadowResist] += 10; stats[StatType.Mana] += 20; stats[StatType.SpellPower] += 6; break;
            case ActorClass.BlackMage:
                stats[StatType.Intellect] += 8; stats[StatType.Mana] += 25; stats[StatType.SpellPower] += 10; break;
            case ActorClass.WhiteMage:
                stats[StatType.Spirit] += 8; stats[StatType.Mana] += 25; stats[StatType.HolyResist] += 10; break;
            case ActorClass.Scholar:
                stats[StatType.Spirit] += 6; stats[StatType.Intellect] += 4; stats[StatType.Mana] += 20; break;
            case ActorClass.Astrologian:
                stats[StatType.Intellect] += 5; stats[StatType.Spirit] += 5; stats[StatType.Luck] += 5; break;
            case ActorClass.Sage:
                stats[StatType.Intellect] += 6; stats[StatType.SpellPower] += 6; stats[StatType.Mana] += 20; break;
            case ActorClass.RedMage:
                stats[StatType.Intellect] += 5; stats[StatType.AttackPower] += 5; stats[StatType.Mana] += 20; break;
            case ActorClass.BlueMage:
                stats[StatType.Intellect] += 5; stats[StatType.Mana] += 15; stats[StatType.Luck] += 5; break;
            case ActorClass.Summoner:
                stats[StatType.Intellect] += 7; stats[StatType.Mana] += 25; stats[StatType.SpellPower] += 7; break;
            case ActorClass.Necromancer:
                stats[StatType.Intellect] += 6; stats[StatType.ShadowResist] += 10; stats[StatType.SpellPower] += 6; break;
            case ActorClass.Artificer:
                stats[StatType.Intellect] += 6; stats[StatType.LightningResist] += 10; stats[StatType.SpellPower] += 5; break;

            // Crafting / Gathering (small flavor boosts)
            case ActorClass.Carpenter:
                stats[StatType.Spirit] += 2; stats[StatType.Luck] += 2; break;
            case ActorClass.Blacksmith:
                stats[StatType.Strength] += 3; stats[StatType.Vitality] += 2; break;
            case ActorClass.Armorer:
                stats[StatType.Armor] += 5; stats[StatType.Vitality] += 2; break;
            case ActorClass.Goldsmith:
                stats[StatType.Luck] += 4; stats[StatType.Intellect] += 2; break;
            case ActorClass.Leatherworker:
                stats[StatType.Agility] += 3; stats[StatType.Evasion] += 3; break;
            case ActorClass.Weaver:
                stats[StatType.Spirit] += 3; stats[StatType.Intellect] += 2; break;
            case ActorClass.Alchemist:
                stats[StatType.Intellect] += 3; stats[StatType.PoisonResist] += 10; break;
            case ActorClass.Culinarian:
                stats[StatType.Spirit] += 3; stats[StatType.Luck] += 3; break;
            case ActorClass.Miner:
                stats[StatType.Strength] += 3; stats[StatType.Health] += 10; break;
            case ActorClass.Botanist:
                stats[StatType.Agility] += 3; stats[StatType.Spirit] += 2; break;
            case ActorClass.Fisher:
                stats[StatType.Spirit] += 3; stats[StatType.ColdResist] += 10; break;
        }
    }

    public static Dictionary<StatType,double> Combine(Species species, ActorClass cls)
    {
        var stats = Default();
        ApplySpecies(ref stats, species);
        ApplyClass(ref stats, cls);
        return stats;
    }

    // Legacy presets retained for convenience/tests
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
