using Spectre.Console;
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

    public ICombatSession BeginCombat(ICombatSession session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold red]\u2694\ufe0f  COMBAT INITIATED  \u2694\ufe0f[/]").RuleStyle("red"));
        AnsiConsole.WriteLine();
        
        RenderCombatStatus(session, current: null, plannedActions: null);
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
            RenderCombatStatusWithMap(session, current: current, plannedMap: BuildPlannedMap(plannedByIndex, aliveAllies));
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
            RenderCombatStatusWithMap(session, current: null, plannedMap: BuildPlannedMap(plannedByIndex, aliveAllies));
            AnsiConsole.Write(new Rule("[yellow]Confirm Planned Actions[/]").RuleStyle("yellow"));
            var confirmChoice = PromptNavigator.PromptChoice(
                "Proceed with these actions?",
                new List<string> { "Confirm", "Edit Actor", "Restart Planning" },
                PromptNavigator.DefaultInputMode,
                PromptNavigator.DefaultControllerType,
                renderBackground: () =>
                {
                    RenderCombatStatusWithMap(session, current: null, plannedMap: BuildPlannedMap(plannedByIndex, aliveAllies));
                });
            if (confirmChoice == "Confirm") break;
            if (confirmChoice == "Restart Planning")
            {
                Array.Fill(plannedByIndex, null);
                idx = 0;
                continue;
            }
            // Edit specific actor
            int editIndex = Math.Max(1, ParseLeadingIndex(
                PromptNavigator.PromptChoice(
                    "Edit which actor?",
                    aliveAllies.Select((a, i) => $"{i+1}. {a.Name}").ToList(),
                    PromptNavigator.DefaultInputMode,
                    PromptNavigator.DefaultControllerType,
                    renderBackground: () =>
                    {
                        RenderCombatStatusWithMap(session, current: null, plannedMap: BuildPlannedMap(plannedByIndex, aliveAllies));
                    }
                ))) - 1;
            editIndex = Math.Clamp(editIndex, 0, aliveAllies.Count - 1);
            idx = editIndex;
            plannedByIndex[idx] = null;
            // loop back to planning from this index
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
            Thread.Sleep(600);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        InputWaiter.WaitForAny(PromptNavigator.DefaultInputMode);
        RenderCombatStatus(session, current: null, plannedActions: null);
    }

    public void EndCombat(ICombatSession session)
    {
        AnsiConsole.WriteLine();
        
        if (session.Outcome == CombatOutcome.Victory)
        {
            AnsiConsole.Write(new Rule("[bold green]\ud83c\udf89 VICTORY! \ud83c\udf89[/]").RuleStyle("green"));
            
            long totalExp = session.Enemies.Sum(e => CalculateExperienceReward(e.Level));
            AnsiConsole.MarkupLine($"\n[yellow]Gained {totalExp} experience![/]");
            
            foreach (var ally in session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0))
            {
                var jobClass = ally.EffectiveClass;
                AnsiConsole.MarkupLine($"[green]{ally.Name}[/] gains {totalExp} EXP for {jobClass}!");
            }
        }
        else if (session.Outcome == CombatOutcome.Defeat)
        {
            AnsiConsole.Write(new Rule("[bold red]\ud83d\udc80 DEFEAT \ud83d\udc80[/]").RuleStyle("red"));
            AnsiConsole.MarkupLine("\n[red]Your party has been defeated...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Returning to main menu...");
        }
        
        AnsiConsole.MarkupLine("Press any key to continue...");
        InputWaiter.WaitForAny(PromptNavigator.DefaultInputMode);
    }

    private void RenderCombatStatus(ICombatSession session, IActor? current, List<string>? plannedActions)
    {
        AnsiConsole.WriteLine();
        var allies = new Table().Border(TableBorder.Rounded).Title("[bold green]Party[/]");
        allies.AddColumn(" ");
        allies.AddColumn("Name");
        allies.AddColumn("Class");
        allies.AddColumn("Species");
        allies.AddColumn("Lv");
        allies.AddColumn("EXP");
        allies.AddColumn("ToNext");
        allies.AddColumn("HP");
        allies.AddColumn("MP");
        allies.AddColumn("TP");
        foreach (var a in session.Allies)
        {
            var (hp, mp, tp) = StatTriplet(a);
            var (exp, toNext, lvl) = LevelTriplet(a);
            bool isCurrent = current != null && a.Id == current.Id;
            var marker = isCurrent ? "[cyan]>[/]" : " ";
            var name = isCurrent ? $"[cyan]{a.Name}[/]" : a.Name;
            allies.AddRow(marker, name, a.EffectiveClass.ToString(), a.Species.ToString(), lvl.ToString(), exp.ToString(), ToNextString(toNext), hp, mp, tp);
        }

        AnsiConsole.Write(allies);
        
        if (plannedActions != null && plannedActions.Count > 0)
        {
            var panel = new Panel(string.Join(Environment.NewLine, plannedActions))
                .Header("[bold yellow]Planned Actions[/]").BorderColor(Color.Yellow);
            AnsiConsole.Write(panel);
        }
    }

    private static (string hp, string mp, string tp) StatTriplet(IActor a)
    {
        var h = a.GetStat(StatType.Health);
        var m = a.GetStat(StatType.Mana);
        var t = a.GetStat(StatType.Technical);
        return ($"{h.Current:F0}/{h.Modified:F0}", $"{m.Current:F0}/{m.Modified:F0}", $"{t.Current:F0}/{t.Modified:F0}");
    }

    private static (long exp, long toNext, int lvl) LevelTriplet(IActor a)
    {
        var jl = a.JobSystem.GetJobLevel(a.EffectiveClass);
        return (jl.Experience, jl.ExperienceToNextLevel, jl.Level);
    }

    private static string ToNextString(long toNext) => toNext <= 0 ? "-" : toNext.ToString();

    private string DescribePlanned(ICombatAction action)
    {
        if (action.ActionType == CombatActionType.UseAbility)
        {
            var ability = (action as CombatAction)?.CustomData as IAbility;
            var targetName = action.Target != null ? action.Target.Name : action.Actor.Name;
            if (ability != null)
            {
                var cost = (ability as AbilityBase)?.CostAmount ?? 0;
                var costTag = ability.CostType == AbilityCostType.Mana ? $"[blue]{cost:F0} MP[/]" : ability.CostType == AbilityCostType.Technical ? $"[magenta]{cost:F0} TP[/]" : ability.CostType == AbilityCostType.Health ? $"[red]{cost:F0} HP[/]" : "";
                return $"Use {ability.Name} on {targetName} {(string.IsNullOrEmpty(costTag)?"":$"({costTag})")}";
            }
            return $"Use Ability on {targetName}";
        }
        return action.ActionType switch
        {
            CombatActionType.Attack => $"Attack {(action.Target != null ? action.Target.Name : "?")}",
            CombatActionType.Defend => "Defend",
            CombatActionType.Pass => "Pass",
            _ => "Act"
        };
    }

    private void WriteCurrentActorPanel(IActor actor)
    {
        // Build a clean, compact stats table with abbreviations.
        var hp = actor.GetStat(StatType.Health);
        var mp = actor.GetStat(StatType.Mana);
        var tp = actor.GetStat(StatType.Technical);
        var jl = actor.JobSystem.GetJobLevel(actor.EffectiveClass);

        var header = $"[bold cyan]{actor.Name}[/]'s Turn  [grey]Lv[/]: {jl.Level}  [grey]Class[/]: {actor.EffectiveClass}  [grey]Species[/]: {actor.Species}";
        var infoTable = new Table().Border(TableBorder.None);
        infoTable.ShowHeaders = false;
        infoTable.AddColumn(""); infoTable.AddColumn(""); infoTable.AddColumn("");
        // Row 1: HP MP TP
        infoTable.AddRow($"[red]HP[/] {hp.Current:F0}/{hp.Modified:F0}", $"[blue]MP[/] {mp.Current:F0}/{mp.Modified:F0}", $"[magenta]TP[/] {tp.Current:F0}/{tp.Modified:F0}");
        // Row 2: STR VIT AGI INT SPR LCK
        infoTable.AddRow(
            $"STR {actor.GetStat(StatType.Strength).Modified:F0}",
            $"VIT {actor.GetStat(StatType.Vitality).Modified:F0}",
            $"AGI {actor.GetStat(StatType.Agility).Modified:F0}");
        infoTable.AddRow(
            $"INT {actor.GetStat(StatType.Intellect).Modified:F0}",
            $"SPR {actor.GetStat(StatType.Spirit).Modified:F0}",
            $"LCK {actor.GetStat(StatType.Luck).Modified:F0}");
        // Row 4: Armor Eva Crit% CritDmg AtkPwr SpPwr Speed (split across two rows)
        infoTable.AddRow(
            $"Armor {actor.GetStat(StatType.Armor).Modified:F0}",
            $"Eva {actor.GetStat(StatType.Evasion).Modified:F0}",
            $"Crit% {actor.GetStat(StatType.CritChance).Modified:F0}");
        infoTable.AddRow(
            $"CritDmg {actor.GetStat(StatType.CritDamage).Modified:F0}",
            $"AtkPwr {actor.GetStat(StatType.AttackPower).Modified:F0}",
            $"SpPwr {actor.GetStat(StatType.SpellPower).Modified:F0}");
        infoTable.AddRow($"Speed {actor.GetStat(StatType.Speed).Modified:F0}", "", "");
        // Resists
        infoTable.AddRow(
            $"Res:Fire {actor.GetStat(StatType.FireResist).Modified:F0}",
            $"Res:Cold {actor.GetStat(StatType.ColdResist).Modified:F0}",
            $"Res:Lightning {actor.GetStat(StatType.LightningResist).Modified:F0}");
        infoTable.AddRow(
            $"Res:Poison {actor.GetStat(StatType.PoisonResist).Modified:F0}",
            $"Res:Arcane {actor.GetStat(StatType.ArcaneResist).Modified:F0}",
            $"Res:Shadow {actor.GetStat(StatType.ShadowResist).Modified:F0}");
        infoTable.AddRow($"Res:Holy {actor.GetStat(StatType.HolyResist).Modified:F0}", "", "");

        var panel = new Panel(infoTable).Header(header).BorderColor(Color.Cyan1).Padding(1, 0);
        AnsiConsole.Write(panel);
    }

    private void WriteEnemiesTable(IReadOnlyList<IActor> enemies)
    {
        var enemiesTable = new Table().Border(TableBorder.Rounded).Title("[bold red]Select Target[/]");
        enemiesTable.AddColumn("#"); enemiesTable.AddColumn("Name"); enemiesTable.AddColumn("Class"); enemiesTable.AddColumn("Species"); enemiesTable.AddColumn("Lv"); enemiesTable.AddColumn("HP"); enemiesTable.AddColumn("MP"); enemiesTable.AddColumn("TP");
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            var hp = e.GetStat(StatType.Health); var mp = e.GetStat(StatType.Mana); var tp = e.GetStat(StatType.Technical);
            enemiesTable.AddRow((i+1).ToString(), e.Name, e.EffectiveClass.ToString(), e.Species.ToString(), e.Level.ToString(), $"{hp.Current:F0}/{hp.Modified:F0}", $"{mp.Current:F0}/{mp.Modified:F0}", $"{tp.Current:F0}/{tp.Modified:F0}");
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
                choices.Insert(1, "✨ Use Ability");
            if (allowBack) choices.Add("Back");

            var choice = PromptNavigator.PromptChoice(
                "Select action:",
                choices,
                PromptNavigator.DefaultInputMode,
                PromptNavigator.DefaultControllerType,
                renderBackground: () =>
                {
                    RenderCombatStatus(session, current: actor, plannedActions: plannedActions);
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
                var chosen = PromptNavigator.PromptChoice(
                    "Choose ability:", abilityLabels,
                    PromptNavigator.DefaultInputMode, PromptNavigator.DefaultControllerType,
                    renderBackground: () => { RenderCombatStatus(session, current: actor, plannedActions: plannedActions); WriteCurrentActorPanel(actor); });
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
                    var ttl = PromptNavigator.PromptChoice("Target ally:", labels, PromptNavigator.DefaultInputMode, PromptNavigator.DefaultControllerType,
                        renderBackground: () => { RenderCombatStatus(session, current: actor, plannedActions: plannedActions); WriteCurrentActorPanel(actor); });
                    if (allowBack && IsBackSelection(ttl)) continue;
                    int ti = Math.Max(1, ParseLeadingIndex(ttl)) - 1;
                    targets = new List<IActor> { session.Allies[ti] };
                }
                else // default SingleEnemy
                {
                    WriteEnemiesTable(validEnemies);
                    var options = validEnemies.Select((e, idx) => $"{idx+1}. {e.Name} (Lv{e.Level} HP {e.GetStat(StatType.Health).Current:F0}/{e.GetStat(StatType.Health).Modified:F0})").ToList();
                    if (allowBack) options.Add("Back");
                    var choiceLabel = PromptNavigator.PromptChoice("Target:", options, PromptNavigator.DefaultInputMode, PromptNavigator.DefaultControllerType,
                        renderBackground: () => { RenderCombatStatus(session, current: actor, plannedActions: plannedActions); WriteCurrentActorPanel(actor); WriteEnemiesTable(validEnemies); });
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
                var choiceLabel = PromptNavigator.PromptChoice(
                    "Target:",
                    options,
                    PromptNavigator.DefaultInputMode,
                    PromptNavigator.DefaultControllerType,
                    renderBackground: () =>
                    {
                        RenderCombatStatus(session, current: actor, plannedActions: plannedActions);
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
            AnsiConsole.MarkupLine($"[bold yellow]\ud83d\udca5 CRITICAL HIT! \ud83d\udca5[/]");
        }
        
        var packet = new DamagePacket(attacker.Id, new[] { new DamageComponent(DamageType.Physical, finalDamage) }, isCritical: isCrit);
        var remaining = defender.TakeDamage(packet);
        
        var attackerColor = session.Allies.Contains(attacker) ? "green" : "red";
        var defenderColor = session.Allies.Contains(defender) ? "green" : "red";
        
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
            if (a != null) list.Add(DescribePlanned(a));
        }
        return list;
    }

    private void RenderCombatStatusWithMap(ICombatSession session, IActor? current, Dictionary<Guid, string>? plannedMap)
    {
        AnsiConsole.WriteLine();
        var allies = new Table().Border(TableBorder.Rounded).Title("[bold green]Party[/]");
        allies.AddColumn(" ");
        allies.AddColumn("Name");
        allies.AddColumn("Class");
        allies.AddColumn("Species");
        allies.AddColumn("Lv");
        allies.AddColumn("EXP");
        allies.AddColumn("ToNext");
        allies.AddColumn("HP");
        allies.AddColumn("MP");
        allies.AddColumn("TP");
        allies.AddColumn("Planned");
        foreach (var a in session.Allies)
        {
            var (hp, mp, tp) = StatTriplet(a);
            var (exp, toNext, lvl) = LevelTriplet(a);
            bool isCurrent = current != null && a.Id == current.Id;
            var marker = isCurrent ? "[cyan]>[/]" : " ";
            var name = isCurrent ? $"[cyan]{a.Name}[/]" : a.Name;
            var planned = plannedMap != null && plannedMap.TryGetValue(a.Id, out var desc) ? desc : "-";
            allies.AddRow(marker, name, a.EffectiveClass.ToString(), a.Species.ToString(), lvl.ToString(), exp.ToString(), ToNextString(toNext), hp, mp, tp, planned);
        }
        AnsiConsole.Write(allies);
    }

    private Dictionary<Guid, string> BuildPlannedMap(ICombatAction?[] planned, List<IActor> allies)
    {
        var dict = new Dictionary<Guid, string>();
        for (int i = 0; i < allies.Count && i < planned.Length; i++)
        {
            var act = planned[i];
            if (act != null) dict[allies[i].Id] = DescribePlanned(act);
        }
        return dict;
    }
}
