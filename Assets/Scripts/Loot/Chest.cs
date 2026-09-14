using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Loot
{
    /// <summary>A treasure chest in the arena: gold cube with bob/rotate animation; a living unit within 1.2 units for 1.0 s opens it (progress cancels when leaving).</summary>
    public sealed class Chest : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.8f, 0.25f, 1f);
        private static readonly Color RingColor = new Color(1f, 0.9f, 0.5f, 0.6f);
        private const float BobAmplitude = 0.12f;
        private const float BobSpeed = 2.5f;
        private const float SpinDegreesPerSecond = 60f;
        private const float ProgressRingRadius = 1.3f;

        private readonly List<Unit> _nearby = new List<Unit>();
        private Transform _body;
        private Transform _progressRing;
        private Material _bodyMaterial;
        private Material _ringMaterial;
        private float _progress;
        private float _clock;
        private bool _built;

        public Vector3 Position => transform.position;
        public bool IsOpened { get; private set; }
        /// <summary>Unit currently channelling the open, or null.</summary>
        public Unit CurrentOpener { get; private set; }

        /// <summary>Builds the visuals and places the chest (called by ChestSpawner right after AddComponent).</summary>
        public void Setup(Vector3 position)
        {
            position.y = 0f;
            transform.position = position;
            if (_built) return;
            _built = true;
            float size = GameConstants.ChestSize;
            _bodyMaterial = PrimitiveFactory.Lit(GoldColor);
            GameObject body = PrimitiveFactory.Cube("ChestBody", transform, position + new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, _bodyMaterial, 0);
            PrimitiveFactory.RemoveCollider(body);
            _body = body.transform;
            _ringMaterial = PrimitiveFactory.Unlit(RingColor);
            GameObject ring = PrimitiveFactory.Disc("ChestProgress", transform, position + new Vector3(0f, 0.04f, 0f), ProgressRingRadius, 0.02f, _ringMaterial);
            _progressRing = ring.transform;
            _progressRing.localScale = PrimitiveFactory.DiscScale(0.01f, 0.02f);
        }

        /// <summary>Open progress 0..1 for the given unit (0 if it is not channelling this chest).</summary>
        public float GetProgress(Unit unit)
        {
            if (unit == null || !ReferenceEquals(unit, CurrentOpener)) return 0f;
            return Mathf.Clamp01(_progress / GameConstants.ChestOpenSeconds);
        }

        /// <summary>Opens the chest for the unit immediately (LootService.OpenChest), then despawns.</summary>
        public void ForceOpen(Unit opener)
        {
            if (IsOpened || opener == null) return;
            IsOpened = true;
            CurrentOpener = null;
            GameServices.Loot?.OpenChest(opener);
            Despawn();
        }

        /// <summary>Unregisters from CombatWorld / ChestSpawner and destroys the GameObject.</summary>
        public void Despawn()
        {
            GameServices.World?.UnregisterChest(this);
            GameServices.Chests?.Forget(this);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (IsOpened) return;
            Animate();
            TickChannel(Time.deltaTime);
        }

        private void Animate()
        {
            _clock += Time.deltaTime;
            if (_body != null)
            {
                Vector3 p = transform.position;
                _body.position = new Vector3(p.x, GameConstants.ChestSize * 0.5f + Mathf.Sin(_clock * BobSpeed) * BobAmplitude, p.z);
                _body.rotation = Quaternion.Euler(0f, _clock * SpinDegreesPerSecond, 0f);
            }
        }

        private void TickChannel(float dt)
        {
            Unit opener = ResolveOpener();
            if (!ReferenceEquals(opener, CurrentOpener))
            {
                CurrentOpener = opener;
                _progress = 0f;
            }
            if (CurrentOpener == null)
            {
                UpdateRing(0f);
                return;
            }
            _progress += dt;
            UpdateRing(Mathf.Clamp01(_progress / GameConstants.ChestOpenSeconds));
            if (_progress >= GameConstants.ChestOpenSeconds) ForceOpen(CurrentOpener);
        }

        /// <summary>Keeps the current opener while it stays in range; otherwise the nearest living unit in range.</summary>
        private Unit ResolveOpener()
        {
            CombatWorld world = GameServices.World;
            if (world == null) return null;
            if (CurrentOpener != null && CurrentOpener.IsAlive && InRange(CurrentOpener)) return CurrentOpener;
            Unit best = null;
            float bestSqr = float.MaxValue;
            IReadOnlyList<Unit> units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !InRange(u)) continue;
                float sqr = CombatWorld.FlatSqrDistance(u.Position, transform.position);
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = u;
            }
            return best;
        }

        private bool InRange(Unit unit)
        {
            float r = GameConstants.ChestOpenRadius;
            return CombatWorld.FlatSqrDistance(unit.Position, transform.position) <= r * r;
        }

        private void UpdateRing(float t)
        {
            if (_progressRing == null) return;
            float radius = Mathf.Max(0.01f, ProgressRingRadius * t);
            _progressRing.localScale = PrimitiveFactory.DiscScale(radius, 0.02f);
        }

        private void OnDestroy()
        {
            PrimitiveFactory.SafeDestroy(_bodyMaterial);
            PrimitiveFactory.SafeDestroy(_ringMaterial);
        }
    }
}
