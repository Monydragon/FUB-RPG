using Fub.Enums;

namespace Fub.Implementations.Items;

/// <summary>
/// Represents a consumable item with specific effects (healing, mana restoration, buffs, etc.)
/// </summary>
public sealed class ConsumableItem : ItemBase
{
    public ResourceType? PrimaryResource { get; }
    public int RestoreAmount { get; }
    public float RestorePercentage { get; }
    public bool IsPercentageBased { get; }
    public bool RemovesStatusEffects { get; }
    public bool RestoresAllResources { get; }
    public string? SpecialEffect { get; }
    
    public ConsumableItem(
        string name,
        RarityTier rarity,
        ResourceType? primaryResource = null,
        int restoreAmount = 0,
        float restorePercentage = 0f,
        bool isPercentageBased = false,
        bool removesStatusEffects = false,
        bool restoresAllResources = false,
        string? specialEffect = null,
        int maxStackSize = 99,
        decimal baseValue = 10m,
        float weight = 0.5f,
        string? description = null)
        : base(name, ItemType.Consumable, rarity)
    {
        PrimaryResource = primaryResource;
        RestoreAmount = restoreAmount;
        RestorePercentage = restorePercentage;
        IsPercentageBased = isPercentageBased;
        RemovesStatusEffects = removesStatusEffects;
        RestoresAllResources = restoresAllResources;
        SpecialEffect = specialEffect;
        Stackable = true;
        MaxStackSize = maxStackSize;
        BaseValue = baseValue;
        Weight = weight;
        Description = description ?? GenerateDescription();
    }

    private string GenerateDescription()
    {
        var desc = "";
        
        if (RestoresAllResources)
        {
            desc += "Fully restores all resources (HP, MP, TP). ";
        }
        else if (PrimaryResource.HasValue)
        {
            var resourceName = PrimaryResource.Value switch
            {
                ResourceType.Health => "HP",
                ResourceType.Mana => "MP",
                ResourceType.Stamina => "TP",
                _ => "Resource"
            };

            if (IsPercentageBased)
            {
                desc += $"Restores {RestorePercentage * 100:F0}% {resourceName}. ";
            }
            else
            {
                desc += $"Restores {RestoreAmount} {resourceName}. ";
            }
        }

        if (RemovesStatusEffects)
        {
            desc += "Removes negative status effects. ";
        }

        if (!string.IsNullOrEmpty(SpecialEffect))
        {
            desc += SpecialEffect;
        }

        return desc.Trim();
    }

    public override string ToString()
    {
        return $"{Name} ({Rarity}) - {Description}";
    }
}

