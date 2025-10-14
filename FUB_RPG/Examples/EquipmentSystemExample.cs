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
/// Demonstrates the complete tier-based equipment and loot system
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
    /// Demonstrates generating complete equipment sets for all classes across all tiers (1-100)
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

    private void InitializeItemDatabase()
    {
        // Add sample items to the loot generator's database
        var sampleItems = CreateSampleItems();
        foreach (var item in sampleItems)
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
        return new List<SimpleItem>
        {
            CreateSampleItem("Health Potion", ItemType.Consumable, true, 50, 10m, 0.2f),
            CreateSampleItem("Mana Potion", ItemType.Consumable, true, 50, 12m, 0.2f),
            CreateSampleItem("Iron Ore", ItemType.Material, true, 99, 5m, 1.0f),
            CreateSampleItem("Magic Crystal", ItemType.Material, true, 20, 25m, 0.1f),
            CreateSampleItem("Ancient Scroll", ItemType.Consumable, false, 1, 100m, 0.1f),
            CreateSampleItem("Gold Coin", ItemType.Currency, true, 1000, 1m, 0.01f)
        };
    }

    private SimpleItem CreateSampleItem(string name, ItemType type, bool stackable, int maxStack, decimal value = 10m, float weight = 1f)
    {
        return new SimpleItem(name, type, RarityTier.Common, stackable, maxStack, value, weight);
    }

    public void RunAllDemonstrations()
    {
        DemonstrateTierLevelSystem();
        Console.WriteLine();
        
        DemonstrateItemStacking();
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
