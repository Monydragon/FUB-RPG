using Fub.Enums;
using Fub.Implementations.Progression.FubWithAgents.Interfaces.Game;
using Fub.Interfaces.Game;
using Fub.Interfaces.Map;
using Fub.Interfaces.Parties;

namespace Fub.Implementations.Game;

public sealed class GameState : IGameState
{
    public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
    public IParty Party { get; private set; }
    public IMap? CurrentMap { get; private set; }
    public IWorld? CurrentWorld { get; private set; }
    public int TurnNumber { get; private set; }
    public InputMode InputMode { get; private set; } = InputMode.Keyboard;
    public ControllerType ControllerType { get; private set; } = ControllerType.Unknown;

    // New: Difficulty
    public Difficulty Difficulty { get; private set; } = Difficulty.Normal;

    public GameState(IParty party)
    {
        Party = party;
    }

    public void SetMap(IMap map) => CurrentMap = map;
    public void SetWorld(IWorld world) => CurrentWorld = world;
    public void SetPhase(GamePhase phase) => Phase = phase;
    public void IncrementTurn() => TurnNumber++;
    public void SetInputMode(InputMode mode) => InputMode = mode;
    public void SetControllerType(ControllerType type) => ControllerType = type;
    public void SetDifficulty(Difficulty difficulty) => Difficulty = difficulty;
}
