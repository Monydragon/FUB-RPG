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

    // Compute safe printable width (avoid auto-wrap by leaving 1 column margin)
    private static int SafeWidth => Math.Max(60, Console.WindowWidth - 1);

    // Build a full-width box header with centered title
    private static void WriteFullWidthHeader(string title)
    {
        int w = SafeWidth;
        string top = "╔" + new string('═', Math.Max(2, w - 2)) + "╗";
        string bottom = "╚" + new string('═', Math.Max(2, w - 2)) + "╝";
        int inner = Math.Max(0, w - 2);
        // title is non-null per annotations
        title = title.Length > inner - 2 ? title.Substring(0, Math.Max(0, inner - 2)) : title;
        int padLeft = Math.Max(0, (inner - title.Length) / 2);
        int padRight = Math.Max(0, inner - title.Length - padLeft);
        string middle = "║" + new string(' ', padLeft) + title + new string(' ', padRight) + "║";
        Console.WriteLine(top);
        Console.WriteLine(middle);
        Console.WriteLine(bottom);
    }

    // Calculate a stable interior width for the XP bar for a given label and progress text sample
    private static int CalcBarInteriorWidth(string label, string progressSample)
    {
        int w = SafeWidth;
        int occupied = label.Length + 1 /*space before progress*/ + progressSample.Length + 2 /*bar brackets*/;
        int interior = w - occupied;
        return Math.Max(10, interior);
    }

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

        // Victory header (full width)
        Console.ForegroundColor = ConsoleColor.Yellow;
        WriteFullWidthHeader("⚔️  VICTORY!  ⚔️");
        Console.ResetColor();

        // Rewards
        DisplayRewardsSection(rewards);

        // Animate XP
        AnimateExperienceGain(actor, rewards, animationSpeedMs);

        // Summary
        DisplaySummary(rewards);

        // Pause so the player can review the entire screen
        Console.WriteLine();
        Console.WriteLine("Press any key to continue...");
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
        Console.ForegroundColor = ConsoleColor.Yellow;
        WriteFullWidthHeader("⚔️  VICTORY!  ⚔️");
        Console.ResetColor();

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

        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];
            var rewards = i < rewardsList.Count ? rewardsList[i] : new VictoryRewards { ExperienceGained = 0, OldLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level, NewLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level };

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"--- {ally.Name} (Lv {rewards.OldLevel} → {rewards.NewLevel}) ---");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"  Species: {ally.Species}  |  Class: {ally.EffectiveClass}");
            Console.ResetColor();

            AnimateExperienceGain(ally, rewards, animationSpeedMs, simple: false);

            ShowStatChanges(rewards);
            ShowLearnedAbilities(rewards);
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("═══ SUMMARY ═══");
        Console.ResetColor();
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

        // Final pause so the player can review all party results and recap
        Console.WriteLine();
        Console.WriteLine("Press any key to continue...");
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

    private void DisplayRewardsSection(VictoryRewards rewards)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("═══ REWARDS ═══");
        Console.ResetColor();

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
            string label = $"  Level {jl.Level}: ";
            string progress = bar.GetProgressText();
            int bw = CalcBarInteriorWidth(label, progress);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(label);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(bar.GetBarString(bw));
            Console.ResetColor();
            Console.WriteLine(" " + progress);
            if (rewards.NewLevel > rewards.OldLevel)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  LEVEL UP! {rewards.OldLevel} → {rewards.NewLevel} (+{rewards.NewLevel - rewards.OldLevel}) - Resources Restored");
                Console.ResetColor();
            }
            return;
        }

        // Local helper: non-blocking skip detection
        static bool IsSkipKey(ConsoleKeyInfo k) => k.Key == ConsoleKey.Spacebar || k.Key == ConsoleKey.Enter;
        bool CheckSkip()
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (IsSkipKey(key)) return true; // consume and skip
                }
            }
            catch { /* ignore environments without a key buffer */ }
            return false;
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
            string label = $"  Level {newLevel}: ";
            string progress = barNoGain.GetProgressText();
            int bw = CalcBarInteriorWidth(label, progress);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(label);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(barNoGain.GetBarString(bw));
            Console.ResetColor();
            Console.WriteLine(" " + progress);
            return;
        }

        int levelsGained = Math.Max(0, newLevel - oldLevel);

        // If this console cannot reposition the cursor reliably, avoid reserving lines.
        bool canRepositionEarly = true;
        try { Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop); } catch { canRepositionEarly = false; }
        if (!canRepositionEarly)
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
            string label = $"  Level {newLevel}: ";
            string progress = finalBar.GetProgressText();
            int bw = CalcBarInteriorWidth(label, progress);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(label);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(finalBar.GetBarString(bw));
            Console.ResetColor();
            Console.WriteLine(" " + progress);
            return;
        }

        // Reserve a block for banners (3 lines each) and a single line for the bar
        int bannerLinesPer = 3;
        int totalBannerLines = levelsGained * bannerLinesPer;
        for (int i = 0; i < totalBannerLines; i++) Console.WriteLine();
        Console.WriteLine();
        int barRow = Math.Max(0, Console.CursorTop - 1);
        int continuationRow = Console.CursorTop;

        bool repositionFailedDuringAnimation = false;
        bool skipRequested = false;
        int bannersDrawnCount = 0;

        void DrawFinalState()
        {
            // Draw any remaining banners bottom-up and the final bar
            if (levelsGained > 0)
            {
                for (int i = bannersDrawnCount; i < levelsGained; i++)
                {
                    int bannerTop = Math.Max(0, barRow - 1 - (i * bannerLinesPer));
                    DrawBannerAt(bannerTop - (bannerLinesPer - 1), oldLevel + 1 + i);
                }
            }

            long finalLevelStart = thresholds[Math.Max(0, newLevel - 1)];
            long finalLevelNext = thresholds[Math.Max(0, newLevel)];
            if (!repositionFailedDuringAnimation)
            {
                DrawFrame(newLevel, finalLevelStart, finalLevelNext, finalTotalXp);
            }
        }

        // Helper: draw one frame at the reserved bar line
        void DrawFrame(int level, long levelStart, long levelNext, long simulatedTotal)
        {
            var frameBar = new ExperienceBar(level, simulatedTotal, levelStart, levelNext);
            try
            {
                Console.SetCursorPosition(0, barRow);
                int clearWidth = Math.Max(0, SafeWidth);
                Console.Write(new string(' ', clearWidth));
                Console.SetCursorPosition(0, barRow);
            }
            catch
            {
                repositionFailedDuringAnimation = true;
                long finalLevelStart2 = thresholds[Math.Max(0, newLevel - 1)];
                long finalLevelNext2 = thresholds[Math.Max(0, newLevel)];
                var finalBar2 = new ExperienceBar(newLevel, finalTotalXp, finalLevelStart2, finalLevelNext2);
                string label2 = $"  Level {newLevel}: ";
                string progress2 = finalBar2.GetProgressText();
                int bw2 = CalcBarInteriorWidth(label2, progress2);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(label2);
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(finalBar2.GetBarString(bw2));
                Console.ResetColor();
                Console.WriteLine(" " + progress2);
                return;
            }

            string lbl = $"  Level {level}: ";
            string ptxt = frameBar.GetProgressText();
            int bwFrame = CalcBarInteriorWidth(lbl, ptxt);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(lbl);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(frameBar.GetBarString(bwFrame));
            Console.ResetColor();
            Console.Write($" {ptxt}");
        }

        // Helper: draw a level-up banner at a specific row (3 lines) without adding new lines
        void DrawBannerAt(int topRow, int newLvl)
        {
            int w = SafeWidth;
            int inner = Math.Max(0, w - 2);
            string top = "╔" + new string('═', inner) + "╗";
            string bottom = "╚" + new string('═', inner) + "╝";
            string text = $"⭐ LEVEL UP! → {newLvl} ⭐";
            text = text.Length > inner ? text.Substring(0, inner) : text;
            int padLeft = Math.Max(0, (inner - text.Length) / 2);
            int padRight = Math.Max(0, inner - text.Length - padLeft);
            string mid = "║" + new string(' ', padLeft) + text + new string(' ', padRight) + "║";
            var blank = new string(' ', w);

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.SetCursorPosition(0, topRow);
                Console.Write(blank);
                Console.SetCursorPosition(0, topRow);
                Console.Write(top);
                Console.SetCursorPosition(0, topRow + 1);
                Console.Write(blank);
                Console.SetCursorPosition(0, topRow + 1);
                Console.Write(mid);
                Console.SetCursorPosition(0, topRow + 2);
                Console.Write(blank);
                Console.SetCursorPosition(0, topRow + 2);
                Console.Write(bottom);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"VictoryScreen: DrawBannerAt positioning failed: {ex.Message}");
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

            int steps = deltaInside <= 0 ? 1 : (int)Math.Clamp(deltaInside / Math.Max(1, span / 80.0), 20, 120);
            double insidePerStep = steps <= 1 ? deltaInside : deltaInside / (double)steps;

            try
            {
                for (int i = 0; i <= steps; i++)
                {
                    if (repositionFailedDuringAnimation) break;
                    if (!skipRequested && CheckSkip()) { skipRequested = true; break; }
                    long inside = startInside + (long)Math.Round(insidePerStep * i);
                    if (i == steps) inside = finalInside;
                    long simulatedTotalXp = levelStart + inside;
                    DrawFrame(newLevel, levelStart, levelNext, simulatedTotalXp);
                    if (!skipRequested && i < steps && animationSpeedMs > 0) Thread.Sleep(animationSpeedMs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"VictoryScreen: XP animation loop failed: {ex.Message}");
            }

            if (skipRequested)
            {
                DrawFinalState();
            }

            if (!repositionFailedDuringAnimation)
            {
                try { Console.SetCursorPosition(0, continuationRow); } catch (Exception ex) { Console.Error.WriteLine($"VictoryScreen: Unable to reposition cursor after XP animation: {ex.Message}"); }
                Console.WriteLine();
            }
            return;
        }

        // Animate across multiple levels
        int bannerIndex = 0;
        for (int lvl = oldLevel; lvl <= newLevel; lvl++)
        {
            if (repositionFailedDuringAnimation) break;
            if (!skipRequested && CheckSkip()) { skipRequested = true; }

            int idxStart = Math.Max(0, lvl - 1);
            int idxNext = Math.Max(0, lvl);
            long levelStart = thresholds[idxStart];
            long levelNext = thresholds[Math.Min(idxNext, thresholds.Count - 1)];

            long segStartInside = lvl == oldLevel ? Math.Max(0, startTotalXp - levelStart) : 0;
            long segEndInside = lvl == newLevel ? Math.Max(0, finalTotalXp - levelStart) : Math.Max(0, levelNext - levelStart);

            long span = Math.Max(1, levelNext - levelStart);
            long deltaInside = Math.Max(0, segEndInside - segStartInside);

            int steps = deltaInside <= 0 ? 1 : (int)Math.Clamp(deltaInside / Math.Max(1, span / 80.0), 20, 150);
            double insidePerStep = steps <= 1 ? deltaInside : deltaInside / (double)steps;

            try
            {
                for (int i = 0; i <= steps; i++)
                {
                    if (repositionFailedDuringAnimation) break;
                    if (!skipRequested && CheckSkip()) { skipRequested = true; break; }
                    long inside = segStartInside + (long)Math.Round(insidePerStep * i);
                    if (i == steps) inside = segEndInside;
                    long simulatedTotalXp = levelStart + inside;
                    DrawFrame(lvl, levelStart, levelNext, simulatedTotalXp);
                    if (!skipRequested && i < steps && animationSpeedMs > 0) Thread.Sleep(animationSpeedMs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"VictoryScreen: XP multi-level animation failed: {ex.Message}");
                break;
            }

            if (skipRequested)
            {
                DrawFinalState();
                break;
            }

            if (lvl < newLevel)
            {
                int bannerTop = Math.Max(0, barRow - 1 - (bannerIndex * bannerLinesPer));
                DrawBannerAt(bannerTop - (bannerLinesPer - 1), lvl + 1);
                bannersDrawnCount++;
                bannerIndex++;
                if (animationSpeedMs > 0) Thread.Sleep(Math.Min(400, animationSpeedMs * 6));
            }
        }

        if (!repositionFailedDuringAnimation)
        {
            try { Console.SetCursorPosition(0, continuationRow); } catch (Exception ex) { Console.Error.WriteLine($"VictoryScreen: Unable to reposition cursor after multi-level animation: {ex.Message}"); }
            Console.WriteLine();
        }
    }

    private void DisplayLevelUp(int newLevel)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        int w = SafeWidth;
        int inner = Math.Max(0, w - 2);
        string top = "╔" + new string('═', inner) + "╗";
        string bottom = "╚" + new string('═', inner) + "╝";
        string text = $"⭐ LEVEL UP! → {newLevel} ⭐";
        text = text.Length > inner ? text.Substring(0, inner) : text;
        int padLeft = Math.Max(0, (inner - text.Length) / 2);
        int padRight = Math.Max(0, inner - text.Length - padLeft);
        string mid = "║" + new string(' ', padLeft) + text + new string(' ', padRight) + "║";
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
