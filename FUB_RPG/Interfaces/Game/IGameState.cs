using Fub.Enums;
using Fub.Implementations.Progression.FubWithAgents.Interfaces.Game;
using Fub.Interfaces.Map;
using Fub.Interfaces.Parties;

namespace Fub.Interfaces.Game;

public interface IGameState
{
    GamePhase Phase { get; }
    IParty Party { get; }
    IMap? CurrentMap { get; }
    IWorld? CurrentWorld { get; }
    int TurnNumber { get; }
    // Expose combat display speed configuration
    CombatSpeed CombatSpeed { get; }
    void SetCombatSpeed(CombatSpeed speed);
    // New: combat log visibility toggle
    bool ShowCombatLog { get; }
    void SetShowCombatLog(bool visible);
}
