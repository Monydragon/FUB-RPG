using System.Collections.Generic;

namespace Fub.Interfaces.Combat;

public interface ICombatLog
{
    IReadOnlyList<ICombatLogEntry> Entries { get; }
    void Append(ICombatLogEntry entry);
}

