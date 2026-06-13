using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelDungeon
{
    /// <summary>Tiny runtime uGUI builder so the HUD and menus can be constructed in code
    /// (design doc 4.1 UI module) without hand-authoring prefabs.</summary>
    public static class UIKit
    {
        private static Font _fallback;
        private static Sprite _square;

        public static Font ResolveFont(Font preferred)
        {
            if (preferred != null) return preferred;
            if (_fallback == null) _fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _fallback;
        }

        public static Sprite Square => _square ??= GameUtil.MakeSquare();

        public static Canvas CreateCanvas()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var go = new GameObject("UICanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Make(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = Make(parent, "Panel");
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var im = rt.gameObject.AddComponent<Image>();
            im.color = color;
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor, Font font)
        {
            var rt = Make(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = ResolveFont(font);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        public static Image Image(Transform parent, Color color, Sprite sprite = null)
        {
            var rt = Make(parent, "Image");
            var im = rt.gameObject.AddComponent<Image>();
            im.color = color;
            if (sprite != null) im.sprite = sprite;
            im.raycastTarget = false;
            return im;
        }

        /// <summary>Returns the fill image (use .fillAmount). Background is created behind it.</summary>
        public static Image Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor)
        {
            var bg = Image(parent, new Color(0f, 0f, 0f, 0.6f), Square);
            Place((RectTransform)bg.transform, anchor, pos, size);

            var fill = Image(parent, fillColor, Square);
            Place((RectTransform)fill.transform, anchor, pos, size);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            return fill;
        }

        public static Button Button(Transform parent, string label, Font font, Action onClick, Vector2 size, Vector2 anchoredPos, Vector2? anchor = null)
        {
            var rt = Make(parent, "Button");
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            Place(rt, a, anchoredPos, size);
            var im = rt.gameObject.AddComponent<Image>();
            im.color = new Color(0.22f, 0.24f, 0.34f, 0.96f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            if (onClick != null) b.onClick.AddListener(() => onClick());

            var t = Label(rt, label, 22, Color.white, TextAnchor.MiddleCenter, font);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return b;
        }
    }
}
