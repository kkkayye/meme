using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Match;
using RuneArena.Runes;
using UnityEngine;

namespace RuneArena.Content
{
    /// <summary>烈焰足迹 (Legendary): dashes and blinks leave a fire trail for 3 s that deals 20 + 20% AP Magical damage every 0.25 s to enemies standing in it.</summary>
    public sealed class FlameTrailHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<DashPerformed>(OnDash); }
        protected override void OnDetach() { EventBus.Unsubscribe<DashPerformed>(OnDash); }

        private void OnDash(DashPerformed e)
        {
            if (!IsOwner(e.Unit)) return;
            Vector3 from = e.From;
            Vector3 to = e.To;
            from.y = 0f;
            to.y = 0f;
            float length = Vector3.Distance(from, to);
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / RuneTuning.FlameTrailSegmentSpacing));
            for (int i = 0; i <= segments; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, segments == 0 ? 0f : (float)i / segments);
                FlameTrailSegment.Spawn(Owner, point);
            }
        }
    }

    /// <summary>One burning patch of the flame trail.</summary>
    public sealed class FlameTrailSegment : MonoBehaviour
    {
        private static readonly Color FlameColor = new Color(1f, 0.45f, 0.1f, 0.5f);
        private readonly List<Unit> _buffer = new List<Unit>();
        private Unit _owner;
        private float _remaining;
        private float _tick;
        private Material _material;

        public static FlameTrailSegment Spawn(Unit owner, Vector3 point)
        {
            Material material = PrimitiveFactory.Unlit(FlameColor);
            GameObject go = PrimitiveFactory.Disc("FlameTrail", CombatFx.Instance.Root, new Vector3(point.x, 0.06f, point.z), RuneTuning.FlameTrailRadius, 0.02f, material);
            var segment = go.AddComponent<FlameTrailSegment>();
            segment._owner = owner;
            segment._remaining = RuneTuning.FlameTrailSeconds;
            segment._material = material;
            return segment;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _remaining -= dt;
            _tick += dt;
            if (_remaining <= 0f || _owner == null)
            {
                Destroy(gameObject);
                return;
            }
            if (_material != null) _material.color = new Color(FlameColor.r, FlameColor.g, FlameColor.b, FlameColor.a * Mathf.Clamp01(_remaining / RuneTuning.FlameTrailSeconds));
            while (_tick >= RuneTuning.FlameTrailTickInterval)
            {
                _tick -= RuneTuning.FlameTrailTickInterval;
                Burn();
            }
        }

        private void Burn()
        {
            if (GameServices.World == null || !_owner.IsAlive) return;
            _buffer.Clear();
            GameServices.World.EnemiesInRadius(transform.position, RuneTuning.FlameTrailRadius, _owner.Team, _buffer);
            float damage = RuneTuning.FlameTrailBaseDamage + _owner.Stats.Get(StatType.AbilityPower) * RuneTuning.FlameTrailApRatio;
            for (int i = 0; i < _buffer.Count; i++)
            {
                DamagePipeline.Deal(_owner, _buffer[i], damage, DamageType.Magical, DamageTag.Rune);
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
