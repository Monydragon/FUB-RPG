using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Scaling;

public interface IDifficultyScaler
{
    Difficulty Difficulty { get; }
    void ApplyActorScaling(IActor actor);
}

