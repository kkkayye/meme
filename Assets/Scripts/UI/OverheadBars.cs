using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Creates a world-space name + HP/shield bar above every spawned unit (ally green / enemy red from the spectated unit's point of view).</summary>
    public sealed class OverheadBars : MonoBehaviour
    {
        private readonly List<OverheadBar> _bars = new List<OverheadBar>();

        private void OnEnable()
        {
            EventBus.Subscribe<UnitSpawned>(OnSpawned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UnitSpawned>(OnSpawned);
        }

        private void OnSpawned(UnitSpawned e)
        {
            if (e.Unit == null) return;
            _bars.RemoveAll(b => b == null);
            _bars.Add(OverheadBar.Attach(e.Unit));
        }
    }

    /// <summary>One unit's overhead bar: a small world-space canvas that billboards toward the camera.</summary>
    public sealed class OverheadBar : MonoBehaviour
    {
        private const float Height = 2.6f;
        private const float CanvasScale = 0.012f;
        private static readonly Vector2 Size = new Vector2(220f, 60f);

        private Unit _unit;
        private Image _hp;
        private Image _shield;
        private Text _name;
        private Canvas _canvas;

        public static OverheadBar Attach(Unit unit)
        {
            var go = new GameObject("OverheadBar", typeof(RectTransform));
            go.transform.SetParent(unit.transform, false);
            go.transform.localPosition = new Vector3(0f, Height, 0f);
            go.transform.localScale = Vector3.one * CanvasScale;
            var bar = go.AddComponent<OverheadBar>();
            bar._unit = unit;
            bar.Build();
            return bar;
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)transform).sizeDelta = Size;
            _name = UiFactory.OutlinedText(transform, "Name", _unit.UnitName, 26, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(_name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(220f, 30f));
            _hp = UiFactory.Bar(transform, "Hp", UiStyle.HealthBg, UiStyle.HealthAlly, new Vector2(180f, 16f));
            RectTransform hpRect = (RectTransform)_hp.transform.parent;
            UiFactory.Place(hpRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(180f, 16f));
            _shield = UiFactory.Panel(hpRect, "Shield", UiStyle.ShieldColor);
            _shield.type = Image.Type.Filled;
            _shield.fillMethod = Image.FillMethod.Horizontal;
            _shield.fillOrigin = (int)Image.OriginHorizontal.Right;
            _shield.fillAmount = 0f;
            UiFactory.StretchRect(_shield.rectTransform, 2f);
        }

        private void LateUpdate()
        {
            if (_unit == null) return;
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            Unit spectated = GameServices.Match != null ? GameServices.Match.SpectatedUnit : null;
            bool enemyView = spectated != null && spectated.Team != _unit.Team;
            bool hidden = !_unit.IsAlive || (_unit.IsInvisible && enemyView);
            if (_canvas.enabled == hidden) _canvas.enabled = !hidden;
            if (hidden) return;
            float max = Mathf.Max(1f, _unit.MaxHealth);
            _hp.fillAmount = Mathf.Clamp01(_unit.Health / max);
            _hp.color = enemyView ? UiStyle.HealthEnemy : UiStyle.HealthAlly;
            _shield.fillAmount = Mathf.Clamp01(_unit.Shields.Total / max);
            _name.color = _unit.Team == Team.Blue ? UiStyle.TeamBlue : UiStyle.TeamRed;
        }
    }
}
