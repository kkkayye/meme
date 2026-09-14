using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;

namespace RuneArena.Match
{
    /// <summary>Per-unit gold ledger: starting gold, passive income, kill/assist/round/capture rewards, spending. Publishes GoldChanged.</summary>
    public sealed class Economy
    {
        private readonly Dictionary<Unit, int> _gold = new Dictionary<Unit, int>();
        private readonly Dictionary<Unit, float> _passive = new Dictionary<Unit, float>();
        private readonly List<Unit> _units = new List<Unit>();

        /// <summary>Current gold (0 for unknown units).</summary>
        public int GetGold(Unit unit)
        {
            return unit != null && _gold.TryGetValue(unit, out int gold) ? gold : 0;
        }

        /// <summary>Adds (or removes, if negative) gold, clamped at 0, and publishes GoldChanged when the value changes.</summary>
        public void AddGold(Unit unit, int amount)
        {
            if (unit == null || amount == 0) return;
            int before = GetGold(unit);
            int after = System.Math.Max(0, before + amount);
            if (after == before) return;
            _gold[unit] = after;
            EventBus.Publish(new GoldChanged(unit, after, after - before));
        }

        /// <summary>Sets gold directly (match start = GameConstants.StartingGold).</summary>
        public void SetGold(Unit unit, int amount)
        {
            if (unit == null) return;
            int before = GetGold(unit);
            int after = System.Math.Max(0, amount);
            if (!_gold.ContainsKey(unit)) _units.Add(unit);
            _gold[unit] = after;
            _passive[unit] = 0f;
            if (after != before) EventBus.Publish(new GoldChanged(unit, after, after - before));
        }

        /// <summary>Deducts if affordable; returns false (no change) otherwise.</summary>
        public bool TrySpend(Unit unit, int amount)
        {
            if (unit == null || amount < 0) return false;
            if (GetGold(unit) < amount) return false;
            AddGold(unit, -amount);
            return true;
        }

        /// <summary>Resets every known unit to StartingGold (match start).</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _units.Count; i++) SetGold(_units[i], GameConstants.StartingGold);
        }

        /// <summary>Registers a unit with starting gold.</summary>
        public void Register(Unit unit)
        {
            if (unit == null) return;
            SetGold(unit, GameConstants.StartingGold);
        }

        /// <summary>Forgets every unit (match teardown).</summary>
        public void Clear()
        {
            _gold.Clear();
            _passive.Clear();
            _units.Clear();
        }

        /// <summary>Accrues passive income (GameConstants.PassiveGoldPerSecond) for living units; called by MatchController only during Combat.</summary>
        public void TickPassive(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _units.Count; i++)
            {
                Unit unit = _units[i];
                if (unit == null || !unit.IsAlive) continue;
                float acc = (_passive.TryGetValue(unit, out float a) ? a : 0f) + GameConstants.PassiveGoldPerSecond * dt;
                int whole = (int)acc;
                _passive[unit] = acc - whole;
                if (whole > 0) AddGold(unit, whole);
            }
        }
    }
}
