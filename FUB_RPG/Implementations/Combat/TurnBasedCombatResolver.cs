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
using Fub.Interfaces.Game;

namespace Fub.Implementations.Combat;

public sealed class TurnBasedCombatResolver : ICombatResolver
{
    private readonly System.Random _rng = new();
    private readonly Dictionary<Guid, bool> _defendingActors = new();
    private readonly IGameState _state;

    public TurnBasedCombatResolver(IGameState state)
    {
        _state = state;
    }

    // Transient UI flash state for hit/heal feedback
    private Guid? _flashEnemyId;
    private int _flashEnemyTicks;
    private Guid? _flashAllyId;
    private int _flashAllyTicks;

    // New: bottom battle message log
    private readonly List<string> _messageLog = new();
    private int _logScrollOffset = 0; // 0 means show newest 5; positive values scroll to older

    private void AddMessage(string markup)
    {
        _messageLog.Add(markup);
        // Snap to latest on new message
        _logScrollOffset = 0;
    }

    private const int LogWindowSize = 5;

    // Dedicated scroll loop to navigate the bottom log with arrow keys
    private void ScrollLogLoop(ICombatSession session)
    {
        if (_messageLog.Count <= LogWindowSize)
        {
            return; // nothing to scroll
        }
        while (true)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
            var action = InputManager.ReadNextAction(PromptNavigator.DefaultInputMode);
            if (action == InputAction.MoveUp)
             {
                 int maxOffset = Math.Max(0, _messageLog.Count - LogWindowSize);
                 _logScrollOffset = Math.Min(maxOffset, _logScrollOffset + 1);
             }
            else if (action == InputAction.MoveDown)
             {
                 _logScrollOffset = Math.Max(0, _logScrollOffset - 1);
             }
            else if (action == InputAction.MoveLeft)
             {
                 int maxOffset = Math.Max(0, _messageLog.Count - LogWindowSize);
                 _logScrollOffset = Math.Min(maxOffset, _logScrollOffset + LogWindowSize);
             }
            else if (action == InputAction.MoveRight)
             {
                 _logScrollOffset = Math.Max(0, _logScrollOffset - LogWindowSize);
             }
            else if (action == InputAction.Inventory) // map Home to Inventory for lack of key (quick jump oldest)
             {
                 _logScrollOffset = Math.Max(0, _messageLog.Count - LogWindowSize);
             }
            else if (action == InputAction.Map) // map End to Map (quick jump newest)
             {
                 _logScrollOffset = 0;
             }
            else if (action == InputAction.Interact || action == InputAction.Menu || action == InputAction.Log)
             {
                 break; // exit scroll mode
             }
        }
    }

    private int GetMessageDisplayMs()
    {
        return _state.CombatSpeed switch
        {

            CombatSpeed.Slow => 3000,
            CombatSpeed.Normal => 1000,
            CombatSpeed.Fast => 500,
            CombatSpeed.Instant => 100,
            _ => 1000
        };
    }

    private void SleepFixed(int ms)
    {
        if (ms > 0) Thread.Sleep(ms);
    }

    // Non-blocking keyboard scroll while displaying an action message; only consumes scroll keys
    private void PollKeyboardScrollNonblocking()
    {
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(true);
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                int maxOffset = Math.Max(0, _messageLog.Count - LogWindowSize);
                _logScrollOffset = Math.Min(maxOffset, _logScrollOffset + 1);
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                _logScrollOffset = Math.Max(0, _logScrollOffset - 1);
            }
            else if (keyInfo.Key == ConsoleKey.PageUp)
            {
                int maxOffset = Math.Max(0, _messageLog.Count - LogWindowSize);
                _logScrollOffset = Math.Min(maxOffset, _logScrollOffset + LogWindowSize);
            }
            else if (keyInfo.Key == ConsoleKey.PageDown)
            {
                _logScrollOffset = Math.Max(0, _logScrollOffset - LogWindowSize);
            }
            else if (keyInfo.Key == ConsoleKey.Home)
            {
                _logScrollOffset = Math.Max(0, _messageLog.Count - LogWindowSize);
            }
            else if (keyInfo.Key == ConsoleKey.End)
            {
                _logScrollOffset = 0;
            }
            else
            {
                // Put back non-scroll keys by ignoring them (we can't truly un-read, but we only enter here during action display)
            }
        }
    }

    private void ShowMessage(ICombatSession session, string markup)
    {
        AddMessage(markup);
        // Re-render scene with latest message at the bottom
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
        int total = GetMessageDisplayMs();
        int elapsed = 0;
        const int slice = 50;
        while (elapsed < total)
        {
            PollKeyboardScrollNonblocking();
            Thread.Sleep(slice);
            elapsed += slice;
        }
    }

    // Guarantees at least 3 seconds display per action message regardless of speed
    private void ShowActionMessage(ICombatSession session, string markup)
    {
        AddMessage(markup);
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
        int total = GetMessageDisplayMs();
        int elapsed = 0;
        const int slice = 50;
        while (elapsed < total)
        {
            PollKeyboardScrollNonblocking();
            Thread.Sleep(slice);
            elapsed += slice;
        }
    }

    private void ShowPrompt(ICombatSession session, string markup)
    {
        AddMessage(markup);
        RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
        InputWaiter.WaitForAny(PromptNavigator.DefaultInputMode);
    }

    private void ToggleLog()
    {
        _state.SetShowCombatLog(!_state.ShowCombatLog);
    }

    // Helper: scale delays based on combat speed (for animations only)
    private int ScaleDelay(int baseMs)
    {
        return _state.CombatSpeed switch
        {
            CombatSpeed.Instant => 0,
            CombatSpeed.Fast => (int)Math.Round(baseMs * 0.5),
            CombatSpeed.Slow => (int)Math.Round(baseMs * 1.5),
            _ => baseMs
        };
    }
    private void Sleep(int baseMs)
    {
        int ms = ScaleDelay(baseMs);
        if (ms > 0) Thread.Sleep(ms);
    }

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

    private static int ClassDisplayWidth()
    {
        int cw = Math.Max(80, Console.WindowWidth);
        if (cw >= 160) return 30;
        if (cw >= 140) return 28;
        if (cw >= 120) return 24;
        return 20;
    }

    private static (int nameW,int speciesW,int classW,int lvlW,int actionW) ColumnWidths()
    {
        int cw = Math.Max(80, Console.WindowWidth);
        // Baselines
        int nameW = 16, speciesW = 12, classW = 18, lvlW = 6, actionW = 18;
        // Overhead: marker(2) + 2 spaces between 5 columns = 2 + 8 = 10
        int used = nameW + speciesW + classW + lvlW + actionW + 10;
        int extra = Math.Max(0, cw - used);
        // Favor action and class first, then name, then species
        int addAction = Math.Min(extra, 20); extra -= addAction; actionW += addAction;
        int addClass = Math.Min(extra, 10); extra -= addClass; classW += addClass;
        int addName = Math.Min(extra, 8);  extra -= addName;  nameW += addName;
        int addSpecies = Math.Min(extra, 6); extra -= addSpecies; speciesW += addSpecies;
        return (nameW, speciesW, classW, lvlW, Math.Max(12, actionW));
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
        // Reset transient visual state and battle log for new encounter
        _flashEnemyId = null; _flashEnemyTicks = 0; _flashAllyId = null; _flashAllyTicks = 0;
        _messageLog.Clear();
        _logScrollOffset = 0;

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
                new List<string> { "Confirm", "Edit Actor", "Restart Planning", _state.ShowCombatLog ? "Hide Log" : "Show Log" }
                    .Concat(_state.ShowCombatLog && _messageLog.Count > LogWindowSize ? new[]{"Scroll Log (↑/↓)"} : Array.Empty<string>())
                    .ToList(),
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
            if (confirmChoice == "Edit Actor")
            {
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
            if (confirmChoice == "Hide Log" || confirmChoice == "Show Log")
            {
                ToggleLog();
                // Loop to re-render with updated visibility
                continue;
            }
            if (confirmChoice == "Scroll Log (↑/↓)")
            {
                ScrollLogLoop(session);
                continue;
            }
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
            Sleep(300);
        }

        AnsiConsole.WriteLine();
        // Move the continue prompt to the bottom message area
        ShowPrompt(session, "[grey]Press any key to continue...[/]");

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
            // Also add to bottom log
            AddMessage("[red]Your party has been defeated...[/]");
        }
        // No input wait here; caller will handle next UI (e.g., Results screen)
    }

    // Render full scene: Enemies on top, Battle animation (optional) in the middle, Party at the bottom, Message log at very bottom
    private void RenderCombatScene(ICombatSession session, IActor? current, List<string>? plannedActions, string? animationFrame)
    {
        // Always clear to keep layers in the right order
        AnsiConsole.Clear();

        // Create quick lookup for planned actions per ally name
        Dictionary<string, string>? plannedByName = null;
        if (plannedActions != null)
        {
            plannedByName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in plannedActions)
            {
                var idx = p.IndexOf(':');
                if (idx > 0)
                {
                    var who = p.Substring(0, idx).Trim();
                    var act = p.Substring(idx + 1).Trim();
                    plannedByName[who] = act;
                }
            }
        }

        // Top: Enemies panel
        var enemiesPanel = BuildEnemiesPanel(session);
        AnsiConsole.Write(enemiesPanel);

        // Middle: optional animation area; if none, don't add spacer to keep layout tight
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

        // Party panel with inline actions
        var alliesPanel = BuildAlliesPanel(session, current, plannedByName);
        AnsiConsole.Write(alliesPanel);

        // Messages panel (very bottom)
        if (_state.ShowCombatLog)
        {
            var msgPanel = BuildMessagesPanel();
            AnsiConsole.Write(msgPanel);
        }
    }

    private Panel BuildAlliesPanel(ICombatSession session, IActor? current, Dictionary<string,string>? plannedByName)
    {
        int w = BarWidth();
        var widths = ColumnWidths();
        var alliesSb = new System.Text.StringBuilder();
        foreach (var a in session.Allies)
        {
            bool isCurrent = current != null && a.Id == current.Id;
            bool isFlashing = _flashAllyId.HasValue && _flashAllyId.Value == a.Id && _flashAllyTicks > 0;
            string marker = isCurrent ? "[cyan]>[/] " : "  ";
            var jl = a.JobSystem.GetJobLevel(a.EffectiveClass);
            long toNext = Math.Max(0, jl.ExperienceToNextLevel - jl.Experience);

            string name = Fit(a.Name, widths.nameW);
            string species = Fit(a.Species.ToString(), widths.speciesW);
            string cls = Fit(a.EffectiveClass.ToString(), widths.classW);
            string lvl = Fit($"Lv {jl.Level}", widths.lvlW);
            string actionText = plannedByName != null && plannedByName.TryGetValue(a.Name, out var act) ? act : "-";
            string action = Fit(actionText, widths.actionW);

            string line1 = isFlashing
                ? $"{marker}[bold white]{name}[/]  [green]{species}[/]  [cyan]{cls}[/]  [yellow]{lvl}[/]  [grey]Action:[/] [white]{action}[/]"
                : $"{marker}[white]{name}[/]  [green]{species}[/]  [cyan]{cls}[/]  [yellow]{lvl}[/]  [grey]Action:[/] [white]{action}[/]";
            alliesSb.AppendLine(line1);

            var hp = a.GetStat(StatType.Health); var mp = a.GetStat(StatType.Mana); var tp = a.GetStat(StatType.Technical);
            alliesSb.AppendLine($"{Bar("HP", hp.Current, hp.Modified, w, "red1")}  {Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1")}  {Bar("TP", tp.Current, tp.Modified, w, "orchid")}  {Bar("EXP", jl.Experience, Math.Max(1, jl.ExperienceToNextLevel), w, "yellow3")}  [grey]ToNext:[/] [white]{toNext}[/]");
        }
        var borderColor = (_flashAllyTicks > 0 && (_flashAllyTicks % 2 == 0)) ? Color.GreenYellow : Color.Green;
        return new Panel(new Markup(alliesSb.ToString())) { Header = new PanelHeader(" Party ", Justify.Center), Border = BoxBorder.Rounded, BorderStyle = new Style(borderColor) };
    }

    private Panel BuildEnemiesPanel(ICombatSession session)
    {
        int w = BarWidth();
        var widths = ColumnWidths();
        var enemiesSb = new System.Text.StringBuilder();
        foreach (var e in session.Enemies)
        {
            bool isFlashing = _flashEnemyId.HasValue && _flashEnemyId.Value == e.Id && _flashEnemyTicks > 0;
            string name = Fit(e.Name, widths.nameW);
            string species = Fit(e.Species.ToString(), widths.speciesW);
            string cls = Fit(e.EffectiveClass.ToString(), widths.classW);
            string lvl = Fit($"Lv {e.Level}", widths.lvlW);
            string line1 = isFlashing
                ? $"  [bold white]{name}[/]  [green]{species}[/]  [cyan]{cls}[/]  [yellow]{lvl}[/]"
                : $"  [white]{name}[/]  [green]{species}[/]  [cyan]{cls}[/]  [yellow]{lvl}[/]";
            enemiesSb.AppendLine(line1);
            var hp = e.GetStat(StatType.Health); var mp = e.GetStat(StatType.Mana); var tp = e.GetStat(StatType.Technical);
            string hpColor = isFlashing ? "yellow1" : "red1";
            enemiesSb.AppendLine($"{Bar("HP", hp.Current, hp.Modified, w, hpColor)}  {Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1")}  {Bar("TP", tp.Current, tp.Modified, w, "orchid")} ");
        }
        var borderColor = (_flashEnemyTicks > 0 && (_flashEnemyTicks % 2 == 0)) ? Color.Yellow : Color.Red;
        return new Panel(new Markup(enemiesSb.ToString())) { Header = new PanelHeader(" Enemies ", Justify.Center), Border = BoxBorder.Rounded, BorderStyle = new Style(borderColor) };
    }

    private Panel BuildMessagesPanel()
    {
        // Determine the 5-line window based on current scroll offset
        var total = _messageLog.Count;
        var sb = new System.Text.StringBuilder();
        if (total == 0)
        {
            sb.AppendLine("[grey]No battle messages yet.[/]");
        }
        else
        {
            int window = LogWindowSize;
            int maxOffset = Math.Max(0, total - window);
            int offset = Math.Clamp(_logScrollOffset, 0, maxOffset);
            int start = Math.Max(0, total - window - offset);
            int end = Math.Min(total, start + window);
            for (int i = start; i < end; i++)
            {
                sb.AppendLine(_messageLog[i]);
            }
        }
        var headerText = _messageLog.Count <= LogWindowSize ? " Messages " : $" Messages  [grey](↑/↓ scroll) [/]{(_messageLog.Count - _logScrollOffset)}/{_messageLog.Count} ";
        return new Panel(new Markup(sb.ToString()))
        {
            Header = new PanelHeader(headerText, Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };
    }

    private void WriteCurrentActorPanel(IActor actor)
    {
        // Compact header + resource bars, followed by a small two-row stats table
        int w = BarWidth();
        var jl = actor.JobSystem.GetJobLevel(actor.EffectiveClass);
        var hp = actor.GetStat(StatType.Health);
        var mp = actor.GetStat(StatType.Mana);
        var tp = actor.GetStat(StatType.Technical);

        var header = $"[bold cyan]{actor.Name}[/]'s Turn  [grey]Class[/]: [cyan]{Fit(actor.EffectiveClass.ToString(),ClassDisplayWidth())}[/]  [grey]Species[/]: {actor.Species}  {LevelTag(jl.Level)}";

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

        // Build compact stats table (two rows) to keep panel small
        var info = new Table().Border(TableBorder.None);
        info.ShowHeaders = false;
        // 7 columns for concise tokens
        for (int i = 0; i < 7; i++) info.AddColumn("");

        string P(string lbl, double val, bool percent = false) => $"[grey]{lbl}[/] [bold white]{(percent ? val.ToString("0") + "%" : val.ToString("0"))}[/]";
        // Primary stats row (7 tokens)
        info.AddRow(
            P("STR", actor.GetStat(StatType.Strength).Modified),
            P("VIT", actor.GetStat(StatType.Vitality).Modified),
            P("AGI", actor.GetStat(StatType.Agility).Modified),
            P("INT", actor.GetStat(StatType.Intellect).Modified),
            P("SPR", actor.GetStat(StatType.Spirit).Modified),
            P("LCK", actor.GetStat(StatType.Luck).Modified),
            P("SPD", actor.GetStat(StatType.Speed).Modified)
        );

        // Defense/Offense + compressed resistances row
        var armor = actor.GetStat(StatType.Armor).Modified;
        var eva = actor.GetStat(StatType.Evasion).Modified;
        var crit = actor.GetStat(StatType.CritChance).Modified;
        var cdmg = actor.GetStat(StatType.CritDamage).Modified;
        var atk = actor.GetStat(StatType.AttackPower).Modified;
        var spw = actor.GetStat(StatType.SpellPower).Modified;
        int rF = (int)Math.Round(actor.GetStat(StatType.FireResist).Modified);
        int rC = (int)Math.Round(actor.GetStat(StatType.ColdResist).Modified);
        int rL = (int)Math.Round(actor.GetStat(StatType.LightningResist).Modified);
        int rP = (int)Math.Round(actor.GetStat(StatType.PoisonResist).Modified);
        int rA = (int)Math.Round(actor.GetStat(StatType.ArcaneResist).Modified);
        int rS = (int)Math.Round(actor.GetStat(StatType.ShadowResist).Modified);
        int rH = (int)Math.Round(actor.GetStat(StatType.HolyResist).Modified);
        string res = $"[grey]Res[/] [bold white]F{rF}% C{rC}% L{rL}% P{rP}% A{rA}% S{rS}% H{rH}%[/]";
        info.AddRow(
            P("Armor", armor),
            P("Eva", eva, percent: true),
            P("Crit", crit, percent: true),
            P("CritDmg", cdmg, percent: true),
            P("AtkPwr", atk),
            P("SpPwr", spw),
            res
        );

        var panel = new Panel(new Rows(new IRenderable[] { new Markup(header), topGrid, info }))
            .Header("[bold cyan]Current Actor[/]")
            .BorderColor(Color.Cyan1)
            .Padding(1, 0);
        AnsiConsole.Write(panel);
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
            // Add toggle and scroll options for log visibility
            choices.Add(_state.ShowCombatLog ? "Hide Log" : "Show Log");
            if (_state.ShowCombatLog && _messageLog.Count > LogWindowSize)
                choices.Add("Scroll Log (↑/↓)");
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

            if (choice == "Hide Log" || choice == "Show Log")
            {
                ToggleLog();
                // Re-render and continue selection
                RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null);
                continue;
            }
            if (choice == "Scroll Log (↑/↓)")
            {
                ScrollLogLoop(session);
                RenderCombatScene(session, current: actor, plannedActions: plannedActions, animationFrame: null);
                continue;
            }

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
                    ShowMessage(session, $"[red]Not enough resources to use {ability.Name}. Choose a different action.[/]");
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
                ExecuteDefend(action, session);
                break;
            case CombatActionType.UseAbility:
                ExecuteAbility(action, session);
                break;
            case CombatActionType.Pass:
                ShowActionMessage(session, $"[grey]{action.Actor.Name} passes their turn.[/]");
                break;
        }
    }

    private void ExecuteAbility(ICombatAction action, ICombatSession session)
    {
        var ability = (action as CombatAction)?.CustomData as IAbility;
        if (ability == null) { ShowActionMessage(session, "[grey]But nothing happened...[/]"); return; }
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
        if (!paid) { ShowActionMessage(session, $"[red]{user.Name} lacks resources to use {ability.Name}![/]"); return; }

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
                ShowActionMessage(session, $"[cyan]{user.Name}[/] uses [yellow]{ability.Name}[/] on [{color}]{t.Name}[/] for [yellow]{baseAmount:F0}[/] damage!");
                if (remain <= 0) ShowActionMessage(session, $"[bold red]\ud83d\udc80 {t.Name} is defeated!\ud83d\udc80[/]");
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
                ShowActionMessage(session, $"[cyan]{user.Name}[/] uses [yellow]{ability.Name}[/] to heal [green]{t.Name}[/] for [green]{amount}[/] HP!");
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
            ShowMessage(session, $"[grey]{attacker.Name} tries to attack {defender.Name}, but the target is already down.[/]");
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
            ShowActionMessage(session, $"[cyan]{defender.Name} is defending![/]");
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
        
        // Build a single-line message, embedding CRITICAL and larger-styled number
        string dmgText = isCrit
            ? $"[bold yellow]{finalDamage:F0}![/]"
            : $"[yellow]{finalDamage:F0}[/]";
        string critTag = isCrit ? " [bold yellow]CRITICAL![/]" : string.Empty;
        var attackerColor = session.Allies.Contains(attacker) ? "green" : "red";
        var defenderColor = session.Allies.Contains(defender) ? "green" : "red";
        
        ShowActionMessage(session, $"[{attackerColor}]{attacker.Name}[/] attacks [{defenderColor}]{defender.Name}[/] for {dmgText} damage!{critTag}");
 
        var packet = new DamagePacket(attacker.Id, new[] { new DamageComponent(DamageType.Physical, finalDamage) }, isCritical: isCrit);
        var remaining = defender.TakeDamage(packet);
        

        
        if (remaining <= 0)
        {
            ShowActionMessage(session, $"[bold red]\ud83d\udc80 {defender.Name} has been defeated! \ud83d\udc80[/]");
        }
    }

    private void ExecuteDefend(ICombatAction action, ICombatSession session)
    {
        _defendingActors[action.Actor.Id] = true;
        ShowActionMessage(session, $"[cyan]{action.Actor.Name} takes a defensive stance![/]");
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
        // Flash-only: briefly flash the defender; no center icons or text frames
        bool defenderIsEnemy = session.Enemies.Any(e => e.Id == defender.Id);
        if (defenderIsEnemy) { _flashEnemyId = defender.Id; _flashEnemyTicks = isCrit ? 6 : 4; }
        else { _flashAllyId = defender.Id; _flashAllyTicks = isCrit ? 6 : 4; }
        int loops = isCrit ? 6 : 4;
        for (int i = 0; i < loops; i++)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
            Sleep(isCrit ? 120 : 90);
            if (defenderIsEnemy) _flashEnemyTicks = Math.Max(0, _flashEnemyTicks - 1); else _flashAllyTicks = Math.Max(0, _flashAllyTicks - 1);
        }
        if (defenderIsEnemy) { _flashEnemyId = null; _flashEnemyTicks = 0; } else { _flashAllyId = null; _flashAllyTicks = 0; }
    }

    private void ShowAbilityAnimation(ICombatSession session, IActor user, IActor target, double amount)
    {
        // Flash-only: set flash flags and render a few quick frames; no center icons
        bool targetIsEnemy = session.Enemies.Any(e => e.Id == target.Id);
        if (targetIsEnemy) { _flashEnemyId = target.Id; _flashEnemyTicks = 4; }
        else { _flashAllyId = target.Id; _flashAllyTicks = 4; }
        int loops = 4;
        for (int i = 0; i < loops; i++)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
            Sleep(100);
            if (targetIsEnemy) _flashEnemyTicks = Math.Max(0, _flashEnemyTicks - 1);
            else _flashAllyTicks = Math.Max(0, _flashAllyTicks - 1);
        }
        _flashEnemyId = null; _flashEnemyTicks = 0; _flashAllyId = null; _flashAllyTicks = 0;
    }

    private void ShowHealAnimation(ICombatSession session, IActor user, IActor target, int amount)
    {
        // Flash-only: highlight the healed ally briefly; no center icons
        _flashAllyId = target.Id; _flashAllyTicks = 4;
        for (int i = 0; i < 4; i++)
        {
            RenderCombatScene(session, current: null, plannedActions: null, animationFrame: null);
            Sleep(100);
            _flashAllyTicks = Math.Max(0, _flashAllyTicks - 1);
        }
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

    private void WriteEnemiesTable(IReadOnlyList<IActor> enemies)
    {
        int w = Math.Clamp(BarWidth(), 12, 18);
        var enemiesTable = new Table().Border(TableBorder.Rounded).Title("[bold red]Select Target[/]");
        enemiesTable.AddColumn("#");
        enemiesTable.AddColumn("Name");
        enemiesTable.AddColumn("Class");
        enemiesTable.AddColumn("Lv");
        enemiesTable.AddColumn("HP");
        enemiesTable.AddColumn("MP");
        enemiesTable.AddColumn("TP");
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            var hp = e.GetStat(StatType.Health);
            var mp = e.GetStat(StatType.Mana);
            var tp = e.GetStat(StatType.Technical);
            enemiesTable.AddRow(
                (i + 1).ToString(),
                e.Name,
                Fit(e.EffectiveClass.ToString(), ClassDisplayWidth()),
                $"[yellow]{e.Level}[/]",
                Bar("HP", hp.Current, hp.Modified, w, "red1"),
                Bar("MP", mp.Current, mp.Modified, w, "deepskyblue1"),
                Bar("TP", tp.Current, tp.Modified, w, "orchid")
            );
        }
        AnsiConsole.Write(enemiesTable);
    }
}
