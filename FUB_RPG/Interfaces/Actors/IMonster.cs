using Fub.Interfaces.Actors;

namespace Fub.Interfaces.Actors;

/// <summary>
/// A non-player hostile actor; may include AI hooks later.
/// </summary>
public interface IMonster : IActor
{
    /// <summary>Determines if monster is elite (affects scaling externally).</summary>
    bool IsElite { get; }
    /// <summary>Determines if monster is a boss-tier entity.</summary>
    bool IsBoss { get; }
}

