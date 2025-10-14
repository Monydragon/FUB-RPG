using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Game;
using Fub.Implementations.Map;
using Fub.Implementations.Parties;
using Fub.Implementations.Player;
using Fub.Implementations.Progression;
using Fub.Implementations.Random;
using Fub.Interfaces.Actors;

namespace Fub.Examples;

/// <summary>
/// Example showing how to integrate the living world system into your actual game
/// </summary>
public class GameIntegrationExample
{
    private readonly LivingWorldManager _worldManager;
    private readonly GameMap _map;
    private readonly Party _party;
    private readonly RandomSource _random;
    private readonly ExperienceCalculator _xpCalculator;
    private float _timeSinceLastUpdate;

    public GameIntegrationExample()
    {
        _random = new RandomSource(Environment.TickCount);
        _xpCalculator = new ExperienceCalculator();
        
        // Create map
        _map = new GameMap("Forest of Trials", MapTheme.Forest, 50, 50);
        GenerateMap();

        // Create party
        var profile = new PlayerProfile("Hero");
        var player = new PlayerActor("Hero", Species.Human, ActorClass.Warrior, profile, 25, 25);
        _party = new Party(player);

        // Initialize living world
        _worldManager = new LivingWorldManager(_map, _party, _random, Difficulty.Normal);
        SetupWorld();
    }

    private void SetupWorld()
    {
        Console.WriteLine("Setting up living world...");
        
        // Configure spawning
        _worldManager.ConfigureSpawning(maxEnemies: 12, respawnTime: 45f);

        // Spawn roaming enemies (they'll move around the map automatically)
        _worldManager.PopulateMapWithEnemies((x, y) => 
        {
            var enemyTypes = new[]
            {
                ("Forest Wolf", Species.Beast, ActorClass.Ranger),
                ("Goblin Scout", Species.Goblin, ActorClass.Rogue),
                ("Wild Boar", Species.Beast, ActorClass.Warrior),
                ("Bandit", Species.Human, ActorClass.Rogue)
            };
            
            var type = enemyTypes[_random.NextInt(0, enemyTypes.Length)];
            var isElite = _random.NextDouble() < 0.1; // 10% chance of elite
            
            return new MonsterActor(type.Item1, type.Item2, type.Item3, x, y, isElite);
        }, baseCount: 8);

        // Spawn NPCs with different behaviors
        SpawnTownNpcs();
        
        Console.WriteLine($"✓ Spawned {_worldManager.GetEnemiesOnMap().Count} enemies");
        Console.WriteLine($"✓ Spawned {_worldManager.GetNpcsOnMap().Count} NPCs");
        Console.WriteLine("World is alive! Entities will now move around the map.");
    }

    private void SpawnTownNpcs()
    {
        // Stationary merchant
        var merchant = new NpcActor("Traveling Merchant", Species.Human, ActorClass.Adventurer, 10, 10, 
            "Welcome! Check out my wares!");
        merchant.IsMerchant = true;
        _worldManager.SpawnNpc(merchant, MovementBehavior.Stationary);

        // Roaming wanderer
        var wanderer = new NpcActor("Lost Traveler", Species.Elf, ActorClass.Ranger, 30, 30,
            "Have you seen the way out of this forest?");
        _worldManager.SpawnNpc(wanderer, MovementBehavior.Roaming, roamRadius: 10);

        // Patrolling guard
        var guard = new NpcActor("Forest Ranger", Species.Human, ActorClass.Ranger, 15, 15,
            "I patrol these woods to keep them safe.");
        var waypoints = new List<(int x, int y)>
        {
            (15, 15), (35, 15), (35, 35), (15, 35)
        };
        _worldManager.SpawnPatrollingNpc(guard, waypoints);
    }

    /// <summary>
    /// Main game loop update - call this every frame
    /// </summary>
    public void Update(float deltaTime)
    {
        _timeSinceLastUpdate += deltaTime;

        // Update living world (enemies/NPCs move, chase player, etc.)
        _worldManager.Update(deltaTime);

        // Check for encounters every second
        if (_timeSinceLastUpdate >= 1.0f)
        {
            CheckForRandomEncounter();
            _timeSinceLastUpdate = 0;
        }

        // Handle player input
        HandlePlayerInput();
    }

    private void CheckForRandomEncounter()
    {
        // 5% chance per second of random encounter
        if (_random.NextDouble() < 0.05)
        {
            Console.WriteLine("\n⚔️ RANDOM ENCOUNTER!");
            StartCombat();
        }
    }

    private void StartCombat()
    {
        var player = _party.Leader;
        Console.WriteLine($"\n═══════════════════════════════════════");
        Console.WriteLine($"  COMBAT ENCOUNTER");
        Console.WriteLine($"═══════════════════════════════════════");
        Console.WriteLine($"Party: {_party.Members.Count} member(s), Avg Level: {GetPartyAverageLevel()}");
        Console.WriteLine();

        // Spawn 1-4 enemies based on party size
        var enemies = _worldManager.SpawnCombatEnemies((x, y) => 
        {
            var enemyTypes = new[]
            {
                ("Forest Wolf", Species.Beast, ActorClass.Ranger),
                ("Goblin Warrior", Species.Goblin, ActorClass.Warrior),
                ("Wild Boar", Species.Beast, ActorClass.Warrior),
                ("Bandit Thief", Species.Human, ActorClass.Rogue)
            };
            
            var type = enemyTypes[_random.NextInt(0, enemyTypes.Length)];
            return new MonsterActor(type.Item1, type.Item2, type.Item3, 0, 0);
        });

        Console.WriteLine($"Enemies ({enemies.Count}):");
        long totalXpReward = 0;
        
        foreach (var enemy in enemies)
        {
            var xpReward = _xpCalculator.CalculateEnemyExperience(enemy.Level, player.Level);
            totalXpReward += xpReward;
            
            Console.WriteLine($"  • {enemy.Name} (Level {enemy.Level}) - {enemy.GetStat(StatType.Health).Current:F0} HP - {xpReward} XP");
        }

        Console.WriteLine();
        Console.WriteLine($"Total XP if victorious: {totalXpReward}");
        
        // Simulate victory
        SimulateCombatVictory(enemies, totalXpReward);
    }

    private void SimulateCombatVictory(List<IMonster> enemies, long totalXp)
    {
        Console.WriteLine("\n🎉 VICTORY!");
        Console.WriteLine($"Gained {totalXp} experience!");
        
        var player = _party.Leader;
        var oldLevel = player.Level;
        var currentXp = player.Experience;
        var xpToNext = player.JobSystem.GetJobLevel(player.Class).ExperienceToNextLevel;

        // Add experience
        _xpCalculator.AddExperience(player, totalXp, ExperienceSourceType.Combat);

        var newLevel = player.Level;
        if (newLevel > oldLevel)
        {
            Console.WriteLine($"\n🌟 LEVEL UP! {oldLevel} → {newLevel}");
            Console.WriteLine($"New stats:");
            Console.WriteLine($"  HP: {player.GetStat(StatType.Health).Current:F0}");
            Console.WriteLine($"  Attack: {player.GetStat(StatType.AttackPower).Current:F0}");
        }
        else
        {
            var newXp = player.Experience;
            Console.WriteLine($"XP Progress: {newXp}/{xpToNext} ({(double)newXp / xpToNext:P0})");
            
            // Show how many more enemies needed
            var xpNeeded = xpToNext - newXp;
            var enemiesNeeded = _xpCalculator.GetEnemiesNeededForLevel(player.Level, player.Level, LevelCurveType.Custom);
            Console.WriteLine($"Approximately {enemiesNeeded} more enemies to next level");
        }

        Console.WriteLine($"\n═══════════════════════════════════════\n");
    }

    private void HandlePlayerInput()
    {
        // This would be your actual input handling
        // For now, just show player info periodically
    }

    private void GenerateMap()
    {
        // Create a forest with clearings
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                // Walls around edges
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

    private int GetPartyAverageLevel()
    {
        if (_party.Members.Count == 0) return 1;
        var total = 0;
        foreach (var member in _party.Members)
            total += member.Level;
        return total / _party.Members.Count;
    }

    /// <summary>
    /// Shows the new leveling curve in action
    /// </summary>
    public static void DemonstrateLevelingCurve()
    {
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("  NEW LEVELING CURVE");
        Console.WriteLine("═══════════════════════════════════════\n");

        var xpCalc = new ExperienceCalculator();

        Console.WriteLine("Level | Total XP | XP for Next | Enemies Needed (same level)");
        Console.WriteLine("------|----------|-------------|----------------------------");

        for (int level = 1; level <= 10; level++)
        {
            var totalXp = xpCalc.GetExperienceForLevel(level, LevelCurveType.Custom);
            var nextXp = xpCalc.GetExperienceForLevel(level + 1, LevelCurveType.Custom);
            var xpForNext = nextXp - totalXp;
            var enemiesNeeded = xpCalc.GetEnemiesNeededForLevel(level, level, LevelCurveType.Custom);

            Console.WriteLine($"  {level,2}  | {totalXp,8} | {xpForNext,11} | {enemiesNeeded,26}");
        }

        Console.WriteLine("\nFormula: Level 2 = 100 XP, Level 3 = 175 XP, +75 XP each level thereafter");
        Console.WriteLine("Enemy XP: 25 per enemy level (modified by level difference)");
        Console.WriteLine("\n═══════════════════════════════════════\n");
    }

    /// <summary>
    /// Shows enemy count distribution
    /// </summary>
    public static void DemonstrateEnemyCounts()
    {
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("  ENEMY COUNT DISTRIBUTION");
        Console.WriteLine("═══════════════════════════════════════\n");

        var random = new System.Random();
        var trials = 1000;

        for (int partySize = 1; partySize <= 4; partySize++)
        {
            var counts = new int[5]; // Index = enemy count
            
            for (int i = 0; i < trials; i++)
            {
                var enemyCount = EnemyScaler.GetEnemyCount(partySize, random);
                counts[enemyCount]++;
            }

            Console.WriteLine($"Party Size {partySize}:");
            for (int i = 1; i <= 4; i++)
            {
                if (counts[i] > 0)
                {
                    var percentage = (counts[i] / (double)trials) * 100;
                    Console.WriteLine($"  {i} enemies: {percentage:F1}% ({counts[i]}/{trials})");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine("═══════════════════════════════════════\n");
    }

    /// <summary>
    /// Run complete integration demo
    /// </summary>
    public void RunDemo()
    {
        Console.Clear();
        Console.WriteLine("╔═══════════════════════════════════════╗");
        Console.WriteLine("║   LIVING WORLD INTEGRATION DEMO       ║");
        Console.WriteLine("╚═══════════════════════════════════════╝\n");

        DemonstrateLevelingCurve();
        DemonstrateEnemyCounts();

        Console.WriteLine("Starting game simulation...\n");

        // Simulate 30 seconds of gameplay
        for (int i = 0; i < 60; i++)
        {
            Update(0.5f);
            
            if (i % 10 == 0)
            {
                Console.WriteLine($"\n[{i * 0.5f}s] Game Update:");
                ShowWorldStatus();
            }
        }

        Console.WriteLine("\n✓ Demo complete!");
        Console.WriteLine("\nKey Features Demonstrated:");
        Console.WriteLine("  ✓ Enemies and NPCs moving around the map");
        Console.WriteLine("  ✓ 1-4 enemies per encounter based on party size");
        Console.WriteLine("  ✓ New leveling curve (Level 2 = 100 XP, Level 3 = 175 XP, etc.)");
        Console.WriteLine("  ✓ Experience calculation with level difference modifiers");
        Console.WriteLine("  ✓ Automatic enemy aggro and chase behavior");
        Console.WriteLine("  ✓ Random encounters while exploring");
    }

    private void ShowWorldStatus()
    {
        var player = _party.Leader;
        Console.WriteLine($"  Player: Level {player.Level}, XP: {player.Experience}/{player.JobSystem.GetJobLevel(player.Class).ExperienceToNextLevel}");
        Console.WriteLine($"  Position: ({player.X}, {player.Y})");
        Console.WriteLine($"  Enemies on map: {_worldManager.GetEnemiesOnMap().Count}");
        Console.WriteLine($"  NPCs on map: {_worldManager.GetNpcsOnMap().Count}");
        
        // Show nearby enemies
        var nearbyEnemies = _worldManager.GetEnemiesOnMap()
            .FindAll(e => Math.Abs(e.X - player.X) <= 5 && Math.Abs(e.Y - player.Y) <= 5);
        
        if (nearbyEnemies.Count > 0)
        {
            Console.WriteLine($"  ⚠️  {nearbyEnemies.Count} enemies nearby!");
        }
    }
}

