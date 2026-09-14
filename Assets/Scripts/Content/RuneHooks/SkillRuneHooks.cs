using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Runes;
using UnityEngine;

namespace RuneArena.Content
{
    /// <summary>燃烧之触: skill hits apply a burn of 4% of the target's max health over 2 s.</summary>
    public sealed class BurningTouchHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<SkillHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<SkillHit>(OnHit); }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            e.Target.Status.ApplyBurn(e.Target.MaxHealth * RuneTuning.BurningTouchMaxHealthFraction, RuneTuning.BurningTouchDuration, Owner, RuneTuning.SourceId(RuneIds.BurningTouch));
        }
    }

    /// <summary>回响: skill hits deal +30 bonus Magical damage, once every 3 s.</summary>
    public sealed class EchoHook : CombatHookBase
    {
        private float _clock;
        private float _readyAt;

        protected override void OnAttach()
        {
            _clock = 0f;
            _readyAt = 0f;
            EventBus.Subscribe<SkillHit>(OnHit);
            RuneTicker.Add(Tick);
        }

        protected override void OnDetach()
        {
            EventBus.Unsubscribe<SkillHit>(OnHit);
            RuneTicker.Remove(Tick);
        }

        private void Tick(float dt) { _clock += dt; }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive || _clock < _readyAt) return;
            _readyAt = _clock + RuneTuning.EchoCooldown;
            DamagePipeline.Deal(Owner, e.Target, RuneTuning.EchoBonusDamage, DamageType.Magical, DamageTag.Rune);
        }
    }

    /// <summary>反击: taking damage stores 8% of it (up to 40% max health); the next skill hit deals the stored amount as bonus damage.</summary>
    public sealed class CounterHook : CombatHookBase
    {
        private float _stored;

        public float Stored => _stored;

        protected override void OnAttach()
        {
            _stored = 0f;
            EventBus.Subscribe<UnitDamaged>(OnDamaged);
            EventBus.Subscribe<SkillHit>(OnHit);
            EventBus.Subscribe<RoundStarted>(OnRound);
        }

        protected override void OnDetach()
        {
            EventBus.Unsubscribe<UnitDamaged>(OnDamaged);
            EventBus.Unsubscribe<SkillHit>(OnHit);
            EventBus.Unsubscribe<RoundStarted>(OnRound);
        }

        private void OnRound(RoundStarted e) { _stored = 0f; }

        private void OnDamaged(UnitDamaged e)
        {
            if (!IsOwner(e.Info.Target) || e.Info.Tag == DamageTag.Rune) return;
            float cap = Owner.MaxHealth * RuneTuning.CounterMaxStoredMaxHealthFraction;
            _stored = Mathf.Min(cap, _stored + e.Result.Total * RuneTuning.CounterStoreFraction);
        }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive || _stored <= 0f) return;
            float bonus = _stored;
            _stored = 0f;
            DamagePipeline.Deal(Owner, e.Target, bonus, e.Skill != null ? e.Skill.DamageType : DamageType.Physical, DamageTag.Rune);
        }
    }

    /// <summary>先手: the first skill cast each round deals +40% damage (its hits within a 3 s window).</summary>
    public sealed class FirstStrikeHook : CombatHookBase
    {
        private bool _usedThisRound;
        private string _armedSkillId;
        private float _windowRemaining;

        protected override void OnAttach()
        {
            _usedThisRound = false;
            _armedSkillId = null;
            EventBus.Subscribe<RoundStarted>(OnRound);
            EventBus.Subscribe<SkillCast>(OnCast);
            EventBus.Subscribe<SkillHit>(OnHit);
            RuneTicker.Add(Tick);
        }

        protected override void OnDetach()
        {
            EventBus.Unsubscribe<RoundStarted>(OnRound);
            EventBus.Unsubscribe<SkillCast>(OnCast);
            EventBus.Unsubscribe<SkillHit>(OnHit);
            RuneTicker.Remove(Tick);
        }

        private void OnRound(RoundStarted e)
        {
            _usedThisRound = false;
            _armedSkillId = null;
        }

        private void OnCast(SkillCast e)
        {
            if (!IsOwner(e.Caster) || _usedThisRound || e.Skill == null || e.Skill.IsBasicAttack || !e.Skill.DealsDamage) return;
            _usedThisRound = true;
            _armedSkillId = e.Skill.Id;
            _windowRemaining = RuneTuning.FirstStrikeWindowSeconds;
        }

        private void Tick(float dt)
        {
            if (_armedSkillId == null) return;
            _windowRemaining -= dt;
            if (_windowRemaining <= 0f) _armedSkillId = null;
        }

        private void OnHit(SkillHit e)
        {
            if (_armedSkillId == null || !IsOwner(e.Source) || e.Skill == null || e.Skill.Id != _armedSkillId) return;
            if (e.Target == null || !e.Target.IsAlive) return;
            float bonus = e.Result.Total * RuneTuning.FirstStrikeBonus;
            if (bonus > 0f) DamagePipeline.Deal(Owner, e.Target, bonus, e.Skill.DamageType, DamageTag.Rune);
        }
    }

    /// <summary>吸魂: skill hits heal the owner for 8% of the damage dealt.</summary>
    public sealed class SiphonHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<SkillHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<SkillHit>(OnHit); }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || !Owner.IsAlive) return;
            float heal = e.Result.Total * RuneTuning.SiphonHealFraction;
            if (heal > 0f) Owner.Heal(heal, Owner);
        }
    }
}
