using System;
using RuneArena.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneArena.UI
{
    /// <summary>Builds every uGUI element in code: canvas, event system, font, sprite, panels, texts, buttons, bars, radial images, input fields.</summary>
    public static class UiFactory
    {
        private static readonly string[] OsFonts = { "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "Noto Sans CJK SC", "Source Han Sans SC", "Arial Unicode MS", "Arial" };
        private const int FontBaseSize = 24;

        private static Font _font;
        private static Sprite _white;

        /// <summary>System font with CJK glyphs when available, else Unity's LegacyRuntime font.</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Font.CreateDynamicFontFromOSFont(OsFonts, FontBaseSize);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>4x4 white sprite for images, bars and radial fills.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                return _white;
            }
        }

        /// <summary>Screen-space overlay canvas scaled for 1920x1080 plus an EventSystem if none exists.</summary>
        public static Canvas CreateCanvas(GameObject host)
        {
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConstants.UiReferenceWidth, GameConstants.UiReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            host.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        /// <summary>Empty RectTransform child.</summary>
        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Full-stretch group (used for panels covering the screen).</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            RectTransform rt = Group(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Positions a RectTransform by anchor (0..1), pivot, offset from the anchor and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Solid colour rectangle.</summary>
        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rt = Group(parent, name);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = White;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Full-screen dim panel that blocks clicks.</summary>
        public static Image Overlay(Transform parent, string name, Color color)
        {
            RectTransform rt = Stretch(parent, name);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = White;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static Text Text(Transform parent, string name, string content, int size, Color color, TextAnchor alignment)
        {
            RectTransform rt = Group(parent, name);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        /// <summary>Text with a dark outline (readable over the arena).</summary>
        public static Text OutlinedText(Transform parent, string name, string content, int size, Color color, TextAnchor alignment)
        {
            Text text = Text(parent, name, content, size, color, alignment);
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = UiStyle.OutlineDark;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        public static Button Button(Transform parent, string name, string label, int fontSize, Action onClick)
        {
            RectTransform rt = Group(parent, name);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = White;
            image.color = Color.white;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = UiStyle.ButtonNormal;
            colors.highlightedColor = UiStyle.ButtonHighlight;
            colors.pressedColor = UiStyle.ButtonPressed;
            colors.selectedColor = UiStyle.ButtonHighlight;
            colors.disabledColor = UiStyle.ButtonDisabled;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Text text = Text(rt, "Label", label, fontSize, UiStyle.TextMain, TextAnchor.MiddleCenter);
            StretchRect(text.rectTransform, 6f);
            return button;
        }

        /// <summary>Changes a button's label text.</summary>
        public static void SetLabel(Button button, string label)
        {
            if (button == null) return;
            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }

        /// <summary>Recolours a button's normal state.</summary>
        public static void SetButtonColor(Button button, Color normal)
        {
            if (button == null) return;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.selectedColor = normal;
            button.colors = colors;
        }

        /// <summary>Horizontal bar: background + left-anchored fill. Returns the fill image (set fillAmount 0..1).</summary>
        public static Image Bar(Transform parent, string name, Color background, Color fill, Vector2 size)
        {
            Image bg = Panel(parent, name, background);
            bg.rectTransform.sizeDelta = size;
            Image fillImage = Panel(bg.transform, "Fill", fill);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            StretchRect(fillImage.rectTransform, 2f);
            return fillImage;
        }

        /// <summary>Radial (clockwise from top) fill overlay for cooldowns.</summary>
        public static Image Radial(Transform parent, string name, Color color)
        {
            Image image = Panel(parent, name, color);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            StretchRect(image.rectTransform, 0f);
            return image;
        }

        /// <summary>Single-line input field (integer content) with placeholder.</summary>
        public static InputField InputField(Transform parent, string name, string placeholder, Vector2 size)
        {
            RectTransform rt = Group(parent, name);
            rt.sizeDelta = size;
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = White;
            bg.color = UiStyle.InputBg;
            Text text = Text(rt, "Text", "", 22, UiStyle.TextDark, TextAnchor.MiddleLeft);
            StretchRect(text.rectTransform, 8f);
            Text hint = Text(rt, "Placeholder", placeholder, 22, UiStyle.InputPlaceholder, TextAnchor.MiddleLeft);
            hint.fontStyle = FontStyle.Italic;
            StretchRect(hint.rectTransform, 8f);
            InputField field = rt.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber;
            field.characterLimit = 9;
            return field;
        }

        /// <summary>Stretches a RectTransform to its parent with a uniform padding.</summary>
        public static void StretchRect(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Card: border (colour) with an inner background; returns the inner RectTransform.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 size, Color border, Color inner, float borderWidth)
        {
            Image outer = Panel(parent, name, border);
            outer.rectTransform.sizeDelta = size;
            outer.raycastTarget = true;
            Image innerImage = Panel(outer.transform, "Inner", inner);
            StretchRect(innerImage.rectTransform, borderWidth);
            return innerImage.rectTransform;
        }
    }

    /// <summary>Scales its transform up while hovered (draft / hero cards).</summary>
    public sealed class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float Scale = GameConstants.DraftCardHoverScale;

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = Vector3.one * Scale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }
    }
}
