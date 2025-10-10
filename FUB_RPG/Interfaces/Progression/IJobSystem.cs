using System.Collections.Generic;
using Fub.Enums;

namespace Fub.Interfaces.Progression;

/// <summary>
/// Manages job levels for an actor (FFXIV-style job system).
/// </summary>
public interface IJobSystem
{
    IReadOnlyDictionary<ActorClass, IJobLevel> JobLevels { get; }
    IJobLevel GetJobLevel(ActorClass jobClass);
    bool AddExperience(ActorClass jobClass, long amount);
    int GetHighestJobLevel();
}

