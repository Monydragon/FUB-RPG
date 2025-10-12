using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;

namespace Fub.Interfaces.Combat;

public interface ICombatAction
{
    Guid ActionId { get; }
    CombatActionType ActionType { get; }
    IActor Actor { get; }
    IActor? Target { get; }
    int Priority { get; }
    string Description { get; }
    IItem? Item { get; }
    object? CustomData { get; }
}
