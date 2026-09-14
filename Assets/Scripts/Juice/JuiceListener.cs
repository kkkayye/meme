using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Juice
{
    /// <summary>Subscribes to EventBus (UnitDamaged, UnitDied, SkillCast, DashPerformed, ...) and drives hit-stop, camera shake, hit flash, VFX and SFX. Never affects gameplay.</summary>
    public sealed class JuiceListener : MonoBehaviour
    {
        private const float FullTraumaRange = 12f;
        private const float NoTraumaRange = 26f;

        private readonly HitStop _hitStop = new HitStop();
        private CameraShake _shake;
        private SfxSynth _sfx;
        private bool _subscribed;

        public Camera Cam { get; private set; }
        /// <summary>Current camera trauma 0..1 (shake = trauma squared).</summary>
        public float Trauma => _shake != null ? _shake.Trauma : 0f;
        /// <summary>True while a hit-stop is in effect (Time.timeScale lowered).</summary>
        public bool InHitStop => _hitStop.IsActive;
        public SfxSynth Sfx => _sfx;

        /// <summary>Creates the listener GameObject (or returns the existing one), binds the camera and subscribes to events.</summary>
        public static JuiceListener Install(Camera cam)
        {
            JuiceListener existing = Object.FindAnyObjectByType<JuiceListener>();
            if (existing == null)
            {
                var go = new GameObject("JuiceListener");
                existing = go.AddComponent<JuiceListener>();
            }
            existing.Bind(cam);
            return existing;
        }

        /// <summary>Requests a hit-stop of the given unscaled duration; overlapping requests coalesce (max remaining).</summary>
        public void RequestHitStop(float seconds)
        {
            if (GameServices.Match != null && GameServices.Match.IsPaused) return;
            _hitStop.Request(seconds);
        }

        /// <summary>Adds camera trauma (clamped to 1).</summary>
        public void AddTrauma(float amount)
        {
            _shake?.AddTrauma(amount);
        }

        /// <summary>Unsubscribes from EventBus and restores Time.timeScale (teardown).</summary>
        public void Uninstall()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventBus.Unsubscribe<UnitDamaged>(OnDamaged);
            EventBus.Unsubscribe<UnitDied>(OnDied);
            EventBus.Unsubscribe<SkillCast>(OnCast);
            EventBus.Unsubscribe<DashPerformed>(OnDash);
            EventBus.Unsubscribe<ChestOpened>(OnChest);
            EventBus.Unsubscribe<RuneAcquired>(OnRune);
            EventBus.Unsubscribe<ItemAcquired>(OnItem);
            _hitStop.Restore();
        }

        private void Bind(Camera cam)
        {
            Cam = cam;
            if (cam != null) _shake = CameraShake.Install(cam);
            if (_sfx == null) _sfx = new SfxSynth(gameObject);
            if (_subscribed) return;
            _subscribed = true;
            EventBus.Subscribe<UnitDamaged>(OnDamaged);
            EventBus.Subscribe<UnitDied>(OnDied);
            EventBus.Subscribe<SkillCast>(OnCast);
            EventBus.Subscribe<DashPerformed>(OnDash);
            EventBus.Subscribe<ChestOpened>(OnChest);
            EventBus.Subscribe<RuneAcquired>(OnRune);
            EventBus.Subscribe<ItemAcquired>(OnItem);
        }

        private void Update()
        {
            _hitStop.Tick(Time.unscaledDeltaTime);
        }

        private void OnDamaged(UnitDamaged e)
        {
            Unit target = e.Info.Target;
            if (target == null || e.Result.Total <= 0f) return;
            if (target.Visuals != null) target.Visuals.Flash();
            Color color = e.Info.Source != null && e.Info.Source.Visuals != null ? e.Info.Source.Visuals.BodyColor : Color.white;
            float proximity = Proximity(target.Position);
            if (!target.IsHero)
            {
                if (e.Info.Tag == DamageTag.Basic || e.Info.Tag == DamageTag.Skill) VfxFactory.Burst(target.Position + Vector3.up * 0.8f, color, 6, 0.15f, 4f);
                if (e.Info.Source != null && e.Info.Source.IsHero) _sfx?.Play(SfxSynth.Sound.Hit, 0.4f);
                return;
            }
            switch (e.Info.Tag)
            {
                case DamageTag.Basic:
                    RequestHitStop(GameConstants.HitStopBasicSeconds);
                    AddTrauma(GameConstants.TraumaBasicHit * proximity);
                    VfxFactory.Burst(target.Position + Vector3.up, color, 8, 0.18f, 4f);
                    _sfx?.Play(SfxSynth.Sound.Hit, 0.8f);
                    break;
                case DamageTag.Skill:
                    RequestHitStop(GameConstants.HitStopSkillSeconds);
                    AddTrauma(GameConstants.TraumaSkillHit * proximity);
                    VfxFactory.Burst(target.Position + Vector3.up, color, 14, 0.25f, 6f);
                    _sfx?.Play(SfxSynth.Sound.Hit);
                    break;
            }
        }

        private void OnDied(UnitDied e)
        {
            if (e.Victim == null) return;
            Color color = e.Victim.Visuals != null ? e.Victim.Visuals.BodyColor : Color.white;
            if (e.Victim.IsMinion)
            {
                VfxFactory.Burst(e.Victim.Position + Vector3.up * 0.6f, color, 10, 0.2f, 5f);
                _sfx?.Play(SfxSynth.Sound.Hit, 0.5f);
                return;
            }
            if (e.Victim.IsTower)
            {
                RequestHitStop(GameConstants.HitStopKillSeconds);
                AddTrauma(GameConstants.TraumaUlt);
                VfxFactory.Burst(e.Victim.Position + Vector3.up * 2f, color, 60, 0.5f, 10f);
                VfxFactory.Ring(e.Victim.Position, color, 6f, 0.8f);
                _sfx?.Play(SfxSynth.Sound.Kill);
                return;
            }
            RequestHitStop(GameConstants.HitStopKillSeconds);
            Unit spectated = GameServices.Match != null ? GameServices.Match.SpectatedUnit : null;
            float trauma = ReferenceEquals(e.Victim, spectated) ? GameConstants.TraumaOwnDeath : GameConstants.TraumaSkillHit * Proximity(e.Victim.Position);
            AddTrauma(trauma);
            VfxFactory.Burst(e.Victim.Position + Vector3.up, color, 28, 0.35f, 8f);
            VfxFactory.Ring(e.Victim.Position, color, 2.5f, 0.45f);
            _sfx?.Play(SfxSynth.Sound.Kill);
        }

        private void OnCast(SkillCast e)
        {
            if (e.Caster == null || e.Skill == null || e.Skill.IsBasicAttack) return;
            _sfx?.Play(SfxSynth.Sound.Cast, e.Skill.IsUltimateEffective ? 1f : 0.6f);
            if (e.Skill.IsUltimateEffective) AddTrauma(GameConstants.TraumaUlt * Proximity(e.Caster.Position));
        }

        private void OnDash(DashPerformed e)
        {
            if (e.Unit == null) return;
            Color color = e.Unit.Visuals != null ? e.Unit.Visuals.BodyColor : Color.white;
            VfxFactory.Ring(e.From, color, 1.2f, 0.3f);
        }

        private void OnChest(ChestOpened e)
        {
            _sfx?.Play(SfxSynth.Sound.Pickup);
            if (e.Unit != null) VfxFactory.Burst(e.Unit.Position + Vector3.up, new Color(1f, 0.85f, 0.3f), 18, 0.25f, 5f);
        }

        private void OnRune(RuneAcquired e)
        {
            _sfx?.Play(SfxSynth.Sound.Reveal, 0.8f);
        }

        private void OnItem(ItemAcquired e)
        {
            _sfx?.Play(SfxSynth.Sound.Click);
        }

        /// <summary>1 near the spectated unit, fading to 0 far away (keeps bot skirmishes elsewhere from shaking the camera).</summary>
        private static float Proximity(Vector3 position)
        {
            Unit spectated = GameServices.Match != null ? GameServices.Match.SpectatedUnit : null;
            if (spectated == null) return 1f;
            float d = Mathf.Sqrt(CombatWorld.FlatSqrDistance(spectated.Position, position));
            return 1f - Mathf.Clamp01((d - FullTraumaRange) / (NoTraumaRange - FullTraumaRange));
        }

        private void OnDestroy()
        {
            Uninstall();
        }
    }
}
