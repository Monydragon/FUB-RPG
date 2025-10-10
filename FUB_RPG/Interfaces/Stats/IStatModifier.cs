using Fub.Enums;

namespace Fub.Interfaces.Stats;

/// <summary>
/// Describes a modifier applied to a stat. Pure data contract.
/// </summary>
public interface IStatModifier
{
    StatType Stat { get; }
    StatModifierType ModifierType { get; }
    double Value { get; }
    string SourceId { get; }
    int? DurationTurns { get; }
    int Priority { get; }
}

