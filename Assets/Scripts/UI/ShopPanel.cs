using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Between-round shop: five item cards with Buy, Reroll with live cost, inventory row with Sell, gold, Ready and countdown.</summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        private static readonly Vector2 CardSize = new Vector2(300f, 300f);
        private const float CardGap = 24f;

        private Unit _unit;
        private RectTransform _root;
        private Text _timer;
        private Text _gold;
        private Button _reroll;
        private Button _ready;
        private readonly List<Image> _borders = new List<Image>();
        private readonly List<Text> _names = new List<Text>();
        private readonly List<Text> _tiers = new List<Text>();
        private readonly List<Text> _descriptions = new List<Text>();
        private readonly List<Button> _buys = new List<Button>();
        private readonly List<Button> _sells = new List<Button>();

        public static ShopPanel Build(Transform parent)
        {
            RectTransform root = UiFactory.Stretch(parent, "ShopPanel");
            ShopPanel panel = root.gameObject.AddComponent<ShopPanel>();
            panel._root = root;
            UiFactory.Overlay(root, "Dim", UiStyle.Dim);
            Text title = UiFactory.OutlinedText(root, "Title", "装备商店  Shop", 44, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 60f));
            panel._timer = UiFactory.OutlinedText(root, "Timer", "", 30, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(panel._timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(600f, 40f));
            panel._gold = UiFactory.OutlinedText(root, "Gold", "", 34, UiStyle.GoldText, TextAnchor.MiddleCenter);
            UiFactory.Place(panel._gold.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(600f, 44f));
            for (int i = 0; i < GameConstants.ShopOfferCount; i++) panel.BuildCard(root, i);
            panel.BuildInventory(root);
            panel._reroll = UiFactory.Button(root, "Reroll", "刷新 Reroll (100)", 24, panel.Reroll);
            UiFactory.Place((RectTransform)panel._reroll.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 60f), new Vector2(300f, 56f));
            panel._ready = UiFactory.Button(root, "Ready", "准备 Ready (Enter)", 24, panel.Ready);
            UiFactory.SetButtonColor(panel._ready, UiStyle.ButtonSelected);
            UiFactory.Place((RectTransform)panel._ready.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 60f), new Vector2(300f, 56f));
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Show(Unit unit)
        {
            _unit = unit;
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
            float x = (index - (GameConstants.ShopOfferCount - 1) * 0.5f) * (CardSize.x + CardGap);
            RectTransform inner = UiFactory.Card(root, "Item" + index, CardSize, UiStyle.Tier1, UiStyle.CardBg, 5f);
            RectTransform border = (RectTransform)inner.parent;
            UiFactory.Place(border, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 40f), CardSize);
            _borders.Add(border.GetComponent<Image>());
            Text name = UiFactory.Text(inner, "Name", "", 28, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(CardSize.x - 24f, 40f));
            _names.Add(name);
            Text tier = UiFactory.Text(inner, "Tier", "", 20, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(tier.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(CardSize.x - 24f, 28f));
            _tiers.Add(tier);
            Text description = UiFactory.Text(inner, "Description", "", 20, UiStyle.TextMain, TextAnchor.UpperCenter);
            UiFactory.Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(CardSize.x - 30f, 130f));
            _descriptions.Add(description);
            int captured = index;
            Button buy = UiFactory.Button(inner, "Buy", "", 22, () => Buy(captured));
            UiFactory.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(CardSize.x - 40f, 48f));
            _buys.Add(buy);
        }

        private void BuildInventory(RectTransform root)
        {
            Text label = UiFactory.OutlinedText(root, "InventoryLabel", "背包 Inventory (click to sell 70%)", 20, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(800f, 28f));
            for (int i = 0; i < GameConstants.InventorySlots; i++)
            {
                int captured = i;
                Button sell = UiFactory.Button(root, "Slot" + i, "", 18, () => Sell(captured));
                float x = (i - (GameConstants.InventorySlots - 1) * 0.5f) * 132f;
                UiFactory.Place((RectTransform)sell.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 132f), new Vector2(124f, 56f));
                _sells.Add(sell);
            }
        }

        private void Buy(int index)
        {
            if (_unit == null || GameServices.Shop == null) return;
            GameServices.Shop.Buy(_unit, index);
            Refresh();
        }

        private void Sell(int slot)
        {
            if (_unit == null || GameServices.Shop == null || _unit.Items == null) return;
            ItemDefinition item = slot < _unit.Items.Slots.Count ? _unit.Items.Slots[slot] : null;
            if (item == null) return;
            GameServices.Shop.Sell(_unit, item);
            Refresh();
        }

        private void Reroll()
        {
            if (_unit == null || GameServices.Shop == null) return;
            GameServices.Shop.Reroll(_unit);
            Refresh();
        }

        private void Ready()
        {
            GameServices.Match?.PlayerReadyForShop();
            Refresh();
        }

        private void Update()
        {
            if (_unit == null) return;
            var match = GameServices.Match;
            if (match != null) _timer.text = UiText.FormatWhole(match.PhaseTimeRemaining) + " s";
            _gold.text = "Gold  " + _unit.Gold;
            RefreshButtons();
        }

        private void Refresh()
        {
            if (_unit == null || GameServices.Shop == null) return;
            IReadOnlyList<ItemDefinition> offer = GameServices.Shop.GetOffer(_unit);
            for (int i = 0; i < _borders.Count; i++)
            {
                ItemDefinition item = i < offer.Count ? offer[i] : null;
                _borders[i].gameObject.SetActive(item != null);
                if (item == null) continue;
                _borders[i].color = UiStyle.TierColor(item.Tier);
                _names[i].text = item.Name;
                _tiers[i].text = UiStyle.Colored(UiText.TierName(item.Tier), UiStyle.TierColor(item.Tier)) + "  ·  " + item.Cost + " g";
                _descriptions[i].text = item.Description;
                UiFactory.SetLabel(_buys[i], "购买 Buy  " + item.Cost);
            }
            for (int i = 0; i < _sells.Count; i++)
            {
                ItemDefinition item = _unit.Items != null && i < _unit.Items.Slots.Count ? _unit.Items.Slots[i] : null;
                UiFactory.SetLabel(_sells[i], item != null ? item.Name + "\n<size=14>+" + item.SellValue + " g</size>" : "-");
                _sells[i].interactable = item != null;
                UiFactory.SetButtonColor(_sells[i], item != null ? UiStyle.Darken(UiStyle.TierColor(item.Tier), 0.5f) : UiStyle.SlotBg);
            }
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (_unit == null || GameServices.Shop == null) return;
            IReadOnlyList<ItemDefinition> offer = GameServices.Shop.GetOffer(_unit);
            for (int i = 0; i < _buys.Count; i++)
            {
                bool has = i < offer.Count && offer[i] != null;
                if (has) _buys[i].interactable = GameServices.Shop.CanBuy(_unit, i);
            }
            int cost = GameServices.Shop.RerollCost(_unit);
            UiFactory.SetLabel(_reroll, "刷新 Reroll (" + cost + ")");
            _reroll.interactable = _unit.Gold >= cost;
            bool ready = GameServices.Shop.IsReady(_unit);
            UiFactory.SetLabel(_ready, ready ? "等待其他玩家  Waiting..." : "准备 Ready (Enter)");
            _ready.interactable = !ready;
        }
    }
}
