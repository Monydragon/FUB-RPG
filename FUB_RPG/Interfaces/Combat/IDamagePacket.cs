using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Interfaces.Combat;

/// <summary>
/// Represents an immutable bundle of damage components delivered in a single hit.
/// </summary>
public interface IDamagePacket
{
    IReadOnlyList<IDamageComponent> Components { get; }
    Guid SourceId { get; }
    Guid? AbilityId { get; }
    bool IsCritical { get; }
}

