namespace Fub.Interfaces.Player;

public interface IPlayerProfile
{
    Guid ProfileId { get; }
    string DisplayName { get; }
    DateTime CreatedUtc { get; }
    DateTime LastPlayedUtc { get; }
}

