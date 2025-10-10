using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Interfaces.Progression;

namespace Fub.Implementations.Progression;

public sealed class JobSystem : IJobSystem
{
    private readonly Dictionary<ActorClass, IJobLevel> _jobLevels = new();

    public IReadOnlyDictionary<ActorClass, IJobLevel> JobLevels => _jobLevels;

    public JobSystem()
    {
        // Initialize all job classes at level 1
        foreach (ActorClass jobClass in Enum.GetValues<ActorClass>())
        {
            _jobLevels[jobClass] = new JobLevel(jobClass, 1, 0);
        }
    }

    public IJobLevel GetJobLevel(ActorClass jobClass)
    {
        if (_jobLevels.TryGetValue(jobClass, out var jobLevel))
            return jobLevel;
        
        // Create if missing
        var newJobLevel = new JobLevel(jobClass, 1, 0);
        _jobLevels[jobClass] = newJobLevel;
        return newJobLevel;
    }

    public bool AddExperience(ActorClass jobClass, long amount)
    {
        var jobLevel = GetJobLevel(jobClass);
        return jobLevel.AddExperience(amount);
    }

    public int GetHighestJobLevel()
    {
        return _jobLevels.Values.Max(j => j.Level);
    }
}