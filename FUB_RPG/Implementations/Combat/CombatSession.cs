using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Combat;
using Fub.Implementations.Loot;
using Fub.Implementations.Combat;

namespace Fub.Implementations.Combat;

public sealed class CombatSession : ICombatSession
{
    private readonly List<IActor> _allies;
    private readonly List<IActor> _enemies;
    private readonly Dictionary<string, int> _combatStats = new();
    private DateTime _startTime;
    
    public Guid Id { get; }
    public IReadOnlyList<IActor> Allies => _allies;
    public IReadOnlyList<IActor> Enemies => _enemies;
    public CombatOutcome Outcome { get; private set; }
    public bool IsActive => Outcome == CombatOutcome.InProgress;
    public TimeSpan CombatDuration => DateTime.UtcNow - _startTime;
    public IReadOnlyDictionary<string, int> CombatStats => _combatStats;

    public CombatSession(IEnumerable<IActor> allies, IEnumerable<IActor> enemies)
    {
        Id = Guid.NewGuid();
        _allies = allies.ToList();
        _enemies = enemies.ToList();
        Outcome = CombatOutcome.InProgress;
        _startTime = DateTime.UtcNow;
        InitializeCombatStats();
    }

    public void UpdateOutcome()
    {
        var alliesAlive = _allies.Count(a => a.GetStat(StatType.Health).Current > 0);
        var enemiesAlive = _enemies.Count(e => e.GetStat(StatType.Health).Current > 0);

        if (enemiesAlive == 0)
            Outcome = CombatOutcome.Victory;
        else if (alliesAlive == 0)
            Outcome = CombatOutcome.Defeat;
    }

    public void AddCombatStat(string statName, int value)
    {
        if (_combatStats.ContainsKey(statName))
            _combatStats[statName] += value;
        else
            _combatStats[statName] = value;
    }

    public Fub.Implementations.Combat.VictoryScreenData? CreateVictoryScreen(Fub.Implementations.Loot.AdvancedLootGenerator lootGenerator)
    {
        if (Outcome != CombatOutcome.Victory) return null;

        var partyClasses = _allies.Select(a => a.Class).ToList();
        var averagePartyLevel = (int)_allies.Average(a => a.Level);

        var victoryManager = new VictoryScreenManager(lootGenerator);
        return victoryManager.CreateVictoryScreen(this, partyClasses, averagePartyLevel, CombatDuration, _combatStats);
    }

    private void InitializeCombatStats()
    {
        _combatStats["Damage Dealt"] = 0;
        _combatStats["Damage Taken"] = 0;
        _combatStats["Abilities Used"] = 0;
        _combatStats["Critical Hits"] = 0;
        _combatStats["Healing Done"] = 0;
    }
}
