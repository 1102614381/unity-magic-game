using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// 纯代码搭 UI 的小工具，目的是让每个场景只需要一个空物体 + 一个脚本就能跑起来。
    /// 后面欢婷出了正式设计稿，逐个替换即可。
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Background = new Color(0.98f, 0.96f, 0.92f);
        public static readonly Color Primary = new Color(0.42f, 0.36f, 0.85f);
        public static readonly Color PrimaryDark = new Color(0.32f, 0.27f, 0.70f);
        public static readonly Color TextDark = new Color(0.18f, 0.17f, 0.22f);
        public static readonly Color TextMuted = new Color(0.45f, 0.44f, 0.50f);
        public static readonly Color Field = new Color(1f, 1f, 1f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { /* 旧版本 Unity */ }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { /* 新版本 Unity */ }
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name = "Canvas", bool withBackground = true)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            if (withBackground) CreateImage(canvas.transform, "Background", Background);

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static RectTransform CreateVerticalContainer(Transform parent, float horizontalPadding = 460f,
                                                            float spacing = 22f, float verticalPadding = 70f)
        {
            var go = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            Stretch(go);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset((int)horizontalPadding, (int)horizontalPadding,
                                            (int)verticalPadding, (int)verticalPadding);

            return (RectTransform)go.transform;
        }

        public static RectTransform CreateHorizontalRow(Transform parent, float spacing, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            return (RectTransform)go.transform;
        }

        public static Text CreateText(Transform parent, string content, float fontSize,
                                      TextAnchor anchor = TextAnchor.MiddleCenter,
                                      Color? color = null, bool addLayoutElement = true)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch(go);

            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = Mathf.RoundToInt(fontSize);
            text.text = content;
            text.alignment = anchor;
            text.color = color ?? TextDark;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            if (addLayoutElement)
            {
                var element = go.AddComponent<LayoutElement>();
                element.minHeight = fontSize * 1.5f;
            }
            return text;
        }

        public static Button CreateButton(Transform parent, string label, float height, Action onClick, float fontSize = 34)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = Primary;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.80f, 0.80f, 0.80f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            var labelText = CreateText(go.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white, false);
            labelText.fontStyle = FontStyle.Normal;

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static InputField CreateInputField(Transform parent, string placeholder, float height = 78f, int characterLimit = 0)
        {
            var go = new GameObject("Input_" + placeholder, typeof(RectTransform), typeof(Image),
                                    typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = Field;

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = Stretch(textGo);
            textRect.offsetMin = new Vector2(24f, 10f);
            textRect.offsetMax = new Vector2(-24f, -10f);

            var textComponent = textGo.GetComponent<Text>();
            textComponent.font = DefaultFont;
            textComponent.fontSize = 30;
            textComponent.color = TextDark;
            textComponent.alignment = TextAnchor.MiddleLeft;
            textComponent.supportRichText = false;
            textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholderRect = Stretch(placeholderGo);
            placeholderRect.offsetMin = new Vector2(24f, 10f);
            placeholderRect.offsetMax = new Vector2(-24f, -10f);

            var placeholderText = placeholderGo.GetComponent<Text>();
            placeholderText.font = DefaultFont;
            placeholderText.fontSize = 30;
            placeholderText.color = new Color(0.6f, 0.6f, 0.65f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = placeholder;

            var input = go.GetComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = textComponent;
            input.placeholder = placeholderText;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;

            return input;
        }

        private static RectTransform Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
