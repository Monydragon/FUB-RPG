using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Stats;

namespace Fub.Implementations.Stats;

public class StatsCollection : IHasStats
{
    private readonly Dictionary<StatType, StatValue> _stats = new();
    private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();

    public IReadOnlyDictionary<StatType, IStatValue> AllStats => _stats.ToDictionary(k => k.Key, v => (IStatValue)v.Value);

    public StatsCollection InitializeDefaults(IEnumerable<(StatType stat, double baseValue)> seeds)
    {
        foreach (var (stat, baseValue) in seeds)
        {
            if (!_stats.ContainsKey(stat))
                _stats[stat] = new StatValue(stat, baseValue);
        }
        RecalculateAll();
        return this;
    }

    public IStatValue GetStat(StatType type) => _stats[type];

    public bool TryGetStat(StatType type, out IStatValue value)
    {
        if (_stats.TryGetValue(type, out var sv))
        {
            value = sv;
            return true;
        }
        value = default!;
        return false;
    }

    public void SetBase(StatType type, double value)
    {
        if (_stats.TryGetValue(type, out var sv))
        {
            sv.SetBase(value);
            Recalculate(type);
        }
        else
        {
            var created = new StatValue(type, value);
            _stats[type] = created;
        }
    }

    public void AddModifier(StatModifier modifier)
    {
        if (!_modifiers.TryGetValue(modifier.Stat, out var list))
        {
            list = new List<StatModifier>();
            _modifiers[modifier.Stat] = list;
        }
        list.Add(modifier);
        Recalculate(modifier.Stat);
    }

    public void TickModifiers()
    {
        foreach (var kvp in _modifiers.ToList())
        {
            var list = kvp.Value;
            bool changed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Tick())
                {
                    list.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed)
                Recalculate(kvp.Key);
        }
    }

    private void RecalculateAll()
    {
        foreach (var stat in _stats.Keys.ToList())
            Recalculate(stat);
    }

    private void Recalculate(StatType stat)
    {
        if (!_stats.TryGetValue(stat, out var sv)) return;
        double additive = 0;
        double multiplicative = 1;
        double? overrideValue = null;
        if (_modifiers.TryGetValue(stat, out var mods))
        {
            foreach (var m in mods.OrderBy(m => m.Priority))
            {
                switch (m.ModifierType)
                {
                    case StatModifierType.Additive:
                        additive += m.Value;
                        break;
                    case StatModifierType.Multiplicative:
                        multiplicative *= m.Value;
                        break;
                    case StatModifierType.Override:
                        overrideValue = m.Value;
                        break;
                }
            }
        }
        sv.Recalculate(additive, multiplicative, overrideValue);
    }
}
