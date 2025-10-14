using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Loot;
using Fub.Interfaces.Random;
using Fub.Interfaces.Items;
using Fub.Enums;

namespace Fub.Implementations.Loot;

/// <summary>
/// Represents a loot table with weighted entries
/// </summary>
public class LootTable : ILootTable
{
    public string Id { get; }
    public IReadOnlyList<ILootEntry> Entries => _entries;
    
    private readonly List<ILootEntry> _entries = new();

    public LootTable(string id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    public void AddEntry(ILootEntry entry)
    {
        if (entry != null)
        {
            _entries.Add(entry);
        }
    }

    public void RemoveEntry(ILootEntry entry)
    {
        _entries.Remove(entry);
    }

    public List<ILootEntry> GenerateLoot(IRandomSource random, int playerLevel, int numRolls = 1)
    {
        var results = new List<ILootEntry>();
        
        for (int i = 0; i < numRolls; i++)
        {
            var entry = SelectRandomEntry(random, playerLevel);
            if (entry != null)
            {
                results.Add(entry);
            }
        }
        
        return results;
    }

    private ILootEntry? SelectRandomEntry(IRandomSource random, int playerLevel)
    {
        var validEntries = _entries.Where(e => e.MeetsConditions(playerLevel)).ToList();
        if (!validEntries.Any()) return null;

        var totalWeight = validEntries.Sum(e => e.Weight);
        if (totalWeight <= 0) return null;

        var roll = random.NextDouble() * totalWeight;
        var currentWeight = 0.0;

        foreach (var entry in validEntries)
        {
            currentWeight += entry.Weight;
            if (roll <= currentWeight)
            {
                return entry;
            }
        }

        return validEntries.LastOrDefault();
    }
}

/// <summary>
/// Enhanced implementation of loot entry matching the existing interface
/// </summary>
public class LootEntry : ILootEntry
{
    public string ItemId { get; }
    public float Weight { get; }
    public int MinQuantity { get; }
    public int MaxQuantity { get; }
    public int MinLevel { get; }
    public int MaxLevel { get; }
    public Dictionary<string, object> Conditions { get; }
    public LootEntryType EntryType { get; }
    public LootRollType RollType { get; }
    public RarityTier? RarityFilter { get; }
    public double Chance { get; }
    public IItem? Item { get; }
    public string? TableReferenceId { get; }
    public (int min, int max)? QuantityRange { get; }

    public LootEntry(
        string itemId, 
        float weight, 
        int minQuantity = 1, 
        int maxQuantity = 1,
        int minLevel = 1,
        int maxLevel = 100,
        Dictionary<string, object>? conditions = null,
        LootEntryType entryType = LootEntryType.Item,
        LootRollType rollType = LootRollType.Chance,
        RarityTier? rarityFilter = null,
        double chance = 1.0,
        IItem? item = null,
        string? tableReferenceId = null)
    {
        ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        Weight = Math.Max(0f, weight);
        MinQuantity = Math.Max(1, minQuantity);
        MaxQuantity = Math.Max(MinQuantity, maxQuantity);
        MinLevel = Math.Max(1, minLevel);
        MaxLevel = Math.Max(MinLevel, maxLevel);
        Conditions = conditions ?? new Dictionary<string, object>();
        EntryType = entryType;
        RollType = rollType;
        RarityFilter = rarityFilter;
        Chance = Math.Clamp(chance, 0.0, 1.0);
        Item = item;
        TableReferenceId = tableReferenceId;
        QuantityRange = (MinQuantity, MaxQuantity);
    }

    public bool MeetsConditions(int playerLevel)
    {
        if (playerLevel < MinLevel || playerLevel > MaxLevel)
            return false;

        // Check rarity filter if specified
        if (RarityFilter.HasValue && Item != null && Item.Rarity != RarityFilter.Value)
            return false;

        // Additional condition checks can be added here
        return true;
    }

    public int RollQuantity(IRandomSource random)
    {
        return random.NextInt(MinQuantity, MaxQuantity + 1);
    }
}

/// <summary>
/// Manages multiple loot tables and provides generation services
/// </summary>
public class LootTableManager
{
    private readonly Dictionary<string, ILootTable> _lootTables = new();
    private readonly IRandomSource _random;

    public LootTableManager(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        InitializeDefaultTables();
    }

    public void RegisterLootTable(ILootTable lootTable)
    {
        _lootTables[lootTable.Id] = lootTable;
    }

    public ILootTable? GetLootTable(string id)
    {
        return _lootTables.GetValueOrDefault(id);
    }

    public List<ILootEntry> GenerateLoot(string tableId, int playerLevel, int numRolls = 1)
    {
        var table = GetLootTable(tableId);
        if (table is LootTable lootTable)
        {
            return lootTable.GenerateLoot(_random, playerLevel, numRolls);
        }
        return new List<ILootEntry>();
    }

    private void InitializeDefaultTables()
    {
        // Create default monster loot table
        var monsterTable = new LootTable("monsters_default");
        monsterTable.AddEntry(new LootEntry("health_potion", 30f, 1, 3, entryType: LootEntryType.Item));
        monsterTable.AddEntry(new LootEntry("mana_potion", 25f, 1, 2, entryType: LootEntryType.Item));
        monsterTable.AddEntry(new LootEntry("iron_ore", 20f, 1, 5, entryType: LootEntryType.Item));
        monsterTable.AddEntry(new LootEntry("leather_scraps", 15f, 2, 8, entryType: LootEntryType.Item));
        monsterTable.AddEntry(new LootEntry("magic_crystal", 8f, 1, 2, entryType: LootEntryType.Item));
        monsterTable.AddEntry(new LootEntry("rare_gem", 2f, 1, 1, 10, 100, entryType: LootEntryType.Item, rarityFilter: RarityTier.Rare));
        RegisterLootTable(monsterTable);

        // Create boss loot table
        var bossTable = new LootTable("boss_default");
        bossTable.AddEntry(new LootEntry("rare_equipment", 40f, 1, 2, 20, 100, entryType: LootEntryType.Item, rarityFilter: RarityTier.Rare));
        bossTable.AddEntry(new LootEntry("epic_weapon", 15f, 1, 1, 30, 100, entryType: LootEntryType.Item, rarityFilter: RarityTier.Epic));
        bossTable.AddEntry(new LootEntry("legendary_armor", 5f, 1, 1, 50, 100, entryType: LootEntryType.Item, rarityFilter: RarityTier.Legendary));
        bossTable.AddEntry(new LootEntry("skill_tome", 20f, 1, 1, 25, 100, entryType: LootEntryType.Item));
        bossTable.AddEntry(new LootEntry("boss_trophy", 10f, 1, 1, entryType: LootEntryType.Item));
        bossTable.AddEntry(new LootEntry("large_gold_pile", 35f, 1, 3, entryType: LootEntryType.Currency));
        RegisterLootTable(bossTable);

        // Create treasure chest loot table
        var chestTable = new LootTable("treasure_chest");
        chestTable.AddEntry(new LootEntry("equipment_upgrade_stone", 25f, 1, 2, entryType: LootEntryType.Item));
        chestTable.AddEntry(new LootEntry("rare_materials", 30f, 2, 5, entryType: LootEntryType.Item));
        chestTable.AddEntry(new LootEntry("enchanted_scroll", 20f, 1, 1, entryType: LootEntryType.Item, rarityFilter: RarityTier.Uncommon));
        chestTable.AddEntry(new LootEntry("gold_coins", 50f, 10, 50, entryType: LootEntryType.Currency));
        chestTable.AddEntry(new LootEntry("precious_jewel", 8f, 1, 1, entryType: LootEntryType.Item, rarityFilter: RarityTier.Rare));
        RegisterLootTable(chestTable);
    }
}
