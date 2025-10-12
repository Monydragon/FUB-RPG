using Spectre.Console;
using Spectre.Console.Rendering;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Combat;
using Fub.Interfaces.Items.Weapons;
using Fub.Implementations.Input;
using Fub.Interfaces.Abilities;
using Fub.Implementations.Abilities;

namespace Fub.Implementations.Combat;

public sealed class TurnBasedCombatResolver : ICombatResolver
{
    private readonly System.Random _rng = new();
    private readonly Dictionary<Guid, bool> _defendingActors = new();

    // Transient UI flash state for hit/heal feedback
    private Guid? _flashEnemyId;
    private int _flashEnemyTicks;
    private Guid? _flashAllyId;
    private int _flashAllyTicks;

    // === UI helpers: bar rendering consistent with exploration HUD ===
    private static string Bar(string label, double current, double max, int width, string color)
    {
        max = Math.Max(1.0, max);
        current = Math.Max(0.0, Math.Min(current, max));
        int filled = (int)Math.Round((current / max) * width);
        int empty = Math.Max(0, width - filled);
        string fill = new string('\u2588', Math.Max(0, filled));
        string rest = new string('\u2500', Math.Max(0, empty));
        string value = $"{current:0}/{max:0}";
        // Colorize label and numeric value to match bar color
        return $"[{color}]{label}[/]: [{color}]{fill}[/][grey]{rest}[/] [{color}]{value}[/]";
    }

    private static int BarWidth()
    {
        int cw = Math.Max(80, Console.WindowWidth);
        return Math.Clamp((cw - 10) / 4 - 10, 12, 24);
    }

    private static string Fit(string s, int width)
    {
        if (s == null) return new string(' ', width);
        return s.Length > width ? s.Substring(0, width) : s.PadRight(width);
    }

    private static string LevelTag(int level)
    {
        // Use yellow for level to make it stand out (was grey)
        return $"[yellow]Lv {level}[/]";
    }

    public ICombatSession BeginCombat(ICombatSession session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold red]\u2694\ufe0f  COMBAT INITIATED  \u2694\ufe0f[/]").RuleStyle("red"));
        AnsiConsole.WriteLine();
        
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
        AnsiConsole.WriteLine();
        return session;
    }

    public void ProcessTurn(ICombatSession session)
    {
        if (!session.IsActive) return;

        _defendingActors.Clear();
        // Build list of alive allies to plan actions for this turn
        var aliveAllies = session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0).ToList();
        if (aliveAllies.Count == 0) return;

        var plannedByIndex = new ICombatAction?[aliveAllies.Count];

        // Planning loop with back/forward navigation
        int idx = 0;
        while (idx < aliveAllies.Count)
        {
            var current = aliveAllies[idx];
            // Render clean planning screen
            AnsiConsole.Clear();
            RenderCombatScene(session, current: current, plannedActions: BuildPlannedDescriptions(plannedByIndex), animationFrame: null);
            var sel = GetPlayerAction(current, session, BuildPlannedDescriptions(plannedByIndex), allowBack: idx > 0);
            if (sel.Back)
            {
                // Go back to previous actor; clear their previous selection to re-choose
                idx = Math.Max(0, idx - 1);
                plannedByIndex[idx] = null;
                continue;
            }
            if (sel.Action != null)
            {
                plannedByIndex[idx] = sel.Action;
                idx++;
            }
            else
            {
                // No action (should not happen normally); treat as pass
                plannedByIndex[idx] = new CombatAction(CombatActionType.Pass, current, priority: -100);
                idx++;
            }
        }

        // Final confirmation screen, allow editing any actor before execution
        while (true)
        {
            AnsiConsole.Clear();
            RenderCombatScene(session, current: null, plannedActions: BuildPlannedDescriptions(plannedByIndex), animationFrame: null);
            AnsiConsole.Write(new Rule("[yellow]Confirm Planned Actions[/]").RuleStyle("yellow"));
            var confirmChoice = PromptNavigator.PromptChoice<string>(
                "Proceed with these actions?",
                new List<string> { "Confirm", "Edit Actor", "Restart Planning" },
                PromptNavigator.DefaultInputMode,
                PromptNavigator.DefaultControllerType,
                renderBackground: () =>
                {
                    RenderCombatScene(session, current: null, plannedActions: BuildPlannedDescriptions(plannedByIndex), animationFrame: null);
                });
            if (confirmChoice == "Confirm") break;
            if (confirmChoice == "Restart Planning")
            {
                Array.Fill(plannedByIndex, null);
                idx = 0;
                // Re-enter planning from the beginning
                while (idx < aliveAllies.Count)
                {
                    var current = aliveAllies[idx];
                    AnsiConsole.Clear();
                    RenderCombatScene(session, current: current, plannedActions: BuildPlannedDescriptions(plannedByIndex), animationFrame: null);
                    var sel2 = GetPlayerAction(current, session, BuildPlannedDescriptions(plannedByIndex), allowBack: idx > 0);
                    if (sel2.Back)
                    {
                        idx = Math.Max(0, idx - 1);
                        plannedByIndex[idx] = null;
                        continue;
                    }
                    plannedByIndex[idx] = sel2.Action ?? new CombatAction(CombatActionType.Pass, current, priority: -100);
                    idx++;
                }
                continue;
            }
            // Edit specific actor in-place (open their main action menu now)
            int editIndex = Math.Max(1, ParseLeadingIndex(
                PromptNavigator.PromptChoice<string>(
                    "Edit which actor?",
                    aliveAllies.Select((a, i) => $"{i+1}. {a.Name}").ToList(),
                    PromptNavigator.DefaultInputMode,
                    PromptNavigator.DefaultControllerType,
                    renderBackground: () =>
                    {
                        RenderCombatScene(session, current: null, plannedActions: BuildPlannedDescriptions(plannedByIndex), animationFrame: null);
                    }
                ))) - 1;
            editIndex = Math.Clamp(editIndex, 0, aliveAllies.Count - 1);
            var actorToEdit = aliveAllies[editIndex];
            var prev = plannedByIndex[editIndex];
            var selection = GetPlayerAction(actorToEdit, session, BuildPlannedDescriptions(plannedByIndex), allowBack: true);
            if (selection.Back)
            {
                // Keep previous selection
                plannedByIndex[editIndex] = prev;
            }
            else
            {
                plannedByIndex[editIndex] = selection.Action ?? new CombatAction(CombatActionType.Pass, actorToEdit, priority: -100);
            }
            // Loop back to confirmation to allow more edits or confirm
        }

        // Collect planned and enemy actions
        var actions = plannedByIndex.Where(a => a != null).Cast<ICombatAction>().ToList();

        // Get actions from all alive enemies (AI)
        foreach (var enemy in session.Enemies.Where(e => e.GetStat(StatType.Health).Current > 0))
        {
            var action = GetEnemyAction(enemy, session);
            if (action != null)
                actions.Add(action);
        }

        // Sort by priority (higher first), then by agility
        actions = actions.OrderByDescending(a => a.Priority)
                        .ThenByDescending(a => a.Actor.GetStat(StatType.Agility).Modified)
                        .ToList();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Turn Resolution[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        // Execute all actions
        foreach (var action in actions)
        {
            if (action.Actor.GetStat(StatType.Health).Current <= 0)
                continue;

            ExecuteAction(action, session);
            // Re-render after each action so numbers/bars update live
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
            Thread.Sleep(300);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        InputWaiter.WaitForAny(PromptNavigator.DefaultInputMode);

        // Regenerate small MP/TP per turn for all alive combatants
        RegenerateResources(session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0), mpPct: 0.03, tpPct: 0.04);
        RegenerateResources(session.Enemies.Where(a => a.GetStat(StatType.Health).Current > 0), mpPct: 0.03, tpPct: 0.04);

        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
    }

    public void EndCombat(ICombatSession session)
    {
        AnsiConsole.WriteLine();
        if (session.Outcome == CombatOutcome.Victory)
        {
            // Minimal banner; details shown in results screen outside resolver
            AnsiConsole.Write(new Rule("[bold green]\ud83c\udf89 VICTORY! \ud83c\udf89[/]").RuleStyle("green"));
        }
        else if (session.Outcome == CombatOutcome.Defeat)
        {
            AnsiConsole.Write(new Rule("[bold red]\ud83d\udc80 DEFEAT \ud83d\udc80[/]").RuleStyle("red"));
            AnsiConsole.MarkupLine("\n[red]Your party has been defeated...[/]");
        }
        // No input wait here; caller will handle next UI (e.g., Results screen)
    }

    // Render full scene: Enemies on top, Battle animation (optional) in the middle, Party at the bottom
    private void RenderCombatScene(ICombatSession session, IActor? current, List<string>? plannedActions, string? animationFrame)
    {
        // Always clear to keep layers in the right order
        AnsiConsole.Clear();

        // Top: Enemies panel
        var enemiesPanel = BuildEnemiesPanel(session);
        AnsiConsole.Write(enemiesPanel);

        // Middle: Battle display / planned actions
        if (!string.IsNullOrEmpty(animationFrame))
        {
            var animPanel = new Panel(new Markup(animationFrame))
            {
                Header = new PanelHeader(" Battle ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };
            AnsiConsole.Write(animPanel);
        }
        else if (plannedActions != null && plannedActions.Count > 0)
        {
            var planPanel = new Panel(string.Join(Environment.NewLine, plannedActions))
                .Header("[bold yellow]Planned Actions[/]").BorderColor(Color.Yellow);
            AnsiConsole.Write(planPanel);
        }
        else
        {
            // Empty spacer to visually separate
            AnsiConsole.Write(new Panel(" ") { Border = BoxBorder.None });
        }

        // Bottom: Allies panel (with optional current marker)
        var alliesPanel = BuildAlliesPanel(session, current);
        AnsiConsole.Write(alliesPanel);
    }

    // Previous helpers now delegate to the unified scene
    private void RenderCombatStatus(ICombatSession session, IActor? current, List<string>? plannedActions)
    {
        RenderCombatScene(session, current, plannedActions, animationFrame: null);
    }

    private void RenderCombatStatusWithMap(ICombatSession session, IActor? current, Dictionary<Guid, string>? plannedMap)
    {
        // Rebuild a readable planned list from map
        List<string>? planned = null;
        if (plannedMap != null)
            planned = session.Allies.Select(a => plannedMap.ContainsKey(a.Id) ? plannedMap[a.Id] : $"{a.Name}: -").ToList();
        RenderCombatScene(session, current, planned, animationFrame: null);
    }

    private Panel BuildAlliesPanel(ICombatSession session, IActor? current)
    {
        int w = BarWidth();
        var alliesSb = new System.Text.StringBuilder();
        foreach (var a in session.Allies)
        {
            bool isCurrent = current != null && a.Id == current.Id;
            bool isFlashing = _flashAllyId.HasValue && _flashAllyId.Value == a.Id && _flashAllyTicks > 0;
            string marker = isCurrent ? "[cyan]>[/] " : "   ";
            var jl = a.JobSystem.GetJobLevel(a.EffectiveClass);
            long toNext = Math.Max(0, jl.ExperienceToNextLevel - jl.Experience);
            string nameLine = isFlashing
                ? $"{marker}[bold green]{a.Name}[/]  [cyan]{Fit(a.EffectiveClass.ToString(),20)}[/]  {LevelTag(jl.Level)} [green]\u2665 HEAL[/]"
                : $"{marker}[white]{a.Name}[/]  [cyan]{Fit(a.EffectiveClass.ToString(),20)}[/]  {LevelTag(jl.Level)}";
            alliesSb.AppendLine(nameLine);
            var hp = a.GetStat(StatType.Health); var mp = a.GetStat(StatType.Mana); var tp = a.GetStat(StatType.Technical);
            alliesSb.AppendLine($"{Bar("HP", hp.Current, hp.Modified, w, "red1")}  {Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1")}  {Bar("TP", tp.Current, tp.Modified, w, "orchid")}  {Bar("EXP", jl.Experience, Math.Max(1, jl.ExperienceToNextLevel), w, "yellow3")}  [grey]ToNext:[/] [white]{toNext}[/]");
        }
        var borderColor = (_flashAllyTicks > 0 && (_flashAllyTicks % 2 == 0)) ? Color.GreenYellow : Color.Green;
        return new Panel(new Markup(alliesSb.ToString())) { Header = new PanelHeader(" Party ", Justify.Center), Border = BoxBorder.Rounded, BorderStyle = new Style(borderColor) };
    }

    private Panel BuildEnemiesPanel(ICombatSession session)
    {
        int w = BarWidth();
        var enemiesSb = new System.Text.StringBuilder();
        foreach (var e in session.Enemies)
        {
            bool isFlashing = _flashEnemyId.HasValue && _flashEnemyId.Value == e.Id && _flashEnemyTicks > 0;
            string nameLine = isFlashing
                ? $"   [bold red]{e.Name}[/]  [cyan]{Fit(e.EffectiveClass.ToString(),20)}[/]  {LevelTag(e.Level)}  [yellow]\ud83d\udca5 HIT![/]"
                : $"   [white]{e.Name}[/]  [cyan]{Fit(e.EffectiveClass.ToString(),20)}[/]  {LevelTag(e.Level)}";
            enemiesSb.AppendLine(nameLine);
            var hp = e.GetStat(StatType.Health); var mp = e.GetStat(StatType.Mana); var tp = e.GetStat(StatType.Technical);
            string hpColor = isFlashing ? "yellow1" : "red1";
            enemiesSb.AppendLine($"{Bar("HP", hp.Current, hp.Modified, w, hpColor)}  {Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1")}  {Bar("TP", tp.Current, tp.Modified, w, "orchid")} ");
        }
        var borderColor = (_flashEnemyTicks > 0 && (_flashEnemyTicks % 2 == 0)) ? Color.Yellow : Color.Red;
        return new Panel(new Markup(enemiesSb.ToString())) { Header = new PanelHeader(" Enemies ", Justify.Center), Border = BoxBorder.Rounded, BorderStyle = new Style(borderColor) };
    }

    private void WriteCurrentActorPanel(IActor actor)
    {
        // Compact header + resource bars, followed by key stats
        int w = BarWidth();
        var jl = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
        var hp = actor.GetStat(StatType.Health);
        var mp = actor.GetStat(StatType.Mana);
        var tp = actor.GetStat(StatType.Technical);

        var header = $"[bold cyan]{actor.Name}[/]'s Turn  [grey]Class[/]: [cyan]{Fit(actor.EffectiveClass.ToString(),20)}[/]  [grey]Species[/]: {actor.Species}  {LevelTag(jl.Level)}";

        var topGrid = new Grid();
        topGrid.AddColumn(new GridColumn().NoWrap());
        topGrid.AddColumn(new GridColumn().NoWrap());
        topGrid.AddColumn(new GridColumn().NoWrap());
        topGrid.AddColumn(new GridColumn().NoWrap());
        topGrid.AddRow(
            new Markup(Bar("HP", hp.Current, hp.Modified, w, "red1")),
            new Markup(Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1")),
            new Markup(Bar("TP", tp.Current, tp.Modified, w, "orchid")),
            new Markup(Bar("EXP", jl.Experience, Math.Max(1, jl.ExperienceToNextLevel), w, "yellow3"))
        );

        var infoTable = new Table().Border(TableBorder.None);
        infoTable.ShowHeaders = false;
        infoTable.AddColumn(""); infoTable.AddColumn(""); infoTable.AddColumn("");
        // Row 1: STR VIT AGI
        infoTable.AddRow(
            $"STR {actor.GetStat(StatType.Strength).Modified:F0}",
            $"VIT {actor.GetStat(StatType.Vitality).Modified:F0}",
            $"AGI {actor.GetStat(StatType.Agility).Modified:F0}");
        // Row 2: INT SPR LCK
        infoTable.AddRow(
            $"INT {actor.GetStat(StatType.Intellect).Modified:F0}",
            $"SPR {actor.GetStat(StatType.Spirit).Modified:F0}",
            $"LCK {actor.GetStat(StatType.Luck).Modified:F0}");
        // Row 3: Armor Eva Crit%
        infoTable.AddRow(
            $"Armor {actor.GetStat(StatType.Armor).Modified:F0}",
            $"Eva {actor.GetStat(StatType.Evasion).Modified:F0}",
            $"Crit% {actor.GetStat(StatType.CritChance).Modified:F0}");
        // Row 4: CritDmg AtkPwr SpPwr
        infoTable.AddRow(
            $"CritDmg {actor.GetStat(StatType.CritDamage).Modified:F0}",
            $"AtkPwr {actor.GetStat(StatType.AttackPower).Modified:F0}",
            $"SpPwr {actor.GetStat(StatType.SpellPower).Modified:F0}");
        infoTable.AddRow($"Speed {actor.GetStat(StatType.Speed).Modified:F0}", "", "");

        var panel = new Panel(new Rows(new IRenderable[] { new Markup(header), topGrid, infoTable }))
            .Header("[bold cyan]Current Actor[/]")
            .BorderColor(Color.Cyan1)
            .Padding(1, 0);
        AnsiConsole.Write(panel);
    }

    private void WriteEnemiesTable(IReadOnlyList<IActor> enemies)
    {
        int w = Math.Clamp(BarWidth(), 12, 18);
        var enemiesTable = new Table().Border(TableBorder.Rounded).Title("[bold red]Select Target[/]");
        enemiesTable.AddColumn("#"); enemiesTable.AddColumn("Name"); enemiesTable.AddColumn("Class"); enemiesTable.AddColumn("Lv"); enemiesTable.AddColumn("HP"); enemiesTable.AddColumn("MP"); enemiesTable.AddColumn("TP");
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            var hp = e.GetStat(StatType.Health); var mp = e.GetStat(StatType.Mana); var tp = e.GetStat(StatType.Technical);
            enemiesTable.AddRow(
                (i+1).ToString(),
                e.Name,
                Fit(e.EffectiveClass.ToString(), 20),
                $"{e.Level}",
                Bar("HP", hp.Current, hp.Modified, w, "red1"),
                Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1"),
                Bar("TP", tp.Current, tp.Modified, w, "orchid")
            );
        }
        AnsiConsole.Write(enemiesTable);
    }

    private static int ParseLeadingIndex(string label)
    {
        var part = label.Split('.')[0];
        return int.TryParse(part, out var n) ? n : 1;
    }

    // Dedicated helper to detect explicit back options reliably (avoid matching ability names like "Backstab")
    private static bool IsBackSelection(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return false;
        var trimmed = label.Trim();
        return string.Equals(trimmed, "Back", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("\u2190 Back", StringComparison.Ordinal);
    }

    // Simple result container for selection with back navigation
    private sealed class SelectionResult
    {
        public ICombatAction? Action { get; init; }
        public bool Back { get; init; }
    }

    private SelectionResult GetPlayerAction(IActor actor, ICombatSession session, List<string> plannedActions, bool allowBack)
    {
        while (true)
        {
            var validEnemies = session.Enemies.Where(e => e.GetStat(StatType.Health).Current > 0).ToList();
            if (validEnemies.Count == 0) return new SelectionResult { Action = null, Back = false };

            AnsiConsole.WriteLine();
            WriteCurrentActorPanel(actor);

            var choices = new List<string> { "\u2694\ufe0f Attack", "\ud83d\udee1\ufe0f Defend", "\ud83c\udfc3 Pass" };
            if (actor is IHasAbilityBook hasBook && hasBook.AbilityBook.KnownAbilities.Count > 0)
                choices.Insert(1, "\u2728 Use Ability");
            if (allowBack) choices.Add("Back");

            var choice = PromptNavigator.PromptChoice<string>(
                "Select action:",
                choices,
                PromptNavigator.DefaultInputMode,
                PromptNavigator.DefaultControllerType,
                renderBackground: () =>
                {
                    RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null);
                    WriteCurrentActorPanel(actor);
                });

            if (allowBack && IsBackSelection(choice))
            {
                return new SelectionResult { Back = true };
            }

            if (choice.Contains("Use Ability") && actor is IHasAbilityBook ab)
            {
                var known = ab.AbilityBook.KnownAbilities;
                var abilityLabels = new List<string>();
                for (int i = 0; i < known.Count; i++)
                {
                    var a = known[i];
                    var baseAbility = a as AbilityBase;
                    var abCost = baseAbility?.CostAmount ?? 0;
                    var tag = a.CostType == AbilityCostType.Mana ? $"[blue]{abCost:F0} MP[/]" : a.CostType == AbilityCostType.Technical ? $"[magenta]{abCost:F0} TP[/]" : a.CostType == AbilityCostType.Health ? $"[red]{abCost:F0} HP[/]" : "";
                    bool canAfford = a.CostType switch { AbilityCostType.Mana => actor.GetStat(StatType.Mana).Current >= abCost, AbilityCostType.Technical => actor.GetStat(StatType.Technical).Current >= abCost, AbilityCostType.Health => actor.GetStat(StatType.Health).Current > abCost, _ => true };
                    var nameWithCost = $"{a.Name}{(string.IsNullOrEmpty(tag)?"":$" ({tag})")}";
                    var label = canAfford ? $"{i+1}. {nameWithCost}" : $"{i+1}. [grey]{nameWithCost}[/] [red](insufficient)[/]";
                    abilityLabels.Add(label);
                }
                if (allowBack) abilityLabels.Add("Back");
                var chosen = PromptNavigator.PromptChoice<string>(
                    "Choose ability:", abilityLabels,
                    PromptNavigator.DefaultInputMode, PromptNavigator.DefaultControllerType,
                    renderBackground: () => { RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null); WriteCurrentActorPanel(actor); });
                if (allowBack && IsBackSelection(chosen)) continue;
                int aidx = Math.Max(1, ParseLeadingIndex(chosen)) - 1;
                var ability = known[aidx];
                var abilityBase = ability as AbilityBase;
                double selCost = abilityBase?.CostAmount ?? 0;
                bool canAffordSel = ability.CostType switch { AbilityCostType.Mana => actor.GetStat(StatType.Mana).Current >= selCost, AbilityCostType.Technical => actor.GetStat(StatType.Technical).Current >= selCost, AbilityCostType.Health => actor.GetStat(StatType.Health).Current > selCost, _ => true };
                if (!canAffordSel)
                {
                    AnsiConsole.MarkupLine($"[red]Not enough resources to use {ability.Name}. Choose a different action.[/]");
                    continue;
                }
                // Determine targets per ability target type
                List<IActor> targets;
                if (ability.TargetType == AbilityTargetType.Self)
                    targets = new List<IActor> { actor };
                else if (ability.TargetType == AbilityTargetType.SingleAlly)
                {
                    var labels = session.Allies.Select((al, i) => $"{i+1}. {al.Name}").ToList();
                    if (allowBack) labels.Add("Back");
                    var ttl = PromptNavigator.PromptChoice<string>("Target ally:", labels, PromptNavigator.DefaultInputMode, PromptNavigator.DefaultControllerType,
                        renderBackground: () => { RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null); WriteCurrentActorPanel(actor); });
                    if (allowBack && IsBackSelection(ttl)) continue;
                    int ti = Math.Max(1, ParseLeadingIndex(ttl)) - 1;
                    targets = new List<IActor> { session.Allies[ti] };
                }
                else // default SingleEnemy
                {
                    WriteEnemiesTable(validEnemies);
                    var options = validEnemies.Select((e, idx) => $"{idx+1}. {e.Name} (Lv{e.Level} HP {e.GetStat(StatType.Health).Current:F0}/{e.GetStat(StatType.Health).Modified:F0})").ToList();
                    if (allowBack) options.Add("Back");
                    var choiceLabel = PromptNavigator.PromptChoice<string>("Target:", options, PromptNavigator.DefaultInputMode, PromptNavigator.DefaultControllerType,
                        renderBackground: () => { RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null); WriteCurrentActorPanel(actor); WriteEnemiesTable(validEnemies); });
                    if (allowBack && IsBackSelection(choiceLabel))
                    {
                        // Back to action menu
                        continue;
                    }
                    int chosenIndex = Math.Max(1, ParseLeadingIndex(choiceLabel)) - 1;
                    var target = validEnemies[chosenIndex];
                    targets = new List<IActor> { target };
                }

                // Store selected ability into action custom data
                return new SelectionResult { Action = new CombatAction(CombatActionType.UseAbility, actor, targets.FirstOrDefault(), 10, ability.Name, null, ability) };
            }
            else if (choice.Contains("Attack"))
            {
                // Show enemies table for selection (no EXP)
                WriteEnemiesTable(validEnemies);

                var options = validEnemies.Select((e, idx) => $"{idx+1}. {e.Name} (Lv{e.Level} HP {e.GetStat(StatType.Health).Current:F0}/{e.GetStat(StatType.Health).Modified:F0})").ToList();
                if (allowBack) options.Add("Back");
                var choiceLabel = PromptNavigator.PromptChoice<string>(
                    "Target:",
                    options,
                    PromptNavigator.DefaultInputMode,
                    PromptNavigator.DefaultControllerType,
                    renderBackground: () =>
                    {
                        RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null);
                        WriteCurrentActorPanel(actor);
                        WriteEnemiesTable(validEnemies);
                    });
                if (allowBack && IsBackSelection(choiceLabel))
                {
                    // Back to action menu
                    continue;
                }
                int chosenIndex = Math.Max(1, ParseLeadingIndex(choiceLabel)) - 1;
                var target = validEnemies[chosenIndex];
                return new SelectionResult { Action = new CombatAction(CombatActionType.Attack, actor, target, priority: 0) };
            }
            else if (choice.Contains("Defend"))
            {
                return new SelectionResult { Action = new CombatAction(CombatActionType.Defend, actor, priority: 100) };
            }
            else if (choice.Contains("Pass"))
            {
                return new SelectionResult { Action = new CombatAction(CombatActionType.Pass, actor, priority: -100) };
            }
        }
    }

    private ICombatAction? GetEnemyAction(IActor enemy, ICombatSession session)
    {
        var validTargets = session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0).ToList();
        if (validTargets.Count == 0) return null;

        if (_rng.Next(100) < 80)
        {
            var target = validTargets[_rng.Next(validTargets.Count)];
            return new CombatAction(CombatActionType.Attack, enemy, target, priority: 0);
        }
        else
        {
            return new CombatAction(CombatActionType.Defend, enemy, priority: 100);
        }
    }

    private void ExecuteAction(ICombatAction action, ICombatSession session)
    {
        switch (action.ActionType)
        {
            case CombatActionType.Attack:
                ExecuteAttack(action, session);
                break;
            case CombatActionType.Defend:
                ExecuteDefend(action);
                break;
            case CombatActionType.UseAbility:
                ExecuteAbility(action, session);
                break;
            case CombatActionType.Pass:
                AnsiConsole.MarkupLine($"[grey]{action.Actor.Name} passes their turn.[/]");
                break;
        }
    }

    private void ExecuteAbility(ICombatAction action, ICombatSession session)
    {
        var ability = (action as CombatAction)?.CustomData as IAbility;
        if (ability == null) { AnsiConsole.MarkupLine("[grey]But nothing happened...[/]"); return; }
        var user = action.Actor;
        // Cost
        double cost = (ability as AbilityBase)?.CostAmount ?? 0;
        bool paid = true;
        if (ability.CostType == AbilityCostType.Mana)
            paid = (user as ActorBase)?.TrySpend(StatType.Mana, cost) ?? false;
        else if (ability.CostType == AbilityCostType.Technical)
            paid = (user as ActorBase)?.TrySpend(StatType.Technical, cost) ?? false;
        else if (ability.CostType == AbilityCostType.Health)
            paid = (user as ActorBase)?.TrySpend(StatType.Health, cost) ?? false;
        if (!paid) { AnsiConsole.MarkupLine($"[red]{user.Name} lacks resources to use {ability.Name}![/]"); return; }

        // Simple effect logic: damage or heal based on category
        var targets = action.Target != null ? new List<IActor> { action.Target } : new List<IActor> { user };
        if (ability.TargetType == AbilityTargetType.SingleAlly && action.Target == null)
            targets = new List<IActor> { user };

        if (ability.Category == AbilityCategory.Damage || ability.Name.Contains("Fire") || ability.Name.Contains("Strike") || ability.Name.Contains("Backstab") )
        {
            foreach (var t in targets)
            {
                double scale = ability.CostType == AbilityCostType.Mana ? user.GetStat(StatType.SpellPower).Modified : user.GetStat(StatType.AttackPower).Modified;
                double baseAmount = 15 + 0.5 * scale;
                var packet = new DamagePacket(user.Id, new[] { new DamageComponent(DamageType.Physical, baseAmount) });
                if (ability.CostType == AbilityCostType.Mana) packet = new DamagePacket(user.Id, new[] { new DamageComponent(DamageType.Arcane, baseAmount) });
                var remain = t.TakeDamage(packet);
                var color = session.Allies.Contains(t) ? "green" : "red";
                // Simple sparkle animation for abilities
                ShowAbilityAnimation(session, user, t, baseAmount);
                AnsiConsole.MarkupLine($"[cyan]{user.Name}[/] uses [yellow]{ability.Name}[/] on [{color}]{t.Name}[/] for [yellow]{baseAmount:F0}[/] damage!");
                if (remain <= 0) AnsiConsole.MarkupLine($"[bold red]\ud83d\udc80 {t.Name} is defeated!\ud83d\udc80[/]");
            }
        }
        else // treat as heal/support
        {
            foreach (var t in targets)
            {
                double scale = user.GetStat(StatType.Spirit).Modified + 0.5 * user.GetStat(StatType.SpellPower).Modified;
                int amount = (int)(10 + 0.4 * scale);
                (t as ActorBase)?.Heal(amount);
                ShowHealAnimation(session, user, t, amount);
                AnsiConsole.MarkupLine($"[cyan]{user.Name}[/] uses [yellow]{ability.Name}[/] to heal [green]{t.Name}[/] for [green]{amount}[/] HP!");
            }
        }
    }

    private void ExecuteAttack(ICombatAction action, ICombatSession session)
    {
        if (action.Target == null) return;

        var attacker = action.Actor;
        var defender = action.Target;

        // Skip if defender already downed by an earlier action this turn
        if (defender.GetStat(StatType.Health).Current <= 0)
        {
            AnsiConsole.MarkupLine($"[grey]{attacker.Name} tries to attack {defender.Name}, but the target is already down.[/]");
            return;
        }
        
        // Base damage uses AttackPower, Strength, and weapon roll
        double attackPower = attacker.GetStat(StatType.AttackPower).Modified;
        double strength = attacker.GetStat(StatType.Strength).Modified;
        double baseDamage = Math.Max(1.0, attackPower + 0.5 * strength);
        
        if (attacker is ActorBase actorBase)
        {
            var weapon = actorBase.GetEquipped(EquipmentSlot.MainHand) as IWeapon;
            if (weapon != null)
            {
                var weaponDamage = _rng.NextDouble() * (weapon.MaxDamage - weapon.MinDamage) + weapon.MinDamage;
                baseDamage += weaponDamage;
            }
        }
        
        // Mitigation uses Armor and Vitality in a diminishing-returns formula
        double armor = defender.GetStat(StatType.Armor).Modified;
        double vitality = defender.GetStat(StatType.Vitality).Modified;
        double mitigation = 100.0 / (100.0 + Math.Max(0.0, armor + 0.5 * vitality)); // scales down damage
        double finalDamage = Math.Max(1.0, baseDamage * mitigation);
        
        if (_defendingActors.ContainsKey(defender.Id))
        {
            finalDamage *= 0.5;
            AnsiConsole.MarkupLine($"[cyan]{defender.Name} is defending![/]");
        }
        
        // Critical based on CritChance/CritDamage stats
        double critChance = Math.Clamp(attacker.GetStat(StatType.CritChance).Modified, 0, 100);
        bool isCrit = _rng.NextDouble() * 100.0 < critChance;
        if (isCrit)
        {
            double critMult = 1.0 + Math.Max(0.0, attacker.GetStat(StatType.CritDamage).Modified) / 100.0; // e.g. 50 => 1.5x
            critMult = Math.Clamp(critMult, 1.25, 3.0);
            finalDamage *= critMult;
        }
        
        // Show a simple left-right arrow animation before applying damage
        ShowAttackAnimation(session, attacker, defender, isCrit, finalDamage);
        
        var packet = new DamagePacket(attacker.Id, new[] { new DamageComponent(DamageType.Physical, finalDamage) }, isCritical: isCrit);
        var remaining = defender.TakeDamage(packet);
        
        var attackerColor = session.Allies.Contains(attacker) ? "green" : "red";
        var defenderColor = session.Allies.Contains(defender) ? "green" : "red";
        
        if (isCrit)
            AnsiConsole.MarkupLine("[bold yellow]\ud83d\udca5 CRITICAL HIT! \ud83d\udca5[/]");

        AnsiConsole.MarkupLine($"[{attackerColor}]{attacker.Name}[/] attacks [{defenderColor}]{defender.Name}[/] for [yellow]{finalDamage:F0}[/] damage!");
        
        if (remaining <= 0)
        {
            AnsiConsole.MarkupLine($"[bold red]\ud83d\udc80 {defender.Name} has been defeated! \ud83d\udc80[/]");
        }
    }

    private void ExecuteDefend(ICombatAction action)
    {
        _defendingActors[action.Actor.Id] = true;
        AnsiConsole.MarkupLine($"[cyan]{action.Actor.Name} takes a defensive stance![/]");
    }

    private long CalculateExperienceReward(int enemyLevel)
    {
        return (long)(50 * Math.Pow(enemyLevel, 1.5));
    }

    private List<string> BuildPlannedDescriptions(ICombatAction?[] planned)
    {
        var list = new List<string>();
        foreach (var a in planned)
        {
            if (a != null)
            {
                string who = a.Actor?.Name ?? "?";
                switch (a.ActionType)
                {
                    case CombatActionType.Attack:
                        list.Add($"{who}: Attack {(a.Target != null ? a.Target.Name : "-")}");
                        break;
                    case CombatActionType.Defend:
                        list.Add($"{who}: Defend");
                        break;
                    case CombatActionType.Pass:
                        list.Add($"{who}: Pass");
                        break;
                    case CombatActionType.UseAbility:
                        if (a is CombatAction ca && ca.CustomData is IAbility abil)
                            list.Add($"{who}: {abil.Name}{(a.Target != null ? $" -> {a.Target.Name}" : "")}");
                        else
                            list.Add($"{who}: Use Ability");
                        break;
                    default:
                        list.Add($"{who}: Action");
                        break;
                }
            }
        }
        return list;
    }

    // === Animations ===
    private void ShowAttackAnimation(ICombatSession session, IActor attacker, IActor defender, bool isCrit, double dmg)
    {
        bool attackerIsAlly = session.Allies.Any(a => a.Id == attacker.Id);
        bool defenderIsEnemy = session.Enemies.Any(e => e.Id == defender.Id);
        var attackerColor = attackerIsAlly ? "green" : "red";
        var defenderColor = attackerIsAlly ? "red" : "green";

        // 1) Preparation
        string prep = $"[bold {attackerColor}]{attacker.Name}[/] gathers themselves...";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: prep);
        Thread.Sleep(350);

        // 2) Strike and set flash on the correct panel for defender
        string strike = $"[bold]{attacker.Name}[/] [yellow]\u27A1\ufe0f[/] [{defenderColor}]{defender.Name}[/] for [yellow]{dmg:F0}[/] damage!";
        if (defenderIsEnemy) { _flashEnemyId = defender.Id; _flashEnemyTicks = isCrit ? 6 : 4; }
        else { _flashAllyId = defender.Id; _flashAllyTicks = isCrit ? 6 : 4; }
        int loops = isCrit ? 6 : 4;
        for (int i = 0; i < loops; i++)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: strike);
            Thread.Sleep(isCrit ? 140 : 110);
            if (defenderIsEnemy) _flashEnemyTicks = Math.Max(0, _flashEnemyTicks - 1); else _flashAllyTicks = Math.Max(0, _flashAllyTicks - 1);
        }

        // 3) Impact emphasis
        string impact = isCrit ? "[bold yellow]\ud83d\udca5 CRITICAL STRIKE! \ud83d\udca5[/]" : "[yellow]\ud83d\udca5 IMPACT! \ud83d\udca5[/]";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: impact);
        Thread.Sleep(isCrit ? 520 : 380);

        // 4) Aftermath snapshot so bars update nicely
        string aftermath = $"[{attackerColor}]{attacker.Name}[/] -> [{defenderColor}]{defender.Name}[/]  [yellow]{dmg:F0}[/]";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: aftermath);
        Thread.Sleep(240);

        // Clear flash state
        if (defenderIsEnemy) { _flashEnemyId = null; _flashEnemyTicks = 0; } else { _flashAllyId = null; _flashAllyTicks = 0; }
    }

    private void ShowAbilityAnimation(ICombatSession session, IActor user, IActor target, double amount)
    {
        bool userIsAlly = session.Allies.Any(a => a.Id == user.Id);
        var userColor = userIsAlly ? "green" : "red";
        var targetColor = userIsAlly ? "red" : "green";

        string cast = "[yellow]\u2728[/] [cyan]" + user.Name + "[/] channels power...";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: cast);
        Thread.Sleep(340);

        // Effect frame + brief flash on target if damaging
        string effect = $"[cyan]{user.Name}[/] unleashes power at [{targetColor}]{target.Name}[/]!";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: effect);
        Thread.Sleep(280);

        // If the target is an enemy from allies' ability, flash enemy panel; else flash allies panel
        if (userIsAlly && session.Enemies.Any(e => e.Id == target.Id)) { _flashEnemyId = target.Id; _flashEnemyTicks = 4; }
        else if (!userIsAlly && session.Allies.Any(a => a.Id == target.Id)) { _flashAllyId = target.Id; _flashAllyTicks = 4; }

        string resolve = $"[yellow]\u2728[/] [{targetColor}]{target.Name}[/] affected for [yellow]{amount:F0}[/]!";
        for (int i = 0; i < 4; i++)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: resolve);
            Thread.Sleep(120);
            if (_flashEnemyTicks > 0) _flashEnemyTicks--; if (_flashAllyTicks > 0) _flashAllyTicks--;
        }
        _flashEnemyId = null; _flashEnemyTicks = 0; _flashAllyId = null; _flashAllyTicks = 0;
    }

    private void ShowHealAnimation(ICombatSession session, IActor user, IActor target, int amount)
    {
        // Pleasant heal sequence with green pulse on allies panel
        string prepare = $"[green]\u2665[/] [cyan]{user.Name}[/] begins to heal...";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: prepare);
        Thread.Sleep(320);

        _flashAllyId = target.Id; _flashAllyTicks = 4;
        string healLine = $"[cyan]{user.Name}[/] heals [green]{target.Name}[/] for [green]{amount}[/] HP!";
        for (int i = 0; i < 4; i++)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: healLine);
            Thread.Sleep(140);
            _flashAllyTicks = Math.Max(0, _flashAllyTicks - 1);
        }

        string glow = $"[green]\u2665\u2665\u2665[/] [{target.Name}] feels renewed";
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: glow);
        Thread.Sleep(300);

        _flashAllyId = null; _flashAllyTicks = 0;
    }

    private void RegenerateResources(IEnumerable<IActor> actors, double mpPct, double tpPct)
    {
        foreach (var a in actors)
        {
            if (a is not ActorBase ab) continue;
            if (ab.TryGetStat(StatType.Mana, out var mp))
            {
                var sv = (Fub.Implementations.Stats.StatValue)mp;
                var delta = Math.Max(1.0, sv.Modified * mpPct);
                sv.ApplyDelta(delta);
            }
            if (ab.TryGetStat(StatType.Technical, out var tp))
            {
                var sv = (Fub.Implementations.Stats.StatValue)tp;
                var delta = Math.Max(1.0, sv.Modified * tpPct);
                sv.ApplyDelta(delta);
            }
        }
    }
}
