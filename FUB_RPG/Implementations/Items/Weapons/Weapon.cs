using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Items;
using Fub.Interfaces.Items.Equipment;

namespace Fub.Implementations.Items.Weapons;

public sealed class Weapon : ItemBase, IEquipment
{
    private static readonly System.Random s_rng = new System.Random();
    
    public EquipmentSlot Slot { get; }
    public int RequiredLevel { get; }
    public IReadOnlyCollection<ActorClass> AllowedClasses { get; }
    public IReadOnlyDictionary<StatType, double> StatRequirements { get; }
    public EquipmentTier Tier { get; }
    public WeaponType WeaponType { get; }
    public DamageType DamageType { get; }
    public double MinDamage { get; }
    public double MaxDamage { get; }
    public double Speed { get; }

    // Constructor for tier-based equipment generation (my new system)
    public Weapon(
        string name,
        WeaponType weaponType,
        RarityTier rarity,
        int requiredLevel = 1,
        IEnumerable<ActorClass>? allowedClasses = null,
        IReadOnlyDictionary<StatType, double>? statRequirements = null,
        EquipmentTier tier = EquipmentTier.Simple)
        : base(name, ItemType.Weapon, rarity)
    {
        WeaponType = weaponType;
        Slot = DetermineSlot(weaponType);
        RequiredLevel = requiredLevel;
        AllowedClasses = allowedClasses?.ToList() ?? new List<ActorClass>(System.Enum.GetValues<ActorClass>());
        StatRequirements = statRequirements ?? new Dictionary<StatType, double>();
        Tier = tier;
        DamageType = DamageType.Physical; // Default
        
        // Calculate damage and speed based on weapon type and tier
        (MinDamage, MaxDamage) = CalculateDamageForTier(weaponType, tier, rarity);
        Speed = CalculateSpeedForWeapon(weaponType, tier);

        // Set appropriate base value and weight based on tier and weapon type
        BaseValue = CalculateBaseValue(tier, rarity, weaponType);
        Weight = CalculateWeight(tier, weaponType);
    }

    // Constructor for existing MapContentPopulator compatibility
    public Weapon(
        string name,
        WeaponType weaponType,
        DamageType damageType,
        double minDamage,
        double maxDamage,
        double speed,
        RarityTier rarity,
        EquipmentSlot slot,
        int requiredLevel = 1,
        IEnumerable<ActorClass>? allowedClasses = null,
        IReadOnlyDictionary<StatType, double>? statRequirements = null,
        EquipmentTier tier = EquipmentTier.Simple)
        : base(name, ItemType.Weapon, rarity)
    {
        WeaponType = weaponType;
        DamageType = damageType;
        MinDamage = minDamage;
        MaxDamage = maxDamage;
        Speed = speed;
        Slot = slot;
        RequiredLevel = requiredLevel;
        AllowedClasses = allowedClasses?.ToList() ?? new List<ActorClass>(System.Enum.GetValues<ActorClass>());
        StatRequirements = statRequirements ?? new Dictionary<StatType, double>();
        Tier = tier;

        // Calculate base value from damage values for existing weapons
        BaseValue = CalculateBaseValueFromDamage(minDamage, maxDamage, speed, rarity);
        Weight = CalculateWeight(tier, weaponType);
    }

    private static EquipmentSlot DetermineSlot(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Dagger when s_rng.NextDouble() < 0.5 => EquipmentSlot.OffHand,
            _ => EquipmentSlot.MainHand
        };
    }

    private (double min, double max) CalculateDamageForTier(WeaponType weaponType, EquipmentTier tier, RarityTier rarity)
    {
        // Base damage scaling by tier
        var tierMultiplier = (int)tier * 2.0;
        var rarityMultiplier = rarity switch
        {
            RarityTier.Common => 1.0,
            RarityTier.Uncommon => 1.3,
            RarityTier.Rare => 1.6,
            RarityTier.Epic => 2.0,
            RarityTier.Legendary => 2.5,
            RarityTier.Mythic => 3.0,
            _ => 1.0
        };

        var weaponDamageMultiplier = weaponType switch
        {
            WeaponType.Dagger => 0.8,
            WeaponType.Sword => 1.0,
            WeaponType.Greataxe => 1.2,
            WeaponType.Mace => 1.1,
            WeaponType.Bow => 0.9,
            WeaponType.Crossbow => 1.1,
            WeaponType.Staff => 0.7,
            WeaponType.Wand => 0.6,
            WeaponType.Spear => 1.0,
            WeaponType.Hammer => 1.3,
            _ => 1.0
        };

        var baseDamage = tierMultiplier * rarityMultiplier * weaponDamageMultiplier;
        var minDamage = Math.Max(1, baseDamage * 0.8);
        var maxDamage = baseDamage * 1.2;

        return (minDamage, maxDamage);
    }

    private double CalculateSpeedForWeapon(WeaponType weaponType, EquipmentTier tier)
    {
        var baseSpeed = weaponType switch
        {
            WeaponType.Dagger => 1.4,
            WeaponType.Sword => 1.0,
            WeaponType.Greataxe => 0.8,
            WeaponType.Mace => 0.9,
            WeaponType.Bow => 1.1,
            WeaponType.Crossbow => 0.7,
            WeaponType.Staff => 1.2,
            WeaponType.Wand => 1.5,
            WeaponType.Spear => 1.0,
            WeaponType.Hammer => 0.6,
            _ => 1.0
        };

        // Higher tier weapons are slightly faster
        var tierBonus = (int)tier * 0.05;
        return Math.Round(baseSpeed + tierBonus, 2);
    }

    private decimal CalculateBaseValue(EquipmentTier tier, RarityTier rarity, WeaponType weaponType)
    {
        var baseTierValue = (int)tier * 15; // Weapons are slightly more valuable than armor
        
        var rarityMultiplier = rarity switch
        {
            RarityTier.Common => 1.0m,
            RarityTier.Uncommon => 1.5m,
            RarityTier.Rare => 2.5m,
            RarityTier.Epic => 4.0m,
            RarityTier.Legendary => 7.0m,
            RarityTier.Mythic => 12.0m,
            _ => 1.0m
        };
        
        var weaponMultiplier = weaponType switch
        {
            WeaponType.Dagger => 0.8m,
            WeaponType.Sword => 1.2m,
            WeaponType.Greataxe => 1.1m,
            WeaponType.Mace => 1.0m,
            WeaponType.Bow => 1.3m,
            WeaponType.Crossbow => 1.4m,
            WeaponType.Staff => 1.1m,
            WeaponType.Wand => 0.9m,
            WeaponType.Spear => 1.0m,
            WeaponType.Hammer => 1.2m,
            _ => 1.0m
        };

        return baseTierValue * rarityMultiplier * weaponMultiplier;
    }

    private decimal CalculateBaseValueFromDamage(double minDamage, double maxDamage, double speed, RarityTier rarity)
    {
        var averageDamage = (minDamage + maxDamage) / 2.0;
        var dps = averageDamage * speed;
        var baseValue = (decimal)(dps * 8); // Base conversion factor

        var rarityMultiplier = rarity switch
        {
            RarityTier.Common => 1.0m,
            RarityTier.Uncommon => 1.5m,
            RarityTier.Rare => 2.5m,
            RarityTier.Epic => 4.0m,
            RarityTier.Legendary => 7.0m,
            RarityTier.Mythic => 12.0m,
            _ => 1.0m
        };

        return baseValue * rarityMultiplier;
    }

    private float CalculateWeight(EquipmentTier tier, WeaponType weaponType)
    {
        var baseWeight = weaponType switch
        {
            WeaponType.Dagger => 1.0f,
            WeaponType.Sword => 3.0f,
            WeaponType.Greataxe => 4.0f,
            WeaponType.Mace => 3.5f,
            WeaponType.Bow => 2.0f,
            WeaponType.Crossbow => 5.0f,
            WeaponType.Staff => 2.5f,
            WeaponType.Wand => 0.5f,
            WeaponType.Spear => 3.0f,
            WeaponType.Hammer => 6.0f,
            _ => 2.0f
        };

        // Higher tier weapons are slightly heavier due to better materials
        return baseWeight * (1.0f + (int)tier * 0.05f);
    }
}
