using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Actors;

public sealed class MonsterActor : ActorBase, IMonster
{
    public bool IsElite { get; }
    public bool IsBoss { get; }

    public MonsterActor(string name, Species species, ActorClass cls, int x, int y, bool elite = false, bool boss = false)
        : base(name, species, cls, x, y)
    {
        IsElite = elite;
        IsBoss = boss;
    }

    /// <summary>
    /// Creates a monster with custom stats. Use ActorStatPresets for convenient defaults.
    /// Example: new MonsterActor("Goblin", Species.Goblin, ActorClass.Warrior, 5, 5, ActorStatPresets.WeakEnemy())
    /// </summary>
    public MonsterActor(string name, Species species, ActorClass cls, int x, int y, Dictionary<StatType, double> customStats, bool elite = false, bool boss = false)
        : base(name, species, cls, x, y, customStats)
    {
        IsElite = elite;
        IsBoss = boss;
    }
}
