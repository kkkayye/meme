using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>One active shield (mutable runtime state owned by Shields).</summary>
    public sealed class ShieldInstance
    {
        public float Amount { get; private set; }
        public float Remaining { get; private set; }
        public string SourceId { get; }

        public ShieldInstance(float amount, float duration, string sourceId)
        {
            Amount = Mathf.Max(0f, amount);
            Remaining = Mathf.Max(0f, duration);
            SourceId = sourceId ?? "";
        }

        public bool Expired => Remaining <= 0f || Amount <= 0f;

        public void Tick(float dt)
        {
            Remaining -= dt;
        }

        /// <summary>Consumes up to 'damage' from this shield and returns the amount absorbed.</summary>
        public float Consume(float damage)
        {
            float absorbed = Mathf.Min(Amount, damage);
            Amount -= absorbed;
            return absorbed;
        }
    }

    /// <summary>Per-unit shield list. Shields absorb before HP; the soonest-expiring shield is consumed first.</summary>
    [DisallowMultipleComponent]
    public sealed class Shields : MonoBehaviour
    {
        private readonly List<ShieldInstance> _shields = new List<ShieldInstance>();

        public Unit Owner { get; private set; }
        public IReadOnlyList<ShieldInstance> Active => _shields;

        /// <summary>Raised when the total changes (add, absorb, expiry, clear).</summary>
        public event Action Changed;

        public float Total
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _shields.Count; i++) sum += _shields[i].Amount;
                return sum;
            }
        }

        public bool HasAny => _shields.Count > 0;

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        public ShieldInstance Add(float amount, float duration, string sourceId)
        {
            if (amount <= 0f || duration <= 0f) return null;
            var shield = new ShieldInstance(amount, duration, sourceId);
            _shields.Add(shield);
            Changed?.Invoke();
            return shield;
        }

        /// <summary>Absorbs up to 'damage' and returns the amount absorbed (0..damage).</summary>
        public float Absorb(float damage)
        {
            if (damage <= 0f || _shields.Count == 0) return 0f;
            _shields.Sort((a, b) => a.Remaining.CompareTo(b.Remaining));
            float absorbed = 0f;
            float remaining = damage;
            for (int i = 0; i < _shields.Count && remaining > 0f; i++)
            {
                float taken = _shields[i].Consume(remaining);
                absorbed += taken;
                remaining -= taken;
            }
            _shields.RemoveAll(s => s.Expired);
            if (absorbed > 0f) Changed?.Invoke();
            return absorbed;
        }

        public void RemoveBySource(string sourceId)
        {
            if (sourceId == null) return;
            int removed = _shields.RemoveAll(s => s.SourceId == sourceId);
            if (removed > 0) Changed?.Invoke();
        }

        public void Clear()
        {
            if (_shields.Count == 0) return;
            _shields.Clear();
            Changed?.Invoke();
        }

        private void Update()
        {
            if (_shields.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _shields.Count; i++) _shields[i].Tick(dt);
            int removed = _shields.RemoveAll(s => s.Expired);
            if (removed > 0) Changed?.Invoke();
        }
    }
}
