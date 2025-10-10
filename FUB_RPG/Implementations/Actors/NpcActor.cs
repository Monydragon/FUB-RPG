using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Implementations.Actors;

public sealed class NpcActor : ActorBase
{
    public NpcActor(string name, Species species, ActorClass cls, int x, int y)
        : base(name, species, cls, x, y)
    {
    }

    /// <summary>
    /// Creates an NPC with custom stats. Use ActorStatPresets for convenient defaults.
    /// </summary>
    public NpcActor(string name, Species species, ActorClass cls, int x, int y, Dictionary<StatType, double> customStats)
        : base(name, species, cls, x, y, customStats)
    {
    }
}
