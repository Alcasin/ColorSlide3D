using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorSlide.Interaction
{
    /// <summary>Serialized initial shape and its compound 3D presentation. No collision authority.</summary>
    public sealed class GridBlockView : MonoBehaviour
    {
        [SerializeField] private string blockId;
        [SerializeField] private Vector2Int initialAnchor;
        [SerializeField] private Vector2Int[] footprint;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private MeshRenderer[] cellRenderers;
        private MaterialPropertyBlock highlight;

        public string BlockId => blockId;
        public Vector2Int InitialAnchor => initialAnchor;
        public IReadOnlyList<Vector2Int> Footprint => footprint;
        public IReadOnlyList<MeshRenderer> CellRenderers => cellRenderers;

        public void Configure(string id, Vector2Int anchor, Vector2Int[] cells, Color tint, MeshRenderer[] renderers)
        {
            blockId = id;
            initialAnchor = anchor;
            footprint = (Vector2Int[])cells.Clone();
            color = tint;
            cellRenderers = (MeshRenderer[])renderers.Clone();
        }

        public void SetSelected(bool selected)
        {
            if (cellRenderers == null)
                return;
            if (highlight == null)
                highlight = new MaterialPropertyBlock();
            foreach (var renderer in cellRenderers)
            {
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(highlight);
                highlight.SetColor("_BaseColor", selected ? Color.Lerp(color, Color.white, 0.4f) : color);
                renderer.SetPropertyBlock(highlight);
            }
        }

        public void SnapTo(Vector3 anchorPosition) => transform.position = anchorPosition;
    }
}
