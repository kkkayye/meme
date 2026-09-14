using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Runes;
using UnityEngine;

namespace RuneArena.Content
{
    /// <summary>三连击: every 3rd basic attack deals +60% bonus Physical damage.</summary>
    public sealed class TripleStrikeHook : CombatHookBase
    {
        private int _count;

        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); _count = 0; }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null) return;
            _count++;
            if (_count < RuneTuning.TripleStrikeEvery) return;
            _count = 0;
            float bonus = e.Result.Total * RuneTuning.TripleStrikeBonus;
            if (bonus > 0f && e.Target.IsAlive) DamagePipeline.Deal(Owner, e.Target, bonus, DamageType.Physical, DamageTag.Rune);
        }
    }

    /// <summary>冰霜: basic attacks slow the target 20% for 1 s.</summary>
    public sealed class FrostHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || !e.Target.IsAlive) return;
            e.Target.Status.Apply(StatusType.Slow, RuneTuning.FrostSlowFraction, RuneTuning.FrostSlowDuration, RuneTuning.SourceId(RuneIds.Frost), Owner);
        }
    }

    /// <summary>战争狂热: each basic attack hit grants +8% attack speed for 3 s, stacking up to 5 times.</summary>
    public sealed class WarFrenzyHook : CombatHookBase
    {
        private readonly List<float> _expiries = new List<float>();
        private float _clock;

        protected override void OnAttach()
        {
            _expiries.Clear();
            _clock = 0f;
            EventBus.Subscribe<BasicAttackHit>(OnHit);
            RuneTicker.Add(Tick);
        }

        protected override void OnDetach()
        {
            EventBus.Unsubscribe<BasicAttackHit>(OnHit);
            RuneTicker.Remove(Tick);
            _expiries.Clear();
            Owner.Stats.RemoveBySource(RuneTuning.SourceId(RuneIds.WarFrenzy));
        }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Source)) return;
            if (_expiries.Count >= RuneTuning.WarFrenzyMaxStacks) _expiries.RemoveAt(0);
            _expiries.Add(_clock + RuneTuning.WarFrenzyDuration);
            Reapply();
        }

        private void Tick(float dt)
        {
            _clock += dt;
            int before = _expiries.Count;
            _expiries.RemoveAll(t => t <= _clock);
            if (_expiries.Count != before) Reapply();
        }

        private void Reapply()
        {
            if (Owner == null) return;
            string source = RuneTuning.SourceId(RuneIds.WarFrenzy);
            Owner.Stats.RemoveBySource(source);
            if (_expiries.Count == 0) return;
            Owner.Stats.AddModifier(StatModifier.PercentOf(StatType.AttackSpeed, RuneTuning.WarFrenzyAttackSpeedPerStack * _expiries.Count, source));
        }
    }

    /// <summary>荆棘: reflects 12% of basic-attack damage taken back to the attacker as Magical damage.</summary>
    public sealed class ThornsHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Target) || e.Source == null || !e.Source.IsAlive) return;
            float reflect = e.Result.Total * RuneTuning.ThornsReflectFraction;
            if (reflect > 0f) DamagePipeline.Deal(Owner, e.Source, reflect, DamageType.Magical, DamageTag.Reflect);
        }
    }

    /// <summary>冰霜护甲: enemies that hit you with basic attacks are slowed 20% for 1 s.</summary>
    public sealed class FrostArmorHook : CombatHookBase
    {
        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Target) || e.Source == null || !e.Source.IsAlive) return;
            e.Source.Status.Apply(StatusType.Slow, RuneTuning.FrostArmorSlowFraction, RuneTuning.FrostArmorSlowDuration, RuneTuning.SourceId(RuneIds.FrostArmor), Owner);
        }
    }

    /// <summary>分裂弹 (Legendary): basic attack hits fire 2 extra bolts at 30% damage toward the nearest other enemies within 6 units.</summary>
    public sealed class SplitShotHook : CombatHookBase
    {
        /// <summary>Non-basic definition so split bolts are tagged Skill and never re-trigger this hook.</summary>
        public static readonly SkillDefinition SplitBolt = new SkillDefinition
        {
            Id = "rune_split_bolt", Name = "Split Bolt", Key = SkillKey.Q, Shape = SkillShape.Projectile,
            Range = RuneTuning.SplitShotRange, Radius = GameConstants.ProjectileVisualRadius, Speed = RuneTuning.SplitShotBoltSpeed,
            AdRatio = RuneTuning.SplitShotDamageFraction, DamageType = DamageType.Physical, Cooldown = 0f
        };

        private readonly List<Unit> _buffer = new List<Unit>();

        protected override void OnAttach() { EventBus.Subscribe<BasicAttackHit>(OnHit); }
        protected override void OnDetach() { EventBus.Unsubscribe<BasicAttackHit>(OnHit); }

        private void OnHit(BasicAttackHit e)
        {
            if (!IsOwner(e.Source) || e.Target == null || GameServices.World == null || !Owner.IsAlive) return;
            _buffer.Clear();
            GameServices.World.EnemiesInRadius(e.Target.Position, RuneTuning.SplitShotRange, Owner.Team, _buffer);
            _buffer.Remove(e.Target);
            _buffer.Sort((a, b) => CombatWorld.FlatSqrDistance(a.Position, e.Target.Position).CompareTo(CombatWorld.FlatSqrDistance(b.Position, e.Target.Position)));
            Vector3 origin = e.Target.Position + Vector3.up;
            int fired = 0;
            for (int i = 0; i < _buffer.Count && fired < RuneTuning.SplitShotBoltCount; i++)
            {
                Vector3 dir = _buffer[i].Position - e.Target.Position;
                if (dir.sqrMagnitude < 1e-4f) continue;
                Projectile.Spawn(Owner, SplitBolt, origin, dir, RuneTuning.SplitShotRange, 1f);
                fired++;
            }
        }
    }
}
