using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Items.Equipment;
using Fub.Implementations.Items.Weapons;

namespace Fub.Implementations.Items.Equipment;

/// <summary>
/// Equipment manager that handles equipped items with tier-based stat bonuses
/// </summary>
public class EquipmentManager
{
    private readonly Dictionary<EquipmentSlot, IEquipment> _equippedItems = new();
    
    public IReadOnlyDictionary<EquipmentSlot, IEquipment> EquippedItems => _equippedItems;
    public decimal TotalEquipmentValue => _equippedItems.Values.Sum(item => item.BaseValue);
    public float TotalEquipmentWeight => _equippedItems.Values.Sum(item => item.Weight);

    public bool IsSlotOccupied(EquipmentSlot slot) => _equippedItems.ContainsKey(slot);

    public IEquipment? GetEquippedItem(EquipmentSlot slot)
    {
        return _equippedItems.GetValueOrDefault(slot);
    }

    public bool CanEquip(IEquipment equipment, int actorLevel, ActorClass actorClass)
    {
        // If requirements are optional, allow equip regardless of unmet gates
        if (equipment.RequirementsOptional)
            return true;
        
        // Check level requirement
        if (actorLevel < equipment.RequiredLevel) return false;
        
        // Check class restriction
        if (equipment.AllowedClasses.Any() && !equipment.AllowedClasses.Contains(actorClass))
            return false;
            
        // Check stat requirements (would need actor stats to fully implement)
        // For now, assume stat requirements are met
        
        return true;
    }

    public bool TryEquip(IEquipment equipment, int actorLevel, ActorClass actorClass)
    {
        if (!CanEquip(equipment, actorLevel, actorClass)) return false;

        // Handle two-handed weapons
        if (equipment.Slot == EquipmentSlot.MainHand && IsTwoHanded(equipment))
        {
            // Remove offhand item if equipped
            _equippedItems.Remove(EquipmentSlot.OffHand);
        }
        else if (equipment.Slot == EquipmentSlot.OffHand && IsMainHandTwoHanded())
        {
            // Can't equip offhand if main hand is two-handed
            return false;
        }

        _equippedItems[equipment.Slot] = equipment;
        return true;
    }

    public IEquipment? Unequip(EquipmentSlot slot)
    {
        if (_equippedItems.TryGetValue(slot, out var equipment))
        {
            _equippedItems.Remove(slot);
            return equipment;
        }
        return null;
    }

    public Dictionary<StatType, double> CalculateEquipmentBonuses()
    {
        var bonuses = new Dictionary<StatType, double>();
        
        foreach (var equipment in _equippedItems.Values)
        {
            // Calculate tier-based stat bonuses
            var tierMultiplier = (int)equipment.Tier * 2.0;
            var rarityMultiplier = equipment.Rarity switch
            {
                RarityTier.Common => 1.0,
                RarityTier.Uncommon => 1.2,
                RarityTier.Rare => 1.5,
                RarityTier.Epic => 2.0,
                RarityTier.Legendary => 2.8,
                RarityTier.Mythic => 4.0,
                _ => 1.0
            };

            // Add slot-specific bonuses
            var slotBonuses = GetSlotSpecificBonuses(equipment.Slot, tierMultiplier, rarityMultiplier);
            foreach (var bonus in slotBonuses)
            {
                if (bonuses.ContainsKey(bonus.Key))
                    bonuses[bonus.Key] += bonus.Value;
                else
                    bonuses[bonus.Key] = bonus.Value;
            }
        }
        
        return bonuses;
    }

    public List<IEquipment> GetEquipmentByTier(EquipmentTier tier)
    {
        return _equippedItems.Values.Where(e => e.Tier == tier).ToList();
    }

    public bool HasCompleteSet(EquipmentTier tier)
    {
        var essentialSlots = new[]
        {
            EquipmentSlot.MainHand,
            EquipmentSlot.Head,
            EquipmentSlot.Chest,
            EquipmentSlot.Legs,
            EquipmentSlot.Feet
        };

        return essentialSlots.All(slot => 
            _equippedItems.TryGetValue(slot, out var equipment) && equipment.Tier == tier);
    }

    public float CalculateSetBonus()
    {
        var tierCounts = new Dictionary<EquipmentTier, int>();
        
        foreach (var equipment in _equippedItems.Values)
        {
            if (tierCounts.ContainsKey(equipment.Tier))
                tierCounts[equipment.Tier]++;
            else
                tierCounts[equipment.Tier] = 1;
        }

        // Calculate set bonuses for having multiple items of same tier
        float totalBonus = 0f;
        foreach (var tierCount in tierCounts.Where(kvp => kvp.Value >= 3))
        {
            var setBonus = tierCount.Value switch
            {
                >= 6 => 0.25f, // 25% bonus for 6+ pieces
                >= 5 => 0.20f, // 20% bonus for 5+ pieces
                >= 4 => 0.15f, // 15% bonus for 4+ pieces
                >= 3 => 0.10f, // 10% bonus for 3+ pieces
                _ => 0f
            };
            totalBonus += setBonus * (int)tierCount.Key * 0.1f; // Higher tiers give better bonuses
        }

        return totalBonus;
    }

    private Dictionary<StatType, double> GetSlotSpecificBonuses(EquipmentSlot slot, double tierMultiplier, double rarityMultiplier)
    {
        var bonuses = new Dictionary<StatType, double>();
        var baseBonus = tierMultiplier * rarityMultiplier;

        switch (slot)
        {
            case EquipmentSlot.MainHand:
                bonuses[StatType.AttackPower] = baseBonus * 3.0;
                bonuses[StatType.Strength] = baseBonus * 1.5;
                break;
                
            case EquipmentSlot.OffHand:
                bonuses[StatType.AttackPower] = baseBonus * 1.5;
                bonuses[StatType.Armor] = baseBonus * 1.0;
                break;
                
            case EquipmentSlot.Head:
                bonuses[StatType.Armor] = baseBonus * 2.0;
                bonuses[StatType.Intellect] = baseBonus * 1.0;
                break;
                
            case EquipmentSlot.Chest:
                bonuses[StatType.Armor] = baseBonus * 3.0;
                bonuses[StatType.Health] = baseBonus * 2.0;
                break;
                
            case EquipmentSlot.Legs:
                bonuses[StatType.Armor] = baseBonus * 2.5;
                bonuses[StatType.Agility] = baseBonus * 1.5;
                break;
                
            case EquipmentSlot.Feet:
                bonuses[StatType.Armor] = baseBonus * 1.5;
                bonuses[StatType.Agility] = baseBonus * 2.0;
                break;
                
            case EquipmentSlot.Hands:
                bonuses[StatType.Armor] = baseBonus * 1.5;
                bonuses[StatType.Technical] = baseBonus * 2.0;
                break;
                
            case EquipmentSlot.Cloak:
                bonuses[StatType.Armor] = baseBonus * 1.8;
                bonuses[StatType.Strength] = baseBonus * 1.2;
                break;
                
            case EquipmentSlot.Ring:
                bonuses[StatType.SpellPower] = baseBonus * 1.5;
                bonuses[StatType.Mana] = baseBonus * 2.0;
                break;
                
            case EquipmentSlot.Amulet:
                bonuses[StatType.SpellPower] = baseBonus * 2.0;
                bonuses[StatType.Health] = baseBonus * 1.5;
                break;
            
            case EquipmentSlot.Belt:
                bonuses[StatType.Vitality] = baseBonus * 1.5;
                bonuses[StatType.Armor] = baseBonus * 1.0;
                break;
        }

        return bonuses;
    }

    private bool IsTwoHanded(IEquipment equipment)
    {
        // Check if weapon is two-handed based on weapon type
        if (equipment is not Weapon weapon) return false;
        
        return weapon.WeaponType switch
        {
            WeaponType.Staff => true,
            WeaponType.Bow => true,
            WeaponType.Crossbow => true,
            WeaponType.Greataxe => true,
            WeaponType.Greatsword => true,
            WeaponType.Scythe => true,
            WeaponType.Hammer => true, // Assuming large hammers are two-handed
            _ => false
        };
    }

    private bool IsMainHandTwoHanded()
    {
        var mainHand = GetEquippedItem(EquipmentSlot.MainHand);
        return mainHand != null && IsTwoHanded(mainHand);
    }
}
