using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Progression;

public interface IExperienceCalculator
{
    long GetExperienceForLevel(int level, LevelCurveType curveType);
    int AddExperience(IActor actor, long amount, ExperienceSourceType sourceType);
}

