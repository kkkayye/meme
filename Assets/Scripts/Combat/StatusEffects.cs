using System;
using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>One active status instance (mutable runtime state owned by StatusEffects).</summary>
    public sealed class StatusInstance
    {
        public StatusType Type { get; }
        /// <summary>Slow/SpeedBoost/DamageReduction: fraction. Burn: TOTAL damage over the duration. Stun/Root/Invisible: unused (1).</summary>
        public float Magnitude { get; }
        public float TotalDuration { get; }
        public float Remaining { get; private set; }
        public string SourceId { get; }
        /// <summary>Unit credited for burn damage (may be null).</summary>
        public Unit Source { get; }
        public float TickTimer { get; set; }

        public StatusInstance(StatusType type, float magnitude, float duration, string sourceId, Unit source)
        {
            Type = type;
            Magnitude = magnitude;
            TotalDuration = Mathf.Max(0f, duration);
            Remaining = TotalDuration;
            SourceId = sourceId ?? "";
            Source = source;
        }

        public bool Expired => Remaining <= 0f;

        public void Tick(float dt)
        {
            Remaining -= dt;
        }
    }

    /// <summary>Per-unit status list: stuns, slows, boosts, damage reduction, invisibility, roots and burn ticks (0.25 s, Magical via DamagePipeline).</summary>
    [DisallowMultipleComponent]
    public sealed class StatusEffects : MonoBehaviour
    {
        private readonly List<StatusInstance> _active = new List<StatusInstance>();

        public Unit Owner { get; private set; }
        public IReadOnlyList<StatusInstance> Active => _active;
        /// <summary>Multiplier applied to Stun/Slow/Root durations on Apply (坚韧 rune sets 0.6).</summary>
        public float CrowdControlDurationMultiplier { get; set; } = 1f;

        public bool IsStunned => Has(StatusType.Stun);
        public bool IsRooted => Has(StatusType.Root);
        public bool IsInvisible => Has(StatusType.Invisible);

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        /// <summary>Adds a status instance (statuses stack as separate instances; queries take the max). Publishes StatusApplied.</summary>
        public StatusInstance Apply(StatusType type, float magnitude, float duration, string sourceId = null, Unit source = null)
        {
            if (Owner == null) Owner = GetComponent<Unit>();
            if (Owner != null && !Owner.IsAlive) return null;
            if (IsCrowdControl(type)) duration *= Mathf.Max(0f, CrowdControlDurationMultiplier);
            if (duration <= 0f && type != StatusType.Burn) return null;
            var instance = new StatusInstance(type, magnitude, duration, sourceId, source);
            _active.Add(instance);
            if (type == StatusType.Invisible) SetVisualInvisible(true);
            if (Owner != null) EventBus.Publish(new StatusApplied(Owner, type, duration));
            return instance;
        }

        /// <summary>Burn: totalDamage dealt as Magical over duration in 0.25 s ticks, credited to source.</summary>
        public StatusInstance ApplyBurn(float totalDamage, float duration, Unit source, string sourceId = null)
        {
            if (totalDamage <= 0f || duration <= 0f) return null;
            return Apply(StatusType.Burn, totalDamage, duration, sourceId, source);
        }

        public bool Has(StatusType type)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Type == type) return true;
            }
            return false;
        }

        /// <summary>Largest magnitude among instances of a type, or 0.</summary>
        public float MaxMagnitude(StatusType type)
        {
            float max = 0f;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Type == type && _active[i].Magnitude > max) max = _active[i].Magnitude;
            }
            return max;
        }

        /// <summary>Longest remaining duration among instances of a type, or 0.</summary>
        public float RemainingOf(StatusType type)
        {
            float max = 0f;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Type == type && _active[i].Remaining > max) max = _active[i].Remaining;
            }
            return max;
        }

        /// <summary>1 - strongest slow (0..1).</summary>
        public float GetSlowMultiplier()
        {
            return Mathf.Clamp01(1f - MaxMagnitude(StatusType.Slow));
        }

        /// <summary>1 + strongest speed boost.</summary>
        public float GetSpeedBoostMultiplier()
        {
            return 1f + Mathf.Max(0f, MaxMagnitude(StatusType.SpeedBoost));
        }

        /// <summary>Combined move speed multiplier (slow * boost). Stun/Root are NOT folded in; UnitMotor checks IsStunned/IsRooted.</summary>
        public float GetMoveSpeedMultiplier()
        {
            return GetSlowMultiplier() * GetSpeedBoostMultiplier();
        }

        /// <summary>Strongest damage reduction fraction (0..1). 1 = invulnerable.</summary>
        public float GetDamageReduction()
        {
            return Mathf.Clamp01(MaxMagnitude(StatusType.DamageReduction));
        }

        /// <summary>Removes every instance of a type.</summary>
        public void Remove(StatusType type)
        {
            int removed = _active.RemoveAll(s => s.Type == type);
            if (removed > 0 && type == StatusType.Invisible) SetVisualInvisible(false);
        }

        public void RemoveBySource(string sourceId)
        {
            if (sourceId == null) return;
            _active.RemoveAll(s => s.SourceId == sourceId);
            if (!Has(StatusType.Invisible)) SetVisualInvisible(false);
        }

        public void Clear()
        {
            bool wasInvisible = Has(StatusType.Invisible);
            _active.Clear();
            if (wasInvisible) SetVisualInvisible(false);
        }

        private void Update()
        {
            if (_active.Count == 0) return;
            float dt = Time.deltaTime;
            bool invisibleBefore = Has(StatusType.Invisible);
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                StatusInstance s = _active[i];
                s.Tick(dt);
                if (s.Type == StatusType.Burn) TickBurn(s, dt);
                // A burn tick may kill the owner, which clears the list; re-validate the index.
                if (i >= _active.Count || !ReferenceEquals(_active[i], s)) continue;
                if (s.Expired) _active.RemoveAt(i);
            }
            if (invisibleBefore && !Has(StatusType.Invisible)) SetVisualInvisible(false);
        }

        private void TickBurn(StatusInstance burn, float dt)
        {
            if (Owner == null || !Owner.IsAlive || burn.TotalDuration <= 0f) return;
            burn.TickTimer += dt;
            while (burn.TickTimer >= GameConstants.BurnTickInterval && Owner.IsAlive)
            {
                burn.TickTimer -= GameConstants.BurnTickInterval;
                float perTick = burn.Magnitude * (GameConstants.BurnTickInterval / burn.TotalDuration);
                DamagePipeline.DealBurnTick(burn.Source, Owner, perTick);
            }
        }

        private void SetVisualInvisible(bool invisible)
        {
            if (Owner != null && Owner.Visuals != null) Owner.Visuals.SetInvisible(invisible);
        }

        private static bool IsCrowdControl(StatusType type)
        {
            return type == StatusType.Stun || type == StatusType.Slow || type == StatusType.Root;
        }
    }
}
