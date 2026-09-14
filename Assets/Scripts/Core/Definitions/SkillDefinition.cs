using System;
using System.Collections.Generic;

namespace RuneArena.Core
{
    /// <summary>Immutable secondary effect carried by a skill (applied to targets hit, or to self for Buff shapes).</summary>
    public sealed class SkillEffect
    {
        public SkillEffectType Type { get; init; }
        /// <summary>Meaning depends on Type: Knockback/Pull = force (units), Slow/DamageReduction/SpeedBoost = fraction (0.4 = 40%), Shield/Heal = flat amount.</summary>
        public float Value { get; init; }
        public float Duration { get; init; }
        /// <summary>Extra amount as a fraction of the caster's MaxHealth (Shield / Heal only), e.g. 0.2 = +20% MaxHealth.</summary>
        public float MaxHealthRatio { get; init; }

        public SkillEffect() { }

        public SkillEffect(SkillEffectType type, float value, float duration = 0f, float maxHealthRatio = 0f)
        {
            Type = type;
            Value = value;
            Duration = duration;
            MaxHealthRatio = maxHealthRatio;
        }

        public static SkillEffect Knockback(float force) => new SkillEffect(SkillEffectType.Knockback, force);
        public static SkillEffect Pull(float force) => new SkillEffect(SkillEffectType.Pull, force);
        public static SkillEffect Stun(float duration) => new SkillEffect(SkillEffectType.Stun, 1f, duration);
        public static SkillEffect Slow(float fraction, float duration) => new SkillEffect(SkillEffectType.Slow, fraction, duration);
        public static SkillEffect Shield(float amount, float duration, float maxHealthRatio = 0f) => new SkillEffect(SkillEffectType.Shield, amount, duration, maxHealthRatio);
        public static SkillEffect Heal(float amount, float maxHealthRatio = 0f) => new SkillEffect(SkillEffectType.Heal, amount, 0f, maxHealthRatio);
        public static SkillEffect Invisible(float duration) => new SkillEffect(SkillEffectType.Invisible, 1f, duration);
        public static SkillEffect DamageReduction(float fraction, float duration) => new SkillEffect(SkillEffectType.DamageReduction, fraction, duration);
        public static SkillEffect SpeedBoost(float fraction, float duration) => new SkillEffect(SkillEffectType.SpeedBoost, fraction, duration);
        public static SkillEffect DamageAmp(float fraction, float duration) => new SkillEffect(SkillEffectType.DamageAmp, fraction, duration);
    }

    /// <summary>Immutable skill definition (DESIGN.md section 5). Author with object initializers; all fields have sane defaults.</summary>
    public sealed class SkillDefinition
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public SkillKey Key { get; init; } = SkillKey.Q;
        public SkillShape Shape { get; init; } = SkillShape.Projectile;
        public AiHint AiHint { get; init; } = AiHint.None;

        /// <summary>Seconds before the effect resolves (telegraph visible).</summary>
        public float Windup { get; init; } = 0.1f;
        /// <summary>Seconds after the effect during which the caster is still "casting" (buffered inputs wait).</summary>
        public float Recovery { get; init; } = 0.1f;
        /// <summary>Base cooldown in seconds (0 for basic attacks; attack speed gates them instead).</summary>
        public float Cooldown { get; init; }
        /// <summary>Max travel / reach / dash distance. For Basic key, executor should use StatBlock AttackRange (see GetRange).</summary>
        public float Range { get; init; } = 5f;
        /// <summary>AoE radius (Circle, SelfCircle, Channel) or projectile hit radius.</summary>
        public float Radius { get; init; } = 1f;
        /// <summary>Total arc in degrees (Cone, Melee, Fan spread).</summary>
        public float Angle { get; init; } = 60f;
        /// <summary>Projectile or dash speed in units/s.</summary>
        public float Speed { get; init; } = 20f;
        /// <summary>Circle: seconds until the ground AoE resolves. Channel: total channel duration.</summary>
        public float Delay { get; init; }
        /// <summary>Number of projectiles for Fan (>= 1).</summary>
        public int ProjectileCount { get; init; } = 1;

        public float BaseDamage { get; init; }
        public float AdRatio { get; init; }
        public float ApRatio { get; init; }
        public DamageType DamageType { get; init; } = DamageType.Physical;
        public IReadOnlyList<SkillEffect> Effects { get; init; } = Array.Empty<SkillEffect>();

        /// <summary>Charges available before the cooldown gates the skill (Legendary rune may raise Q/W/E to 2).</summary>
        public int Charges { get; init; } = 1;
        /// <summary>Whether casting this skill removes Invisible from the caster. Default true (E Smoke sets false).</summary>
        public bool BreaksInvisibility { get; init; } = true;
        /// <summary>Whether the caster cannot move while casting. Default: true for Channel, false otherwise (see RootsCasterEffective).</summary>
        public bool? RootsCaster { get; init; }
        /// <summary>Whether this is the hero's ultimate (used for trauma 0.6 and the AI Ult rule). Defaults to Key == R.</summary>
        public bool? IsUltimate { get; init; }
        /// <summary>Fraction of the caster's CURRENT health paid when the skill resolves (燃血). Never lethal (floors at 1 HP). 0 = free.</summary>
        public float HealthCostFraction { get; init; }
        /// <summary>Second stage resolved FollowUpDelay seconds later at the skill's ground point (Circle / SelfCircle) or at the caster's landing point (Dash / Blink). Its Shape must be Circle or SelfCircle.</summary>
        public SkillDefinition FollowUp { get; init; }
        public float FollowUpDelay { get; init; }
        /// <summary>Visual arc height (units) of the body while a Dash travels (leap). 0 = flat dash. Never affects hit queries.</summary>
        public float LeapHeight { get; init; }
        /// <summary>Cosmetic tag consumed by the Juice layer (e.g. "clam", "carrot", "fire", "spin"). Never affects gameplay.</summary>
        public string Vfx { get; init; } = "";

        public bool IsBasicAttack => Key == SkillKey.Basic;
        public bool IsMovementSkill => Shape == SkillShape.Dash || Shape == SkillShape.Blink;
        public bool RootsCasterEffective => RootsCaster ?? (Shape == SkillShape.Channel);
        public bool IsUltimateEffective => IsUltimate ?? (Key == SkillKey.R);
        public bool DealsDamage => BaseDamage > 0f || AdRatio > 0f || ApRatio > 0f;
        public bool HasFollowUp => FollowUp != null;

        /// <summary>Range to use at runtime: basic attacks read the AttackRange stat, everything else uses Range.</summary>
        public float GetRange(StatBlock stats)
        {
            if (IsBasicAttack && stats != null) return stats.Get(StatType.AttackRange);
            return Range;
        }

        /// <summary>Raw (pre-mitigation, pre-crit) damage for a caster: BaseDamage + AD * AdRatio + AP * ApRatio.</summary>
        public float ComputeDamage(StatBlock stats)
        {
            if (stats == null) return BaseDamage;
            return BaseDamage + stats.Get(StatType.AttackDamage) * AdRatio + stats.Get(StatType.AbilityPower) * ApRatio;
        }

        /// <summary>Returns the first effect of a type, or null.</summary>
        public SkillEffect FindEffect(SkillEffectType type)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Type == type) return Effects[i];
            }
            return null;
        }

        /// <summary>Returns a copy with a different charge count (used by the 双重施法 legendary rune; never mutates the original).</summary>
        public SkillDefinition WithCharges(int charges)
        {
            return new SkillDefinition
            {
                Id = Id, Name = Name, Description = Description, Key = Key, Shape = Shape, AiHint = AiHint,
                Windup = Windup, Recovery = Recovery, Cooldown = Cooldown, Range = Range, Radius = Radius,
                Angle = Angle, Speed = Speed, Delay = Delay, ProjectileCount = ProjectileCount,
                BaseDamage = BaseDamage, AdRatio = AdRatio, ApRatio = ApRatio, DamageType = DamageType,
                Effects = Effects, Charges = charges, BreaksInvisibility = BreaksInvisibility,
                RootsCaster = RootsCaster, IsUltimate = IsUltimate, HealthCostFraction = HealthCostFraction,
                FollowUp = FollowUp, FollowUpDelay = FollowUpDelay, LeapHeight = LeapHeight, Vfx = Vfx
            };
        }
    }
}
