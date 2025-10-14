using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Implementations.Actors;

namespace Fub.Implementations.Progression;

/// <summary>
/// Provides per-class per-level stat growth values.
/// Values are per level gained. Missing stats default to 0 growth.
/// </summary>
public static class ClassStatGrowth
{
    private static readonly Dictionary<ActorClass, Dictionary<StatType,double>> Growth = new()
    {
        // Melee / Tanks
        { ActorClass.Warrior, new(){ {StatType.Health,22},{StatType.Strength,4},{StatType.Vitality,5},{StatType.Mana,5},{StatType.Technical,2},{StatType.Agility,2} } },
        { ActorClass.Paladin, new(){ {StatType.Health,18},{StatType.Strength,3},{StatType.Vitality,4},{StatType.Mana,8},{StatType.Spirit,2} } },
        { ActorClass.DarkKnight, new(){ {StatType.Health,20},{StatType.Strength,4},{StatType.Vitality,4},{StatType.Mana,10} } },
        { ActorClass.Gunbreaker, new(){ {StatType.Health,19},{StatType.Strength,4},{StatType.Vitality,4},{StatType.Technical,4},{StatType.Agility,2} } },
        { ActorClass.Barbarian, new(){ {StatType.Health,26},{StatType.Strength,5},{StatType.Vitality,5},{StatType.Agility,2} } },
        { ActorClass.Monk, new(){ {StatType.Health,18},{StatType.Strength,4},{StatType.Agility,4},{StatType.Vitality,3},{StatType.Speed,1} } },
        { ActorClass.Samurai, new(){ {StatType.Health,18},{StatType.Strength,5},{StatType.Agility,3},{StatType.CritDamage,0.5} } },
        { ActorClass.Dragoon, new(){ {StatType.Health,19},{StatType.Strength,4},{StatType.Agility,3},{StatType.CritChance,0.4} } },
        { ActorClass.Ninja, new(){ {StatType.Health,16},{StatType.Agility,5},{StatType.Strength,2},{StatType.CritChance,0.6},{StatType.Speed,1} } },
        { ActorClass.Reaper, new(){ {StatType.Health,20},{StatType.Strength,4},{StatType.Spirit,2},{StatType.CritChance,0.5},{StatType.Mana,6} } },
        { ActorClass.Rogue, new(){ {StatType.Health,16},{StatType.Agility,5},{StatType.Strength,3},{StatType.CritChance,0.5},{StatType.Speed,1} } },
        { ActorClass.Cleric, new(){ {StatType.Health,17},{StatType.Spirit,5},{StatType.Intellect,3},{StatType.Mana,14},{StatType.Vitality,3} } },

        // Ranged / Physical
        { ActorClass.Ranger, new(){ {StatType.Health,15},{StatType.Agility,5},{StatType.Strength,2},{StatType.CritChance,0.5},{StatType.Speed,1} } },
        { ActorClass.Hunter, new(){ {StatType.Health,15},{StatType.Agility,5},{StatType.Strength,2},{StatType.CritChance,0.55},{StatType.Speed,1},{StatType.Mana,4} } },
        { ActorClass.Machinist, new(){ {StatType.Health,15},{StatType.Agility,4},{StatType.Technical,5},{StatType.Mana,4} } },
        { ActorClass.Dancer, new(){ {StatType.Health,14},{StatType.Agility,5},{StatType.Speed,2},{StatType.Spirit,3},{StatType.CritChance,0.4} } },
        { ActorClass.Bard, new(){ {StatType.Health,14},{StatType.Agility,4},{StatType.Spirit,3},{StatType.Mana,8} } },

        // Magic / Casters / Support
        { ActorClass.Wizard, new(){ {StatType.Health,12},{StatType.Intellect,6},{StatType.Mana,18},{StatType.SpellPower,5},{StatType.Spirit,2} } },
        { ActorClass.Sorcerer, new(){ {StatType.Health,12},{StatType.Intellect,6},{StatType.Mana,20},{StatType.SpellPower,6} } },
        { ActorClass.Warlock, new(){ {StatType.Health,13},{StatType.Intellect,5},{StatType.Mana,16},{StatType.Spirit,3},{StatType.SpellPower,5} } },
        { ActorClass.WhiteMage, new(){ {StatType.Health,13},{StatType.Intellect,5},{StatType.Mana,22},{StatType.Spirit,5} } },
        { ActorClass.BlackMage, new(){ {StatType.Health,11},{StatType.Intellect,7},{StatType.Mana,24},{StatType.SpellPower,7} } },
        { ActorClass.Druid, new(){ {StatType.Health,14},{StatType.Intellect,5},{StatType.Mana,18},{StatType.Spirit,4},{StatType.Vitality,2} } },
        { ActorClass.RedMage, new(){ {StatType.Health,14},{StatType.Intellect,5},{StatType.Strength,2},{StatType.Mana,18},{StatType.SpellPower,4} } },
        { ActorClass.Summoner, new(){ {StatType.Health,12},{StatType.Intellect,6},{StatType.Mana,20},{StatType.SpellPower,5} } },
        { ActorClass.Necromancer, new(){ {StatType.Health,12},{StatType.Intellect,5},{StatType.Mana,18},{StatType.Spirit,3},{StatType.SpellPower,5} } },
        { ActorClass.Scholar, new(){ {StatType.Health,13},{StatType.Intellect,5},{StatType.Mana,20},{StatType.Spirit,4},{StatType.SpellPower,4} } },
        { ActorClass.Astrologian, new(){ {StatType.Health,13},{StatType.Intellect,5},{StatType.Spirit,5},{StatType.Mana,20},{StatType.CritChance,0.3} } },
        { ActorClass.Sage, new(){ {StatType.Health,13},{StatType.Intellect,5},{StatType.Mana,21},{StatType.Technical,3},{StatType.SpellPower,5} } },
        { ActorClass.BlueMage, new(){ {StatType.Health,15},{StatType.Intellect,5},{StatType.Mana,16},{StatType.SpellPower,5},{StatType.CritChance,0.4} } },
        { ActorClass.Artificer, new(){ {StatType.Health,14},{StatType.Intellect,5},{StatType.Technical,5},{StatType.Mana,15},{StatType.SpellPower,3} } },

        // Crafting (Hand) focus on Technical / Spirit / Intellect moderate HP
        { ActorClass.Carpenter, new(){ {StatType.Health,12},{StatType.Technical,6},{StatType.Spirit,2},{StatType.Mana,8} } },
        { ActorClass.Blacksmith, new(){ {StatType.Health,14},{StatType.Strength,3},{StatType.Technical,5},{StatType.Vitality,3} } },
        { ActorClass.Armorer, new(){ {StatType.Health,15},{StatType.Vitality,5},{StatType.Strength,2},{StatType.Technical,4} } },
        { ActorClass.Goldsmith, new(){ {StatType.Health,11},{StatType.Technical,6},{StatType.Spirit,3},{StatType.Mana,6} } },
        { ActorClass.Leatherworker, new(){ {StatType.Health,13},{StatType.Agility,3},{StatType.Technical,5},{StatType.Vitality,2} } },
        { ActorClass.Weaver, new(){ {StatType.Health,11},{StatType.Technical,6},{StatType.Spirit,3},{StatType.Mana,6} } },
        { ActorClass.Alchemist, new(){ {StatType.Health,12},{StatType.Intellect,4},{StatType.Technical,6},{StatType.Mana,10} } },
        { ActorClass.Culinarian, new(){ {StatType.Health,13},{StatType.Spirit,4},{StatType.Technical,5},{StatType.Mana,8} } },

        // Gathering (Land) focus on Agility / Technical / Luck
        { ActorClass.Miner, new(){ {StatType.Health,16},{StatType.Vitality,4},{StatType.Strength,3},{StatType.Technical,4},{StatType.Luck,0.3} } },
        { ActorClass.Botanist, new(){ {StatType.Health,14},{StatType.Agility,4},{StatType.Spirit,3},{StatType.Technical,4},{StatType.Luck,0.4} } },
        { ActorClass.Fisher, new(){ {StatType.Health,13},{StatType.Spirit,4},{StatType.Technical,5},{StatType.Luck,0.5},{StatType.Mana,6} } },

        // Generic fallback
        { ActorClass.Adventurer, new(){ {StatType.Health,16},{StatType.Strength,2},{StatType.Intellect,2},{StatType.Agility,2},{StatType.Mana,10},{StatType.Vitality,2} } },
    };

    public static IReadOnlyDictionary<StatType,double> GetGrowth(ActorClass cls)
    {
        if (Growth.TryGetValue(cls, out var map)) return map;
        return Growth[ActorClass.Adventurer];
    }

    /// <summary>
    /// Apply growth for each level gained (exclusive start, inclusive end) and restore resources each time.
    /// </summary>
    public static void ApplyGrowth(IActor actor, int previousLevel, int newLevel)
    {
        if (newLevel <= previousLevel) return;
        if (actor is not ActorBase concrete) return; // need concrete access
        var growthMap = GetGrowth(actor.EffectiveClass);
        for (int lvl = previousLevel + 1; lvl <= newLevel; lvl++)
        {
            foreach (var kv in growthMap)
            {
                concrete.IncreaseBaseStat(kv.Key, kv.Value);
            }
        }
        concrete.RestoreResourceStats();
    }
}
