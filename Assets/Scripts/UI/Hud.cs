using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Runes;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Combat HUD: score / round / timer, gold, player HP bar, owned runes with set progress, item slots and the skill bar.</summary>
    public sealed class Hud : MonoBehaviour
    {
        private static readonly RuneSet[] Sets = { RuneSet.Ember, RuneSet.Iron, RuneSet.Shadow, RuneSet.Storm };
        private const int RuneSquares = 12;

        private Unit _unit;
        private RectTransform _root;
        private Text _score;
        private Text _timer;
        private Text _phase;
        private Text _gold;
        private Text _controlText;
        private Image _hpFill;
        private Image _shieldFill;
        private Text _hpText;
        private Text _name;
        private Text _setProgress;
        private readonly List<Image> _runeSquares = new List<Image>();
        private readonly List<Text> _runeLabels = new List<Text>();
        private readonly List<Image> _itemSlots = new List<Image>();
        private readonly List<Text> _itemLabels = new List<Text>();
        private SkillBar _skillBar;

        public static Hud Build(Transform parent)
        {
            RectTransform root = UiFactory.Stretch(parent, "Hud");
            Hud hud = root.gameObject.AddComponent<Hud>();
            hud._root = root;
            hud.BuildTop();
            hud.BuildPlayerBar();
            hud.BuildRunes();
            hud.BuildItems();
            hud._skillBar = SkillBar.Build(root);
            return hud;
        }

        public void Bind(Unit unit)
        {
            _unit = unit;
            _skillBar.Bind(unit);
            RefreshRunes();
            RefreshItems();
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
        }

        private void BuildTop()
        {
            _score = UiFactory.OutlinedText(_root, "Score", "", 34, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(600f, 44f));
            _timer = UiFactory.OutlinedText(_root, "Timer", "", 30, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(300f, 36f));
            _phase = UiFactory.OutlinedText(_root, "Phase", "", 22, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(_phase.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(500f, 30f));
            _gold = UiFactory.OutlinedText(_root, "Gold", "", 30, UiStyle.GoldText, TextAnchor.MiddleLeft);
            UiFactory.Place(_gold.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(300f, 40f));
            _controlText = UiFactory.OutlinedText(_root, "Control", "", 22, UiStyle.TextMuted, TextAnchor.MiddleLeft);
            UiFactory.Place(_controlText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -56f), new Vector2(400f, 30f));
        }

        private void BuildPlayerBar()
        {
            _hpFill = UiFactory.Bar(_root, "PlayerHp", UiStyle.HealthBg, UiStyle.HealthAlly, new Vector2(520f, 30f));
            RectTransform bar = (RectTransform)_hpFill.transform.parent;
            UiFactory.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(520f, 30f));
            _shieldFill = UiFactory.Panel(bar, "Shield", UiStyle.ShieldColor);
            _shieldFill.type = Image.Type.Filled;
            _shieldFill.fillMethod = Image.FillMethod.Horizontal;
            _shieldFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            _shieldFill.fillAmount = 0f;
            UiFactory.StretchRect(_shieldFill.rectTransform, 2f);
            _hpText = UiFactory.OutlinedText(bar, "HpText", "", 20, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.StretchRect(_hpText.rectTransform, 0f);
            _name = UiFactory.OutlinedText(_root, "Name", "", 22, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(_name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 162f), new Vector2(520f, 28f));
        }

        private void BuildRunes()
        {
            RectTransform column = UiFactory.Group(_root, "Runes");
            UiFactory.Place(column, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 120f), new Vector2(60f, 600f));
            Text title = UiFactory.OutlinedText(column, "Title", "Runes", 18, UiStyle.TextMuted, TextAnchor.MiddleLeft);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(200f, 24f));
            for (int i = 0; i < RuneSquares; i++)
            {
                Image square = UiFactory.Panel(column, "Rune" + i, UiStyle.SlotBg);
                UiFactory.Place(square.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -30f - i * 42f), new Vector2(40f, 40f));
                Text label = UiFactory.Text(square.transform, "Label", "", 16, UiStyle.TextMain, TextAnchor.MiddleCenter);
                UiFactory.StretchRect(label.rectTransform, 0f);
                Text count = UiFactory.OutlinedText(square.transform, "Count", "", 14, UiStyle.GoldText, TextAnchor.LowerRight);
                UiFactory.StretchRect(count.rectTransform, 1f);
                square.gameObject.SetActive(false);
                _runeSquares.Add(square);
                _runeLabels.Add(label);
            }
            _setProgress = UiFactory.OutlinedText(column, "Sets", "", 16, UiStyle.TextMuted, TextAnchor.UpperLeft);
            UiFactory.Place(_setProgress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(220f, 200f));
        }

        private void BuildItems()
        {
            RectTransform row = UiFactory.Group(_root, "Items");
            UiFactory.Place(row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(330f, 110f));
            for (int i = 0; i < GameConstants.InventorySlots; i++)
            {
                Image slot = UiFactory.Panel(row, "Item" + i, UiStyle.SlotBg);
                int col = i % 3;
                int line = i / 3;
                UiFactory.Place(slot.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(2 - col) * 108f, line * 54f), new Vector2(104f, 50f));
                Text label = UiFactory.Text(slot.transform, "Label", "", 16, UiStyle.TextMain, TextAnchor.MiddleCenter);
                UiFactory.StretchRect(label.rectTransform, 2f);
                _itemSlots.Add(slot);
                _itemLabels.Add(label);
            }
        }

        private void Update()
        {
            var match = GameServices.Match;
            if (match == null) return;
            _score.text = UiStyle.Colored("Blue " + match.Wins(Team.Blue), UiStyle.TeamBlue) + "  -  " + UiStyle.Colored(match.Wins(Team.Red) + " Red", UiStyle.TeamRed);
            _timer.text = match.Phase == MatchPhase.Combat ? UiText.FormatClock(match.PhaseTimeRemaining) : UiText.FormatWhole(match.PhaseTimeRemaining);
            _phase.text = "Round " + match.Round + "  ·  " + PhaseLabel(match.Phase);
            UpdateControlPoint();
            if (_unit == null) return;
            _gold.text = "Gold  " + _unit.Gold;
            UpdateHealth();
        }

        private void UpdateControlPoint()
        {
            var point = GameServices.ControlPoint;
            if (point == null || !point.IsBuilt)
            {
                _controlText.text = "";
                return;
            }
            string owner = point.Owner.HasValue ? UiText.TeamName(point.Owner.Value) : "-";
            _controlText.text = point.IsLocked ? "Point locked " + UiText.FormatWhole(point.LockoutRemaining) + "s" : "Point " + owner + " " + Mathf.RoundToInt(point.Progress) + "%";
        }

        private void UpdateHealth()
        {
            float max = Mathf.Max(1f, _unit.MaxHealth);
            _hpFill.fillAmount = Mathf.Clamp01(_unit.Health / max);
            _shieldFill.fillAmount = Mathf.Clamp01(_unit.Shields.Total / max);
            _hpText.text = Mathf.CeilToInt(_unit.Health) + " / " + Mathf.CeilToInt(max) + (_unit.Shields.Total > 0f ? "  (+" + Mathf.CeilToInt(_unit.Shields.Total) + ")" : "");
            _name.text = _unit.UnitName + (_unit.IsAlive ? "" : "  (dead)");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RuneAcquired>(OnRune);
            EventBus.Subscribe<ItemAcquired>(OnItem);
            EventBus.Subscribe<ItemSold>(OnItemSold);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RuneAcquired>(OnRune);
            EventBus.Unsubscribe<ItemAcquired>(OnItem);
            EventBus.Unsubscribe<ItemSold>(OnItemSold);
        }

        private void OnRune(RuneAcquired e) { if (ReferenceEquals(e.Unit, _unit)) RefreshRunes(); }
        private void OnItem(ItemAcquired e) { if (ReferenceEquals(e.Unit, _unit)) RefreshItems(); }
        private void OnItemSold(ItemSold e) { if (ReferenceEquals(e.Unit, _unit)) RefreshItems(); }

        /// <summary>Rebuilds the rune column from the bound unit's inventory.</summary>
        public void RefreshRunes()
        {
            IReadOnlyList<RuneStack> owned = _unit != null && _unit.Runes != null ? _unit.Runes.Owned : null;
            for (int i = 0; i < _runeSquares.Count; i++)
            {
                bool has = owned != null && i < owned.Count;
                _runeSquares[i].gameObject.SetActive(has);
                if (!has) continue;
                RuneStack stack = owned[i];
                _runeSquares[i].color = UiStyle.Darken(UiStyle.Rarity(stack.Definition.Rarity), 0.55f);
                _runeLabels[i].text = stack.Definition.GetShortLabel();
                Text count = _runeSquares[i].transform.Find("Count").GetComponent<Text>();
                count.text = stack.Stacks > 1 ? "x" + stack.Stacks : "";
            }
            _setProgress.text = owned != null ? BuildSetText() : "";
        }

        private string BuildSetText()
        {
            string text = "";
            for (int i = 0; i < Sets.Length; i++)
            {
                int count = _unit.Runes.SetCount(Sets[i]);
                if (count == 0) continue;
                string line = Sets[i] + " " + count + "/" + GameConstants.RuneSetBonusCount + (_unit.Runes.IsSetActive(Sets[i]) ? " ✓" : "");
                text += UiStyle.Colored(line, UiStyle.SetColor(Sets[i])) + "\n";
            }
            return text;
        }

        /// <summary>Rebuilds the item slots from the bound unit's inventory.</summary>
        public void RefreshItems()
        {
            for (int i = 0; i < _itemSlots.Count; i++)
            {
                ItemDefinition item = _unit != null && _unit.Items != null && i < _unit.Items.Slots.Count ? _unit.Items.Slots[i] : null;
                _itemSlots[i].color = item != null ? UiStyle.Darken(UiStyle.TierColor(item.Tier), 0.5f) : UiStyle.SlotBg;
                _itemLabels[i].text = item != null ? item.Name : "";
            }
        }

        private static string PhaseLabel(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.RuneDraft: return "符文抽选 Rune Draft";
                case MatchPhase.Shop: return "商店 Shop";
                case MatchPhase.Countdown: return "准备 Get Ready";
                case MatchPhase.Combat: return "战斗 Combat";
                case MatchPhase.RoundEnd: return "回合结束 Round Over";
                case MatchPhase.MatchEnd: return "比赛结束 Match Over";
                default: return phase.ToString();
            }
        }
    }
}
