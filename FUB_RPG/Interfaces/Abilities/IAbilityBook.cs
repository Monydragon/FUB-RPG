using System;
using System.Collections.Generic;

namespace Fub.Interfaces.Abilities;

public interface IAbilityBook
{
    IReadOnlyList<IAbility> KnownAbilities { get; }
    bool Learn(IAbility ability);
    bool Forget(Guid abilityId);
    IAbility? Get(Guid abilityId);
}
