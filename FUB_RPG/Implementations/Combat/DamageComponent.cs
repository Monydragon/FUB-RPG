using Fub.Enums;
using Fub.Interfaces.Combat;

namespace Fub.Implementations.Combat;

public sealed class DamageComponent : IDamageComponent
{
    public DamageType Type { get; }
    public double Amount { get; }
    public bool CanCrit { get; }

    public DamageComponent(DamageType type, double amount, bool canCrit = true)
    {
        Type = type;
        Amount = amount;
        CanCrit = canCrit;
    }
}
