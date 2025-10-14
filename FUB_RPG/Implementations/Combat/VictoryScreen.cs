using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fub.Enums; // Added for StatType / RarityTier / LevelCurveType
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
    public StatType Stat { get; set; } // removed redundant qualifier
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

        var totalGold = rewardsList.Sum(r => r.GoldGained);
        var allItems = rewardsList.SelectMany(r => r.ItemsDropped).ToList();
        var aggregated = new VictoryRewards
        {
            ExperienceGained = rewardsList.Sum(r => r.ExperienceGained),
            GoldGained = totalGold,
            ItemsDropped = allItems
        };

        DisplayRewardsSection(aggregated);

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("--- Party Summary ---");
        Console.ResetColor();
        foreach (var ally in allies)
        {
            var jl = ally.JobSystem.GetJobLevel(ally.EffectiveClass);
            Console.WriteLine($"  {ally.Name} (Lv {jl.Level})");
        }
        Console.WriteLine();

        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];
            var rewards = i < rewardsList.Count ? rewardsList[i] : new VictoryRewards { ExperienceGained = 0, OldLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level, NewLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level };

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"--- {ally.Name} (Lv {rewards.OldLevel} → {rewards.NewLevel}) ---");
            Console.ResetColor();

            // Class/species subheader
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"  Species: {ally.Species}  |  Class: {ally.EffectiveClass}");
            Console.ResetColor();

            // Use animated mode (simple:false) now that overlap handling is fixed
            AnimateExperienceGain(ally, rewards, animationSpeedMs, simple: false);

            ShowStatChanges(rewards);
            ShowLearnedAbilities(rewards);
            Console.WriteLine();
        }

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
            if (r.LearnedAbilities is { Count: > 0 })
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

    private static void ShowStatChanges(VictoryRewards rewards)
    {
        if (rewards.StatChanges is not { Count: > 0 }) return;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Stats Increased:");
        Console.ResetColor();
        foreach (var sc in rewards.StatChanges)
        {
            var delta = sc.NewValue - sc.OldValue;
            if (delta <= 0) continue;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    +{delta:0.#} {sc.Stat}");
            Console.ResetColor();
        }
    }

    private static void ShowLearnedAbilities(VictoryRewards rewards)
    {
        if (rewards.LearnedAbilities is not { Count: > 0 }) return;
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

    private void AnimateExperienceGain(IActor actor, VictoryRewards rewards, int animationSpeedMs, bool simple = false)
    {
        if (simple)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("EXPERIENCE");
            Console.ResetColor();
            var jl = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
            var xpCurLevel = _xpCalculator.GetExperienceForLevel(jl.Level, LevelCurveType.Custom);
            var xpNext = _xpCalculator.GetExperienceForLevel(jl.Level + 1, LevelCurveType.Custom);
            var bar = new ExperienceBar(jl.Level, jl.Experience, xpCurLevel, xpNext);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  Level {jl.Level}: ");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(bar.GetBarString(40));
            Console.ResetColor();
            Console.WriteLine(" " + bar.GetProgressText());
            if (rewards.NewLevel > rewards.OldLevel)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  LEVEL UP! {rewards.OldLevel} → {rewards.NewLevel} (+{rewards.NewLevel - rewards.OldLevel}) - Resources Restored");
                Console.ResetColor();
            }
            return;
        }

        var levelInfo = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
        int oldLevel = rewards.OldLevel;
        int newLevel = rewards.NewLevel;
        long xpGained = rewards.ExperienceGained;
        long finalTotalXp = levelInfo.Experience; // cumulative xp after reward
        long startTotalXp = finalTotalXp - xpGained;

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("EXPERIENCE");
        Console.ResetColor();
        Console.WriteLine();

        // Precompute thresholds up to newLevel+1 (cumulative xp required to be that level)
        var thresholds = new List<long>();
        for (int lvl = 1; lvl <= Math.Max(1, newLevel) + 1; lvl++)
            thresholds.Add(_xpCalculator.GetExperienceForLevel(lvl, LevelCurveType.Custom));

        // Guard when no XP gained
        if (xpGained <= 0)
        {
            long levelStartNoGain = thresholds[Math.Max(0, newLevel - 1)];
            long levelNextNoGain = thresholds[Math.Max(0, newLevel)];
            var barNoGain = new ExperienceBar(newLevel, finalTotalXp, levelStartNoGain, levelNextNoGain);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  Level {newLevel}: ");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(barNoGain.GetBarString(40));
            Console.ResetColor();
            Console.WriteLine(" " + barNoGain.GetProgressText());
            Console.WriteLine();
            return;
        }

        int levelsGained = Math.Max(0, newLevel - oldLevel);

        // Reserve a block: K banners (3 lines each) + 1 line for bar
        int startRow = Console.CursorTop;
        int bannerLinesPer = 3;
        int bannerCount = levelsGained;
        int totalBannerLines = bannerCount * bannerLinesPer;
        int barRow = startRow + totalBannerLines;
        for (int i = 0; i < totalBannerLines + 1; i++) Console.WriteLine();
        int continuationRow = barRow + 1; // where subsequent content should resume

        // Detect cursor reposition capability
        bool canReposition = true;
        try { Console.SetCursorPosition(0, barRow); } catch { canReposition = false; }

        // If we cannot reposition, avoid multi-frame output: show banners (if any) and print only the final bar once
        if (!canReposition)
        {
            if (levelsGained > 0)
            {
                for (int lvl = oldLevel + 1; lvl <= newLevel; lvl++)
                {
                    DisplayLevelUp(lvl);
                }
            }

            long finalLevelStart = thresholds[Math.Max(0, newLevel - 1)];
            long finalLevelNext = thresholds[Math.Max(0, newLevel)];
            var finalBar = new ExperienceBar(newLevel, finalTotalXp, finalLevelStart, finalLevelNext);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  Level {newLevel}: ");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(finalBar.GetBarString(40));
            Console.ResetColor();
            Console.WriteLine(" " + finalBar.GetProgressText());
            Console.WriteLine();
            return;
        }

        // Helper: draw one frame at the reserved bar line
        void DrawFrame(int level, long levelStart, long levelNext, long simulatedTotal)
        {
            var frameBar = new ExperienceBar(level, simulatedTotal, levelStart, levelNext);
            try
            {
                Console.SetCursorPosition(0, barRow);
                int clearWidth = Math.Max(0, Console.WindowWidth - 1);
                Console.Write(new string(' ', clearWidth));
                Console.SetCursorPosition(0, barRow);
            }
            catch
            {
                // If reposition fails mid-animation, abort and fall back to printing final bar once at current position
                long finalLevelStart2 = thresholds[Math.Max(0, newLevel - 1)];
                long finalLevelNext2 = thresholds[Math.Max(0, newLevel)];
                var finalBar2 = new ExperienceBar(newLevel, finalTotalXp, finalLevelStart2, finalLevelNext2);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  Level {newLevel}: ");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(finalBar2.GetBarString(40));
                Console.ResetColor();
                Console.Write($" {finalBar2.GetProgressText()}\n");
                throw; // rethrow to break animation loops
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  Level {level}: ");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(frameBar.GetBarString(40));
            Console.ResetColor();
            Console.Write($" {frameBar.GetProgressText()}");
        }

        // Helper: draw a level-up banner at a specific row (3 lines) without adding new lines
        void DrawBannerAt(int topRow, int newLvl)
        {
            const string top = "╔═══════════════════════════════════════╗";
            const string bottom = "╚═══════════════════════════════════════╝";
            string mid = $"║   ⭐ LEVEL UP! → {newLvl,2} ⭐                ║";
            if (mid.Length < top.Length) mid = mid.PadRight(top.Length - 1) + "║";
            var blank = new string(' ', Math.Max(0, Console.WindowWidth - 1));

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                // Clear and draw top line
                Console.SetCursorPosition(0, topRow);
                Console.Write(blank);
                Console.SetCursorPosition(0, topRow);
                Console.Write(top);
                // Clear and draw mid line
                Console.SetCursorPosition(0, topRow + 1);
                Console.Write(blank);
                Console.SetCursorPosition(0, topRow + 1);
                Console.Write(mid);
                // Clear and draw bottom line
                Console.SetCursorPosition(0, topRow + 2);
                Console.Write(blank);
                Console.SetCursorPosition(0, topRow + 2);
                Console.Write(bottom);
                Console.ResetColor();
            }
            catch
            {
                // If we cannot position reliably, fall back to printing banner normally (adds lines)
                DisplayLevelUp(newLvl);
            }
        }

        if (levelsGained == 0)
        {
            // Animate within the same level (no level up)
            long levelStart = thresholds[Math.Max(0, newLevel - 1)];
            long levelNext = thresholds[Math.Max(0, newLevel)];
            long startInside = Math.Max(0, startTotalXp - levelStart);
            long finalInside = Math.Max(0, finalTotalXp - levelStart);
            long span = Math.Max(1, levelNext - levelStart);
            long deltaInside = Math.Max(0, finalInside - startInside);

            int steps = deltaInside <= 0 ? 1 : (int)Math.Clamp(deltaInside / Math.Max(1, span / 40.0), 15, 60);
            double insidePerStep = steps <= 1 ? deltaInside : deltaInside / (double)steps;

            try
            {
                for (int i = 0; i <= steps; i++)
                {
                    long inside = startInside + (long)Math.Round(insidePerStep * i);
                    if (i == steps) inside = finalInside; // exact final
                    long simulatedTotalXp = levelStart + inside;
                    DrawFrame(newLevel, levelStart, levelNext, simulatedTotalXp);
                    if (i < steps && animationSpeedMs > 0) Thread.Sleep(animationSpeedMs);
                }
            }
            catch
            {
                // Already printed final bar in DrawFrame catch; ensure cursor moves below
            }

            try { Console.SetCursorPosition(0, continuationRow); } catch { }
            Console.WriteLine();
            return;
        }

        // Animate across level-ups: fill current level to threshold, show banner (in reserved space), then continue
        int bannerIndex = 0;
        for (int lvl = oldLevel; lvl <= newLevel; lvl++)
        {
            int idxStart = Math.Max(0, lvl - 1);
            int idxNext = Math.Max(0, lvl);
            long levelStart = thresholds[idxStart];
            long levelNext = thresholds[Math.Min(idxNext, thresholds.Count - 1)];

            long segStartInside = lvl == oldLevel ? Math.Max(0, startTotalXp - levelStart) : 0;
            long segEndInside = lvl == newLevel ? Math.Max(0, finalTotalXp - levelStart) : Math.Max(0, levelNext - levelStart);

            long span = Math.Max(1, levelNext - levelStart);
            long deltaInside = Math.Max(0, segEndInside - segStartInside);
            int steps = deltaInside <= 0 ? 1 : (int)Math.Clamp(deltaInside / Math.Max(1, span / 40.0), 15, 60);
            double insidePerStep = steps <= 1 ? deltaInside : deltaInside / (double)steps;

            try
            {
                for (int i = 0; i <= steps; i++)
                {
                    long inside = segStartInside + (long)Math.Round(insidePerStep * i);
                    if (i == steps) inside = segEndInside; // exact final
                    long simulatedTotalXp = levelStart + inside;
                    DrawFrame(lvl, levelStart, levelNext, simulatedTotalXp);
                    if (i < steps && animationSpeedMs > 0) Thread.Sleep(animationSpeedMs);
                }
            }
            catch
            {
                // Final bar already printed in DrawFrame catch, break the animation
                break;
            }

            // If we completed a level and there are more levels to go, draw the banner in the reserved area
            if (lvl < newLevel)
            {
                int bannerTop = startRow + bannerIndex * bannerLinesPer;
                DrawBannerAt(bannerTop, lvl + 1);
                bannerIndex++;
                if (animationSpeedMs > 0) Thread.Sleep(Math.Min(400, animationSpeedMs * 6));
            }
        }

        // Move cursor below the reserved block for subsequent sections
        try { Console.SetCursorPosition(0, continuationRow); } catch { }
        Console.WriteLine();
    }

    private void DisplayLevelUp(int newLevel)
    {
        // Compact banner (consistent width; no extra blank padding to prevent overlap)
        Console.ForegroundColor = ConsoleColor.Yellow;
        const string top = "╔═══════════════════════════════════════╗";
        const string bottom = "╚═══════════════════════════════════════╝";
        // Center content to approx width; keep fixed to avoid jitter
        string mid = $"║   ⭐ LEVEL UP! → {newLevel,2} ⭐                ║";
        if (mid.Length < top.Length) mid = mid.PadRight(top.Length - 1) + "║";
        Console.WriteLine(top);
        Console.WriteLine(mid);
        Console.WriteLine(bottom);
        Console.ResetColor();
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

    private ConsoleColor GetRarityColor(IItem item) => item.Rarity switch
    {
        RarityTier.Common => ConsoleColor.Gray,
        RarityTier.Uncommon => ConsoleColor.Green,
        RarityTier.Rare => ConsoleColor.Blue,
        RarityTier.Epic => ConsoleColor.Magenta,
        RarityTier.Legendary => ConsoleColor.Yellow,
        RarityTier.Mythic => ConsoleColor.Red,
        _ => ConsoleColor.White
    };

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
            _xpCalculator.GetExperienceForLevel(jobLevel.Level, LevelCurveType.Custom),
            _xpCalculator.GetExperienceForLevel(jobLevel.Level + 1, LevelCurveType.Custom)
        );
        
        Console.WriteLine($"\n{bar.GetBarString()} {bar.GetProgressText()}");
    }
}
