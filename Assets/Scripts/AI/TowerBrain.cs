using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.AI
{
    /// <summary>Tower AI: shoots the nearest enemy in range (minions first, heroes otherwise), switches to heroes that hit allied heroes under it, and ramps damage on consecutive hero hits.</summary>
    public sealed class TowerBrain : MonoBehaviour
    {
        private const string HeatSource = "tower_heat";
        private const float HeatResetSeconds = 2f;

        private float _tick;
        private float _clock;
        private Unit _aggro;
        private float _aggroUntil;
        private Unit _heatTarget;
        private int _heat;
        private float _lastShotAt;
        private bool _subscribed;

        public Unit Owner { get; private set; }
        public Unit Target { get; private set; }
        public int Heat => _heat;

        public static TowerBrain Attach(Unit unit)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            TowerBrain brain = unit.GetComponent<TowerBrain>();
            if (brain == null) brain = unit.gameObject.AddComponent<TowerBrain>();
            brain.Owner = unit;
            brain.Subscribe();
            return brain;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventBus.Subscribe<UnitDamaged>(OnDamaged);
            EventBus.Subscribe<BasicAttackHit>(OnHit);
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventBus.Unsubscribe<UnitDamaged>(OnDamaged);
            EventBus.Unsubscribe<BasicAttackHit>(OnHit);
        }

        private void Update()
        {
            if (Owner == null || !Owner.IsAlive) return;
            MatchController match = GameServices.Match;
            if (match == null || match.Phase != MatchPhase.Combat || match.IsPaused) return;
            float dt = Time.deltaTime;
            _clock += dt;
            _tick -= dt;
            if (_tick > 0f) return;
            _tick = GameConstants.TowerTickInterval;
            Decide();
        }

        private void Decide()
        {
            CombatWorld world = GameServices.World;
            if (world == null) return;
            Target = PickTarget(world);
            if (Target == null)
            {
                if (_clock - _lastShotAt > HeatResetSeconds) ResetHeat();
                return;
            }
            Vector3 to = Target.Position - Owner.Position;
            Owner.Motor.Face(to);
            if (!Owner.Caster.IsCasting) Owner.Caster.TryCast(SkillKey.Basic, to, Target.Position);
        }

        private Unit PickTarget(CombatWorld world)
        {
            float range = Owner.Stats.Get(StatType.AttackRange);
            if (_aggro != null && _aggro.IsAlive && _clock < _aggroUntil && InRange(_aggro, range)) return _aggro;
            _aggro = null;
            Unit minion = world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Minion, range + GameConstants.MinionRadius);
            if (minion != null) return minion;
            return world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Hero, range + GameConstants.HeroRadius);
        }

        private bool InRange(Unit unit, float range)
        {
            return Steering.FlatDistance(Owner.Position, unit.Position) <= range + unit.BodyRadius;
        }

        /// <summary>An enemy hero hitting an allied hero under the tower draws its fire for a few seconds.</summary>
        private void OnDamaged(UnitDamaged e)
        {
            if (Owner == null || !Owner.IsAlive) return;
            Unit source = e.Info.Source;
            Unit target = e.Info.Target;
            if (source == null || target == null || !source.IsHero || !target.IsHero) return;
            if (source.Team == Owner.Team || target.Team != Owner.Team) return;
            float range = Owner.Stats.Get(StatType.AttackRange);
            if (!InRange(target, range) || !InRange(source, range)) return;
            _aggro = source;
            _aggroUntil = _clock + GameConstants.TowerAggroSeconds;
        }

        /// <summary>Consecutive hits on the same hero ramp damage by +25% each, up to +100%.</summary>
        private void OnHit(BasicAttackHit e)
        {
            if (!ReferenceEquals(e.Source, Owner) || e.Target == null) return;
            _lastShotAt = _clock;
            if (!e.Target.IsHero)
            {
                ResetHeat();
                return;
            }
            if (ReferenceEquals(e.Target, _heatTarget)) _heat = Mathf.Min(_heat + 1, Mathf.RoundToInt(GameConstants.TowerHeatMax / GameConstants.TowerHeatPerShot));
            else
            {
                _heatTarget = e.Target;
                _heat = 0;
            }
            ApplyHeat();
        }

        private void ResetHeat()
        {
            _heat = 0;
            _heatTarget = null;
            ApplyHeat();
        }

        private void ApplyHeat()
        {
            if (Owner == null || Owner.Stats == null) return;
            Owner.Stats.RemoveBySource(HeatSource);
            if (_heat <= 0) return;
            Owner.Stats.AddModifier(StatModifier.PercentOf(StatType.AttackDamage, _heat * GameConstants.TowerHeatPerShot, HeatSource));
        }
    }
}
