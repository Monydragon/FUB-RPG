using System;

namespace Fub.Implementations.Progression;

/// <summary>
/// Represents the current state of an experience bar with progress tracking
/// </summary>
public class ExperienceBar
{
    private long _currentXp;
    private long _xpForCurrentLevel;
    private long _xpForNextLevel;
    private int _currentLevel;

    public int CurrentLevel => _currentLevel;
    public long CurrentXp => _currentXp;
    public long XpForCurrentLevel => _xpForCurrentLevel;
    public long XpForNextLevel => _xpForNextLevel;
    
    /// <summary>
    /// XP needed from current level to next level (resets each level)
    /// </summary>
    public long XpNeededForLevel => _xpForNextLevel - _xpForCurrentLevel;
    
    /// <summary>
    /// Current progress within this level (0 to XpNeededForLevel)
    /// </summary>
    public long CurrentLevelProgress => _currentXp - _xpForCurrentLevel;
    
    /// <summary>
    /// Percentage progress within current level (0.0 to 1.0)
    /// </summary>
    public double PercentageInLevel => XpNeededForLevel > 0 
        ? (double)CurrentLevelProgress / XpNeededForLevel 
        : 0.0;

    /// <summary>
    /// Has reached max level (no more XP needed)
    /// </summary>
    public bool IsMaxLevel => _xpForNextLevel == _xpForCurrentLevel;

    public ExperienceBar(int currentLevel, long currentXp, long xpForCurrentLevel, long xpForNextLevel)
    {
        _currentLevel = currentLevel;
        _currentXp = currentXp;
        _xpForCurrentLevel = xpForCurrentLevel;
        _xpForNextLevel = xpForNextLevel;
    }

    /// <summary>
    /// Updates the bar state
    /// </summary>
    public void Update(int currentLevel, long currentXp, long xpForCurrentLevel, long xpForNextLevel)
    {
        _currentLevel = currentLevel;
        _currentXp = currentXp;
        _xpForCurrentLevel = xpForCurrentLevel;
        _xpForNextLevel = xpForNextLevel;
    }

    /// <summary>
    /// Gets a visual representation of the bar
    /// </summary>
    public string GetBarString(int width = 30)
    {
        var filled = (int)(PercentageInLevel * width);
        var empty = width - filled;
        
        return "[" + new string('█', filled) + new string('░', empty) + "]";
    }

    /// <summary>
    /// Gets formatted progress text
    /// </summary>
    public string GetProgressText()
    {
        if (IsMaxLevel)
            return "MAX LEVEL";
        
        return $"{CurrentLevelProgress}/{XpNeededForLevel} XP ({PercentageInLevel:P0})";
    }
}

