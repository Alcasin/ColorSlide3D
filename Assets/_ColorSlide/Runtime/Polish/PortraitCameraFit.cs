using UnityEngine;

namespace ColorSlide.Polish
{
    [DefaultExecutionOrder(-200)]
    public sealed class PortraitCameraFit : MonoBehaviour
    {
        [SerializeField] private float referenceSize = 6.25f;
        private Camera target;
        public float ReferenceSize => referenceSize;
        public void Configure(float size) => referenceSize = size;
        private void OnEnable() { target = GetComponent<Camera>(); if (Application.IsPlaying(gameObject)) Apply(); }
        private void Update() => Apply();
        private void Apply()
        {
            if (target == null || !target.orthographic) return;
            var size = SizeFor(referenceSize, target.aspect);
            if (Mathf.Abs(target.orthographicSize - size) > 0.001f) target.orthographicSize = size;
        }
        public static float SizeFor(float size, float aspect) => aspect > 0f ? size * Mathf.Max(1f, (9f / 16f) / aspect) : size;
    }
}
