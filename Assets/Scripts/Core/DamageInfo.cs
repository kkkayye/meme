using System;
using RuneArena.Combat;

namespace RuneArena.Core
{
    /// <summary>Immutable description of one damage instance BEFORE mitigation (armor/DR/shields are applied by DamagePipeline).</summary>
    public sealed class DamageInfo
    {
        /// <summary>Unit dealing the damage. May be null for environmental damage.</summary>
        public Unit Source { get; init; }
        public Unit Target { get; init; }
        /// <summary>Raw pre-mitigation amount. Crit (if any) must already be applied by the caller.</summary>
        public float Amount { get; init; }
        public DamageType Type { get; init; } = DamageType.Physical;
        public DamageTag Tag { get; init; } = DamageTag.Skill;
        public bool IsCrit { get; init; }
        /// <summary>Id of the skill that produced this damage, or null. Defaults to Skill?.Id.</summary>
        public string SkillId
        {
            get => _skillId ?? Skill?.Id;
            init => _skillId = value;
        }
        /// <summary>Optional definition of the skill that produced this damage (lets DamagePipeline publish SkillHit).</summary>
        public SkillDefinition Skill { get; init; }

        private readonly string _skillId;

        public DamageInfo() { }

        public DamageInfo(Unit source, Unit target, float amount, DamageType type, DamageTag tag, bool isCrit = false, SkillDefinition skill = null)
        {
            Source = source;
            Target = target;
            Amount = amount;
            Type = type;
            Tag = tag;
            IsCrit = isCrit;
            Skill = skill;
        }

        /// <summary>Returns a copy with a different amount (used by hooks that scale damage before it is applied).</summary>
        public DamageInfo WithAmount(float amount)
        {
            return new DamageInfo(Source, Target, amount, Type, Tag, IsCrit, Skill) { SkillId = _skillId };
        }

        /// <summary>Returns a copy with a different target.</summary>
        public DamageInfo WithTarget(Unit target)
        {
            return new DamageInfo(Source, target, Amount, Type, Tag, IsCrit, Skill) { SkillId = _skillId };
        }

        /// <summary>Returns a copy with a different type and tag.</summary>
        public DamageInfo WithTypeAndTag(DamageType type, DamageTag tag)
        {
            return new DamageInfo(Source, Target, Amount, type, tag, IsCrit, Skill) { SkillId = _skillId };
        }
    }

    /// <summary>Immutable outcome of applying a DamageInfo: HP actually removed, amount absorbed by shields, and whether it killed.</summary>
    public sealed class DamageResult
    {
        public static readonly DamageResult None = new DamageResult(0f, 0f, false);

        /// <summary>Health removed (post-mitigation, post-shield).</summary>
        public float Dealt { get; }
        /// <summary>Amount absorbed by shields (post-mitigation).</summary>
        public float Absorbed { get; }
        public bool Killed { get; }
        /// <summary>Dealt + Absorbed: total post-mitigation damage, useful for lifesteal and hook math.</summary>
        public float Total => Dealt + Absorbed;

        public DamageResult(float dealt, float absorbed, bool killed)
        {
            Dealt = Math.Max(0f, dealt);
            Absorbed = Math.Max(0f, absorbed);
            Killed = killed;
        }
    }
}
