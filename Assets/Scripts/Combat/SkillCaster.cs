using System;
using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Runs the cast state machine (windup → effect → dash/channel → recovery), cooldowns with CDR, charges, and the 0.25 s input buffer; hands resolution to SkillExecutor.</summary>
    [DisallowMultipleComponent]
    public sealed class SkillCaster : MonoBehaviour
    {
        private enum CastState { None, Windup, Dashing, Channel, Recovery }

        private const int KeyCount = 5;

        private readonly float[] _cooldown = new float[KeyCount];
        private readonly float[] _cooldownTotal = new float[KeyCount];
        private readonly int[] _charges = new int[KeyCount];
        private readonly int[] _extraCharges = new int[KeyCount];
        private readonly HashSet<Unit> _dashHits = new HashSet<Unit>();
        private bool _initialised;

        private CastState _state = CastState.None;
        private SkillDefinition _current;
        private Vector3 _aimDir;
        private Vector3 _aimPoint;
        private float _timer;
        private float _channelTick;
        private Vector3 _dashPrev;

        private bool _hasBuffer;
        private SkillKey _bufferKey;
        private Vector3 _bufferDir;
        private Vector3 _bufferPoint;
        private float _bufferTimer;

        public Unit Owner { get; private set; }
        /// <summary>True during windup, dash, channel or recovery of any skill (including basic attacks).</summary>
        public bool IsCasting => _state != CastState.None;
        /// <summary>Key currently being cast, or null.</summary>
        public SkillKey? CurrentKey { get; private set; }
        /// <summary>True while a Channel skill is running.</summary>
        public bool IsChanneling => _state == CastState.Channel;
        /// <summary>True during the windup of the current cast.</summary>
        public bool IsInWindup => _state == CastState.Windup;
        /// <summary>Definition currently being cast, or null.</summary>
        public SkillDefinition CurrentSkill => _current;

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        /// <summary>Returns the (possibly charge-modified) definition for a key, or null if the hero has none.</summary>
        public SkillDefinition GetSkill(SkillKey key)
        {
            return Owner != null && Owner.Hero != null ? Owner.Hero.GetSkill(key) : null;
        }

        /// <summary>Requests a cast toward aimDir / aimPoint (ground, y = 0). Returns true if started or buffered (buffer window 0.25 s); false if on cooldown, stunned, dead, or invalid.</summary>
        public bool TryCast(SkillKey key, Vector3 aimDir, Vector3 aimPoint)
        {
            EnsureInit();
            SkillDefinition skill = GetSkill(key);
            if (skill == null || Owner == null || !Owner.IsAlive) return false;
            if (Owner.Status.IsStunned || (Owner.Motor != null && Owner.Motor.Locked)) return false;
            if (!IsReady(key)) return false;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 1e-6f) aimDir = Owner.Facing;
            aimDir.Normalize();
            aimPoint.y = 0f;
            if (IsCasting)
            {
                bool interruptsBasic = skill.IsMovementSkill && _current != null && _current.IsBasicAttack && _state == CastState.Windup;
                if (!interruptsBasic)
                {
                    Buffer(key, aimDir, aimPoint);
                    return true;
                }
                CancelCast();
            }
            StartCast(key, skill, aimDir, aimPoint);
            return true;
        }

        /// <summary>Aborts the current cast (and any buffered input) without triggering its effect or cooldown.</summary>
        public void CancelCast()
        {
            if (_state == CastState.Dashing && Owner != null)
            {
                if (Owner.Motor != null) Owner.Motor.CancelDash();
                if (Owner.Visuals != null) Owner.Visuals.SetBodyLift(0f);
            }
            EndCast(false);
            _hasBuffer = false;
        }

        /// <summary>Seconds until the key has at least one charge available (0 if ready).</summary>
        public float GetCooldownRemaining(SkillKey key)
        {
            EnsureInit();
            int i = (int)key;
            return _charges[i] > 0 ? 0f : Mathf.Max(0f, _cooldown[i]);
        }

        /// <summary>Remaining / total cooldown in 0..1 (0 = ready). For radial HUD overlays.</summary>
        public float GetCooldownFraction(SkillKey key)
        {
            EnsureInit();
            int i = (int)key;
            if (_charges[i] > 0 || _cooldownTotal[i] <= 0f) return 0f;
            return Mathf.Clamp01(_cooldown[i] / _cooldownTotal[i]);
        }

        /// <summary>Charges currently available for the key.</summary>
        public int GetCharges(SkillKey key)
        {
            EnsureInit();
            return _charges[(int)key];
        }

        public int GetMaxCharges(SkillKey key)
        {
            SkillDefinition skill = GetSkill(key);
            if (skill == null) return 0;
            return Mathf.Max(1, skill.Charges + _extraCharges[(int)key]);
        }

        public bool IsReady(SkillKey key)
        {
            EnsureInit();
            return GetSkill(key) != null && _charges[(int)key] > 0;
        }

        /// <summary>Reduces every active cooldown by 'seconds' (Storm set). Publishes CooldownReady for any that reach 0.</summary>
        public void ReduceAllCooldowns(float seconds)
        {
            EnsureInit();
            if (seconds <= 0f) return;
            for (int i = 0; i < KeyCount; i++)
            {
                if (_cooldown[i] <= 0f) continue;
                _cooldown[i] -= seconds;
                if (_cooldown[i] <= 0f) OnCooldownElapsed((SkillKey)i);
            }
        }

        /// <summary>Clears all cooldowns and restores full charges (between rounds).</summary>
        public void ResetCooldowns()
        {
            _initialised = true;
            for (int i = 0; i < KeyCount; i++)
            {
                _cooldown[i] = 0f;
                _cooldownTotal[i] = 0f;
                _charges[i] = GetMaxCharges((SkillKey)i);
            }
        }

        /// <summary>Adds bonus charges on top of the definition's Charges (双重施法 sets +1 for Q/W/E; pass 0 to remove).</summary>
        public void SetExtraCharges(SkillKey key, int extraCharges)
        {
            EnsureInit();
            int i = (int)key;
            int before = GetMaxCharges(key);
            _extraCharges[i] = Mathf.Max(0, extraCharges);
            int after = GetMaxCharges(key);
            if (after > before) _charges[i] += after - before;
            if (_charges[i] > after) _charges[i] = after;
        }

        /// <summary>Starts (or restarts) the cooldown for a key using the effective cooldown (base * (1 - CDR)).</summary>
        public void StartCooldown(SkillKey key)
        {
            EnsureInit();
            int i = (int)key;
            float total = GetEffectiveCooldown(key);
            if (total <= 0f)
            {
                _cooldown[i] = 0f;
                return;
            }
            if (_cooldown[i] <= 0f)
            {
                _cooldown[i] = total;
                _cooldownTotal[i] = total;
            }
        }

        /// <summary>Effective cooldown for a key after CooldownReduction. Basic attacks use 1 / AttackSpeed minus their windup.</summary>
        public float GetEffectiveCooldown(SkillKey key)
        {
            SkillDefinition skill = GetSkill(key);
            if (skill == null || Owner == null) return 0f;
            if (skill.IsBasicAttack)
            {
                float attackSpeed = Mathf.Max(0.1f, Owner.Stats.Get(StatType.AttackSpeed));
                return Mathf.Max(0f, 1f / attackSpeed - skill.Windup);
            }
            return skill.Cooldown * (1f - Owner.Stats.Get(StatType.CooldownReduction));
        }

        private void EnsureInit()
        {
            if (_initialised) return;
            if (Owner == null) Owner = GetComponent<Unit>();
            if (Owner == null || Owner.Hero == null) return;
            ResetCooldowns();
        }

        private void Buffer(SkillKey key, Vector3 dir, Vector3 point)
        {
            _hasBuffer = true;
            _bufferKey = key;
            _bufferDir = dir;
            _bufferPoint = point;
            _bufferTimer = GameConstants.InputBufferSeconds;
        }

        private void StartCast(SkillKey key, SkillDefinition skill, Vector3 aimDir, Vector3 aimPoint)
        {
            _current = skill;
            CurrentKey = key;
            _aimDir = aimDir;
            _aimPoint = aimPoint;
            _state = CastState.Windup;
            _timer = skill.Windup;
            Owner.Motor.Face(aimDir);
            Owner.Motor.SpeedMultiplier = skill.RootsCasterEffective ? 0f : GameConstants.CastMoveSpeedFactor;
            Owner.Visuals.SquashCast();
            SkillExecutor.OnWindupStarted(Owner, skill, aimDir, aimPoint);
            if (_timer <= 0f) Resolve();
        }

        private void Resolve()
        {
            SkillDefinition skill = _current;
            int i = (int)CurrentKey.Value;
            if (skill.BreaksInvisibility) Owner.BreakInvisibility();
            _charges[i] = Mathf.Max(0, _charges[i] - 1);
            StartCooldown(CurrentKey.Value);
            Owner.Visuals.SquashRelease();
            EventBus.Publish(new SkillCast(Owner, skill));
            SkillExecutor.Execute(Owner, skill, _aimDir, _aimPoint);
            if (!Owner.IsAlive || _state == CastState.None) return;
            if (skill.Shape == SkillShape.Dash && Owner.Motor.IsDashing)
            {
                _state = CastState.Dashing;
                _dashHits.Clear();
                _dashPrev = Owner.Position;
                Owner.Visuals.StretchAlong(_aimDir, 1f);
                return;
            }
            if (skill.Shape == SkillShape.Channel)
            {
                _state = CastState.Channel;
                _timer = skill.Delay;
                _channelTick = 0f;
                Owner.Motor.SpeedMultiplier = 0f;
                return;
            }
            EnterRecovery();
        }

        private void EnterRecovery()
        {
            _state = CastState.Recovery;
            _timer = _current != null ? _current.Recovery : 0f;
            Owner.Motor.SpeedMultiplier = GameConstants.CastMoveSpeedFactor;
            if (_timer <= 0f) EndCast(true);
        }

        private void EndCast(bool consumeBuffer)
        {
            _state = CastState.None;
            _current = null;
            CurrentKey = null;
            if (Owner != null && Owner.Motor != null) Owner.Motor.SpeedMultiplier = 1f;
            if (!consumeBuffer || !_hasBuffer) return;
            _hasBuffer = false;
            TryCast(_bufferKey, _bufferDir, _bufferPoint);
        }

        private void Update()
        {
            EnsureInit();
            if (Owner == null || Owner.Hero == null) return;
            float dt = Time.deltaTime;
            TickCooldowns(dt);
            TickBuffer(dt);
            if (_state == CastState.None) return;
            if (!Owner.IsAlive || Owner.Status.IsStunned)
            {
                CancelCast();
                return;
            }
            switch (_state)
            {
                case CastState.Windup: TickWindup(dt); break;
                case CastState.Dashing: TickDash(); break;
                case CastState.Channel: TickChannel(dt); break;
                case CastState.Recovery: TickRecovery(dt); break;
            }
        }

        private void TickWindup(float dt)
        {
            _timer -= dt;
            if (_timer <= 0f) Resolve();
        }

        private void TickDash()
        {
            Vector3 now = Owner.Position;
            SkillExecutor.DashTick(Owner, _current, _dashPrev, now, _dashHits);
            _dashPrev = now;
            if (Owner.Motor.IsDashing)
            {
                if (_current.LeapHeight > 0f) Owner.Visuals.SetBodyLift(LeapLift(now));
                return;
            }
            Owner.Visuals.SetBodyLift(0f);
            Owner.Visuals.ResetScale();
            SkillDefinition finished = _current;
            EnterRecovery();
            SkillExecutor.OnDashEnded(Owner, finished);
        }

        /// <summary>Half-sine arc over the dash distance (visual only; hit queries stay on the ground).</summary>
        private float LeapLift(Vector3 now)
        {
            float total = Mathf.Max(0.01f, _current.Range);
            Vector3 start = Owner.Motor.DashStart;
            start.y = 0f;
            now.y = 0f;
            float t = Mathf.Clamp01(Vector3.Distance(start, now) / total);
            return _current.LeapHeight * Mathf.Sin(t * Mathf.PI);
        }

        private void TickChannel(float dt)
        {
            _timer -= dt;
            _channelTick += dt;
            while (_channelTick >= GameConstants.ChannelTickInterval && _state == CastState.Channel)
            {
                _channelTick -= GameConstants.ChannelTickInterval;
                SkillExecutor.ChannelTick(Owner, _current);
            }
            if (_state == CastState.Channel && _timer <= 0f) EnterRecovery();
        }

        private void TickRecovery(float dt)
        {
            _timer -= dt;
            if (_timer <= 0f) EndCast(true);
        }

        private void TickCooldowns(float dt)
        {
            for (int i = 0; i < KeyCount; i++)
            {
                if (_cooldown[i] <= 0f) continue;
                _cooldown[i] -= dt;
                if (_cooldown[i] <= 0f) OnCooldownElapsed((SkillKey)i);
            }
        }

        private void OnCooldownElapsed(SkillKey key)
        {
            int i = (int)key;
            _cooldown[i] = 0f;
            int max = GetMaxCharges(key);
            bool wasEmpty = _charges[i] <= 0;
            _charges[i] = Mathf.Min(max, _charges[i] + 1);
            if (_charges[i] < max)
            {
                float total = GetEffectiveCooldown(key);
                _cooldown[i] = total;
                _cooldownTotal[i] = total;
            }
            if (wasEmpty && Owner != null && !GetSkill(key).IsBasicAttack) EventBus.Publish(new CooldownReady(Owner, key));
        }

        private void TickBuffer(float dt)
        {
            if (!_hasBuffer) return;
            _bufferTimer -= dt;
            if (_bufferTimer <= 0f) _hasBuffer = false;
        }
    }
}
