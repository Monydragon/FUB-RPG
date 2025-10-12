using Fub.Enums;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Abilities
{
    // Placeholder/job-themed abilities for all classes to support learnsets.
    // Keep them lightweight; combat logic treats Damage as damage and others as support/heal.

    // Melee / Tanks / Physical
    public sealed class ClericSmite : AbilityBase { public ClericSmite() : base("Cleric's Smite", "Holy strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class ClericHeal : AbilityBase { public ClericHeal() : base("Prayer Heal", "Heal an ally.", AbilityCategory.Heal, AbilityTargetType.SingleAlly, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class DarkCleave : AbilityBase { public DarkCleave() : base("Dark Cleave", "Shadow-infused slash.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class AbyssalDrain : AbilityBase { public AbyssalDrain() : base("Abyssal Drain", "Leech vitality.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class RageSlash : AbilityBase { public RageSlash() : base("Rage Slash", "Ferocious strike.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 16, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class WarCry : AbilityBase { public WarCry() : base("War Cry", "Bolster self.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class ChiBurst : AbilityBase { public ChiBurst() : base("Chi Burst", "Channel inner force.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Meditation : AbilityBase { public Meditation() : base("Meditation", "Center the mind.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class ShadowStrike : AbilityBase { public ShadowStrike() : base("Shadow Strike", "Ninja art.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 15, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class SmokeBomb : AbilityBase { public SmokeBomb() : base("Smoke Bomb", "Evasive veil.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class HarvestMoon : AbilityBase { public HarvestMoon() : base("Harvest Moon", "Reaper slice.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class SoulShield : AbilityBase { public SoulShield() : base("Soul Shield", "Wreathe in souls.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    // Ranged
    public sealed class PowerShot : AbilityBase { public PowerShot() : base("Power Shot", "Charged arrow.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class HunterMark : AbilityBase { public HunterMark() : base("Hunter's Mark", "Focus target.", AbilityCategory.Support, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Drill : AbilityBase { public Drill() : base("Drill", "Piercing shot.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 16, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class AutomatonQueen : AbilityBase { public AutomatonQueen() : base("Automaton", "Deploy helper.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class FanDance : AbilityBase { public FanDance() : base("Fan Dance", "Graceful strikes.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class DancePartner : AbilityBase { public DancePartner() : base("Dance Partner", "Inspiring step.", AbilityCategory.Support, AbilityTargetType.SingleAlly, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class PiercingArrow : AbilityBase { public PiercingArrow() : base("Piercing Arrow", "Bardic shot.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class BattleHymn : AbilityBase { public BattleHymn() : base("Battle Hymn", "Rousing song.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    // Magic / Support
    public sealed class Entangle : AbilityBase { public Entangle() : base("Entangle", "Roots bind the foe.", AbilityCategory.Support, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class NatureMend : AbilityBase { public NatureMend() : base("Nature's Mend", "Restore vitality.", AbilityCategory.Heal, AbilityTargetType.SingleAlly, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class ArcaneBolt : AbilityBase { public ArcaneBolt() : base("Arcane Bolt", "Raw arcana.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class ChaosBurst : AbilityBase { public ChaosBurst() : base("Chaos Burst", "Destructive surge.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class EldritchBlast : AbilityBase { public EldritchBlast() : base("Eldritch Blast", "Warlock staple.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 11, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Hex : AbilityBase { public Hex() : base("Hex", "Weaken target.", AbilityCategory.Support, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Fira : AbilityBase { public Fira() : base("Fira", "Potent fire.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Thundara : AbilityBase { public Thundara() : base("Thundara", "Potent lightning.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Adloquium : AbilityBase { public Adloquium() : base("Adloquium", "Shielding heal.", AbilityCategory.Heal, AbilityTargetType.SingleAlly, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Biolysis : AbilityBase { public Biolysis() : base("Biolysis", "Aether damage.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Benefic : AbilityBase { public Benefic() : base("Benefic", "Starry blessing.", AbilityCategory.Heal, AbilityTargetType.SingleAlly, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class DrawCard : AbilityBase { public DrawCard() : base("Draw", "Fate's boon.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Kardia : AbilityBase { public Kardia() : base("Kardia", "Harmonize ether.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Dosis : AbilityBase { public Dosis() : base("Dosis", "Noulith beam.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Riposte : AbilityBase { public Riposte() : base("Riposte", "Fencing counter.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Verfire : AbilityBase { public Verfire() : base("Verfire", "Red magic.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Needles1000 : AbilityBase { public Needles1000() : base("1000 Needles", "Blue magic.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 15, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Mimicry : AbilityBase { public Mimicry() : base("Mimicry", "Copy a trick.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class SummonCarbuncle : AbilityBase { public SummonCarbuncle() : base("Summon Carbuncle", "Egia aid.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Ruin : AbilityBase { public Ruin() : base("Ruin", "Ruinous bolt.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class RaiseSkeleton : AbilityBase { public RaiseSkeleton() : base("Raise Skeleton", "Call bones.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class ShadowBolt : AbilityBase { public ShadowBolt() : base("Shadow Bolt", "Necrotic blast.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class DeployTurret : AbilityBase { public DeployTurret() : base("Deploy Turret", "Gadgeteer trick.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 14, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class AlchemicalShot : AbilityBase { public AlchemicalShot() : base("Alchemical Shot", "Infused round.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 12, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    // Crafting / Gathering
    public sealed class MasterCraft : AbilityBase { public MasterCraft() : base("Master Craft", "Careful crafting.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class RapidSynthesis : AbilityBase { public RapidSynthesis() : base("Rapid Synthesis", "Hasty work.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class TemperedStrike : AbilityBase { public TemperedStrike() : base("Tempered Strike", "Forge swing.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class Metalworking : AbilityBase { public Metalworking() : base("Metalworking", "Smithing focus.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class ShieldPolish : AbilityBase { public ShieldPolish() : base("Shield Polish", "Armor care.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class PlateBash : AbilityBase { public PlateBash() : base("Plate Bash", "Armor bash.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class GemCut : AbilityBase { public GemCut() : base("Gem Cut", "Precise cut.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class GleamShot : AbilityBase { public GleamShot() : base("Gleam Shot", "Shiny pellet.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class LeatherTanning : AbilityBase { public LeatherTanning() : base("Leather Tanning", "Craft focus.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class LashWhip : AbilityBase { public LashWhip() : base("Lash Whip", "Leather lash.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Weave : AbilityBase { public Weave() : base("Weave", "Thread craft.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class ThreadShot : AbilityBase { public ThreadShot() : base("Thread Shot", "String bolt.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class Transmute : AbilityBase { public Transmute() : base("Transmute", "Alchemical craft.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class AcidFlask : AbilityBase { public AcidFlask() : base("Acid Flask", "Corrosive throw.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Mana, 10, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class SavoryMeal : AbilityBase { public SavoryMeal() : base("Savory Meal", "Cook's boon.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class FryingPanWhack : AbilityBase { public FryingPanWhack() : base("Pan Whack", "Kitchen smack.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class PickaxeSwing : AbilityBase { public PickaxeSwing() : base("Pickaxe Swing", "Strike the vein.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class StoneSense : AbilityBase { public StoneSense() : base("Stone Sense", "Survey terrain.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class SickleSwipe : AbilityBase { public SickleSwipe() : base("Sickle Swipe", "Harvest cut.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class HerbLore : AbilityBase { public HerbLore() : base("Herb Lore", "Plant knowledge.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }

    public sealed class HookCast : AbilityBase { public HookCast() : base("Hook Cast", "Snag catch.", AbilityCategory.Damage, AbilityTargetType.SingleEnemy, AbilityCostType.Technical, 8, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
    public sealed class FisherLuck : AbilityBase { public FisherLuck() : base("Fisher's Luck", "Tide's favor.", AbilityCategory.Support, AbilityTargetType.Self, AbilityCostType.Technical, 6, 0) {} public override IAbilityExecutionResult Execute(IActor u, IAbilityContext c) => new AbilityExecutionResult(true); }
}
