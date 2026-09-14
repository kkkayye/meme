using System;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Builds arena props from Unity primitives and code-made materials (no asset references anywhere).</summary>
    public static class PrimitiveFactory
    {
        private const string LitShaderName = "Standard";
        private const string UnlitShaderName = "Sprites/Default";
        /// <summary>Unity's Cylinder primitive is 2 units tall and 1 unit in diameter at scale 1.</summary>
        private const float CylinderUnitHeight = 2f;

        /// <summary>Opaque lit material (Standard shader) tinted with the color.</summary>
        public static Material Lit(Color color)
        {
            Shader shader = Shader.Find(LitShaderName);
            if (shader == null) shader = Shader.Find(UnlitShaderName);
            if (shader == null) throw new InvalidOperationException("Neither '" + LitShaderName + "' nor '" + UnlitShaderName + "' shader is available.");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        /// <summary>Unlit, alpha-capable material (Sprites/Default) tinted with the color.</summary>
        public static Material Unlit(Color color)
        {
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null) throw new InvalidOperationException("Shader '" + UnlitShaderName + "' is not available.");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        /// <summary>Axis-aligned cube with its BoxCollider kept, placed on the given layer.</summary>
        public static GameObject Cube(string name, Transform parent, Vector3 center, Vector3 size, Material material, int layer)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = size;
            go.layer = layer;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Flat cylinder disc (collider removed) of the given radius and thickness, centered at 'center'.</summary>
        public static GameObject Disc(string name, Transform parent, Vector3 center, float radius, float thickness, Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = DiscScale(radius, thickness);
            RemoveCollider(go);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>Local scale that turns the unit cylinder into a disc of the given radius and thickness.</summary>
        public static Vector3 DiscScale(float radius, float thickness)
        {
            return new Vector3(radius * 2f, thickness / CylinderUnitHeight, radius * 2f);
        }

        /// <summary>Removes the primitive's collider (deferred destroy in play mode, immediate in edit mode / tests).</summary>
        public static void RemoveCollider(GameObject go)
        {
            if (go == null) return;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) SafeDestroy(collider);
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode (EditMode tests create units and materials too).</summary>
        public static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
