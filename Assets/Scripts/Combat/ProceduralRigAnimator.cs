using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Procedural skeletal animation for rigged models that have no animation clips: locomotion (leg/arm swing, bob, lean), attack swing, cast, hurt recoil, dash lean and death fall, driven purely by gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralRigAnimator : MonoBehaviour
    {
        private const float StrideLength = 1.7f;
        private const float LegSwing = 38f;
        private const float KneeBend = 42f;
        private const float ArmSwing = 28f;
        private const float RunLean = 10f;
        private const float DashLean = 28f;
        private const float BobHeight = 0.05f;
        private const float AttackSeconds = 0.32f;
        private const float CastSeconds = 0.4f;
        private const float HurtSeconds = 0.25f;
        private const float DeathSeconds = 0.6f;
        private const float SpeedSmoothing = 14f;

        private Unit _unit;
        private RigBoneMap _map;
        private List<Transform> _bones;
        private Quaternion[] _bind;
        private Vector3 _hipsBindPos;
        private float _phase;
        private float _speed;
        private float _attackT = 1f;
        private float _castT = 1f;
        private float _hurtT = 1f;
        private float _deathT;
        private bool _subscribed;

        public RigBoneMap Map => _map;

        /// <summary>Binds the animator to a unit and the model root that holds the skeleton. Returns null if no usable bones were found.</summary>
        public static ProceduralRigAnimator TryAttach(Unit unit, Transform modelRoot)
        {
            if (unit == null || modelRoot == null) return null;
            RigBoneMap map = RigBoneMap.Build(modelRoot);
            if (!map.IsValid) return null;
            ProceduralRigAnimator existing = unit.GetComponent<ProceduralRigAnimator>();
            if (existing == null) existing = unit.gameObject.AddComponent<ProceduralRigAnimator>();
            existing.Bind(unit, map);
            return existing;
        }

        private void Bind(Unit unit, RigBoneMap map)
        {
            _unit = unit;
            _map = map;
            _bones = map.OrderedBones();
            _bind = new Quaternion[_bones.Count];
            for (int i = 0; i < _bones.Count; i++) _bind[i] = _bones[i].localRotation;
            _hipsBindPos = map.Hips != null ? map.Hips.localPosition : Vector3.zero;
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
            if (_attackT < 1f || _castT < 1f) return;
            _hurtT = 0f;
        }

        private void OnDied(UnitDied e)
        {
            if (ReferenceEquals(e.Victim, _unit)) _deathT = 0.0001f;
        }

        private void LateUpdate()
        {
            if (_unit == null || _map == null) return;
            float dt = Time.deltaTime;
            RestoreBind();
            if (_unit.IsAlive) _deathT = 0f;
            else if (_deathT > 0f) _deathT = Mathf.Min(1f, _deathT + dt / DeathSeconds);
            Advance(ref _attackT, dt, AttackSeconds);
            Advance(ref _castT, dt, CastSeconds);
            Advance(ref _hurtT, dt, HurtSeconds);
            UpdateLocomotion(dt);
            ApplyTorso();
            ApplyLegs();
            ApplyArms();
            if (_deathT > 0f) ApplyDeath();
        }

        private static void Advance(ref float t, float dt, float seconds)
        {
            if (t < 1f) t = Mathf.Min(1f, t + dt / seconds);
        }

        private void RestoreBind()
        {
            for (int i = 0; i < _bones.Count; i++) _bones[i].localRotation = _bind[i];
            if (_map.Hips != null) _map.Hips.localPosition = _hipsBindPos;
        }

        private void UpdateLocomotion(float dt)
        {
            float maxSpeed = Mathf.Max(0.1f, _unit.Stats.Get(StatType.MoveSpeed));
            float velocity = _unit.IsAlive && _unit.Motor != null ? _unit.Motor.Velocity.magnitude : 0f;
            float target = Mathf.Clamp01(velocity / maxSpeed);
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-SpeedSmoothing * dt));
            _phase += velocity / StrideLength * Mathf.PI * 2f * dt;
        }

        private bool Dashing => _unit.Motor != null && _unit.Motor.IsDashing && !_unit.Motor.IsDisplaced;

        private void ApplyTorso()
        {
            float breathe = Mathf.Sin(Time.time * 2.2f) * 1.5f;
            float lean = _speed * RunLean + (Dashing ? DashLean : 0f);
            float hurt = _hurtT < 1f ? -14f * Mathf.Sin(_hurtT * Mathf.PI) : 0f;
            float castLean = _castT < 1f ? -8f * Mathf.Sin(_castT * Mathf.PI) : 0f;
            float attackTwist = _attackT < 1f ? 18f * Mathf.Sin(_attackT * Mathf.PI * 2f) : 0f;
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * BobHeight * _speed;
            if (_map.Hips != null)
            {
                Pitch(_map.Hips, lean + hurt + castLean);
                _map.Hips.localPosition = _hipsBindPos + Vector3.up * bob;
            }
            if (_map.Spine != null)
            {
                Pitch(_map.Spine, breathe);
                Yaw(_map.Spine, attackTwist);
            }
            if (_map.Chest != null) Pitch(_map.Chest, breathe * 0.5f + hurt * 0.5f);
        }

        private void ApplyLegs()
        {
            float swing = Mathf.Sin(_phase) * LegSwing * _speed;
            float spread = Dashing ? 18f : 0f;
            Leg(_map.LeftUpperLeg, _map.LeftLowerLeg, swing, spread);
            Leg(_map.RightUpperLeg, _map.RightLowerLeg, -swing, -spread);
        }

        private void Leg(Transform upper, Transform lower, float swing, float spread)
        {
            if (upper != null) Pitch(upper, swing + spread);
            if (lower != null) Pitch(lower, Mathf.Max(0f, swing) * (KneeBend / LegSwing));
        }

        private void ApplyArms()
        {
            float swing = Mathf.Sin(_phase) * ArmSwing * _speed;
            float leftPitch = -swing;
            float rightPitch = swing;
            float leftLower = 0f;
            float rightLower = 0f;
            if (_attackT < 1f)
            {
                float k = _attackT;
                float windup = Mathf.Clamp01(k / 0.3f);
                float strike = Mathf.Clamp01((k - 0.3f) / 0.7f);
                rightPitch = Mathf.Lerp(0f, 70f, windup) - Mathf.SmoothStep(0f, 1f, strike) * 150f;
                rightLower = -35f * (1f - strike);
            }
            if (_castT < 1f)
            {
                float k = Mathf.Sin(_castT * Mathf.PI);
                leftPitch = -95f * k;
                rightPitch = -95f * k;
                leftLower = rightLower = -25f * k;
            }
            Arm(_map.LeftUpperArm, _map.LeftLowerArm, leftPitch, leftLower);
            Arm(_map.RightUpperArm, _map.RightLowerArm, rightPitch, rightLower);
        }

        private void Arm(Transform upper, Transform lower, float pitch, float lowerPitch)
        {
            if (upper != null) Pitch(upper, pitch);
            if (lower != null) Pitch(lower, lowerPitch);
        }

        /// <summary>Falls forward around the feet and settles on the ground.</summary>
        private void ApplyDeath()
        {
            if (_map.Hips == null) return;
            float k = Mathf.SmoothStep(0f, 1f, _deathT);
            _map.Hips.RotateAround(_unit.Position, _unit.transform.right, 85f * k);
        }

        /// <summary>Rotates a bone in world space about the unit's right axis (positive = top of the bone tips forward, legs swing back).</summary>
        private void Pitch(Transform bone, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.001f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, _unit.transform.right) * bone.rotation;
        }

        private void Yaw(Transform bone, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.001f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * bone.rotation;
        }
    }
}
