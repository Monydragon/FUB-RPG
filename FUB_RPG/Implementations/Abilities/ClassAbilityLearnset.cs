using Fub.Enums;
using Fub.Interfaces.Abilities;

namespace Fub.Implementations.Abilities;

public static class ClassAbilityLearnset
{
    public record Unlock(int Level, Func<IAbility> Factory);

    private static readonly Dictionary<ActorClass, List<Unlock>> Map = new()
    {
        // General
        { ActorClass.Adventurer, new() { new Unlock(1, ()=> new FocusTechnique()) } },

        // Combat - Melee
        { ActorClass.Warrior, new() { new Unlock(1, ()=> new PowerStrike()), new Unlock(3, ()=> new GuardShout()) } },
        { ActorClass.Cleric, new() { new Unlock(1, ()=> new ClericSmite()), new Unlock(3, ()=> new ClericHeal()) } },
        { ActorClass.Paladin, new() { new Unlock(1, ()=> new Smite()), new Unlock(5, ()=> new HolyBlade()) } },
        { ActorClass.DarkKnight, new() { new Unlock(1, ()=> new DarkCleave()), new Unlock(4, ()=> new AbyssalDrain()) } },
        { ActorClass.Gunbreaker, new() { new Unlock(1, ()=> new BurstShot()) } },
        { ActorClass.Barbarian, new() { new Unlock(1, ()=> new RageSlash()), new Unlock(3, ()=> new WarCry()) } },
        { ActorClass.Monk, new() { new Unlock(1, ()=> new ChiBurst()), new Unlock(4, ()=> new Meditation()) } },
        { ActorClass.Samurai, new() { new Unlock(1, ()=> new Iaijutsu()) } },
        { ActorClass.Dragoon, new() { new Unlock(1, ()=> new JumpStrike()) } },
        { ActorClass.Ninja, new() { new Unlock(1, ()=> new ShadowStrike()), new Unlock(4, ()=> new SmokeBomb()) } },
        { ActorClass.Reaper, new() { new Unlock(1, ()=> new HarvestMoon()), new Unlock(4, ()=> new SoulShield()) } },
        { ActorClass.Rogue, new() { new Unlock(1, ()=> new Backstab()), new Unlock(4, ()=> new PoisonDagger()) } },

        // Combat - Ranged
        { ActorClass.Ranger, new() { new Unlock(1, ()=> new PowerShot()), new Unlock(3, ()=> new HunterMark()) } },
        { ActorClass.Machinist, new() { new Unlock(1, ()=> new Drill()), new Unlock(4, ()=> new AutomatonQueen()) } },
        { ActorClass.Dancer, new() { new Unlock(1, ()=> new FanDance()), new Unlock(3, ()=> new DancePartner()) } },
        { ActorClass.Bard, new() { new Unlock(1, ()=> new PiercingArrow()), new Unlock(3, ()=> new BattleHymn()) } },

        // Combat - Magic / Support
        { ActorClass.Druid, new() { new Unlock(1, ()=> new Entangle()), new Unlock(3, ()=> new NatureMend()) } },
        { ActorClass.Wizard, new() { new Unlock(1, ()=> new FireSpell()), new Unlock(3, ()=> new IceSpell()) } },
        { ActorClass.Sorcerer, new() { new Unlock(1, ()=> new ArcaneBolt()), new Unlock(4, ()=> new ChaosBurst()) } },
        { ActorClass.Warlock, new() { new Unlock(1, ()=> new EldritchBlast()), new Unlock(3, ()=> new Hex()) } },
        { ActorClass.BlackMage, new() { new Unlock(1, ()=> new Fira()), new Unlock(4, ()=> new Thundara()) } },
        { ActorClass.WhiteMage, new() { new Unlock(1, ()=> new HealSpell()), new Unlock(4, ()=> new CureAllSpell()) } },
        { ActorClass.Scholar, new() { new Unlock(1, ()=> new Adloquium()), new Unlock(4, ()=> new Biolysis()) } },
        { ActorClass.Astrologian, new() { new Unlock(1, ()=> new Benefic()), new Unlock(3, ()=> new DrawCard()) } },
        { ActorClass.Sage, new() { new Unlock(1, ()=> new Kardia()), new Unlock(4, ()=> new Dosis()) } },
        { ActorClass.RedMage, new() { new Unlock(1, ()=> new Riposte()), new Unlock(4, ()=> new Verfire()) } },
        { ActorClass.BlueMage, new() { new Unlock(1, ()=> new Needles1000()), new Unlock(4, ()=> new Mimicry()) } },
        { ActorClass.Summoner, new() { new Unlock(1, ()=> new SummonCarbuncle()), new Unlock(3, ()=> new Ruin()) } },
        { ActorClass.Necromancer, new() { new Unlock(1, ()=> new RaiseSkeleton()), new Unlock(4, ()=> new ShadowBolt()) } },
        { ActorClass.Artificer, new() { new Unlock(1, ()=> new DeployTurret()), new Unlock(3, ()=> new AlchemicalShot()) } },

        // Crafting (Disciples of the Hand)
        { ActorClass.Carpenter, new() { new Unlock(1, ()=> new MasterCraft()), new Unlock(3, ()=> new RapidSynthesis()) } },
        { ActorClass.Blacksmith, new() { new Unlock(1, ()=> new TemperedStrike()), new Unlock(3, ()=> new Metalworking()) } },
        { ActorClass.Armorer, new() { new Unlock(1, ()=> new ShieldPolish()), new Unlock(3, ()=> new PlateBash()) } },
        { ActorClass.Goldsmith, new() { new Unlock(1, ()=> new GemCut()), new Unlock(3, ()=> new GleamShot()) } },
        { ActorClass.Leatherworker, new() { new Unlock(1, ()=> new LeatherTanning()), new Unlock(3, ()=> new LashWhip()) } },
        { ActorClass.Weaver, new() { new Unlock(1, ()=> new Weave()), new Unlock(3, ()=> new ThreadShot()) } },
        { ActorClass.Alchemist, new() { new Unlock(1, ()=> new Transmute()), new Unlock(3, ()=> new AcidFlask()) } },
        { ActorClass.Culinarian, new() { new Unlock(1, ()=> new SavoryMeal()), new Unlock(3, ()=> new FryingPanWhack()) } },

        // Gathering (Disciples of the Land)
        { ActorClass.Miner, new() { new Unlock(1, ()=> new PickaxeSwing()), new Unlock(3, ()=> new StoneSense()) } },
        { ActorClass.Botanist, new() { new Unlock(1, ()=> new SickleSwipe()), new Unlock(3, ()=> new HerbLore()) } },
        { ActorClass.Fisher, new() { new Unlock(1, ()=> new HookCast()), new Unlock(3, ()=> new FisherLuck()) } },
    };

    public static IEnumerable<Unlock> GetUnlocks(ActorClass cls)
    {
        if (Map.TryGetValue(cls, out var list)) return list;
        // default fallback ability
        return Map[ActorClass.Adventurer];
    }
}
