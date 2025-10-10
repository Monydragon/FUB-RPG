using System.Collections.Generic;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Parties;

public interface IParty
{
    IReadOnlyList<IActor> Members { get; }
    IActor Leader { get; }
    int MaxSize { get; }
    int Gold { get; }
    void AddGold(int amount);
    bool TrySpendGold(int amount);
    bool TryAdd(IActor actor);
    bool Remove(Guid actorId);
    bool SetLeader(Guid actorId);
}
