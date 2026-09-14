using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Makes the game run from any scene: after the first scene loads, creates the persistent GameRoot (camera, light, UI, systems) if none exists.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            EnsureRoot();
        }

        /// <summary>Returns the existing GameRoot or creates and sets one up.</summary>
        public static GameRoot EnsureRoot()
        {
            if (GameRoot.Instance != null)
            {
                GameRoot.Instance.Setup();
                return GameRoot.Instance;
            }
            var go = new GameObject("GameRoot");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            GameRoot root = go.AddComponent<GameRoot>();
            root.Setup();
            return root;
        }
    }
}
