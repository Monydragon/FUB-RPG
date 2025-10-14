using Fub.Enums;

namespace Fub.Interfaces.Config;

public interface IGameConfig
{
    // UI / Rendering
    int UiReservedVerticalLines { get; }
    int LogMaxExpandedRows { get; }
    int LogMaxEntries { get; }
    int CellWidthChars { get; }
    int MaxViewportWidth { get; }
    int MaxViewportHeight { get; }
    int MinViewportWidth { get; }
    int MinViewportHeight { get; }

    // World / Exploration
    int RespawnIntervalSteps { get; }
    int RespawnMaxNew { get; }
    int RespawnMinDistanceFromLeader { get; }

    // Resource regen per step
    double RegenMpPercentPerStep { get; }
    double RegenTpPercentPerStep { get; }

    // Search action chances and rewards (percent chances, 0-100)
    int SearchFindGoldChancePercent { get; }
    int SearchFindPassageChancePercent { get; }
    int SearchGoldMin { get; }
    int SearchGoldMax { get; }

    // Chests
    int ChestGoldMin { get; }
    int ChestGoldMax { get; }

    // Coin item pickup on map (when stepping on a coin)
    int CoinPickupMin { get; }
    int CoinPickupMax { get; }

    // Starter gear defaults
    double StartingWeaponMinDamage { get; }
    double StartingWeaponMaxDamage { get; }
    double StartingWeaponAttackSpeed { get; }
    EquipmentTier StartingWeaponTier { get; }

    // Economy
    int StartingGold { get; }
}
