using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Parties;

namespace Fub.Implementations.Parties;

public sealed class Party : IParty
{
    private readonly List<IActor> _members = new();
    public IReadOnlyList<IActor> Members => _members;
    public IActor Leader { get; private set; }
    public int MaxSize { get; }

    public Party(IActor leader, int maxSize = 4)
    {
        MaxSize = maxSize;
        _members.Add(leader);
        Leader = leader;
    }

    public bool TryAdd(IActor actor)
    {
        if (_members.Count >= MaxSize) return false;
        if (_members.Any(m => m.Id == actor.Id)) return false;
        _members.Add(actor);
        return true;
    }

    public bool Remove(Guid actorId)
    {
        var found = _members.FirstOrDefault(m => m.Id == actorId);
        if (found == null) return false;
        if (found == Leader && _members.Count > 1)
        {
            // Promote first other member
            Leader = _members.First(m => m.Id != actorId);
        }
        _members.Remove(found);
        return true;
    }

    public bool SetLeader(Guid actorId)
    {
        var found = _members.FirstOrDefault(m => m.Id == actorId);
        if (found == null) return false;
        Leader = found;
        return true;
    }
}

