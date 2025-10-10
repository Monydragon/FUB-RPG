using Fub.Enums;

namespace Fub.Interfaces.Progression;

/// <summary>
/// Tracks experience and level for a specific job/class.
/// </summary>
public interface IJobLevel
{
    ActorClass JobClass { get; }
    int Level { get; }
    long Experience { get; }
    long ExperienceToNextLevel { get; }
    bool AddExperience(long amount);
    bool CanLevelUp();
    void LevelUp();
}

