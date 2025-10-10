using System.Collections.Generic;

namespace Fub.Interfaces.Effects;

public interface IEffectContainer
{
    IReadOnlyList<IEffectInstance> ActiveEffects { get; }
    bool TryAdd(IEffectInstance effect);
    bool Remove(Guid instanceId);
    void TickTurn();
}

