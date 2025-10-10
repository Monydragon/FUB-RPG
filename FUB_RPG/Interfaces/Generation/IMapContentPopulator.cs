using Fub.Interfaces.Map;
using Fub.Interfaces.Parties;

namespace Fub.Interfaces.Generation;

public interface IMapContentPopulator
{
    void Populate(IMap map, IParty party, IMapContentConfig? config = null);
}

