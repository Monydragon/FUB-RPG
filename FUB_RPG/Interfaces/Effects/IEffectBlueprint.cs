using Fub.Enums;

namespace Fub.Interfaces.Effects;

/// <summary>
/// Immutable static description of an effect. Instances are runtime stateful versions.
/// </summary>
public interface IEffectBlueprint
{
    Guid Id { get; }
    string Name { get; }
    string? Description { get; }
    EffectType Type { get; }
    EffectStackBehavior StackBehavior { get; }
    int? MaxStacks { get; }
    int? DurationTurns { get; }
    bool IsDispellable { get; }
}

