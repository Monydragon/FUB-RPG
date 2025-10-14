using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Interfaces.Items.Equipment;

/// <summary>
/// Base for any item that can be equipped in a gear slot.
/// </summary>
public interface IEquipment : IItem
{
    EquipmentSlot Slot { get; }
    int RequiredLevel { get; }
    IReadOnlyCollection<ActorClass> AllowedClasses { get; }
    IReadOnlyDictionary<StatType, double> StatRequirements { get; }
    EquipmentTier Tier { get; }
}
