using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Map;

public interface IRoom
{
    Guid Id { get; }
    RoomType RoomType { get; }
    int X { get; }
    int Y { get; }
    int Width { get; }
    int Height { get; }
    IReadOnlyList<Guid> ConnectedRoomIds { get; }
    IReadOnlyList<IActor> Actors { get; }
}

