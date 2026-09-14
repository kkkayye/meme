using System.Collections;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Non-blocking chest reveal: a card near the top of the screen flips open (scale x 0 → 1) in the rarity colour and fades after 1.5 s (unscaled).</summary>
    public sealed class ChestRevealPanel : MonoBehaviour
    {
        private const float FlipSeconds = 0.25f;

        private RectTransform _card;
        private Image _border;
        private Text _kind;
        private Text _name;
        private Coroutine _routine;

        public static ChestRevealPanel Build(Transform parent)
        {
            RectTransform root = UiFactory.Group(parent, "ChestReveal");
            UiFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(360f, 120f));
            ChestRevealPanel panel = root.gameObject.AddComponent<ChestRevealPanel>();
            RectTransform inner = UiFactory.Card(root, "Card", new Vector2(360f, 120f), UiStyle.RarityCommon, UiStyle.CardBg, 5f);
            panel._card = (RectTransform)inner.parent;
            UiFactory.StretchRect(panel._card, 0f);
            panel._border = panel._card.GetComponent<Image>();
            panel._border.raycastTarget = false;
            panel._kind = UiFactory.Text(inner, "Kind", "", 20, UiStyle.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.Place(panel._kind.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(340f, 28f));
            panel._name = UiFactory.Text(inner, "Name", "", 30, UiStyle.TextMain, TextAnchor.MiddleCenter);
            UiFactory.Place(panel._name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(340f, 50f));
            panel._card.gameObject.SetActive(false);
            return panel;
        }

        public void Show(LootKind kind, string rewardName)
        {
            RuneRarity rarity = new LootResult { Kind = kind }.DisplayRarity;
            _border.color = UiStyle.Rarity(rarity);
            _kind.text = "宝箱  " + UiText.LootKindLabel(kind);
            _name.text = UiStyle.Colored(rewardName, UiStyle.Rarity(rarity));
            if (_routine != null) StopCoroutine(_routine);
            if (!isActiveAndEnabled) return;
            _routine = StartCoroutine(Flip());
        }

        private IEnumerator Flip()
        {
            _card.gameObject.SetActive(true);
            float t = 0f;
            while (t < FlipSeconds)
            {
                t += Time.unscaledDeltaTime;
                _card.localScale = new Vector3(Mathf.SmoothStep(0f, 1f, t / FlipSeconds), 1f, 1f);
                yield return null;
            }
            _card.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(GameConstants.ChestRevealSeconds - FlipSeconds);
            _card.gameObject.SetActive(false);
            _routine = null;
        }
    }
}
