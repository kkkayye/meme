using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Runes
{
    /// <summary>Lazily created per-frame ticker so plain ICombatHook classes (not MonoBehaviours) can run timed logic such as buff expiry.</summary>
    public sealed class RuneTicker : MonoBehaviour
    {
        private static RuneTicker _instance;

        private readonly List<Action<float>> _ticks = new List<Action<float>>();
        private Action<float>[] _snapshot = Array.Empty<Action<float>>();
        private bool _dirty;

        public static bool Exists => _instance != null;
        public int Count => _ticks.Count;

        /// <summary>Registers a callback invoked every frame with scaled delta time. Duplicate registrations are ignored.</summary>
        public static void Add(Action<float> tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            RuneTicker ticker = Instance();
            if (ticker._ticks.Contains(tick)) return;
            ticker._ticks.Add(tick);
            ticker._dirty = true;
        }

        /// <summary>Unregisters a callback. Safe to call when no ticker exists.</summary>
        public static void Remove(Action<float> tick)
        {
            if (tick == null || _instance == null) return;
            if (_instance._ticks.Remove(tick)) _instance._dirty = true;
        }

        private static RuneTicker Instance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("RuneTicker");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            _instance = go.AddComponent<RuneTicker>();
            return _instance;
        }

        private void Update()
        {
            if (_ticks.Count == 0 && _snapshot.Length == 0) return;
            if (_dirty)
            {
                _snapshot = _ticks.ToArray();
                _dirty = false;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < _snapshot.Length; i++)
            {
                _snapshot[i](dt);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this)) _instance = null;
        }
    }
}
