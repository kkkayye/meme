using System;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Stateless damage math: armor mitigation, status DR, shields before HP, lethal guards, lifesteal, then events and death.</summary>
    public static class DamagePipeline
    {
        /// <summary>Applies a DamageInfo to its target. Returns DamageResult.None for dead targets or non-positive amounts.</summary>
        public static DamageResult Apply(DamageInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            Unit target = info.Target;
            if (target == null) throw new ArgumentException("DamageInfo.Target is null.", nameof(info));
            if (!target.IsAlive || info.Amount <= 0f) return DamageResult.None;

            float mitigated = MitigateByArmor(info.Amount, info.Type, target.Stats.Get(StatType.Armor));
            mitigated *= 1f - Mathf.Clamp01(target.Status.GetDamageReduction());
            if (mitigated <= 0f) return DamageResult.None;

            float absorbed = target.Shields.Absorb(mitigated);
            float remaining = mitigated - absorbed;
            string guardId = null;
            float dealt = 0f;
            bool killed = false;
            if (remaining > 0f)
            {
                dealt = ApplyToHealth(target, remaining, out killed, out guardId);
            }

            var result = new DamageResult(dealt, absorbed, killed);
            ApplyLifesteal(info, result);
            PublishHitEvents(info, result);
            if (guardId != null) EventBus.Publish(new LethalDamagePrevented(target, guardId));
            if (killed) target.Kill(info.Source);
            return result;
        }

        /// <summary>Armor formula: dmg * 100 / (100 + armor) for Physical and Magical; True damage ignores armor.</summary>
        public static float MitigateByArmor(float amount, DamageType type, float armor)
        {
            if (amount <= 0f) return 0f;
            if (type == DamageType.True) return amount;
            float effectiveArmor = Mathf.Max(0f, armor);
            return amount * GameConstants.ArmorConstant / (GameConstants.ArmorConstant + effectiveArmor);
        }

        /// <summary>Raw skill damage for a caster: BaseDamage + AD * AdRatio + AP * ApRatio (no crit).</summary>
        public static float ComputeSkillDamage(Unit source, SkillDefinition skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            return skill.ComputeDamage(source != null ? source.Stats : null);
        }

        /// <summary>Builds the DamageInfo for a skill hit (Tag = Skill, no crit). Multiplier covers backstab/execute bonuses.</summary>
        public static DamageInfo ForSkill(Unit source, Unit target, SkillDefinition skill, float multiplier = 1f)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            float amount = ComputeSkillDamage(source, skill) * multiplier;
            return new DamageInfo(source, target, amount, skill.DamageType, DamageTag.Skill, false, skill);
        }

        /// <summary>Builds the DamageInfo for a basic attack: AD * AdRatio (default 1), crit rolled with rng and CritDamage applied.</summary>
        public static DamageInfo ForBasicAttack(Unit source, Unit target, Rng rng, float multiplier = 1f)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            SkillDefinition basic = source.Hero != null ? source.Hero.BasicAttack : null;
            float amount = basic != null ? basic.ComputeDamage(source.Stats) : source.Stats.Get(StatType.AttackDamage);
            bool crit = RollCrit(source, rng);
            if (crit) amount *= source.Stats.Get(StatType.CritDamage);
            DamageType type = basic != null ? basic.DamageType : DamageType.Physical;
            return new DamageInfo(source, target, amount * multiplier, type, DamageTag.Basic, crit, basic);
        }

        /// <summary>Rolls CritChance with the given rng (GameServices.Rng if null).</summary>
        public static bool RollCrit(Unit source, Rng rng)
        {
            if (source == null) return false;
            float chance = source.Stats.Get(StatType.CritChance);
            if (chance <= 0f) return false;
            Rng r = rng ?? GameServices.RngOrDefault();
            return r.Chance(chance);
        }

        /// <summary>Convenience: apply a flat amount with a given type and tag (used by burns, reflects, item/rune procs).</summary>
        public static DamageResult Deal(Unit source, Unit target, float amount, DamageType type, DamageTag tag)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return Apply(new DamageInfo(source, target, amount, type, tag));
        }

        /// <summary>One burn tick: Magical damage tagged Burn.</summary>
        public static DamageResult DealBurnTick(Unit source, Unit target, float amount)
        {
            return Deal(source, target, amount, DamageType.Magical, DamageTag.Burn);
        }

        /// <summary>True when attackDir (attacker to target) roughly matches the target's facing, i.e. the hit comes from behind.</summary>
        public static bool IsBackstab(Unit target, Vector3 attackDir)
        {
            if (target == null) return false;
            attackDir.y = 0f;
            if (attackDir.sqrMagnitude < 1e-6f) return false;
            return Vector3.Dot(target.Facing, attackDir.normalized) > GameConstants.BackstabDotThreshold;
        }

        private static float ApplyToHealth(Unit target, float remaining, out bool killed, out string guardId)
        {
            float before = target.Health;
            float newHealth = before - remaining;
            killed = false;
            guardId = null;
            if (newHealth > 0f)
            {
                target.SetHealthRaw(newHealth);
                return remaining;
            }
            if (target.TryConsumeLethalGuard(out guardId))
            {
                target.SetHealthRaw(1f);
                return Mathf.Max(0f, before - 1f);
            }
            target.SetHealthRaw(0f);
            killed = true;
            return before;
        }

        private static void ApplyLifesteal(DamageInfo info, DamageResult result)
        {
            if (info.Tag != DamageTag.Basic || info.Source == null || !info.Source.IsAlive) return;
            float lifesteal = info.Source.Stats.Get(StatType.Lifesteal);
            if (lifesteal <= 0f || result.Total <= 0f) return;
            info.Source.Heal(result.Total * lifesteal, info.Source);
        }

        private static void PublishHitEvents(DamageInfo info, DamageResult result)
        {
            EventBus.Publish(new UnitDamaged(info, result));
            if (info.Source == null) return;
            if (info.Tag == DamageTag.Basic)
            {
                EventBus.Publish(new BasicAttackHit(info.Source, info.Target, result));
            }
            else if (info.Tag == DamageTag.Skill && info.Skill != null)
            {
                EventBus.Publish(new SkillHit(info.Source, info.Target, info.Skill, result));
            }
        }
    }
}
