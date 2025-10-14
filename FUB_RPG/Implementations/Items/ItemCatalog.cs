using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Items;
using Fub.Implementations.Items.Weapons;

namespace Fub.Implementations.Items;

/// <summary>
/// Central catalog of all consumable items, materials, and special items in the game
/// </summary>
public static class ItemCatalog
{
    /// <summary>
    /// Gets all consumable items including potions, elixirs, and restoration items
    /// </summary>
    public static List<IItem> GetAllConsumables()
    {
        var items = new List<IItem>();
        
        // HP Potions - Various tiers
        items.AddRange(GetHealthPotions());
        
        // MP Potions - Various tiers
        items.AddRange(GetManaPotions());
        
        // TP (Stamina) Potions - Various tiers
        items.AddRange(GetStaminaPotions());
        
        // Elixirs - Multi-resource restoration
        items.AddRange(GetElixirs());
        
        // Status Effect Removers
        items.AddRange(GetStatusEffectRemovers());
        
        // Special Consumables
        items.AddRange(GetSpecialConsumables());
        
        return items;
    }

    /// <summary>
    /// Gets all key items (quest items, special keys, etc.)
    /// </summary>
    public static List<IItem> GetAllKeyItems()
    {
        return new List<IItem>
        {
            new SimpleItem("Rusty Key", ItemType.Quest, RarityTier.Common, false, 1, 5m, 0.1f, 
                "An old, rusty key. Might open a forgotten door."),
            
            new SimpleItem("Iron Key", ItemType.Quest, RarityTier.Common, false, 1, 25m, 0.2f,
                "A sturdy iron key. Opens common locked chests."),
            
            new SimpleItem("Silver Key", ItemType.Quest, RarityTier.Uncommon, false, 1, 100m, 0.2f,
                "A well-crafted silver key. Opens uncommon locked chests."),
            
            new SimpleItem("Gold Key", ItemType.Quest, RarityTier.Rare, false, 1, 500m, 0.3f,
                "An ornate golden key. Opens rare treasure chests."),
            
            new SimpleItem("Master Key", ItemType.Quest, RarityTier.Epic, false, 1, 2500m, 0.3f,
                "A master crafted key. Opens most locks with ease."),
            
            new SimpleItem("Skeleton Key", ItemType.Quest, RarityTier.Legendary, false, 1, 10000m, 0.2f,
                "A mystical key that can open any mundane lock."),
            
            new SimpleItem("Ancient Seal Fragment", ItemType.Quest, RarityTier.Epic, true, 10, 1000m, 0.5f,
                "A fragment of an ancient magical seal. Collect all pieces to unlock something powerful."),
            
            new SimpleItem("Mysterious Orb", ItemType.Quest, RarityTier.Rare, false, 1, 500m, 1.0f,
                "A glowing orb that pulses with strange energy."),
            
            new SimpleItem("Dungeon Map", ItemType.Quest, RarityTier.Common, false, 1, 50m, 0.1f,
                "A map revealing the layout of nearby dungeons."),
            
            new SimpleItem("Teleport Crystal", ItemType.Quest, RarityTier.Uncommon, true, 10, 150m, 0.2f,
                "A crystal that can teleport you back to the last safe point.")
        };
    }

    /// <summary>
    /// Gets all crafting materials
    /// </summary>
    public static List<IItem> GetAllMaterials()
    {
        return new List<IItem>
        {
            // Ores and Metals
            new SimpleItem("Iron Ore", ItemType.Material, RarityTier.Common, true, 99, 5m, 2.0f,
                "Raw iron ore. Can be smelted into ingots."),
            
            new SimpleItem("Copper Ore", ItemType.Material, RarityTier.Common, true, 99, 3m, 1.5f,
                "Raw copper ore. Useful for basic crafting."),
            
            new SimpleItem("Silver Ore", ItemType.Material, RarityTier.Uncommon, true, 99, 15m, 1.8f,
                "Raw silver ore. Used in magical item crafting."),
            
            new SimpleItem("Gold Ore", ItemType.Material, RarityTier.Rare, true, 99, 50m, 2.5f,
                "Raw gold ore. Precious and valuable."),
            
            new SimpleItem("Mithril Ore", ItemType.Material, RarityTier.Epic, true, 50, 200m, 1.0f,
                "Rare mithril ore. Incredibly light yet strong."),
            
            new SimpleItem("Adamantite Ore", ItemType.Material, RarityTier.Legendary, true, 20, 1000m, 3.0f,
                "Legendary adamantite ore. The strongest metal known."),
            
            // Magical Materials
            new SimpleItem("Magic Crystal", ItemType.Material, RarityTier.Uncommon, true, 50, 25m, 0.1f,
                "A crystallized form of magical energy."),
            
            new SimpleItem("Mana Shard", ItemType.Material, RarityTier.Rare, true, 50, 75m, 0.2f,
                "A shard pulsing with concentrated mana."),
            
            new SimpleItem("Dragon Scale", ItemType.Material, RarityTier.Epic, true, 20, 500m, 0.5f,
                "A scale from a mighty dragon. Extremely durable."),
            
            new SimpleItem("Phoenix Feather", ItemType.Material, RarityTier.Legendary, true, 10, 2000m, 0.1f,
                "A feather from a phoenix. Radiates warmth and life energy."),
            
            // Herbs and Plants
            new SimpleItem("Healing Herb", ItemType.Material, RarityTier.Common, true, 99, 2m, 0.1f,
                "A common herb with mild healing properties."),
            
            new SimpleItem("Mana Flower", ItemType.Material, RarityTier.Uncommon, true, 99, 8m, 0.1f,
                "A blue flower that absorbs ambient mana."),
            
            new SimpleItem("Moonleaf", ItemType.Material, RarityTier.Rare, true, 50, 30m, 0.1f,
                "A rare plant that only grows under moonlight."),
            
            new SimpleItem("Sunblossom", ItemType.Material, RarityTier.Rare, true, 50, 35m, 0.1f,
                "A golden flower that blooms in sunlight."),
            
            // Monster Parts
            new SimpleItem("Slime Gel", ItemType.Material, RarityTier.Common, true, 99, 1m, 0.5f,
                "Viscous gel from a slime. Used in alchemy."),
            
            new SimpleItem("Wolf Pelt", ItemType.Material, RarityTier.Common, true, 50, 10m, 1.0f,
                "A thick pelt from a wolf. Good for leather crafting."),
            
            new SimpleItem("Goblin Tooth", ItemType.Material, RarityTier.Common, true, 99, 2m, 0.1f,
                "A sharp tooth from a goblin."),
            
            new SimpleItem("Demon Horn", ItemType.Material, RarityTier.Rare, true, 20, 100m, 0.8f,
                "A curved horn from a demon. Radiates dark energy."),
            
            // Essence and Reagents
            new SimpleItem("Essence of Fire", ItemType.Material, RarityTier.Uncommon, true, 50, 20m, 0.1f,
                "Bottled essence of elemental fire."),
            
            new SimpleItem("Essence of Ice", ItemType.Material, RarityTier.Uncommon, true, 50, 20m, 0.1f,
                "Bottled essence of elemental ice."),
            
            new SimpleItem("Essence of Lightning", ItemType.Material, RarityTier.Rare, true, 50, 40m, 0.1f,
                "Bottled essence of elemental lightning."),
            
            new SimpleItem("Soul Fragment", ItemType.Material, RarityTier.Epic, true, 20, 250m, 0.1f,
                "A fragment of a powerful soul. Handle with care.")
        };
    }

    private static List<IItem> GetHealthPotions()
    {
        return new List<IItem>
        {
            new ConsumableItem("Minor HP Potion", RarityTier.Common, ResourceType.Health, 50, 0f, false,
                maxStackSize: 99, baseValue: 10m, weight: 0.3f),
            
            new ConsumableItem("HP Potion", RarityTier.Common, ResourceType.Health, 150, 0f, false,
                maxStackSize: 99, baseValue: 30m, weight: 0.4f),
            
            new ConsumableItem("Greater HP Potion", RarityTier.Uncommon, ResourceType.Health, 300, 0f, false,
                maxStackSize: 99, baseValue: 75m, weight: 0.5f),
            
            new ConsumableItem("Superior HP Potion", RarityTier.Rare, ResourceType.Health, 600, 0f, false,
                maxStackSize: 50, baseValue: 200m, weight: 0.6f),
            
            new ConsumableItem("Mega HP Potion", RarityTier.Epic, ResourceType.Health, 1200, 0f, false,
                maxStackSize: 50, baseValue: 500m, weight: 0.7f),
            
            new ConsumableItem("Ultra HP Potion", RarityTier.Legendary, ResourceType.Health, 2500, 0f, false,
                maxStackSize: 20, baseValue: 1500m, weight: 0.8f),
            
            // Percentage-based HP potions
            new ConsumableItem("HP Potion (25%)", RarityTier.Uncommon, ResourceType.Health, 0, 0.25f, true,
                maxStackSize: 50, baseValue: 100m, weight: 0.4f),
            
            new ConsumableItem("HP Potion (50%)", RarityTier.Rare, ResourceType.Health, 0, 0.50f, true,
                maxStackSize: 30, baseValue: 250m, weight: 0.5f),
            
            new ConsumableItem("HP Potion (100%)", RarityTier.Epic, ResourceType.Health, 0, 1.0f, true,
                maxStackSize: 10, baseValue: 800m, weight: 0.6f)
        };
    }

    private static List<IItem> GetManaPotions()
    {
        return new List<IItem>
        {
            new ConsumableItem("Minor MP Potion", RarityTier.Common, ResourceType.Mana, 40, 0f, false,
                maxStackSize: 99, baseValue: 12m, weight: 0.3f),
            
            new ConsumableItem("MP Potion", RarityTier.Common, ResourceType.Mana, 120, 0f, false,
                maxStackSize: 99, baseValue: 35m, weight: 0.4f),
            
            new ConsumableItem("Greater MP Potion", RarityTier.Uncommon, ResourceType.Mana, 250, 0f, false,
                maxStackSize: 99, baseValue: 80m, weight: 0.5f),
            
            new ConsumableItem("Superior MP Potion", RarityTier.Rare, ResourceType.Mana, 500, 0f, false,
                maxStackSize: 50, baseValue: 220m, weight: 0.6f),
            
            new ConsumableItem("Mega MP Potion", RarityTier.Epic, ResourceType.Mana, 1000, 0f, false,
                maxStackSize: 50, baseValue: 550m, weight: 0.7f),
            
            new ConsumableItem("Ultra MP Potion", RarityTier.Legendary, ResourceType.Mana, 2000, 0f, false,
                maxStackSize: 20, baseValue: 1600m, weight: 0.8f),
            
            // Percentage-based MP potions
            new ConsumableItem("MP Potion (25%)", RarityTier.Uncommon, ResourceType.Mana, 0, 0.25f, true,
                maxStackSize: 50, baseValue: 110m, weight: 0.4f),
            
            new ConsumableItem("MP Potion (50%)", RarityTier.Rare, ResourceType.Mana, 0, 0.50f, true,
                maxStackSize: 30, baseValue: 280m, weight: 0.5f),
            
            new ConsumableItem("MP Potion (100%)", RarityTier.Epic, ResourceType.Mana, 0, 1.0f, true,
                maxStackSize: 10, baseValue: 900m, weight: 0.6f)
        };
    }

    private static List<IItem> GetStaminaPotions()
    {
        return new List<IItem>
        {
            new ConsumableItem("Minor TP Potion", RarityTier.Common, ResourceType.Stamina, 30, 0f, false,
                maxStackSize: 99, baseValue: 8m, weight: 0.3f),
            
            new ConsumableItem("TP Potion", RarityTier.Common, ResourceType.Stamina, 100, 0f, false,
                maxStackSize: 99, baseValue: 25m, weight: 0.4f),
            
            new ConsumableItem("Greater TP Potion", RarityTier.Uncommon, ResourceType.Stamina, 200, 0f, false,
                maxStackSize: 99, baseValue: 65m, weight: 0.5f),
            
            new ConsumableItem("Superior TP Potion", RarityTier.Rare, ResourceType.Stamina, 400, 0f, false,
                maxStackSize: 50, baseValue: 180m, weight: 0.6f),
            
            new ConsumableItem("Mega TP Potion", RarityTier.Epic, ResourceType.Stamina, 800, 0f, false,
                maxStackSize: 50, baseValue: 450m, weight: 0.7f),
            
            new ConsumableItem("Ultra TP Potion", RarityTier.Legendary, ResourceType.Stamina, 1600, 0f, false,
                maxStackSize: 20, baseValue: 1400m, weight: 0.8f),
            
            // Percentage-based TP potions
            new ConsumableItem("TP Potion (25%)", RarityTier.Uncommon, ResourceType.Stamina, 0, 0.25f, true,
                maxStackSize: 50, baseValue: 90m, weight: 0.4f),
            
            new ConsumableItem("TP Potion (50%)", RarityTier.Rare, ResourceType.Stamina, 0, 0.50f, true,
                maxStackSize: 30, baseValue: 230m, weight: 0.5f),
            
            new ConsumableItem("TP Potion (100%)", RarityTier.Epic, ResourceType.Stamina, 0, 1.0f, true,
                maxStackSize: 10, baseValue: 750m, weight: 0.6f)
        };
    }

    private static List<IItem> GetElixirs()
    {
        return new List<IItem>
        {
            new ConsumableItem("Minor Elixir", RarityTier.Uncommon, restoresAllResources: true,
                specialEffect: "Restores 30% of all resources.",
                maxStackSize: 20, baseValue: 200m, weight: 0.8f),
            
            new ConsumableItem("Elixir", RarityTier.Rare, restoresAllResources: true,
                specialEffect: "Restores 60% of all resources.",
                maxStackSize: 10, baseValue: 600m, weight: 1.0f),
            
            new ConsumableItem("Greater Elixir", RarityTier.Epic, restoresAllResources: true,
                specialEffect: "Fully restores all resources.",
                maxStackSize: 5, baseValue: 2000m, weight: 1.2f),
            
            new ConsumableItem("Megalixir", RarityTier.Legendary, restoresAllResources: true,
                specialEffect: "Fully restores all party members' resources.",
                maxStackSize: 3, baseValue: 10000m, weight: 1.5f),
            
            // Combination potions
            new ConsumableItem("HP+MP Potion", RarityTier.Uncommon, 
                specialEffect: "Restores 200 HP and 150 MP.",
                maxStackSize: 50, baseValue: 90m, weight: 0.6f),
            
            new ConsumableItem("HP+TP Potion", RarityTier.Uncommon,
                specialEffect: "Restores 200 HP and 120 TP.",
                maxStackSize: 50, baseValue: 80m, weight: 0.6f),
            
            new ConsumableItem("MP+TP Potion", RarityTier.Uncommon,
                specialEffect: "Restores 150 MP and 120 TP.",
                maxStackSize: 50, baseValue: 85m, weight: 0.6f)
        };
    }

    private static List<IItem> GetStatusEffectRemovers()
    {
        return new List<IItem>
        {
            new ConsumableItem("Smelling Salts", RarityTier.Common, removesStatusEffects: true,
                specialEffect: "Removes sleep, confusion, and stun effects.",
                maxStackSize: 99, baseValue: 20m, weight: 0.2f),
            
            new ConsumableItem("Antidote", RarityTier.Common, removesStatusEffects: true,
                specialEffect: "Cures poison status.",
                maxStackSize: 99, baseValue: 15m, weight: 0.2f),
            
            new ConsumableItem("Eye Drops", RarityTier.Common, removesStatusEffects: true,
                specialEffect: "Cures blind status.",
                maxStackSize: 99, baseValue: 15m, weight: 0.2f),
            
            new ConsumableItem("Echo Herbs", RarityTier.Common, removesStatusEffects: true,
                specialEffect: "Cures silence status.",
                maxStackSize: 99, baseValue: 18m, weight: 0.2f),
            
            new ConsumableItem("Panacea", RarityTier.Rare, removesStatusEffects: true,
                specialEffect: "Cures all negative status effects.",
                maxStackSize: 20, baseValue: 300m, weight: 0.4f),
            
            new ConsumableItem("Holy Water", RarityTier.Uncommon, removesStatusEffects: true,
                specialEffect: "Removes curses and undead-inflicted debuffs.",
                maxStackSize: 50, baseValue: 100m, weight: 0.5f),
            
            new ConsumableItem("Remedy", RarityTier.Rare, removesStatusEffects: true,
                specialEffect: "Cures all status effects and restores 100 HP.",
                maxStackSize: 30, baseValue: 400m, weight: 0.5f)
        };
    }

    private static List<IItem> GetSpecialConsumables()
    {
        return new List<IItem>
        {
            new ConsumableItem("Phoenix Down", RarityTier.Rare, 
                specialEffect: "Revives a fallen ally with 50% HP.",
                maxStackSize: 10, baseValue: 500m, weight: 0.3f),
            
            new ConsumableItem("Mega Phoenix", RarityTier.Legendary,
                specialEffect: "Revives all fallen allies with 100% HP.",
                maxStackSize: 3, baseValue: 5000m, weight: 0.5f),
            
            new ConsumableItem("Ether", RarityTier.Rare, ResourceType.Mana, 0, 1.0f, true,
                specialEffect: "Fully restores MP.",
                maxStackSize: 20, baseValue: 1000m, weight: 0.4f),
            
            new ConsumableItem("Turbo Ether", RarityTier.Epic, ResourceType.Mana,
                specialEffect: "Fully restores MP instantly.",
                maxStackSize: 10, baseValue: 2500m, weight: 0.4f),
            
            new ConsumableItem("Power Tonic", RarityTier.Uncommon,
                specialEffect: "Temporarily increases attack by 20% for 5 turns.",
                maxStackSize: 50, baseValue: 150m, weight: 0.3f),
            
            new ConsumableItem("Guard Tonic", RarityTier.Uncommon,
                specialEffect: "Temporarily increases defense by 20% for 5 turns.",
                maxStackSize: 50, baseValue: 150m, weight: 0.3f),
            
            new ConsumableItem("Speed Tonic", RarityTier.Uncommon,
                specialEffect: "Temporarily increases speed by 25% for 5 turns.",
                maxStackSize: 50, baseValue: 160m, weight: 0.3f),
            
            new ConsumableItem("Hero's Elixir", RarityTier.Epic,
                specialEffect: "Temporarily increases all stats by 30% for 10 turns.",
                maxStackSize: 5, baseValue: 3000m, weight: 0.8f),
            
            new ConsumableItem("Experience Potion", RarityTier.Rare,
                specialEffect: "Grants 500 bonus experience points when used.",
                maxStackSize: 20, baseValue: 400m, weight: 0.5f),
            
            new ConsumableItem("Lucky Charm", RarityTier.Rare,
                specialEffect: "Increases item drop rate by 50% for 10 battles.",
                maxStackSize: 10, baseValue: 800m, weight: 0.2f),
            
            new ConsumableItem("Smoke Bomb", RarityTier.Common,
                specialEffect: "Guarantees escape from battle.",
                maxStackSize: 99, baseValue: 50m, weight: 0.2f),
            
            new ConsumableItem("Tent", RarityTier.Common,
                specialEffect: "Fully restores party at a camp site.",
                maxStackSize: 10, baseValue: 300m, weight: 5.0f),
            
            new ConsumableItem("Cottage", RarityTier.Uncommon,
                specialEffect: "Fully restores party and removes all status effects at a camp site.",
                maxStackSize: 5, baseValue: 800m, weight: 10.0f)
        };
    }

    /// <summary>
    /// Gets all weapons across all tiers (410+ weapons: 41 types × 10 tiers)
    /// </summary>
    public static List<IItem> GetAllWeapons()
    {
        var weapons = new List<IItem>();
        
        // Generate all weapon types across all tiers
        var allWeaponTypes = System.Enum.GetValues<WeaponType>();
        var allTiers = System.Enum.GetValues<EquipmentTier>();
        
        foreach (var weaponType in allWeaponTypes)
        {
            foreach (var tier in allTiers)
            {
                weapons.Add(CreateWeapon(weaponType, tier));
            }
        }
        
        return weapons;
    }

    /// <summary>
    /// Gets weapons of a specific type across all tiers
    /// </summary>
    public static List<IItem> GetWeaponsByType(WeaponType weaponType)
    {
        var weapons = new List<IItem>();
        var allTiers = System.Enum.GetValues<EquipmentTier>();
        
        foreach (var tier in allTiers)
        {
            weapons.Add(CreateWeapon(weaponType, tier));
        }
        
        return weapons;
    }

    /// <summary>
    /// Gets all weapons for a specific tier
    /// </summary>
    public static List<IItem> GetWeaponsByTier(EquipmentTier tier)
    {
        var weapons = new List<IItem>();
        var allWeaponTypes = System.Enum.GetValues<WeaponType>();
        
        foreach (var weaponType in allWeaponTypes)
        {
            weapons.Add(CreateWeapon(weaponType, tier));
        }
        
        return weapons;
    }

    private static Weapon CreateWeapon(WeaponType weaponType, EquipmentTier tier)
    {
        var tierName = GetTierPrefix(tier);
        var weaponName = GetWeaponBaseName(weaponType);
        var fullName = $"{tierName} {weaponName}";
        
        var rarity = GetRarityForTier(tier);
        var requiredLevel = GetRequiredLevelForTier(tier);
        var allowedClasses = GetClassesForWeaponType(weaponType);
        
        return new Weapon(
            name: fullName,
            weaponType: weaponType,
            rarity: rarity,
            requiredLevel: requiredLevel,
            allowedClasses: allowedClasses,
            tier: tier
        );
    }

    private static string GetTierPrefix(EquipmentTier tier)
    {
        return tier switch
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
    }

    private static string GetWeaponBaseName(WeaponType weaponType)
    {
        return weaponType switch
        {
            // General
            WeaponType.Toolkit => "Toolkit",
            
            // Melee
            WeaponType.Sword => "Sword",
            WeaponType.Mace => "Mace",
            WeaponType.HolySword => "Holy Sword",
            WeaponType.Greatsword => "Greatsword",
            WeaponType.Gunblade => "Gunblade",
            WeaponType.Greataxe => "Greataxe",
            WeaponType.Handwraps => "Handwraps",
            WeaponType.Katana => "Katana",
            WeaponType.Spear => "Spear",
            WeaponType.Kunai => "Kunai",
            WeaponType.Scythe => "Scythe",
            WeaponType.Dagger => "Dagger",
            WeaponType.Scimitar => "Scimitar",
            
            // Ranged
            WeaponType.Bow => "Bow",
            WeaponType.Crossbow => "Crossbow",
            WeaponType.Firearm => "Firearm",
            WeaponType.Chakrams => "Chakrams",
            WeaponType.Lute => "Lute",
            
            // Magic / Support
            WeaponType.Wand => "Wand",
            WeaponType.Orb => "Orb",
            WeaponType.PactTome => "Pact Tome",
            WeaponType.Rod => "Rod",
            WeaponType.Staff => "Staff",
            WeaponType.Rapier => "Rapier",
            WeaponType.Cane => "Cane",
            WeaponType.Grimoire => "Grimoire",
            WeaponType.Codex => "Codex",
            WeaponType.Astrolabe => "Astrolabe",
            WeaponType.Nouliths => "Nouliths",
            WeaponType.Focus => "Focus",
            WeaponType.MultiTool => "Multi-Tool",
            
            // Crafting
            WeaponType.Saw => "Saw",
            WeaponType.Hammer => "Hammer",
            WeaponType.RaisingHammer => "Raising Hammer",
            WeaponType.ChasingHammer => "Chasing Hammer",
            WeaponType.HeadKnife => "Head Knife",
            WeaponType.Needle => "Needle",
            
            _ => weaponType.ToString()
        };
    }

    public static RarityTier GetRarityForTier(EquipmentTier tier)
    {
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

    private static int GetRequiredLevelForTier(EquipmentTier tier)
    {
        return ((int)tier - 1) * 10 + 1; // Tier 1 = Level 1, Tier 2 = Level 11, etc.
    }

    private static IEnumerable<ActorClass> GetClassesForWeaponType(WeaponType weaponType)
    {
        return weaponType switch
        {
            // General
            WeaponType.Toolkit => new[] { ActorClass.Adventurer },
            
            // Melee
            WeaponType.Sword => new[] { ActorClass.Warrior },
            WeaponType.Mace => new[] { ActorClass.Cleric },
            WeaponType.HolySword => new[] { ActorClass.Paladin },
            WeaponType.Greatsword => new[] { ActorClass.DarkKnight },
            WeaponType.Gunblade => new[] { ActorClass.Gunbreaker },
            WeaponType.Greataxe => new[] { ActorClass.Barbarian },
            WeaponType.Handwraps => new[] { ActorClass.Monk },
            WeaponType.Katana => new[] { ActorClass.Samurai },
            WeaponType.Spear => new[] { ActorClass.Dragoon },
            WeaponType.Kunai => new[] { ActorClass.Ninja },
            WeaponType.Scythe => new[] { ActorClass.Reaper },
            WeaponType.Dagger => new[] { ActorClass.Rogue },
            WeaponType.Scimitar => new[] { ActorClass.Druid },
            
            // Ranged
            WeaponType.Bow => new[] { ActorClass.Ranger },
            WeaponType.Crossbow => new[] { ActorClass.Hunter },
            WeaponType.Firearm => new[] { ActorClass.Machinist },
            WeaponType.Chakrams => new[] { ActorClass.Dancer },
            WeaponType.Lute => new[] { ActorClass.Bard },
            
            // Magic / Support
            WeaponType.Wand => new[] { ActorClass.Wizard },
            WeaponType.Orb => new[] { ActorClass.Sorcerer },
            WeaponType.PactTome => new[] { ActorClass.Warlock },
            WeaponType.Rod => new[] { ActorClass.BlackMage },
            WeaponType.Staff => new[] { ActorClass.WhiteMage },
            WeaponType.Rapier => new[] { ActorClass.RedMage },
            WeaponType.Cane => new[] { ActorClass.BlueMage },
            WeaponType.Grimoire => new[] { ActorClass.Summoner },
            WeaponType.Codex => new[] { ActorClass.Scholar },
            WeaponType.Astrolabe => new[] { ActorClass.Astrologian },
            WeaponType.Nouliths => new[] { ActorClass.Sage },
            WeaponType.Focus => new[] { ActorClass.Necromancer },
            WeaponType.MultiTool => new[] { ActorClass.Artificer },
            
            // Crafting
            WeaponType.Saw => new[] { ActorClass.Carpenter },
            WeaponType.Hammer => new[] { ActorClass.Blacksmith },
            WeaponType.RaisingHammer => new[] { ActorClass.Armorer },
            WeaponType.ChasingHammer => new[] { ActorClass.Goldsmith },
            WeaponType.HeadKnife => new[] { ActorClass.Leatherworker },
            WeaponType.Needle => new[] { ActorClass.Weaver },
            
            _ => System.Enum.GetValues<ActorClass>()
        };
    }
}
