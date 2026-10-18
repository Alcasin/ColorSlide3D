using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
using UnityEngine;

namespace ColorSlide.Polish
{
    public sealed class PolishedBlockVisual : MonoBehaviour
    {
        private GridBlockView view;
        private GameplayController owner;
        private Transform[] visuals;
        private Vector3[] positions, scales;
        private float lift;
        private bool exiting;
        public void Configure(GridBlockView block, GameplayController gameplay, float cellSize)
        {
            view = block; owner = gameplay; lift = cellSize * 0.018f;
            visuals = view.CellRenderers.Select(renderer => renderer.transform).ToArray();
            positions = visuals.Select(child => child.localPosition).ToArray();
            scales = visuals.Select(child => child.localScale).ToArray();
        }
        private void OnEnable()
        {
            if (view == null) return;
            view.SelectionChanged += OnSelection;
            owner.Drag.MovementRequested += OnMove;
            owner.Restarted += ResetPresentation;
            ResetPresentation();
        }
        private void OnDisable()
        {
            if (view == null) return;
            view.SelectionChanged -= OnSelection;
            if (owner != null) { owner.Drag.MovementRequested -= OnMove; owner.Restarted -= ResetPresentation; }
            ResetPresentation();
        }
        private void OnSelection(bool selected) => Refresh();
        private void OnMove(GridBlockView block, MovementResult move)
        { if (block == view && move.Status == MovementStatus.Exited) exiting = true; }
        public void ResetPresentation()
        {
            exiting = false;
            if (visuals == null) return;
            for (var i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) { visuals[i].localPosition = positions[i]; visuals[i].localScale = scales[i]; }
        }
        private void LateUpdate() { if (exiting) Refresh(); }
        private void Refresh()
        {
            if (visuals == null) return;
            var shrink = exiting && owner != null ? 1f - 0.06f * owner.ExitProgress : 1f;
            for (var i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] == null) continue;
                visuals[i].localScale = scales[i] * shrink;
                var y = view != null && view.IsSelected ? lift / visuals[i].parent.lossyScale.y : 0f;
                visuals[i].localPosition = positions[i] + Vector3.up * y;
            }
        }
    }
}
