using System.Collections.Generic;
using Fub.Interfaces.Effects;

namespace Fub.Interfaces.Abilities;

public interface IAbilityExecutionResult
{
    bool Success { get; }
    string? FailureReason { get; }
    IReadOnlyList<IEffectInstance> AppliedEffects { get; }
    double CooldownAppliedSeconds { get; }
}

