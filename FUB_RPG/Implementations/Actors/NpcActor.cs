using Fub.Enums;

namespace Fub.Implementations.Actors;

public sealed class NpcActor : ActorBase
{
    public NpcActor(string name, Species species, ActorClass cls, int x, int y)
        : base(name, species, cls, x, y)
    {
    }
}
