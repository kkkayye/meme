using System;
using System.Collections.Generic;
using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Resolves a skill's shape into hits (CombatWorld queries / projectiles / dashes / delayed circles), applies damage through DamagePipeline and applies SkillEffects to targets or the caster.</summary>
    public static class SkillExecutor
    {
        private const float ProjectileSpawnHeight = 1f;
        private const float ProjectileSpawnForward = 0.6f;
        private const float BlinkBackstep = 0.5f;
        private const int BlinkMaxSteps = 20;

        /// <summary>Shows the telegraph for the windup (and the ground delay for Circle skills).</summary>
        public static void OnWindupStarted(Unit caster, SkillDefinition skill, Vector3 aimDir, Vector3 aimPoint)
        {
            if (caster == null || skill == null) return;
            Color color = caster.Visuals != null ? caster.Visuals.BodyColor : Color.white;
            float range = skill.GetRange(caster.Stats);
            switch (skill.Shape)
            {
                case SkillShape.Cone:
                case SkillShape.Melee:
                    Telegraphs.Cone(caster.Position, aimDir, range, skill.Angle, skill.Windup, color);
                    break;
                case SkillShape.Circle:
                    Telegraphs.Circle(ClampToRange(caster.Position, aimPoint, range), skill.Radius, skill.Windup + skill.Delay, color);
                    break;
                case SkillShape.SelfCircle:
                    Telegraphs.Circle(caster.Position, skill.Radius, skill.Windup, color);
                    break;
                case SkillShape.Channel:
                    Telegraphs.Circle(caster.Position, skill.Radius, skill.Windup + skill.Delay, color);
                    break;
                case SkillShape.Dash:
                    Telegraphs.Line(caster.Position, caster.Position + aimDir * range, skill.Windup, color);
                    break;
            }
        }

        /// <summary>Resolves the skill at the effect moment (after windup). Dashes start here and hit via DashTick; channels tick via ChannelTick.</summary>
        public static void Execute(Unit caster, SkillDefinition skill, Vector3 aimDir, Vector3 aimPoint)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 1e-6f) aimDir = caster.Facing;
            aimDir.Normalize();
            ApplySelfEffects(caster, skill);
            float range = skill.GetRange(caster.Stats);
            switch (skill.Shape)
            {
                case SkillShape.Projectile: FireProjectile(caster, skill, aimDir, range); break;
                case SkillShape.Fan: FireFan(caster, skill, aimDir, range); break;
                case SkillShape.Cone:
                case SkillShape.Melee: ResolveCone(caster, skill, aimDir, range); break;
                case SkillShape.Circle: ScheduleCircle(caster, skill, ClampToRange(caster.Position, aimPoint, range)); break;
                case SkillShape.SelfCircle: ResolveCircle(caster, skill, caster.Position); break;
                case SkillShape.Dash: caster.Motor.BeginDash(aimDir, range, skill.Speed, null); break;
                case SkillShape.Blink: Blink(caster, skill, aimDir, range); break;
                case SkillShape.Buff:
                case SkillShape.Channel: break;
            }
        }

        /// <summary>One channel tick: damages every enemy within Radius of the caster.</summary>
        public static void ChannelTick(Unit caster, SkillDefinition skill)
        {
            if (caster == null || skill == null || !caster.IsAlive) return;
            ResolveCircle(caster, skill, caster.Position);
        }

        /// <summary>Hits enemies passed through between two dash positions (each enemy at most once per dash).</summary>
        public static void DashTick(Unit caster, SkillDefinition skill, Vector3 from, Vector3 to, HashSet<Unit> alreadyHit)
        {
            if (caster == null || skill == null || GameServices.World == null || alreadyHit == null) return;
            var hits = new List<Unit>();
            GameServices.World.EnemiesAlongSegment(from, to, skill.Radius, caster.Team, hits);
            Vector3 dir = caster.Motor != null ? caster.Motor.DashDirection : (to - from);
            for (int i = 0; i < hits.Count; i++)
            {
                if (!alreadyHit.Add(hits[i])) continue;
                HitTarget(caster, skill, hits[i], dir, 1f);
            }
        }

        /// <summary>Applies the skill's damage (with special multipliers) and target effects to one enemy.</summary>
        public static void HitTarget(Unit caster, SkillDefinition skill, Unit target, Vector3 hitDir, float damageMultiplier)
        {
            if (caster == null || skill == null || target == null || !target.IsAlive) return;
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 1e-6f) hitDir = target.Position - caster.Position;
            hitDir.Normalize();
            if (skill.DealsDamage)
            {
                DamageInfo info = skill.IsBasicAttack
                    ? DamagePipeline.ForBasicAttack(caster, target, GameServices.RngOrDefault(), damageMultiplier)
                    : DamagePipeline.ForSkill(caster, target, skill, damageMultiplier * SkillSpecials.SkillMultiplier(caster, skill, target, hitDir));
                target.ApplyDamage(info);
            }
            ApplyTargetEffects(caster, skill, target, hitDir);
        }

        /// <summary>Clamps a ground point to within 'range' of the origin (y = 0).</summary>
        public static Vector3 ClampToRange(Vector3 origin, Vector3 point, float range)
        {
            origin.y = 0f;
            point.y = 0f;
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude > range * range) offset = offset.normalized * range;
            return origin + offset;
        }

        private static void FireProjectile(Unit caster, SkillDefinition skill, Vector3 dir, float range)
        {
            Vector3 origin = caster.Position + Vector3.up * ProjectileSpawnHeight + dir * ProjectileSpawnForward;
            Projectile.Spawn(caster, skill, origin, dir, range, 1f);
        }

        private static void FireFan(Unit caster, SkillDefinition skill, Vector3 dir, float range)
        {
            int count = Mathf.Max(1, skill.ProjectileCount);
            if (count == 1)
            {
                FireProjectile(caster, skill, dir, range);
                return;
            }
            float step = skill.Angle / (count - 1);
            float start = -skill.Angle * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector3 rotated = Quaternion.Euler(0f, start + step * i, 0f) * dir;
                FireProjectile(caster, skill, rotated, range);
            }
        }

        private static void ResolveCone(Unit caster, SkillDefinition skill, Vector3 dir, float range)
        {
            if (GameServices.World == null) return;
            var hits = new List<Unit>();
            GameServices.World.EnemiesInCone(caster.Position, dir, range, skill.Angle, caster.Team, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                HitTarget(caster, skill, hits[i], hits[i].Position - caster.Position, 1f);
            }
        }

        private static void ScheduleCircle(Unit caster, SkillDefinition skill, Vector3 point)
        {
            CombatFx.Instance.Schedule(skill.Delay, () =>
            {
                if (caster == null) return;
                ResolveCircle(caster, skill, point);
            });
        }

        private static void ResolveCircle(Unit caster, SkillDefinition skill, Vector3 center)
        {
            if (GameServices.World == null) return;
            var hits = new List<Unit>();
            GameServices.World.EnemiesInRadius(center, skill.Radius, caster.Team, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                HitTarget(caster, skill, hits[i], hits[i].Position - center, 1f);
            }
        }

        private static void Blink(Unit caster, SkillDefinition skill, Vector3 dir, float range)
        {
            Vector3 from = caster.Position;
            Vector3 to = from + dir * range;
            CombatWorld world = GameServices.World;
            if (world != null)
            {
                to = world.ClampToArena(to);
                int steps = 0;
                while (!world.IsWalkable(to) && steps++ < BlinkMaxSteps)
                {
                    to -= dir * BlinkBackstep;
                }
                if (!world.IsWalkable(to)) to = from;
            }
            caster.Motor.SetPosition(to);
            EventBus.Publish(new DashPerformed(caster, from, to));
        }

        private static void ApplySelfEffects(Unit caster, SkillDefinition skill)
        {
            IReadOnlyList<SkillEffect> effects = skill.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillEffect fx = effects[i];
                if (!SkillBook.IsSelfEffect(fx.Type)) continue;
                ApplySelfEffect(caster, skill, fx);
            }
        }

        private static void ApplySelfEffect(Unit caster, SkillDefinition skill, SkillEffect fx)
        {
            float maxHealth = caster.MaxHealth;
            switch (fx.Type)
            {
                case SkillEffectType.Shield:
                    caster.AddShield(fx.Value + fx.MaxHealthRatio * maxHealth, fx.Duration, skill.Id);
                    break;
                case SkillEffectType.Heal:
                    caster.Heal(fx.Value + fx.MaxHealthRatio * maxHealth, caster);
                    break;
                case SkillEffectType.Invisible:
                    caster.Status.Apply(StatusType.Invisible, 1f, fx.Duration, skill.Id, caster);
                    break;
                case SkillEffectType.DamageReduction:
                    caster.Status.Apply(StatusType.DamageReduction, fx.Value, fx.Duration, skill.Id, caster);
                    break;
                case SkillEffectType.SpeedBoost:
                    caster.Status.Apply(StatusType.SpeedBoost, fx.Value, fx.Duration, skill.Id, caster);
                    break;
            }
        }

        private static void ApplyTargetEffects(Unit caster, SkillDefinition skill, Unit target, Vector3 hitDir)
        {
            if (!target.IsAlive) return;
            IReadOnlyList<SkillEffect> effects = skill.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillEffect fx = effects[i];
                if (SkillBook.IsSelfEffect(fx.Type)) continue;
                switch (fx.Type)
                {
                    case SkillEffectType.Knockback:
                        target.Motor.BeginKnockback(hitDir, fx.Value);
                        break;
                    case SkillEffectType.Pull:
                        target.Motor.BeginKnockback(-hitDir, fx.Value);
                        break;
                    case SkillEffectType.Stun:
                        target.Status.Apply(StatusType.Stun, 1f, fx.Duration, skill.Id, caster);
                        break;
                    case SkillEffectType.Slow:
                        target.Status.Apply(StatusType.Slow, fx.Value, fx.Duration, skill.Id, caster);
                        break;
                }
            }
        }
    }
}
