using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items.Equipment;

// Added

namespace Fub.Implementations.Items.Equipment;

public sealed class EquipmentManager : IEquipmentManager
{
    private readonly Dictionary<EquipmentSlot, IEquipment?> _equipped = new();

    public EquipmentManager()
    {
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            _equipped[slot] = null;
    }

    public IReadOnlyDictionary<EquipmentSlot, IEquipment?> Equipped => _equipped;

    public bool CanEquip(IEquipment equipment) => _equipped.ContainsKey(equipment.Slot);

    public bool TryEquip(IEquipment equipment, out IEquipment? replaced)
    {
        replaced = null;
        if (!CanEquip(equipment)) return false;
        replaced = _equipped[equipment.Slot];
        _equipped[equipment.Slot] = equipment;
        return true;
    }

    // Actor-aware versions
    public bool CanEquip(IEquipment equipment, IActor actor)
    {
        if (!CanEquip(equipment)) return false;
        if (actor.Level < equipment.RequiredLevel) return false;
        // Class requirement: allow if actor's effective OR base class in allowed list
        if (equipment.AllowedClasses.Count > 0 &&
            !equipment.AllowedClasses.Contains(actor.EffectiveClass) &&
            !equipment.AllowedClasses.Contains(actor.Class))
            return false;
        // Stat requirements
        foreach (var req in equipment.StatRequirements)
        {
            if (!actor.TryGetStat(req.Key, out var stat) || stat.Modified < req.Value)
                return false;
        }
        return true;
    }

    public bool TryEquip(IEquipment equipment, IActor actor, out IEquipment? replaced)
    {
        replaced = null;
        if (!CanEquip(equipment, actor)) return false;
        replaced = _equipped[equipment.Slot];
        _equipped[equipment.Slot] = equipment;
        return true;
    }

    public bool TryUnequip(EquipmentSlot slot, out IEquipment? removed)
    {
        removed = null;
        if (!_equipped.TryGetValue(slot, out var existing) || existing is null) return false;
        removed = existing;
        _equipped[slot] = null;
        return true;
    }

    public IEquipment? Get(EquipmentSlot slot) => _equipped.TryGetValue(slot, out var eq) ? eq : null;
}
