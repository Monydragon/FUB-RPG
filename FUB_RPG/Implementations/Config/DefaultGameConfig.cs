using Fub.Enums;
using Fub.Interfaces.Config;

namespace Fub.Implementations.Config;

public sealed class DefaultGameConfig : IGameConfig
{
    // UI / Rendering
    public int UiReservedVerticalLines { get; init; } = 28;
    public int LogMaxExpandedRows { get; init; } = 8;
    public int LogMaxEntries { get; init; } = 200;
    public int CellWidthChars { get; init; } = 8; 
    public int MaxViewportWidth { get; init; } = 120;
    public int MaxViewportHeight { get; init; } = 35;
    public int MinViewportWidth { get; init; } = 15;
    public int MinViewportHeight { get; init; } = 10;

    // World / Exploration
    public int RespawnIntervalSteps { get; init; } = 30;
    public int RespawnMaxNew { get; init; } = 3;
    public int RespawnMinDistanceFromLeader { get; init; } = 6;

    // Resource regen per step
    public double RegenMpPercentPerStep { get; init; } = 0.02; // 2%
    public double RegenTpPercentPerStep { get; init; } = 0.03; // 3%

    // Search action
    public int SearchFindGoldChancePercent { get; init; } = 30;
    public int SearchFindPassageChancePercent { get; init; } = 20; // 30-50 window previously
    public int SearchGoldMin { get; init; } = 5;
    public int SearchGoldMax { get; init; } = 21; // exclusive upper bound behavior kept

    // Chests
    public int ChestGoldMin { get; init; } = 10;
    public int ChestGoldMax { get; init; } = 51; // exclusive upper bound

    // Coins on map
    public int CoinPickupMin { get; init; } = 1;
    public int CoinPickupMax { get; init; } = 11; // exclusive upper bound

    // Starter gear
    public double StartingWeaponMinDamage { get; init; } = 3;
    public double StartingWeaponMaxDamage { get; init; } = 6;
    public double StartingWeaponAttackSpeed { get; init; } = 1.0;
    public EquipmentTier StartingWeaponTier { get; init; } = EquipmentTier.Simple;

    // Economy
    public int StartingGold { get; init; } = 50;
}
