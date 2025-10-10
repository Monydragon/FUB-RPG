using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Interfaces.Stats;

public interface IHasStats
{
    IReadOnlyDictionary<StatType, IStatValue> AllStats { get; }
    IStatValue GetStat(StatType type);
    bool TryGetStat(StatType type, out IStatValue value);
}
