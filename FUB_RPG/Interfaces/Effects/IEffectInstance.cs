using System;
using Fub.Enums;

namespace Fub.Interfaces.Effects;

/// <summary>
/// Runtime stateful instance of an effect derived from a blueprint.
/// </summary>
public interface IEffectInstance
{
    Guid InstanceId { get; }
    IEffectBlueprint Blueprint { get; }
    Guid SourceId { get; }
    Guid TargetId { get; }
    int RemainingTurns { get; }
    int Stacks { get; }
    bool Expired { get; }
}
