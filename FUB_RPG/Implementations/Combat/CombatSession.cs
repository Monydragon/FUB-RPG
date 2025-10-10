using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Combat;

namespace Fub.Implementations.Combat;

public sealed class CombatSession : ICombatSession
{
    private readonly List<IActor> _allies;
    private readonly List<IActor> _enemies;
    
    public Guid Id { get; }
    public IReadOnlyList<IActor> Allies => _allies;
    public IReadOnlyList<IActor> Enemies => _enemies;
    public CombatOutcome Outcome { get; private set; }
    public bool IsActive => Outcome == CombatOutcome.InProgress;

    public CombatSession(IEnumerable<IActor> allies, IEnumerable<IActor> enemies)
    {
        Id = Guid.NewGuid();
        _allies = allies.ToList();
        _enemies = enemies.ToList();
        Outcome = CombatOutcome.InProgress;
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
}

