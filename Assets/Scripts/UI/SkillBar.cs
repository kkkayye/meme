using System.Collections;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Bottom-center skill bar: 5 slots (LMB, Q, E, F, R) with key labels, radial cooldown overlays, cooldown seconds, charges and a ready ping.</summary>
    public sealed class SkillBar : MonoBehaviour
    {
        private static readonly SkillKey[] Keys = { SkillKey.Basic, SkillKey.Q, SkillKey.W, SkillKey.E, SkillKey.R };
        private static readonly string[] KeyLabels = { "LMB", "Q", "E", "F", "R" };
        private const float SlotSize = 84f;
        private const float SlotGap = 10f;

        private Unit _unit;
        private readonly RectTransform[] _slots = new RectTransform[5];
        private readonly Image[] _icons = new Image[5];
        private readonly Image[] _overlays = new Image[5];
        private readonly Text[] _cooldowns = new Text[5];
        private readonly Text[] _charges = new Text[5];
        private readonly Text[] _names = new Text[5];
        private readonly Image[] _flashes = new Image[5];

        public static SkillBar Build(Transform parent)
        {
            RectTransform root = UiFactory.Group(parent, "SkillBar");
            UiFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(5 * SlotSize + 4 * SlotGap, SlotSize + 24f));
            SkillBar bar = root.gameObject.AddComponent<SkillBar>();
            for (int i = 0; i < Keys.Length; i++) bar.BuildSlot(root, i);
            return bar;
        }

        public void Bind(Unit unit)
        {
            _unit = unit;
            for (int i = 0; i < Keys.Length; i++)
            {
                SkillDefinition skill = unit != null && unit.Hero != null ? unit.Hero.GetSkill(Keys[i]) : null;
                _names[i].text = skill != null ? skill.Name : "";
                _icons[i].color = skill != null ? UiStyle.KeyColor(Keys[i]) : UiStyle.KeyEmpty;
            }
        }

        private void BuildSlot(RectTransform root, int index)
        {
            float x = index * (SlotSize + SlotGap);
            Image icon = UiFactory.Panel(root, "Slot" + index, UiStyle.KeyEmpty);
            UiFactory.Place(icon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 0f), new Vector2(SlotSize, SlotSize));
            _slots[index] = icon.rectTransform;
            _icons[index] = icon;
            Text key = UiFactory.OutlinedText(icon.transform, "Key", KeyLabels[index], 22, UiStyle.TextMain, TextAnchor.UpperLeft);
            UiFactory.StretchRect(key.rectTransform, 6f);
            _overlays[index] = UiFactory.Radial(icon.transform, "Cooldown", UiStyle.CooldownOverlay);
            _cooldowns[index] = UiFactory.OutlinedText(icon.transform, "Seconds", "", 26, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.StretchRect(_cooldowns[index].rectTransform, 0f);
            _charges[index] = UiFactory.OutlinedText(icon.transform, "Charges", "", 18, UiStyle.GoldText, TextAnchor.LowerRight);
            UiFactory.StretchRect(_charges[index].rectTransform, 4f);
            Image flash = UiFactory.Panel(icon.transform, "Flash", new Color(1f, 1f, 1f, 0f));
            UiFactory.StretchRect(flash.rectTransform, 0f);
            _flashes[index] = flash;
            _names[index] = UiFactory.OutlinedText(root, "Name" + index, "", 14, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(_names[index].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, SlotSize + 2f), new Vector2(SlotSize, 20f));
        }

        private void Update()
        {
            if (_unit == null || _unit.Caster == null) return;
            for (int i = 0; i < Keys.Length; i++)
            {
                SkillKey key = Keys[i];
                if (_unit.Caster.GetSkill(key) == null) continue;
                float fraction = _unit.Caster.GetCooldownFraction(key);
                float remaining = _unit.Caster.GetCooldownRemaining(key);
                _overlays[i].fillAmount = fraction;
                _cooldowns[i].text = remaining > 0.05f && key != SkillKey.Basic ? UiText.FormatCooldown(remaining) : "";
                int max = _unit.Caster.GetMaxCharges(key);
                _charges[i].text = max > 1 ? _unit.Caster.GetCharges(key) + "/" + max : "";
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CooldownReady>(OnReady);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CooldownReady>(OnReady);
        }

        private void OnReady(CooldownReady e)
        {
            if (!ReferenceEquals(e.Unit, _unit) || !isActiveAndEnabled) return;
            int index = (int)e.Key;
            if (index < 0 || index >= _slots.Length) return;
            StartCoroutine(Ping(index));
        }

        private IEnumerator Ping(int index)
        {
            float t = 0f;
            while (t < GameConstants.CooldownPingSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / GameConstants.CooldownPingSeconds);
                _slots[index].localScale = Vector3.one * Mathf.Lerp(1f, GameConstants.CooldownPingScale, k);
                _flashes[index].color = new Color(1f, 1f, 1f, 0.7f * k);
                yield return null;
            }
            _slots[index].localScale = Vector3.one;
            _flashes[index].color = new Color(1f, 1f, 1f, 0f);
        }
    }
}
