using System.Collections.Generic;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Abilities;

public sealed class AbilityContext : IAbilityContext
{
    public IReadOnlyList<IActor> Targets { get; }
    public int? TargetX { get; }
    public int? TargetY { get; }
    public object? Data { get; }

    public AbilityContext(IReadOnlyList<IActor> targets, int? tx = null, int? ty = null, object? data = null)
    {
        Targets = targets;
        TargetX = tx; TargetY = ty; Data = data;
    }
}

