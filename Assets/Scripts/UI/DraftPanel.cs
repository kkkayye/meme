using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Runes;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Rune draft: three rarity-coloured cards (name, set, description, owned stacks); click or 1/2/3 to pick; countdown.</summary>
    public sealed class DraftPanel : MonoBehaviour
    {
        private const float CardGap = 40f;

        private Unit _unit;
        private RectTransform _root;
        private Text _title;
        private Text _timer;
        private readonly List<Image> _borders = new List<Image>();
        private readonly List<Image> _inners = new List<Image>();
        private readonly List<Text> _names = new List<Text>();
        private readonly List<Text> _tags = new List<Text>();
        private readonly List<Text> _descriptions = new List<Text>();
        private readonly List<Text> _owned = new List<Text>();
        private readonly List<Button> _buttons = new List<Button>();
        private int _lastOfferHash = -1;
        private RuneDefinition _lastPicked;

        public static DraftPanel Build(Transform parent)
        {
            RectTransform root = UiFactory.Stretch(parent, "DraftPanel");
            DraftPanel panel = root.gameObject.AddComponent<DraftPanel>();
            panel._root = root;
            UiFactory.Overlay(root, "Dim", UiStyle.Dim);
            panel._title = UiFactory.OutlinedText(root, "Title", "符文抽选  Rune Draft", 44, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(panel._title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 60f));
            panel._timer = UiFactory.OutlinedText(root, "Timer", "", 30, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(panel._timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(600f, 40f));
            for (int i = 0; i < GameConstants.DraftCardCount; i++) panel.BuildCard(root, i);
            Text hint = UiFactory.OutlinedText(root, "Hint", "点击卡牌或按 1 / 2 / 3 选择    Click a card or press 1 / 2 / 3", 22, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1000f, 30f));
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Show(Unit unit)
        {
            _unit = unit;
            _lastOfferHash = -1;
            _root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
            _unit = null;
        }

        private void BuildCard(RectTransform root, int index)
        {
            var size = new Vector2(GameConstants.DraftCardWidth, GameConstants.DraftCardHeight);
            float x = (index - (GameConstants.DraftCardCount - 1) * 0.5f) * (size.x + CardGap);
            RectTransform inner = UiFactory.Card(root, "Card" + index, size, UiStyle.RarityCommon, UiStyle.CardBg, 6f);
            RectTransform border = (RectTransform)inner.parent;
            UiFactory.Place(border, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -10f), size);
            border.gameObject.AddComponent<HoverScale>();
            _borders.Add(border.GetComponent<Image>());
            _inners.Add(inner.GetComponent<Image>());
            Text name = UiFactory.Text(inner, "Name", "", 30, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(size.x - 30f, 44f));
            _names.Add(name);
            Text tag = UiFactory.Text(inner, "Tag", "", 20, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(size.x - 30f, 30f));
            _tags.Add(tag);
            Text description = UiFactory.Text(inner, "Description", "", 22, UiStyle.TextMain, TextAnchor.UpperCenter);
            UiFactory.Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(size.x - 40f, 220f));
            _descriptions.Add(description);
            Text owned = UiFactory.Text(inner, "Owned", "", 20, UiStyle.GoldText, TextAnchor.MiddleCenter);
            UiFactory.Place(owned.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(size.x - 30f, 30f));
            _owned.Add(owned);
            int captured = index;
            Button button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = border.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Pick(captured));
            _buttons.Add(button);
            Text key = UiFactory.OutlinedText(inner, "Key", (index + 1).ToString(), 26, UiStyle.TextMuted, TextAnchor.UpperLeft);
            UiFactory.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(40f, 34f));
        }

        private void Pick(int index)
        {
            if (_unit == null || GameServices.Draft == null) return;
            GameServices.Draft.Pick(_unit, index);
            Refresh();
        }

        private void Update()
        {
            if (_unit == null) return;
            var match = GameServices.Match;
            if (match != null) _timer.text = UiText.FormatWhole(match.PhaseTimeRemaining) + " s";
            IReadOnlyList<RuneDefinition> offer = GameServices.Draft != null ? GameServices.Draft.GetOffer(_unit) : null;
            int hash = OfferHash(offer);
            RuneDefinition picked = GameServices.Draft != null ? GameServices.Draft.GetPicked(_unit) : null;
            if (hash != _lastOfferHash || !ReferenceEquals(picked, _lastPicked)) Refresh();
        }

        private void Refresh()
        {
            if (_unit == null || GameServices.Draft == null) return;
            IReadOnlyList<RuneDefinition> offer = GameServices.Draft.GetOffer(_unit);
            _lastOfferHash = OfferHash(offer);
            RuneDefinition picked = GameServices.Draft.GetPicked(_unit);
            _lastPicked = picked;
            bool pity = GameServices.Draft.WasPityForced(_unit);
            _title.text = "符文抽选  Rune Draft" + (pity ? "   <color=#FFC733>保底 Pity</color>" : "");
            for (int i = 0; i < _borders.Count; i++)
            {
                bool has = offer != null && i < offer.Count && offer[i] != null;
                _borders[i].gameObject.SetActive(has);
                if (!has) continue;
                FillCard(i, offer[i], picked);
            }
        }

        private void FillCard(int i, RuneDefinition rune, RuneDefinition picked)
        {
            Color rarity = UiStyle.Rarity(rune.Rarity);
            bool isPicked = picked != null && ReferenceEquals(picked, rune);
            _borders[i].color = rarity;
            _inners[i].color = isPicked ? UiStyle.CardBgPicked : UiStyle.CardBg;
            _names[i].text = UiStyle.Colored(rune.Name, rarity);
            string set = rune.Set == RuneSet.None ? "" : UiStyle.Colored(UiText.SetName(rune.Set), UiStyle.SetColor(rune.Set)) + "  ·  ";
            _tags[i].text = set + UiText.RarityName(rune.Rarity) + (rune.Stackable ? "  ·  可叠加" : "");
            _descriptions[i].text = rune.Description;
            int owned = _unit.Runes != null ? _unit.Runes.Count(rune.Id) : 0;
            _owned[i].text = isPicked ? "已选择  Picked" : (owned > 0 ? "已拥有 " + owned + "/" + rune.EffectiveMaxStacks : "");
            _buttons[i].interactable = picked == null;
        }

        private static int OfferHash(IReadOnlyList<RuneDefinition> offer)
        {
            if (offer == null) return 0;
            int hash = offer.Count;
            for (int i = 0; i < offer.Count; i++) hash = hash * 31 + (offer[i] != null ? offer[i].Id.GetHashCode() : 7);
            return hash;
        }
    }
}
