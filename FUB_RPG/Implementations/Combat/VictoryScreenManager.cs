using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fub.Enums;
using Fub.Implementations.Loot;

namespace Fub.Implementations.Combat;

/// <summary>
/// Victory screen data containing combat results and loot information
/// </summary>
public class VictoryScreenData
{
    public LootDrop TotalLoot { get; } = new();
    public List<LootDrop> IndividualEnemyDrops { get; } = new();
    public TimeSpan CombatDuration { get; set; }
    public int EnemiesDefeated { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int TotalExperienceGained => TotalLoot.ExperienceGained;
    public decimal TotalGoldGained => TotalLoot.GoldGained;
    public int TotalItemsFound => TotalLoot.Items.Count;
    
    // Performance metrics
    public Dictionary<string, int> PerformanceStats { get; } = new();
    
    public void AddEnemyLoot(string enemyName, LootDrop loot)
    {
        loot.Source = enemyName;
        IndividualEnemyDrops.Add(loot);
        TotalLoot.Merge(loot);
    }
    
    public void AddPerformanceStat(string statName, int value)
    {
        PerformanceStats[statName] = value;
    }
}

/// <summary>
/// Manages victory screen display and loot distribution
/// </summary>
public class VictoryScreenManager
{
    private readonly Fub.Implementations.Loot.AdvancedLootGenerator _lootGenerator;
    
    public VictoryScreenManager(Fub.Implementations.Loot.AdvancedLootGenerator lootGenerator)
    {
        _lootGenerator = lootGenerator ?? throw new ArgumentNullException(nameof(lootGenerator));
    }
    
    /// <summary>
    /// Creates victory screen data from a completed combat session
    /// </summary>
    public VictoryScreenData CreateVictoryScreen(
        CombatSession combatSession, 
        List<ActorClass> partyClasses,
        int averagePartyLevel,
        TimeSpan combatDuration,
        IReadOnlyDictionary<string, int> combatStats)
    {
        var victoryData = new VictoryScreenData
        {
            CombatDuration = combatDuration,
            EnemiesDefeated = combatSession.Enemies.Count
        };
        
        // Copy combat statistics
        foreach (var stat in combatStats)
        {
            victoryData.AddPerformanceStat(stat.Key, stat.Value);
        }
        
        // Generate loot from each defeated enemy
        foreach (var enemy in combatSession.Enemies)
        {
            var enemyLoot = _lootGenerator.GenerateEnemyLoot(enemy, averagePartyLevel, partyClasses);
            victoryData.AddEnemyLoot(enemy.Name, enemyLoot);
        }
        
        return victoryData;
    }
    
    /// <summary>
    /// Formats victory screen for display
    /// </summary>
    public string FormatVictoryScreen(VictoryScreenData victoryData)
    {
        var output = new StringBuilder();
        
        output.AppendLine("=== VICTORY! ===");
        output.AppendLine();
        output.AppendLine($"Combat Duration: {victoryData.CombatDuration:m\\:ss}");
        output.AppendLine($"Enemies Defeated: {victoryData.EnemiesDefeated}");
        output.AppendLine();
        
        // Experience and Gold summary
        output.AppendLine("=== REWARDS ===");
        output.AppendLine($"Experience Gained: {victoryData.TotalExperienceGained:N0}");
        output.AppendLine($"Gold Found: {victoryData.TotalGoldGained:N0}");
        output.AppendLine();
        
        // Items found
        if (victoryData.TotalLoot.HasItems)
        {
            output.AppendLine("=== ITEMS FOUND ===");
            var itemGroups = victoryData.TotalLoot.Items
                .GroupBy(stack => stack.Item.Rarity)
                 .OrderBy(group => group.Key);
                
            foreach (var rarityGroup in itemGroups)
            {
                output.AppendLine($"{rarityGroup.Key} Items:");
                foreach (var stack in rarityGroup.OrderBy(s => s.Item.Name))
                {
                    var valueText = stack.Item.BaseValue > 0 ? $" ({stack.TotalValue:N0} gold)" : "";
                    output.AppendLine($"  • {stack}{valueText}");
                }
                output.AppendLine();
            }
        }
        
        // Individual enemy drops (for detailed view)
        if (victoryData.IndividualEnemyDrops.Count > 1)
        {
            output.AppendLine("=== DETAILED DROPS ===");
            foreach (var drop in victoryData.IndividualEnemyDrops.Where(d => !d.IsEmpty))
            {
                output.AppendLine($"{drop.Source}:");
                if (drop.ExperienceGained > 0)
                    output.AppendLine($"  Experience: {drop.ExperienceGained}");
                if (drop.GoldGained > 0)
                    output.AppendLine($"  Gold: {drop.GoldGained}");
                foreach (var stack in drop.Items)
                {
                    output.AppendLine($"  • {stack}");
                }
                output.AppendLine();
            }
        }
        
        // Performance statistics
        if (victoryData.PerformanceStats.Any())
        {
            output.AppendLine("=== COMBAT STATISTICS ===");
            foreach (var stat in victoryData.PerformanceStats)
            {
                output.AppendLine($"{stat.Key}: {stat.Value:N0}");
            }
        }
        
        return output.ToString();
    }
}
