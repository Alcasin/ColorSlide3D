using UnityEngine;

namespace ColorSlide.Polish
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ResultReveal : MonoBehaviour
    {
        private CanvasGroup group;
        private float age;
        private void OnEnable()
        {
            group = GetComponent<CanvasGroup>(); age = 0f;
            if (Application.IsPlaying(gameObject)) { group.alpha = 0f; transform.localScale = Vector3.one * 0.97f; }
        }
        private void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(age / 0.16f); var ease = 1f - (1f - t) * (1f - t) * (1f - t);
            group.alpha = ease; transform.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, ease);
        }
        private void OnDisable() { if (group != null) group.alpha = 1f; transform.localScale = Vector3.one; }
    }
}
