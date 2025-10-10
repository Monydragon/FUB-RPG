using Fub.Enums;

namespace Fub.Interfaces.Config;

public interface IGameSetting
{
    string Key { get; }
    string DisplayName { get; }
    string? Description { get; }
    GameSettingScope Scope { get; }
    SettingDataType DataType { get; }
    object? DefaultValue { get; }
    object? CurrentValue { get; }
}

