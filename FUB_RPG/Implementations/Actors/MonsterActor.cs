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
}
