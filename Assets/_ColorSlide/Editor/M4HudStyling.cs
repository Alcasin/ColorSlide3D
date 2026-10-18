using System;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ColorSlide.Editor
{
    public static class M4HudStyling
    {
        public static Button[] Apply(GameplayHud hud, SoftToyTheme theme)
        {
            var fields = new SerializedObject(hud);
            var timer = Read<Text>(fields, "timeText"); var count = Read<Text>(fields, "countText");
            var title = Read<Text>(fields, "levelText"); var message = Read<Text>(fields, "resultText");
            var restart = Read<Button>(fields, "restart"); var retry = Read<Button>(fields, "retry"); var next = Read<Button>(fields, "next");
            var result = Read<GameObject>(fields, "resultPanel"); var font = timer.font;
            if (font == null) throw new InvalidOperationException("M4 requires the accepted imported/built-in HUD font.");
            var top = timer.transform.parent.GetComponent<RectTransform>();
            var safe = Rect("HUD Safe Area", hud.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaPanel>();
            top.SetParent(safe, false); top.anchorMin = new Vector2(0f, 1f); top.anchorMax = Vector2.one;
            top.pivot = new Vector2(0.5f, 1f); top.anchoredPosition = new Vector2(0f, -12f); top.sizeDelta = new Vector2(-32f, 280f);
            ReplacePanel(top.gameObject, 36f, new Color(1f, 1f, 1f, 0.96f), true);

            var number = Label("Level Number", top, font, 24, theme.InkSrgb * new Color(1f, 1f, 1f, 0.65f));
            Place(number.rectTransform, top, new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(700f, 36f), new Vector2(0f, 1f));
            Place(title.rectTransform, top, new Vector2(0f, 1f), new Vector2(28f, -61f), new Vector2(760f, 64f), new Vector2(0f, 1f));
            Style(title, 40, theme.InkSrgb, TextAnchor.MiddleLeft, true);

            var timeCard = Rect("Time Pill", top, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 20f), new Vector2(270f, 112f));
            timeCard.pivot = Vector2.zero;
            ReplacePanel(timeCard.gameObject, 26f, Hex("F0F5FA"), false);
            var timeCaption = Label("Time Caption", timeCard, font, 20, Hex("718194")); timeCaption.text = "TIME";
            Place(timeCaption.rectTransform, timeCard, new Vector2(0f, 1f), new Vector2(22f, -9f), new Vector2(220f, 30f), new Vector2(0f, 1f));
            Place(timer.rectTransform, timeCard, new Vector2(0f, 0f), new Vector2(22f, 10f), new Vector2(220f, 64f), Vector2.zero);
            Style(timer, 44, theme.InkSrgb, TextAnchor.MiddleLeft, true);
            var countCard = Rect("Pieces Pill", top, Vector2.zero, Vector2.zero, new Vector2(320f, 20f), new Vector2(250f, 112f));
            countCard.pivot = Vector2.zero; ReplacePanel(countCard.gameObject, 26f, Hex("F0F5FA"), false);
            var countCaption = Label("Pieces Caption", countCard, font, 20, Hex("718194")); countCaption.text = "PIECES";
            Place(countCaption.rectTransform, countCard, new Vector2(0f, 1f), new Vector2(22f, -9f), new Vector2(210f, 30f), new Vector2(0f, 1f));
            Place(count.rectTransform, countCard, Vector2.zero, new Vector2(22f, 10f), new Vector2(210f, 64f), Vector2.zero);
            Style(count, 36, theme.InkSrgb, TextAnchor.MiddleLeft, true);
            Place(restart.GetComponent<RectTransform>(), top, new Vector2(1f, 0f), new Vector2(-28f, 20f), new Vector2(200f, 112f), new Vector2(1f, 0f));
            StyleButton(restart, Hex("E6EDF4"), theme.InkSrgb);

            ReplacePanel(result, 0f, new Color(theme.BackgroundSrgb.r, theme.BackgroundSrgb.g, theme.BackgroundSrgb.b, 0.88f), false, true);
            var resultSafe = Rect("Result Safe Area", result.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            resultSafe.gameObject.AddComponent<SafeAreaPanel>();
            var card = Rect("Result Card", resultSafe, new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.67f), Vector2.zero, Vector2.zero);
            ReplacePanel(card.gameObject, 44f, Color.white, true); card.gameObject.AddComponent<CanvasGroup>(); card.gameObject.AddComponent<ResultReveal>();
            Place(message.rectTransform, card, new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(860f, 110f), new Vector2(0.5f, 0.5f));
            Style(message, 54, theme.InkSrgb, TextAnchor.MiddleCenter, true);
            // Stretch copy for smaller reference widths; avoid text extending outside the safe card.
            message.rectTransform.anchorMin = new Vector2(0.04f, 0.65f); message.rectTransform.anchorMax = new Vector2(0.96f, 0.85f);
            message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;
            var caption = Label("Result Caption", card, font, 30, Hex("718194")); caption.alignment = TextAnchor.MiddleCenter;
            caption.rectTransform.anchorMin = new Vector2(0.06f, 0.43f); caption.rectTransform.anchorMax = new Vector2(0.94f, 0.62f);
            caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
            PlaceResultButton(retry, card, 0.08f, 0.46f); StyleButton(retry, Hex("E6EDF4"), theme.InkSrgb);
            PlaceResultButton(next, card, 0.54f, 0.92f); StyleButton(next, Hex("529FE8"), theme.InkSrgb);
            next.gameObject.SetActive(false); result.SetActive(false);
            hud.ConfigurePolishedLabels();
            hud.gameObject.AddComponent<PolishedHudEffects>().Configure(hud, number, caption, retry, next);
            return new[] { restart, retry, next };
        }

        private static T Read<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            fields.FindProperty(name).objectReferenceValue as T ?? throw new InvalidOperationException("Missing HUD field: " + name);
        private static void PlaceResultButton(Button button, RectTransform card, float left, float right)
        {
            var rect = button.GetComponent<RectTransform>(); rect.SetParent(card, false);
            rect.anchorMin = new Vector2(left, 0.12f); rect.anchorMax = new Vector2(right, 0.30f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void StyleButton(Button button, Color fill, Color ink)
        {
            var graphic = ReplacePanel(button.gameObject, 28f, fill, true, true); button.targetGraphic = graphic;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.91f, 0.95f); colors.disabledColor = new Color(0.72f, 0.75f, 0.79f, 0.75f);
            colors.fadeDuration = 0.08f; button.colors = colors; button.transition = Selectable.Transition.ColorTint;
            var label = button.GetComponentInChildren<Text>(true); Style(label, 30, ink, TextAnchor.MiddleCenter, true);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 4f); label.rectTransform.offsetMax = new Vector2(-8f, -4f);
        }
        private static RoundedPanel ReplacePanel(GameObject obj, float radius, Color color, bool shadow, bool blocksRaycasts = false)
        {
            var image = obj.GetComponent<Image>(); if (image != null) UnityEngine.Object.DestroyImmediate(image);
            // Graphic itself only requires RectTransform. Supply the renderer before enabling our replacement.
            if (obj.GetComponent<CanvasRenderer>() == null) obj.AddComponent<CanvasRenderer>();
            var panel = obj.AddComponent<RoundedPanel>(); panel.Configure(radius, color); panel.raycastTarget = blocksRaycasts;
            if (shadow)
            {
                var effect = obj.AddComponent<Shadow>(); effect.effectColor = new Color(0.12f, 0.18f, 0.24f, 0.10f);
                effect.effectDistance = new Vector2(0f, -5f); effect.useGraphicAlpha = true;
            }
            return panel;
        }
        public static bool RepairGraphics(GameplayHud hud)
        {
            var changed = false;
            var result = Read<GameObject>(new SerializedObject(hud), "resultPanel");
            foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true))
            {
                // Inspect the actual component, not Graphic.canvasRenderer's lazy cache.
                if (graphic.GetComponent<CanvasRenderer>() == null)
                {
                    graphic.gameObject.AddComponent<CanvasRenderer>(); graphic.SetAllDirty(); changed = true;
                }
                // Buttons and the modal result backdrop retain their input-blocking behavior.
                if (graphic is RoundedPanel && graphic.gameObject != result && graphic.GetComponent<Button>() == null && graphic.raycastTarget)
                { graphic.raycastTarget = false; changed = true; }
            }
            return changed;
        }
        private static void Style(Text text, int size, Color color, TextAnchor alignment, bool bold)
        { text.fontSize = size; text.color = color; text.alignment = alignment; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.raycastTarget = false; }
        private static Text Label(string name, Transform parent, Font font, int size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>(); text.font = font; Style(text, size, color, TextAnchor.MiddleLeft, false); return text;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Place(RectTransform rect, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
        public static Color Hex(string hex)
        { if (!ColorUtility.TryParseHtmlString("#" + hex, out var color)) throw new ArgumentException(hex); return color; }
    }
}
