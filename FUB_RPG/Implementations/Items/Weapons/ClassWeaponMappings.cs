using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Implementations.Items.Weapons;

public static class ClassWeaponMappings
{
    private static readonly Dictionary<ActorClass, WeaponType> ClassToWeapon = new()
    {
        // General
        { ActorClass.Adventurer, WeaponType.Toolkit },

        // Melee
        { ActorClass.Warrior, WeaponType.Sword },
        { ActorClass.Paladin, WeaponType.Mace },
        { ActorClass.Barbarian, WeaponType.Axe },
        { ActorClass.Druid, WeaponType.Scimitar },
        { ActorClass.Dragoon, WeaponType.Spear },
        { ActorClass.Rogue, WeaponType.Dagger },
        { ActorClass.Monk, WeaponType.Handwraps },
        { ActorClass.Ninja, WeaponType.Kunai },

        // Ranged
        { ActorClass.Ranger, WeaponType.Bow },
        { ActorClass.Gunner, WeaponType.Firearm },

        // Magic / Support
        { ActorClass.BlackMage, WeaponType.Rod },
        { ActorClass.WhiteMage, WeaponType.Staff },
        { ActorClass.RedMage, WeaponType.Rapier },
        { ActorClass.BlueMage, WeaponType.Cane },
        { ActorClass.Summoner, WeaponType.Grimoire },
        { ActorClass.Necromancer, WeaponType.Focus },
        { ActorClass.Artificer, WeaponType.MultiTool },
        { ActorClass.Bard, WeaponType.Lute },

        // Crafting (DoH)
        { ActorClass.Carpenter, WeaponType.Saw },
        { ActorClass.Blacksmith, WeaponType.Hammer },
        { ActorClass.Armorer, WeaponType.RaisingHammer },
        { ActorClass.Goldsmith, WeaponType.ChasingHammer },
        { ActorClass.Leatherworker, WeaponType.HeadKnife },
        { ActorClass.Weaver, WeaponType.Needle },
        { ActorClass.Alchemist, WeaponType.Alembic },
        { ActorClass.Culinarian, WeaponType.Skillet },

        // Gathering (DoL)
        { ActorClass.Miner, WeaponType.Pickaxe },
        { ActorClass.Botanist, WeaponType.Hatchet },
        { ActorClass.Fisher, WeaponType.FishingRod }
    };

    private static readonly Dictionary<WeaponType, ActorClass> WeaponToClass;

    static ClassWeaponMappings()
    {
        WeaponToClass = new();
        // Primary one-to-one mappings
        foreach (var kvp in ClassToWeapon)
        {
            WeaponToClass[kvp.Value] = kvp.Key;
        }
        // No legacy/fallback mappings by design
    }

    public static WeaponType GetWeaponTypeForClass(ActorClass cls)
        => ClassToWeapon[cls];

    public static ActorClass GetClassForWeaponType(WeaponType type)
        => WeaponToClass[type];

    public static bool TryGetClassForWeaponType(WeaponType type, out ActorClass cls)
        => WeaponToClass.TryGetValue(type, out cls);

    // Starter weapon naming + damage flavor
    public static (string name, WeaponType type, DamageType dmg) GetStarterSpec(ActorClass cls)
    {
        return cls switch
        {
            // General
            ActorClass.Adventurer => ("Traveler's Toolkit", WeaponType.Toolkit, DamageType.Physical),

            // Melee
            ActorClass.Warrior => ("Iron Sword", WeaponType.Sword, DamageType.Physical),
            ActorClass.Paladin => ("Oath Mace", WeaponType.Mace, DamageType.Holy),
            ActorClass.Barbarian => ("Tribal Axe", WeaponType.Axe, DamageType.Physical),
            ActorClass.Druid => ("Grove Scimitar", WeaponType.Scimitar, DamageType.Physical),
            ActorClass.Dragoon => ("Bronze Spear", WeaponType.Spear, DamageType.Physical),
            ActorClass.Rogue => ("Stiletto", WeaponType.Dagger, DamageType.Physical),
            ActorClass.Monk => ("Leather Handwraps", WeaponType.Handwraps, DamageType.Physical),
            ActorClass.Ninja => ("Steel Kunai", WeaponType.Kunai, DamageType.Physical),

            // Ranged
            ActorClass.Ranger => ("Yew Bow", WeaponType.Bow, DamageType.Physical),
            ActorClass.Gunner => ("Rusty Firearm", WeaponType.Firearm, DamageType.Physical),

            // Magic / Support
            ActorClass.BlackMage => ("Black Rod", WeaponType.Rod, DamageType.Arcane),
            ActorClass.WhiteMage => ("Healer's Staff", WeaponType.Staff, DamageType.Holy),
            ActorClass.RedMage => ("Arcane Rapier", WeaponType.Rapier, DamageType.Arcane),
            ActorClass.BlueMage => ("Azure Cane", WeaponType.Cane, DamageType.Psychic),
            ActorClass.Summoner => ("Summoner's Grimoire", WeaponType.Grimoire, DamageType.Arcane),
            ActorClass.Necromancer => ("Bone Focus", WeaponType.Focus, DamageType.Shadow),
            ActorClass.Artificer => ("Tinker's Multi-Tool", WeaponType.MultiTool, DamageType.Lightning),
            ActorClass.Bard => ("Wandering Lute", WeaponType.Lute, DamageType.Psychic),

            // Crafting (DoH)
            ActorClass.Carpenter => ("Carpenter's Saw", WeaponType.Saw, DamageType.Physical),
            ActorClass.Blacksmith => ("Smithing Hammer", WeaponType.Hammer, DamageType.Physical),
            ActorClass.Armorer => ("Raising Hammer", WeaponType.RaisingHammer, DamageType.Physical),
            ActorClass.Goldsmith => ("Chasing Hammer", WeaponType.ChasingHammer, DamageType.Physical),
            ActorClass.Leatherworker => ("Head Knife", WeaponType.HeadKnife, DamageType.Physical),
            ActorClass.Weaver => ("Weaver's Needle", WeaponType.Needle, DamageType.Physical),
            ActorClass.Alchemist => ("Alembic", WeaponType.Alembic, DamageType.Poison),
            ActorClass.Culinarian => ("Frying Pan", WeaponType.Skillet, DamageType.Physical),

            // Gathering (DoL)
            ActorClass.Miner => ("Bronze Pickaxe", WeaponType.Pickaxe, DamageType.Physical),
            ActorClass.Botanist => ("Initiate's Hatchet", WeaponType.Hatchet, DamageType.Physical),
            ActorClass.Fisher => ("Weathered Fishing Rod", WeaponType.FishingRod, DamageType.Physical),

            _ => ("Unknown", WeaponType.Toolkit, DamageType.Physical)
        };
    }
}
