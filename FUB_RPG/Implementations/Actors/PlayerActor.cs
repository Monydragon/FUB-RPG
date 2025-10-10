using Fub.Enums;
using Fub.Interfaces.Player;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Actors;

public sealed class PlayerActor : ActorBase, IPlayer
{
    public IPlayerProfile Profile { get; }

    public PlayerActor(string name, Species species, ActorClass cls, IPlayerProfile profile, int startX, int startY)
        : base(name, species, cls, startX, startY)
    {
        Profile = profile;
    }
}
