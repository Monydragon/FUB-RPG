using Fub.Enums;
using Fub.Interfaces.Items;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items.Equipment;
using Fub.Interfaces.Random;
using Fub.Implementations.Combat;

namespace Fub.Implementations.Loot;

/// <summary>
/// Advanced loot generator that handles tier-based equipment and class-specific drops
/// </summary>
public class AdvancedLootGenerator
{
    private readonly IRandomSource _random;
    private readonly Dictionary<string, LootConfiguration> _configurations = new();
    private readonly Dictionary<string, IItem> _itemDatabase = new();

    public AdvancedLootGenerator(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        InitializeDefaultConfigurations();
    }

    public void AddConfiguration(string key, LootConfiguration configuration)
    {
        _configurations[key] = configuration;
    }

    public void RemoveConfiguration(string key)
    {
        _configurations.Remove(key);
    }

    public LootConfiguration? GetConfiguration(string key)
    {
        return _configurations.TryGetValue(key, out var config) ? config : null;
    }

    public void RegisterLootConfiguration(LootConfiguration config)
    {
        _configurations[config.Id] = config;
    }

    public void RegisterItem(IItem item)
    {
        // Use item.Name as key to align with other in-memory databases and weighted entries
        _itemDatabase[item.Name] = item;
    }

    public void AddItemToDatabase(string key, IItem item)
    {
        _itemDatabase[key] = item;
    }

    public void RemoveItemFromDatabase(string key)
    {
        _itemDatabase.Remove(key);
    }

    public IItem? GetItemFromDatabase(string key)
    {
        return _itemDatabase.TryGetValue(key, out var item) ? item : null;
    }

    /// <summary>
    /// Generates loot drops from defeated enemies
    /// </summary>
    public LootDrop GenerateEnemyLoot(IActor enemy, int playerLevel, List<ActorClass> playerClasses)
    {
        var configId = GetEnemyLootConfigId(enemy);
        var config = _configurations.GetValueOrDefault(configId) ?? _configurations["default"];
        
        var lootDrop = new LootDrop
        {
            Source = $"Defeated {enemy.Name}"
        };

        // Scale values based on enemy and player level
        var levelScale = CalculateLevelScale(config, enemy.Level, playerLevel);
        
        // Generate gold
        var goldAmount = _random.NextDouble() * (double)(config.MaxGold - config.MinGold) + (double)config.MinGold;
        lootDrop.AddGold((decimal)(goldAmount * levelScale));
        
        // Generate experience
        var expAmount = (int)(_random.NextDouble() * (config.MaxExperience - config.MinExperience) + config.MinExperience * levelScale);
        lootDrop.AddExperience(expAmount);
        
        // Generate items
        var itemCount = _random.NextInt(config.MinItems, config.MaxItems + 1);
        for (int i = 0; i < itemCount; i++)
        {
            var item = GenerateRandomItem(config, playerLevel, playerClasses);
            if (item != null)
            {
                var quantity = DetermineItemQuantity(item, config);
                lootDrop.AddItem(item, quantity);
            }
        }

        // Add guaranteed items
        foreach (var guaranteedEntry in config.GuaranteedItems)
        {
            if (MeetsConditions(guaranteedEntry, playerLevel, playerClasses))
            {
                var item = _itemDatabase.GetValueOrDefault(guaranteedEntry.ItemId);
                if (item != null)
                {
                    var quantity = _random.NextInt(guaranteedEntry.MinQuantity, guaranteedEntry.MaxQuantity + 1);
                    lootDrop.AddItem(item, quantity);
                }
            }
        }

        return lootDrop;
    }

    /// <summary>
    /// Generates tier-appropriate equipment for classes
    /// </summary>
    public IItem? GenerateTierEquipment(ActorClass actorClass, EquipmentTier tier, EquipmentSlot slot, int level)
    {
        // Calculate appropriate item level based on tier (each tier spans 10 levels)
        var baseLevelForTier = ((int)tier - 1) * 10 + 1;
        var maxLevelForTier = (int)tier * 10;
        var itemLevel = Math.Max(baseLevelForTier, Math.Min(level, maxLevelForTier));

        // Find appropriate items from database
        var candidates = _itemDatabase.Values
            .Where(item => IsAppropriateEquipment(item, actorClass, tier, slot, itemLevel))
            .ToList();

        if (!candidates.Any()) return null;

        // Weight selection based on rarity and appropriateness
        var weights = candidates.Select(item => CalculateItemWeight(item, actorClass, itemLevel)).ToList();
        var selectedIndex = WeightedChoice(weights);
        
        return selectedIndex >= 0 && selectedIndex < candidates.Count ? candidates[selectedIndex] : null;
    }

    /// <summary>
    /// Generates a complete equipment set for a class at a specific tier
    /// </summary>
    public Dictionary<EquipmentSlot, IItem> GenerateEquipmentSet(ActorClass actorClass, EquipmentTier tier, int level)
    {
        var equipmentSet = new Dictionary<EquipmentSlot, IItem>();
        
        // Essential slots for all classes
        var essentialSlots = new[]
        {
            EquipmentSlot.MainHand,
            EquipmentSlot.Chest,
            EquipmentSlot.Legs,
            EquipmentSlot.Feet,
            EquipmentSlot.Head
        };

        foreach (var slot in essentialSlots)
        {
            var item = GenerateTierEquipment(actorClass, tier, slot, level);
            if (item != null)
            {
                equipmentSet[slot] = item;
            }
        }

        // Optional secondary weapon or shield
        if (_random.NextBool(0.7))
        {
            var offhandItem = GenerateTierEquipment(actorClass, tier, EquipmentSlot.OffHand, level);
            if (offhandItem != null)
            {
                equipmentSet[EquipmentSlot.OffHand] = offhandItem;
            }
        }

        return equipmentSet;
    }

    private int WeightedChoice(List<float> weights)
    {
        if (!weights.Any()) return -1;
        
        var totalWeight = weights.Sum();
        if (totalWeight <= 0) return -1;
        
        var randomValue = _random.NextDouble() * totalWeight;
        var currentWeight = 0.0;
        
        for (int i = 0; i < weights.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue <= currentWeight)
                return i;
        }
        
        return weights.Count - 1; // Fallback to last item
    }

    private void InitializeDefaultConfigurations()
    {
        // Default enemy loot
        RegisterLootConfiguration(LootConfiguration.CreateDefault());
        
        // Boss loot configuration
        var bossConfig = new LootConfiguration
        {
            Id = "boss",
            Name = "Boss Loot",
            MinItems = 2,
            MaxItems = 5,
            MinGold = 50,
            MaxGold = 200,
            MinExperience = 100,
            MaxExperience = 300,
            ScaleWithLevel = true,
            LevelScalingFactor = 1.5f
        };
        
        // Higher rarity weights for bosses
        bossConfig.RarityWeights[RarityTier.Common] = 30f;
        bossConfig.RarityWeights[RarityTier.Uncommon] = 35f;
        bossConfig.RarityWeights[RarityTier.Rare] = 25f;
        bossConfig.RarityWeights[RarityTier.Epic] = 8f;
        bossConfig.RarityWeights[RarityTier.Legendary] = 2f;
        bossConfig.RarityWeights[RarityTier.Mythic] = 0.5f;
        
        RegisterLootConfiguration(bossConfig);
        
        // Elite enemy configuration
        var eliteConfig = new LootConfiguration
        {
            Id = "elite",
            Name = "Elite Enemy Loot",
            MinItems = 1,
            MaxItems = 3,
            MinGold = 25,
            MaxGold = 75,
            MinExperience = 50,
            MaxExperience = 120,
            LevelScalingFactor = 1.3f
        };
        
        eliteConfig.RarityWeights[RarityTier.Common] = 50f;
        eliteConfig.RarityWeights[RarityTier.Uncommon] = 30f;
        eliteConfig.RarityWeights[RarityTier.Rare] = 15f;
        eliteConfig.RarityWeights[RarityTier.Epic] = 4f;
        eliteConfig.RarityWeights[RarityTier.Legendary] = 1f;
        
        RegisterLootConfiguration(eliteConfig);
    }

    private double CalculateLevelScale(LootConfiguration config, int enemyLevel, int playerLevel)
    {
        if (!config.ScaleWithLevel) return 1.0;
        
        var averageLevel = (enemyLevel + playerLevel) / 2.0;
        var levelDifference = averageLevel - config.BaseLevel;
        return Math.Max(0.1, 1.0 + (levelDifference * (config.LevelScalingFactor - 1.0) / 10.0));
    }

    private string GetEnemyLootConfigId(IActor enemy)
    {
        // Determine loot config based on enemy properties
        if (enemy.Name.Contains("Boss", StringComparison.OrdinalIgnoreCase))
            return "boss";
        if (enemy.Name.Contains("Elite", StringComparison.OrdinalIgnoreCase))
            return "elite";
        return "default";
    }

    // Public wrapper to allow external callers (e.g., chests) to pick an item
    public IItem? PickRandomItem(LootConfiguration config, int playerLevel, List<ActorClass> playerClasses)
    {
        return PickRandomItemInternal(config, playerLevel, playerClasses);
    }

    public int GetSuggestedQuantity(IItem item, LootConfiguration config)
    {
        return DetermineItemQuantity(item, config);
    }

    private IItem? PickRandomItemInternal(LootConfiguration config, int playerLevel, List<ActorClass> playerClasses)
    {
        // Select from possible items based on weights and conditions
        var availableItems = config.PossibleItems
            .Where(entry => MeetsConditions(entry, playerLevel, playerClasses))
            .ToList();

        if (!availableItems.Any()) return null;

        var weights = availableItems.Select(entry => entry.Weight).ToList();
        var selectedIndex = WeightedChoice(weights);
        
        if (selectedIndex >= 0 && selectedIndex < availableItems.Count)
        {
            var selectedEntry = availableItems[selectedIndex];
            return _itemDatabase.GetValueOrDefault(selectedEntry.ItemId);
        }

        return null;
    }

    private bool MeetsConditions(WeightedItemEntry entry, int playerLevel, List<ActorClass> playerClasses)
    {
        if (playerLevel < entry.MinLevel || playerLevel > entry.MaxLevel)
            return false;
            
        if (entry.AllowedClasses.Any() && !entry.AllowedClasses.Intersect(playerClasses).Any())
            return false;

        return true;
    }

    private int DetermineItemQuantity(IItem item, LootConfiguration config)
    {
        if (!item.Stackable) return 1;
        
        // For stackable items, determine quantity based on item type and rarity
        var baseQuantity = item.ItemType switch
        {
            ItemType.Consumable => _random.NextInt(1, 5),
            ItemType.Material => _random.NextInt(1, 10),
            ItemType.Currency => _random.NextInt(5, 50),
            _ => 1
        };

        // Adjust based on rarity (rarer items drop in smaller quantities)
        var rarityMultiplier = item.Rarity switch
        {
            RarityTier.Common => 1.0f,
            RarityTier.Uncommon => 0.8f,
            RarityTier.Rare => 0.6f,
            RarityTier.Epic => 0.4f,
            RarityTier.Legendary => 0.2f,
            RarityTier.Mythic => 0.1f,
            _ => 1.0f
        };

        // Optional quantity modifier from config (e.g., seasonal events, difficulty)
        float quantityModifier = 1.0f;
        if (config.ConditionalModifiers.TryGetValue("QuantityMultiplier", out var qMul))
        {
            quantityModifier = Math.Max(0.1f, qMul);
        }

        var finalQuantity = Math.Max(1, (int)(baseQuantity * rarityMultiplier * quantityModifier));
        return Math.Min(finalQuantity, item.MaxStackSize);
    }

    private bool IsAppropriateEquipment(IItem item, ActorClass actorClass, EquipmentTier tier, EquipmentSlot slot, int level)
    {
        if (item is not IEquipment equipment) return false;
        
        return equipment.Slot == slot &&
               equipment.Tier == tier &&
               level >= equipment.RequiredLevel &&
               (equipment.AllowedClasses.Contains(actorClass) || !equipment.AllowedClasses.Any());
    }

    private float CalculateItemWeight(IItem item, ActorClass actorClass, int level)
    {
        float weight = 1.0f;
        
        if (item is IEquipment equipment)
        {
            // Prefer items that exactly match the class
            if (equipment.AllowedClasses.Contains(actorClass))
                weight *= 2.0f;
                
            // Prefer items closer to the target level
            var levelDifference = Math.Abs(equipment.RequiredLevel - level);
            weight *= Math.Max(0.1f, 1.0f - (levelDifference * 0.1f));
        }
        
        // Adjust based on rarity
        weight *= item.Rarity switch
        {
            RarityTier.Common => 1.0f,
            RarityTier.Uncommon => 0.8f,
            RarityTier.Rare => 0.6f,
            RarityTier.Epic => 0.3f,
            RarityTier.Legendary => 0.1f,
            RarityTier.Mythic => 0.05f,
            _ => 1.0f
        };
        
        return weight;
    }

    private IItem? GenerateRandomItem(LootConfiguration config, int playerLevel, List<ActorClass> playerClasses)
    {
        // 1) Try class-specific pools first (merge for all matching classes)
        var classEntries = new List<WeightedItemEntry>();
        foreach (var cls in playerClasses)
        {
            if (config.ClassSpecificItems.TryGetValue(cls, out var entries))
            {
                classEntries.AddRange(entries.Where(e => MeetsConditions(e, playerLevel, playerClasses)));
            }
        }

        if (classEntries.Count > 0)
        {
            var weights = classEntries.Select(e => e.Weight).ToList();
            var idx = WeightedChoice(weights);
            if (idx >= 0 && idx < classEntries.Count)
            {
                var entry = classEntries[idx];
                var item = _itemDatabase.GetValueOrDefault(entry.ItemId);
                if (item != null)
                    return item;
            }
        }

        // 2) Try configured possible items
        var configured = PickRandomItemInternal(config, playerLevel, playerClasses);
        if (configured != null)
            return configured;

        // 3) Fallback: pick from entire item database with heuristic weights
        if (_itemDatabase.Count == 0)
            return null;

        var candidates = _itemDatabase.Values.ToList();
        var candWeights = new List<float>(candidates.Count);

        foreach (var item in candidates)
        {
            float weight = 1f;

            // Rarity weighting
            if (config.RarityWeights.TryGetValue(item.Rarity, out var rarityW))
                weight *= Math.Max(0.0001f, rarityW);

            // ItemType weighting (optional)
            if (config.ItemTypeWeights != null && config.ItemTypeWeights.TryGetValue(item.ItemType, out var typeW))
                weight *= Math.Max(0.0001f, typeW);

            if (item is IEquipment eq)
            {
                // Tier weighting (optional)
                if (config.TierWeights != null && config.TierWeights.TryGetValue(eq.Tier, out var tierW))
                    weight *= Math.Max(0.0001f, tierW);

                // Level suitability: closer required level gets more weight
                var levelDiff = Math.Abs(eq.RequiredLevel - playerLevel);
                weight *= Math.Max(0.05f, 1f - (levelDiff * 0.07f));

                // Penalize items far above current level
                if (eq.RequiredLevel > playerLevel + 5)
                    weight *= 0.2f;

                // Class fit bonus/penalty
                var allowed = eq.AllowedClasses;
                if (allowed.Count > 0)
                {
                    if (playerClasses.Any(c => allowed.Contains(c)))
                        weight *= 1.5f;
                    else
                        weight *= 0.25f;
                }
            }

            candWeights.Add(weight);
        }

        var chosen = WeightedChoice(candWeights);
        return chosen >= 0 && chosen < candidates.Count ? candidates[chosen] : null;
    }
}
