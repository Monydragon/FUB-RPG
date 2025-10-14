using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Random;
using Fub.Implementations.Items;
using Fub.Implementations.Items.Equipment;
using Fub.Implementations.Items.Weapons;
using Fub.Implementations.Loot;
using Fub.Implementations.Combat;
using Fub.Implementations.Map;
using Fub.Interfaces.Actors;

namespace Fub.Examples;

/// <summary>
/// Demonstrates the complete tier-based equipment and loot system with expanded consumables
/// Shows how every class gets appropriate equipment for each 10-level tier
/// </summary>
public class EquipmentSystemExample
{
    private readonly TierBasedEquipmentGenerator _equipmentGenerator;
    private readonly AdvancedLootGenerator _lootGenerator;
    private readonly ChestManager _chestManager;
    private readonly VictoryScreenManager _victoryManager;
    private readonly IRandomSource _random;

    public EquipmentSystemExample(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        
        _equipmentGenerator = new TierBasedEquipmentGenerator(_random);
        _lootGenerator = new AdvancedLootGenerator(_random);
        _chestManager = new ChestManager(_lootGenerator, _random);
        _victoryManager = new VictoryScreenManager(_lootGenerator);
        
        InitializeItemDatabase();
        SetupLootConfigurations();
    }

    /// <summary>
    /// Demonstrates generating complete equipment sets for all classes across all 10 tiers (1-100)
    /// </summary>
    public void DemonstrateClassEquipmentGeneration()
    {
        Console.WriteLine("=== CLASS EQUIPMENT GENERATION ===");
        Console.WriteLine("Generating equipment for all classes across all 10 tiers (levels 1-100)");
        Console.WriteLine();

        var allClasses = Enum.GetValues<ActorClass>();
        
        foreach (var actorClass in allClasses)
        {
            Console.WriteLine($"--- {actorClass} Equipment Sets ---");
            
            // Generate equipment for each tier (1-10, representing levels 1-100)
            for (int tierNum = 1; tierNum <= 10; tierNum++)
            {
                var tier = (EquipmentTier)tierNum;
                var levelRange = $"{TierBasedEquipmentGenerator.GetTierBaseLevel(tier)}-{TierBasedEquipmentGenerator.GetTierMaxLevel(tier)}";
                
                Console.WriteLine($"{tier} Tier (Levels {levelRange}):");
                
                var equipmentSet = _equipmentGenerator.GenerateCompleteSet(actorClass, tier);
                
                foreach (var equipment in equipmentSet)
                {
                    var item = equipment.Value;
                    Console.WriteLine($"  {equipment.Key}: {item.Name} (Value: {item.BaseValue:N0} gold, Weight: {item.Weight:F1} lbs)");
                }
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Demonstrates the victory screen with detailed loot drops
    /// </summary>
    public void DemonstrateVictoryScreen()
    {
        Console.WriteLine("=== VICTORY SCREEN DEMONSTRATION ===");
        
        // Simulate a combat victory
        var victoryData = CreateSampleVictoryData();
        var victoryDisplay = _victoryManager.FormatVictoryScreen(victoryData);
        
        Console.WriteLine(victoryDisplay);
    }

    /// <summary>
    /// Demonstrates chest system with respawning mechanics
    /// </summary>
    public void DemonstrateChestSystem()
    {
        Console.WriteLine("=== CHEST SYSTEM DEMONSTRATION ===");
        
        // Create different types of chests
        var woodenChest = _chestManager.CreateChest("default", 10, 15, playerLevel: 5);
        var treasureChest = _chestManager.CreateChest("treasure", 25, 30, playerLevel: 20);
        var supplyCrate = _chestManager.CreateChest("supply_crate", 5, 8, playerLevel: 3);
        
        Console.WriteLine("Created chests:");
        Console.WriteLine($"1. {woodenChest.Name} at ({woodenChest.X}, {woodenChest.Y}) - Can respawn: {woodenChest.CanRespawn}");
        Console.WriteLine($"2. {treasureChest.Name} at ({treasureChest.X}, {treasureChest.Y}) - Requires key: {treasureChest.Configuration.RequiresKey}");
        Console.WriteLine($"3. {supplyCrate.Name} at ({supplyCrate.X}, {supplyCrate.Y}) - Respawn time: {supplyCrate.Configuration.RespawnDelay.TotalMinutes} min");
        Console.WriteLine();
        
        // Open chests and show loot
        Console.WriteLine("Opening chests:");
        
        var loot1 = _chestManager.OpenChest(woodenChest.Id, true, 5);
        DisplayLootDrop("Wooden Chest", loot1);
        
        var loot2 = _chestManager.OpenChest(treasureChest.Id, true, 20); // Has required key
        DisplayLootDrop("Treasure Chest", loot2);
        
        var loot3 = _chestManager.OpenChest(supplyCrate.Id, true, 3);
        DisplayLootDrop("Supply Crate", loot3);
    }

    /// <summary>
    /// Demonstrates item stacking mechanics
    /// </summary>
    public void DemonstrateItemStacking()
    {
        Console.WriteLine("=== ITEM STACKING DEMONSTRATION ===");
        
        // Create stackable items (consumables, materials)
        var healthPotion = CreateSampleItem("Health Potion", ItemType.Consumable, true, 50);
        var ironOre = CreateSampleItem("Iron Ore", ItemType.Material, true, 99);
        
        // Create item stacks
        var potionStack1 = new ItemStack(healthPotion, 10);
        var potionStack2 = new ItemStack(healthPotion, 25);
        var oreStack = new ItemStack(ironOre, 30);
        
        Console.WriteLine("Initial stacks:");
        Console.WriteLine($"Potion Stack 1: {potionStack1} (Total value: {potionStack1.TotalValue:N0} gold)");
        Console.WriteLine($"Potion Stack 2: {potionStack2} (Total value: {potionStack2.TotalValue:N0} gold)");
        Console.WriteLine($"Ore Stack: {oreStack} (Total value: {oreStack.TotalValue:N0} gold)");
        Console.WriteLine();
        
        // Demonstrate merging
        Console.WriteLine("Merging potion stacks:");
        var merged = potionStack1.Merge(potionStack2);
        Console.WriteLine($"Merged {merged} potions");
        Console.WriteLine($"Potion Stack 1 after merge: {potionStack1}");
        Console.WriteLine($"Potion Stack 2 after merge: {potionStack2}");
        Console.WriteLine();
        
        // Demonstrate splitting
        Console.WriteLine("Splitting ore stack:");
        var splitStack = oreStack.Split(15);
        Console.WriteLine($"Original ore stack: {oreStack}");
        Console.WriteLine($"Split off stack: {splitStack}");
    }

    /// <summary>
    /// Shows the tier-level relationship (each tier covers 10 levels)
    /// </summary>
    public void DemonstrateTierLevelSystem()
    {
        Console.WriteLine("=== TIER-LEVEL SYSTEM ===");
        Console.WriteLine("Each tier covers 10 character levels:");
        Console.WriteLine();
        
        for (int tierNum = 1; tierNum <= 10; tierNum++)
        {
            var tier = (EquipmentTier)tierNum;
            var baseLevel = TierBasedEquipmentGenerator.GetTierBaseLevel(tier);
            var maxLevel = TierBasedEquipmentGenerator.GetTierMaxLevel(tier);
            
            Console.WriteLine($"Tier {tierNum} ({tier}): Levels {baseLevel}-{maxLevel}");
        }
        Console.WriteLine();
        
        // Demonstrate level-to-tier conversion
        Console.WriteLine("Example level-to-tier conversions:");
        var testLevels = new[] { 1, 15, 27, 43, 56, 72, 88, 95, 100 };
        
        foreach (var level in testLevels)
        {
            var appropriateTier = TierBasedEquipmentGenerator.GetTierForLevel(level);
            Console.WriteLine($"Level {level} -> {appropriateTier} Tier");
        }
    }

    /// <summary>
    /// Demonstrates the expanded consumable item system
    /// </summary>
    public void DemonstrateConsumableItems()
    {
        Console.WriteLine("=== CONSUMABLE ITEMS CATALOG ===");
        Console.WriteLine();

        var consumables = ItemCatalog.GetAllConsumables();
        
        Console.WriteLine($"Total Consumables: {consumables.Count}");
        Console.WriteLine();

        // Group by rarity
        var grouped = consumables.GroupBy(i => i.Rarity).OrderBy(g => g.Key);
        
        foreach (var group in grouped)
        {
            Console.WriteLine($"--- {group.Key} Consumables ({group.Count()} items) ---");
            foreach (var item in group)
            {
                Console.WriteLine($"  • {item.Name}");
                if (item is ConsumableItem consumable)
                {
                    Console.WriteLine($"    {consumable.Description}");
                    Console.WriteLine($"    Stack: {item.MaxStackSize} | Value: {item.BaseValue:N0}g | Weight: {item.Weight:F1} lbs");
                }
                else
                {
                    Console.WriteLine($"    {item.Description}");
                    Console.WriteLine($"    Stack: {item.MaxStackSize} | Value: {item.BaseValue:N0}g | Weight: {item.Weight:F1} lbs");
                }
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Demonstrates key items and quest items
    /// </summary>
    public void DemonstrateKeyItems()
    {
        Console.WriteLine("=== KEY ITEMS & QUEST ITEMS ===");
        Console.WriteLine();

        var keyItems = ItemCatalog.GetAllKeyItems();
        
        Console.WriteLine($"Total Key Items: {keyItems.Count}");
        Console.WriteLine();

        foreach (var item in keyItems)
        {
            Console.WriteLine($"• {item.Name} ({item.Rarity})");
            Console.WriteLine($"  {item.Description}");
            Console.WriteLine($"  Value: {item.BaseValue:N0}g | Weight: {item.Weight:F1} lbs");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates crafting materials
    /// </summary>
    public void DemonstrateCraftingMaterials()
    {
        Console.WriteLine("=== CRAFTING MATERIALS ===");
        Console.WriteLine();

        var materials = ItemCatalog.GetAllMaterials();
        
        Console.WriteLine($"Total Materials: {materials.Count}");
        Console.WriteLine();

        // Group by rarity
        var grouped = materials.GroupBy(i => i.Rarity).OrderBy(g => g.Key);
        
        foreach (var group in grouped)
        {
            Console.WriteLine($"--- {group.Key} Materials ({group.Count()} items) ---");
            foreach (var item in group)
            {
                Console.WriteLine($"  • {item.Name}");
                Console.WriteLine($"    {item.Description}");
                Console.WriteLine($"    Stack: {item.MaxStackSize} | Value: {item.BaseValue:N0}g | Weight: {item.Weight:F1} lbs");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Demonstrates a sample inventory with various items
    /// </summary>
    public void DemonstrateSampleInventory()
    {
        Console.WriteLine("=== SAMPLE INVENTORY ===");
        Console.WriteLine("Showing a typical adventurer's inventory:");
        Console.WriteLine();

        var inventory = new List<ItemStack>
        {
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "HP Potion"), 15),
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "MP Potion"), 10),
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "TP Potion"), 8),
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "Elixir"), 2),
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "Smelling Salts"), 20),
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "Antidote"), 15),
            new ItemStack(ItemCatalog.GetAllConsumables().First(i => i.Name == "Phoenix Down"), 3),
            new ItemStack(ItemCatalog.GetAllKeyItems().First(i => i.Name == "Iron Key"), 1),
            new ItemStack(ItemCatalog.GetAllMaterials().First(i => i.Name == "Iron Ore"), 25),
            new ItemStack(ItemCatalog.GetAllMaterials().First(i => i.Name == "Magic Crystal"), 12),
            new ItemStack(ItemCatalog.GetAllMaterials().First(i => i.Name == "Healing Herb"), 30)
        };

        decimal totalValue = 0;
        float totalWeight = 0;

        foreach (var stack in inventory)
        {
            Console.WriteLine($"• {stack}");
            totalValue += stack.TotalValue;
            totalWeight += stack.Item.Weight * stack.Quantity;
        }

        Console.WriteLine();
        Console.WriteLine($"Total Items: {inventory.Count} stacks ({inventory.Sum(s => s.Quantity)} individual items)");
        Console.WriteLine($"Total Value: {totalValue:N0} gold");
        Console.WriteLine($"Total Weight: {totalWeight:F1} lbs");
    }

    /// <summary>
    /// Demonstrates loot drops with the new items
    /// </summary>
    public void DemonstrateEnhancedLootDrops()
    {
        Console.WriteLine("=== ENHANCED LOOT DROPS ===");
        Console.WriteLine();

        // Different enemy types with different loot tables
        var scenarios = new[]
        {
            ("Common Slime", 5, new[] { "Minor HP Potion", "Slime Gel", "Healing Herb" }),
            ("Forest Wolf Pack", 12, new[] { "HP Potion", "Wolf Pelt", "Antidote", "TP Potion" }),
            ("Treasure Goblin", 18, new[] { "Gold Ore", "Silver Ore", "Iron Key", "Magic Crystal" }),
            ("Elite Dragon", 50, new[] { "Megalixir", "Dragon Scale", "Phoenix Down", "Mithril Ore", "Elixir" }),
            ("Legendary Boss", 75, new[] { "Ultra HP Potion", "Adamantite Ore", "Phoenix Feather", "Mega Phoenix", "Hero's Elixir" })
        };

        foreach (var (enemyName, level, possibleDrops) in scenarios)
        {
            Console.WriteLine($"--- {enemyName} (Level {level}) ---");
            var lootDrop = CreateEnhancedLootDrop(enemyName, level, possibleDrops);
            
            Console.WriteLine($"Gold: {lootDrop.GoldGained:N0}");
            Console.WriteLine($"Experience: {lootDrop.ExperienceGained:N0}");
            Console.WriteLine("Items:");
            
            foreach (var stack in lootDrop.Items)
            {
                Console.WriteLine($"  • {stack}");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates all weapons in the catalog
    /// </summary>
    public void DemonstrateWeaponsCatalog()
    {
        Console.WriteLine("=== WEAPONS CATALOG ===");
        Console.WriteLine();

        var allWeapons = ItemCatalog.GetAllWeapons();
        
        Console.WriteLine($"Total Weapons: {allWeapons.Count}");
        Console.WriteLine($"(41 weapon types × 10 tiers = 410+ unique weapons)");
        Console.WriteLine();

        // Show weapons by tier
        var tiers = System.Enum.GetValues<EquipmentTier>();
        foreach (var tier in tiers)
        {
            var tierWeapons = ItemCatalog.GetWeaponsByTier(tier);
            Console.WriteLine($"--- {tier} Tier ({tierWeapons.Count} weapons) ---");
            Console.WriteLine($"Level Requirement: {((int)tier - 1) * 10 + 1}+");
            Console.WriteLine($"Rarity: {ItemCatalog.GetRarityForTier(tier)}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates weapons for specific classes
    /// </summary>
    public void DemonstrateClassSpecificWeapons(ActorClass actorClass)
    {
        Console.WriteLine($"=== {actorClass.ToString().ToUpper()} WEAPONS ===");
        Console.WriteLine();

        // Find the weapon type for this class
        var weaponType = GetWeaponTypeForClass(actorClass);
        var weapons = ItemCatalog.GetWeaponsByType(weaponType);

        Console.WriteLine($"Weapon Type: {weaponType}");
        Console.WriteLine($"Total Variants: {weapons.Count}");
        Console.WriteLine();

        foreach (var item in weapons)
        {
            if (item is Weapon weapon)
            {
                Console.WriteLine($"• {weapon.Name} (Level {weapon.RequiredLevel})");
                Console.WriteLine($"  Damage: {weapon.MinDamage:F1}-{weapon.MaxDamage:F1} | Speed: {weapon.Speed:F2}");
                Console.WriteLine($"  Value: {weapon.BaseValue:N0}g | Weight: {weapon.Weight:F1} lbs | Rarity: {weapon.Rarity}");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Demonstrates weapon progression for a specific type
    /// </summary>
    public void DemonstrateWeaponProgression()
    {
        Console.WriteLine("=== WEAPON PROGRESSION EXAMPLES ===");
        Console.WriteLine();

        var exampleTypes = new[]
        {
            WeaponType.Sword,
            WeaponType.Bow,
            WeaponType.Staff,
            WeaponType.Katana,
            WeaponType.Grimoire
        };

        foreach (var weaponType in exampleTypes)
        {
            Console.WriteLine($"--- {weaponType} Progression ---");
            var weapons = ItemCatalog.GetWeaponsByType(weaponType);
            
            foreach (var item in weapons)
            {
                if (item is Weapon weapon)
                {
                    Console.WriteLine($"Level {weapon.RequiredLevel,3}: {weapon.Name,-25} " +
                                    $"DMG {weapon.MinDamage,4:F0}-{weapon.MaxDamage,4:F0} " +
                                    $"Speed {weapon.Speed:F2} " +
                                    $"({weapon.Rarity})");
                }
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates comparing weapons across tiers
    /// </summary>
    public void DemonstrateWeaponComparison()
    {
        Console.WriteLine("=== WEAPON TIER COMPARISON ===");
        Console.WriteLine();

        var weaponType = WeaponType.Sword;
        Console.WriteLine($"Comparing all {weaponType} tiers:");
        Console.WriteLine();

        var weapons = ItemCatalog.GetWeaponsByType(weaponType);
        
        Console.WriteLine($"{"Tier",-15} {"Level",-8} {"Damage Range",-20} {"Value",-15} {"Rarity",-12}");
        Console.WriteLine(new string('-', 75));

        foreach (var item in weapons)
        {
            if (item is Weapon weapon)
            {
                var damageRange = $"{weapon.MinDamage:F0}-{weapon.MaxDamage:F0}";
                Console.WriteLine($"{weapon.Tier,-15} {weapon.RequiredLevel,-8} {damageRange,-20} {weapon.BaseValue + "g",-15} {weapon.Rarity,-12}");
            }
        }
    }

    private WeaponType GetWeaponTypeForClass(ActorClass actorClass)
    {
        return actorClass switch
        {
            ActorClass.Adventurer => WeaponType.Toolkit,
            ActorClass.Warrior => WeaponType.Sword,
            ActorClass.Cleric => WeaponType.Mace,
            ActorClass.Paladin => WeaponType.HolySword,
            ActorClass.DarkKnight => WeaponType.Greatsword,
            ActorClass.Gunbreaker => WeaponType.Gunblade,
            ActorClass.Barbarian => WeaponType.Greataxe,
            ActorClass.Monk => WeaponType.Handwraps,
            ActorClass.Samurai => WeaponType.Katana,
            ActorClass.Dragoon => WeaponType.Spear,
            ActorClass.Ninja => WeaponType.Kunai,
            ActorClass.Reaper => WeaponType.Scythe,
            ActorClass.Rogue => WeaponType.Dagger,
            ActorClass.Druid => WeaponType.Scimitar,
            ActorClass.Ranger => WeaponType.Bow,
            ActorClass.Hunter => WeaponType.Crossbow,
            ActorClass.Machinist => WeaponType.Firearm,
            ActorClass.Dancer => WeaponType.Chakrams,
            ActorClass.Bard => WeaponType.Lute,
            ActorClass.Wizard => WeaponType.Wand,
            ActorClass.Sorcerer => WeaponType.Orb,
            ActorClass.Warlock => WeaponType.PactTome,
            ActorClass.BlackMage => WeaponType.Rod,
            ActorClass.WhiteMage => WeaponType.Staff,
            ActorClass.RedMage => WeaponType.Rapier,
            ActorClass.BlueMage => WeaponType.Cane,
            ActorClass.Summoner => WeaponType.Grimoire,
            ActorClass.Scholar => WeaponType.Codex,
            ActorClass.Astrologian => WeaponType.Astrolabe,
            ActorClass.Sage => WeaponType.Nouliths,
            ActorClass.Necromancer => WeaponType.Focus,
            ActorClass.Artificer => WeaponType.MultiTool,
            ActorClass.Carpenter => WeaponType.Saw,
            ActorClass.Blacksmith => WeaponType.Hammer,
            ActorClass.Armorer => WeaponType.RaisingHammer,
            ActorClass.Goldsmith => WeaponType.ChasingHammer,
            ActorClass.Leatherworker => WeaponType.HeadKnife,
            ActorClass.Weaver => WeaponType.Needle,
            _ => WeaponType.Sword
        };
    }

    private void InitializeItemDatabase()
    {
        // Add all consumables to the loot generator's database
        var allConsumables = ItemCatalog.GetAllConsumables();
        foreach (var item in allConsumables)
        {
            _lootGenerator.AddItemToDatabase(item.Name, item);
        }

        // Add all materials
        var allMaterials = ItemCatalog.GetAllMaterials();
        foreach (var item in allMaterials)
        {
            _lootGenerator.AddItemToDatabase(item.Name, item);
        }

        // Add all key items
        var allKeyItems = ItemCatalog.GetAllKeyItems();
        foreach (var item in allKeyItems)
        {
            _lootGenerator.AddItemToDatabase(item.Name, item);
        }

        // Add all weapons
        var allWeapons = ItemCatalog.GetAllWeapons();
        foreach (var item in allWeapons)
        {
            _lootGenerator.AddItemToDatabase(item.Name, item);
        }
    }

    private void SetupLootConfigurations()
    {
        // Create enhanced loot configurations
        var bossConfig = new LootConfiguration
        {
            Id = "enhanced_boss",
            Name = "Enhanced Boss Loot",
            MinItems = 3,
            MaxItems = 6,
            MinGold = 100,
            MaxGold = 500,
            MinExperience = 200,
            MaxExperience = 800,
            ScaleWithLevel = true,
            LevelScalingFactor = 1.8f
        };
        
        // Higher chance for rare items
        bossConfig.RarityWeights[RarityTier.Common] = 20f;
        bossConfig.RarityWeights[RarityTier.Uncommon] = 30f;
        bossConfig.RarityWeights[RarityTier.Rare] = 30f;
        bossConfig.RarityWeights[RarityTier.Epic] = 15f;
        bossConfig.RarityWeights[RarityTier.Legendary] = 4f;
        bossConfig.RarityWeights[RarityTier.Mythic] = 1f;
        
        _lootGenerator.AddConfiguration("enhanced_boss", bossConfig);
    }

    private VictoryScreenData CreateSampleVictoryData()
    {
        var victoryData = new VictoryScreenData
        {
            CombatDuration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(45)),
            EnemiesDefeated = 4
        };
        
        // Add sample enemy drops
        victoryData.AddEnemyLoot("Orc Warrior", CreateSampleLootDrop("Orc Warrior", 50, 150, 2));
        victoryData.AddEnemyLoot("Goblin Shaman", CreateSampleLootDrop("Goblin Shaman", 35, 120, 1));
        victoryData.AddEnemyLoot("Elite Troll", CreateSampleLootDrop("Elite Troll", 200, 400, 3));
        victoryData.AddEnemyLoot("Dire Wolf", CreateSampleLootDrop("Dire Wolf", 25, 80, 1));
        
        // Add performance stats
        victoryData.AddPerformanceStat("Damage Dealt", 2450);
        victoryData.AddPerformanceStat("Damage Taken", 780);
        victoryData.AddPerformanceStat("Critical Hits", 12);
        victoryData.AddPerformanceStat("Abilities Used", 28);
        victoryData.AddPerformanceStat("Healing Done", 320);
        
        return victoryData;
    }

    private LootDrop CreateSampleLootDrop(string source, decimal gold, int experience, int itemCount)
    {
        var loot = new LootDrop { Source = source };
        loot.AddGold(gold);
        loot.AddExperience(experience);
        
        // Add random items
        var sampleItems = CreateSampleItems().Take(itemCount);
        foreach (var item in sampleItems)
        {
            var quantity = item.Stackable ? _random.NextInt(1, 5) : 1;
            loot.AddItem(item, quantity);
        }
        
        return loot;
    }

    private void DisplayLootDrop(string chestName, LootDrop? loot)
    {
        if (loot == null)
        {
            Console.WriteLine($"{chestName}: Empty or locked");
            return;
        }
        
        Console.WriteLine($"{chestName} contents:");
        Console.WriteLine($"  Gold: {loot.GoldGained:N0}");
        Console.WriteLine($"  Experience: {loot.ExperienceGained:N0}");
        
        if (loot.Items.Any())
        {
            Console.WriteLine("  Items:");
            foreach (var stack in loot.Items)
            {
                Console.WriteLine($"    • {stack}");
            }
        }
        Console.WriteLine();
    }

    private List<SimpleItem> CreateSampleItems()
    {
        // Return items from catalog instead of creating new ones
        var items = new List<SimpleItem>();
        
        // Get a selection of items from the catalog
        var consumables = ItemCatalog.GetAllConsumables().Take(6).OfType<SimpleItem>();
        items.AddRange(consumables);
        
        return items;
    }

    private LootDrop CreateEnhancedLootDrop(string source, int level, string[] possibleItems)
    {
        var loot = new LootDrop { Source = source };
        
        // Scale gold and experience with level
        var goldAmount = (decimal)(_random.NextInt(10, 50) * level);
        var experience = _random.NextInt(20, 100) * level;
        
        loot.AddGold(goldAmount);
        loot.AddExperience(experience);
        
        // Add random items from possible drops
        var itemCount = _random.NextInt(1, Math.Min(possibleItems.Length + 1, 5));
        var selectedItems = possibleItems.OrderBy(_ => _random.NextDouble()).Take(itemCount);
        
        foreach (var itemName in selectedItems)
        {
            var item = _lootGenerator.GetItemFromDatabase(itemName);
            if (item != null)
            {
                var quantity = item.Stackable ? _random.NextInt(1, Math.Min(6, item.MaxStackSize / 10 + 1)) : 1;
                loot.AddItem(item, quantity);
            }
        }
        
        return loot;
    }

    private SimpleItem CreateSampleItem(string name, ItemType type, bool stackable, int maxStack, decimal value = 10m, float weight = 1f)
    {
        return new SimpleItem(name, type, RarityTier.Common, stackable, maxStack, value, weight, $"A sample {type} item.");
    }

    /// <summary>
    /// Runs all demonstrations in sequence with pauses between each
    /// </summary>
    public void RunAllDemonstrations()
    {
        DemonstrateTierLevelSystem();
        Console.WriteLine();
        
        Console.WriteLine("Press any key to view consumable items...");
        Console.ReadKey();
        Console.Clear();
        
        DemonstrateConsumableItems();
        
        Console.WriteLine("Press any key to view key items...");
        Console.ReadKey();
        Console.Clear();
        
        DemonstrateKeyItems();
        
        Console.WriteLine("Press any key to view crafting materials...");
        Console.ReadKey();
        Console.Clear();
        
        DemonstrateCraftingMaterials();
        
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
        Console.Clear();
        
        DemonstrateItemStacking();
        Console.WriteLine();
        
        DemonstrateSampleInventory();
        Console.WriteLine();
        
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
        Console.Clear();
        
        DemonstrateEnhancedLootDrops();
        Console.WriteLine();
        
        DemonstrateChestSystem();
        Console.WriteLine();
        
        DemonstrateVictoryScreen();
        Console.WriteLine();
        
        Console.WriteLine("Press any key to generate full equipment sets (this will be lengthy)...");
        Console.ReadKey();
        Console.Clear();
        
        DemonstrateClassEquipmentGeneration();
    }
}
