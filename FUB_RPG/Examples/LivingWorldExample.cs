using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Game;
using Fub.Implementations.Items;
using Fub.Implementations.Loot;
using Fub.Implementations.Map;
using Fub.Implementations.Parties;
using Fub.Implementations.Player;
using Fub.Implementations.Random;

namespace Fub.Examples;

/// <summary>
/// Demonstrates the living world system with NPC/enemy movement, pathfinding, and dynamic spawning
/// </summary>
public class LivingWorldExample
{
    private readonly LivingWorldManager _worldManager;
    private readonly GameMap _map;
    private readonly Party _party;
    private readonly RandomSource _random;

    public LivingWorldExample()
    {
        _random = new RandomSource(12345);
        
        // Create a simple map
        _map = new GameMap("Test Dungeon", MapTheme.Cave, 40, 40);
        GenerateSimpleMap();

        // Create a party with player profile
        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 20, 20);
        var companionProfile = new PlayerProfile("Companion");
        var companion = new PlayerActor("Companion", Species.Elf, ActorClass.Cleric, companionProfile, 20, 20);
        _party = new Party(player);
        _party.TryAdd(companion);

        // Initialize living world manager
        _worldManager = new LivingWorldManager(_map, _party, _random, Difficulty.Normal);
    }

    /// <summary>
    /// Demonstrates basic NPC and enemy spawning with movement behaviors
    /// </summary>
    public void DemonstrateBasicMovement()
    {
        Console.WriteLine("=== LIVING WORLD: BASIC MOVEMENT ===");
        Console.WriteLine();

        // Spawn roaming enemies
        Console.WriteLine("Spawning 10 enemies with various behaviors...");
        _worldManager.PopulateMapWithEnemies((x, y) => CreateRandomEnemy(x, y));

        // Spawn roaming NPCs
        Console.WriteLine("Spawning roaming NPCs...");
        var merchantNpc = new NpcActor("Traveling Merchant", Species.Human, ActorClass.Adventurer, 15, 15, 
            "Looking to trade? I've got the finest goods!");
        merchantNpc.IsMerchant = true;
        _worldManager.SpawnNpc(merchantNpc, MovementBehavior.Roaming, 5);

        var wandererNpc = new NpcActor("Wandering Bard", Species.Elf, ActorClass.Bard, 25, 25,
            "Care to hear a tale?");
        _worldManager.SpawnNpc(wandererNpc, MovementBehavior.Roaming, 8);

        // Spawn stationary NPC
        var guardNpc = new NpcActor("Town Guard", Species.Human, ActorClass.Paladin, 20, 10,
            "Halt! State your business.");
        _worldManager.SpawnNpc(guardNpc, MovementBehavior.Stationary);

        Console.WriteLine($"✓ Spawned {_worldManager.GetEnemiesOnMap().Count} enemies");
        Console.WriteLine($"✓ Spawned {_worldManager.GetNpcsOnMap().Count} NPCs");
        Console.WriteLine();

        // Simulate game updates
        Console.WriteLine("Simulating 10 seconds of gameplay...");
        for (int i = 0; i < 20; i++) // 20 ticks × 0.5s = 10 seconds
        {
            _worldManager.Update(0.5f);
            
            if (i % 5 == 0)
            {
                Console.WriteLine($"  [{i * 0.5f}s] Entities moving around the map...");
                ShowNearbyEntities();
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates patrol behavior with waypoints
    /// </summary>
    public void DemonstratePatrolBehavior()
    {
        Console.WriteLine("=== PATROL SYSTEM ===");
        Console.WriteLine();

        var waypoints = new List<(int x, int y)>
        {
            (10, 10),
            (30, 10),
            (30, 30),
            (10, 30)
        };

        var patrolGuard = new NpcActor("Patrol Guard", Species.Human, ActorClass.Warrior, 10, 10,
            "I'm on patrol duty.");
        
        _worldManager.SpawnPatrollingNpc(patrolGuard, waypoints);
        
        Console.WriteLine($"Guard '{patrolGuard.Name}' patrolling between 4 waypoints:");
        foreach (var wp in waypoints)
        {
            Console.WriteLine($"  - ({wp.x}, {wp.y})");
        }
        Console.WriteLine();

        Console.WriteLine("Watching patrol for 15 seconds...");
        for (int i = 0; i < 30; i++)
        {
            _worldManager.Update(0.5f);
            
            if (i % 5 == 0)
            {
                Console.WriteLine($"  [{i * 0.5f}s] Guard position: ({patrolGuard.X}, {patrolGuard.Y})");
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates dynamic level scaling based on party size and difficulty
    /// </summary>
    public void DemonstrateLevelScaling()
    {
        Console.WriteLine("=== DYNAMIC LEVEL SCALING ===");
        Console.WriteLine();

        var partyLevel = (_party.Members[0].Level + _party.Members[1].Level) / 2;
        Console.WriteLine($"Party composition: {_party.Members.Count} members, average level {partyLevel}");
        Console.WriteLine();

        // Test different difficulties
        var difficulties = new[] { Difficulty.Story, Difficulty.Normal, Difficulty.Hard, Difficulty.Ultra, Difficulty.Nightmare };
        
        foreach (var difficulty in difficulties)
        {
            var scaledLevel = EnemyScaler.GetScaledEnemyLevel(partyLevel, _party.Members.Count, difficulty);
            var enemyCount = EnemyScaler.GetEnemyCount(_party.Members.Count, new System.Random());
            
            Console.WriteLine($"{difficulty} Difficulty:");
            Console.WriteLine($"  Recommended enemy level: {scaledLevel}");
            Console.WriteLine($"  Enemy count in combat: {enemyCount}");
            
            // Show scaling example
            var testEnemy = CreateRandomEnemy(0, 0);
            var originalHealth = testEnemy.GetStat(StatType.Health).Current;
            EnemyScaler.ScaleEnemy(testEnemy, scaledLevel, difficulty);
            var scaledHealth = testEnemy.GetStat(StatType.Health).Current;
            
            Console.WriteLine($"  Enemy health: {originalHealth:F0} → {scaledHealth:F0} ({scaledHealth/originalHealth:P0} increase)");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates combat spawning with appropriate enemy counts
    /// </summary>
    public void DemonstrateCombatSpawning()
    {
        Console.WriteLine("=== COMBAT ENCOUNTER SPAWNING ===");
        Console.WriteLine();

        Console.WriteLine($"Party size: {_party.Members.Count}");
        Console.WriteLine("When entering combat, enemies are spawned dynamically:");
        Console.WriteLine();

        for (int i = 0; i < 5; i++)
        {
            var enemies = _worldManager.SpawnCombatEnemies((x, y) => CreateRandomEnemy(x, y));
            
            Console.WriteLine($"Encounter #{i + 1}:");
            Console.WriteLine($"  Enemy count: {enemies.Count}");
            foreach (var enemy in enemies)
            {
                Console.WriteLine($"    - {enemy.Name} (Level {enemy.Level}, HP: {enemy.GetStat(StatType.Health).Current:F0})");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates overlapping entities (items, chests, enemies on same cell)
    /// </summary>
    public void DemonstrateEntityOverlapping()
    {
        Console.WriteLine("=== ENTITY OVERLAPPING SYSTEM ===");
        Console.WriteLine();
        Console.WriteLine("Entities can now coexist on the same cell:");
        Console.WriteLine("  ✓ Items can be on cells with enemies/NPCs/chests");
        Console.WriteLine("  ✓ Chests can be on cells with items/enemies");
        Console.WriteLine("  ✓ Enemies and NPCs block each other but not items/chests");
        Console.WriteLine();

        // Spawn an enemy
        var enemy = CreateRandomEnemy(20, 20);
        var enemyObj = new MapObject(MapObjectKind.Enemy, 20, 20, null, enemy);
        _map.AddObject(enemyObj);

        // Spawn an item on the same cell
        var potion = new SimpleItem("Health Potion", ItemType.Consumable, RarityTier.Common, 
            true, 50, 25m, 0.5f, "Restores health");
        _worldManager.SpawnItem(potion, 20, 20);

        // Try to spawn a chest on the same cell
        var chestConfig = new ChestConfiguration
        { 
            Name = "Wooden Chest",
            MinGold = 10,
            MaxGold = 50
        };
        var chest = new LootChest(chestConfig, 20, 20);
        _worldManager.SpawnChest(chest);

        Console.WriteLine($"Cell (20, 20) contents:");
        var (enemies, npcs, items, chests) = _worldManager.GetEntitiesAt(20, 20);
        Console.WriteLine($"  Enemies: {enemies.Count} ({string.Join(", ", enemies.Select(e => e.Name))})");
        Console.WriteLine($"  NPCs: {npcs.Count}");
        Console.WriteLine($"  Items: {items.Count} ({string.Join(", ", items.Select(i => i.Name))})");
        Console.WriteLine($"  Chests: {chests.Count} ({string.Join(", ", chests.Select(c => c.Name))})");
        Console.WriteLine();
        Console.WriteLine("✓ All entities successfully coexist on the same cell!");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates pathfinding system
    /// </summary>
    public void DemonstratePathfinding()
    {
        Console.WriteLine("=== PATHFINDING SYSTEM ===");
        Console.WriteLine();

        var startX = 5;
        var startY = 5;
        var goalX = 35;
        var goalY = 35;

        Console.WriteLine($"Finding path from ({startX}, {startY}) to ({goalX}, {goalY})...");
        var path = _worldManager.FindPath(startX, startY, goalX, goalY);

        if (path != null)
        {
            Console.WriteLine($"✓ Path found! {path.Count} steps:");
            Console.WriteLine($"  Start: ({path[0].x}, {path[0].y})");
            
            if (path.Count > 10)
            {
                Console.WriteLine($"  ... {path.Count - 2} intermediate steps ...");
            }
            else
            {
                for (int i = 1; i < path.Count - 1; i++)
                {
                    Console.WriteLine($"  Step {i}: ({path[i].x}, {path[i].y})");
                }
            }
            
            Console.WriteLine($"  Goal: ({path[^1].x}, {path[^1].y})");
        }
        else
        {
            Console.WriteLine("✗ No path found!");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates the complete living world system
    /// </summary>
    public void DemonstrateFullSystem()
    {
        Console.WriteLine("=== COMPLETE LIVING WORLD DEMONSTRATION ===");
        Console.WriteLine();

        // Configure spawning
        _worldManager.ConfigureSpawning(maxEnemies: 15, respawnTime: 30f);

        // Populate the world
        Console.WriteLine("Populating world with entities...");
        _worldManager.PopulateMapWithEnemies((x, y) => CreateRandomEnemy(x, y), 8);
        
        // Add various NPCs
        SpawnVariousNpcs();

        // Add items scattered around
        SpawnRandomItems(5);

        // Add chests
        SpawnRandomChests(3);

        Console.WriteLine($"World populated:");
        Console.WriteLine($"  Enemies: {_worldManager.GetEnemiesOnMap().Count}");
        Console.WriteLine($"  NPCs: {_worldManager.GetNpcsOnMap().Count}");
        Console.WriteLine($"  Total map objects: {_map.Objects.Count}");
        Console.WriteLine();

        // Run simulation
        Console.WriteLine("Running 30-second simulation of living world...");
        for (int i = 0; i < 60; i++) // 60 ticks × 0.5s = 30 seconds
        {
            _worldManager.Update(0.5f);
            
            if (i % 10 == 0)
            {
                Console.WriteLine($"\n[{i * 0.5f}s elapsed]");
                ShowNearbyEntities();
                ShowSystemStats();
            }
        }

        Console.WriteLine("\nSimulation complete!");
    }

    /// <summary>
    /// Runs all demonstrations
    /// </summary>
    public void RunAllDemonstrations()
    {
        DemonstrateBasicMovement();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();

        DemonstratePatrolBehavior();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();

        DemonstrateLevelScaling();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();

        DemonstrateCombatSpawning();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();

        DemonstrateEntityOverlapping();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();

        DemonstratePathfinding();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();

        DemonstrateFullSystem();
    }

    // Helper methods
    private void GenerateSimpleMap()
    {
        // Create a simple open area with some walls
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                // Walls around the edges
                if (x == 0 || x == _map.Width - 1 || y == 0 || y == _map.Height - 1)
                {
                    _map.SetTile(x, y, MapTileType.Wall);
                }
                else
                {
                    _map.SetTile(x, y, MapTileType.Floor);
                }
            }
        }
    }

    private MonsterActor CreateRandomEnemy(int x, int y)
    {
        var enemyTypes = new[]
        {
            ("Goblin", Species.Goblin, ActorClass.Rogue),
            ("Orc Warrior", Species.Orc, ActorClass.Warrior),
            ("Dark Mage", Species.Demon, ActorClass.BlackMage),
            ("Wolf", Species.Beast, ActorClass.Ranger)
        };

        var type = enemyTypes[_random.NextInt(0, enemyTypes.Length)];
        var isElite = _random.NextDouble() < 0.15; // 15% chance of elite

        return new MonsterActor(type.Item1, type.Item2, type.Item3, x, y, isElite);
    }

    private void SpawnVariousNpcs()
    {
        var npcs = new[]
        {
            (new NpcActor("Healer", Species.Human, ActorClass.Cleric, 12, 12, "Need healing?"), 
                MovementBehavior.Stationary, 0),
            (new NpcActor("Adventurer", Species.Elf, ActorClass.Ranger, 28, 15, "The roads are dangerous!"), 
                MovementBehavior.Roaming, 6),
            (new NpcActor("Sage", Species.Human, ActorClass.Wizard, 15, 28, "Seek knowledge..."), 
                MovementBehavior.Guard, 0)
        };

        foreach (var (npc, behavior, radius) in npcs)
        {
            _worldManager.SpawnNpc(npc, behavior, radius);
        }
    }

    private void SpawnRandomItems(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var x = _random.NextInt(5, _map.Width - 5);
            var y = _random.NextInt(5, _map.Height - 5);
            
            var item = new SimpleItem($"Treasure {i + 1}", ItemType.Material, 
                RarityTier.Uncommon, true, 10, 50m, 1f, "A valuable find!");
            
            _worldManager.SpawnItem(item, x, y);
        }
    }

    private void SpawnRandomChests(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var x = _random.NextInt(5, _map.Width - 5);
            var y = _random.NextInt(5, _map.Height - 5);
            
            var chestConfig = new ChestConfiguration
            {
                Name = $"Treasure Chest {i + 1}",
                MinGold = 50,
                MaxGold = 200,
                MinItems = 1,
                MaxItems = 3
            };
            var chest = new LootChest(chestConfig, x, y);
            
            _worldManager.SpawnChest(chest);
        }
    }

    private void ShowNearbyEntities()
    {
        var player = _party.Leader;
        var radius = 5;
        
        Console.WriteLine($"  Entities near player at ({player.X}, {player.Y}):");
        
        var nearbyEnemies = _worldManager.GetEnemiesOnMap()
            .Where(e => Math.Abs(e.X - player.X) <= radius && Math.Abs(e.Y - player.Y) <= radius)
            .ToList();
        
        var nearbyNpcs = _worldManager.GetNpcsOnMap()
            .Where(n => Math.Abs(n.X - player.X) <= radius && Math.Abs(n.Y - player.Y) <= radius)
            .ToList();

        if (nearbyEnemies.Any())
            Console.WriteLine($"    Enemies: {string.Join(", ", nearbyEnemies.Select(e => $"{e.Name} ({e.X},{e.Y})"))}");
        
        if (nearbyNpcs.Any())
            Console.WriteLine($"    NPCs: {string.Join(", ", nearbyNpcs.Select(n => $"{n.Name} ({n.X},{n.Y})"))}");
        
        if (!nearbyEnemies.Any() && !nearbyNpcs.Any())
            Console.WriteLine("    (none in range)");
    }

    private void ShowSystemStats()
    {
        Console.WriteLine($"  System stats:");
        Console.WriteLine($"    Total enemies: {_worldManager.GetEnemiesOnMap().Count}");
        Console.WriteLine($"    Total NPCs: {_worldManager.GetNpcsOnMap().Count}");
        Console.WriteLine($"    Total objects: {_map.Objects.Count}");
    }
}
