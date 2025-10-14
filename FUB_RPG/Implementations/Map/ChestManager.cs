using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fub.Implementations.Loot;
using Fub.Interfaces.Map;
using Fub.Interfaces.Random;
using Fub.Enums;
using Fub.Implementations.Combat;
using Fub.Interfaces.Items;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Map;

/// <summary>
/// Represents a chest or container that can be looted
/// </summary>
public class LootChest : IMapObject
{
    private static readonly System.Random s_rng = new();
    
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Chest";
    public MapObjectKind ObjectKind => MapObjectKind.Container;
    public int X { get; set; }
    public int Y { get; set; }
    public IItem? Item => null; // Chests don't represent single items
    public IActor? Actor => null; // Chests are not actors
    
    public bool IsOpen { get; private set; }
    public bool IsEmpty => !HasLoot;
    public bool HasLoot { get; private set; } = true;
    public ChestConfiguration Configuration { get; }
    public DateTime? LastOpenedTime { get; private set; }
    public DateTime? NextRespawnTime { get; private set; }
    public int TimesOpened { get; private set; }
    public bool CanRespawn => Configuration.CanRespawn && 
                            (Configuration.MaxRespawns < 0 || TimesOpened < Configuration.MaxRespawns);

    private LootDrop? _currentLoot;

    public LootChest(ChestConfiguration configuration, int x, int y)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        X = x;
        Y = y;
        Name = configuration.Name;
    }

    public LootDrop? Open(bool hasRequiredKey = true)
    {
        if (IsOpen || IsEmpty) return null;
        
        if (Configuration.RequiresKey && !hasRequiredKey)
        {
            return null; // Could throw exception or return error result
        }

        IsOpen = true;
        HasLoot = false;
        LastOpenedTime = DateTime.UtcNow;
        TimesOpened++;

        // Schedule respawn if applicable
        if (CanRespawn)
        {
            ScheduleRespawn();
        }

        var loot = _currentLoot;
        _currentLoot = null;
        return loot;
    }

    public void SetLoot(LootDrop loot)
    {
        _currentLoot = loot;
        HasLoot = !loot.IsEmpty;
    }

    public void Reset()
    {
        IsOpen = false;
        HasLoot = true;
        NextRespawnTime = null;
        _currentLoot = null;
    }

    public bool CheckRespawn()
    {
        if (!CanRespawn || NextRespawnTime == null) return false;
        
        if (DateTime.UtcNow >= NextRespawnTime.Value)
        {
            Reset();
            return true;
        }
        
        return false;
    }

    private void ScheduleRespawn()
    {
        if (!CanRespawn) return;
        
        var baseDelay = Configuration.RespawnDelay;
        var variation = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * 0.2); // ±20% variation
        var actualDelay = baseDelay.Add(TimeSpan.FromMilliseconds(
            (s_rng.NextDouble() - 0.5) * 2 * variation.TotalMilliseconds));
            
        NextRespawnTime = DateTime.UtcNow.Add(actualDelay);
    }
}

/// <summary>
/// Manages chest spawning, respawning, and loot population
/// </summary>
public class ChestManager : IDisposable
{
    private readonly Dictionary<Guid, LootChest> _chests = new();
    private readonly Dictionary<string, ChestConfiguration> _chestConfigs = new();
    private readonly AdvancedLootGenerator _lootGenerator;
    private readonly IRandomSource _random;

    public IReadOnlyCollection<LootChest> Chests => _chests.Values;

    public ChestManager(AdvancedLootGenerator lootGenerator, IRandomSource random)
    {
        _lootGenerator = lootGenerator ?? throw new ArgumentNullException(nameof(lootGenerator));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        
        InitializeDefaultChestConfigurations();
    }

    public void RegisterChestConfiguration(ChestConfiguration config)
    {
        _chestConfigs[config.Id] = config;
    }

    public LootChest CreateChest(string configId, int x, int y, int playerLevel = 1)
    {
        if (!_chestConfigs.TryGetValue(configId, out var config))
        {
            config = _chestConfigs["default"];
        }

        var chest = new LootChest(config, x, y);
        _chests[chest.Id] = chest;

        // Generate initial loot
        PopulateChestLoot(chest, playerLevel);

        return chest;
    }

    public void PopulateChestLoot(LootChest chest, int playerLevel)
    {
        // Resolve the loot configuration via loot generator registry
        var lootConfig = _lootGenerator.GetConfiguration(chest.Configuration.LootConfigurationId);
        if (lootConfig == null)
        {
            lootConfig = LootConfiguration.CreateDefault();
        }

        var loot = GenerateChestLoot(lootConfig, playerLevel);
        chest.SetLoot(loot);
    }

    public LootDrop? OpenChest(Guid chestId, bool hasRequiredKey = true, int playerLevel = 1)
    {
        if (!_chests.TryGetValue(chestId, out var chest))
            return null;

        var loot = chest.Open(hasRequiredKey);
        
        // If chest can respawn, schedule loot regeneration
        if (chest.CanRespawn && loot != null)
        {
            // Schedule loot regeneration for when chest respawns
            _ = Task.Delay(chest.Configuration.RespawnDelay).ContinueWith(_ =>
            {
                if (chest.CheckRespawn())
                {
                    PopulateChestLoot(chest, playerLevel);
                }
            });
        }

        return loot;
    }

    public void RemoveChest(Guid chestId)
    {
        _chests.Remove(chestId);
    }

    public void ResetAllChests(int playerLevel = 1)
    {
        foreach (var chest in _chests.Values.Where(c => c.Configuration.RespawnOnMapReset))
        {
            chest.Reset();
            PopulateChestLoot(chest, playerLevel);
        }
    }

    /// <summary>
    /// Manually check for chest respawns - call this periodically
    /// </summary>
    public void CheckRespawns(int playerLevel = 1)
    {
        var respawnedChests = _chests.Values.Where(c => c.CheckRespawn()).ToList();
        
        foreach (var chest in respawnedChests)
        {
            // Regenerate loot for respawned chests
            PopulateChestLoot(chest, playerLevel);
        }
    }

    private LootDrop GenerateChestLoot(LootConfiguration lootConfig, int playerLevel)
    {
        var loot = new LootDrop { Source = "Chest" };

        // Generate gold
        var goldAmount = _random.NextDouble() * (double)(lootConfig.MaxGold - lootConfig.MinGold) + (double)lootConfig.MinGold;
        loot.AddGold((decimal)goldAmount);

        // Generate items - this would use the _lootGenerator but simplified for now
        var itemCount = _random.NextInt(lootConfig.MinItems, lootConfig.MaxItems + 1);
        // TODO: Integrate with _lootGenerator.GenerateRandomItem when item database is populated

        return loot;
    }

    private void InitializeDefaultChestConfigurations()
    {
        // Default wooden chest
        var defaultChest = new ChestConfiguration
        {
            Id = "default",
            Name = "Wooden Chest",
            LootConfigurationId = "default",
            CanRespawn = true,
            RespawnDelay = TimeSpan.FromMinutes(30),
            ChestModel = "wooden_chest",
            OpenSound = "chest_open"
        };
        RegisterChestConfiguration(defaultChest);

        // Rare treasure chest
        var treasureChest = new ChestConfiguration
        {
            Id = "treasure",
            Name = "Treasure Chest",
            LootConfigurationId = "boss", // Uses boss loot table
            CanRespawn = false,
            RequiresKey = true,
            RequiredKeyId = "treasure_key",
            ChestModel = "treasure_chest",
            OpenSound = "treasure_open"
        };
        RegisterChestConfiguration(treasureChest);

        // Supply crate (respawns frequently)
        var supplyCrate = new ChestConfiguration
        {
            Id = "supply_crate",
            Name = "Supply Crate",
            LootConfigurationId = "default",
            CanRespawn = true,
            RespawnDelay = TimeSpan.FromMinutes(15),
            MaxRespawns = 5,
            ChestModel = "supply_crate",
            OpenSound = "crate_open"
        };
        RegisterChestConfiguration(supplyCrate);
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}
