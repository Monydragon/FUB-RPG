using System.Collections.Generic;

namespace Fub.Interfaces.Config;

public interface IGameSettingsProvider
{
    IEnumerable<IGameSetting> AllSettings { get; }
    IGameSetting? Get(string key);
    bool TryGet(string key, out IGameSetting setting);
}

