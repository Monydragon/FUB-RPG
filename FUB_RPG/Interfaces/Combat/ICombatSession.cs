using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Combat;

public interface ICombatSession
{
    Guid Id { get; }
    IReadOnlyList<IActor> Allies { get; }
    IReadOnlyList<IActor> Enemies { get; }
    CombatOutcome Outcome { get; }
    bool IsActive { get; }
}

