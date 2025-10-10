using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map;

public sealed class Room : IRoom
{
    private readonly List<Guid> _connections = new();
    private readonly List<IActor> _actors = new();

    public Guid Id { get; } = Guid.NewGuid();
    public RoomType RoomType { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<Guid> ConnectedRoomIds => _connections;
    public IReadOnlyList<IActor> Actors => _actors;

    public Room(RoomType type, int x, int y, int width, int height)
    {
        RoomType = type;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public void Connect(Room other)
    {
        if (!_connections.Contains(other.Id)) _connections.Add(other.Id);
        if (!other._connections.Contains(Id)) other._connections.Add(Id);
    }

    public bool Contains(int tx, int ty) => tx >= X && ty >= Y && tx < X + Width && ty < Y + Height;

    public (int cx, int cy) Center() => (X + Width / 2, Y + Height / 2);

    public void AddActor(IActor actor)
    {
        if (!_actors.Contains(actor)) _actors.Add(actor);
    }
}
