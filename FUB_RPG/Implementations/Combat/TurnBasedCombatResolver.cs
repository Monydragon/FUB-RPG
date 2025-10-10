using Fub.Implementations.Combat;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Combat;
using Fub.Interfaces.Items.Weapons;
using Fub.Implementations.Input;

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
        var actions = new List<ICombatAction>();
        var planned = new List<string>();

        // Get actions from all alive allies with clear UI
        foreach (var ally in session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0))
        {
            // Clear and render fresh status highlighting the current actor
            AnsiConsole.Clear();
            RenderCombatStatus(session, current: ally, plannedActions: planned);
            var action = GetPlayerAction(ally, session, planned);
            if (action != null)
            {
                actions.Add(action);
                planned.Add(DescribePlanned(action));
            }
        }

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
        return action.ActionType switch
        {
            CombatActionType.Attack => $"{action.Actor.Name} will Attack {(action.Target != null ? action.Target.Name : "?")}",
            CombatActionType.Defend => $"{action.Actor.Name} will Defend",
            CombatActionType.Pass => $"{action.Actor.Name} will Pass",
            _ => $"{action.Actor.Name} will Act"
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

    private ICombatAction? GetPlayerAction(IActor actor, ICombatSession session, List<string> plannedActions)
    {
        var validEnemies = session.Enemies.Where(e => e.GetStat(StatType.Health).Current > 0).ToList();
        if (validEnemies.Count == 0) return null;

        AnsiConsole.WriteLine();
        // Show current actor details panel for the action selection screen
        WriteCurrentActorPanel(actor);

        var choices = new List<string> { "\u2694\ufe0f Attack", "\ud83d\udee1\ufe0f Defend", "\ud83c\udfc3 Pass" };
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

        if (choice.Contains("Attack"))
        {
            // Show enemies table for selection (no EXP)
            WriteEnemiesTable(validEnemies);

            var options = validEnemies.Select((e, idx) => $"{idx+1}. {e.Name} (Lv{e.Level} HP {e.GetStat(StatType.Health).Current:F0}/{e.GetStat(StatType.Health).Modified:F0})").ToList();
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
            int chosenIndex = Math.Max(1, ParseLeadingIndex(choiceLabel)) - 1;
            var target = validEnemies[chosenIndex];
            return new CombatAction(CombatActionType.Attack, actor, target, priority: 0);
        }
        else if (choice.Contains("Defend"))
        {
            return new CombatAction(CombatActionType.Defend, actor, priority: 100);
        }
        else
        {
            return new CombatAction(CombatActionType.Pass, actor, priority: -100);
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
            case CombatActionType.Pass:
                AnsiConsole.MarkupLine($"[grey]{action.Actor.Name} passes their turn.[/]");
                break;
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
        
        var strength = attacker.GetStat(StatType.Strength).Modified;
        var baseDamage = strength * 2.0;
        
        if (attacker is ActorBase actorBase)
        {
            var weapon = actorBase.GetEquipped(EquipmentSlot.MainHand) as IWeapon;
            if (weapon != null)
            {
                var weaponDamage = _rng.NextDouble() * (weapon.MaxDamage - weapon.MinDamage) + weapon.MinDamage;
                baseDamage += weaponDamage;
            }
        }
        
        var defense = defender.GetStat(StatType.Vitality).Modified;
        var damageReduction = defense * 0.5;
        var finalDamage = Math.Max(1, baseDamage - damageReduction);
        
        if (_defendingActors.ContainsKey(defender.Id))
        {
            finalDamage *= 0.5;
            AnsiConsole.MarkupLine($"[cyan]{defender.Name} is defending![/]");
        }
        
        bool isCrit = _rng.Next(100) < 10;
        if (isCrit)
        {
            finalDamage *= 1.5;
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
}
