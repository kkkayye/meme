using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Runes;

namespace RuneArena.Content
{
    /// <summary>冲刺护盾: dashes and blinks grant a shield of 10% max health for 2 s.</summary>
    public sealed class DashShieldHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<DashPerformed>(OnDash); }
        protected override void OnDetach() { EventBus.Unsubscribe<DashPerformed>(OnDash); }

        private void OnDash(DashPerformed e)
        {
            if (!IsOwner(e.Unit) || !Owner.IsAlive) return;
            Owner.AddShield(Owner.MaxHealth * RuneTuning.DashShieldMaxHealthFraction, RuneTuning.DashShieldDuration, RuneTuning.SourceId(RuneIds.DashShield));
        }
    }

    /// <summary>猎杀: kills restore 20% max health.</summary>
    public sealed class HunterHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<UnitDied>(OnDied); }
        protected override void OnDetach() { EventBus.Unsubscribe<UnitDied>(OnDied); }

        private void OnDied(UnitDied e)
        {
            if (!IsOwner(e.Killer) || !Owner.IsAlive) return;
            Owner.Heal(Owner.MaxHealth * RuneTuning.HunterHealFraction, Owner);
        }
    }

    /// <summary>破釜沉舟: below 30% health, +25% attack damage and ability power.</summary>
    public sealed class LastStandHook : CombatHookBase
    {
        private bool _active;

        protected override void OnAttach()
        {
            _active = false;
            RuneTicker.Add(Tick);
        }

        protected override void OnDetach()
        {
            RuneTicker.Remove(Tick);
            SetActive(false);
        }

        private void Tick(float dt)
        {
            if (Owner == null) return;
            bool low = Owner.IsAlive && Owner.HealthFraction < RuneTuning.LastStandHealthFraction;
            if (low != _active) SetActive(low);
        }

        private void SetActive(bool active)
        {
            _active = active;
            if (Owner == null) return;
            string source = RuneTuning.SourceId(RuneIds.LastStand);
            Owner.Stats.RemoveBySource(source);
            if (!active) return;
            Owner.Stats.AddModifier(StatModifier.PercentOf(StatType.AttackDamage, RuneTuning.LastStandBonusPercent, source));
            Owner.Stats.AddModifier(StatModifier.PercentOf(StatType.AbilityPower, RuneTuning.LastStandBonusPercent, source));
        }
    }

    /// <summary>迅影: kills grant +40% move speed for 3 s.</summary>
    public sealed class SwiftShadowHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<UnitDied>(OnDied); }
        protected override void OnDetach() { EventBus.Unsubscribe<UnitDied>(OnDied); }

        private void OnDied(UnitDied e)
        {
            if (!IsOwner(e.Killer) || !Owner.IsAlive) return;
            Owner.Status.Apply(StatusType.SpeedBoost, RuneTuning.SwiftShadowSpeedBoost, RuneTuning.SwiftShadowDuration, RuneTuning.SourceId(RuneIds.SwiftShadow), Owner);
        }
    }

    /// <summary>坚韧: stun, slow and root durations are reduced by 40%.</summary>
    public sealed class TenacityHook : CombatHookBase
    {
        protected override void OnAttach() { Owner.Status.CrowdControlDurationMultiplier = RuneTuning.TenacityCrowdControlMultiplier; }
        protected override void OnDetach() { if (Owner.Status != null) Owner.Status.CrowdControlDurationMultiplier = 1f; }
    }

    /// <summary>复苏: at the start of every round gain a 150 shield for 5 s.</summary>
    public sealed class RevivalHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<RoundStarted>(OnRound); }
        protected override void OnDetach() { EventBus.Unsubscribe<RoundStarted>(OnRound); }

        private void OnRound(RoundStarted e)
        {
            if (Owner == null || !Owner.IsAlive) return;
            Owner.AddShield(RuneTuning.RevivalShield, RuneTuning.RevivalShieldDuration, RuneTuning.SourceId(RuneIds.Revival));
        }
    }

    /// <summary>双重施法 (Legendary): Q, W and E gain a second charge.</summary>
    public sealed class DoubleCastHook : CombatHookBase
    {
        protected override void OnAttach()
        {
            Owner.Caster.SetExtraCharges(SkillKey.Q, RuneTuning.DoubleCastExtraCharges);
            Owner.Caster.SetExtraCharges(SkillKey.W, RuneTuning.DoubleCastExtraCharges);
            Owner.Caster.SetExtraCharges(SkillKey.E, RuneTuning.DoubleCastExtraCharges);
        }

        protected override void OnDetach()
        {
            if (Owner.Caster == null) return;
            Owner.Caster.SetExtraCharges(SkillKey.Q, 0);
            Owner.Caster.SetExtraCharges(SkillKey.W, 0);
            Owner.Caster.SetExtraCharges(SkillKey.E, 0);
        }
    }

    /// <summary>不死 (Legendary): once per round, survive lethal damage at 1 HP and gain 60% damage reduction for 3 s.</summary>
    public sealed class UndyingHook : CombatHookBase
    {
        private string SourceId => RuneTuning.SourceId(RuneIds.Undying);

        protected override void OnAttach()
        {
            Owner.AddLethalGuard(SourceId);
            EventBus.Subscribe<RoundStarted>(OnRound);
            EventBus.Subscribe<LethalDamagePrevented>(OnPrevented);
        }

        protected override void OnDetach()
        {
            Owner.RemoveLethalGuard(SourceId);
            EventBus.Unsubscribe<RoundStarted>(OnRound);
            EventBus.Unsubscribe<LethalDamagePrevented>(OnPrevented);
        }

        private void OnRound(RoundStarted e)
        {
            if (Owner != null) Owner.AddLethalGuard(SourceId);
        }

        private void OnPrevented(LethalDamagePrevented e)
        {
            if (!IsOwner(e.Unit) || e.SourceId != SourceId) return;
            Owner.Status.Apply(StatusType.DamageReduction, RuneTuning.UndyingDamageReduction, RuneTuning.UndyingDamageReductionSeconds, SourceId, Owner);
        }
    }
}
