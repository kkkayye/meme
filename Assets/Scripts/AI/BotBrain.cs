using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Loot;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.AI
{
    /// <summary>Hero bot FSM (Seek / Fight / Retreat / Capture / Loot / Push) evaluated every 0.1 s; drives UnitMotor and SkillCaster like a player would. Fights heroes first, farms minions, pushes the tower behind its wave and backs off when a tower would shoot it alone.</summary>
    public sealed class BotBrain : MonoBehaviour
    {
        public enum BotState { Seek, Fight, Retreat, Capture, Loot, Push }

        private const float RangedThreshold = 3f;
        private const float StrafeFlipSeconds = 1.2f;
        private const float KiteFraction = 0.6f;
        private const float HeroEngageRange = 9f;
        private const float MinionFarmRange = 12f;
        private const float TowerSafetyMargin = 1.5f;
        private const float MinionsNearTowerRange = 6f;
        private const float DiveTowerHealthFraction = 0.25f;
        private const float DiveHealthFraction = 0.45f;

        private float _tickTimer;
        private float _clock;
        private float _retreatUntil;
        private float _strafeFlipAt;
        private float _strafeSign = 1f;
        private Unit _pendingTarget;
        private float _targetSwitchAt;
        private Vector3 _moveDir;
        private Vector3 _faceDir;
        private Chest _lootTarget;
        private Unit _towerThreat;
        private Rng _rng;

        public Unit Owner { get; private set; }
        public bool Enabled { get; private set; } = true;
        public BotState State { get; private set; } = BotState.Seek;
        /// <summary>Current FSM state name for debugging / overhead labels.</summary>
        public string StateName => State.ToString();
        /// <summary>Current attack target (hero, minion or tower), or null.</summary>
        public Unit Target { get; private set; }
        public bool IsRanged => Owner != null && Owner.Stats.Get(StatType.AttackRange) > RangedThreshold;
        private Team EnemyTeam => Owner.Team == Team.Blue ? Team.Red : Team.Blue;

        /// <summary>Adds (or returns the existing) BotBrain on the unit and binds it.</summary>
        public static BotBrain Attach(Unit unit)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            BotBrain brain = unit.GetComponent<BotBrain>();
            if (brain == null) brain = unit.gameObject.AddComponent<BotBrain>();
            brain.Owner = unit;
            return brain;
        }

        /// <summary>Enables/disables decision making (disabled during countdown, pause, non-combat phases).</summary>
        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            if (!enabled) _moveDir = Vector3.zero;
        }

        /// <summary>Deterministic per-bot random source (forked from the match seed).</summary>
        public Rng Random
        {
            get
            {
                if (_rng == null) _rng = GameServices.RngOrDefault().Fork("bot:" + (Owner != null ? Owner.DebugName : name));
                return _rng;
            }
        }

        private void Update()
        {
            if (!Enabled || Owner == null || !Owner.IsAlive) return;
            MatchController match = GameServices.Match;
            if (match == null || match.Phase != MatchPhase.Combat || match.IsPaused) return;
            float dt = Time.deltaTime;
            _clock += dt;
            _tickTimer -= dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = GameConstants.BotTickInterval;
                Decide();
            }
            Act();
        }

        private void Act()
        {
            if (!Owner.CanAct)
            {
                Owner.Motor.Move(Vector3.zero);
                return;
            }
            Owner.Motor.Move(_moveDir);
            if (!Owner.Motor.IsDashing && _faceDir.sqrMagnitude > 1e-4f) Owner.Motor.Face(_faceDir);
        }

        private void Decide()
        {
            CombatWorld world = GameServices.World;
            if (world == null) return;
            Unit hero = world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Hero, float.MaxValue);
            float heroDist = hero != null ? Steering.FlatDistance(Owner.Position, hero.Position) : float.MaxValue;
            _towerThreat = TowerThreat(world);
            UpdateHeroTarget(hero, heroDist);
            State = ChooseState(world, heroDist);
            Steer(world, heroDist);
            float targetDist = Target != null ? Steering.FlatDistance(Owner.Position, Target.Position) - Target.BodyRadius + GameConstants.HeroRadius : float.MaxValue;
            if (Target != null && State != BotState.Retreat) BotCombat.TryAct(this, Target, targetDist);
        }

        /// <summary>Heroes are engaged with a reaction delay; minions and towers are picked instantly when no hero is close.</summary>
        private void UpdateHeroTarget(Unit hero, float heroDist)
        {
            if (Target != null && (!Target.IsAlive || Target.IsInvisible)) Target = null;
            if (hero != null && heroDist <= HeroEngageRange)
            {
                if (Target != null && Target.IsHero && ReferenceEquals(Target, hero)) return;
                if (Target == null || !Target.IsHero)
                {
                    Target = hero;
                    return;
                }
                if (!ReferenceEquals(hero, _pendingTarget))
                {
                    _pendingTarget = hero;
                    _targetSwitchAt = _clock + GameConstants.BotReactionDelay;
                    return;
                }
                if (_clock >= _targetSwitchAt) Target = hero;
                return;
            }
            if (Target != null && Target.IsHero) Target = null;
        }

        private BotState ChooseState(CombatWorld world, float heroDist)
        {
            if (ShouldRetreat(heroDist)) return BotState.Retreat;
            if (_clock < _retreatUntil && State == BotState.Retreat) return BotState.Retreat;
            if (Target != null && Target.IsHero) return BotState.Fight;
            if (TryPickLoot(world, heroDist)) return BotState.Loot;
            if (ShouldCapture(heroDist)) return BotState.Capture;
            Unit minion = world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Minion, MinionFarmRange);
            if (minion != null)
            {
                Target = minion;
                return BotState.Fight;
            }
            Unit tower = world.TowerOf(EnemyTeam);
            if (tower != null && CanPushTower(world, tower))
            {
                Target = tower;
                return BotState.Push;
            }
            Target = null;
            return BotState.Seek;
        }

        private bool ShouldRetreat(float heroDist)
        {
            bool lowHealth = Owner.HealthFraction < GameConstants.BotRetreatHealthFraction && heroDist <= GameConstants.BotDefensiveEnemyRange * 2f && !BotCombat.HasReadyDefensive(Owner);
            bool towerDanger = _towerThreat != null && !SafeUnderTower(GameServices.World, _towerThreat);
            if (!lowHealth && !towerDanger) return false;
            if (State != BotState.Retreat) _retreatUntil = _clock + (towerDanger ? 0.5f : GameConstants.BotRetreatSeconds);
            return true;
        }

        /// <summary>The enemy tower if this bot stands inside its firing range.</summary>
        private Unit TowerThreat(CombatWorld world)
        {
            Unit tower = world.TowerOf(EnemyTeam);
            if (tower == null) return null;
            float range = tower.Stats.Get(StatType.AttackRange) + Owner.BodyRadius + TowerSafetyMargin;
            return Steering.FlatDistance(Owner.Position, tower.Position) <= range ? tower : null;
        }

        /// <summary>Standing under the enemy tower is fine when allied minions tank it (and we are healthy) or the tower is nearly dead.</summary>
        private bool SafeUnderTower(CombatWorld world, Unit tower)
        {
            if (world == null || tower == null) return true;
            if (tower.HealthFraction < DiveTowerHealthFraction) return true;
            if (Owner.HealthFraction < DiveHealthFraction) return false;
            return AlliedMinionsNear(world, tower.Position) > 0;
        }

        private bool CanPushTower(CombatWorld world, Unit tower)
        {
            return tower.HealthFraction < DiveTowerHealthFraction || AlliedMinionsNear(world, tower.Position) > 0;
        }

        private int AlliedMinionsNear(CombatWorld world, Vector3 point)
        {
            int count = 0;
            foreach (Unit ally in world.AlliesOf(Owner.Team))
            {
                if (ally.IsMinion && Steering.FlatDistance(ally.Position, point) <= MinionsNearTowerRange) count++;
            }
            return count;
        }

        private bool TryPickLoot(CombatWorld world, float heroDist)
        {
            if (heroDist < GameConstants.BotLootNoEnemyRange) return false;
            _lootTarget = world.NearestChest(Owner.Position, GameConstants.BotLootChestRange);
            return _lootTarget != null;
        }

        private bool ShouldCapture(float heroDist)
        {
            ControlPoint point = GameServices.ControlPoint;
            if (point == null || point.IsLocked) return false;
            if (heroDist < GameConstants.BotCaptureNoEnemyRange) return false;
            return point.Owner != Owner.Team || point.Progress < GameConstants.CaptureProgressMax;
        }

        private void Steer(CombatWorld world, float heroDist)
        {
            Vector3 desired = Vector3.zero;
            _faceDir = Vector3.zero;
            switch (State)
            {
                case BotState.Retreat: desired = RetreatDirection(); break;
                case BotState.Loot: desired = Steering.Toward(Owner, _lootTarget != null ? _lootTarget.Position : Owner.Position, 0.3f); break;
                case BotState.Capture: desired = Steering.Toward(Owner, GameServices.ControlPoint != null ? GameServices.ControlPoint.Center : Vector3.zero, 1.2f); break;
                case BotState.Fight:
                case BotState.Push: desired = FightDirection(); break;
                default: desired = SeekDirection(world); break;
            }
            if (Target != null && State != BotState.Retreat) _faceDir = Target.Position - Owner.Position;
            else if (desired.sqrMagnitude > 1e-4f) _faceDir = desired;
            _moveDir = Steering.Avoid(Owner, desired);
        }

        private Vector3 FightDirection()
        {
            if (Target == null) return Vector3.zero;
            float range = Owner.Stats.Get(StatType.AttackRange) + Target.BodyRadius;
            float preferred = IsRanged ? range * GameConstants.BotRangedStopFactor : range * 0.9f;
            Vector3 to = Target.Position - Owner.Position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > preferred) return to.normalized;
            if (!Target.IsHero) return Vector3.zero;
            if (IsRanged && dist < preferred * KiteFraction) return -to.normalized;
            if (_clock >= _strafeFlipAt)
            {
                _strafeFlipAt = _clock + StrafeFlipSeconds;
                _strafeSign = Random.Chance(0.5f) ? 1f : -1f;
            }
            return Vector3.Cross(Vector3.up, to.normalized) * _strafeSign;
        }

        /// <summary>No targets: walk with the wave toward the enemy tower, but hold outside its range when no allied minions are there.</summary>
        private Vector3 SeekDirection(CombatWorld world)
        {
            Unit tower = world.TowerOf(EnemyTeam);
            Vector3 goal = GameServices.ControlPoint != null ? GameServices.ControlPoint.Center : Vector3.zero;
            float stop = 1.5f;
            if (tower != null)
            {
                goal = tower.Position;
                stop = tower.Stats.Get(StatType.AttackRange) + Owner.BodyRadius + TowerSafetyMargin + 1f;
                if (AlliedMinionsNear(world, tower.Position) > 0) stop = 3f;
            }
            return Steering.Toward(Owner, goal, stop);
        }

        private Vector3 RetreatDirection()
        {
            Vector3 away = Vector3.zero;
            if (_towerThreat != null) away = Owner.Position - _towerThreat.Position;
            else if (Target != null) away = Owner.Position - Target.Position;
            Vector3 home = GameServices.Arena != null ? GameServices.Arena.BasePosition(Owner.Team) - Owner.Position : Vector3.zero;
            Vector3 dir = away.normalized + home.normalized * 0.5f;
            dir.y = 0f;
            return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
        }
    }
}
