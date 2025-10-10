using Fub.Enums;
using Fub.Interfaces.Items.Equipment;

// Added

namespace Fub.Interfaces.Items.Weapons;

public interface IWeapon : IEquipment
{
    WeaponType WeaponType { get; }
    double MinDamage { get; }
    double MaxDamage { get; }
    double AttackSpeed { get; } // attacks per second baseline
    DamageType DefaultDamageType { get; }
}
