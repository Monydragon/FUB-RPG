using Fub.Enums;

namespace Fub.Interfaces.Stats;

/// <summary>
/// Represents a single stat with base and modified values. No logic implementation here.
/// </summary>
public interface IStatValue
{
    StatType Type { get; }
    double Base { get; }
    double Modified { get; }
    double Current { get; }
}

