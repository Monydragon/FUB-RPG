using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Random;
using Fub.Interfaces.Loot;

namespace Fub.Interfaces.Loot;

public interface ILootGenerator
{
    IEnumerable<ILootEntry> Generate(ILootTable table, LootRarityCurve curve, IRandomSource rng);
}

