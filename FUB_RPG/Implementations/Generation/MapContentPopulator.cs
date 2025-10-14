using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Items;
using Fub.Implementations.Items.Equipment;
using Fub.Implementations.Items.Weapons;
using Fub.Implementations.Map.Objects;
using Fub.Interfaces.Generation;
using Fub.Interfaces.Map;
using Fub.Interfaces.Parties;

namespace Fub.Implementations.Generation;

public sealed class MapContentPopulator : IMapContentPopulator
{
    public void Populate(IMap map, IParty party, IMapContentConfig? config = null)
    {
        var cfg = config ?? new MapContentConfig();
        var rng = cfg.Seed.HasValue ? new System.Random(cfg.Seed.Value) : new System.Random();

        // Gather all floor tiles
        var floors = new List<(int x, int y)>();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    floors.Add((x, y));

        if (floors.Count == 0) return;

        // Avoid party positions
        var partyPositions = party.Members.Select(m => (m.X, m.Y)).ToHashSet();
        floors.RemoveAll(p => partyPositions.Contains(p));

        // Helper to ensure min distance from leader
        var leader = party.Leader;
        bool FarEnough((int x, int y) p) => Math.Abs(p.x - leader.X) + Math.Abs(p.y - leader.Y) >= cfg.MinDistanceFromLeader;

        // Shuffle floors for random selection
        floors = floors.OrderBy(_ => rng.Next()).ToList();

        // Occupancy helpers
        bool IsOccupied((int x, int y) pos) => map.GetObjectsAt(pos.x, pos.y).Any();

        // Determine desired counts based on map kind (safe zones)
        int desiredEnemies = cfg.EnemyCount;
        int desiredNpcs = cfg.NpcCount;
        int desiredItems = cfg.ItemCount;
        int chestCount = 0;
        int shopCount = 0;

        if (map.Kind == MapKind.Town || map.Kind == MapKind.Interior)
        {
            // Town rules
            desiredEnemies = 0; // absolutely no enemies in towns
            // NPCs: at least configured range based on map size
            int minTownNpcs = Math.Max(1, cfg.TownMinNpcs);
            int maxTownNpcs = Math.Max(cfg.TownMaxNpcs, (map.Width * map.Height) / 120);
            desiredNpcs = Math.Max(desiredNpcs, rng.Next(minTownNpcs, maxTownNpcs + 1));
            // Items: a few scattered pickups
            desiredItems = Math.Max(desiredItems, Math.Max(2, map.Width * map.Height / 400));
            chestCount = Math.Max(1, map.Width * map.Height / 900);
            // Shops: at least configured minimum
            shopCount = Math.Max(cfg.TownMinShops, Math.Max(1, map.Width * map.Height / 900));
        }
        else if (map.Kind == MapKind.Overworld)
        {
            desiredEnemies = Math.Max(desiredEnemies, map.Width * map.Height / 300);
            desiredNpcs = Math.Min(desiredNpcs, 2);
            chestCount = Math.Max(0, map.Width * map.Height / 1000);
            shopCount = rng.NextDouble() < 0.1 ? 1 : 0;
        }
        else if (map.Kind == MapKind.Dungeon)
        {
            desiredEnemies = Math.Max(desiredEnemies, map.Width * map.Height / 150);
            desiredNpcs = 0;
            chestCount = Math.Max(1, map.Width * map.Height / 600);
            shopCount = 0;
        }

        // Place enemies first
        int enemiesPlaced = 0;
        var allSpecies = Enum.GetValues<Species>().ToList();
        var allClasses = Enum.GetValues<ActorClass>()
            .Where(c => c is not (ActorClass.Carpenter or ActorClass.Blacksmith or ActorClass.Armorer or ActorClass.Goldsmith or ActorClass.Leatherworker or ActorClass.Weaver or ActorClass.Alchemist or ActorClass.Culinarian or ActorClass.Miner or ActorClass.Botanist or ActorClass.Fisher))
            .ToList();
        foreach (var pos in floors)
        {
            if (enemiesPlaced >= desiredEnemies) break;
            if (!FarEnough(pos)) continue;
            if (IsOccupied(pos)) continue;
            var species = PickRandom(rng, allSpecies);
            var cls = PickRandom(rng, allClasses);
            var enemy = new MonsterActor($"{species} {cls}", species, cls, pos.x, pos.y);
            map.AddObject(new MapEnemyObject(enemy.Name, enemy, pos.x, pos.y));
            enemiesPlaced++;
        }

        // Place NPCs next, ensuring no overlap with enemies
        int npcsPlaced = 0;
        foreach (var pos in floors)
        {
            if (npcsPlaced >= desiredNpcs) break;
            if (!FarEnough(pos)) continue;
            if (IsOccupied(pos)) continue; // avoid all object overlap; in particular avoid enemies
            var npc = new NpcActor($"Villager {npcsPlaced + 1}", Species.Human, ActorClass.Adventurer, pos.x, pos.y);
            var dialogue = GenerateNpcDialogue(rng, npc.Name, map.Theme);
            map.AddObject(new MapNpcObject(npc.Name, npc, pos.x, pos.y, dialogue));
            npcsPlaced++;
        }

        // Place Items
        int itemsPlaced = 0;
        foreach (var pos in floors)
        {
            if (itemsPlaced >= desiredItems) break;
            if (!FarEnough(pos)) continue;
            if (IsOccupied(pos)) continue;
            var itemName = PickRandom(rng, new[] { "Potion", "Coin", "Herb", "Gem", "Scroll" });
            var item = new SimpleItem(itemName, ItemType.Consumable, RarityTier.Common, stackable: false);
            map.AddObject(new MapItemObject(itemName, item, pos.x, pos.y));
            itemsPlaced++;
        }

        // Place chests (interactables)
        int chestsPlaced = 0;
        foreach (var pos in floors)
        {
            if (chestsPlaced >= chestCount) break;
            if (!FarEnough(pos)) continue;
            if (IsOccupied(pos)) continue;
            map.AddObject(new MapInteractableObject("Chest", "Chest", pos.x, pos.y));
            chestsPlaced++;
        }

        // Place themed shops (MapShopObject), 3–10 inventory items each
        int shopsPlaced = 0;
        // Shop themes to use in towns: Weapon, Armor, Item, General
        var availableThemes = new[] { ShopTheme.Weapon, ShopTheme.Armor, ShopTheme.Item, ShopTheme.General };
        foreach (var pos in floors)
        {
            if (shopsPlaced >= shopCount) break;
            if (!FarEnough(pos)) continue;
            if (IsOccupied(pos)) continue;
            var theme = PickRandom(rng, availableThemes);
            int stock = rng.Next(Math.Max(1, cfg.ShopStockMin), Math.Max(cfg.ShopStockMin + 1, cfg.ShopStockMax + 1)); // inclusive bounds
            var inventory = BuildShopInventory(rng, theme, stock);
            string name = theme switch
            {
                ShopTheme.Weapon => "Weapon Shop",
                ShopTheme.Armor => "Armor Shop",
                ShopTheme.Item => "Item Shop",
                _ => "General Shop"
            };
            map.AddObject(new MapShopObject(name, theme, inventory, pos.x, pos.y));
            shopsPlaced++;
        }
    }

    private static IReadOnlyList<string> GenerateNpcDialogue(System.Random rng, string npcName, MapTheme theme)
    {
        var lines = new List<string>();
        var openers = new[]
        {
            $"Hello there! I'm {npcName}.",
            "Greetings, traveler!",
            "Lovely day, isn't it?",
            "Welcome to our town!",
            "Stay a while and listen..."
        };
        var tips = new[]
        {
            "The dungeon is dangerous—bring potions!",
            "Shops restock every now and then... or do they?",
            "If you see a chest, open it before someone else does!",
            "Talk to everyone; you never know who has a quest.",
            "Exploring restores your energy bit by bit."
        };
        var thematics = theme switch
        {
            MapTheme.City => new[]{"Our market's the finest around.", "Keep an eye out for rare goods."},
            MapTheme.Dungeon => new[]{"Darkness ahead—keep your torch lit.", "I heard monsters get stronger deeper down."},
            MapTheme.Forest => new[]{"The woods whisper if you listen.", "Paths change after a storm."},
            MapTheme.Cave => new[]{"Watch your step—loose stones.", "Echoes carry secrets here."},
            MapTheme.Desert => new[]{"Water is worth its weight in gold.", "The dunes hide ancient ruins."},
            _ => Array.Empty<string>()
        };
        lines.Add(PickRandom(rng, openers));
        lines.Add(PickRandom(rng, tips));
        if (thematics.Length > 0) lines.Add(PickRandom(rng, thematics));
        return lines;
    }

    private static List<(Fub.Interfaces.Items.IItem item, int price)> BuildShopInventory(System.Random rng, ShopTheme theme, int count)
    {
        var list = new List<(Fub.Interfaces.Items.IItem item, int price)>();
        for (int i = 0; i < count; i++)
        {
            switch (theme)
            {
                case ShopTheme.Weapon:
                    list.Add(MakeWeapon(rng));
                    break;
                case ShopTheme.Armor:
                    list.Add(MakeArmor(rng));
                    break;
                case ShopTheme.Item:
                    list.Add(MakeConsumable(rng));
                    break;
                default:
                    // General: mix
                    int roll = rng.Next(3);
                    if (roll == 0) list.Add(MakeWeapon(rng));
                    else if (roll == 1) list.Add(MakeArmor(rng));
                    else list.Add(MakeConsumable(rng));
                    break;
            }
        }
        return list;
    }

    private static (Fub.Interfaces.Items.IItem item, int price) MakeConsumable(System.Random rng)
    {
        var names = new[] { "Potion", "Hi-Potion", "Ether", "Elixir", "Antidote", "Herb", "Smoke Bomb" };
        var name = PickRandom(rng, names);
        var rarity = rng.NextDouble() < 0.15 ? RarityTier.Rare : RarityTier.Common;
        var price = rarity == RarityTier.Rare ? rng.Next(40, 91) : rng.Next(10, 31);
        var item = new SimpleItem(name, ItemType.Consumable, rarity, stackable: false);
        return (item, price);
    }

    private static (Fub.Interfaces.Items.IItem item, int price) MakeArmor(System.Random rng)
    {
        var slot = PickRandom(rng, new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Legs, EquipmentSlot.Feet, EquipmentSlot.Hands, EquipmentSlot.Cloak, EquipmentSlot.Belt });
        var rarity = PickRandom(rng, new[] { RarityTier.Common, RarityTier.Uncommon, RarityTier.Rare });
        string slotName = slot.ToString();
        string prefix = PickRandom(rng, new[] { "Leather", "Iron", "Steel", "Silken", "Chain" });
        var name = $"{prefix} {slotName}";
        var item = new Armor(name, slot, rarity, requiredLevel: rng.Next(1, 6));
        int basePrice = slot switch { EquipmentSlot.Chest => 60, EquipmentSlot.Legs => 50, EquipmentSlot.Head => 40, EquipmentSlot.Feet => 35, EquipmentSlot.Hands => 35, EquipmentSlot.Cloak => 45, EquipmentSlot.Belt => 30, _ => 40 };
        int rarityMult = rarity switch { RarityTier.Rare => 3, RarityTier.Uncommon => 2, _ => 1 };
        int price = rng.Next((int)(basePrice * 0.8), (int)(basePrice * 1.2) + 1) * rarityMult;
        return ((Fub.Interfaces.Items.IItem)item, price);
    }

    private static (Fub.Interfaces.Items.IItem item, int price) MakeWeapon(System.Random rng)
    {
        var wtype = PickRandom(rng, Enum.GetValues<WeaponType>().Where(t => t is not (WeaponType.Alembic or WeaponType.Saw or WeaponType.Hammer or WeaponType.RaisingHammer or WeaponType.ChasingHammer or WeaponType.HeadKnife or WeaponType.Needle or WeaponType.Skillet or WeaponType.Pickaxe or WeaponType.Hatchet or WeaponType.FishingRod)).ToList());
        var rarity = PickRandom(rng, new[] { RarityTier.Common, RarityTier.Uncommon, RarityTier.Rare });
        double min = rng.Next(3, 8);
        double max = min + rng.Next(3, 8);
        double speed = Math.Round(rng.NextDouble() * 0.6 + 0.8, 2);
        var name = $"{rarity} {wtype}";
        var item = new Weapon(name, wtype, DamageType.Physical, min, max, speed, rarity, EquipmentSlot.MainHand, requiredLevel: rng.Next(1, 6));
        int basePrice = (int)(min + max) * 4;
        int rarityMult = rarity switch { RarityTier.Rare => 3, RarityTier.Uncommon => 2, _ => 1 };
        int price = rng.Next((int)(basePrice * 0.8), (int)(basePrice * 1.3) + 1) * rarityMult;
        return (item, price);
    }

    private static T PickRandom<T>(System.Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];
}
