using System;
using System.Collections.Generic;

namespace RuneArena.Core
{
    /// <summary>Mutable per-unit stat container: base values plus a list of modifiers. Get() = (base + sum flat) * (1 + sum percent), with clamps.</summary>
    public sealed class StatBlock
    {
        private static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));

        private readonly Dictionary<StatType, float> _base = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, float> _cache = new Dictionary<StatType, float>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private bool _dirty = true;

        /// <summary>Raised whenever a base value or the modifier list changes.</summary>
        public event Action Changed;

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public StatBlock()
        {
            for (int i = 0; i < AllStats.Length; i++) _base[AllStats[i]] = 0f;
            _base[StatType.CritDamage] = GameConstants.DefaultCritDamage;
        }

        /// <summary>Final value after modifiers and clamps.</summary>
        public float Get(StatType stat)
        {
            if (_dirty) Recompute();
            return _cache[stat];
        }

        public float GetBase(StatType stat)
        {
            return _base[stat];
        }

        public void SetBase(StatType stat, float value)
        {
            _base[stat] = value;
            MarkDirty();
        }

        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            _modifiers.Add(modifier);
            MarkDirty();
        }

        public void AddModifiers(IReadOnlyList<StatModifier> modifiers, string sourceIdOverride = null)
        {
            if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier m = modifiers[i];
                if (m == null) continue;
                _modifiers.Add(sourceIdOverride == null ? m : m.WithSource(sourceIdOverride));
            }
            MarkDirty();
        }

        /// <summary>Removes every modifier whose SourceId equals sourceId. Returns the number removed.</summary>
        public int RemoveBySource(string sourceId)
        {
            if (sourceId == null) throw new ArgumentNullException(nameof(sourceId));
            int removed = _modifiers.RemoveAll(m => m.SourceId == sourceId);
            if (removed > 0) MarkDirty();
            return removed;
        }

        public bool HasSource(string sourceId)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].SourceId == sourceId) return true;
            }
            return false;
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count == 0) return;
            _modifiers.Clear();
            MarkDirty();
        }

        /// <summary>Applies the stat clamps from DESIGN.md section 4.</summary>
        public static float Clamp(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.CooldownReduction: return Math.Min(Math.Max(value, 0f), GameConstants.MaxCooldownReduction);
                case StatType.CritChance: return Math.Min(Math.Max(value, 0f), GameConstants.MaxCritChance);
                case StatType.Lifesteal: return Math.Max(value, 0f);
                case StatType.MaxHealth: return Math.Max(value, 1f);
                case StatType.AttackSpeed: return Math.Max(value, 0.1f);
                case StatType.MoveSpeed: return Math.Max(value, 0f);
                case StatType.AttackRange: return Math.Max(value, 0.5f);
                default: return value;
            }
        }

        /// <summary>Builds a fresh StatBlock from a hero definition's base stats (no modifiers).</summary>
        public static StatBlock FromHero(HeroDefinition hero)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            var block = new StatBlock();
            foreach (KeyValuePair<StatType, float> pair in hero.BaseStats)
            {
                block._base[pair.Key] = pair.Value;
            }
            block.MarkDirty();
            return block;
        }

        private void MarkDirty()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        private void Recompute()
        {
            for (int i = 0; i < AllStats.Length; i++)
            {
                StatType stat = AllStats[i];
                float flat = 0f;
                float percent = 0f;
                for (int m = 0; m < _modifiers.Count; m++)
                {
                    StatModifier mod = _modifiers[m];
                    if (mod.Stat != stat) continue;
                    flat += mod.Flat;
                    percent += mod.Percent;
                }
                float raw = (_base[stat] + flat) * (1f + percent);
                _cache[stat] = Clamp(stat, raw);
            }
            _dirty = false;
        }
    }
}
