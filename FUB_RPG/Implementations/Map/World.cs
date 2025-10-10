using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Implementations.Progression.FubWithAgents.Interfaces.Game;
using Fub.Interfaces.Map;
using Fub.Interfaces.Game;

namespace Fub.Implementations.Game;

public sealed class World : IWorld
{
    private readonly List<IMap> _maps = new();
    private readonly Dictionary<(Guid fromMapId, string exitName), (Guid toMapId, int toX, int toY)> _connections = new();

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<IMap> Maps => _maps;

    public World(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
    }

    public IMap? GetMap(Guid mapId) => _maps.FirstOrDefault(m => m.Id == mapId);

    public void AddMap(IMap map)
    {
        if (!_maps.Any(m => m.Id == map.Id))
            _maps.Add(map);
    }

    public bool TryGetMapConnection(Guid fromMapId, string exitName, out Guid toMapId, out int toX, out int toY)
    {
        if (_connections.TryGetValue((fromMapId, exitName), out var connection))
        {
            toMapId = connection.toMapId;
            toX = connection.toX;
            toY = connection.toY;
            return true;
        }
        toMapId = Guid.Empty;
        toX = 0;
        toY = 0;
        return false;
    }

    public void AddMapConnection(Guid fromMapId, string exitName, Guid toMapId, int toX, int toY)
    {
        _connections[(fromMapId, exitName)] = (toMapId, toX, toY);
    }
}

