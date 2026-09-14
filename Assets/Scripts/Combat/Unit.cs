using System;
using System.Collections.Generic;
using RuneArena.Core;
using RuneArena.Items;
using RuneArena.Runes;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Runtime state of one hero (human or bot): stats, health, components. Damage math lives in DamagePipeline.</summary>
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour
    {
        private static int _nextId = 1;

        private readonly List<string> _lethalGuards = new List<string>();
        private float _lastMaxHealth;

        public int Id { get; private set; }
        public string UnitName { get; private set; } = "Unit";
        public Team Team { get; private set; }
        public HeroDefinition Hero { get; private set; }
        public StatBlock Stats { get; private set; }
        public float Health { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsHuman { get; private set; }
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public Vector3 SpawnPosition { get; private set; }
        /// <summary>Set false to pause passive HealthRegen (regen only runs during MatchPhase.Combat anyway).</summary>
        public bool RegenEnabled { get; set; } = true;

        public CharacterController Controller { get; private set; }
        public UnitMotor Motor { get; private set; }
        public SkillCaster Caster { get; private set; }
        public StatusEffects Status { get; private set; }
        public Shields Shields { get; private set; }
        public RuneInventory Runes { get; private set; }
        public Inventory Items { get; private set; }
        public UnitVisuals Visuals { get; private set; }

        public bool IsSetUp => Hero != null;
        public float MaxHealth => Stats != null ? Stats.Get(StatType.MaxHealth) : 0f;
        public float HealthFraction => MaxHealth > 0f ? Mathf.Clamp01(Health / MaxHealth) : 0f;
        public bool IsInvisible => Status != null && Status.Has(StatusType.Invisible);
        public bool IsTargetable => IsAlive;
        public bool CanAct => IsAlive && Status != null && !Status.IsStunned && (Motor == null || !Motor.Locked);
        public int Gold => GameServices.Economy != null ? GameServices.Economy.GetGold(this) : 0;
        public Vector3 Position => transform.position;
        public string DebugName => UnitName + "#" + Id;

        /// <summary>Flattened forward direction (y = 0), normalized.</summary>
        public Vector3 Facing
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>Initializes the unit and adds every gameplay component. Registers with GameServices.World and publishes UnitSpawned.</summary>
        public void Setup(HeroDefinition hero, Team team, bool isHuman, string name)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (IsSetUp) throw new InvalidOperationException("Unit.Setup called twice on " + DebugName);
            Id = _nextId++;
            Hero = hero;
            Team = team;
            IsHuman = isHuman;
            UnitName = string.IsNullOrEmpty(name) ? hero.Name : name;
            gameObject.name = "Unit_" + UnitName;
            PhysicsSetup.Ensure();
            gameObject.layer = GameConstants.UnitLayer;
            Stats = StatBlock.FromHero(hero);
            _lastMaxHealth = Stats.Get(StatType.MaxHealth);
            Stats.Changed += OnStatsChanged;
            EnsureController();
            Status = GetOrAdd<StatusEffects>();
            Shields = GetOrAdd<Shields>();
            Motor = GetOrAdd<UnitMotor>();
            Caster = GetOrAdd<SkillCaster>();
            Runes = GetOrAdd<RuneInventory>();
            Items = GetOrAdd<Inventory>();
            Visuals = GetOrAdd<UnitVisuals>();
            Visuals.Build(hero, team);
            Health = MaxHealth;
            IsAlive = true;
            SpawnPosition = transform.position;
            GameServices.World?.Register(this);
            EventBus.Publish(new UnitSpawned(this));
        }

        /// <summary>Applies damage through DamagePipeline (armor, status DR, shields, HP, lifesteal, events, death).</summary>
        public DamageResult ApplyDamage(DamageInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (!ReferenceEquals(info.Target, this)) throw new ArgumentException("DamageInfo.Target must be this unit.", nameof(info));
            return DamagePipeline.Apply(info);
        }

        /// <summary>Restores health (clamped to MaxHealth) and publishes UnitHealed. Ignored when dead or amount &lt;= 0.</summary>
        public void Heal(float amount, Unit source)
        {
            if (!IsAlive || amount <= 0f) return;
            float before = Health;
            Health = Mathf.Min(Health + amount, MaxHealth);
            float healed = Health - before;
            if (healed > 0f) EventBus.Publish(new UnitHealed(this, source, healed));
        }

        public void AddShield(float amount, float duration, string sourceId)
        {
            if (!IsAlive || amount <= 0f) return;
            Shields.Add(amount, duration, sourceId);
        }

        /// <summary>Kills the unit (killer may be null), clears statuses/shields, disables the controller and publishes UnitDied.</summary>
        public void Kill(Unit killer)
        {
            if (!IsAlive) return;
            Health = 0f;
            IsAlive = false;
            Deaths++;
            if (killer != null && !ReferenceEquals(killer, this)) killer.Kills++;
            Status.Clear();
            Shields.Clear();
            Caster.CancelCast();
            Motor.CancelDash();
            Motor.Locked = true;
            if (Controller != null) Controller.enabled = false;
            Visuals.SetDead(true);
            EventBus.Publish(new UnitDied(this, killer));
        }

        /// <summary>Brings a dead unit back at the given health fraction (e.g. 守护天使). Publishes UnitRevived.</summary>
        public void Revive(float healthFraction)
        {
            if (IsAlive) return;
            IsAlive = true;
            Health = Mathf.Max(1f, MaxHealth * Mathf.Clamp01(healthFraction));
            if (Controller != null) Controller.enabled = true;
            Motor.Locked = false;
            Visuals.SetDead(false);
            EventBus.Publish(new UnitRevived(this));
        }

        /// <summary>Full heal, clear statuses/shields/cooldowns/guards, re-enable and move to spawn. Kills/Deaths and inventories are kept.</summary>
        public void ResetForRound(Vector3 spawn)
        {
            SpawnPosition = spawn;
            Status.Clear();
            Shields.Clear();
            _lethalGuards.Clear();
            IsAlive = true;
            Health = MaxHealth;
            Caster.CancelCast();
            Caster.ResetCooldowns();
            Motor.CancelDash();
            Motor.Locked = false;
            if (Controller != null) Controller.enabled = true;
            Motor.SetPosition(spawn);
            Visuals.SetDead(false);
            Visuals.SetInvisible(false);
        }

        public void AddGold(int amount)
        {
            GameServices.Economy?.AddGold(this, amount);
        }

        /// <summary>Sets health directly (clamped). Reaching 0 while alive kills the unit with no killer.</summary>
        public void SetHealth(float value)
        {
            Health = Mathf.Clamp(value, 0f, MaxHealth);
            if (Health <= 0f && IsAlive) Kill(null);
        }

        /// <summary>DamagePipeline only: writes health without any death handling.</summary>
        public void SetHealthRaw(float value)
        {
            Health = Mathf.Clamp(value, 0f, MaxHealth);
        }

        /// <summary>Removes the Invisible status (called when attacking / casting a skill that BreaksInvisibility).</summary>
        public void BreakInvisibility()
        {
            if (Status != null) Status.Remove(StatusType.Invisible);
        }

        /// <summary>Registers a one-shot "survive lethal damage at 1 HP" guard (不死 rune). Cleared on ResetForRound.</summary>
        public void AddLethalGuard(string sourceId)
        {
            if (sourceId == null) throw new ArgumentNullException(nameof(sourceId));
            if (!_lethalGuards.Contains(sourceId)) _lethalGuards.Add(sourceId);
        }

        public void RemoveLethalGuard(string sourceId)
        {
            if (sourceId != null) _lethalGuards.Remove(sourceId);
        }

        public bool HasLethalGuard => _lethalGuards.Count > 0;

        /// <summary>Consumes the oldest lethal guard, returning its source id. DamagePipeline calls this on a lethal hit.</summary>
        public bool TryConsumeLethalGuard(out string sourceId)
        {
            if (_lethalGuards.Count == 0)
            {
                sourceId = null;
                return false;
            }
            sourceId = _lethalGuards[0];
            _lethalGuards.RemoveAt(0);
            return true;
        }

        private void Update()
        {
            if (!IsAlive || !RegenEnabled || Stats == null) return;
            if (GameServices.Match != null && GameServices.Match.Phase != MatchPhase.Combat) return;
            float regen = Stats.Get(StatType.HealthRegen);
            if (regen <= 0f || Health >= MaxHealth) return;
            Health = Mathf.Min(Health + regen * Time.deltaTime, MaxHealth);
        }

        private void OnDestroy()
        {
            if (Stats != null) Stats.Changed -= OnStatsChanged;
            GameServices.World?.Unregister(this);
        }

        private void OnStatsChanged()
        {
            float newMax = Stats.Get(StatType.MaxHealth);
            float delta = newMax - _lastMaxHealth;
            if (delta > 0f && IsAlive) Health += delta;
            Health = Mathf.Min(Health, newMax);
            _lastMaxHealth = newMax;
        }

        private void EnsureController()
        {
            Controller = GetComponent<CharacterController>();
            if (Controller == null) Controller = gameObject.AddComponent<CharacterController>();
            Controller.radius = GameConstants.HeroRadius;
            Controller.height = GameConstants.HeroHeight;
            Controller.center = new Vector3(0f, GameConstants.HeroHeight * 0.5f, 0f);
        }

        private T GetOrAdd<T>() where T : Component
        {
            T existing = GetComponent<T>();
            return existing != null ? existing : gameObject.AddComponent<T>();
        }
    }
}
