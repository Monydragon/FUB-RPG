using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Map;
using Fub.Interfaces.Parties;
using Fub.Interfaces.Random;
using Fub.Implementations.Actors;
using Fub.Implementations.Map;

namespace Fub.Implementations.Game;

/// <summary>
/// Manages living world systems: movement, spawning, AI, and dynamic scaling
/// </summary>
public class LivingWorldManager
{
    private readonly IMap _map;
    private readonly IParty _party;
    private readonly IRandomSource _random;
    private readonly EntitySpawnManager _spawnManager;
    private readonly Pathfinder _pathfinder;
    private readonly Difficulty _difficulty;
    
    private float _accumulatedTime;
    private const float UpdateInterval = 0.5f; // Update AI every 0.5 seconds

    public LivingWorldManager(IMap map, IParty party, IRandomSource random, Difficulty difficulty = Difficulty.Normal)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _difficulty = difficulty;
        
        _spawnManager = new EntitySpawnManager(map, random);
        _pathfinder = new Pathfinder(map.Width, map.Height, IsWalkable);
    }

    /// <summary>
    /// Updates the living world - call this each game frame
    /// </summary>
    public void Update(float deltaTime)
    {
        _accumulatedTime += deltaTime;

        // Ensure controllers exist for any new entities
        EnsureMovementControllersForExistingEntities();

        // Update movement controllers
        _spawnManager.UpdateMovement(deltaTime);

        // Update spawning system
        if (_accumulatedTime >= UpdateInterval)
        {
            var partyAvgLevel = GetPartyAverageLevel();
            var partySize = _party.Members.Count;
            
            _spawnManager.UpdateSpawning(_accumulatedTime, partyAvgLevel, partySize, _difficulty);
            UpdateChaseTargets();
            
            _accumulatedTime = 0;
        }
    }

    /// <summary>
    /// Scan the map and attach movement controllers to any enemies/NPCs missing one.
    /// </summary>
    public void EnsureMovementControllersForExistingEntities()
    {
        var withControllers = _spawnManager.GetMovingActors().Select(t => t.actor.Id).ToHashSet();

        foreach (var obj in _map.Objects)
        {
            if (obj.Actor == null) continue;
            if (withControllers.Contains(obj.Actor.Id)) continue;

            if (obj.Actor is IMonster)
            {
                // Enemies roam by default
                int radius = _random.NextInt(3, 8);
                _spawnManager.CreateMovementController(obj.Actor, MovementBehavior.Roaming, radius);
            }
            else if (obj.Actor is INpc)
            {
                // NPCs roam in towns, otherwise occasionally roam
                var behavior = _map.Kind == MapKind.Town ? MovementBehavior.Roaming : (_random.NextDouble() < 0.5 ? MovementBehavior.Roaming : MovementBehavior.Stationary);
                int radius = _map.Kind == MapKind.Town ? _random.NextInt(3, 7) : _random.NextInt(2, 5);
                if (behavior == MovementBehavior.Roaming)
                    _spawnManager.CreateMovementController(obj.Actor, behavior, radius);
            }
        }
    }

    /// <summary>
    /// Spawns initial enemies on the map based on party composition
    /// </summary>
    public void PopulateMapWithEnemies(Func<int, int, IMonster> enemyFactory, int baseCount = 10)
    {
        var partySize = _party.Members.Count;
        var partyLevel = GetPartyAverageLevel();
        
        // Scale enemy count based on party size
        var enemyCount = baseCount + (partySize - 1) * 3;
        
        _spawnManager.SpawnInitialEnemies(enemyFactory, enemyCount, partyLevel, partySize, _difficulty);
    }

    /// <summary>
    /// Spawns NPCs with various behaviors on the map
    /// </summary>
    public void SpawnNpc(NpcActor npc, MovementBehavior behavior = MovementBehavior.Roaming, int roamRadius = 5)
    {
        var mapObject = new MapObject(MapObjectKind.Npc, npc.X, npc.Y, null, npc);
        _map.AddObject(mapObject);
        
        if (behavior != MovementBehavior.Stationary)
        {
            _spawnManager.CreateMovementController(npc, behavior, roamRadius);
        }
    }

    /// <summary>
    /// Spawns a patrolling NPC with predefined waypoints
    /// </summary>
    public void SpawnPatrollingNpc(NpcActor npc, List<(int x, int y)> waypoints)
    {
        var mapObject = new MapObject(MapObjectKind.Npc, npc.X, npc.Y, null, npc);
        _map.AddObject(mapObject);
        
        var controller = _spawnManager.CreateMovementController(npc, MovementBehavior.Patrol);
        controller.PatrolWaypoints = waypoints;
    }

    /// <summary>
    /// Spawns items that can coexist with enemies, NPCs, and chests
    /// </summary>
    public void SpawnItem(Interfaces.Items.IItem item, int x, int y)
    {
        // Items can be placed anywhere that's walkable
        if (!IsWalkable(x, y))
            return;

        var mapObject = new MapObject(MapObjectKind.Item, x, y, item);
        _map.AddObject(mapObject);
    }

    /// <summary>
    /// Spawns chests that can coexist with other entities
    /// </summary>
    public void SpawnChest(LootChest chest)
    {
        // Chests can be placed on any floor tile
        if (!_map.InBounds(chest.X, chest.Y))
            return;

        var tile = _map.GetTile(chest.X, chest.Y);
        if (tile.TileType != MapTileType.Floor)
            return;

        _map.AddObject(chest);
    }

    /// <summary>
    /// Makes enemies near the player chase them
    /// </summary>
    private void UpdateChaseTargets()
    {
        var leader = _party.Leader;
        var aggroRange = 8; // Enemy aggro radius

        foreach (var (actor, controller) in _spawnManager.GetMovingActors())
        {
            // Only update enemy behaviors
            if (actor is not IMonster)
                continue;

            var distance = Math.Abs(actor.X - leader.X) + Math.Abs(actor.Y - leader.Y);

            // If player is in range, chase them
            if (distance <= aggroRange && controller.Behavior != MovementBehavior.Chase)
            {
                controller.Behavior = MovementBehavior.Chase;
                controller.ChaseTarget = leader;
                controller.MovementCooldown = 0.6f; // Chase faster
            }
            // If player is out of range, return to roaming
            else if (distance > aggroRange * 1.5 && controller.Behavior == MovementBehavior.Chase)
            {
                controller.Behavior = MovementBehavior.Roaming;
                controller.ChaseTarget = null;
                controller.MovementCooldown = (float)(_random.NextDouble() * 1.0 + 1.0);
            }
        }
    }

    /// <summary>
    /// Handles enemy death and respawning
    /// </summary>
    public void OnEnemyDefeated(Guid enemyId)
    {
        _spawnManager.RemoveDeadEnemy(enemyId);
    }

    /// <summary>
    /// Gets a path from one point to another using A* pathfinding
    /// </summary>
    public List<(int x, int y)>? FindPath(int startX, int startY, int goalX, int goalY)
    {
        return _pathfinder.FindPath(startX, startY, goalX, goalY);
    }

    /// <summary>
    /// Checks if a position is walkable (floor and not blocked by actors)
    /// </summary>
    private bool IsWalkable(int x, int y)
    {
        if (!_map.InBounds(x, y))
            return false;

        var tile = _map.GetTile(x, y);
        if (tile.TileType != MapTileType.Floor)
            return false;

        // Check for blocking entities (NPCs and enemies block movement)
        var objects = _map.GetObjectsAt(x, y);
        return !objects.Any(o => o.ObjectKind == MapObjectKind.Enemy || o.ObjectKind == MapObjectKind.Npc);
    }

    /// <summary>
    /// Spawns enemies for combat based on party size (2-4 enemies for party of 2+)
    /// </summary>
    public List<IMonster> SpawnCombatEnemies(Func<int, int, IMonster> enemyFactory)
    {
        var enemies = new List<IMonster>();
        var partySize = _party.Members.Count;
        var partyLevel = GetPartyAverageLevel();
        
        var enemyCount = EnemyScaler.GetEnemyCount(partySize, new System.Random());
        var enemyLevel = EnemyScaler.GetScaledEnemyLevel(partyLevel, partySize, _difficulty);

        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = enemyFactory(0, 0); // Position doesn't matter for combat
            if (enemy != null)
            {
                EnemyScaler.ScaleEnemy(enemy, enemyLevel, _difficulty);
                enemies.Add(enemy);
            }
        }

        return enemies;
    }

    /// <summary>
    /// Gets all enemies currently on the map
    /// </summary>
    public List<IMonster> GetEnemiesOnMap()
    {
        return _map.Objects
            .Where(o => o.ObjectKind == MapObjectKind.Enemy && o.Actor is IMonster)
            .Select(o => o.Actor as IMonster)
            .Where(m => m != null)
            .Cast<IMonster>()
            .ToList();
    }

    /// <summary>
    /// Gets all NPCs currently on the map
    /// </summary>
    public List<INpc> GetNpcsOnMap()
    {
        return _map.Objects
            .Where(o => o.ObjectKind == MapObjectKind.Npc && o.Actor is INpc)
            .Select(o => o.Actor as INpc)
            .Where(n => n != null)
            .Cast<INpc>()
            .ToList();
    }

    /// <summary>
    /// Gets entities at a specific position (can be multiple types)
    /// </summary>
    public (List<IMonster> enemies, List<INpc> npcs, List<Interfaces.Items.IItem> items, List<LootChest> chests) 
        GetEntitiesAt(int x, int y)
    {
        var objects = _map.GetObjectsAt(x, y);
        
        var enemies = objects
            .Where(o => o.ObjectKind == MapObjectKind.Enemy && o.Actor is IMonster)
            .Select(o => o.Actor as IMonster)
            .Cast<IMonster>()
            .ToList();

        var npcs = objects
            .Where(o => o.ObjectKind == MapObjectKind.Npc && o.Actor is INpc)
            .Select(o => o.Actor as INpc)
            .Cast<INpc>()
            .ToList();

        var items = objects
            .Where(o => o.ObjectKind == MapObjectKind.Item && o.Item != null)
            .Select(o => o.Item!)
            .ToList();

        var chests = objects
            .Where(o => o.ObjectKind == MapObjectKind.Container && o is LootChest)
            .Cast<LootChest>()
            .ToList();

        return (enemies, npcs, items, chests);
    }

    private int GetPartyAverageLevel()
    {
        if (_party.Members.Count == 0)
            return 1;

        return (int)Math.Ceiling(_party.Members.Average(m => m.Level));
    }

    /// <summary>
    /// Configures spawn manager settings
    /// </summary>
    public void ConfigureSpawning(int maxEnemies = 20, float respawnTime = 30f)
    {
        _spawnManager.MaxEnemiesOnMap = maxEnemies;
        _spawnManager.EnemyRespawnTime = respawnTime;
    }
}
