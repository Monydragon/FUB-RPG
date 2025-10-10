using Fub.Interfaces.Generation;

namespace Fub.Implementations.Generation;

public sealed class ProceduralSeed : IProceduralSeed
{
    public int Value { get; }
    public ProceduralSeed(int value) => Value = value;
    public override string ToString() => Value.ToString();
}
