using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Combat;
using Fub.Implementations.Items;
using Fub.Implementations.Map;
using Fub.Implementations.Parties;
using Fub.Implementations.Player;
using Fub.Implementations.Progression;
using Fub.Implementations.Random;
using Fub.Interfaces.Actors;

namespace Fub.Examples;

/// <summary>
/// Demonstrates the new XP bar system and animated victory screen
/// </summary>
public class VictoryScreenExample
{
    private readonly ExperienceCalculator _xpCalculator;
    private readonly VictoryScreen _victoryScreen;
    private readonly RandomSource _random;

    public VictoryScreenExample()
    {
        _xpCalculator = new ExperienceCalculator();
        _victoryScreen = new VictoryScreen(_xpCalculator);
        _random = new RandomSource(Environment.TickCount);
    }

    /// <summary>
    /// Demonstrates the animated victory screen with all features
    /// </summary>
    public void DemonstrateVictoryScreen()
    {
        Console.Clear();
        Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║         VICTORY SCREEN DEMONSTRATION                  ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This demo shows:");
        Console.WriteLine("  ✓ Animated XP bar that fills up");
        Console.WriteLine("  ✓ XP bar resets each level");
        Console.WriteLine("  ✓ Level up animations");
        Console.WriteLine("  ✓ Gold and item drops display");
        Console.WriteLine("  ✓ Color-coded item rarities");
        Console.WriteLine();
        Console.WriteLine("Press any key to start...");
        Console.ReadKey();

        // Scenario 1: Small XP gain (no level up)
        Console.Clear();
        Console.WriteLine("═══ SCENARIO 1: Small Battle ═══\n");
        DemoSmallBattle();
        Console.ReadKey();

        // Scenario 2: Level up once
        Console.Clear();
        Console.WriteLine("═══ SCENARIO 2: Level Up! ═══\n");
        DemoSingleLevelUp();
        Console.ReadKey();

        // Scenario 3: Multiple level ups
        Console.Clear();
        Console.WriteLine("═══ SCENARIO 3: Big Victory! ═══\n");
        DemoMultipleLevelUps();
        Console.ReadKey();

        // Scenario 4: Boss victory with rare loot
        Console.Clear();
        Console.WriteLine("═══ SCENARIO 4: Boss Victory! ═══\n");
        DemoBossVictory();
        Console.ReadKey();

        Console.Clear();
        Console.WriteLine("✓ Demo complete!");
    }

    private void DemoSmallBattle()
    {
        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 10, 10);

        var rewards = new VictoryRewards
        {
            ExperienceGained = 50,
            GoldGained = 25,
            ItemsDropped = new List<Interfaces.Items.IItem>
            {
                new SimpleItem("Health Potion", ItemType.Consumable, RarityTier.Common, true, 50, 10m, 0.5f, "Restores health")
            },
            OldLevel = player.Level,
            LevelsGained = 0,
            NewLevel = player.Level
        };

        // Add XP
        var oldLevel = player.Level;
        _xpCalculator.AddExperience(player, rewards.ExperienceGained, ExperienceSourceType.Combat);
        rewards.NewLevel = player.Level;
        rewards.LevelsGained = rewards.NewLevel - oldLevel;

        _victoryScreen.DisplayVictory(player, rewards, animationSpeedMs: 30);
    }

    private void DemoSingleLevelUp()
    {
        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 10, 10);
        
        // Set player close to leveling
        var currentJobLevel = player.JobSystem.GetJobLevel(player.Class);
        var xpForNextLevel = _xpCalculator.GetExperienceForLevel(player.Level + 1, LevelCurveType.Custom);
        var currentXp = xpForNextLevel - 30; // 30 XP away from level up
        
        // Manually set XP (in real game, this would happen through combat)
        player.JobSystem.AddExperience(player.Class, currentXp);

        var rewards = new VictoryRewards
        {
            ExperienceGained = 80, // Will cause level up
            GoldGained = 50,
            ItemsDropped = new List<Interfaces.Items.IItem>
            {
                new SimpleItem("Leather Boots", ItemType.Equipment, RarityTier.Uncommon, true, 100, 40m, 2f, "Light armor"),
                new SimpleItem("Iron Sword", ItemType.Equipment, RarityTier.Uncommon, true, 150, 80m, 5f, "Basic weapon")
            },
            OldLevel = player.Level
        };

        var oldLevel = player.Level;
        _xpCalculator.AddExperience(player, rewards.ExperienceGained, ExperienceSourceType.Combat);
        rewards.NewLevel = player.Level;
        rewards.LevelsGained = rewards.NewLevel - oldLevel;

        _victoryScreen.DisplayVictory(player, rewards, animationSpeedMs: 40);
    }

    private void DemoMultipleLevelUps()
    {
        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 10, 10);

        var rewards = new VictoryRewards
        {
            ExperienceGained = 400, // Will cause multiple level ups
            GoldGained = 150,
            ItemsDropped = new List<Interfaces.Items.IItem>
            {
                new SimpleItem("Magic Scroll", ItemType.Consumable, RarityTier.Rare, true, 200, 100m, 0.3f, "Powerful spell"),
                new SimpleItem("Gold Ring", ItemType.Equipment, RarityTier.Rare, true, 300, 250m, 0.5f, "Enchanted jewelry"),
                new SimpleItem("Health Potion x3", ItemType.Consumable, RarityTier.Common, true, 150, 30m, 1.5f, "Restores health")
            },
            OldLevel = player.Level
        };

        var oldLevel = player.Level;
        _xpCalculator.AddExperience(player, rewards.ExperienceGained, ExperienceSourceType.Combat);
        rewards.NewLevel = player.Level;
        rewards.LevelsGained = rewards.NewLevel - oldLevel;

        _victoryScreen.DisplayVictory(player, rewards, animationSpeedMs: 35);
    }

    private void DemoBossVictory()
    {
        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 10, 10);

        var rewards = new VictoryRewards
        {
            ExperienceGained = 500,
            GoldGained = 500,
            ItemsDropped = new List<Interfaces.Items.IItem>
            {
                new SimpleItem("Dragon Scale", ItemType.Material, RarityTier.Legendary, true, 1000, 500m, 1f, "Legendary crafting material"),
                new SimpleItem("Epic Sword", ItemType.Equipment, RarityTier.Epic, true, 800, 400m, 8f, "Powerful weapon"),
                new SimpleItem("Boss Key", ItemType.KeyItem, RarityTier.Rare, false, 0, 0m, 0.1f, "Opens the treasure room"),
                new SimpleItem("Rare Gem", ItemType.Material, RarityTier.Rare, true, 300, 200m, 0.5f, "Valuable gem")
            },
            OldLevel = player.Level
        };

        var oldLevel = player.Level;
        _xpCalculator.AddExperience(player, rewards.ExperienceGained, ExperienceSourceType.Combat);
        rewards.NewLevel = player.Level;
        rewards.LevelsGained = rewards.NewLevel - oldLevel;

        _victoryScreen.DisplayVictory(player, rewards, animationSpeedMs: 30);
    }

    /// <summary>
    /// Shows how the XP bar works at different levels
    /// </summary>
    public static void DemonstrateXPBarMechanics()
    {
        Console.Clear();
        Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║         XP BAR MECHANICS DEMONSTRATION                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var xpCalc = new ExperienceCalculator();

        Console.WriteLine("How the XP bar works:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        // Show XP requirements for levels 1-5
        for (int level = 1; level <= 5; level++)
        {
            var xpForThisLevel = xpCalc.GetExperienceForLevel(level, LevelCurveType.Custom);
            var xpForNextLevel = xpCalc.GetExperienceForLevel(level + 1, LevelCurveType.Custom);
            var xpNeeded = xpForNextLevel - xpForThisLevel;

            Console.WriteLine($"Level {level}:");
            Console.WriteLine($"  Total XP at this level: {xpForThisLevel}");
            Console.WriteLine($"  XP needed to reach Level {level + 1}: {xpNeeded}");
            Console.WriteLine($"  Total XP to reach Level {level + 1}: {xpForNextLevel}");
            
            // Show bar at 0%, 50%, 100%
            Console.WriteLine("  Progress examples:");
            
            var bar0 = new ExperienceBar(level, xpForThisLevel, xpForThisLevel, xpForNextLevel);
            Console.WriteLine($"    0%:   {bar0.GetBarString(30)} {bar0.GetProgressText()}");
            
            var bar50 = new ExperienceBar(level, xpForThisLevel + (xpNeeded / 2), xpForThisLevel, xpForNextLevel);
            Console.WriteLine($"    50%:  {bar50.GetBarString(30)} {bar50.GetProgressText()}");
            
            var bar100 = new ExperienceBar(level, xpForNextLevel, xpForThisLevel, xpForNextLevel);
            Console.WriteLine($"    100%: {bar100.GetBarString(30)} {bar100.GetProgressText()}");
            
            Console.WriteLine();
        }

        Console.WriteLine("KEY POINTS:");
        Console.WriteLine("  ✓ Bar shows progress WITHIN current level (resets each level)");
        Console.WriteLine("  ✓ Each level requires +75 XP more than the previous");
        Console.WriteLine("  ✓ Bar fills from 0% to 100%, then resets at next level");
        Console.WriteLine("  ✓ Level 1→2: Need 100 XP total");
        Console.WriteLine("  ✓ Level 2→3: Need 75 MORE XP (175 total)");
        Console.WriteLine("  ✓ Level 3→4: Need 75 MORE XP (250 total)");
        Console.WriteLine();
    }

    /// <summary>
    /// Interactive demo - add XP and watch the bar fill
    /// </summary>
    public void InteractiveXPBarDemo()
    {
        Console.Clear();
        Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║         INTERACTIVE XP BAR DEMO                       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Watch the XP bar fill up in real-time!");
        Console.WriteLine("Press any key to gain XP...");
        Console.WriteLine();

        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 10, 10);

        while (true)
        {
            var jobLevel = player.JobSystem.GetJobLevel(player.Class);
            var xpForCurrentLevel = _xpCalculator.GetExperienceForLevel(jobLevel.Level, LevelCurveType.Custom);
            var xpForNextLevel = _xpCalculator.GetExperienceForLevel(jobLevel.Level + 1, LevelCurveType.Custom);

            var bar = new ExperienceBar(jobLevel.Level, jobLevel.Experience, xpForCurrentLevel, xpForNextLevel);

            Console.SetCursorPosition(0, 5);
            Console.WriteLine($"Level: {bar.CurrentLevel}                    ");
            Console.WriteLine($"{bar.GetBarString(50)}");
            Console.WriteLine($"{bar.GetProgressText()}                    ");
            Console.WriteLine();
            Console.WriteLine("Press SPACE to add XP, ESC to exit");

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape) break;
            if (key.Key == ConsoleKey.Spacebar)
            {
                var oldLevel = player.Level;
                _xpCalculator.AddExperience(player, 25, ExperienceSourceType.Combat);
                
                if (player.Level > oldLevel)
                {
                    Console.SetCursorPosition(0, 9);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⭐ LEVEL UP! ⭐                    ");
                    Console.ResetColor();
                    Thread.Sleep(500);
                }
            }
        }
    }

    /// <summary>
    /// Run all demonstrations
    /// </summary>
    public void RunAllDemos()
    {
        DemonstrateXPBarMechanics();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();

        DemonstrateVictoryScreen();
        
        Console.Clear();
        Console.WriteLine("Would you like to try the interactive XP bar demo? (Y/N)");
        var response = Console.ReadKey();
        if (response.Key == ConsoleKey.Y)
        {
            InteractiveXPBarDemo();
        }

        Console.Clear();
        Console.WriteLine("✓ All demos complete!");
        Console.WriteLine("\nKey Features Implemented:");
        Console.WriteLine("  ✓ XP bar shows current level progress only");
        Console.WriteLine("  ✓ Bar resets to 0% each time you level up");
        Console.WriteLine("  ✓ Smooth animation as XP fills the bar");
        Console.WriteLine("  ✓ Victory screen shows gold, items, and XP");
        Console.WriteLine("  ✓ Color-coded items by rarity");
        Console.WriteLine("  ✓ Level up animations with celebration");
        Console.WriteLine("  ✓ Clean, polished RPG-style presentation");
    }
}

