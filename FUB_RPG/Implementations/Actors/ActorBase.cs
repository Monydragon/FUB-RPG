// Added
// Added
using Fub.Implementations.Inventory;
// Added
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Implementations.Items.Equipment;
using Fub.Implementations.Items.Weapons;
using Fub.Implementations.Progression;
using Fub.Implementations.Stats;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Combat;
using Fub.Interfaces.Inventory;
using Fub.Interfaces.Items.Equipment;
using Fub.Interfaces.Items.Weapons;
using Fub.Interfaces.Progression;
using Fub.Interfaces.Stats;

// Use centralized mappings

namespace Fub.Implementations.Actors;

public abstract class ActorBase : EntityBase, IActor
{
    private Func<int,int,bool>? _canMove;
    protected readonly StatsCollection StatsInternal;
    protected readonly EquipmentManager EquipmentManager;

    public Species Species { get; protected set; }
    public ActorClass Class { get; protected set; }
    public int Level => JobSystem.GetJobLevel(EffectiveClass).Level;
    public long Experience => JobSystem.GetJobLevel(EffectiveClass).Experience;
    public IJobSystem JobSystem { get; }

    public int X { get; private set; }
    public int Y { get; private set; }

    public IInventory Inventory { get; }
    public IReadOnlyDictionary<StatType, IStatValue> AllStats => StatsInternal.AllStats;

    public ActorClass EffectiveClass => EvaluateEffectiveClass();

    protected ActorBase(string name, Species species, ActorClass @class, int startX, int startY) : base(name)
    {
        Species = species;
        Class = @class;
        X = startX;
        Y = startY;
        JobSystem = new JobSystem();
        Inventory = new Inventory.Inventory(20); // Corrected
        StatsInternal = new StatsCollection()
            .InitializeDefaults(new[]
            {
                (StatType.Health, 100d),
                (StatType.Mana, 50d),
                (StatType.Stamina, 50d),
                (StatType.Strength, 10d),
                (StatType.Agility, 10d),
                (StatType.Intellect, 10d),
                (StatType.Vitality, 10d)
            });
        EquipmentManager = new EquipmentManager();
    }

    // Make GetEquipped accessible
    public IEquipment? GetEquipped(EquipmentSlot slot) => EquipmentManager.Get(slot);

    // Add method to equip items
    public bool TryEquip(IEquipment equipment, out IEquipment? replaced)
    {
        return EquipmentManager.TryEquip(equipment, this, out replaced);
    }

    public void SetMovementValidator(Func<int,int,bool> validator) => _canMove = validator;

    public bool TryMove(int dx, int dy)
    {
        int nx = X + dx;
        int ny = Y + dy;
        if (_canMove is not null && !_canMove(nx, ny))
            return false;
        X = nx;
        Y = ny;
        return true;
    }

    public IStatValue GetStat(StatType type) => StatsInternal.GetStat(type);
    public bool TryGetStat(StatType type, out IStatValue value) => StatsInternal.TryGetStat(type, out value);

    public int TakeDamage(IDamagePacket packet)
    {
        var health = (StatValue)StatsInternal.GetStat(StatType.Health);
        double total = packet.Components.Sum(c => c.Amount);
        health.ApplyDelta(-total);
        return (int)health.Current;
    }

    public int Heal(int amount)
    {
        var health = (StatValue)StatsInternal.GetStat(StatType.Health);
        health.ApplyDelta(amount);
        return (int)health.Current;
    }

    public void Teleport(int x, int y)
    {
        X = x;
        Y = y;
    }

    internal void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    private ActorClass EvaluateEffectiveClass()
    {
        var mainHand = GetEquipped(EquipmentSlot.MainHand) as IWeapon;
        if (mainHand == null) return Class; // fallback when no weapon
        return ClassWeaponMappings.TryGetClassForWeaponType(mainHand.WeaponType, out var mapped)
            ? mapped
            : Class;
    }
}
