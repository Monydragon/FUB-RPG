using System;
using Fub.Interfaces.Player;

namespace Fub.Implementations.Player;

public sealed class PlayerProfile : IPlayerProfile
{
    public Guid ProfileId { get; } = Guid.NewGuid();
    public string DisplayName { get; }
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    public DateTime LastPlayedUtc { get; private set; } = DateTime.UtcNow;

    public PlayerProfile(string displayName)
    {
        DisplayName = displayName;
    }

    public void Touch() => LastPlayedUtc = DateTime.UtcNow;
}
