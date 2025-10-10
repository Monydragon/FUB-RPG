using System.Collections.Generic;
using System.Linq;
using Fub.Interfaces.Combat;

namespace Fub.Implementations.Combat;

public sealed class DamagePacket : IDamagePacket
{
    public IReadOnlyList<IDamageComponent> Components { get; }
    public Guid SourceId { get; }
    public Guid? AbilityId { get; }
    public bool IsCritical { get; }

    public DamagePacket(Guid sourceId, IEnumerable<IDamageComponent> components, bool isCritical = false, Guid? abilityId = null)
    {
        SourceId = sourceId;
        AbilityId = abilityId;
        Components = components.ToList();
        IsCritical = isCritical;
    }
}
