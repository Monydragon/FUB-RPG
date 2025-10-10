using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Combat;
using Fub.Interfaces.Actors;
using Fub.Implementations.Combat;
using Spectre.Console;

namespace Fub.Implementations.Combat;

public sealed class SimpleCombatResolver : ICombatResolver
{
    public ICombatSession BeginCombat(ICombatSession session)
    {
        AnsiConsole.MarkupLine("[bold red]Combat begins![/]");
        return session;
    }

    public void ProcessTurn(ICombatSession session)
    {
        if (!session.IsActive) return;

        // Simple turn: allies attack enemies
        foreach (var ally in session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0))
        {
            var validTargets = session.Enemies.Where(e => e.GetStat(StatType.Health).Current > 0).ToList();
            if (validTargets.Count == 0) break;

            var target = validTargets[new System.Random().Next(validTargets.Count)];
            var damage = ally.GetStat(StatType.Strength).Modified * 2;
            var packet = new DamagePacket(ally.Id, new[] { new DamageComponent(DamageType.Physical, damage) });
            var remaining = target.TakeDamage(packet);
            AnsiConsole.MarkupLine($"[green]{ally.Name}[/] attacks [red]{target.Name}[/] for [yellow]{damage:F0}[/] damage!");
            
            if (remaining <= 0)
                AnsiConsole.MarkupLine($"[red]{target.Name} is defeated![/]");
        }

        // Enemies attack back
        foreach (var enemy in session.Enemies.Where(e => e.GetStat(StatType.Health).Current > 0))
        {
            var validTargets = session.Allies.Where(a => a.GetStat(StatType.Health).Current > 0).ToList();
            if (validTargets.Count == 0) break;

            var target = validTargets[new System.Random().Next(validTargets.Count)];
            var damage = enemy.GetStat(StatType.Strength).Modified * 1.5;
            var packet = new DamagePacket(enemy.Id, new[] { new DamageComponent(DamageType.Physical, damage) });
            var remaining = target.TakeDamage(packet);
            AnsiConsole.MarkupLine($"[red]{enemy.Name}[/] attacks [green]{target.Name}[/] for [yellow]{damage:F0}[/] damage!");
            
            if (remaining <= 0)
                AnsiConsole.MarkupLine($"[green]{target.Name} is defeated![/]");
        }
    }

    public void EndCombat(ICombatSession session)
    {
        var alliesAlive = session.Allies.Count(a => a.GetStat(StatType.Health).Current > 0);
        var enemiesAlive = session.Enemies.Count(e => e.GetStat(StatType.Health).Current > 0);

        if (enemiesAlive == 0)
            AnsiConsole.MarkupLine("[bold green]Victory![/]");
        else if (alliesAlive == 0)
            AnsiConsole.MarkupLine("[bold red]Defeat![/]");
    }
}

