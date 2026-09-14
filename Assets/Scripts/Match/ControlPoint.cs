using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Center control point (radius 3): capture 0→100 at 20/s, contested pause, empty decay 10/s; on 100 grants gold, spawns a chest, resets, 10 s lockout.</summary>
    public sealed class ControlPoint : MonoBehaviour
    {
        private static readonly Color RingColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color NeutralFill = new Color(0.8f, 0.8f, 0.8f, 0.35f);
        private static readonly Color LockedFill = new Color(0.3f, 0.3f, 0.3f, 0.35f);

        private Transform _fill;
        private Material _fillMaterial;
        private Material _ringMaterial;
        private bool _built;

        public float Radius => GameConstants.ControlPointRadius;
        public Vector3 Center => transform.position;
        /// <summary>Capture progress 0..100.</summary>
        public float Progress { get; private set; }
        /// <summary>Team currently accumulating progress (null when empty or contested).</summary>
        public Team? Owner { get; private set; }
        /// <summary>Team that completed the most recent capture, or null.</summary>
        public Team? LastCapturedBy { get; private set; }
        /// <summary>Seconds of lockout remaining after a capture (0 = capturable).</summary>
        public float LockoutRemaining { get; private set; }
        public bool IsLocked => LockoutRemaining > 0f;
        public bool IsBuilt => _built;

        /// <summary>Builds the ring visuals at the given center. Idempotent.</summary>
        public void Build(Vector3 center)
        {
            center.y = 0f;
            transform.position = center;
            if (_built) return;
            _built = true;
            _ringMaterial = PrimitiveFactory.Unlit(RingColor);
            PrimitiveFactory.Disc("ControlRing", transform, center + new Vector3(0f, 0.025f, 0f), Radius, 0.02f, _ringMaterial);
            _fillMaterial = PrimitiveFactory.Unlit(NeutralFill);
            GameObject fill = PrimitiveFactory.Disc("ControlFill", transform, center + new Vector3(0f, 0.035f, 0f), 0.01f, 0.02f, _fillMaterial);
            _fill = fill.transform;
            UpdateVisuals();
        }

        /// <summary>Advances capture logic; MatchController calls this only during Combat. Also records capture seconds into GameServices.Scoring.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (IsLocked)
            {
                LockoutRemaining = Mathf.Max(0f, LockoutRemaining - dt);
                UpdateVisuals();
                return;
            }
            Team? capturing = CapturingTeam();
            if (capturing == null)
            {
                Decay(dt);
            }
            else if (Owner == null || Owner == capturing)
            {
                Owner = capturing;
                Progress = Mathf.Min(GameConstants.CaptureProgressMax, Progress + GameConstants.CaptureRatePerSecond * dt);
                GameServices.Scoring?.RecordCaptureSeconds(capturing.Value, dt);
                if (Progress >= GameConstants.CaptureProgressMax) Capture(capturing.Value);
            }
            else
            {
                Progress -= GameConstants.CaptureRatePerSecond * dt;
                if (Progress <= 0f)
                {
                    Progress = 0f;
                    Owner = capturing;
                }
            }
            UpdateVisuals();
        }

        /// <summary>Clears progress, owner and lockout (round start).</summary>
        public void ResetPoint()
        {
            Progress = 0f;
            Owner = null;
            LockoutRemaining = 0f;
            UpdateVisuals();
        }

        public bool IsInside(Vector3 position)
        {
            Vector3 d = position - Center;
            d.y = 0f;
            return d.sqrMagnitude <= Radius * Radius;
        }

        /// <summary>Team alone inside the ring (null when empty or contested).</summary>
        public Team? CapturingTeam()
        {
            CombatWorld world = GameServices.World;
            if (world == null) return null;
            bool blue = false;
            bool red = false;
            IReadOnlyList<Unit> units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !IsInside(u.Position)) continue;
                if (u.Team == Team.Blue) blue = true;
                else red = true;
            }
            if (blue == red) return null;
            return blue ? Team.Blue : Team.Red;
        }

        private void Decay(float dt)
        {
            if (Progress <= 0f) return;
            Progress = Mathf.Max(0f, Progress - GameConstants.CaptureDecayPerSecond * dt);
            if (Progress <= 0f) Owner = null;
        }

        private void Capture(Team team)
        {
            CombatWorld world = GameServices.World;
            if (world != null)
            {
                foreach (Unit u in world.UnitsOfTeam(team)) u.AddGold(GameConstants.CaptureGold);
            }
            GameServices.Chests?.SpawnAt(Center);
            LastCapturedBy = team;
            Owner = null;
            Progress = 0f;
            LockoutRemaining = GameConstants.CaptureLockoutSeconds;
            EventBus.Publish(new ControlPointCaptured(team));
        }

        private void UpdateVisuals()
        {
            if (_fill == null) return;
            float t = Progress / GameConstants.CaptureProgressMax;
            _fill.localScale = PrimitiveFactory.DiscScale(Mathf.Max(0.01f, Radius * t), 0.02f);
            Color c = IsLocked ? LockedFill : NeutralFill;
            if (Owner == Team.Blue) c = new Color(0.3f, 0.55f, 1f, 0.45f);
            else if (Owner == Team.Red) c = new Color(1f, 0.35f, 0.3f, 0.45f);
            if (_fillMaterial != null) _fillMaterial.color = c;
        }

        private void OnDestroy()
        {
            PrimitiveFactory.SafeDestroy(_fillMaterial);
            PrimitiveFactory.SafeDestroy(_ringMaterial);
        }
    }
}
