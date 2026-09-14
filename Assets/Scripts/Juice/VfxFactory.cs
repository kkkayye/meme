using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Juice
{
    /// <summary>Code-built particle bursts and expanding rings for impacts, kills and dashes. Purely cosmetic.</summary>
    public static class VfxFactory
    {
        private static readonly string[] ParticleShaders = { "Particles/Standard Unlit", "Legacy Shaders/Particles/Alpha Blended", "Sprites/Default" };
        private const float BurstLifetime = 0.3f;

        /// <summary>Radial particle burst at a position.</summary>
        public static void Burst(Vector3 position, Color color, int count, float size, float speed)
        {
            var go = new GameObject("Vfx_Burst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            ParticleSystem.MainModule main = ps.main;
            main.duration = BurstLifetime;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = BurstLifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(8, count);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            Object.Destroy(go, BurstLifetime + 0.2f);
        }

        /// <summary>Flat ring that expands to 'radius' and fades over 'seconds'.</summary>
        public static void Ring(Vector3 position, Color color, float radius, float seconds)
        {
            Material material = PrimitiveFactory.Unlit(new Color(color.r, color.g, color.b, 0.5f));
            GameObject go = PrimitiveFactory.Disc("Vfx_Ring", null, new Vector3(position.x, 0.08f, position.z), 0.05f, 0.02f, material);
            ExpandingRing ring = go.AddComponent<ExpandingRing>();
            ring.Init(radius, seconds, material);
        }

        private static Material ParticleMaterial(Color color)
        {
            for (int i = 0; i < ParticleShaders.Length; i++)
            {
                Shader shader = Shader.Find(ParticleShaders[i]);
                if (shader == null) continue;
                var material = new Material(shader);
                if (material.HasProperty("_Color")) material.color = color;
                return material;
            }
            return PrimitiveFactory.Unlit(color);
        }
    }

    /// <summary>Scales a disc from 0 to a radius while fading its material, then destroys itself.</summary>
    public sealed class ExpandingRing : MonoBehaviour
    {
        private float _radius;
        private float _seconds;
        private float _elapsed;
        private Material _material;
        private Color _color;

        public void Init(float radius, float seconds, Material material)
        {
            _radius = radius;
            _seconds = Mathf.Max(0.01f, seconds);
            _material = material;
            _color = material != null ? material.color : Color.white;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / _seconds);
            transform.localScale = PrimitiveFactory.DiscScale(Mathf.Max(0.05f, _radius * Mathf.SmoothStep(0f, 1f, t)), 0.02f);
            if (_material != null) _material.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
