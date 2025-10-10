namespace Fub.Interfaces.Generation;

public interface IMapContentConfig
{
    int EnemyCount { get; }
    int NpcCount { get; }
    int ItemCount { get; }
    int MinDistanceFromLeader { get; }
    int? Seed { get; }
}

