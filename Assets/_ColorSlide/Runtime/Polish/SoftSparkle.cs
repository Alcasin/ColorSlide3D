using UnityEngine;
using UnityEngine.UI;

namespace ColorSlide.Polish
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SoftSparkle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); var rect = rectTransform.rect;
            helper.AddVert(rect.center, color, Vector2.zero);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                var radius = (i % 2 == 0 ? 0.5f : 0.16f) * Mathf.Min(rect.width, rect.height);
                helper.AddVert(rect.center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius, color, Vector2.zero);
            }
            for (var i = 0; i < 8; i++) helper.AddTriangle(0, i + 1, (i + 1) % 8 + 1);
        }
    }
}
