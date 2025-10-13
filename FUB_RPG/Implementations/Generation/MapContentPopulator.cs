using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Items;
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
            desiredEnemies = 0; // Safe zone
            desiredNpcs = Math.Max(desiredNpcs, Math.Max(3, map.Width * map.Height / 200));
            desiredItems = Math.Max(desiredItems, map.Width * map.Height / 300);
            chestCount = Math.Max(1, map.Width * map.Height / 700);
            shopCount = Math.Max(1, map.Width * map.Height / 800);
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
            map.AddObject(new MapNpcObject(npc.Name, npc, pos.x, pos.y));
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
            var item = new SimpleItem(itemName, rarity: RarityTier.Common, stackable: false);
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

        // Place shops (interactables)
        int shopsPlaced = 0;
        foreach (var pos in floors)
        {
            if (shopsPlaced >= shopCount) break;
            if (!FarEnough(pos)) continue;
            if (IsOccupied(pos)) continue;
            map.AddObject(new MapInteractableObject("Shop", "Shop", pos.x, pos.y));
            shopsPlaced++;
        }
    }

    private static T PickRandom<T>(System.Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];
}
