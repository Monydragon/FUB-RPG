using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fub.Implementations.Progression;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Combat;

/// <summary>
/// Represents rewards from combat victory
/// </summary>
public class VictoryRewards
{
    public long ExperienceGained { get; set; }
    public int GoldGained { get; set; }
    public List<IItem> ItemsDropped { get; set; } = new();
    public int LevelsGained { get; set; }
    public int OldLevel { get; set; }
    public int NewLevel { get; set; }

    // New: stat changes and abilities learned during this victory
    public List<VictoryStatChange> StatChanges { get; set; } = new();
    public List<string> LearnedAbilities { get; set; } = new();
}

/// <summary>
/// Small helper to represent a stat change
/// </summary>
public class VictoryStatChange
{
    public Fub.Enums.StatType Stat { get; set; }
    public double OldValue { get; set; }
    public double NewValue { get; set; }
}

/// <summary>
/// Handles animated victory screen display with XP bar, items, and gold
/// </summary>
public class VictoryScreen
{
    private readonly ExperienceCalculator _xpCalculator;
    
    public VictoryScreen(ExperienceCalculator xpCalculator)
    {
        _xpCalculator = xpCalculator ?? throw new ArgumentNullException(nameof(xpCalculator));
    }

    /// <summary>
    /// Displays animated victory screen with XP bar, gold, and items
    /// </summary>
    public void DisplayVictory(IActor actor, VictoryRewards rewards, int animationSpeedMs = 50)
    {
        Console.Clear();
        
        // Victory header
        DisplayVictoryHeader();
        
        // Gold and items display
        DisplayRewardsSection(rewards);
        
        // Animate XP gain
        AnimateExperienceGain(actor, rewards, animationSpeedMs);
        
        // Final summary
        DisplaySummary(rewards);
        
        Console.WriteLine("\nPress any key to continue...");
        // Pause so the player can view the victory screen
        Console.ReadKey(true);
    }

    /// <summary>
    /// Displays a consolidated victory screen for the whole party. Aggregates gold/items and
    /// animates XP gain per ally in sequence, then pauses once at the end.
    /// </summary>
    public void DisplayPartyVictory(List<IActor> allies, List<VictoryRewards> rewardsList, int animationSpeedMs = 40)
    {
        if (allies == null) throw new ArgumentNullException(nameof(allies));
        if (rewardsList == null) throw new ArgumentNullException(nameof(rewardsList));

        Console.Clear();
        DisplayVictoryHeader();

        // Aggregate gold and items for a concise header
        var totalGold = rewardsList.Sum(r => r.GoldGained);
        var allItems = rewardsList.SelectMany(r => r.ItemsDropped).ToList();
        var aggregated = new VictoryRewards
        {
            ExperienceGained = rewardsList.Sum(r => r.ExperienceGained),
            GoldGained = totalGold,
            ItemsDropped = allItems
        };

        DisplayRewardsSection(aggregated);

        // Compact overview: show all members with a small EXP bar (like party summary)
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("--- Party Summary ---");
        Console.ResetColor();
        foreach (var ally in allies)
        {
            var jl = ally.JobSystem.GetJobLevel(ally.EffectiveClass);
            // Show name and level only to avoid duplicate bars — the animated bar will be shown per-actor below
            Console.WriteLine($"  {ally.Name} (Lv {jl.Level})");
        }

        Console.WriteLine();

        // Animate XP per ally in sequence and show stat/ability changes
        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];
            var rewards = i < rewardsList.Count ? rewardsList[i] : new VictoryRewards { ExperienceGained = 0, OldLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level, NewLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level };

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"--- {ally.Name} (Lv {rewards.OldLevel} → {rewards.NewLevel}) ---");
            Console.ResetColor();

            // Animate XP for this ally
            AnimateExperienceGain(ally, rewards, animationSpeedMs);

            // Show stat increases
            if (rewards.StatChanges != null && rewards.StatChanges.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Stats Increased:");
                Console.ResetColor();
                foreach (var sc in rewards.StatChanges)
                {
                    var delta = sc.NewValue - sc.OldValue;
                    if (delta > 0.0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"    +{delta:0.#} {sc.Stat}");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                }
            }

            // Show newly learned abilities
            if (rewards.LearnedAbilities != null && rewards.LearnedAbilities.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("  Learned Abilities:");
                Console.ResetColor();
                foreach (var a in rewards.LearnedAbilities)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"    • {a}");
                    Console.ResetColor();
                }
            }

            // Small pause between allies
            Thread.Sleep(200);

            Console.WriteLine();
        }

        // Final per-ally summary
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("═══ SUMMARY ═══");
        Console.ResetColor();
        Console.WriteLine();
        for (int i = 0; i < rewardsList.Count; i++)
        {
            var r = rewardsList[i];
            var name = i < allies.Count ? allies[i].Name : "Ally";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  Experience Gained: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{name}: +{r.ExperienceGained} XP (Lv {r.OldLevel} → {r.NewLevel})");
            Console.ResetColor();
            if (r.LearnedAbilities != null && r.LearnedAbilities.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("    New Abilities: ");
                Console.ResetColor();
                Console.WriteLine(string.Join(", ", r.LearnedAbilities));
            }
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private void DisplayVictoryHeader()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    ⚔️  VICTORY!  ⚔️                    ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void DisplayRewardsSection(VictoryRewards rewards)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("═══ REWARDS ═══");
        Console.ResetColor();                                             
        Console.WriteLine();

        // Gold
        if (rewards.GoldGained > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("  💰 Gold: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"+{rewards.GoldGained}");
            Console.ResetColor();
        }

        // Items
        if (rewards.ItemsDropped.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  📦 Items Dropped:");
            Console.ResetColor();
            
            foreach (var item in rewards.ItemsDropped)
            {
                var rarityColor = GetRarityColor(item);
                Console.ForegroundColor = rarityColor;
                Console.WriteLine($"    • {item.Name}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
    }

    private void AnimateExperienceGain(IActor actor, VictoryRewards rewards, int animationSpeedMs)
    {
        var jobLevel = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
        var oldLevel = rewards.OldLevel;
        var startXp = jobLevel.Experience - rewards.ExperienceGained;

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("═══ EXPERIENCE ═══");
        Console.ResetColor();
        Console.WriteLine();

        // Reserve a single row for the animated XP bar to avoid duplicate bars
        int barRow = Console.CursorTop;
        Console.WriteLine(); // blank line reserved; we'll overwrite this row each frame

        // Calculate XP thresholds
        var xpForOldLevel = _xpCalculator.GetExperienceForLevel(oldLevel, Enums.LevelCurveType.Custom);
        var xpForNextLevel = _xpCalculator.GetExperienceForLevel(oldLevel + 1, Enums.LevelCurveType.Custom);

        // Create initial bar
        var currentLevel = oldLevel;
        
        // Animate XP gain
        int steps = (int)Math.Min(50, Math.Max(1, rewards.ExperienceGained)); // ensure at least 1 step and fit into int
        var xpPerStep = rewards.ExperienceGained / (double)steps;
        
        for (int i = 0; i <= steps; i++)
        {
            var displayXp = (long)(startXp + (xpPerStep * i));
            
            // Check for level up
            while (displayXp >= xpForNextLevel && currentLevel < rewards.NewLevel)
            {
                currentLevel++;
                xpForOldLevel = _xpCalculator.GetExperienceForLevel(currentLevel, Enums.LevelCurveType.Custom);
                xpForNextLevel = _xpCalculator.GetExperienceForLevel(currentLevel + 1, Enums.LevelCurveType.Custom);
                
                // Show level up animation
                DisplayLevelUp(currentLevel);
            }

            // Update bar display on the reserved line
            var bar = new ExperienceBar(currentLevel, displayXp, xpForOldLevel, xpForNextLevel);
            DisplayExperienceBar(bar, i == steps, targetRow: barRow);
            
            if (i < steps)
            {
                Thread.Sleep(animationSpeedMs);
            }
        }

        // Ensure cursor moves below the reserved bar after animation
        try { Console.SetCursorPosition(0, barRow + 1); } catch { Console.WriteLine(); }
        Console.WriteLine();
    }

    private void DisplayExperienceBar(ExperienceBar bar, bool isFinal, int? targetRow = null)
    {
        // Overwrite a specific target row if provided; otherwise clear current line
        if (targetRow.HasValue)
        {
            try
            {
                int row = targetRow.Value;
                int width = Math.Max(0, Console.WindowWidth - 1);
                Console.SetCursorPosition(0, row);
                Console.Write(new string(' ', width));
                Console.SetCursorPosition(0, row);
            }
            catch
            {
                // Fallback
                if (!isFinal) Console.Write('\r');
            }
        }
        else
        {
            try
            {
                int row = Console.CursorTop;
                int width = Math.Max(0, Console.WindowWidth - 1);
                Console.SetCursorPosition(0, row);
                Console.Write(new string(' ', width));
                Console.SetCursorPosition(0, row);
            }
            catch
            {
                if (!isFinal) Console.Write('\r');
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"  Level {bar.CurrentLevel}: ");
        Console.ResetColor();

        // Draw bar
        var barString = bar.GetBarString(40);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(barString);
        Console.ResetColor();

        // Show progress
        Console.Write($" {bar.GetProgressText()}");

        if (isFinal)
        {
            // Move cursor to next line so subsequent output is below the bar
            Console.WriteLine();
        }
    }

    private void DisplayLevelUp(int newLevel)
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔═══════════════════════════════════════╗");
        Console.WriteLine($"║        ⭐ LEVEL UP! → {newLevel,2}  ⭐        ║");
        Console.WriteLine("╚═══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Thread.Sleep(800); // Pause on level up
    }

    private void DisplaySummary(VictoryRewards rewards)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("═══ SUMMARY ═══");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  Experience Gained: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"+{rewards.ExperienceGained} XP");
        Console.ResetColor();

        if (rewards.LevelsGained > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("  Level Progress: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{rewards.OldLevel} → {rewards.NewLevel} (+{rewards.LevelsGained} level{(rewards.LevelsGained > 1 ? "s" : "")})");
            Console.ResetColor();
        }
    }

    private ConsoleColor GetRarityColor(IItem item)
    {
        return item.Rarity switch
        {
            Enums.RarityTier.Common => ConsoleColor.Gray,
            Enums.RarityTier.Uncommon => ConsoleColor.Green,
            Enums.RarityTier.Rare => ConsoleColor.Blue,
            Enums.RarityTier.Epic => ConsoleColor.Magenta,
            Enums.RarityTier.Legendary => ConsoleColor.Yellow,
            Enums.RarityTier.Mythic => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
    }

    /// <summary>
    /// Quick victory display without animation (for testing)
    /// </summary>
    public void DisplayVictoryQuick(IActor actor, VictoryRewards rewards)
    {
        Console.WriteLine("\n⚔️ VICTORY! ⚔️");
        Console.WriteLine($"Gold: +{rewards.GoldGained}");
        Console.WriteLine($"Experience: +{rewards.ExperienceGained} XP");
        
        if (rewards.LevelsGained > 0)
        {
            Console.WriteLine($"LEVEL UP! {rewards.OldLevel} → {rewards.NewLevel}");
        }
        
        if (rewards.ItemsDropped.Count > 0)
        {
            Console.WriteLine("\nItems Dropped:");
            foreach (var item in rewards.ItemsDropped)
            {
                Console.WriteLine($"  • {item.Name}");
            }
        }
        
        var jobLevel = actor.JobSystem.GetJobLevel(actor.Class);
        var bar = new ExperienceBar(
            jobLevel.Level,
            jobLevel.Experience,
            _xpCalculator.GetExperienceForLevel(jobLevel.Level, Enums.LevelCurveType.Custom),
            _xpCalculator.GetExperienceForLevel(jobLevel.Level + 1, Enums.LevelCurveType.Custom)
        );
        
        Console.WriteLine($"\n{bar.GetBarString()} {bar.GetProgressText()}");
    }
}
