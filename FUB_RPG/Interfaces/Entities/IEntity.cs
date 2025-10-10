namespace Fub.Interfaces.Entities;

/// <summary>
/// Base entity in the game world. Every domain object should have a stable Id and human-readable Name.
/// </summary>
public interface IEntity
{
    Guid Id { get; }
    string Name { get; }
}

