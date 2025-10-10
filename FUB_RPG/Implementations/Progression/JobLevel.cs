using System;
using Fub.Enums;
using Fub.Interfaces.Progression;

namespace Fub.Implementations.Progression;

public sealed class JobLevel : IJobLevel
{
    private const int MaxLevel = 100;
    private int _level;
    private long _experience;

    public ActorClass JobClass { get; }
    public int Level => _level;
    public long Experience => _experience;
    public long ExperienceToNextLevel => CalculateExperienceForLevel(_level + 1);

    public JobLevel(ActorClass jobClass, int startLevel = 1, long startExp = 0)
    {
        JobClass = jobClass;
        _level = Math.Clamp(startLevel, 1, MaxLevel);
        _experience = startExp;
    }

    public bool AddExperience(long amount)
    {
        if (_level >= MaxLevel) return false;
        
        _experience += amount;
        bool leveled = false;
        
        while (_level < MaxLevel && _experience >= ExperienceToNextLevel)
        {
            LevelUp();
            leveled = true;
        }
        
        return leveled;
    }

    public bool CanLevelUp() => _level < MaxLevel && _experience >= ExperienceToNextLevel;

    public void LevelUp()
    {
        if (_level >= MaxLevel) return;
        _level++;
    }

    private long CalculateExperienceForLevel(int level)
    {
        // Exponential curve similar to FFXIV
        return (long)(100 * Math.Pow(level, 2.5));
    }
}

