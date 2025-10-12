namespace Fub.Implementations.Abilities;

public sealed class AbilityBook : Fub.Interfaces.Abilities.IAbilityBook
{
    private readonly List<Fub.Interfaces.Abilities.IAbility> _known = new();
    public IReadOnlyList<Fub.Interfaces.Abilities.IAbility> KnownAbilities => _known;

    public bool Learn(Fub.Interfaces.Abilities.IAbility ability)
    {
        if (_known.Any(a => string.Equals(a.Name, ability.Name, StringComparison.OrdinalIgnoreCase))) return false;
        _known.Add(ability);
        return true;
    }

    public bool Forget(Guid abilityId)
    {
        var i = _known.FindIndex(a => a.Id == abilityId);
        if (i < 0) return false;
        _known.RemoveAt(i);
        return true;
    }

    public Fub.Interfaces.Abilities.IAbility? Get(Guid abilityId) => _known.FirstOrDefault(a => a.Id == abilityId);
}
