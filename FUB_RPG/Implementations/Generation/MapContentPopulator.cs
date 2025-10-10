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

        // Place enemies (any species, any class except crafting/gathering)
        int enemiesPlaced = 0;
        var allSpecies = Enum.GetValues<Species>().ToList();
        var allClasses = Enum.GetValues<ActorClass>().Where(c => c is not (ActorClass.Carpenter or ActorClass.Blacksmith or ActorClass.Armorer or ActorClass.Goldsmith or ActorClass.Leatherworker or ActorClass.Weaver or ActorClass.Alchemist or ActorClass.Culinarian or ActorClass.Miner or ActorClass.Botanist or ActorClass.Fisher)).ToList();
        foreach (var pos in floors)
        {
            if (enemiesPlaced >= cfg.EnemyCount) break;
            if (!FarEnough(pos)) continue;
            var species = PickRandom(rng, allSpecies);
            var cls = PickRandom(rng, allClasses);
            var enemy = new MonsterActor($"{species} {cls}", species, cls, pos.x, pos.y);
            map.AddObject(new MapEnemyObject(enemy.Name, enemy, pos.x, pos.y));
            enemiesPlaced++;
        }

        // Place NPCs
        int npcsPlaced = 0;
        foreach (var pos in floors)
        {
            if (npcsPlaced >= cfg.NpcCount) break;
            if (!FarEnough(pos)) continue;
            var npc = new NpcActor($"Villager {npcsPlaced + 1}", Species.Human, ActorClass.Adventurer, pos.x, pos.y);
            map.AddObject(new MapNpcObject(npc.Name, npc, pos.x, pos.y));
            npcsPlaced++;
        }

        // Place Items
        int itemsPlaced = 0;
        foreach (var pos in floors)
        {
            if (itemsPlaced >= cfg.ItemCount) break;
            if (!FarEnough(pos)) continue;
            var itemName = PickRandom(rng, new[] { "Potion", "Coin", "Herb", "Gem", "Scroll" });
            var item = new SimpleItem(itemName, rarity: RarityTier.Common, stackable: false);
            map.AddObject(new MapItemObject(itemName, item, pos.x, pos.y));
            itemsPlaced++;
        }
    }

    private static T PickRandom<T>(System.Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];
}
