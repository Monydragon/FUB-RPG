using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Player;

public interface IPlayer : IActor
{
    IPlayerProfile Profile { get; }
}

