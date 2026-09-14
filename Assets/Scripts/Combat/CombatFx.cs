using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Scene-side helper for combat effects: parent object for projectiles / telegraphs and a scaled-time scheduler for delayed skill resolution (Meteor).</summary>
    public sealed class CombatFx : MonoBehaviour
    {
        private struct Pending
        {
            public float Time;
            public Action Action;
        }

        private static CombatFx _instance;

        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<Pending> _due = new List<Pending>();
        private float _clock;

        public static CombatFx Instance
        {
            get
            {
                if (_instance == null) Create();
                return _instance;
            }
        }

        public static bool Exists => _instance != null;
        public Transform Root => transform;
        public int PendingCount => _pending.Count;

        /// <summary>Runs the action after 'delay' scaled seconds (hit-stop and pause delay it too).</summary>
        public void Schedule(float delay, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _pending.Add(new Pending { Time = _clock + Mathf.Max(0f, delay), Action = action });
        }

        /// <summary>Drops every pending action and destroys every child (round / match teardown).</summary>
        public void ClearAll()
        {
            _pending.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private static void Create()
        {
            var go = new GameObject("CombatFx");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatFx>();
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            if (_pending.Count == 0) return;
            _due.Clear();
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Time <= _clock) _due.Add(_pending[i]);
            }
            if (_due.Count == 0) return;
            _pending.RemoveAll(p => p.Time <= _clock);
            for (int i = 0; i < _due.Count; i++)
            {
                _due[i].Action();
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this)) _instance = null;
        }
    }
}
