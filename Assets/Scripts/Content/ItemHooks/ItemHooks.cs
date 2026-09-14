using RuneArena.Combat;
using RuneArena.Core;

namespace RuneArena.Content
{
    /// <summary>冰霜之锤: basic attacks slow the target 25% for 1.5 s.</summary>
    public sealed class FrostHammerHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            e.Target.Status.Apply(StatusType.Slow, ItemPassiveConstants.FrostHammerSlowFraction, ItemPassiveConstants.FrostHammerSlowSeconds, ItemSource.Of(ItemIds.FrostHammer), Owner);
        }
    }

    /// <summary>荆棘甲: 15% of damage taken is reflected to the attacker as Magical damage.</summary>
    public sealed class ThornmailHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<UnitDamaged>(OnDamaged); }
        protected override void OnDetach() { EventBus.Unsubscribe<UnitDamaged>(OnDamaged); }

        private void OnDamaged(UnitDamaged e)
        {
            if (!IsOwner(e.Info.Target) || e.Info.Source == null || !e.Info.Source.IsAlive) return;
            if (e.Info.Tag == DamageTag.Reflect || ReferenceEquals(e.Info.Source, Owner)) return;
            float reflect = e.Result.Total * ItemPassiveConstants.ThornmailReflectFraction;
            if (reflect > 0f) DamagePipeline.Deal(Owner, e.Info.Source, reflect, DamageType.Magical, DamageTag.Reflect);
        }
    }

    /// <summary>血饮: kills grant a 100 HP shield for 3 s.</summary>
    public sealed class BloodthirsterHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<UnitDied>(OnDied); }
        protected override void OnDetach() { EventBus.Unsubscribe<UnitDied>(OnDied); }

        private void OnDied(UnitDied e)
        {
            if (!IsOwner(e.Killer) || !Owner.IsAlive) return;
            Owner.AddShield(ItemPassiveConstants.BloodthirsterShieldAmount, ItemPassiveConstants.BloodthirsterShieldSeconds, ItemSource.Of(ItemIds.Bloodthirster));
        }
    }

    /// <summary>幽梦: kills grant +30% move speed for 3 s.</summary>
    public sealed class PhantomDancerHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<UnitDied>(OnDied); }
        protected override void OnDetach() { EventBus.Unsubscribe<UnitDied>(OnDied); }

        private void OnDied(UnitDied e)
        {
            if (!IsOwner(e.Killer) || !Owner.IsAlive) return;
            Owner.Status.Apply(StatusType.SpeedBoost, ItemPassiveConstants.PhantomDancerSpeedFraction, ItemPassiveConstants.PhantomDancerSpeedSeconds, ItemSource.Of(ItemIds.PhantomDancer), Owner);
        }
    }

    /// <summary>守护天使: once per round, revive 2 s after dying with 40% max health.</summary>
    public sealed class GuardianAngelHook : CombatHookBase
    {
        private bool _usedThisRound;

        public bool UsedThisRound => _usedThisRound;

        protected override void OnAttach()
        {
            _usedThisRound = false;
            EventBus.Subscribe<UnitDied>(OnDied);
            EventBus.Subscribe<RoundStarted>(OnRound);
        }

        protected override void OnDetach()
        {
            EventBus.Unsubscribe<UnitDied>(OnDied);
            EventBus.Unsubscribe<RoundStarted>(OnRound);
        }

        private void OnRound(RoundStarted e) { _usedThisRound = false; }

        private void OnDied(UnitDied e)
        {
            if (!IsOwner(e.Victim) || _usedThisRound) return;
            _usedThisRound = true;
            Unit unit = Owner;
            CombatFx.Instance.Schedule(ItemPassiveConstants.GuardianAngelReviveDelaySeconds, () =>
            {
                if (unit == null || unit.IsAlive || !IsAttached) return;
                if (GameServices.Match != null && GameServices.Match.Phase != MatchPhase.Combat) return;
                unit.Revive(ItemPassiveConstants.GuardianAngelReviveHealthFraction);
            });
        }
    }

    /// <summary>死刑宣告: skill hits deal +8% of the target's missing health as True damage.</summary>
    public sealed class DeathSentenceHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<SkillHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<SkillHit>(OnHit); }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            float missing = e.Target.MaxHealth - e.Target.Health;
            float bonus = missing * ItemPassiveConstants.DeathSentenceMissingHealthFraction;
            if (bonus > 0f) DamagePipeline.Deal(Owner, e.Target, bonus, DamageType.True, DamageTag.Item);
        }
    }

    /// <summary>大天使: skill hits heal the owner for 5% of the damage dealt.</summary>
    public sealed class ArchangelHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<SkillHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<SkillHit>(OnHit); }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || !Owner.IsAlive) return;
            float heal = e.Result.Total * ItemPassiveConstants.ArchangelHealFraction;
            if (heal > 0f) Owner.Heal(heal, Owner);
        }
    }

    /// <summary>魔女之爪: skill hits burn the target for 5% of its max health over 3 s.</summary>
    public sealed class WitchClawHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<SkillHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<SkillHit>(OnHit); }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            e.Target.Status.ApplyBurn(e.Target.MaxHealth * ItemPassiveConstants.WitchClawBurnMaxHealthFraction, ItemPassiveConstants.WitchClawBurnSeconds, Owner, ItemSource.Of(ItemIds.WitchClaw));
        }
    }

    /// <summary>Source-id helper shared by item hooks and Inventory ("item:&lt;id&gt;").</summary>
    public static class ItemSource
    {
        public const string Prefix = "item:";

        public static string Of(string itemId)
        {
            return Prefix + (itemId ?? "");
        }
    }
}
