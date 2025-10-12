using Fub.Enums;
using Fub.Implementations.Abilities;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Actors;

// A handful of basic concrete abilities for learnsets

public sealed class PowerStrike : AbilityBase
{
    public PowerStrike() : base("Power Strike", "A heavy TP strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 20, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class GuardShout : AbilityBase
{
    public GuardShout() : base("Guard Shout", "Bolster resolve.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 10, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class FireSpell : AbilityBase
{
    public FireSpell() : base("Fire", "Launch a firebolt.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class IceSpell : AbilityBase
{
    public IceSpell() : base("Blizzard", "Frost shard.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class HealSpell : AbilityBase
{
    public HealSpell() : base("Cure", "Restore HP to one ally.", AbilityCategory.Heal, AbilityTargetType.SingleAlly, AbilityCostType.Mana, 12, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class CureAllSpell : AbilityBase
{
    public CureAllSpell() : base("Medica", "Restore HP to party.", AbilityCategory.Heal, AbilityTargetType.Self, AbilityCostType.Mana, 20, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class Backstab : AbilityBase
{
    public Backstab() : base("Backstab", "A precise TP strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 15, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class PoisonDagger : AbilityBase
{
    public PoisonDagger() : base("Poison Dagger", "Envenomed strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 12, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class Smite : AbilityBase
{
    public Smite() : base("Smite", "Holy strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class HolyBlade : AbilityBase
{
    public HolyBlade() : base("Holy Blade", "Blessed blade.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 16, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class Iaijutsu : AbilityBase
{
    public Iaijutsu() : base("Iaijutsu", "Swift draw.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 18, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class JumpStrike : AbilityBase
{
    public JumpStrike() : base("Jump", "Leaping strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 18, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class BurstShot : AbilityBase
{
    public BurstShot() : base("Burst Shot", "Gunblade burst.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 12, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

public sealed class FocusTechnique : AbilityBase
{
    public FocusTechnique() : base("Focus", "Gather strength.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 10, 0) {}
    public override IAbilityExecutionResult Execute(IActor user, IAbilityContext context) => new AbilityExecutionResult(true);
}

