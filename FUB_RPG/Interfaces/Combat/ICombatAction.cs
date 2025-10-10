using System;
using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Combat;

public interface ICombatAction
{
    Guid ActionId { get; }
    CombatActionType ActionType { get; }
    IActor Actor { get; }
    IActor? Target { get; }
    int Priority { get; }
    string Description { get; }
}

