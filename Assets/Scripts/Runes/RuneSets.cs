using RuneArena.Combat;
using RuneArena.Core;

namespace RuneArena.Runes
{
    /// <summary>Set bonuses (3 runes of one set): Ember burn on skill hits, Iron stats, Shadow backstab basics, Storm dash cooldown refund + move speed. Applied once per unit and set by RuneInventory.</summary>
    public static class RuneSets
    {
        /// <summary>Creates the hook implementing a set bonus (null for pure-stat sets).</summary>
        public static ICombatHook CreateHook(RuneSet set)
        {
            switch (set)
            {
                case RuneSet.Ember: return new EmberSetHook();
                case RuneSet.Shadow: return new ShadowSetHook();
                case RuneSet.Storm: return new StormSetHook();
                default: return null;
            }
        }

        /// <summary>Applies the set's stat modifiers (Iron, Storm) to the unit.</summary>
        public static void ApplyModifiers(Unit unit, RuneSet set)
        {
            string source = RuneTuning.SetSourceId(set);
            switch (set)
            {
                case RuneSet.Iron:
                    unit.Stats.AddModifier(StatModifier.PercentOf(StatType.MaxHealth, GameConstants.IronSetMaxHealthPercent, source));
                    unit.Stats.AddModifier(StatModifier.FlatOf(StatType.Armor, GameConstants.IronSetArmorFlat, source));
                    break;
                case RuneSet.Storm:
                    unit.Stats.AddModifier(StatModifier.PercentOf(StatType.MoveSpeed, GameConstants.StormSetMoveSpeedPercent, source));
                    break;
            }
        }

        public static void RemoveModifiers(Unit unit, RuneSet set)
        {
            unit.Stats.RemoveBySource(RuneTuning.SetSourceId(set));
        }

        public static string Describe(RuneSet set)
        {
            switch (set)
            {
                case RuneSet.Ember: return "Ember 3: skills burn 3% max HP over 2 s";
                case RuneSet.Iron: return "Iron 3: +15% HP, +20 Armor";
                case RuneSet.Shadow: return "Shadow 3: basics from behind +25%";
                case RuneSet.Storm: return "Storm 3: dashes refund 1 s of cooldowns, +10% MS";
                default: return "";
            }
        }
    }

    /// <summary>Ember set: skill hits apply a burn of 3% of the target's max health over 2 s.</summary>
    public sealed class EmberSetHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<SkillHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<SkillHit>(OnHit); }

        private void OnHit(SkillHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            e.Target.Status.ApplyBurn(e.Target.MaxHealth * GameConstants.EmberBurnMaxHealthFraction, GameConstants.EmberBurnDuration, Owner, RuneTuning.SetSourceId(RuneSet.Ember));
        }
    }

    /// <summary>Shadow set: basic attacks from behind deal +25% bonus damage.</summary>
    public sealed class ShadowSetHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            if (!DamagePipeline.IsBackstab(e.Target, e.Target.Position - Owner.Position)) return;
            float bonus = e.Result.Total * GameConstants.ShadowSetBackstabBonus;
            if (bonus > 0f) DamagePipeline.Deal(Owner, e.Target, bonus, DamageType.Physical, DamageTag.Rune);
        }
    }

    /// <summary>Storm set: dashes and blinks refund 1 s of every cooldown.</summary>
    public sealed class StormSetHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<DashPerformed>(OnDash); }
        protected override void OnDetach() { EventBus.Unsubscribe<DashPerformed>(OnDash); }

        private void OnDash(DashPerformed e)
        {
            if (!IsOwner(e.Unit) || Owner.Caster == null) return;
            Owner.Caster.ReduceAllCooldowns(GameConstants.StormSetCooldownReset);
        }
    }
}
