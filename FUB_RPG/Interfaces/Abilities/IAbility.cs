using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Effects;

namespace Fub.Interfaces.Abilities;

public interface IAbility
{
    Guid Id { get; }
    string Name { get; }
    string? Description { get; }
    AbilityCategory Category { get; }
    AbilityTargetType TargetType { get; }
    AbilityCostType CostType { get; }
    double BaseCooldownSeconds { get; }
    IReadOnlyList<IEffectBlueprint> EffectBlueprints { get; }

    bool CanUse(IActor user, IAbilityContext context);
    IAbilityExecutionResult Execute(IActor user, IAbilityContext context);
}
