using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Procedural whole-body motion for unrigged models: bob and lean while moving, lunge on attack, rise on cast, recoil on hurt, dash lean, topple on death. Applied to the model transform under Body.</summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralBodyMotion : MonoBehaviour
    {
        private const float StrideLength = 1.4f;
        private const float BobHeight = 0.08f;
        private const float RollDegrees = 5f;
        private const float RunLean = 9f;
        private const float DashLean = 22f;
        private const float AttackSeconds = 0.3f;
        private const float CastSeconds = 0.4f;
        private const float HurtSeconds = 0.22f;
        private const float DeathSeconds = 0.6f;
        private const float SpeedSmoothing = 14f;

        private Unit _unit;
        private Transform _model;
        private Vector3 _basePos;
        private Quaternion _baseRot;
        private Vector3 _baseScale;
        private float _phase;
        private float _speed;
        private float _attackT = 1f;
        private float _castT = 1f;
        private float _hurtT = 1f;
        private float _deathT;
        private bool _subscribed;

        public static ProceduralBodyMotion Attach(Unit unit, Transform model)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            ProceduralBodyMotion existing = unit.GetComponent<ProceduralBodyMotion>();
            if (existing == null) existing = unit.gameObject.AddComponent<ProceduralBodyMotion>();
            existing.Bind(unit, model);
            return existing;
        }

        private void Bind(Unit unit, Transform model)
        {
            _unit = unit;
            _model = model;
            _basePos = model.localPosition;
            _baseRot = model.localRotation;
            _baseScale = model.localScale;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventBus.Subscribe<SkillCast>(OnCast);
            EventBus.Subscribe<UnitDamaged>(OnDamaged);
            EventBus.Subscribe<UnitDied>(OnDied);
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventBus.Unsubscribe<SkillCast>(OnCast);
            EventBus.Unsubscribe<UnitDamaged>(OnDamaged);
            EventBus.Unsubscribe<UnitDied>(OnDied);
        }

        private void OnCast(SkillCast e)
        {
            if (!ReferenceEquals(e.Caster, _unit) || e.Skill == null || e.Skill.IsMovementSkill) return;
            if (e.Skill.IsBasicAttack) _attackT = 0f;
            else _castT = 0f;
        }

        private void OnDamaged(UnitDamaged e)
        {
            if (!ReferenceEquals(e.Info.Target, _unit) || e.Result.Total <= 0f) return;
            _hurtT = 0f;
        }

        private void OnDied(UnitDied e)
        {
            if (ReferenceEquals(e.Victim, _unit)) _deathT = 0.0001f;
        }

        private void LateUpdate()
        {
            if (_unit == null || _model == null) return;
            float dt = Time.deltaTime;
            if (_unit.IsAlive) _deathT = 0f;
            else if (_deathT > 0f) _deathT = Mathf.Min(1f, _deathT + dt / DeathSeconds);
            if (_attackT < 1f) _attackT = Mathf.Min(1f, _attackT + dt / AttackSeconds);
            if (_castT < 1f) _castT = Mathf.Min(1f, _castT + dt / CastSeconds);
            if (_hurtT < 1f) _hurtT = Mathf.Min(1f, _hurtT + dt / HurtSeconds);
            float maxSpeed = Mathf.Max(0.1f, _unit.Stats.Get(StatType.MoveSpeed));
            float velocity = _unit.IsAlive && _unit.Motor != null ? _unit.Motor.Velocity.magnitude : 0f;
            _speed = Mathf.Lerp(_speed, Mathf.Clamp01(velocity / maxSpeed), 1f - Mathf.Exp(-SpeedSmoothing * dt));
            _phase += velocity / StrideLength * Mathf.PI * 2f * dt;
            Apply();
        }

        private void Apply()
        {
            bool dashing = _unit.Motor != null && _unit.Motor.IsDashing && !_unit.Motor.IsDisplaced;
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * BobHeight * _speed;
            float roll = Mathf.Sin(_phase) * RollDegrees * _speed;
            float pitch = _speed * RunLean + (dashing ? DashLean : 0f) + Mathf.Sin(Time.time * 2.2f) * 1.2f;
            float lunge = 0f;
            float rise = 0f;
            if (_attackT < 1f)
            {
                float k = Mathf.Sin(_attackT * Mathf.PI);
                lunge = 0.35f * k;
                pitch += 16f * k;
            }
            if (_castT < 1f)
            {
                float k = Mathf.Sin(_castT * Mathf.PI);
                rise = 0.12f * k;
                pitch -= 12f * k;
            }
            if (_hurtT < 1f)
            {
                float k = Mathf.Sin(_hurtT * Mathf.PI);
                pitch -= 14f * k;
                lunge -= 0.12f * k;
            }
            float death = _deathT > 0f ? Mathf.SmoothStep(0f, 1f, _deathT) : 0f;
            Quaternion tilt = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(-80f * death, Vector3.forward);
            _model.localRotation = tilt * _baseRot;
            _model.localPosition = _basePos + Vector3.up * (bob + rise - 0.3f * death) + Vector3.forward * lunge;
            _model.localScale = _baseScale * (1f + rise * 0.6f);
        }
    }
}
