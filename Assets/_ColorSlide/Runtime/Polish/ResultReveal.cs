using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Polish
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ResultReveal : MonoBehaviour
    {
        [SerializeField] private GameplayHud feedbackHud;
        private CanvasGroup group;
        private float age;
        private GameplayController celebratedOwner;
        private GameObject decoration;
        private SoftSparkle[] sparkles;
        private Vector2[] anchors;
        private Vector3 originalScale;
        private float originalAlpha;
        private bool captured, win;
        public GameplayHud FeedbackHud => feedbackHud;
        public bool IsCelebrating => win && decoration != null;
        public int ActiveDecorationCount => decoration != null && decoration.activeSelf ? decoration.transform.childCount : 0;
        public void ConfigureFeedback(GameplayHud hud) => feedbackHud = hud;
        private void OnEnable()
        {
            group = GetComponent<CanvasGroup>(); age = 0f;
            if (Application.IsPlaying(gameObject)) BeginPresentation();
        }
        public void BeginPresentation()
        {
            CancelFeedback(); group = GetComponent<CanvasGroup>();
            originalScale = feedbackHud != null ? transform.localScale : Vector3.one;
            originalAlpha = feedbackHud != null ? group.alpha : 1f;
            captured = true; age = 0f;
            var owner = feedbackHud != null ? feedbackHud.Gameplay : null;
            win = owner != null && owner.Session != null && owner.Session.State == GameplayState.Won && !owner.IsPresentingExit;
            if (win && celebratedOwner == owner) { win = false; age = FeedbackMotion.WinSeconds; return; }
            if (win) { celebratedOwner = owner; CreateDecoration(); }
            group.alpha = 0f; transform.localScale = originalScale * 0.97f;
        }
        private void LateUpdate() => AdvancePresentation(Time.unscaledDeltaTime);
        public void AdvancePresentation(float elapsed)
        {
            if (!captured || age >= FeedbackMotion.WinSeconds) return;
            age += elapsed;
            var t = Mathf.Clamp01(age / 0.16f); var ease = 1f - (1f - t) * (1f - t) * (1f - t);
            group.alpha = originalAlpha * ease;
            transform.localScale = originalScale * Mathf.Lerp(0.97f, 1f, win ? FeedbackMotion.WinEase(age) : ease);
            if (!win) return;
            var u = Mathf.Clamp01(age / FeedbackMotion.WinSeconds);
            for (var i = 0; i < sparkles.Length; i++)
            {
                var rect = sparkles[i].rectTransform;
                rect.anchoredPosition = anchors[i] + new Vector2(i % 2 == 0 ? -20f : 20f, 26f) * FeedbackMotion.WinEase(age);
                rect.localScale = Vector3.one * (0.85f + 0.15f * ease);
                var tint = sparkles[i].color; tint.a = Mathf.Sin(Mathf.PI * u) * 0.95f; sparkles[i].color = tint;
            }
            if (age >= FeedbackMotion.WinSeconds) CancelFeedback();
        }
        private void CreateDecoration()
        {
            decoration = new GameObject("Win Sparkles", typeof(RectTransform));
            var root = decoration.GetComponent<RectTransform>(); root.SetParent(transform, false);
            root.SetAsLastSibling(); // Draw above the existing card; no extra Canvas or result panel.
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            sparkles = new SoftSparkle[6]; anchors = new Vector2[6];
            for (var i = 0; i < sparkles.Length; i++)
            {
                var obj = new GameObject("Sparkle " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(SoftSparkle));
                var rect = obj.GetComponent<RectTransform>(); rect.SetParent(root, false);
                // Upper border and outer sides only; buttons occupy the lower part of the existing card.
                rect.anchorMin = rect.anchorMax = new Vector2(i % 2 == 0 ? 0.025f : 0.975f, 0.62f + i / 2 * 0.18f);
                rect.sizeDelta = Vector2.one * (i % 3 == 0 ? 54f : 44f);
                anchors[i] = rect.anchoredPosition = Vector2.zero;
                sparkles[i] = obj.GetComponent<SoftSparkle>(); sparkles[i].raycastTarget = false;
                sparkles[i].color = i % 2 == 0 ? new Color(0.76f, 0.40f, 0.065f, 0f) : new Color(0.92f, 0.60f, 0.12f, 0f);
            }
        }
        public void ResetCelebration() { celebratedOwner = null; CancelFeedback(); }
        public void CancelFeedback()
        {
            if (decoration != null)
            {
                decoration.SetActive(false);
                if (Application.isPlaying) Destroy(decoration); else DestroyImmediate(decoration);
            }
            decoration = null; sparkles = null; anchors = null; win = false;
            if (captured) { if (group != null) group.alpha = originalAlpha; transform.localScale = originalScale; }
            captured = false;
        }
        private void OnDisable() => CancelFeedback();
    }
}
