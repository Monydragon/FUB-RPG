# Living World System Guide

## Overview

This guide documents the complete **Living World System** that adds NPC/enemy movement, pathfinding, roaming behaviors, dynamic level scaling, and improved entity spawning to your RPG game.

---

## Features Implemented

### 1. **Movement Behaviors** (`MovementBehavior` enum)
NPCs and enemies can now have different movement patterns:

- **Stationary** - Does not move (guards, shopkeepers)
- **Roaming** - Wanders randomly within a radius
- **Patrol** - Moves between fixed waypoints in order
- **Chase** - Follows a target (player/enemy)
- **Flee** - Runs away from a target
- **Guard** - Returns to a home position when idle
- **Custom** - Script-controlled behavior

### 2. **A* Pathfinding System** (`Pathfinder` class)
Intelligent navigation around obstacles:

```csharp
var pathfinder = new Pathfinder(mapWidth, mapHeight, isWalkableFunc);
var path = pathfinder.FindPath(startX, startY, goalX, goalY);
var nextStep = pathfinder.GetNextStep(startX, startY, goalX, goalY);
```

Features:
- Finds optimal paths using A* algorithm
- Configurable maximum path distance
- Manhattan distance heuristic for grid-based movement
- Returns null if no path exists

### 3. **Movement Controller** (`MovementController` class)
Manages entity movement on the map:

```csharp
var controller = new MovementController(actor, MovementBehavior.Roaming);
controller.RoamRadius = 5;
controller.MovementCooldown = 1.0f;
controller.Update(deltaTime, canMoveToFunc);
```

Features:
- Update-based movement (call every frame)
- Configurable movement speed via cooldown
- Home position tracking for roaming/guarding
- Waypoint system for patrol routes
- Chase target support

### 4. **NPC System** (`NpcActor` class)
Non-player characters with dialogue and functionality:

```csharp
var npc = new NpcActor("Merchant", Species.Human, ActorClass.Adventurer, x, y, 
    "Welcome to my shop!");
npc.IsMerchant = true;
npc.IsQuestGiver = true;
npc.AddQuest("quest_001");
```

Properties:
- `Dialogue` - NPC's greeting/message
- `IsHostile` - Can attack players
- `IsMerchant` - Can trade items
- `IsQuestGiver` - Offers quests
- `QuestIds` - List of associated quests

### 5. **Enemy Scaling System** (`EnemyScaler` class)
Dynamic difficulty scaling based on party composition:

```csharp
// Scale enemy to match party level and difficulty
EnemyScaler.ScaleEnemy(enemy, targetLevel, Difficulty.Hard);

// Get recommended enemy level
int enemyLevel = EnemyScaler.GetScaledEnemyLevel(partyAvgLevel, partySize, difficulty);

// Get enemy count for combat
int count = EnemyScaler.GetEnemyCount(partySize, random);
```

**Scaling Features:**
- **Level Scaling**: +10% stats per level difference
- **Difficulty Multipliers**:
  - Story: 0.75x
  - Normal: 1.0x
  - Hard: 1.35x
  - Ultra: 1.75x
  - Nightmare: 2.5x
- **Elite Enemies**: 1.5x multiplier
- **Boss Enemies**: 2.5x multiplier
- **Party Size Scaling**: More members = higher enemy level
- **Solo Play**: -1 level adjustment (easier)
- **2+ Party**: 2-4 enemies spawn in combat

### 6. **Entity Spawning System** (`EntitySpawnManager` class)
Advanced spawning with respawn mechanics:

```csharp
var spawnManager = new EntitySpawnManager(map, randomSource);
spawnManager.MaxEnemiesOnMap = 20;
spawnManager.EnemyRespawnTime = 30f; // seconds

// Spawn initial enemies
spawnManager.SpawnInitialEnemies(enemyFactory, count, partyLevel, partySize, difficulty);

// Create movement controller for an actor
var controller = spawnManager.CreateMovementController(actor, MovementBehavior.Roaming, roamRadius: 5);

// Update spawning and movement
spawnManager.UpdateMovement(deltaTime);
spawnManager.UpdateSpawning(deltaTime, partyLevel, partySize, difficulty);
```

### 7. **Entity Overlap System**
Multiple entities can coexist on the same cell:

✅ **Items** can be on cells with enemies/NPCs/chests  
✅ **Chests** can be on cells with items/enemies  
✅ **Enemies and NPCs** block each other (cannot stack)

This makes the world feel more natural and allows for complex scenarios.

### 8. **Living World Manager** (`LivingWorldManager` class)
Centralized system that ties everything together:

```csharp
var worldManager = new LivingWorldManager(map, party, randomSource, Difficulty.Normal);

// Spawn enemies based on party composition
worldManager.PopulateMapWithEnemies(enemyFactory, baseCount: 10);

// Spawn NPCs with behaviors
worldManager.SpawnNpc(npc, MovementBehavior.Roaming, roamRadius: 5);
worldManager.SpawnPatrollingNpc(npc, waypointList);

// Spawn items and chests
worldManager.SpawnItem(item, x, y);
worldManager.SpawnChest(chest);

// Update the world (call every frame)
worldManager.Update(deltaTime);

// Pathfinding
var path = worldManager.FindPath(startX, startY, goalX, goalY);

// Combat spawning
var enemies = worldManager.SpawnCombatEnemies(enemyFactory);

// Handle enemy death
worldManager.OnEnemyDefeated(enemyId);
```

---

## Usage Examples

### Example 1: Create a Roaming Enemy

```csharp
// Create enemy
var goblin = new MonsterActor("Goblin Scout", Species.Goblin, ActorClass.Rogue, x, y);

// Add to map
var mapObject = new MapObject(MapObjectKind.Enemy, x, y, null, goblin);
map.AddObject(mapObject);

// Add roaming behavior
var controller = new MovementController(goblin, MovementBehavior.Roaming);
controller.RoamRadius = 7;
controller.MovementCooldown = 1.5f; // Moves every 1.5 seconds

// Update in game loop
controller.Update(deltaTime, (nx, ny) => map.GetTile(nx, ny).TileType == MapTileType.Floor);
```

### Example 2: Create a Patrolling Guard

```csharp
var guard = new NpcActor("City Guard", Species.Human, ActorClass.Warrior, 10, 10, "Halt!");

var waypoints = new List<(int x, int y)>
{
    (10, 10),
    (30, 10),
    (30, 30),
    (10, 30)
};

var controller = new MovementController(guard, MovementBehavior.Patrol);
controller.PatrolWaypoints = waypoints;
```

### Example 3: Enemy Chases Player

```csharp
var wolf = new MonsterActor("Dire Wolf", Species.Beast, ActorClass.Ranger, x, y);
var controller = new MovementController(wolf, MovementBehavior.Chase);
controller.ChaseTarget = player;
controller.MovementCooldown = 0.6f; // Chase faster than roaming
```

### Example 4: Scale Enemy Difficulty

```csharp
// Create base enemy
var dragon = new MonsterActor("Dragon", Species.Dragon, ActorClass.Warrior, x, y, elite: false, boss: true);

// Scale to party level on Hard difficulty
var partyAvgLevel = party.Members.Average(m => m.Level);
EnemyScaler.ScaleEnemy(dragon, (int)partyAvgLevel + 5, Difficulty.Hard);

// Result: Dragon scaled with:
// - Base level scaling
// - Hard difficulty: 1.35x multiplier
// - Boss modifier: 2.5x multiplier
// - Total: Very challenging encounter!
```

### Example 5: Complete Living World Setup

```csharp
// Initialize
var worldManager = new LivingWorldManager(map, party, randomSource, Difficulty.Normal);
worldManager.ConfigureSpawning(maxEnemies: 15, respawnTime: 30f);

// Populate world
worldManager.PopulateMapWithEnemies((x, y) => 
{
    var enemy = new MonsterActor("Orc", Species.Orc, ActorClass.Warrior, x, y);
    return enemy;
}, baseCount: 10);

// Add NPCs
var merchant = new NpcActor("Traveling Merchant", Species.Human, ActorClass.Adventurer, 20, 20, "Wares for sale!");
merchant.IsMerchant = true;
worldManager.SpawnNpc(merchant, MovementBehavior.Roaming, 8);

// Game loop
while (gameRunning)
{
    float deltaTime = CalculateDeltaTime();
    worldManager.Update(deltaTime);
    
    // Enemies automatically:
    // - Roam around their spawn points
    // - Chase the player when nearby
    // - Return to roaming when player leaves
    // - Respawn after being defeated
}
```

---

## Combat Scaling

When party size is 2 or more, the system automatically spawns 2-4 enemies in combat encounters:

```csharp
// Party of 1: 1-2 enemies
// Party of 2+: 2-4 enemies
var combatEnemies = worldManager.SpawnCombatEnemies(enemyFactory);

foreach (var enemy in combatEnemies)
{
    // Each enemy is automatically scaled to party level + difficulty
    Console.WriteLine($"{enemy.Name} - Level {enemy.Level}, HP: {enemy.GetStat(StatType.Health).Current}");
}
```

---

## Integration Points

### Map Requirements
Your map must support:
- `IMap.GetTile(x, y)` - Returns tile with `TileType` property
- `IMap.InBounds(x, y)` - Checks coordinate validity
- `IMap.AddObject(IMapObject)` - Adds entities to map
- `IMap.GetObjectsAt(x, y)` - Gets all entities at position
- `IMap.RemoveObject(Guid)` - Removes entity by ID

### Actor Requirements
Actors must implement:
- `IActor.X, Y` - Position properties
- `IActor.TryMove(dx, dy)` - Movement method
- `IActor.SetMovementValidator(Func<int,int,bool>)` - Set movement rules

### Game Loop Integration
```csharp
public class GameLoop
{
    private LivingWorldManager _worldManager;
    
    public void Update(float deltaTime)
    {
        // Update living world BEFORE player input
        _worldManager.Update(deltaTime);
        
        // Handle player input
        HandleInput();
        
        // Render
        Render();
    }
}
```

---

## Performance Considerations

1. **Movement Updates**: Entities update every 0.5 seconds by default (configurable)
2. **Pathfinding**: Limited to 100 steps by default to prevent long searches
3. **Spawn Cap**: Maximum enemies on map is configurable (default: 20)
4. **Chase Range**: Enemies aggro within 8 tiles (configurable in code)

---

## File Structure

### New Files Created:
- `Enums/MovementBehavior.cs` - Movement behavior types
- `Implementations/Map/Pathfinder.cs` - A* pathfinding
- `Implementations/Actors/MovementController.cs` - Movement logic
- `Implementations/Actors/EnemyScaler.cs` - Dynamic scaling
- `Implementations/Actors/NpcActor.cs` - NPC implementation
- `Implementations/Map/EntitySpawnManager.cs` - Spawn system
- `Implementations/Game/LivingWorldManager.cs` - Main manager
- `Examples/LivingWorldExample.cs` - Usage demonstrations

---

## Troubleshooting

**Q: Enemies are not moving**  
A: Ensure you're calling `worldManager.Update(deltaTime)` every frame

**Q: Pathfinding returns null**  
A: Check that the destination is walkable and there's a valid path

**Q: Too many/few enemies spawning**  
A: Adjust `MaxEnemiesOnMap` in EntitySpawnManager or change base count in PopulateMapWithEnemies

**Q: Enemies are too weak/strong**  
A: Adjust difficulty level or modify scaling multipliers in EnemyScaler

**Q: NPCs walking through walls**  
A: Ensure your `canMoveTo` function checks tile walkability correctly

---

## Future Enhancements

Potential additions to consider:
- **Formation Movement** - Groups moving together
- **Tactical AI** - Flanking, cover, retreat logic
- **Dynamic Difficulty** - Adjusts based on player performance
- **Spawn Zones** - Specific areas for certain enemy types
- **Event-Driven Spawning** - Spawn waves based on triggers
- **Behavior Trees** - More complex AI decision making

---

## Summary

You now have a complete living world system with:
- ✅ NPCs and enemies that move around the map
- ✅ Multiple movement behaviors (roaming, patrol, chase, etc.)
- ✅ A* pathfinding for intelligent navigation
- ✅ Dynamic level scaling based on party size and difficulty
- ✅ Automatic enemy spawning and respawning
- ✅ 2-4 enemies in combat for parties of 2+
- ✅ Entity overlap (items, chests, enemies, NPCs can coexist)
- ✅ Configurable aggro range and chase behavior

The game world now feels alive with entities moving around, making the gameplay more dynamic and engaging!

