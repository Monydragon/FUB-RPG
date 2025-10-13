using Fub.Interfaces.Map;

namespace Fub.Implementations.Progression.FubWithAgents.Interfaces.Game;

/// <summary>
/// Represents the game world containing multiple interconnected maps.
/// </summary>
public interface IWorld
{
    Guid Id { get; }
    string Name { get; }
    int Seed { get; } // Added for deterministic generation
    IReadOnlyList<IMap> Maps { get; }
    IMap? GetMap(Guid mapId);
    void AddMap(IMap map);
    bool TryGetMapConnection(Guid fromMapId, string exitName, out Guid toMapId, out int toX, out int toY);
    void AddMapConnection(Guid fromMapId, string exitName, Guid toMapId, int toX, int toY);
}