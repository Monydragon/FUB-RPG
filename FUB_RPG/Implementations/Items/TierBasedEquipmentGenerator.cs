using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Items;
using Fub.Interfaces.Items.Equipment;
using Fub.Interfaces.Random;
using Fub.Implementations.Items.Equipment;
using Fub.Implementations.Items.Weapons;

namespace Fub.Implementations.Items;

/// <summary>
/// Generates tier-appropriate equipment sets for all classes
/// Ensures each class has weapons and armor for every 10-level tier
/// </summary>
public class TierBasedEquipmentGenerator
{
    private readonly Dictionary<ActorClass, ClassEquipmentTemplate> _classTemplates = new();
    private readonly IRandomSource _random;
    
    public TierBasedEquipmentGenerator(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        InitializeClassTemplates();
    }

    /// <summary>
    /// Generates a complete equipment set for a class at a specific tier
    /// </summary>
    public Dictionary<EquipmentSlot, IEquipment> GenerateCompleteSet(ActorClass actorClass, EquipmentTier tier)
    {
        var template = _classTemplates[actorClass];
        var equipmentSet = new Dictionary<EquipmentSlot, IEquipment>();
        var tierLevel = GetTierBaseLevel(tier);

        // Generate main hand weapon
        var mainWeapon = GenerateWeapon(actorClass, tier, template.PreferredWeaponTypes, tierLevel);
        equipmentSet[EquipmentSlot.MainHand] = mainWeapon;

        // Generate off-hand weapon or shield if applicable
        if (template.UsesDualWield || template.UsesShield)
        {
            var offhandItem = GenerateOffhandItem(actorClass, tier, template, tierLevel);
            if (offhandItem != null)
                equipmentSet[EquipmentSlot.OffHand] = offhandItem;
        }

        // Generate armor pieces
        var armorPieces = new[]
        {
            EquipmentSlot.Head,
            EquipmentSlot.Chest,
            EquipmentSlot.Legs,
            EquipmentSlot.Feet,
            EquipmentSlot.Hands
        };

        foreach (var slot in armorPieces)
        {
            var armor = GenerateArmor(actorClass, tier, slot, template.PreferredArmorTypes, tierLevel);
            equipmentSet[slot] = armor;
        }

        // Generate accessories
        var accessories = GenerateAccessories(actorClass, tier, tierLevel);
        foreach (var accessory in accessories)
        {
            if (!equipmentSet.ContainsKey(accessory.Key))
                equipmentSet[accessory.Key] = accessory.Value;
        }

        return equipmentSet;
    }

    /// <summary>
    /// Generates all equipment tiers for a specific class (levels 1-100)
    /// </summary>
    public Dictionary<EquipmentTier, Dictionary<EquipmentSlot, IEquipment>> GenerateAllTiersForClass(ActorClass actorClass)
    {
        var allTiers = new Dictionary<EquipmentTier, Dictionary<EquipmentSlot, IEquipment>>();
        
        for (int tierNum = 1; tierNum <= 10; tierNum++)
        {
            var t = (EquipmentTier)tierNum;
            allTiers[t] = GenerateCompleteSet(actorClass, t);
        }

        return allTiers;
    }

    /// <summary>
    /// Gets the base level for a tier (tier 1 = level 1, tier 2 = level 11, etc.)
    /// </summary>
    public static int GetTierBaseLevel(EquipmentTier tier) => ((int)tier - 1) * 10 + 1;

    /// <summary>
    /// Gets the max level for a tier (tier 1 = level 10, tier 2 = level 20, etc.)
    /// </summary>
    public static int GetTierMaxLevel(EquipmentTier tier) => (int)tier * 10;

    /// <summary>
    /// Determines the appropriate tier for a given level
    /// </summary>
    public static EquipmentTier GetTierForLevel(int level)
    {
        var tierNum = Math.Max(1, Math.Min(10, (level - 1) / 10 + 1));
        return (EquipmentTier)tierNum;
    }

    private Weapon GenerateWeapon(ActorClass actorClass, EquipmentTier tier, List<WeaponType> preferredTypes, int level)
    {
        var weaponType = preferredTypes.Count > 0 ? preferredTypes[0] : WeaponType.Sword;
        var rarity = DetermineRarityForTier(tier);
        
        var weaponName = GenerateWeaponName(actorClass, tier, weaponType);
        
        return new Weapon(
            name: weaponName,
            weaponType: weaponType,
            rarity: rarity,
            requiredLevel: level,
            allowedClasses: new[] { actorClass },
            tier: tier
        );
    }

    private IEquipment? GenerateOffhandItem(ActorClass actorClass, EquipmentTier tier, ClassEquipmentTemplate template, int level)
    {
        if (template.UsesDualWield)
        {
            // Generate a secondary weapon
            var list = template.PreferredWeaponTypes;
            var offhandWeaponType = list.Count > 1 ? list[1] : list[0];
            return GenerateWeapon(actorClass, tier, new List<WeaponType> { offhandWeaponType }, level);
        }
        else if (template.UsesShield)
        {
            // Generate a shield
            var shieldName = GenerateShieldName(actorClass, tier);
            var rarity = DetermineRarityForTier(tier);
            
            return new Armor(
                name: shieldName,
                slot: EquipmentSlot.OffHand,
                rarity: rarity,
                requiredLevel: level,
                allowedClasses: new[] { actorClass },
                tier: tier,
                armorType: ArmorType.Shield
            );
        }

        return null;
    }

    private Armor GenerateArmor(ActorClass actorClass, EquipmentTier tier, EquipmentSlot slot, List<ArmorType> preferredTypes, int level)
    {
        var armorType = preferredTypes.Count > 0 ? preferredTypes[0] : ArmorType.Leather;
        var rarity = DetermineRarityForTier(tier);
        var armorName = GenerateArmorName(actorClass, tier, slot, armorType);

        return new Armor(
            name: armorName,
            slot: slot,
            rarity: rarity,
            requiredLevel: level,
            allowedClasses: new[] { actorClass },
            tier: tier,
            armorType: armorType
        );
    }

    private Dictionary<EquipmentSlot, IEquipment> GenerateAccessories(ActorClass actorClass, EquipmentTier tier, int level)
    {
        var accessories = new Dictionary<EquipmentSlot, IEquipment>();
        var rarity = DetermineRarityForTier(tier);

        // Generate ring
        var ringName = GenerateAccessoryName(actorClass, tier, "Ring");
        var ring = new Armor(ringName, EquipmentSlot.Ring, rarity, level, new[] { actorClass }, tier: tier);
        accessories[EquipmentSlot.Ring] = ring;

        // Generate amulet
        var amuletName = GenerateAccessoryName(actorClass, tier, "Amulet");
        var amulet = new Armor(amuletName, EquipmentSlot.Amulet, rarity, level, new[] { actorClass }, tier: tier);
        accessories[EquipmentSlot.Amulet] = amulet;

        return accessories;
    }

    // ...existing code for private helper methods...

    private RarityTier DetermineRarityForTier(EquipmentTier tier)
    {
        // Higher tiers have better base rarity
        return tier switch
        {
            EquipmentTier.Simple => RarityTier.Common,
            EquipmentTier.Fine => RarityTier.Common,
            EquipmentTier.Superior => RarityTier.Uncommon,
            EquipmentTier.Exquisite => RarityTier.Uncommon,
            EquipmentTier.Masterwork => RarityTier.Rare,
            EquipmentTier.Epic => RarityTier.Rare,
            EquipmentTier.Relic => RarityTier.Epic,
            EquipmentTier.Celestial => RarityTier.Epic,
            EquipmentTier.Eldritch => RarityTier.Legendary,
            EquipmentTier.Dragonic => RarityTier.Legendary,
            _ => RarityTier.Common
        };
    }

    private string GenerateWeaponName(ActorClass actorClass, EquipmentTier tier, WeaponType weaponType)
    {
        var tierPrefix = GetTierPrefix(tier);
        var classPrefix = GetClassPrefix(actorClass);
        var weaponBase = GetWeaponBaseName(weaponType);
        
        return $"{tierPrefix} {classPrefix} {weaponBase}";
    }

    private string GenerateArmorName(ActorClass actorClass, EquipmentTier tier, EquipmentSlot slot, ArmorType armorType)
    {
        var tierPrefix = GetTierPrefix(tier);
        var classPrefix = GetClassPrefix(actorClass);
        var slotName = GetSlotName(slot);
        
        return $"{tierPrefix} {classPrefix} {slotName}";
    }

    private string GenerateShieldName(ActorClass actorClass, EquipmentTier tier)
    {
        var tierPrefix = GetTierPrefix(tier);
        var classPrefix = GetClassPrefix(actorClass);
        
        return $"{tierPrefix} {classPrefix} Shield";
    }

    private string GenerateAccessoryName(ActorClass actorClass, EquipmentTier tier, string accessoryType)
    {
        var tierPrefix = GetTierPrefix(tier);
        var classPrefix = GetClassPrefix(actorClass);
        
        return $"{tierPrefix} {classPrefix} {accessoryType}";
    }

    private string GetTierPrefix(EquipmentTier tier) => tier switch
    {
        EquipmentTier.Simple => "Simple",
        EquipmentTier.Fine => "Fine",
        EquipmentTier.Superior => "Superior",
        EquipmentTier.Exquisite => "Exquisite",
        EquipmentTier.Masterwork => "Masterwork",
        EquipmentTier.Epic => "Epic",
        EquipmentTier.Relic => "Relic",
        EquipmentTier.Celestial => "Celestial",
        EquipmentTier.Eldritch => "Eldritch",
        EquipmentTier.Dragonic => "Dragonic",
        _ => "Unknown"
    };

    private string GetClassPrefix(ActorClass actorClass) => actorClass switch
    {
        ActorClass.Warrior => "Warrior's",
        ActorClass.Wizard => "Wizard's",
        ActorClass.Rogue => "Rogue's",
        ActorClass.Cleric => "Cleric's",
        ActorClass.Ranger => "Ranger's",
        ActorClass.Paladin => "Paladin's",
        ActorClass.Barbarian => "Barbarian's",
        ActorClass.Sorcerer => "Sorcerer's",
        ActorClass.Warlock => "Warlock's",
        ActorClass.Bard => "Bard's",
        _ => "Adventurer's"
    };

    private string GetWeaponBaseName(WeaponType weaponType) => weaponType switch
    {
        WeaponType.Sword => "Sword",
        WeaponType.Greataxe => "Greataxe",
        WeaponType.Mace => "Mace",
        WeaponType.Dagger => "Dagger",
        WeaponType.Bow => "Bow",
        WeaponType.Staff => "Staff",
        WeaponType.Wand => "Wand",
        WeaponType.Crossbow => "Crossbow",
        WeaponType.Spear => "Spear",
        WeaponType.Hammer => "Hammer",
        _ => "Weapon"
    };

    private string GetSlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Head => "Helmet",
        EquipmentSlot.Chest => "Chestplate",
        EquipmentSlot.Legs => "Leggings",
        EquipmentSlot.Feet => "Boots",
        EquipmentSlot.Hands => "Gauntlets",
        EquipmentSlot.Cloak => "Cloak",
        EquipmentSlot.Belt => "Belt",
        EquipmentSlot.Ring => "Ring",
        EquipmentSlot.Amulet => "Amulet",
        _ => "Armor"
    };

    private void InitializeClassTemplates()
    {
        // Warrior - Heavy armor, swords/greataxes, shields
        _classTemplates[ActorClass.Warrior] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Sword, WeaponType.Greataxe, WeaponType.Mace },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Plate, ArmorType.Mail },
            UsesShield = true,
            UsesDualWield = false
        };

        // Wizard - Light armor, staves/wands
        _classTemplates[ActorClass.Wizard] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Staff, WeaponType.Wand },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Cloth, ArmorType.Leather },
            UsesShield = false,
            UsesDualWield = false
        };

        // Rogue - Light armor, daggers, dual wield
        _classTemplates[ActorClass.Rogue] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Dagger, WeaponType.Sword },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Leather, ArmorType.Cloth },
            UsesShield = false,
            UsesDualWield = true
        };

        // Cleric - Medium armor, maces, shields
        _classTemplates[ActorClass.Cleric] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Mace, WeaponType.Staff },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Mail, ArmorType.Plate },
            UsesShield = true,
            UsesDualWield = false
        };

        // Ranger - Medium armor, bows/crossbows
        _classTemplates[ActorClass.Ranger] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Bow, WeaponType.Crossbow, WeaponType.Sword },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Leather, ArmorType.Mail },
            UsesShield = false,
            UsesDualWield = false
        };

        // Paladin - Heavy armor, swords/maces, shields
        _classTemplates[ActorClass.Paladin] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Sword, WeaponType.Mace },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Plate },
            UsesShield = true,
            UsesDualWield = false
        };

        // Barbarian - Medium armor, greataxes/hammers, dual wield
        _classTemplates[ActorClass.Barbarian] = new ClassEquipmentTemplate
        {
            PreferredWeaponTypes = new List<WeaponType> { WeaponType.Greataxe, WeaponType.Hammer },
            PreferredArmorTypes = new List<ArmorType> { ArmorType.Leather, ArmorType.Mail },
            UsesShield = false,
            UsesDualWield = true
        };
    }
}

public class ClassEquipmentTemplate
{
    public List<WeaponType> PreferredWeaponTypes { get; set; } = new();
    public List<ArmorType> PreferredArmorTypes { get; set; } = new();
    public bool UsesShield { get; set; }
    public bool UsesDualWield { get; set; }
}
