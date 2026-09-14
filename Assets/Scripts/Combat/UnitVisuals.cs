using System;
using System.Collections;
using System.Collections.Generic;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Builds the hero body from primitives (capsule, nose cube, team ring) and exposes flash / squash / stretch / invisibility / death visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitVisuals : MonoBehaviour
    {
        private static readonly Vector3 CastPose = new Vector3(1.1f, 0.85f, 1.1f);
        private static readonly Vector3 ReleasePose = new Vector3(0.9f, 1.15f, 0.9f);
        private const float NoseSize = 0.3f;
        private const float RingRadius = 0.75f;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material> _materials = new List<Material>();
        private Material _translucent;
        private Renderer _ring;
        private bool _built;
        private bool _dead;
        private bool _invisible;
        private bool _flashing;
        private Coroutine _scaleRoutine;
        private Coroutine _flashRoutine;

        public Unit Owner { get; private set; }
        /// <summary>Every renderer that belongs to the body (for hit flash and invisibility).</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;
        public Color BodyColor { get; private set; } = Color.white;
        /// <summary>Root transform of the scalable body (squash & stretch target).</summary>
        public Transform Body { get; private set; }
        public bool IsBuilt => _built;

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        /// <summary>Creates capsule (radius 0.5, height 2), nose cube, team ring and materials in code. Idempotent.</summary>
        public void Build(HeroDefinition hero, Team team)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (_built) return;
            Color teamColor = team == Team.Blue ? new Color(0.3f, 0.55f, 1f, 1f) : new Color(1f, 0.35f, 0.3f, 1f);
            BodyColor = hero.Kind == UnitKind.Hero ? hero.Color : Color.Lerp(teamColor, hero.Color, 0.35f);
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            Body.localPosition = Vector3.zero;
            if (hero.Kind == UnitKind.Tower) BuildTower(hero, teamColor);
            else BuildCapsuleBody(hero, teamColor);
            _translucent = PrimitiveFactory.Unlit(new Color(BodyColor.r, BodyColor.g, BodyColor.b, GameConstants.InvisibleOwnTeamAlpha));
            _built = true;
        }

        /// <summary>Heroes and minions: capsule sized by BodyRadius/BodyHeight, a nose cube and a team ring.</summary>
        private void BuildCapsuleBody(HeroDefinition hero, Color teamColor)
        {
            float radius = hero.BodyRadius;
            float height = hero.BodyHeight;
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(Body, false);
            capsule.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            capsule.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            PrimitiveFactory.RemoveCollider(capsule);
            AddBodyRenderer(capsule.GetComponent<Renderer>(), BodyColor);

            float nose = NoseSize * (radius / GameConstants.HeroRadius);
            GameObject noseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noseGo.name = "Nose";
            noseGo.transform.SetParent(Body, false);
            noseGo.transform.localPosition = new Vector3(0f, height * 0.6f, radius + nose * 0.35f);
            noseGo.transform.localScale = new Vector3(nose, nose, nose * 1.6f);
            PrimitiveFactory.RemoveCollider(noseGo);
            AddBodyRenderer(noseGo.GetComponent<Renderer>(), Color.Lerp(BodyColor, Color.white, 0.6f));

            Color ringColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.85f);
            float ringRadius = RingRadius * (radius / GameConstants.HeroRadius);
            GameObject ring = PrimitiveFactory.Disc("TeamRing", transform, new Vector3(0f, 0.03f, 0f), ringRadius, 0.04f, PrimitiveFactory.Unlit(ringColor));
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            _ring = ring.GetComponent<Renderer>();
        }

        /// <summary>Towers: a tall cylinder, a turret cube that shows facing, and a base disc.</summary>
        private void BuildTower(HeroDefinition hero, Color teamColor)
        {
            float radius = hero.BodyRadius;
            float height = hero.BodyHeight;
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.transform.SetParent(Body, false);
            column.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            column.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            PrimitiveFactory.RemoveCollider(column);
            AddBodyRenderer(column.GetComponent<Renderer>(), Color.Lerp(BodyColor, Color.black, 0.25f));

            GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cube);
            turret.name = "Turret";
            turret.transform.SetParent(Body, false);
            turret.transform.localPosition = new Vector3(0f, height + 0.4f, radius * 0.4f);
            turret.transform.localScale = new Vector3(radius * 0.9f, 0.8f, radius * 1.4f);
            PrimitiveFactory.RemoveCollider(turret);
            AddBodyRenderer(turret.GetComponent<Renderer>(), Color.Lerp(BodyColor, Color.white, 0.4f));

            Color ringColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.6f);
            GameObject ring = PrimitiveFactory.Disc("Base", transform, new Vector3(0f, 0.03f, 0f), radius * 1.6f, 0.06f, PrimitiveFactory.Unlit(ringColor));
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            _ring = ring.GetComponent<Renderer>();
        }

        /// <summary>Invisible: alpha 0.3 for the local human's team, renderers hidden for the enemy team. False restores full visibility.</summary>
        public void SetInvisible(bool invisible)
        {
            _invisible = invisible;
            Refresh();
        }

        /// <summary>Hides the body (dead) or shows it again.</summary>
        public void SetDead(bool dead)
        {
            _dead = dead;
            Refresh();
        }

        /// <summary>White flash for GameConstants.HitFlashSeconds (unscaled).</summary>
        public void Flash()
        {
            if (!_built || !isActiveAndEnabled) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        /// <summary>Cast windup pose: scale (1.1, 0.85, 1.1), held until release.</summary>
        public void SquashCast()
        {
            if (!_built) return;
            StopScale();
            Body.localScale = CastPose;
        }

        /// <summary>Release pose: (0.9, 1.15, 0.9) easing back to 1 over GameConstants.SquashStretchSeconds.</summary>
        public void SquashRelease()
        {
            if (!_built || !isActiveAndEnabled) return;
            StopScale();
            _scaleRoutine = StartCoroutine(EaseScale(ReleasePose, Vector3.one, GameConstants.SquashStretchSeconds));
        }

        /// <summary>Stretches the body along its facing (dashes are always along facing) by factor t (0 = none, 1 = full dash stretch).</summary>
        public void StretchAlong(Vector3 dir, float t)
        {
            if (!_built) return;
            StopScale();
            t = Mathf.Clamp01(t);
            Body.localScale = new Vector3(1f - 0.15f * t, 1f - 0.15f * t, 1f + 0.35f * t);
        }

        /// <summary>Resets the body scale to 1 (called when a dash ends).</summary>
        public void ResetScale()
        {
            if (!_built) return;
            StopScale();
            if (isActiveAndEnabled) _scaleRoutine = StartCoroutine(EaseScale(Body.localScale, Vector3.one, GameConstants.SquashStretchSeconds));
            else Body.localScale = Vector3.one;
        }

        private void AddBodyRenderer(Renderer renderer, Color color)
        {
            Material material = PrimitiveFactory.Lit(color);
            renderer.sharedMaterial = material;
            _renderers.Add(renderer);
            _materials.Add(material);
        }

        private void Refresh()
        {
            if (!_built) return;
            bool enemyView = IsEnemyOfLocalHuman();
            bool visible = !_dead && !(_invisible && enemyView);
            for (int i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].enabled = visible;
                if (!_flashing) _renderers[i].sharedMaterial = _invisible && !enemyView ? _translucent : _materials[i];
            }
            if (_ring != null) _ring.enabled = !_dead && !(_invisible && enemyView);
        }

        private bool IsEnemyOfLocalHuman()
        {
            Unit human = GameServices.Match != null ? GameServices.Match.HumanUnit : null;
            return human != null && Owner != null && human.Team != Owner.Team;
        }

        private IEnumerator FlashRoutine()
        {
            _flashing = true;
            for (int i = 0; i < _materials.Count; i++) _materials[i].color = Color.white;
            yield return new WaitForSecondsRealtime(GameConstants.HitFlashSeconds);
            RestoreColors();
            _flashing = false;
            _flashRoutine = null;
            Refresh();
        }

        private void RestoreColors()
        {
            if (_materials.Count == 0) return;
            _materials[0].color = BodyColor;
            for (int i = 1; i < _materials.Count; i++) _materials[i].color = Color.Lerp(BodyColor, Color.white, 0.6f);
        }

        private IEnumerator EaseScale(Vector3 from, Vector3 to, float seconds)
        {
            float t = 0f;
            Body.localScale = from;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                Body.localScale = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }
            Body.localScale = to;
            _scaleRoutine = null;
        }

        private void StopScale()
        {
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = null;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                PrimitiveFactory.SafeDestroy(_materials[i]);
            }
            PrimitiveFactory.SafeDestroy(_translucent);
        }
    }
}
