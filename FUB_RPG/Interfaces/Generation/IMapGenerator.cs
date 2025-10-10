using Fub.Interfaces.Map;
using Fub.Interfaces.Generation;

namespace Fub.Interfaces.Generation;

public interface IMapGenerator
{
    IMap Generate(IMapGenerationConfig config, IProceduralSeed seed);
}

