using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Map;

namespace Fub.Interfaces.Map;

public interface IMap
{
    Guid Id { get; }
    string Name { get; }
    MapTheme Theme { get; }
    MapKind Kind { get; }
    int Width { get; }
    int Height { get; }
    IReadOnlyList<IRoom> Rooms { get; }
    IReadOnlyList<IMapObject> Objects { get; }
    IMapTile GetTile(int x, int y);
    bool InBounds(int x, int y);
    void AddObject(IMapObject obj);
    IReadOnlyList<IMapObject> GetObjectsAt(int x, int y); // Added
    bool RemoveObject(Guid objectId); // Added
}
