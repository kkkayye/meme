using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Pooled floating damage numbers drawn on the overlay canvas at the projected world position, rising and fading over 0.8 s (unscaled).</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private sealed class Entry
        {
            public Text Text;
            public Vector3 World;
            public float Age;
            public bool Active;
        }

        private const int PoolSize = 40;
        private const float RiseWorldUnits = 1.6f;
        private const int BaseFontSize = 28;
        private const float MinShown = 1f;

        private readonly List<Entry> _pool = new List<Entry>();
        private RectTransform _root;
        private Canvas _canvas;

        public static DamageNumbers Build(Transform parent)
        {
            RectTransform root = UiFactory.Stretch(parent, "DamageNumbers");
            DamageNumbers numbers = root.gameObject.AddComponent<DamageNumbers>();
            numbers._root = root;
            numbers._canvas = parent.GetComponent<Canvas>();
            for (int i = 0; i < PoolSize; i++)
            {
                Text text = UiFactory.OutlinedText(root, "Number" + i, "", BaseFontSize, Color.white, TextAnchor.MiddleCenter);
                text.rectTransform.sizeDelta = new Vector2(200f, 50f);
                text.gameObject.SetActive(false);
                numbers._pool.Add(new Entry { Text = text });
            }
            return numbers;
        }

        public void Spawn(Vector3 worldPos, float amount, DamageType type, bool crit)
        {
            if (amount < MinShown) return;
            Entry entry = Acquire();
            entry.World = worldPos;
            entry.Age = 0f;
            entry.Active = true;
            entry.Text.text = Mathf.RoundToInt(amount).ToString() + (crit ? "!" : "");
            entry.Text.color = UiStyle.DamageColor(type);
            entry.Text.fontSize = crit ? Mathf.RoundToInt(BaseFontSize * GameConstants.DamageNumberCritScale) : BaseFontSize;
            entry.Text.gameObject.SetActive(true);
            Position(entry);
        }

        private Entry Acquire()
        {
            Entry oldest = _pool[0];
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Active) return _pool[i];
                if (_pool[i].Age > oldest.Age) oldest = _pool[i];
            }
            return oldest;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _pool.Count; i++)
            {
                Entry e = _pool[i];
                if (!e.Active) continue;
                e.Age += dt;
                if (e.Age >= GameConstants.DamageNumberSeconds)
                {
                    e.Active = false;
                    e.Text.gameObject.SetActive(false);
                    continue;
                }
                Position(e);
            }
        }

        private void Position(Entry e)
        {
            Camera cam = Camera.main;
            if (cam == null || _canvas == null) return;
            float t = e.Age / GameConstants.DamageNumberSeconds;
            Vector3 world = e.World + Vector3.up * (2f + RiseWorldUnits * t);
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f)
            {
                e.Text.enabled = false;
                return;
            }
            e.Text.enabled = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            e.Text.rectTransform.anchoredPosition = local;
            Color c = e.Text.color;
            c.a = 1f - Mathf.SmoothStep(0f, 1f, t);
            e.Text.color = c;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UnitDamaged>(OnDamaged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UnitDamaged>(OnDamaged);
        }

        private void OnDamaged(UnitDamaged e)
        {
            if (e.Info.Target == null) return;
            Spawn(e.Info.Target.Position, e.Result.Total, e.Info.Type, e.Info.IsCrit);
        }
    }
}
