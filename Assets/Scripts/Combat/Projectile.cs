using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>A flying bolt (sphere + trail) that hits the first living enemy along its path, then applies the skill through SkillExecutor.HitTarget.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        private const float FlightHeight = 1f;

        private Unit _owner;
        private SkillDefinition _skill;
        private Vector3 _dir;
        private float _speed;
        private float _remaining;
        private float _hitRadius;
        private float _damageMultiplier;
        private bool _done;
        private Renderer _renderer;
        private TrailRenderer _trail;

        public Unit Owner => _owner;
        public SkillDefinition Skill => _skill;
        public bool IsDone => _done;

        /// <summary>Creates a projectile at origin flying along dir for 'range' units. damageMultiplier scales the hit (split bolts use 0.3).</summary>
        public static Projectile Spawn(Unit owner, SkillDefinition skill, Vector3 origin, Vector3 dir, float range, float damageMultiplier)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (skill == null) throw new System.ArgumentNullException(nameof(skill));
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = owner.Facing;
            dir.Normalize();
            Color color = owner.Visuals != null ? owner.Visuals.BodyColor : Color.white;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile_" + skill.Id;
            go.transform.SetParent(CombatFx.Instance.Root, false);
            go.transform.position = new Vector3(origin.x, FlightHeight, origin.z);
            go.transform.localScale = Vector3.one * (GameConstants.ProjectileVisualRadius * 2f);
            PrimitiveFactory.RemoveCollider(go);
            var p = go.AddComponent<Projectile>();
            p._owner = owner;
            p._skill = skill;
            p._dir = dir;
            p._speed = Mathf.Max(0.1f, skill.Speed);
            p._remaining = Mathf.Max(0.1f, range);
            p._hitRadius = Mathf.Max(0.05f, skill.Radius);
            p._damageMultiplier = damageMultiplier;
            p.BuildVisuals(go, color);
            return p;
        }

        private void BuildVisuals(GameObject go, Color color)
        {
            _renderer = go.GetComponent<Renderer>();
            _renderer.sharedMaterial = PrimitiveFactory.Unlit(Color.Lerp(color, Color.white, 0.35f));
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _trail = go.AddComponent<TrailRenderer>();
            _trail.time = GameConstants.ProjectileTrailSeconds;
            _trail.startWidth = GameConstants.ProjectileTrailWidth;
            _trail.endWidth = 0f;
            _trail.minVertexDistance = 0.05f;
            _trail.sharedMaterial = PrimitiveFactory.Unlit(Color.white);
            _trail.startColor = color;
            _trail.endColor = new Color(color.r, color.g, color.b, 0f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
        }

        private void Update()
        {
            if (_done) return;
            if (_owner == null)
            {
                Finish();
                return;
            }
            float step = _speed * Time.deltaTime;
            Vector3 prev = transform.position;
            Vector3 next = prev + _dir * step;
            CombatWorld world = GameServices.World;
            if (world != null)
            {
                Unit hit = world.FirstEnemyAlongSegment(Flat(prev), Flat(next), _hitRadius, _owner.Team, true);
                if (hit != null)
                {
                    transform.position = new Vector3(hit.Position.x, FlightHeight, hit.Position.z);
                    SkillExecutor.HitTarget(_owner, _skill, hit, _dir, _damageMultiplier);
                    Finish();
                    return;
                }
            }
            transform.position = next;
            _remaining -= step;
            if (_remaining <= 0f || (world != null && !world.IsWalkable(Flat(next)))) Finish();
        }

        private void Finish()
        {
            _done = true;
            if (_renderer != null) _renderer.enabled = false;
            Destroy(gameObject, GameConstants.ProjectileTrailSeconds);
        }

        private void OnDestroy()
        {
            if (_renderer != null && _renderer.sharedMaterial != null) Destroy(_renderer.sharedMaterial);
            if (_trail != null && _trail.sharedMaterial != null) Destroy(_trail.sharedMaterial);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
