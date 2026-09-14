using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.AI
{
    /// <summary>Shared obstacle avoidance for bots and minions: samples straight / ±45° / ±90° and returns the first free direction.</summary>
    public static class Steering
    {
        private const float ProbeDistance = 1.4f;

        public static Vector3 Avoid(Unit unit, Vector3 dir)
        {
            if (unit == null || dir.sqrMagnitude < 1e-4f) return dir;
            dir.y = 0f;
            dir.Normalize();
            if (IsFree(unit, dir)) return dir;
            float a = GameConstants.BotAvoidanceAngle;
            float[] angles = { a, -a, a * 2f, -a * 2f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, angles[i], 0f) * dir;
                if (IsFree(unit, candidate)) return candidate;
            }
            return Vector3.zero;
        }

        public static bool IsFree(Unit unit, Vector3 dir)
        {
            Vector3 probe = unit.Position + dir.normalized * (ProbeDistance + unit.BodyRadius);
            if (GameServices.World != null && !GameServices.World.IsWalkable(probe)) return false;
            Vector3 bottom = probe + Vector3.up * (unit.BodyRadius + 0.1f);
            Vector3 top = probe + Vector3.up * Mathf.Max(unit.BodyRadius + 0.2f, unit.BodyHeight - unit.BodyRadius);
            return !Physics.CheckCapsule(bottom, top, unit.BodyRadius * 0.9f, Arena.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Direction toward a point, or zero when within stopDistance (measured center to center).</summary>
        public static Vector3 Toward(Unit unit, Vector3 point, float stopDistance)
        {
            Vector3 to = point - unit.Position;
            to.y = 0f;
            return to.magnitude <= stopDistance ? Vector3.zero : to.normalized;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(CombatWorld.FlatSqrDistance(a, b));
        }
    }
}
