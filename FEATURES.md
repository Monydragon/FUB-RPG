# FubWithAgents - RPG System Features

## Overview
This document describes the major systems implemented for a full-featured console RPG with Final Fantasy XIV-inspired mechanics.

---

## 🎮 Major Features Implemented

### 1. **Turn-Based Combat System**
- **Location**: `Implementations/Combat/TurnBasedCombatResolver.cs`
- **Features**:
  - Full turn-based combat with action selection for each party member
  - Action types: Attack, Defend, Pass
  - Priority-based turn order (Defend actions go first, then by Agility stat)
  - Defensive stance reduces incoming damage by 50%
  - Critical hit system (10% chance for 1.5x damage)
  - Weapon damage integration
  - Visual combat feedback with health bars and damage numbers
  - Experience rewards after victory

### 2. **FFXIV-Style Job System**
- **Location**: `Interfaces/Progression/IJobSystem.cs`, `Implementations/Progression/JobSystem.cs`
- **Features**:
  - Each actor tracks levels for ALL job classes independently
  - **Weapon-based class switching**: Equipping a weapon automatically changes your active class
    - Sword → Warrior
    - Axe → Barbarian
    - Bow → Ranger
    - Dagger → Rogue
    - Staff/Wand → Mage
    - Mace → Cleric
    - Spear → Adventurer
    - Shield → Paladin
    - Unarmed → Necromancer
  - Experience is gained for the currently equipped job
  - Independent leveling: Each job has its own level (1-100) and experience curve
  - Exponential experience curve: `100 * level^2.5`

### 3. **Multi-Map World System**
- **Location**: `Interfaces/Game/IWorld.cs`, `Implementations/Game/World.cs`
- **Features**:
  - World contains multiple interconnected maps
  - Three starting maps generated:
    - **Town** (30x20): Safe starting area with few enemies
    - **Forest** (40x30): Medium-difficulty exploration area
    - **Dungeon** (50x40): High-difficulty endgame content
  - Map connections with named exits (e.g., "North Exit", "Cave Entrance")
  - Each map has unique enemy counts and difficulty scaling

### 4. **Enhanced Exploration System**
- **Features**:
  - Context-aware action menu shows only valid actions
  - Dynamic encounters:
    - Pick up items from the ground
    - Talk to NPCs
    - Engage enemies in combat
    - Search rooms for secrets
  - World map view showing all discovered locations
  - Movement in 4 directions (North, South, East, West)

### 5. **Party Management System**
- **Features**:
  - View all party member stats and equipment
  - Change party leader at any time
  - Equipment management interface showing all equipped items
  - Job levels display for each party member
  - Individual inventory system per character

### 6. **Equipment & Inventory System**
- **Features**:
  - Pick up and equip weapons dynamically
  - Starting weapons given to all party members:
    - Iron Sword (Warrior class)
    - Wooden Bow (Ranger class)
    - Oak Staff (Mage class)
    - Bronze Dagger (Rogue class)
  - Equipment requirements:
    - Level requirements
    - Stat requirements
    - Class restrictions (can equip if base OR current class matches)
  - Visual feedback when equipping shows class change

---

## 🎯 Class System Details

### Base Classes
Each actor chooses a base class at character creation, but can switch to any class by equipping the appropriate weapon:

1. **Adventurer** - Jack-of-all-trades (Spear)
2. **Warrior** - Melee fighter (Sword)
3. **Mage** - Spellcaster (Staff/Wand)
4. **Rogue** - Sneaky attacker (Dagger)
5. **Ranger** - Ranged attacker (Bow)
6. **Cleric** - Healer/Support (Mace)
7. **Barbarian** - Heavy melee (Axe)
8. **Paladin** - Tank/Defender (Shield)
9. **Necromancer** - Dark magic (Unarmed/special)

### Job Progression
- Each job levels independently from 1 to 100
- Experience requirements increase exponentially
- Your displayed level is always for your **currently equipped weapon's job**
- Switching weapons mid-game changes your effective class instantly

---

## 🗺️ World Structure

### Maps Generated
1. **Starting Town** (30x20)
   - 2 enemies, 1 NPC, 3 items
   - Safe area for learning mechanics
   
2. **Dark Forest** (40x30)
   - 6 enemies, 2 NPCs, 5 items
   - Medium difficulty exploration
   
3. **Ancient Dungeon** (50x40)
   - 10 enemies, 1 NPC, 8 items
   - High difficulty, best rewards

### Map Connections
- Town → Forest (North Exit)
- Forest → Town (South Exit)
- Forest → Dungeon (Cave Entrance)
- Dungeon → Forest (Exit)

---

## ⚔️ Combat System Details

### Turn Flow
1. **Action Selection**: Each alive party member chooses an action
2. **Enemy AI**: Enemies choose actions (80% attack, 20% defend)
3. **Turn Resolution**: Actions execute in priority order, then by Agility
4. **Outcome Check**: Victory if all enemies defeated, Defeat if party wiped

### Damage Calculation
```
Base Damage = Strength × 2.0
Weapon Damage = Random(MinDamage, MaxDamage)
Total Damage = Base + Weapon - (Defender Vitality × 0.5)
Final Damage = Max(1, Total Damage)

If Defending: Final Damage × 0.5
If Critical: Final Damage × 1.5
```

### Experience Rewards
```
Experience per Enemy = 50 × (Enemy Level)^1.5
```
All surviving party members gain experience for their current job.

---

## 🎨 User Interface Features

### Menus Available
- **Main Menu**: New Game, Quit
- **Exploration Menu**: Move, Pick Up Items, Talk, Fight, Search, Inventory, Party, World Map
- **Party Menu**: View Stats, Change Leader, Manage Equipment
- **Inventory Menu**: Examine items, Equip weapons, View details
- **Combat Menu**: Attack, Defend, Pass (per character)

### Visual Elements
- Colored text for different actor types (green = allies, red = enemies)
- Health bars in combat
- Combat log with damage numbers
- Critical hit indicators (💥)
- Defeat markers (💀)
- Victory/Defeat screens with experience summary

---

## 📊 Stats System

### Primary Stats
- **Health**: Hit points, 0 = defeated
- **Mana**: Magic points (future use)
- **Stamina**: Endurance (future use)
- **Strength**: Increases physical damage
- **Agility**: Determines turn order
- **Intellect**: Magic damage (future use)
- **Vitality**: Reduces incoming damage

---

## 🔮 Future Enhancement Opportunities

1. **Abilities System**: Special moves per job class
2. **Magic System**: Spells that consume mana
3. **Item System**: Potions, consumables, equipment beyond weapons
4. **Quest System**: NPCs can give quests with rewards
5. **Save/Load System**: Persist game progress
6. **Advanced AI**: Enemy tactics and behaviors
7. **Status Effects**: Buffs, debuffs, DOTs
8. **Crafting System**: Create weapons and items
9. **Skill Trees**: Unlock abilities per job
10. **Boss Encounters**: Unique challenging enemies

---

## 🚀 How to Play

1. **Start Game**: Create your party (1-4 characters)
2. **Choose Base Class**: Each character picks a starting class
3. **Receive Starting Weapons**: Each member gets a weapon
4. **Explore**: Move around the map, discover items and enemies
5. **Combat**: Engage enemies, choose actions each turn
6. **Level Jobs**: Gain experience in your currently equipped job
7. **Switch Classes**: Equip different weapons to change jobs instantly
8. **Manage Party**: View equipment, stats, and job levels
9. **Explore World**: Travel between multiple maps
10. **Conquer Dungeons**: Face increasing challenges

---

## 💡 Key Design Decisions

### Why Weapon-Based Classes?
Following FFXIV's armory system where your equipped weapon determines your class. This encourages:
- Experimentation with different playstyles
- Strategic weapon switching
- Collection of multiple weapon types
- Leveling multiple jobs for versatility

### Why Independent Job Levels?
Allows players to:
- Master multiple combat roles
- Switch roles based on party composition
- Feel progression in each distinct playstyle
- Replay value through different job combinations

### Why Turn-Based Combat?
Provides:
- Strategic depth
- Time to make decisions
- Clear action resolution
- Party coordination opportunities
- Accessibility for all skill levels

---

## 📁 File Structure

```
Implementations/
├── Combat/
│   ├── TurnBasedCombatResolver.cs (NEW)
│   ├── CombatAction.cs (NEW)
│   └── CombatSession.cs
├── Game/
│   ├── World.cs (NEW)
│   ├── GameLoop.cs (UPDATED - major features)
│   └── GameState.cs (UPDATED)
├── Progression/
│   ├── JobSystem.cs (NEW)
│   └── JobLevel.cs (NEW)
└── Actors/
    └── ActorBase.cs (UPDATED - job system integration)

Interfaces/
├── Game/
│   └── IWorld.cs (NEW)
├── Progression/
│   ├── IJobSystem.cs (NEW)
│   └── IJobLevel.cs (NEW)
├── Combat/
│   └── ICombatAction.cs (NEW)
└── Actors/
    └── IActor.cs (UPDATED - job system)

Enums/
└── CombatActionType.cs (NEW)
```

---

## ✅ Implementation Status

- ✅ Turn-based combat system
- ✅ FFXIV-style job system with weapon switching
- ✅ Multi-map world exploration
- ✅ Independent job leveling (1-100)
- ✅ Equipment system with requirements
- ✅ Dynamic combat with defend/pass actions
- ✅ Experience rewards and level-ups
- ✅ Party management interface
- ✅ World map navigation
- ✅ Context-aware exploration menus
- ✅ Visual combat feedback
- ✅ Critical hit system
- ✅ Enemy AI behaviors

All core systems are implemented and ready for use!

