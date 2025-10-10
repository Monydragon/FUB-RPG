using System;
using Fub.Enums;
using Fub.Interfaces.Stats;

namespace Fub.Implementations.Stats;

public sealed class StatValue : IStatValue
{
    public StatType Type { get; }
    public double Base { get; private set; }
    public double Modified { get; private set; }
    public double Current { get; private set; }

    public StatValue(StatType type, double baseValue)
    {
        Type = type;
        Base = baseValue;
        Modified = baseValue;
        Current = baseValue;
    }

    public void SetBase(double value)
    {
        Base = value;
        Recalculate();
    }

    public void ApplyDelta(double amount)
    {
        Current = Math.Clamp(Current + amount, 0, Modified);
    }

    public void SetCurrentToMax() => Current = Modified;

    public void Recalculate(double additive = 0, double multiplicative = 1, double? overrideValue = null)
    {
        if (overrideValue.HasValue)
            Modified = overrideValue.Value;
        else
            Modified = (Base + additive) * multiplicative;
        if (Current > Modified) Current = Modified;
    }
}
