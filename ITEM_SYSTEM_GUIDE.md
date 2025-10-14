# Item System Guide

## Overview

The FUB RPG now features a comprehensive item system with over 500+ unique items including consumables, crafting materials, key items, and **410+ weapons across all tiers**. All items are integrated with the loot generation and database systems.

## Item Categories

### 1. Consumable Items (60+ items)

#### HP Potions
Restores Health Points with various tiers:
- **Minor HP Potion** (Common) - Restores 50 HP
- **HP Potion** (Common) - Restores 150 HP
- **Greater HP Potion** (Uncommon) - Restores 300 HP
- **Superior HP Potion** (Rare) - Restores 600 HP
- **Mega HP Potion** (Epic) - Restores 1,200 HP
- **Ultra HP Potion** (Legendary) - Restores 2,500 HP
- **Percentage-based variants** - Restore 25%, 50%, or 100% HP

#### MP Potions
Restores Mana Points for spell casting:
- **Minor MP Potion** through **Ultra MP Potion** (40 to 2,000 MP)
- **Percentage-based variants** - Restore 25%, 50%, or 100% MP

#### TP Potions
Restores Stamina/Tactical Points for special abilities:
- **Minor TP Potion** through **Ultra TP Potion** (30 to 1,600 TP)
- **Percentage-based variants** - Restore 25%, 50%, or 100% TP

#### Elixirs
Multi-resource restoration items:
- **Minor Elixir** (Uncommon) - Restores 30% of all resources
- **Elixir** (Rare) - Restores 60% of all resources
- **Greater Elixir** (Epic) - Fully restores all resources
- **Megalixir** (Legendary) - Fully restores all party members
- **Combination Potions** - HP+MP, HP+TP, MP+TP variants

#### Status Effect Removers
Cure various negative status effects:
- **Smelling Salts** - Removes sleep, confusion, stun
- **Antidote** - Cures poison
- **Eye Drops** - Cures blindness
- **Echo Herbs** - Cures silence
- **Holy Water** - Removes curses
- **Panacea** (Rare) - Cures all negative status effects
- **Remedy** (Rare) - Cures all statuses + restores 100 HP

#### Special Consumables
Unique effect items:
- **Phoenix Down** (Rare) - Revives fallen ally with 50% HP
- **Mega Phoenix** (Legendary) - Revives all allies with 100% HP
- **Ether/Turbo Ether** - Full MP restoration
- **Power/Guard/Speed Tonics** - Temporary stat boosts
- **Hero's Elixir** (Epic) - Increases all stats by 30% for 10 turns
- **Experience Potion** - Grants 500 bonus XP
- **Lucky Charm** - Increases drop rate by 50%
- **Smoke Bomb** - Guarantees escape from battle
- **Tent/Cottage** - Full party restoration at camp sites

### 2. Key Items & Quest Items (10 items)

Special non-consumable items for progression:
- **Rusty Key** (Common) - Opens forgotten doors
- **Iron Key** (Common) - Opens common locked chests
- **Silver Key** (Uncommon) - Opens uncommon chests
- **Gold Key** (Rare) - Opens rare treasure chests
- **Master Key** (Epic) - Opens most locks
- **Skeleton Key** (Legendary) - Opens any mundane lock
- **Ancient Seal Fragment** (Epic) - Collectible quest item
- **Mysterious Orb** (Rare) - Strange energy orb
- **Dungeon Map** (Common) - Reveals dungeon layout
- **Teleport Crystal** (Uncommon) - Returns to safe point

### 3. Crafting Materials (22 items)

#### Ores & Metals
- **Copper/Iron Ore** (Common) - Basic crafting materials
- **Silver Ore** (Uncommon) - Magical item crafting
- **Gold Ore** (Rare) - Precious metal
- **Mithril Ore** (Epic) - Light yet strong
- **Adamantite Ore** (Legendary) - Strongest metal

#### Magical Materials
- **Magic Crystal** (Uncommon) - Crystallized magic energy
- **Mana Shard** (Rare) - Concentrated mana
- **Dragon Scale** (Epic) - Extremely durable
- **Phoenix Feather** (Legendary) - Life energy

#### Herbs & Plants
- **Healing Herb** (Common) - Mild healing properties
- **Mana Flower** (Uncommon) - Absorbs ambient mana
- **Moonleaf** (Rare) - Grows under moonlight
- **Sunblossom** (Rare) - Blooms in sunlight

#### Monster Parts
- **Slime Gel** (Common) - Alchemy ingredient
- **Wolf Pelt** (Common) - Leather crafting
- **Goblin Tooth** (Common) - Sharp trophy
- **Demon Horn** (Rare) - Dark energy material

#### Essences & Reagents
- **Essence of Fire/Ice/Lightning** - Elemental essences
- **Soul Fragment** (Epic) - Powerful soul fragment

### 4. Weapons (410+ items)

The weapon system generates **41 weapon types × 10 tiers = 410+ unique weapons**, covering all classes from level 1 to 100.

#### Weapon Tiers
Each tier covers 10 levels and has increasing power and rarity:

| Tier | Levels | Rarity |
|------|--------|--------|
| Simple | 1-10 | Common |
| Fine | 11-20 | Common |
| Superior | 21-30 | Uncommon |
| Exquisite | 31-40 | Uncommon |
| Masterwork | 41-50 | Rare |
| Epic | 51-60 | Rare |
| Relic | 61-70 | Epic |
| Celestial | 71-80 | Epic |
| Eldritch | 81-90 | Legendary |
| Dragonic | 91-100 | Legendary |

#### Weapon Types by Category

**General Weapons (1 type)**
- Toolkit - Adventurer

**Melee Weapons (14 types)**
- Sword - Warrior
- Mace - Cleric
- Holy Sword - Paladin
- Greatsword - Dark Knight
- Gunblade - Gunbreaker
- Greataxe - Barbarian
- Handwraps - Monk
- Katana - Samurai
- Spear - Dragoon
- Kunai - Ninja
- Scythe - Reaper
- Dagger - Rogue
- Scimitar - Druid

**Ranged Weapons (5 types)**
- Bow - Ranger
- Crossbow - Hunter
- Firearm - Machinist
- Chakrams - Dancer
- Lute - Bard

**Magic/Support Weapons (15 types)**
- Wand - Wizard
- Orb - Sorcerer
- Pact Tome - Warlock
- Rod - Black Mage
- Staff - White Mage
- Rapier - Red Mage
- Cane - Blue Mage
- Grimoire - Summoner
- Codex - Scholar
- Astrolabe - Astrologian
- Nouliths - Sage
- Focus - Necromancer
- Multi-Tool - Artificer

**Crafting Weapons (6 types)**
- Saw - Carpenter
- Hammer - Blacksmith
- Raising Hammer - Armorer
- Chasing Hammer - Goldsmith
- Head Knife - Leatherworker
- Needle - Weaver

#### Weapon Properties

Each weapon has:
- **Damage Range** - Min and max damage values (scales with tier)
- **Speed** - Attack speed (varies by weapon type)
- **Required Level** - Based on tier (1, 11, 21, 31, 41, 51, 61, 71, 81, 91)
- **Allowed Classes** - Class restrictions
- **Rarity** - Based on tier (Common → Legendary)
- **Base Value** - Gold value (increases with tier)
- **Weight** - Inventory weight (varies by weapon type)

#### Example Weapon Progression

**Sword Progression (Warrior)**
- Simple Sword (Level 1) - Common
- Fine Sword (Level 11) - Common
- Superior Sword (Level 21) - Uncommon
- Exquisite Sword (Level 31) - Uncommon
- Masterwork Sword (Level 41) - Rare
- Epic Sword (Level 51) - Rare
- Relic Sword (Level 61) - Epic
- Celestial Sword (Level 71) - Epic
- Eldritch Sword (Level 81) - Legendary
- Dragonic Sword (Level 91) - Legendary

## System Features

### ConsumableItem Class
New specialized class for consumables with properties:
- **Primary Resource** - Which resource it restores (HP/MP/TP)
- **Restore Amount** - Fixed amount restored
- **Restore Percentage** - Percentage-based restoration
- **Removes Status Effects** - Can cure negative effects
- **Restores All Resources** - Elixir functionality
- **Special Effects** - Custom effects like stat boosts

### ItemCatalog Class
Central repository for all items in the game:
```csharp
// Get all consumables
var consumables = ItemCatalog.GetAllConsumables();

// Get all key items
var keyItems = ItemCatalog.GetAllKeyItems();

// Get all crafting materials
var materials = ItemCatalog.GetAllMaterials();
```

### Item Stacking
All stackable items support:
- **Merging** - Combine two stacks of the same item
- **Splitting** - Divide a stack into two
- **Max Stack Sizes** - Different limits per item type (20-99)
- **Total Value Calculation** - Automatic value × quantity

### Loot Integration
All items are automatically integrated with:
- **Loot Drop System** - Items drop from enemies based on level
- **Chest System** - Items found in treasure chests
- **Item Database** - Centralized item lookup
- **Rarity-Based Drops** - Higher level enemies drop rarer items

## Usage Examples

### Using the Example System
```csharp
var random = new RandomSource();
var example = new EquipmentSystemExample(random);

// View all consumables organized by rarity
example.DemonstrateConsumableItems();

// View key items and quest items
example.DemonstrateKeyItems();

// View crafting materials
example.DemonstrateCraftingMaterials();

// View all weapons in the catalog
example.DemonstrateWeaponsCatalog();

// View weapons for a specific class
example.DemonstrateClassSpecificWeapons(ActorClass.Warrior);

// View weapon progression examples
example.DemonstrateWeaponProgression();

// Compare weapons across tiers
example.DemonstrateWeaponComparison();

// See a sample inventory with various items
example.DemonstrateSampleInventory();

// View enhanced loot drops from enemies
example.DemonstrateEnhancedLootDrops();

// Run all demonstrations
example.RunAllDemonstrations();
```

### Getting Weapons from Catalog

```csharp
// Get all weapons (410+ items)
var allWeapons = ItemCatalog.GetAllWeapons();

// Get all swords across all tiers (10 items)
var allSwords = ItemCatalog.GetWeaponsByType(WeaponType.Sword);

// Get all weapons for a specific tier (41 items)
var tier5Weapons = ItemCatalog.GetWeaponsByTier(EquipmentTier.Masterwork);
```

### Enemy Loot Examples

#### Common Slime (Level 5)
- Minor HP Potion, Slime Gel, Healing Herb
- 50-250 gold
- 100-500 XP

#### Forest Wolf Pack (Level 12)
- HP Potion, Wolf Pelt, Antidote, TP Potion
- 120-600 gold
- 240-1,200 XP

#### Treasure Goblin (Level 18)
- Gold Ore, Silver Ore, Iron Key, Magic Crystal
- 180-900 gold
- 360-1,800 XP

#### Elite Dragon (Level 50)
- Megalixir, Dragon Scale, Phoenix Down, Mithril Ore, Elixir
- 500-2,500 gold
- 1,000-5,000 XP

#### Legendary Boss (Level 75)
- Ultra HP Potion, Adamantite Ore, Phoenix Feather, Mega Phoenix, Hero's Elixir
- 750-3,750 gold
- 1,500-7,500 XP

## Rarity Tiers

Items are classified by rarity affecting their power and value:
- **Common** - Basic items, frequently found
- **Uncommon** - Slightly better items, regular drops
- **Rare** - Powerful items, less common
- **Epic** - Very powerful items, rare drops
- **Legendary** - Extremely powerful, very rare
- **Mythic** - Ultimate tier items

## Item Values & Weight

Each item has:
- **Base Value** (in gold) - Used for selling/buying
- **Weight** (in lbs) - Affects inventory capacity
- **Stack Size** - Maximum items per stack

Weight and value scale with item quality and rarity.

## Future Enhancements

Potential additions to the system:
- **Item Effects System** - Apply actual effects when consumed
- **Crafting System** - Use materials to create items
- **Equipment Upgrade System** - Use materials to enhance gear
- **Shop System** - Buy/sell items with dynamic pricing
- **Inventory Management** - Full inventory UI with sorting
- **Item Sets** - Bonuses for wearing complete sets
- **Unique Items** - One-of-a-kind legendary items

## Architecture

### File Structure
```
FUB_RPG/Implementations/Items/
├── ItemBase.cs              - Abstract base class
├── SimpleItem.cs            - Basic item implementation
├── ConsumableItem.cs        - Consumable items with effects
├── ItemCatalog.cs           - Central item repository
├── ItemStack.cs             - Item stacking logic
└── TierBasedEquipmentGenerator.cs - Equipment generation

FUB_RPG/Examples/
└── EquipmentSystemExample.cs - Demonstration system
```

### Key Interfaces
- **IItem** - Base item interface
- **IItemDatabase** - Item storage and retrieval
- **ILootGenerator** - Loot generation system

## Notes

All items are designed to be data-driven and easily extensible. The system supports:
- JSON/XML serialization for save games
- Dynamic item generation
- Modding support through external data files
- Balance adjustments through configuration

---

**Total Items in Database: 502+**
- Consumables: 60+
- Key Items: 10
- Crafting Materials: 22
- Weapons: 410+ (41 types × 10 tiers)

This system provides a solid foundation for a complete RPG item economy with comprehensive equipment progression!
