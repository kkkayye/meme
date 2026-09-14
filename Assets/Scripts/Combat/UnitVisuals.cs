using System;
using System.Collections;
using System.Collections.Generic;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Builds a unit's body: an imported character prefab from Resources/Characters/&lt;id&gt; when one exists (with UnitAnimator), else primitives (capsule / tower). Exposes flash, squash, stretch, invisibility and death visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitVisuals : MonoBehaviour
    {
        private static readonly Vector3 CastPose = new Vector3(1.1f, 0.85f, 1.1f);
        private static readonly Vector3 ReleasePose = new Vector3(0.9f, 1.15f, 0.9f);
        private const string CharacterResourcePrefix = "Characters/";
        private const float NoseSize = 0.3f;
        private const float RingRadius = 0.75f;
        private const float DeathHideDelay = 1.6f;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material[]> _originalMaterials = new List<Material[]>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _originalColors = new List<Color>();
        private Material _translucent;
        private Renderer _ring;
        private bool _built;
        private bool _dead;
        private bool _invisible;
        private bool _flashing;
        private Coroutine _scaleRoutine;
        private Coroutine _flashRoutine;
        private Coroutine _deathRoutine;

        public Unit Owner { get; private set; }
        /// <summary>Every renderer that belongs to the body (for hit flash and invisibility).</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;
        public Color BodyColor { get; private set; } = Color.white;
        /// <summary>Root transform of the scalable body (squash & stretch target).</summary>
        public Transform Body { get; private set; }
        public bool IsBuilt => _built;
        /// <summary>True when an imported character model (not primitives) is displayed.</summary>
        public bool HasModel { get; private set; }
        public UnitAnimator Animator { get; private set; }

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        /// <summary>Creates the body (model prefab or primitives), the team ring and materials in code. Idempotent.</summary>
        public void Build(HeroDefinition hero, Team team)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (_built) return;
            Color teamColor = team == Team.Blue ? new Color(0.3f, 0.55f, 1f, 1f) : new Color(1f, 0.35f, 0.3f, 1f);
            BodyColor = hero.Kind == UnitKind.Hero ? hero.Color : Color.Lerp(teamColor, hero.Color, 0.35f);
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            Body.localPosition = Vector3.zero;
            if (!TryBuildModel(hero))
            {
                if (hero.Kind == UnitKind.Tower) BuildTower(hero);
                else BuildCapsuleBody(hero);
            }
            BuildRing(hero, teamColor);
            _translucent = PrimitiveFactory.Unlit(new Color(BodyColor.r, BodyColor.g, BodyColor.b, GameConstants.InvisibleOwnTeamAlpha));
            _built = true;
        }

        /// <summary>Instantiates Resources/Characters/&lt;heroId&gt;.prefab under Body, fits it to BodyHeight with feet on the ground and binds its Animator.</summary>
        private bool TryBuildModel(HeroDefinition hero)
        {
            GameObject prefab = Resources.Load<GameObject>(CharacterResourcePrefix + hero.Id);
            if (prefab == null) return false;
            GameObject instance = Instantiate(prefab, Body);
            instance.name = "Model";
            CharacterVisualConfig config = instance.GetComponent<CharacterVisualConfig>();
            float yaw = config != null ? config.FacingYawOffset : 0f;
            float scaleMultiplier = config != null ? config.ScaleMultiplier : 1f;
            float groundOffset = config != null ? config.GroundOffset : 0f;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(instance);
                return false;
            }
            Bounds bounds = WorldBounds(renderers);
            float scale = bounds.size.y > 1e-3f ? hero.BodyHeight / bounds.size.y * scaleMultiplier : 1f;
            instance.transform.localScale = Vector3.one * scale;
            bounds = WorldBounds(renderers);
            Vector3 offset = transform.position - bounds.center;
            offset.y = transform.position.y - bounds.min.y + groundOffset;
            instance.transform.position += offset;
            for (int i = 0; i < renderers.Length; i++) RegisterRenderer(renderers[i]);
            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator != null && Owner != null) Animator = UnitAnimator.Attach(Owner, animator);
            HasModel = true;
            return true;
        }

        private static Bounds WorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Heroes and minions without a model: capsule sized by BodyRadius/BodyHeight plus a nose cube.</summary>
        private void BuildCapsuleBody(HeroDefinition hero)
        {
            float radius = hero.BodyRadius;
            float height = hero.BodyHeight;
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(Body, false);
            capsule.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            capsule.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            PrimitiveFactory.RemoveCollider(capsule);
            AddPrimitive(capsule.GetComponent<Renderer>(), BodyColor);

            float nose = NoseSize * (radius / GameConstants.HeroRadius);
            GameObject noseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noseGo.name = "Nose";
            noseGo.transform.SetParent(Body, false);
            noseGo.transform.localPosition = new Vector3(0f, height * 0.6f, radius + nose * 0.35f);
            noseGo.transform.localScale = new Vector3(nose, nose, nose * 1.6f);
            PrimitiveFactory.RemoveCollider(noseGo);
            AddPrimitive(noseGo.GetComponent<Renderer>(), Color.Lerp(BodyColor, Color.white, 0.6f));
        }

        /// <summary>Towers without a model: a tall cylinder and a turret cube that shows facing.</summary>
        private void BuildTower(HeroDefinition hero)
        {
            float radius = hero.BodyRadius;
            float height = hero.BodyHeight;
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.transform.SetParent(Body, false);
            column.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            column.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            PrimitiveFactory.RemoveCollider(column);
            AddPrimitive(column.GetComponent<Renderer>(), Color.Lerp(BodyColor, Color.black, 0.25f));

            GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cube);
            turret.name = "Turret";
            turret.transform.SetParent(Body, false);
            turret.transform.localPosition = new Vector3(0f, height + 0.4f, radius * 0.4f);
            turret.transform.localScale = new Vector3(radius * 0.9f, 0.8f, radius * 1.4f);
            PrimitiveFactory.RemoveCollider(turret);
            AddPrimitive(turret.GetComponent<Renderer>(), Color.Lerp(BodyColor, Color.white, 0.4f));
        }

        private void BuildRing(HeroDefinition hero, Color teamColor)
        {
            float alpha = hero.Kind == UnitKind.Tower ? 0.6f : 0.85f;
            float radius = hero.Kind == UnitKind.Tower ? hero.BodyRadius * 1.6f : RingRadius * (hero.BodyRadius / GameConstants.HeroRadius);
            GameObject ring = PrimitiveFactory.Disc("TeamRing", transform, new Vector3(0f, 0.03f, 0f), radius, 0.04f, PrimitiveFactory.Unlit(new Color(teamColor.r, teamColor.g, teamColor.b, alpha)));
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            _ring = ring.GetComponent<Renderer>();
        }

        private void AddPrimitive(Renderer renderer, Color color)
        {
            renderer.sharedMaterial = PrimitiveFactory.Lit(color);
            RegisterRenderer(renderer);
        }

        /// <summary>Tracks a renderer and instanced copies of its materials (for flash and invisibility).</summary>
        private void RegisterRenderer(Renderer renderer)
        {
            Material[] instances = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
            _renderers.Add(renderer);
            _originalMaterials.Add(instances);
            for (int i = 0; i < instances.Length; i++)
            {
                _materials.Add(instances[i]);
                _originalColors.Add(instances[i] != null && instances[i].HasProperty("_Color") ? instances[i].color : Color.white);
            }
        }

        /// <summary>Invisible: translucent for the local human's team, renderers hidden for the enemy team. False restores full visibility.</summary>
        public void SetInvisible(bool invisible)
        {
            _invisible = invisible;
            Refresh();
        }

        /// <summary>Hides the body (dead) or shows it again. Models stay visible briefly so the death animation can play.</summary>
        public void SetDead(bool dead)
        {
            _dead = dead;
            if (_deathRoutine != null) StopCoroutine(_deathRoutine);
            _deathRoutine = null;
            if (dead && HasModel && isActiveAndEnabled)
            {
                _deathRoutine = StartCoroutine(HideAfterDeath());
                return;
            }
            Refresh();
        }

        /// <summary>White flash for GameConstants.HitFlashSeconds (unscaled).</summary>
        public void Flash()
        {
            if (!_built || !isActiveAndEnabled) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        /// <summary>Cast windup pose: scale (1.1, 0.85, 1.1), held until release. Skipped for animated models.</summary>
        public void SquashCast()
        {
            if (!_built || HasModel) return;
            StopScale();
            Body.localScale = CastPose;
        }

        /// <summary>Release pose: (0.9, 1.15, 0.9) easing back to 1 over GameConstants.SquashStretchSeconds. Skipped for animated models.</summary>
        public void SquashRelease()
        {
            if (!_built || HasModel || !isActiveAndEnabled) return;
            StopScale();
            _scaleRoutine = StartCoroutine(EaseScale(ReleasePose, Vector3.one, GameConstants.SquashStretchSeconds));
        }

        /// <summary>Stretches the body along its facing by factor t (0 = none, 1 = full dash stretch).</summary>
        public void StretchAlong(Vector3 dir, float t)
        {
            if (!_built) return;
            StopScale();
            t = Mathf.Clamp01(t) * (HasModel ? 0.5f : 1f);
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

        private void Refresh()
        {
            if (!_built) return;
            bool enemyView = IsEnemyOfLocalHuman();
            bool visible = !_dead && !(_invisible && enemyView);
            bool translucent = _invisible && !enemyView && !HasModel;
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.enabled = visible;
                if (_flashing) continue;
                r.sharedMaterials = translucent ? Filled(_originalMaterials[i].Length, _translucent) : _originalMaterials[i];
            }
            if (_ring != null) _ring.enabled = !_dead && !(_invisible && enemyView);
        }

        private static Material[] Filled(int count, Material material)
        {
            var array = new Material[count];
            for (int i = 0; i < count; i++) array[i] = material;
            return array;
        }

        private bool IsEnemyOfLocalHuman()
        {
            Unit human = GameServices.Match != null ? GameServices.Match.HumanUnit : null;
            return human != null && Owner != null && human.Team != Owner.Team;
        }

        private IEnumerator HideAfterDeath()
        {
            if (_ring != null) _ring.enabled = false;
            yield return new WaitForSeconds(DeathHideDelay);
            _deathRoutine = null;
            Refresh();
        }

        private IEnumerator FlashRoutine()
        {
            _flashing = true;
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null && _materials[i].HasProperty("_Color")) _materials[i].color = Color.white;
            }
            yield return new WaitForSecondsRealtime(GameConstants.HitFlashSeconds);
            RestoreColors();
            _flashing = false;
            _flashRoutine = null;
            Refresh();
        }

        private void RestoreColors()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null && _materials[i].HasProperty("_Color")) _materials[i].color = _originalColors[i];
            }
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
            for (int i = 0; i < _materials.Count; i++) PrimitiveFactory.SafeDestroy(_materials[i]);
            PrimitiveFactory.SafeDestroy(_translucent);
        }
    }
}
