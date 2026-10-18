using UnityEngine;
using UnityEngine.UI;

namespace ColorSlide.Polish
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedPanel : MaskableGraphic
    {
        [SerializeField] private float radius = 28f;
        public void Configure(float cornerRadius, Color tint) { radius = cornerRadius; color = tint; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); var rect = rectTransform.rect;
            var perimeter = SoftGeometry.RoundedRect(rect.width, rect.height, radius);
            if (perimeter.Length < 3) return;
            var vertex = UIVertex.simpleVert; vertex.color = color; vertex.position = rect.center; helper.AddVert(vertex);
            foreach (var point in perimeter) { vertex.position = point + rect.center; helper.AddVert(vertex); }
            for (var i = 0; i < perimeter.Length; i++) helper.AddTriangle(0, i + 1, (i + 1) % perimeter.Length + 1);
        }
    }
}
