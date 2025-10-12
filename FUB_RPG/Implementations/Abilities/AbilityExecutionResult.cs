using System.Collections.Generic;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Effects;

namespace Fub.Implementations.Abilities;

public sealed class AbilityExecutionResult : IAbilityExecutionResult
{
    public bool Success { get; }
    public string? FailureReason { get; }
    public IReadOnlyList<IEffectInstance> AppliedEffects { get; }
    public double CooldownAppliedSeconds { get; }

    public AbilityExecutionResult(bool success, string? reason = null, IReadOnlyList<IEffectInstance>? effects = null, double cd = 0)
    {
        Success = success;
        FailureReason = reason;
        AppliedEffects = effects ?? new List<IEffectInstance>();
        CooldownAppliedSeconds = cd;
    }
}

