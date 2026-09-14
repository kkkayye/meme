using System;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Moves and rotates a unit via its CharacterController: instant WASD-style movement, facing, dashes / knockbacks, speed multipliers and lock. Y is always kept at 0.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitMotor : MonoBehaviour
    {
        private const float KnockbackSeconds = 0.15f;
        private const float MinDashStep = 0.02f;
        private const float WallStopFraction = 0.5f;

        private Vector3 _dashDir = Vector3.forward;
        private float _dashRemaining;
        private float _dashSpeed;
        private Action _dashOnComplete;
        private bool _dashIsSkill;

        public Unit Owner { get; private set; }

        /// <summary>External speed multiplier (e.g. 0.6 while casting). Status slows/boosts are read from Owner.Status separately.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>When true the unit cannot move or dash (countdown, death). Stun/Root are read from Owner.Status.</summary>
        public bool Locked { get; set; }
        public bool IsDashing { get; private set; }
        /// <summary>True while a knockback / pull displacement is running (a dash that never publishes DashPerformed).</summary>
        public bool IsDisplaced { get; private set; }
        /// <summary>Current horizontal velocity (units/s) for visuals.</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>Last non-zero movement input direction (normalized, y = 0).</summary>
        public Vector3 LastMoveDir { get; private set; } = Vector3.forward;
        public Vector3 DashDirection => _dashDir;
        public Vector3 DashStart { get; private set; }

        public bool CanMove => Owner != null && Owner.IsAlive && !Owner.IsStatic && !Locked && !IsDashing
            && Owner.Status != null && !Owner.Status.IsStunned && !Owner.Status.IsRooted;

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        /// <summary>Call every frame with the desired direction (y ignored, zero = stop). Applies MoveSpeed * SpeedMultiplier * status multiplier * dt. Keeps y = 0.</summary>
        public void Move(Vector3 dir)
        {
            dir.y = 0f;
            if (!CanMove || dir.sqrMagnitude < 1e-6f)
            {
                if (!IsDashing) Velocity = Vector3.zero;
                return;
            }
            dir.Normalize();
            LastMoveDir = dir;
            float speed = Owner.Stats.Get(StatType.MoveSpeed) * Mathf.Max(0f, SpeedMultiplier) * Owner.Status.GetMoveSpeedMultiplier();
            Velocity = dir * speed;
            Step(Velocity * Time.deltaTime);
        }

        /// <summary>Rotates the unit to face dir (y ignored). No-op for zero vectors.</summary>
        public void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>Teleports (CharacterController-safe) to a position with y = 0.</summary>
        public void SetPosition(Vector3 position)
        {
            position.y = 0f;
            CharacterController controller = Owner != null ? Owner.Controller : GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            transform.position = position;
            if (controller != null) controller.enabled = wasEnabled;
        }

        /// <summary>Starts a dash of 'distance' units along dir at 'speed' units/s, clamped by walls. Publishes DashPerformed when done and calls onComplete.</summary>
        public void BeginDash(Vector3 dir, float distance, float speed, Action onComplete)
        {
            if (Owner == null || !Owner.IsAlive || Owner.IsStatic || Locked) return;
            StartDisplacement(dir, distance, speed, onComplete, true);
        }

        /// <summary>Pushes the unit 'distance' units along dir over a short time (knockback / pull). Interrupts a running dash.</summary>
        public void BeginKnockback(Vector3 dir, float distance)
        {
            if (Owner == null || !Owner.IsAlive || Owner.IsStatic || distance <= 0f) return;
            StartDisplacement(dir, distance, distance / KnockbackSeconds, null, false);
        }

        /// <summary>Stops a dash in progress without calling its completion callback.</summary>
        public void CancelDash()
        {
            IsDashing = false;
            IsDisplaced = false;
            _dashOnComplete = null;
            _dashRemaining = 0f;
            gameObject.layer = GameConstants.UnitLayer;
        }

        private void StartDisplacement(Vector3 dir, float distance, float speed, Action onComplete, bool isSkill)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = LastMoveDir;
            if (IsDashing) CancelDash();
            _dashDir = dir.normalized;
            _dashRemaining = Mathf.Max(0f, distance);
            _dashSpeed = Mathf.Max(0.01f, speed);
            _dashOnComplete = onComplete;
            _dashIsSkill = isSkill;
            DashStart = transform.position;
            IsDashing = true;
            IsDisplaced = !isSkill;
            gameObject.layer = GameConstants.DashLayer;
            if (isSkill) Face(_dashDir);
        }

        private void Update()
        {
            if (!IsDashing) return;
            if (Owner != null && !Owner.IsAlive)
            {
                CancelDash();
                return;
            }
            float step = Mathf.Min(_dashSpeed * Time.deltaTime, _dashRemaining);
            Vector3 before = transform.position;
            CollisionFlags flags = Step(_dashDir * step);
            float moved = Vector3.Distance(Flat(before), Flat(transform.position));
            Velocity = _dashDir * _dashSpeed;
            _dashRemaining -= step;
            bool hitWall = (flags & CollisionFlags.Sides) != 0 && moved < step * WallStopFraction;
            if (_dashRemaining <= MinDashStep || hitWall) EndDash();
        }

        private void EndDash()
        {
            bool wasSkill = _dashIsSkill;
            Action callback = _dashOnComplete;
            IsDashing = false;
            IsDisplaced = false;
            _dashOnComplete = null;
            Velocity = Vector3.zero;
            gameObject.layer = GameConstants.UnitLayer;
            if (wasSkill && Owner != null) EventBus.Publish(new DashPerformed(Owner, DashStart, transform.position));
            callback?.Invoke();
        }

        private CollisionFlags Step(Vector3 delta)
        {
            delta.y = 0f;
            CharacterController controller = Owner != null ? Owner.Controller : null;
            CollisionFlags flags = CollisionFlags.None;
            if (controller != null && controller.enabled)
            {
                flags = controller.Move(delta);
            }
            else
            {
                transform.position += delta;
            }
            Vector3 p = transform.position;
            if (GameServices.World != null) p = GameServices.World.ClampToArena(p);
            p.y = 0f;
            if (p != transform.position) transform.position = p;
            return flags;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
