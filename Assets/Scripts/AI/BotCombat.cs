using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.AI
{
    /// <summary>Bot skill selection by AiHint (Engage / Poke / Burst / Defensive / Escape / Ult) plus basic attacks, with aim error.</summary>
    public static class BotCombat
    {
        private static readonly SkillKey[] Keys = { SkillKey.R, SkillKey.Q, SkillKey.W, SkillKey.E };
        private static readonly List<Unit> Buffer = new List<Unit>(8);

        /// <summary>Tries at most one skill, then a basic attack when in range.</summary>
        public static void TryAct(BotBrain brain, Unit target, float distance)
        {
            if (brain == null || brain.Owner == null || target == null) return;
            Unit self = brain.Owner;
            if (!self.CanAct || self.Caster.IsCasting) return;
            for (int i = 0; i < Keys.Length; i++)
            {
                if (TrySkill(brain, self, target, distance, Keys[i])) return;
            }
            float range = self.Stats.Get(StatType.AttackRange);
            if (distance <= range + GameConstants.HeroRadius && brain.State != BotBrain.BotState.Retreat)
            {
                self.Caster.TryCast(SkillKey.Basic, Aim(brain, target.Position - self.Position), target.Position);
            }
        }

        /// <summary>True if the unit has a ready skill hinted Defensive or Escape.</summary>
        public static bool HasReadyDefensive(Unit unit)
        {
            if (unit == null || unit.Caster == null) return false;
            for (int i = 0; i < Keys.Length; i++)
            {
                SkillDefinition skill = unit.Caster.GetSkill(Keys[i]);
                if (skill == null) continue;
                if ((skill.AiHint == AiHint.Defensive || skill.AiHint == AiHint.Escape) && unit.Caster.IsReady(Keys[i])) return true;
            }
            return false;
        }

        private static bool TrySkill(BotBrain brain, Unit self, Unit target, float distance, SkillKey key)
        {
            SkillDefinition skill = self.Caster.GetSkill(key);
            if (skill == null || !self.Caster.IsReady(key)) return false;
            bool selfOnly = skill.AiHint == AiHint.Defensive || skill.AiHint == AiHint.Escape;
            if (!target.IsHero && !selfOnly) return false;
            Vector3 toTarget = target.Position - self.Position;
            float attackRange = self.Stats.Get(StatType.AttackRange);
            bool inDanger = self.HealthFraction < GameConstants.BotDefensiveHealthFraction && distance <= GameConstants.BotDefensiveEnemyRange;
            switch (skill.AiHint)
            {
                case AiHint.Engage:
                    if (brain.State == BotBrain.BotState.Retreat) return Cast(brain, self, key, -toTarget, self.Position - toTarget.normalized * skill.Range);
                    if (distance > attackRange && distance <= skill.Range + attackRange) return Cast(brain, self, key, toTarget, target.Position);
                    return false;
                case AiHint.Poke:
                    return distance <= skill.Range && Cast(brain, self, key, toTarget, target.Position);
                case AiHint.Burst:
                    return distance <= EffectiveRange(skill) && Cast(brain, self, key, toTarget, target.Position);
                case AiHint.Defensive:
                    return inDanger && Cast(brain, self, key, toTarget, self.Position);
                case AiHint.Escape:
                    return inDanger && Cast(brain, self, key, -toTarget, self.Position - toTarget.normalized * skill.Range);
                case AiHint.Ult:
                    return ShouldUlt(self, target, distance, skill) && Cast(brain, self, key, toTarget, target.Position);
                default:
                    return false;
            }
        }

        private static bool ShouldUlt(Unit self, Unit target, float distance, SkillDefinition skill)
        {
            float reach = EffectiveRange(skill);
            if (distance > reach) return false;
            if (target.HealthFraction < GameConstants.BotUltTargetHealthFraction) return true;
            if (GameServices.World == null) return false;
            Buffer.Clear();
            GameServices.World.EnemiesInRadius(self.Position, Mathf.Max(skill.Radius, reach), self.Team, Buffer);
            int heroes = 0;
            for (int i = 0; i < Buffer.Count; i++)
            {
                if (Buffer[i].IsHero) heroes++;
            }
            return heroes >= GameConstants.BotUltEnemyCount;
        }

        /// <summary>Distance within which a skill can reach a target: melee/cone use Range, ground circles Range, self circles Radius.</summary>
        private static float EffectiveRange(SkillDefinition skill)
        {
            switch (skill.Shape)
            {
                case SkillShape.SelfCircle:
                case SkillShape.Channel:
                    return skill.Radius;
                case SkillShape.Melee:
                case SkillShape.Cone:
                    return skill.Range + GameConstants.HeroRadius;
                default:
                    return skill.Range;
            }
        }

        private static bool Cast(BotBrain brain, Unit self, SkillKey key, Vector3 dir, Vector3 point)
        {
            return self.Caster.TryCast(key, Aim(brain, dir), point);
        }

        private static Vector3 Aim(BotBrain brain, Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return brain.Owner.Facing;
            float error = brain.Random.Range(-GameConstants.BotAimErrorDegrees, GameConstants.BotAimErrorDegrees);
            return Quaternion.Euler(0f, error, 0f) * dir.normalized;
        }
    }
}
