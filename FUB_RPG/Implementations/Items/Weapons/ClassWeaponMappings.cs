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
        { ActorClass.Cleric, WeaponType.Mace },
        { ActorClass.Paladin, WeaponType.HolySword },
        { ActorClass.DarkKnight, WeaponType.Greatsword },
        { ActorClass.Gunbreaker, WeaponType.Gunblade },
        { ActorClass.Barbarian, WeaponType.Greataxe },
        { ActorClass.Monk, WeaponType.Handwraps },
        { ActorClass.Samurai, WeaponType.Katana },
        { ActorClass.Dragoon, WeaponType.Spear },
        { ActorClass.Ninja, WeaponType.Kunai },
        { ActorClass.Reaper, WeaponType.Scythe },
        { ActorClass.Rogue, WeaponType.Dagger },

        // Ranged
        { ActorClass.Ranger, WeaponType.Bow },
        { ActorClass.Hunter, WeaponType.Crossbow },
        { ActorClass.Machinist, WeaponType.Firearm },
        { ActorClass.Dancer, WeaponType.Chakrams },
        { ActorClass.Bard, WeaponType.Lute },

        // Magic / Support
        { ActorClass.Druid, WeaponType.Scimitar },
        { ActorClass.Wizard, WeaponType.Wand },
        { ActorClass.Sorcerer, WeaponType.Orb },
        { ActorClass.Warlock, WeaponType.PactTome },
        { ActorClass.BlackMage, WeaponType.Rod },
        { ActorClass.WhiteMage, WeaponType.Staff },
        { ActorClass.Scholar, WeaponType.Codex },
        { ActorClass.Astrologian, WeaponType.Astrolabe },
        { ActorClass.Sage, WeaponType.Nouliths },
        { ActorClass.RedMage, WeaponType.Rapier },
        { ActorClass.BlueMage, WeaponType.Cane },
        { ActorClass.Summoner, WeaponType.Grimoire },
        { ActorClass.Necromancer, WeaponType.Focus },
        { ActorClass.Artificer, WeaponType.MultiTool },

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
        foreach (var kvp in ClassToWeapon)
            WeaponToClass[kvp.Value] = kvp.Key;
    }

    public static WeaponType GetWeaponTypeForClass(ActorClass cls)
        => ClassToWeapon[cls];

    public static ActorClass GetClassForWeaponType(WeaponType type)
        => WeaponToClass[type];

    public static bool TryGetClassForWeaponType(WeaponType type, out ActorClass cls)
        => WeaponToClass.TryGetValue(type, out cls);

    public static (string name, WeaponType type, DamageType dmg) GetStarterSpec(ActorClass cls)
        => cls switch
        {
            // General
            ActorClass.Adventurer => ("Traveler's Toolkit", WeaponType.Toolkit, DamageType.Physical),

            // Melee
            ActorClass.Warrior => ("Iron Sword", WeaponType.Sword, DamageType.Physical),
            ActorClass.Cleric => ("Blessed Mace", WeaponType.Mace, DamageType.Holy),
            ActorClass.Paladin => ("Knight's Holy Blade", WeaponType.HolySword, DamageType.Holy),
            ActorClass.DarkKnight => ("Blacksteel Greatsword", WeaponType.Greatsword, DamageType.Shadow),
            ActorClass.Gunbreaker => ("Steel Gunblade", WeaponType.Gunblade, DamageType.Physical),
            ActorClass.Barbarian => ("Tribal Greataxe", WeaponType.Greataxe, DamageType.Physical),
            ActorClass.Monk => ("Leather Handwraps", WeaponType.Handwraps, DamageType.Physical),
            ActorClass.Samurai => ("Weathered Katana", WeaponType.Katana, DamageType.Physical),
            ActorClass.Dragoon => ("Bronze Spear", WeaponType.Spear, DamageType.Physical),
            ActorClass.Ninja => ("Steel Kunai", WeaponType.Kunai, DamageType.Physical),
            ActorClass.Reaper => ("Grim Scythe", WeaponType.Scythe, DamageType.Shadow),
            ActorClass.Rogue => ("Stiletto", WeaponType.Dagger, DamageType.Physical),

            // Ranged
            ActorClass.Ranger => ("Yew Bow", WeaponType.Bow, DamageType.Physical),
            ActorClass.Hunter => ("Oak Crossbow", WeaponType.Crossbow, DamageType.Physical),
            ActorClass.Machinist => ("Rusty Firearm", WeaponType.Firearm, DamageType.Physical),
            ActorClass.Dancer => ("Bronze Chakrams", WeaponType.Chakrams, DamageType.Physical),
            ActorClass.Bard => ("Wandering Lute", WeaponType.Lute, DamageType.Psychic),

            // Magic / Support
            ActorClass.Druid => ("Grove Scimitar", WeaponType.Scimitar, DamageType.Physical),
            ActorClass.Wizard => ("Apprentice Wand", WeaponType.Wand, DamageType.Arcane),
            ActorClass.Sorcerer => ("Cracked Orb", WeaponType.Orb, DamageType.Arcane),
            ActorClass.Warlock => ("Pact-Bound Tome", WeaponType.PactTome, DamageType.Shadow),
            ActorClass.BlackMage => ("Black Rod", WeaponType.Rod, DamageType.Arcane),
            ActorClass.WhiteMage => ("Healer's Staff", WeaponType.Staff, DamageType.Holy),
            ActorClass.Scholar => ("Weathered Codex", WeaponType.Codex, DamageType.Arcane),
            ActorClass.Astrologian => ("Star Astrolabe", WeaponType.Astrolabe, DamageType.Arcane),
            ActorClass.Sage => ("Initiate Nouliths", WeaponType.Nouliths, DamageType.Arcane),
            ActorClass.RedMage => ("Arcane Rapier", WeaponType.Rapier, DamageType.Arcane),
            ActorClass.BlueMage => ("Azure Cane", WeaponType.Cane, DamageType.Psychic),
            ActorClass.Summoner => ("Summoner's Grimoire", WeaponType.Grimoire, DamageType.Arcane),
            ActorClass.Necromancer => ("Bone Focus", WeaponType.Focus, DamageType.Shadow),
            ActorClass.Artificer => ("Tinker's Multi-Tool", WeaponType.MultiTool, DamageType.Lightning),

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
