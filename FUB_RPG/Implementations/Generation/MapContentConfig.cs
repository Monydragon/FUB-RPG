using Fub.Interfaces.Generation;

namespace Fub.Implementations.Generation;

public sealed class MapContentConfig : IMapContentConfig
{
    public int EnemyCount { get; init; } = 5;
    public int NpcCount { get; init; } = 2;
    public int ItemCount { get; init; } = 6;
    public int MinDistanceFromLeader { get; init; } = 3;
    public int? Seed { get; init; }

    // New: shop/town configuration knobs
    public int ShopStockMin { get; init; } = 3;
    public int ShopStockMax { get; init; } = 10;
    public int TownMinShops { get; init; } = 3;
    public int TownMinNpcs { get; init; } = 3;
    public int TownMaxNpcs { get; init; } = 10;
}
