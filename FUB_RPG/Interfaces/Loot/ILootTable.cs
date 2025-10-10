using System.Collections.Generic;

namespace Fub.Interfaces.Loot;

public interface ILootTable
{
    string Id { get; }
    IReadOnlyList<ILootEntry> Entries { get; }
}

