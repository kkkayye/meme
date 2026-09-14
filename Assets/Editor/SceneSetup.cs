using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuneArena.Editor
{
    /// <summary>Creates Assets/Scenes/Arena.unity (camera, light, GameRoot marker), adds it to Build Settings and names layer 8 "Obstacle". Usable from the menu or -executeMethod.</summary>
    public static class SceneSetup
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/Arena.unity";
        private const int ObstacleLayer = 8;

        [MenuItem("RuneArena/Create Arena Scene")]
        public static void CreateArenaScene()
        {
            NameObstacleLayer();
            if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets", "Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 16f, -8.5f);
            camera.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            var light = new GameObject("Directional Light");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            new GameObject("GameRoot (created at runtime by Bootstrap)");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("RuneArena: created " + ScenePath + " and added it to Build Settings.");
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath) return;
            }
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Best effort: names layer 8 "Obstacle" in the TagManager so the arena's pillars read nicely in the Inspector.</summary>
        public static void NameObstacleLayer()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray || layers.arraySize <= ObstacleLayer) return;
            SetLayerName(layers, ObstacleLayer, "Obstacle");
            SetLayerName(layers, ObstacleLayer + 1, "Unit");
            SetLayerName(layers, ObstacleLayer + 2, "Dashing");
            tagManager.ApplyModifiedProperties();
        }

        private static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            if (index >= layers.arraySize) return;
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue)) layer.stringValue = name;
        }
    }
}
