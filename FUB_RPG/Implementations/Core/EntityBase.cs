using System;
using Fub.Interfaces.Entities;

namespace Fub.Implementations.Core;

public abstract class EntityBase : IEntity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; protected set; }

    protected EntityBase(string name)
    {
        Name = name;
    }

    public override string ToString() => $"{Name} ({Id})";
}
