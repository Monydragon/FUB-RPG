using System;
using Fub.Enums;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Combat;
using Fub.Interfaces.Items;

namespace Fub.Implementations.Combat;

public sealed class CombatAction : ICombatAction
{
    public Guid ActionId { get; }
    public CombatActionType ActionType { get; }
    public IActor Actor { get; }
    public IActor? Target { get; }
    public int Priority { get; }
    public string Description { get; }
    public IItem? Item { get; }
    public object? CustomData { get; }

    public CombatAction(
        CombatActionType actionType,
        IActor actor,
        IActor? target = null,
        int priority = 0,
        string? description = null,
        IItem? item = null,
        object? customData = null)
    {
        ActionId = Guid.NewGuid();
        ActionType = actionType;
        Actor = actor;
        Target = target;
        Priority = priority;
        Description = description ?? actionType.ToString();
        Item = item;
        CustomData = customData;
    }
}

