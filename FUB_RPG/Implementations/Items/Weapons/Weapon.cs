using System;
using System.Collections.Generic;
using Fub.Implementations.Items;
using Fub.Interfaces.Items.Equipment;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Items.Weapons;

namespace Fub.Implementations.Items.Weapons;

public sealed class Weapon : ItemBase, IWeapon
{
    public WeaponType WeaponType { get; }
    public double MinDamage { get; }
    public double MaxDamage { get; }
    public double AttackSpeed { get; }
    public DamageType DefaultDamageType { get; }

    public int RequiredLevel { get; }
    public IReadOnlyCollection<ActorClass> AllowedClasses { get; }
    public IReadOnlyDictionary<StatType, double> StatRequirements { get; }
    public EquipmentSlot Slot { get; } // Changed to getter only

    public Weapon(
        string name,
        WeaponType weaponType,
        DamageType dmgType,
        double min,
        double max,
        double atkSpeed,
        RarityTier rarity,
        EquipmentSlot slot,
        int requiredLevel = 1,
        IEnumerable<ActorClass>? allowedClasses = null,
        IReadOnlyDictionary<StatType, double>? statRequirements = null)
        : base(name, Enums.ItemType.Weapon, rarity)
    {
        WeaponType = weaponType;
        DefaultDamageType = dmgType;
        MinDamage = min;
        MaxDamage = max;
        AttackSpeed = atkSpeed;
        Slot = slot;
        RequiredLevel = requiredLevel;
        AllowedClasses = allowedClasses?.ToList() ?? new List<ActorClass>(System.Enum.GetValues<ActorClass>());
        StatRequirements = statRequirements ?? new Dictionary<StatType, double>();
    }

    public double RollDamage(System.Random rng) => rng.NextDouble() * (MaxDamage - MinDamage) + MinDamage;
}
