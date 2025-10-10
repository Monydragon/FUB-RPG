using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;
// Added for actor context
using Fub.Interfaces.Items.Equipment;

namespace Fub.Interfaces.Items.Equipment;

public interface IEquipmentManager
{
    IReadOnlyDictionary<EquipmentSlot, IEquipment?> Equipped { get; }
    bool CanEquip(IEquipment equipment); // Legacy simple
    bool TryEquip(IEquipment equipment, out IEquipment? replaced); // Legacy simple
    bool CanEquip(IEquipment equipment, IActor actor); // Added actor context
    bool TryEquip(IEquipment equipment, IActor actor, out IEquipment? replaced); // Added actor context
    bool TryUnequip(EquipmentSlot slot, out IEquipment? removed);
    IEquipment? Get(EquipmentSlot slot);
}
