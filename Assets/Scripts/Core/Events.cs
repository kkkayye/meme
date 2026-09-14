using RuneArena.Combat;
using RuneArena.Loot;
using UnityEngine;

namespace RuneArena.Core
{
    /// <summary>Event payloads published through EventBus. All are readonly structs with a positional constructor.</summary>
    public readonly struct PhaseChanged
    {
        public readonly MatchPhase From;
        public readonly MatchPhase To;
        public readonly int Round;
        public PhaseChanged(MatchPhase from, MatchPhase to, int round) { From = from; To = to; Round = round; }
    }

    /// <summary>Published when a match begins (after GameServices are populated, before the first RuneDraft).</summary>
    public readonly struct MatchStarted
    {
        public readonly MatchConfig Config;
        public MatchStarted(MatchConfig config) { Config = config; }
    }

    /// <summary>Published when a team reaches RoundsToWin.</summary>
    public readonly struct MatchEnded
    {
        public readonly Team Winner;
        public MatchEnded(Team winner) { Winner = winner; }
    }

    /// <summary>Published at the start of Combat for a round (after Countdown). Hooks reset per-round state here.</summary>
    public readonly struct RoundStarted
    {
        public readonly int Round;
        public RoundStarted(int round) { Round = round; }
    }

    public readonly struct RoundEnded
    {
        public readonly int Round;
        public readonly Team Winner;
        public RoundEnded(int round, Team winner) { Round = round; Winner = winner; }
    }

    public readonly struct UnitSpawned
    {
        public readonly Unit Unit;
        public UnitSpawned(Unit unit) { Unit = unit; }
    }

    /// <summary>Published by DamagePipeline for every damage application that reached the target (including fully absorbed hits).</summary>
    public readonly struct UnitDamaged
    {
        public readonly DamageInfo Info;
        public readonly DamageResult Result;
        public UnitDamaged(DamageInfo info, DamageResult result) { Info = info; Result = result; }
    }

    /// <summary>Published by Unit.Heal when a positive amount was restored.</summary>
    public readonly struct UnitHealed
    {
        public readonly Unit Target;
        public readonly Unit Source;
        public readonly float Amount;
        public UnitHealed(Unit target, Unit source, float amount) { Target = target; Source = source; Amount = amount; }
    }

    /// <summary>Published by Unit.Kill. Killer may be null.</summary>
    public readonly struct UnitDied
    {
        public readonly Unit Victim;
        public readonly Unit Killer;
        public UnitDied(Unit victim, Unit killer) { Victim = victim; Killer = killer; }
    }

    /// <summary>Published by Unit.Revive (e.g. 守护天使 item).</summary>
    public readonly struct UnitRevived
    {
        public readonly Unit Unit;
        public UnitRevived(Unit unit) { Unit = unit; }
    }

    /// <summary>Published when a lethal guard (不死 rune) consumed a lethal hit and left the unit at 1 HP.</summary>
    public readonly struct LethalDamagePrevented
    {
        public readonly Unit Unit;
        public readonly string SourceId;
        public LethalDamagePrevented(Unit unit, string sourceId) { Unit = unit; SourceId = sourceId; }
    }

    /// <summary>Published by DamagePipeline for DamageTag.Basic hits.</summary>
    public readonly struct BasicAttackHit
    {
        public readonly Unit Source;
        public readonly Unit Target;
        public readonly DamageResult Result;
        public BasicAttackHit(Unit source, Unit target, DamageResult result) { Source = source; Target = target; Result = result; }
    }

    /// <summary>Published by SkillCaster at the moment a skill's effect resolves (cooldown starts).</summary>
    public readonly struct SkillCast
    {
        public readonly Unit Caster;
        public readonly SkillDefinition Skill;
        public SkillCast(Unit caster, SkillDefinition skill) { Caster = caster; Skill = skill; }
    }

    /// <summary>Published by DamagePipeline for DamageTag.Skill hits whose DamageInfo.Skill is set.</summary>
    public readonly struct SkillHit
    {
        public readonly Unit Source;
        public readonly Unit Target;
        public readonly SkillDefinition Skill;
        public readonly DamageResult Result;
        public SkillHit(Unit source, Unit target, SkillDefinition skill, DamageResult result) { Source = source; Target = target; Skill = skill; Result = result; }
    }

    /// <summary>Published by UnitMotor when a dash or blink completes (Storm set, 冲刺护盾, 烈焰足迹).</summary>
    public readonly struct DashPerformed
    {
        public readonly Unit Unit;
        public readonly Vector3 From;
        public readonly Vector3 To;
        public DashPerformed(Unit unit, Vector3 from, Vector3 to) { Unit = unit; From = from; To = to; }
    }

    public readonly struct StatusApplied
    {
        public readonly Unit Target;
        public readonly StatusType Type;
        public readonly float Duration;
        public StatusApplied(Unit target, StatusType type, float duration) { Target = target; Type = type; Duration = duration; }
    }

    public readonly struct GoldChanged
    {
        public readonly Unit Unit;
        public readonly int NewGold;
        public readonly int Delta;
        public GoldChanged(Unit unit, int newGold, int delta) { Unit = unit; NewGold = newGold; Delta = delta; }
    }

    public readonly struct RuneAcquired
    {
        public readonly Unit Unit;
        public readonly RuneDefinition Rune;
        public RuneAcquired(Unit unit, RuneDefinition rune) { Unit = unit; Rune = rune; }
    }

    public readonly struct ItemAcquired
    {
        public readonly Unit Unit;
        public readonly ItemDefinition Item;
        public ItemAcquired(Unit unit, ItemDefinition item) { Unit = unit; Item = item; }
    }

    public readonly struct ItemSold
    {
        public readonly Unit Unit;
        public readonly ItemDefinition Item;
        public ItemSold(Unit unit, ItemDefinition item) { Unit = unit; Item = item; }
    }

    public readonly struct ChestSpawned
    {
        public readonly Chest Chest;
        public ChestSpawned(Chest chest) { Chest = chest; }
    }

    public readonly struct ChestOpened
    {
        public readonly Unit Unit;
        public readonly LootKind Kind;
        public readonly string RewardName;
        public ChestOpened(Unit unit, LootKind kind, string rewardName) { Unit = unit; Kind = kind; RewardName = rewardName; }
    }

    public readonly struct ControlPointCaptured
    {
        public readonly Team Team;
        public ControlPointCaptured(Team team) { Team = team; }
    }

    /// <summary>Published by SkillCaster when a cooldown finishes (HUD ping).</summary>
    public readonly struct CooldownReady
    {
        public readonly Unit Unit;
        public readonly SkillKey Key;
        public CooldownReady(Unit unit, SkillKey key) { Unit = unit; Key = key; }
    }
}
