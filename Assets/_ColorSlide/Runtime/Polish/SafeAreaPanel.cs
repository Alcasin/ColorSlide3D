using UnityEngine;

namespace ColorSlide.Polish
{
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private Rect previous;
        private Vector2 dimensions;
        private void OnEnable() { if (Application.IsPlaying(gameObject)) Apply(); }
        private void LateUpdate()
        {
            if (previous != Screen.safeArea || dimensions != new Vector2(Screen.width, Screen.height)) Apply();
        }
        private void Apply()
        {
            previous = Screen.safeArea; dimensions = new Vector2(Screen.width, Screen.height);
            var normalized = Normalize(previous, dimensions);
            var rect = (RectTransform)transform; rect.anchorMin = normalized.min; rect.anchorMax = normalized.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        public static Rect Normalize(Rect area, Vector2 screen)
        {
            if (screen.x <= 0f || screen.y <= 0f) return new Rect(0f, 0f, 1f, 1f);
            var min = new Vector2(Mathf.Clamp01(area.xMin / screen.x), Mathf.Clamp01(area.yMin / screen.y));
            var max = new Vector2(Mathf.Clamp01(area.xMax / screen.x), Mathf.Clamp01(area.yMax / screen.y));
            return Rect.MinMaxRect(min.x, min.y, Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }
    }
}
