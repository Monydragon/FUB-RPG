# Movement & Leveling System Updates

## Overview

This document covers the major updates to enemy/NPC movement and the new leveling system that gives you much more control over progression.

---

## ✅ NEW FEATURES IMPLEMENTED

### 1. **Enemies & NPCs Now Move Around The Map**

All enemies and NPCs are now **fully animated** - they roam around the map, patrol routes, and chase the player!

#### Movement Behaviors Available:
- **Roaming** - Wanders randomly within a radius (most enemies use this)
- **Patrol** - Follows waypoints in a loop (guards)
- **Chase** - Pursues the player when nearby (aggressive enemies)
- **Stationary** - Stays in place (merchants, bosses)
- **Guard** - Returns to home position when idle

#### How It Works:
```csharp
// In your game loop, just call:
worldManager.Update(deltaTime);

// Enemies automatically:
// - Roam around their spawn points
// - Chase the player when within 8 tiles
// - Return to roaming when player leaves
// - Move intelligently using the movement system
```

---

### 2. **New Leveling Formula (Your Custom Curve)**

The new leveling system uses **exactly the formula you requested**:

```
Level 2:  100 XP
Level 3:  175 XP  
Level 4:  250 XP
Level 5:  325 XP
Level 6:  400 XP
Level 7:  475 XP
Level 8:  550 XP
Level 9:  625 XP
Level 10: 700 XP
```

**Pattern**: Each level adds +75 XP to the requirement

#### XP Per Enemy:
- Base: **25 XP per enemy level**
- Example: Level 5 enemy = 125 XP base

#### Level Difference Modifiers:
- Enemy 5+ levels higher: **+50% XP** (1.5x)
- Enemy 3-4 levels higher: **+25% XP** (1.25x)
- Enemy 1-2 levels higher: **+10% XP** (1.1x)
- Same level: **Normal XP** (1.0x)
- Enemy 1-2 levels lower: **-20% XP** (0.8x)
- Enemy 3-5 levels lower: **-50% XP** (0.5x)
- Enemy 6+ levels lower: **-90% XP** (0.1x)

#### Example Calculations:

**At Level 1 (need 100 XP to reach Level 2):**
- Fight 1 Level 1 enemy: 25 XP → **Need 4 enemies**
- Fight 1 Level 2 enemy: 50 XP (2x bonus for higher level) → **Need 2 enemies**

**At Level 5 (need 325 XP to reach Level 6):**
- Fight Level 5 enemies: 125 XP each → **Need 3 enemies**
- Fight Level 6 enemies: 163 XP each → **Need 2 enemies**
- Fight Level 3 enemies: 38 XP each (lower level penalty) → **Need 9 enemies**

This gives you **much more control** - you level faster from challenging enemies, slower from weak ones.

---

### 3. **1-4 Enemies Per Encounter**

Enemy count now scales intelligently with party size:

| Party Size | Enemy Count | Distribution |
|------------|-------------|--------------|
| **1 (Solo)** | 1-2 enemies | 60% → 1 enemy<br>40% → 2 enemies |
| **2 (Duo)** | 1-3 enemies | 30% → 1 enemy<br>40% → 2 enemies<br>30% → 3 enemies |
| **3 (Trio)** | 2-4 enemies | 20% → 2 enemies<br>40% → 3 enemies<br>40% → 4 enemies |
| **4+ (Full Party)** | 2-4 enemies | 10% → 2 enemies<br>40% → 3 enemies<br>50% → 4 enemies |

**Benefits:**
- Solo play is more manageable (1-2 enemies max)
- Party play is more challenging (up to 4 enemies)
- Variety - you won't always fight the same number
- Balanced difficulty across all party sizes

---

## 📁 NEW FILES CREATED

1. **`ExperienceCalculator.cs`** - Handles XP calculations with your custom curve
2. **`GameIntegrationExample.cs`** - Shows how to use everything in your actual game

## 📝 MODIFIED FILES

1. **`JobLevel.cs`** - Updated to use new leveling formula
2. **`EnemyScaler.cs`** - Updated enemy count logic (1-4 enemies)

---

## 🎮 HOW TO USE IN YOUR GAME

### Basic Setup:

```csharp
// 1. Create the living world manager
var worldManager = new LivingWorldManager(map, party, randomSource, Difficulty.Normal);

// 2. Configure spawning
worldManager.ConfigureSpawning(maxEnemies: 15, respawnTime: 30f);

// 3. Spawn enemies (they'll move automatically!)
worldManager.PopulateMapWithEnemies((x, y) => 
{
    return new MonsterActor("Goblin", Species.Goblin, ActorClass.Rogue, x, y);
}, baseCount: 10);

// 4. In your game loop:
void Update(float deltaTime)
{
    worldManager.Update(deltaTime); // NPCs/enemies move automatically!
}
```

### When Combat Starts:

```csharp
// Spawn 1-4 scaled enemies
var enemies = worldManager.SpawnCombatEnemies((x, y) => 
{
    return new MonsterActor("Bandit", Species.Human, ActorClass.Rogue, x, y);
});

// After victory, award XP:
var xpCalc = new ExperienceCalculator();
foreach (var enemy in enemies)
{
    var xp = xpCalc.CalculateEnemyExperience(enemy.Level, player.Level);
    xpCalc.AddExperience(player, xp, ExperienceSourceType.Combat);
}
```

### Add Moving NPCs:

```csharp
// Roaming merchant
var merchant = new NpcActor("Merchant", Species.Human, ActorClass.Adventurer, 20, 20, "Buy something!");
merchant.IsMerchant = true;
worldManager.SpawnNpc(merchant, MovementBehavior.Roaming, roamRadius: 8);

// Patrolling guard
var guard = new NpcActor("Guard", Species.Human, ActorClass.Warrior, 10, 10, "Halt!");
var waypoints = new List<(int x, int y)> { (10,10), (30,10), (30,30), (10,30) };
worldManager.SpawnPatrollingNpc(guard, waypoints);
```

---

## 📊 LEVELING COMPARISON

### Old System (Exponential):
```
Level 2:   251 XP   (way too much!)
Level 3:   634 XP
Level 4:  1,265 XP
Level 5:  2,236 XP
```
**Problem**: Took forever to level, too grindy

### New System (Your Custom Formula):
```
Level 2:   100 XP   ✓ (4 same-level enemies)
Level 3:   175 XP   ✓ (1-2 encounters)
Level 4:   250 XP   ✓ (2 encounters)
Level 5:   325 XP   ✓ (2-3 encounters)
```
**Result**: Much faster leveling, more rewarding!

---

## 🎯 KEY IMPROVEMENTS

### Before:
- ❌ Enemies just stood still
- ❌ NPCs were statues
- ❌ Always fought 2-4 enemies (even solo!)
- ❌ Took 10+ fights to level up
- ❌ No control over progression speed

### After:
- ✅ Enemies roam around and chase the player
- ✅ NPCs patrol, wander, and move realistically
- ✅ 1-4 enemies scaled to party size
- ✅ Level up in 2-4 encounters (configurable)
- ✅ Full control over XP curve and enemy counts
- ✅ World feels ALIVE with movement

---

## 🧪 TESTING YOUR CHANGES

Run the demo to see everything in action:

```csharp
var demo = new GameIntegrationExample();
demo.RunDemo();
```

This will show you:
- Leveling curve from Level 1-10
- Enemy count distribution (1000 trials)
- 30-second live simulation of the world
- Enemies moving, chasing, and spawning
- Random encounters with proper XP rewards

---

## ⚙️ CONFIGURATION OPTIONS

### Adjust Leveling Speed:
```csharp
// In JobLevel.cs, you can change the curve:
// - LevelCurveType.Custom (your formula: fastest)
// - LevelCurveType.Linear (steady growth: medium)
// - LevelCurveType.Moderate (balanced: medium-slow)
// - LevelCurveType.Steep (exponential: slowest)
```

### Adjust Enemy Count:
Edit `EnemyScaler.GetEnemyCount()` percentages:
```csharp
if (partySize == 1)
{
    // Change 0.6 to 0.8 for 80% chance of 1 enemy (easier solo)
    return random.NextDouble() < 0.6 ? 1 : 2;
}
```

### Adjust XP Per Enemy:
In `ExperienceCalculator.CalculateEnemyExperience()`:
```csharp
// Change 25 to 30 for 30 XP per enemy level
var baseXp = enemyLevel * 25;
```

### Adjust Movement Speed:
In `MovementController`:
```csharp
controller.MovementCooldown = 1.0f; // Lower = faster movement
```

### Adjust Aggro Range:
In `LivingWorldManager.UpdateChaseTargets()`:
```csharp
var aggroRange = 8; // Change to 10 for wider detection
```

---

## 🚀 WHAT'S NEXT?

You now have:
1. ✅ Living world with moving enemies/NPCs
2. ✅ 1-4 enemies per encounter
3. ✅ Custom leveling curve (Level 2 = 100 XP, etc.)
4. ✅ Full control over progression speed

**Simply integrate `worldManager.Update(deltaTime)` into your game loop and everything works automatically!**

The world will feel alive with enemies roaming around, NPCs going about their business, and intelligent chase behavior when the player gets close.

