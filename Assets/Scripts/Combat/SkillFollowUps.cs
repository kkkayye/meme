using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Second-stage resolution for skills with a FollowUp (蒸蚌: trap then explode; 飞天狗砸: leap then quake). Stages chain: a follow-up may itself carry a follow-up.</summary>
    public static class SkillFollowUps
    {
        /// <summary>Schedules skill.FollowUp at 'point' after skill.FollowUpDelay scaled seconds (immediately when the delay is 0). Shows a telegraph for delayed stages. No-op without a follow-up.</summary>
        public static void Schedule(Unit caster, SkillDefinition skill, Vector3 point)
        {
            if (caster == null || skill == null || skill.FollowUp == null) return;
            SkillDefinition next = skill.FollowUp;
            point.y = 0f;
            float delay = Mathf.Max(0f, skill.FollowUpDelay);
            if (delay <= 0f)
            {
                Resolve(caster, next, point);
                return;
            }
            Color color = caster.Visuals != null ? caster.Visuals.BodyColor : Color.white;
            Telegraphs.Circle(point, next.Radius, delay, color);
            CombatFx.Instance.Schedule(delay, () =>
            {
                if (caster == null) return;
                Resolve(caster, next, point);
            });
        }

        /// <summary>Resolves one stage at a point (Circle / SelfCircle semantics) and schedules the stage after it. The caster may be dead: the trap still springs, credited to them.</summary>
        public static void Resolve(Unit caster, SkillDefinition stage, Vector3 point)
        {
            if (caster == null || stage == null) return;
            SkillExecutor.ResolveCircleAt(caster, stage, point);
            Schedule(caster, stage, point);
        }
    }
}
