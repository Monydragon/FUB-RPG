using System.Collections.Generic;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Abilities;

public interface IAbilityContext
{
    IReadOnlyList<IActor> Targets { get; }
    int? TargetX { get; }
    int? TargetY { get; }
    object? Data { get; }
}

