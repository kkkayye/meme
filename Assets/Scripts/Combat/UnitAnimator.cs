using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Drives an Animator on an imported character model from gameplay state: locomotion speed, attack / cast / dash / hurt triggers, death. Animation never affects gameplay (no root motion).</summary>
    [DisallowMultipleComponent]
    public sealed class UnitAnimator : MonoBehaviour
    {
        public const string SpeedParam = "Speed";
        public const string AttackParam = "Attack";
        public const string CastParam = "Cast";
        public const string DashParam = "Dashing";
        public const string DeadParam = "Dead";
        public const string HurtParam = "Hurt";
        public const string AttackSpeedParam = "AttackSpeed";

        private const float SpeedSmoothing = 12f;
        private const float HurtCooldown = 0.35f;

        private Unit _unit;
        private Animator _animator;
        private float _speed;
        private float _lastHurtAt;
        private bool _hasSpeed;
        private bool _hasAttack;
        private bool _hasCast;
        private bool _hasDash;
        private bool _hasDead;
        private bool _hasHurt;
        private bool _hasAttackSpeed;
        private bool _subscribed;

        public Animator Animator => _animator;

        /// <summary>Binds the animator on a model instance to the unit that owns it.</summary>
        public static UnitAnimator Attach(Unit unit, Animator animator)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            if (animator == null) throw new System.ArgumentNullException(nameof(animator));
            UnitAnimator existing = unit.GetComponent<UnitAnimator>();
            if (existing == null) existing = unit.gameObject.AddComponent<UnitAnimator>();
            existing.Bind(unit, animator);
            return existing;
        }

        private void Bind(Unit unit, Animator animator)
        {
            _unit = unit;
            _animator = animator;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            CacheParameters();
            Subscribe();
        }

        private void CacheParameters()
        {
            _hasSpeed = _hasAttack = _hasCast = _hasDash = _hasDead = _hasHurt = _hasAttackSpeed = false;
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                switch (parameters[i].name)
                {
                    case SpeedParam: _hasSpeed = true; break;
                    case AttackParam: _hasAttack = true; break;
                    case CastParam: _hasCast = true; break;
                    case DashParam: _hasDash = true; break;
                    case DeadParam: _hasDead = true; break;
                    case HurtParam: _hasHurt = true; break;
                    case AttackSpeedParam: _hasAttackSpeed = true; break;
                }
            }
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventBus.Subscribe<SkillCast>(OnCast);
            EventBus.Subscribe<UnitDamaged>(OnDamaged);
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventBus.Unsubscribe<SkillCast>(OnCast);
            EventBus.Unsubscribe<UnitDamaged>(OnDamaged);
        }

        private void Update()
        {
            if (_unit == null || _animator == null || !_animator.isActiveAndEnabled) return;
            float maxSpeed = Mathf.Max(0.1f, _unit.Stats.Get(StatType.MoveSpeed));
            float target = _unit.IsAlive && _unit.Motor != null ? Mathf.Clamp01(_unit.Motor.Velocity.magnitude / maxSpeed) : 0f;
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-SpeedSmoothing * Time.deltaTime));
            if (_hasSpeed) _animator.SetFloat(SpeedParam, _speed);
            if (_hasDash) _animator.SetBool(DashParam, _unit.Motor != null && _unit.Motor.IsDashing && !_unit.Motor.IsDisplaced);
            if (_hasDead) _animator.SetBool(DeadParam, !_unit.IsAlive);
            if (_hasAttackSpeed) _animator.SetFloat(AttackSpeedParam, Mathf.Max(0.5f, _unit.Stats.Get(StatType.AttackSpeed)));
        }

        private void OnCast(SkillCast e)
        {
            if (!ReferenceEquals(e.Caster, _unit) || e.Skill == null || _animator == null) return;
            if (e.Skill.IsMovementSkill) return;
            if (e.Skill.IsBasicAttack)
            {
                if (_hasAttack) _animator.SetTrigger(AttackParam);
                return;
            }
            if (_hasCast) _animator.SetTrigger(CastParam);
        }

        private void OnDamaged(UnitDamaged e)
        {
            if (!ReferenceEquals(e.Info.Target, _unit) || _animator == null || !_hasHurt) return;
            if (e.Result.Total <= 0f || !_unit.IsAlive) return;
            if (_unit.Caster != null && _unit.Caster.IsCasting) return;
            if (Time.time - _lastHurtAt < HurtCooldown) return;
            _lastHurtAt = Time.time;
            _animator.SetTrigger(HurtParam);
        }
    }
}
