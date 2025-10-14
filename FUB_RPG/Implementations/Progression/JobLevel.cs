using System;
using Fub.Enums;
using Fub.Interfaces.Progression;

namespace Fub.Implementations.Progression;

public sealed class JobLevel : IJobLevel
{
    private const int MaxLevel = 100;
    private int _level;
    private long _experience;
    private LevelCurveType _curveType;

    public ActorClass JobClass { get; }
    public int Level => _level;
    public long Experience => _experience;
    public long ExperienceToNextLevel => CalculateExperienceForLevel(_level + 1);

    public JobLevel(ActorClass jobClass, int startLevel = 1, long startExp = 0, LevelCurveType curveType = LevelCurveType.Custom)
    {
        JobClass = jobClass;
        _level = Math.Clamp(startLevel, 1, MaxLevel);
        _experience = startExp;
        _curveType = curveType;
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
        if (level <= 1) return 0;
        
        // Use custom curve by default: Level 2 = 100, Level 3 = 175, Level 4 = 250, etc.
        switch (_curveType)
        {
            case LevelCurveType.Linear:
                return CalculateLinearXP(level);
            case LevelCurveType.Moderate:
                return CalculateModerateXP(level);
            case LevelCurveType.Steep:
                return (long)(100 * Math.Pow(level, 2.5));
            case LevelCurveType.Custom:
            default:
                return CalculateCustomXP(level);
        }
    }

    private long CalculateLinearXP(int level)
    {
        // Cumulative XP: Level 2 = 100, Level 3 = 275 (100+175), Level 4 = 525 (100+175+250), etc.
        // Each level costs: 25 + level * 75
        long totalXp = 0;
        for (int i = 2; i <= level; i++)
        {
            totalXp += 25 + (i * 75); // Level 2 costs 175, Level 3 costs 250, etc.
        }
        return totalXp;
    }

    private long CalculateModerateXP(int level)
    {
        // Cumulative XP with balanced scaling
        long totalXp = 0;
        for (int i = 2; i <= level; i++)
        {
            totalXp += 50 + ((i - 1) * 50); // Level 2 costs 100, Level 3 costs 150, Level 4 costs 200
        }
        return totalXp;
    }

    private long CalculateCustomXP(int level)
    {
        // Your formula: Need 100 total XP to reach Level 2, 175 total to reach Level 3, etc.
        // This is CUMULATIVE, not per-level
        if (level <= 1) return 0;
        
        // Level 2 needs 100 XP total
        // Level 3 needs 175 XP total (75 more than level 2)
        // Level 4 needs 250 XP total (75 more than level 3)
        // Pattern: 100 + (level - 2) * 75
        return 100 + ((level - 2) * 75);
    }
}
