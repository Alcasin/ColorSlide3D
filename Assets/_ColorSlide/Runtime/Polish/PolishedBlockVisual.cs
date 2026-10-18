using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
using UnityEngine;

namespace ColorSlide.Polish
{
    public sealed class PolishedBlockVisual : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private GridBlockView view;
        private GameplayController owner;
        private Transform[] visuals;
        private Vector3[] positions, scales;
        private float lift;
        private bool exiting;
        private bool feedback, blocked;
        private float blockedAge;
        private MaterialPropertyBlock working;
        private MaterialPropertyBlock[] originalTints;
        private Color[] selectedColors;
        public bool IsBlockedPulse => blocked;
        public void Configure(GridBlockView block, GameplayController gameplay, float cellSize, bool gameplayFeedback = false)
        {
            feedback = gameplayFeedback;
            view = block; owner = gameplay; lift = cellSize * 0.018f;
            visuals = view.CellRenderers.Select(renderer => renderer.transform).ToArray();
            positions = visuals.Select(child => child.localPosition).ToArray();
            scales = visuals.Select(child => child.localScale).ToArray();
        }
        private void OnEnable()
        {
            if (view == null) return;
            view.SelectionChanged += OnSelection;
            if (!feedback) owner.Drag.MovementRequested += OnMove;
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
        private void OnSelection(bool selected)
        {
            // GridBlockView has already applied the new selection tint before raising this event.
            if (!selected) CancelBlocked(false);
            Refresh();
        }
        private void OnMove(GridBlockView block, MovementResult move)
        {
            if (block != view) return;
            if (move.AcceptedSteps > 0 || move.Status == MovementStatus.Moved || move.Status == MovementStatus.Exited) { CancelBlocked(); Refresh(); }
            if (move.Status == MovementStatus.Exited && !owner.UsesSoftCubeBurst) exiting = true;
        }
        public void ObserveMovement(MovementResult move, bool blockedCue)
        {
            if (!feedback || !isActiveAndEnabled) return;
            // One ordered delivery: clear recovered/partial travel first, then present a new contact.
            // Independent MovementRequested listeners could cancel that contact in the same dispatch.
            OnMove(view, move);
            if (blockedCue) PlayBlocked();
        }
        // Called only by PolishFeedback's existing, once-per-gesture Blocked cue decision.
        public void PlayBlocked()
        {
            if (!feedback || !isActiveAndEnabled || blocked || !view.IsSelected || !owner.CanInteract) return;
            // Native buffers are created in the event handler, never in a MonoBehaviour initializer.
            if (working == null) working = new MaterialPropertyBlock();
            if (originalTints == null)
            {
                originalTints = new MaterialPropertyBlock[visuals.Length]; selectedColors = new Color[visuals.Length];
                for (var i = 0; i < visuals.Length; i++) originalTints[i] = new MaterialPropertyBlock();
            }
            for (var i = 0; i < visuals.Length; i++)
            {
                var renderer = view.CellRenderers[i]; renderer.GetPropertyBlock(originalTints[i]);
                selectedColors[i] = originalTints[i].HasColor(BaseColor) ? originalTints[i].GetColor(BaseColor) : SoftToyTheme.SelectionSrgb(renderer.sharedMaterial);
            }
            blocked = true; blockedAge = 0f;
        }
        private void CancelBlocked(bool restoreTint = true)
        {
            if (blocked && restoreTint && originalTints != null)
                for (var i = 0; i < visuals.Length; i++)
                    if (view.CellRenderers[i] != null) view.CellRenderers[i].SetPropertyBlock(originalTints[i]);
            blocked = false; blockedAge = 0f;
        }
        public void ResetPresentation()
        {
            exiting = false; CancelBlocked();
            if (visuals == null) return;
            for (var i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) { visuals[i].localPosition = positions[i]; visuals[i].localScale = scales[i]; }
        }
        private void LateUpdate() => AdvanceFeedback(Time.unscaledDeltaTime);
        public void AdvanceFeedback(float elapsed)
        {
            if (blocked)
            {
                blockedAge += elapsed;
                if (!view.IsSelected || !owner.CanInteract || blockedAge >= FeedbackMotion.BlockedSeconds) CancelBlocked();
                Refresh();
            }
            else if (exiting) Refresh();
        }
        private void Refresh()
        {
            if (visuals == null) return;
            var shrink = exiting && owner != null ? 1f - 0.06f * owner.ExitProgress : 1f;
            for (var i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] == null) continue;
                // Every axis shrinks inside its occupied cell; collider transforms and selection lift stay fixed.
                visuals[i].localScale = Vector3.Scale(scales[i] * shrink, blocked ? FeedbackMotion.BlockedScale(blockedAge) : Vector3.one);
                if (blocked)
                {
                    var renderer = view.CellRenderers[i]; renderer.GetPropertyBlock(working);
                    var warm = new Color(1f, 0.66f, 0.30f, selectedColors[i].a);
                    working.SetColor(BaseColor, Color.Lerp(selectedColors[i], warm, 0.55f * FeedbackMotion.BlockedStrength(blockedAge)));
                    renderer.SetPropertyBlock(working);
                }
                var y = view != null && view.IsSelected ? lift / visuals[i].parent.lossyScale.y : 0f;
                visuals[i].localPosition = positions[i] + Vector3.up * y;
            }
        }
    }
}
