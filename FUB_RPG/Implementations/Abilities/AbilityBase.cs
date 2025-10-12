using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Effects;

namespace Fub.Implementations.Abilities;

public abstract class AbilityBase : IAbility
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public string? Description { get; }
    public AbilityCategory Category { get; }
    public AbilityTargetType TargetType { get; }
    public AbilityCostType CostType { get; }
    public double BaseCooldownSeconds { get; }
    public IReadOnlyList<IEffectBlueprint> EffectBlueprints { get; } = new List<IEffectBlueprint>();
    public double CostAmount { get; }

    protected AbilityBase(string name, string? desc, AbilityCategory cat, AbilityTargetType tgt, AbilityCostType costType, double costAmt, double cd)
    {
        Name = name; Description = desc; Category = cat; TargetType = tgt; CostType = costType; BaseCooldownSeconds = cd; CostAmount = costAmt;
    }

    public virtual bool CanUse(IActor user, IAbilityContext context) => true;
    public abstract IAbilityExecutionResult Execute(IActor user, IAbilityContext context);
}

