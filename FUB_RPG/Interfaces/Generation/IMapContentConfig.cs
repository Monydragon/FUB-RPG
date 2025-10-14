namespace Fub.Interfaces.Generation;

public interface IMapContentConfig
{
    int EnemyCount { get; }
    int NpcCount { get; }
    int ItemCount { get; }
    int MinDistanceFromLeader { get; }
    int? Seed { get; }

    // New: shop/town configuration knobs
    int ShopStockMin { get; }
    int ShopStockMax { get; }
    int TownMinShops { get; }
    int TownMinNpcs { get; }
    int TownMaxNpcs { get; }
}
