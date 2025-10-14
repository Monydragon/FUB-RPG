using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Items;
using Fub.Interfaces.Items.Equipment;

namespace Fub.Implementations.Items.Equipment;

public sealed class Armor : ItemBase, IEquipment
{
    public EquipmentSlot Slot { get; }
    public int RequiredLevel { get; }
    public IReadOnlyCollection<ActorClass> AllowedClasses { get; }
    public IReadOnlyDictionary<StatType, double> StatRequirements { get; }

    public EquipmentTier Tier { get; }
    public ArmorType ArmorType { get; }

    public Armor(
        string name,
        EquipmentSlot slot,
        RarityTier rarity,
        int requiredLevel = 1,
        IEnumerable<ActorClass>? allowedClasses = null,
        IReadOnlyDictionary<StatType, double>? statRequirements = null,
        EquipmentTier tier = EquipmentTier.Simple,
        ArmorType armorType = ArmorType.Leather)
        : base(name, ItemType.Armor, rarity)
    {
        Slot = slot;
        RequiredLevel = requiredLevel;
        AllowedClasses = allowedClasses?.ToList() ?? new List<ActorClass>(System.Enum.GetValues<ActorClass>());
        StatRequirements = statRequirements ?? new Dictionary<StatType, double>();
        Tier = tier;
        ArmorType = armorType;

        // Set appropriate base value based on tier and rarity
        BaseValue = CalculateBaseValue(tier, rarity, slot);
        Weight = CalculateWeight(tier, armorType, slot);
    }

    private decimal CalculateBaseValue(EquipmentTier tier, RarityTier rarity, EquipmentSlot slot)
    {
        var baseTierValue = (int)tier * 10; // Simple = 10, Fine = 20, etc.
        var rarityMultiplier = rarity switch
        {
            RarityTier.Common => 1.0m,
            RarityTier.Uncommon => 1.5m,
            RarityTier.Rare => 2.5m,
            RarityTier.Epic => 4.0m,
            RarityTier.Legendary => 7.0m,
            RarityTier.Mythic => 12.0m,
            _ => 1.0m
        };
        
        var slotMultiplier = slot switch
        {
            EquipmentSlot.Chest => 2.0m,
            EquipmentSlot.Legs => 1.5m,
            EquipmentSlot.Head or EquipmentSlot.Feet => 1.2m,
            EquipmentSlot.Hands => 1.0m,
            EquipmentSlot.OffHand => 1.3m, // Shield
            _ => 1.0m
        };

        return baseTierValue * rarityMultiplier * slotMultiplier;
    }

    private float CalculateWeight(EquipmentTier tier, ArmorType armorType, EquipmentSlot slot)
    {
        var baseWeight = armorType switch
        {
            ArmorType.Cloth => 0.5f,
            ArmorType.Leather => 2.0f,
            ArmorType.Mail => 4.0f,
            ArmorType.Plate => 8.0f,
            ArmorType.Shield => 3.0f,
            _ => 1.0f
        };

        var slotMultiplier = slot switch
        {
            EquipmentSlot.Chest => 2.0f,
            EquipmentSlot.Legs => 1.5f,
            EquipmentSlot.Head => 1.0f,
            EquipmentSlot.Feet => 1.2f,
            EquipmentSlot.Hands => 0.8f,
            EquipmentSlot.OffHand => 1.0f,
            _ => 1.0f
        };

        return baseWeight * slotMultiplier * (1.0f + (int)tier * 0.1f);
    }
}
