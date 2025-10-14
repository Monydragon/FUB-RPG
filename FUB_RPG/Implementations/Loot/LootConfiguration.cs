using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Loot;

/// <summary>
/// Configuration for loot generation with detailed control
/// </summary>
public class LootConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Basic drop settings
    public int MinItems { get; set; } = 0;
    public int MaxItems { get; set; } = 3;
    public decimal MinGold { get; set; } = 0;
    public decimal MaxGold { get; set; } = 100;
    public int MinExperience { get; set; } = 0;
    public int MaxExperience { get; set; } = 50;
    
    // Rarity settings
    public Dictionary<RarityTier, float> RarityWeights { get; set; } = new();
    public Dictionary<EquipmentTier, float> TierWeights { get; set; } = new();
    public Dictionary<ItemType, float> ItemTypeWeights { get; set; } = new();
    
    // Level scaling
    public bool ScaleWithLevel { get; set; } = true;
    public int BaseLevel { get; set; } = 1;
    public float LevelScalingFactor { get; set; } = 1.2f;
    
    // Conditional modifiers
    public Dictionary<string, float> ConditionalModifiers { get; set; } = new();
    
    // Specific item pools
    public List<WeightedItemEntry> GuaranteedItems { get; set; } = new();
    public List<WeightedItemEntry> PossibleItems { get; set; } = new();
    
    // Class-specific equipment
    public Dictionary<ActorClass, List<WeightedItemEntry>> ClassSpecificItems { get; set; } = new();
    
    public static LootConfiguration CreateDefault()
    {
        var config = new LootConfiguration
        {
            Id = "default",
            Name = "Default Loot",
            MinItems = 0,
            MaxItems = 2,
            MinGold = 5,
            MaxGold = 25,
            MinExperience = 10,
            MaxExperience = 30
        };
        
        // Set default rarity weights
        config.RarityWeights[RarityTier.Common] = 70f;
        config.RarityWeights[RarityTier.Uncommon] = 20f;
        config.RarityWeights[RarityTier.Rare] = 8f;
        config.RarityWeights[RarityTier.Epic] = 2f;
        config.RarityWeights[RarityTier.Legendary] = 0.5f;
        config.RarityWeights[RarityTier.Mythic] = 0.1f;
        
        // Set default tier weights (favoring lower tiers)
        for (int i = 1; i <= 10; i++)
        {
            var tier = (EquipmentTier)i;
            config.TierWeights[tier] = Math.Max(1f, 11f - i);
        }
        
        return config;
    }
}

public class WeightedItemEntry
{
    public string ItemId { get; set; } = string.Empty;
    public float Weight { get; set; } = 1f;
    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 100;
    public List<ActorClass> AllowedClasses { get; set; } = new();
    public Dictionary<string, object> Conditions { get; set; } = new();
}

/// <summary>
/// Configuration for chest and container spawning
/// </summary>
public class ChestConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LootConfigurationId { get; set; } = string.Empty;
    
    // Spawning behavior
    public bool CanRespawn { get; set; } = true;
    public TimeSpan RespawnDelay { get; set; } = TimeSpan.FromMinutes(30);
    public int MaxRespawns { get; set; } = -1; // -1 = infinite
    public bool RespawnOnMapReset { get; set; } = true;
    
    // Visual and interaction
    public string ChestModel { get; set; } = "wooden_chest";
    public string OpenSound { get; set; } = "chest_open";
    public string EmptyMessage { get; set; } = "The chest is empty.";
    public bool RequiresKey { get; set; } = false;
    public string RequiredKeyId { get; set; } = string.Empty;
    
    // Location constraints
    public List<MapTheme> AllowedMapThemes { get; set; } = new();
    public List<RoomType> AllowedRoomTypes { get; set; } = new();
    public int MinDistanceFromEntrance { get; set; } = 0;
    public int MaxDistanceFromEntrance { get; set; } = int.MaxValue;
}

/// <summary>
/// Configuration for map object respawning
/// </summary>
public class RespawnConfiguration
{
    public string ObjectType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public TimeSpan BaseRespawnTime { get; set; } = TimeSpan.FromMinutes(15);
    public float RandomVariation { get; set; } = 0.2f; // ±20% variation
    public int MaxConcurrentObjects { get; set; } = 10;
    public bool RespawnOnPlayerExit { get; set; } = false;
    public bool RespawnOnMapTransition { get; set; } = true;
    
    // Conditions for respawning
    public Dictionary<string, object> RespawnConditions { get; set; } = new();
}
