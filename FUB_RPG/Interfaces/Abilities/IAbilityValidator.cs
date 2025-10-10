using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Abilities;

public interface IAbilityValidator
{
    bool Validate(IActor user, IAbility ability, IAbilityContext context, out string? failureReason);
}

