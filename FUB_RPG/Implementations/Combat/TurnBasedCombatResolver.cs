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
        
        AnsiConsole.MarkupLine("[bold green]Your Party:[/]");
        foreach (var ally in session.Allies)
            AnsiConsole.MarkupLine($"  \u2022 {ally.Name} (Lv.{ally.Level} {ally.EffectiveClass}) - HP: {ally.GetStat(StatType.Health).Current:F0}/{ally.GetStat(StatType.Health).Modified:F0}");
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold red]Enemies:[/]");
        foreach (var enemy in session.Enemies)
            AnsiConsole.MarkupLine($"  \u2022 {enemy.Name} (Lv.{enemy.Level}) - HP: {enemy.GetStat(StatType.Health).Current:F0}/{enemy.GetStat(StatType.Health).Modified:F0}");
        
        AnsiConsole.WriteLine();
        return session;
    }

    public void ProcessTurn(ICombatSession session)
    {
        if (!session.IsActive) return;

        _defendingActors.Clear();
        var actions = new List<ICombatAction>();

        // Get actions from all alive allies
        foreach (var ally in session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0))
        {
            var action = GetPlayerAction(ally, session);
            if (action != null)
                actions.Add(action);
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
            Thread.Sleep(800);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
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
        
        AnsiConsole.WriteLine();
    }

    private ICombatAction? GetPlayerAction(IActor actor, ICombatSession session)
    {
        var validEnemies = session.Enemies.Where(e => e.GetStat(StatType.Health).Current > 0).ToList();
        if (validEnemies.Count == 0) return null;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel($"[bold cyan]{actor.Name}[/]'s Turn\nHP: {actor.GetStat(StatType.Health).Current:F0}/{actor.GetStat(StatType.Health).Modified:F0} | Class: {actor.EffectiveClass}")
            .BorderColor(Color.Cyan1)
            .Padding(1, 0));

        var choices = new List<string> { "\u2694\ufe0f Attack", "\ud83d\udee1\ufe0f Defend", "\ud83c\udfc3 Pass" };
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select action:")
                .AddChoices(choices));

        if (choice.Contains("Attack"))
        {
            var displayMap = validEnemies.ToDictionary(
                e => $"{e.Name} (HP: {e.GetStat(StatType.Health).Current:F0}/{e.GetStat(StatType.Health).Modified:F0})",
                e => e);

            var targetDisplay = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select target:")
                    .AddChoices(displayMap.Keys));
            var target = displayMap[targetDisplay];
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
