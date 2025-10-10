using Fub.Enums;

namespace Fub.Interfaces.Combat;

public interface IDamageComponent
{
    DamageType Type { get; }
    double Amount { get; }
    bool CanCrit { get; }
}

