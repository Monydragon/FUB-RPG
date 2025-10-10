using Fub.Enums;
using Fub.Interfaces.Stats;

namespace Fub.Implementations.Stats;

public sealed class StatModifier : IStatModifier
{
    public StatType Stat { get; }
    public StatModifierType ModifierType { get; }
    public double Value { get; }
    public string SourceId { get; }
    public int? DurationTurns { get; private set; }
    public int Priority { get; }

    public StatModifier(StatType stat, StatModifierType modifierType, double value, string sourceId, int? durationTurns = null, int priority = 0)
    {
        Stat = stat;
        ModifierType = modifierType;
        Value = value;
        SourceId = sourceId;
        DurationTurns = durationTurns;
        Priority = priority;
    }

    public bool Tick()
    {
        if (DurationTurns is null) return false;
        DurationTurns--;
        return DurationTurns <= 0;
    }
}
