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

    // Build a full-width yellow banner with centered content at the current cursor row
    private static void WriteBanner(string content)
    {
        int w = SafeWidth;
        int inner = Math.Max(0, w - 2);
        string top = "╔" + new string('═', inner) + "╗";
        string bottom = "╚" + new string('═', inner) + "╝";
        content = content.Length > inner ? content.Substring(0, inner) : content;
        int padLeft = Math.Max(0, (inner - content.Length) / 2);
        int padRight = Math.Max(0, inner - content.Length - padLeft);
        string mid = "║" + new string(' ', padLeft) + content + new string(' ', padRight) + "║";
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(top);
        Console.WriteLine(mid);
        Console.WriteLine(bottom);
        Console.ResetColor();
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
        try
        {
            Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
        }
        catch { /* ignore if not supported */ }
        Console.CursorVisible = false;
        Console.Clear();

        // Victory header (full width)
        Console.ForegroundColor = ConsoleColor.Yellow;
        WriteBanner("⚔️  VICTORY!  ⚔️");
        Console.ResetColor();

        // Rewards
        DisplayRewardsSection(rewards);

        // Section banner with actor name and class level change/final
        var gained = Math.Max(0, rewards.NewLevel - rewards.OldLevel);
        string sectionTitle = gained > 0
            ? $"⭐ {actor.Name} ({actor.Species}): CLASS LEVEL UP! {rewards.OldLevel} → {rewards.NewLevel} ⭐"
            : $"⭐ {actor.Name} ({actor.Species}) — {actor.EffectiveClass} — Class Lv {rewards.NewLevel} ⭐";
        WriteBanner(sectionTitle);

        // Animate XP (no extra EXPERIENCE header)
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

        try
        {
            Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
        }
        catch { /* ignore if not supported */ }
        Console.CursorVisible = false;
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        WriteBanner("⚔️  VICTORY!  ⚔️");
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

        // Compute alignment parameters so all party XP bars line up perfectly
        // Build the maximum label width across allies (use the longer of old/new level for each)
        int maxLabelWidth = 0;
        int maxProgressLen = 0;

        static string BuildLabel(IActor a, int lvl) => $"  {a.Name} ({a.Species}): Class Lv {lvl} ({a.EffectiveClass}): ";

        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];
            var rewards = i < rewardsList.Count ? rewardsList[i] : new VictoryRewards { ExperienceGained = 0, OldLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level, NewLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level };

            // Label width: take the longer between old and new level labels to remain stable during animation
            int labelLen = Math.Max(BuildLabel(ally, Math.Max(1, rewards.OldLevel)).Length,
                                    BuildLabel(ally, Math.Max(1, rewards.NewLevel)).Length);
            maxLabelWidth = Math.Max(maxLabelWidth, labelLen);

            // Progress text length: use final state as representative sample
            var jl = ally.JobSystem.GetJobLevel(ally.EffectiveClass);
            var xpCur = _xpCalculator.GetExperienceForLevel(jl.Level, LevelCurveType.Custom);
            var xpNxt = _xpCalculator.GetExperienceForLevel(jl.Level + 1, LevelCurveType.Custom);
            var bar = new ExperienceBar(jl.Level, jl.Experience, xpCur, xpNxt);
            maxProgressLen = Math.Max(maxProgressLen, bar.GetProgressText().Length);
        }

        int uniformBarWidth = Math.Max(10, SafeWidth - (maxLabelWidth + 1 + maxProgressLen + 2));

        // Compact per-ally section: no three-line banners, just the bar with the ally name in-label
        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];
            var rewards = i < rewardsList.Count ? rewardsList[i] : new VictoryRewards { ExperienceGained = 0, OldLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level, NewLevel = ally.JobSystem.GetJobLevel(ally.EffectiveClass).Level };

            // XP animation (tight, single line; label includes name/species and is padded to common width; bar uses uniform width)
            AnimateExperienceGain(ally, rewards, animationSpeedMs, simple: false, labelPadWidth: maxLabelWidth, fixedBarWidth: uniformBarWidth);

            ShowStatChanges(rewards);
            ShowLearnedAbilities(rewards);
        }

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
            Console.WriteLine($"{name}: +{r.ExperienceGained} XP (Class Lv {r.OldLevel} → {r.NewLevel})");
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

    private void AnimateExperienceGain(IActor actor, VictoryRewards rewards, int animationSpeedMs, bool simple = false, int? labelPadWidth = null, int? fixedBarWidth = null)
    {
        if (simple)
        {
            var jl = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
            var xpCurLevel = _xpCalculator.GetExperienceForLevel(jl.Level, LevelCurveType.Custom);
            var xpNext = _xpCalculator.GetExperienceForLevel(jl.Level + 1, LevelCurveType.Custom);
            var bar = new ExperienceBar(jl.Level, jl.Experience, xpCurLevel, xpNext);
            string label = $"  {actor.Name} ({actor.Species}): Class Lv {jl.Level} ({actor.EffectiveClass}): ";
            if (labelPadWidth.HasValue) label = label.PadRight(labelPadWidth.Value);
            string progress = bar.GetProgressText();
            int bw = fixedBarWidth ?? CalcBarInteriorWidth(label, progress);
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
                Console.WriteLine($"  CLASS LEVEL UP! {rewards.OldLevel} → {rewards.NewLevel} (+{rewards.NewLevel - rewards.OldLevel}) - Resources Restored");
                Console.ResetColor();
            }
            return;
        }

        // Non-simple: animate bar in place without extra banners/headers
        static bool IsSkipKey(ConsoleKeyInfo k) => k.Key == ConsoleKey.Spacebar || k.Key == ConsoleKey.Enter;
        bool CheckSkip()
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (IsSkipKey(key)) return true;
                }
            }
            catch { }
            return false;
        }

        var levelInfo = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
        int oldLevel = rewards.OldLevel;
        int newLevel = rewards.NewLevel;
        long xpGained = rewards.ExperienceGained;
        long finalTotalXp = levelInfo.Experience;
        long startTotalXp = finalTotalXp - xpGained;

        // Precompute thresholds up to newLevel+1 (cumulative xp required to be that level)
        var thresholds = new List<long>();
        for (int lvl = 1; lvl <= Math.Max(1, newLevel) + 1; lvl++)
            thresholds.Add(_xpCalculator.GetExperienceForLevel(lvl, LevelCurveType.Custom));

        // Decide if we can animate in place (reposition supported)
        bool canReposition = true;
        try { Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop); } catch { canReposition = false; }
        if (!canReposition)
        {
            // Fallback: print final bar only
            long levelStart = thresholds[Math.Max(0, newLevel - 1)];
            long levelNext = thresholds[Math.Max(0, newLevel)];
            var finalBar = new ExperienceBar(newLevel, finalTotalXp, levelStart, levelNext);
            string label = $"  {actor.Name} ({actor.Species}): Class Lv {newLevel} ({actor.EffectiveClass}): ";
            if (labelPadWidth.HasValue) label = label.PadRight(labelPadWidth.Value);
            string progress = finalBar.GetProgressText();
            int bw = fixedBarWidth ?? CalcBarInteriorWidth(label, progress);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(label);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(finalBar.GetBarString(bw));
            Console.ResetColor();
            Console.WriteLine(" " + progress);
            return;
        }

        int barRow = Console.CursorTop; // reserve current line for bar (no extra blank line)
        // Do not insert an extra Console.WriteLine() here; draw the bar on the current line to keep everything top-aligned

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
                // If we fail positioning mid-animation, just print final state once below
                long fs = thresholds[Math.Max(0, newLevel - 1)];
                long fn = thresholds[Math.Max(0, newLevel)];
                var fb = new ExperienceBar(newLevel, finalTotalXp, fs, fn);
                string l2 = $"  {actor.Name} ({actor.Species}): Class Lv {newLevel} ({actor.EffectiveClass}): ";
                if (labelPadWidth.HasValue) l2 = l2.PadRight(labelPadWidth.Value);
                string p2 = fb.GetProgressText();
                int bw2 = fixedBarWidth ?? CalcBarInteriorWidth(l2, p2);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(l2);
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(fb.GetBarString(bw2));
                Console.ResetColor();
                Console.WriteLine(" " + p2);
                return;
            }

            string lbl = $"  {actor.Name} ({actor.Species}): Class Lv {level} ({actor.EffectiveClass}): ";
            if (labelPadWidth.HasValue) lbl = lbl.PadRight(labelPadWidth.Value);
            string ptxt = frameBar.GetProgressText();
            int bwFrame = fixedBarWidth ?? CalcBarInteriorWidth(lbl, ptxt);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(lbl);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(frameBar.GetBarString(bwFrame));
            Console.ResetColor();
            Console.Write($" {ptxt}");
        }

        if (xpGained <= 0)
        {
            long levelStart = thresholds[Math.Max(0, newLevel - 1)];
            long levelNext = thresholds[Math.Max(0, newLevel)];
            DrawFrame(newLevel, levelStart, levelNext, finalTotalXp);
            try { Console.SetCursorPosition(0, barRow + 1); } catch { }
            return;
        }

        // Animate either within a level or across multiple levels
        if (newLevel == oldLevel)
        {
            long levelStart = thresholds[Math.Max(0, newLevel - 1)];
            long levelNext = thresholds[Math.Max(0, newLevel)];
            long startInside = Math.Max(0, startTotalXp - levelStart);
            long finalInside = Math.Max(0, finalTotalXp - levelStart);
            long span = Math.Max(1, levelNext - levelStart);
            long deltaInside = Math.Max(0, finalInside - startInside);

            int steps = deltaInside <= 0 ? 1 : (int)Math.Clamp(deltaInside / Math.Max(1, span / 80.0), 20, 120);
            double insidePerStep = steps <= 1 ? deltaInside : deltaInside / (double)steps;

            for (int i = 0; i <= steps; i++)
            {
                if (CheckSkip()) { break; }
                long inside = startInside + (long)Math.Round(insidePerStep * i);
                if (i == steps) inside = finalInside;
                long simulatedTotalXp = levelStart + inside;
                DrawFrame(newLevel, levelStart, levelNext, simulatedTotalXp);
                if (i < steps && animationSpeedMs > 0) Thread.Sleep(animationSpeedMs);
            }
        }
        else
        {
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

                int steps = deltaInside <= 0 ? 1 : (int)Math.Clamp(deltaInside / Math.Max(1, span / 80.0), 20, 150);
                double insidePerStep = steps <= 1 ? deltaInside : deltaInside / (double)steps;

                for (int i = 0; i <= steps; i++)
                {
                    if (CheckSkip()) { lvl = newLevel; break; }
                    long inside = segStartInside + (long)Math.Round(insidePerStep * i);
                    if (i == steps) inside = segEndInside;
                    long simulatedTotalXp = levelStart + inside;
                    DrawFrame(lvl, levelStart, levelNext, simulatedTotalXp);
                    if (i < steps && animationSpeedMs > 0) Thread.Sleep(animationSpeedMs);
                }
            }
        }

        try { Console.SetCursorPosition(0, barRow + 1); } catch { }
        // No extra Console.WriteLine() here — keep tight, top-aligned layout
    }

    private void DisplayLevelUp(int newLevel)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        int w = SafeWidth;
        int inner = Math.Max(0, w - 2);
        string top = "╔" + new string('═', inner) + "╗";
        string bottom = "╚" + new string('═', inner) + "╝";
        string text = $"⭐ CLASS LEVEL UP! → {newLevel} ⭐";
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
            Console.Write("  Class Level Progress: ");
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
            Console.WriteLine($"CLASS LEVEL UP! {rewards.OldLevel} → {rewards.NewLevel}");
        }

        if (rewards.ItemsDropped.Count > 0)
        {
            Console.WriteLine("\nItems Dropped:");
            foreach (var item in rewards.ItemsDropped)
            {
                Console.WriteLine($"  • {item.Name}");
            }
        }

        var jobLevel = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
        var bar = new ExperienceBar(
            jobLevel.Level,
            jobLevel.Experience,
            _xpCalculator.GetExperienceForLevel(jobLevel.Level, LevelCurveType.Custom),
            _xpCalculator.GetExperienceForLevel(jobLevel.Level + 1, LevelCurveType.Custom)
        );

        Console.WriteLine($"\n{bar.GetBarString()} {bar.GetProgressText()}");
    }
}
