using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Loot;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.AI
{
    /// <summary>Bot FSM (Seek / Fight / Retreat / Capture / Loot) evaluated every 0.1 s; drives UnitMotor and SkillCaster like a player would.</summary>
    public sealed class BotBrain : MonoBehaviour
    {
        public enum BotState { Seek, Fight, Retreat, Capture, Loot }

        private const float RangedThreshold = 3f;
        private const float StrafeFlipSeconds = 1.2f;
        private const float KiteFraction = 0.6f;
        private const float ProbeDistance = 1.4f;

        private readonly List<Unit> _buffer = new List<Unit>();
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
        private Rng _rng;

        public Unit Owner { get; private set; }
        public bool Enabled { get; private set; } = true;
        public BotState State { get; private set; } = BotState.Seek;
        /// <summary>Current FSM state name for debugging / overhead labels.</summary>
        public string StateName => State.ToString();
        /// <summary>Current attack target, or null.</summary>
        public Unit Target { get; private set; }
        public bool IsRanged => Owner != null && Owner.Stats.Get(StatType.AttackRange) > RangedThreshold;

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
            UpdateTarget(world.NearestEnemy(Owner, float.MaxValue));
            float enemyDist = Target != null ? FlatDistance(Owner.Position, Target.Position) : float.MaxValue;
            if (ShouldRetreat(enemyDist)) State = BotState.Retreat;
            else if (_clock < _retreatUntil && State == BotState.Retreat) State = BotState.Retreat;
            else if (TryPickLoot(world, enemyDist)) State = BotState.Loot;
            else if (ShouldCapture(enemyDist)) State = BotState.Capture;
            else State = Target != null ? BotState.Fight : BotState.Seek;
            Steer(enemyDist);
            if (Target != null) BotCombat.TryAct(this, Target, enemyDist);
        }

        private void UpdateTarget(Unit nearest)
        {
            if (Target != null && (!Target.IsAlive || Target.IsInvisible)) Target = null;
            if (nearest == null || ReferenceEquals(nearest, Target)) return;
            if (Target == null)
            {
                Target = nearest;
                return;
            }
            if (!ReferenceEquals(nearest, _pendingTarget))
            {
                _pendingTarget = nearest;
                _targetSwitchAt = _clock + GameConstants.BotReactionDelay;
                return;
            }
            if (_clock >= _targetSwitchAt) Target = nearest;
        }

        private bool ShouldRetreat(float enemyDist)
        {
            if (Owner.HealthFraction >= GameConstants.BotRetreatHealthFraction) return false;
            if (enemyDist > GameConstants.BotDefensiveEnemyRange * 2f) return false;
            if (BotCombat.HasReadyDefensive(Owner)) return false;
            if (State != BotState.Retreat) _retreatUntil = _clock + GameConstants.BotRetreatSeconds;
            return true;
        }

        private bool TryPickLoot(CombatWorld world, float enemyDist)
        {
            if (enemyDist < GameConstants.BotLootNoEnemyRange) return false;
            _lootTarget = world.NearestChest(Owner.Position, GameConstants.BotLootChestRange);
            return _lootTarget != null;
        }

        private bool ShouldCapture(float enemyDist)
        {
            ControlPoint point = GameServices.ControlPoint;
            if (point == null || point.IsLocked) return false;
            if (enemyDist < GameConstants.BotCaptureNoEnemyRange) return false;
            return true;
        }

        private void Steer(float enemyDist)
        {
            Vector3 desired = Vector3.zero;
            _faceDir = Vector3.zero;
            switch (State)
            {
                case BotState.Retreat: desired = RetreatDirection(); break;
                case BotState.Loot: desired = Toward(_lootTarget != null ? _lootTarget.Position : Owner.Position, 0.3f); break;
                case BotState.Capture: desired = Toward(GameServices.ControlPoint != null ? GameServices.ControlPoint.Center : Vector3.zero, 1.2f); break;
                case BotState.Fight: desired = FightDirection(enemyDist); break;
                default: desired = Target != null ? Toward(Target.Position, 1f) : Toward(GameServices.ControlPoint != null ? GameServices.ControlPoint.Center : Vector3.zero, 1.5f); break;
            }
            if (Target != null && State != BotState.Retreat) _faceDir = Target.Position - Owner.Position;
            else if (desired.sqrMagnitude > 1e-4f) _faceDir = desired;
            _moveDir = Avoid(desired);
        }

        private Vector3 FightDirection(float dist)
        {
            if (Target == null) return Vector3.zero;
            float range = Owner.Stats.Get(StatType.AttackRange);
            float preferred = IsRanged ? range * GameConstants.BotRangedStopFactor : range * 0.9f;
            Vector3 to = Target.Position - Owner.Position;
            to.y = 0f;
            if (dist > preferred) return to.normalized;
            if (IsRanged && dist < preferred * KiteFraction) return -to.normalized;
            if (_clock >= _strafeFlipAt)
            {
                _strafeFlipAt = _clock + StrafeFlipSeconds;
                _strafeSign = Random.Chance(0.5f) ? 1f : -1f;
            }
            return Vector3.Cross(Vector3.up, to.normalized) * _strafeSign;
        }

        private Vector3 RetreatDirection()
        {
            Vector3 away = Vector3.zero;
            if (Target != null) away = Owner.Position - Target.Position;
            Vector3 home = GameServices.Arena != null ? GameServices.Arena.BasePosition(Owner.Team) - Owner.Position : Vector3.zero;
            Vector3 dir = away.normalized + home.normalized * 0.5f;
            dir.y = 0f;
            return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
        }

        private Vector3 Toward(Vector3 point, float stopDistance)
        {
            Vector3 to = point - Owner.Position;
            to.y = 0f;
            return to.magnitude <= stopDistance ? Vector3.zero : to.normalized;
        }

        /// <summary>Samples straight / ±45° / ±90° against the obstacle layer and returns the first free direction.</summary>
        private Vector3 Avoid(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-4f) return dir;
            if (IsFree(dir)) return dir;
            float a = GameConstants.BotAvoidanceAngle;
            float[] angles = { a, -a, a * 2f, -a * 2f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, angles[i], 0f) * dir;
                if (IsFree(candidate)) return candidate;
            }
            return Vector3.zero;
        }

        private bool IsFree(Vector3 dir)
        {
            Vector3 probe = Owner.Position + dir.normalized * ProbeDistance;
            if (GameServices.World != null && !GameServices.World.IsWalkable(probe)) return false;
            Vector3 bottom = probe + Vector3.up * 0.6f;
            Vector3 top = probe + Vector3.up * 1.4f;
            return !Physics.CheckCapsule(bottom, top, GameConstants.HeroRadius * 0.9f, Arena.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(CombatWorld.FlatSqrDistance(a, b));
        }
    }
}
