namespace Fub.Interfaces.Random;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    double NextDouble();
    bool NextBool(double trueProbability = 0.5);
    void Reseed(int seed);
    int CurrentSeed { get; }
}

