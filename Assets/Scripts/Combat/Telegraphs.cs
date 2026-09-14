using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Ground telegraphs built in code: filling discs for circles, wedge meshes for cones, thin lines for dashes. Each destroys itself when its duration ends.</summary>
    public static class Telegraphs
    {
        private const float GroundY = 0.05f;
        private const float DiscThickness = 0.02f;
        private const float LineWidth = 0.25f;
        private const float BaseAlpha = 0.35f;
        private const int ConeSegments = 16;

        /// <summary>Disc that grows from the center to 'radius' over 'duration' seconds (scaled time).</summary>
        public static GameObject Circle(Vector3 center, float radius, float duration, Color color)
        {
            Material material = PrimitiveFactory.Unlit(WithAlpha(color, BaseAlpha));
            GameObject go = PrimitiveFactory.Disc("Telegraph_Circle", CombatFx.Instance.Root, new Vector3(center.x, GroundY, center.z), radius, DiscThickness, material);
            TelegraphFade fade = go.AddComponent<TelegraphFade>();
            fade.Init(duration, material, PrimitiveFactory.DiscScale(radius, DiscThickness), true);
            return go;
        }

        /// <summary>Translucent wedge covering the cone (total arc angleDeg) for 'duration' seconds.</summary>
        public static GameObject Cone(Vector3 origin, Vector3 dir, float range, float angleDeg, float duration, Color color)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            var go = new GameObject("Telegraph_Cone");
            go.transform.SetParent(CombatFx.Instance.Root, false);
            go.transform.position = new Vector3(origin.x, GroundY, origin.z);
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildWedge(range, angleDeg);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Material material = PrimitiveFactory.Unlit(WithAlpha(color, BaseAlpha));
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            TelegraphFade fade = go.AddComponent<TelegraphFade>();
            fade.Init(duration, material, Vector3.one, false);
            return go;
        }

        /// <summary>Thin strip from → to for 'duration' seconds (dash preview).</summary>
        public static GameObject Line(Vector3 from, Vector3 to, float duration, Color color)
        {
            from.y = GroundY;
            to.y = GroundY;
            Vector3 delta = to - from;
            float length = Mathf.Max(0.1f, delta.magnitude);
            Material material = PrimitiveFactory.Unlit(WithAlpha(color, BaseAlpha));
            GameObject go = PrimitiveFactory.Cube("Telegraph_Line", CombatFx.Instance.Root, (from + to) * 0.5f, new Vector3(LineWidth, DiscThickness, length), material, 0);
            PrimitiveFactory.RemoveCollider(go);
            if (delta.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            TelegraphFade fade = go.AddComponent<TelegraphFade>();
            fade.Init(duration, material, go.transform.localScale, false);
            return go;
        }

        private static Mesh BuildWedge(float range, float angleDeg)
        {
            var mesh = new Mesh { name = "Wedge" };
            var vertices = new Vector3[ConeSegments + 2];
            var triangles = new int[ConeSegments * 3];
            vertices[0] = Vector3.zero;
            float half = angleDeg * 0.5f;
            for (int i = 0; i <= ConeSegments; i++)
            {
                float a = Mathf.Deg2Rad * (-half + angleDeg * i / ConeSegments);
                vertices[i + 1] = new Vector3(Mathf.Sin(a) * range, 0f, Mathf.Cos(a) * range);
            }
            for (int i = 0; i < ConeSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }
    }

    /// <summary>Drives a telegraph's grow / fade animation and destroys it (and its material) when done.</summary>
    public sealed class TelegraphFade : MonoBehaviour
    {
        private float _duration;
        private float _elapsed;
        private Material _material;
        private Vector3 _fullScale;
        private bool _grow;
        private Color _color;

        public void Init(float duration, Material material, Vector3 fullScale, bool grow)
        {
            _duration = Mathf.Max(0.01f, duration);
            _material = material;
            _fullScale = fullScale;
            _grow = grow;
            _color = material != null ? material.color : Color.white;
            if (_grow) transform.localScale = new Vector3(0.01f, _fullScale.y, 0.01f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            if (_grow) transform.localScale = new Vector3(_fullScale.x * t, _fullScale.y, _fullScale.z * t);
            if (_material != null) _material.color = new Color(_color.r, _color.g, _color.b, _color.a * (0.6f + 0.4f * t));
            if (_elapsed >= _duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Wedge") Destroy(filter.sharedMesh);
        }
    }
}
