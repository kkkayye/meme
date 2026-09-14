using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Hero-specific damage rules that DESIGN.md attaches to particular skills (Shade backstab on Q, Shade execute on R). Everything else is data-driven.</summary>
    public static class SkillSpecials
    {
        /// <summary>Multiplier applied to a skill's raw damage against a target hit from hitDir (attacker → target direction).</summary>
        public static float SkillMultiplier(Unit caster, SkillDefinition skill, Unit target, Vector3 hitDir)
        {
            if (skill == null || target == null) return 1f;
            float multiplier = 1f;
            if (skill.Id == SkillBook.ShadeQ.Id && DamagePipeline.IsBackstab(target, hitDir))
            {
                multiplier *= 1f + GameConstants.ShadeBackstabBonus;
            }
            if (skill.Id == SkillBook.ShadeR.Id && target.HealthFraction < GameConstants.ShadeExecuteHealthThreshold)
            {
                multiplier *= GameConstants.ShadeExecuteMultiplier;
            }
            return multiplier;
        }
    }
}
