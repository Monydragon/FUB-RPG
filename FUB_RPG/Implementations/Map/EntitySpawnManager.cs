using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Map;
using Fub.Interfaces.Random;
using Fub.Implementations.Actors;

namespace Fub.Implementations.Map;

/// <summary>
/// Manages entity spawning and respawning on the map
/// </summary>
public class EntitySpawnManager
{
    private readonly IMap _map;
    private readonly IRandomSource _random;
    private readonly List<SpawnPoint> _spawnPoints = new();
    private readonly Dictionary<Guid, MovementController> _movementControllers = new();
    
    public int MaxEnemiesOnMap { get; set; } = 20;
    public float EnemyRespawnTime { get; set; } = 30f; // seconds
    
    public EntitySpawnManager(IMap map, IRandomSource random)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Adds a spawn point to the map
    /// </summary>
    public void AddSpawnPoint(int x, int y, MapObjectKind spawnType, float respawnTime = 30f, int maxSpawns = -1)
    {
        var spawnPoint = new SpawnPoint
        {
            X = x,
            Y = y,
            SpawnType = spawnType,
            RespawnTime = respawnTime,
            MaxSpawns = maxSpawns,
            SpawnsRemaining = maxSpawns
        };
        
        _spawnPoints.Add(spawnPoint);
    }

    /// <summary>
    /// Registers a movement controller for an actor
    /// </summary>
    public void RegisterMovementController(IActor actor, MovementController controller)
    {
        _movementControllers[actor.Id] = controller;
    }

    /// <summary>
    /// Creates a movement controller for an actor with specified behavior
    /// </summary>
    public MovementController CreateMovementController(IActor actor, MovementBehavior behavior, int roamRadius = 5)
    {
        var controller = new MovementController(actor, behavior)
        {
            RoamRadius = roamRadius,
            MovementCooldown = behavior == MovementBehavior.Roaming ? 
                (float)(_random.NextDouble() * 1.2 + 0.8) : 1.0f
        };
        
        RegisterMovementController(actor, controller);
        return controller;
    }

    /// <summary>
    /// Updates all movement controllers
    /// </summary>
    public void UpdateMovement(float deltaTime)
    {
        foreach (var kvp in _movementControllers.ToList())
        {
            var actor = GetActorById(kvp.Key);
            if (actor == null)
            {
                _movementControllers.Remove(kvp.Key);
                continue;
            }

            var controller = kvp.Value;
            controller.Update(deltaTime, CanMoveToPosition);
        }
    }

    /// <summary>
    /// Updates spawn points and respawns entities
    /// </summary>
    public void UpdateSpawning(float deltaTime, int partyAverageLevel, int partySize, Difficulty difficulty)
    {
        var currentEnemyCount = CountEnemiesOnMap();
        
        foreach (var spawnPoint in _spawnPoints)
        {
            if (spawnPoint.SpawnType != MapObjectKind.Enemy)
                continue;

            if (currentEnemyCount >= MaxEnemiesOnMap)
                break;

            spawnPoint.TimeSinceLastSpawn += deltaTime;
            
            if (!spawnPoint.IsActive && spawnPoint.TimeSinceLastSpawn >= spawnPoint.RespawnTime)
            {
                if (spawnPoint.CanSpawn && IsCellAvailableForSpawn(spawnPoint.X, spawnPoint.Y))
                {
                    SpawnEnemyAtPoint(spawnPoint);
                    currentEnemyCount++;
                }
            }
        }
    }

    /// <summary>
    /// Spawns initial enemies on the map
    /// </summary>
    public void SpawnInitialEnemies(Func<int, int, IMonster> enemyFactory, int count, int partyLevel, int partySize, Difficulty difficulty)
    {
        var spawnedCount = 0;
        var maxAttempts = count * 10;
        var attempts = 0;

        while (spawnedCount < count && attempts < maxAttempts)
        {
            attempts++;
            
            var x = _random.NextInt(0, _map.Width);
            var y = _random.NextInt(0, _map.Height);

            if (!IsCellAvailableForSpawn(x, y))
                continue;

            var enemy = enemyFactory(x, y);
            if (enemy == null)
                continue;

            // Scale enemy to appropriate level
            var targetLevel = EnemyScaler.GetScaledEnemyLevel(partyLevel, partySize, difficulty);
            EnemyScaler.ScaleEnemy(enemy, targetLevel, difficulty);

            // Create map object for enemy
            var mapObject = new MapObject(MapObjectKind.Enemy, x, y, null, enemy);
            _map.AddObject(mapObject);

            // Add movement behavior
            var behavior = _random.NextDouble() > 0.3 ? MovementBehavior.Roaming : MovementBehavior.Stationary;
            CreateMovementController(enemy, behavior, _random.NextInt(3, 8));

            spawnedCount++;
        }
    }

    /// <summary>
    /// Checks if a cell can have an enemy spawned (enemies can overlap with items/chests)
    /// </summary>
    private bool IsCellAvailableForSpawn(int x, int y)
    {
        if (!_map.InBounds(x, y))
            return false;

        var tile = _map.GetTile(x, y);
        if (tile.TileType != MapTileType.Floor)
            return false;

        // Check for NPCs - enemies can't spawn on NPCs
        var objectsAtCell = _map.GetObjectsAt(x, y);
        if (objectsAtCell.Any(o => o.ObjectKind == MapObjectKind.Npc))
            return false;

        // Check for other enemies - don't stack enemies on the same cell
        if (objectsAtCell.Any(o => o.ObjectKind == MapObjectKind.Enemy))
            return false;

        // Items, chests, and decorations are OK to overlap with
        return true;
    }

    /// <summary>
    /// Checks if an entity can move to a position (allows moving through items/chests)
    /// </summary>
    private bool CanMoveToPosition(int x, int y)
    {
        if (!_map.InBounds(x, y))
            return false;

        var tile = _map.GetTile(x, y);
        if (tile.TileType != MapTileType.Floor)
            return false;

        // Can't move through other actors (NPCs or enemies)
        var objectsAtCell = _map.GetObjectsAt(x, y);
        if (objectsAtCell.Any(o => o.ObjectKind == MapObjectKind.Enemy || o.ObjectKind == MapObjectKind.Npc))
            return false;

        return true;
    }

    private void SpawnEnemyAtPoint(SpawnPoint spawnPoint)
    {
        // This would need an enemy factory - placeholder for now
        spawnPoint.IsActive = true;
        spawnPoint.TimeSinceLastSpawn = 0;
        
        if (spawnPoint.MaxSpawns > 0)
            spawnPoint.SpawnsRemaining--;
    }

    private int CountEnemiesOnMap()
    {
        return _map.Objects.Count(o => o.ObjectKind == MapObjectKind.Enemy);
    }

    private IActor? GetActorById(Guid id)
    {
        return _map.Objects
            .Where(o => o.Actor != null && o.Actor.Id == id)
            .Select(o => o.Actor)
            .FirstOrDefault();
    }

    /// <summary>
    /// Removes dead enemies from the map and schedules respawn
    /// </summary>
    public void RemoveDeadEnemy(Guid enemyId)
    {
        _map.RemoveObject(enemyId);
        _movementControllers.Remove(enemyId);
    }

    /// <summary>
    /// Gets all actors with movement controllers
    /// </summary>
    public IEnumerable<(IActor actor, MovementController controller)> GetMovingActors()
    {
        foreach (var kvp in _movementControllers)
        {
            var actor = GetActorById(kvp.Key);
            if (actor != null)
            {
                yield return (actor, kvp.Value);
            }
        }
    }

    private class SpawnPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
        public MapObjectKind SpawnType { get; set; }
        public float RespawnTime { get; set; }
        public float TimeSinceLastSpawn { get; set; }
        public bool IsActive { get; set; }
        public int MaxSpawns { get; set; } = -1; // -1 = infinite
        public int SpawnsRemaining { get; set; }
        public bool CanSpawn => MaxSpawns < 0 || SpawnsRemaining > 0;
    }
}

/// <summary>
/// Simple map object implementation
/// </summary>
public class MapObject : IMapObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public MapObjectKind ObjectKind { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public Interfaces.Items.IItem? Item { get; }
    public IActor? Actor { get; }

    public MapObject(MapObjectKind kind, int x, int y, Interfaces.Items.IItem? item = null, IActor? actor = null)
    {
        ObjectKind = kind;
        X = x;
        Y = y;
        Item = item;
        Actor = actor;
        Name = kind.ToString();
    }
}
