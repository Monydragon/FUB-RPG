using System;
using Fub.Interfaces.Random;

namespace Fub.Implementations.Random;

public sealed class RandomSource : IRandomSource
{
    private System.Random _rng;
    public int CurrentSeed { get; private set; }

    public RandomSource(int seed)
    {
        CurrentSeed = seed;
        _rng = new System.Random(seed);
    }

    public int NextInt(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
    public double NextDouble() => _rng.NextDouble();

    public bool NextBool(double trueProbability = 0.5)
        => _rng.NextDouble() < trueProbability;

    public void Reseed(int seed)
    {
        CurrentSeed = seed;
        _rng = new System.Random(seed);
    }
}
