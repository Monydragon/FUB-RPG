using Fub.Interfaces.Combat;

namespace Fub.Interfaces.Combat;

public interface ICombatResolver
{
    ICombatSession BeginCombat(ICombatSession session);
    void ProcessTurn(ICombatSession session);
    void EndCombat(ICombatSession session);
}

