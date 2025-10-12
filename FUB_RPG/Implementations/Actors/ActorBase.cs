// Added
// Added
// Removed unnecessary using directives flagged by analyzer
// using Fub.Implementations.Inventory;
// using System.Linq;
// using System.Collections.Generic;
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
using Fub.Interfaces.Abilities;
using Fub.Implementations.Abilities;

// Use centralized mappings

namespace Fub.Implementations.Actors;

public abstract class ActorBase : EntityBase, IActor, IHasAbilityBook
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
    public IAbilityBook AbilityBook { get; }

    protected ActorBase(string name, Species species, ActorClass @class, int startX, int startY) : base(name)
    {
        Species = species;
        Class = @class;
        X = startX;
        Y = startY;
        JobSystem = new JobSystem();
        Inventory = new Fub.Implementations.Inventory.Inventory(1000);
        StatsInternal = InitializeStats(ActorStatPresets.Default());
        EquipmentManager = new EquipmentManager();
        AbilityBook = new AbilityBook();
        LearnStartingAbilities();
    }

    /// <summary>
    /// Constructor that accepts custom stat values for full control.
    /// Use ActorStatPresets for convenient defaults.
    /// </summary>
    protected ActorBase(string name, Species species, ActorClass @class, int startX, int startY, Dictionary<StatType, double> customStats) : base(name)
    {
        Species = species;
        Class = @class;
        X = startX;
        Y = startY;
        JobSystem = new JobSystem();
        Inventory = new Fub.Implementations.Inventory.Inventory(1000);
        StatsInternal = InitializeStats(customStats);
        EquipmentManager = new EquipmentManager();
        AbilityBook = new AbilityBook();
        LearnStartingAbilities();
    }

    private void LearnStartingAbilities()
    {
        var lvl = JobSystem.GetJobLevel(EffectiveClass).Level;
        foreach (var u in ClassAbilityLearnset.GetUnlocks(EffectiveClass))
        {
            if (u.Level <= lvl)
            {
                AbilityBook.Learn(u.Factory());
            }
        }
    }

    private StatsCollection InitializeStats(Dictionary<StatType, double> statValues)
    {
        var collection = new StatsCollection();
        collection.InitializeDefaults(statValues.Select(kvp => (kvp.Key, kvp.Value)));
        return collection;
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

    public bool TrySpend(StatType resource, double amount)
    {
        if (!StatsInternal.TryGetStat(resource, out var stat)) return false;
        var sv = (StatValue)stat;
        if (sv.Current < amount) return false;
        sv.ApplyDelta(-amount);
        return true;
    }
}
