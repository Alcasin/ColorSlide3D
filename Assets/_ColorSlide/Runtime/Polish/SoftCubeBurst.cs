using System;
using ColorSlide.Board;
using ColorSlide.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorSlide.Polish
{
    /// <summary>One owned effect, advanced by GameplayController's existing exit clock. Never emits gameplay/audio events.</summary>
    public sealed class SoftCubeBurst : MonoBehaviour
    {
        private SoftToyTheme theme;
        private float cellSize;
        private GridBlockView target;
        private Vector3 start, end;
        private Renderer[] originals;
        private bool[] originalEnabled;
        private SoftBurstPiece[] pieces;
        private Transform fragmentRoot;
        private Transform[] fragments;
        private PortraitCameraFit framing;
        public bool IsRunning => target != null;
        public int ActiveFragmentCount => fragmentRoot != null && fragmentRoot.gameObject.activeSelf ? SoftBurstMotion.FragmentCount : 0;
        public SoftToyTheme Theme => theme;
        public void ConfigureFraming(PortraitCameraFit cameraFit) => framing = cameraFit;
        public void Configure(SoftToyTheme assets, float size)
        {
            if (assets == null || float.IsNaN(size) || float.IsInfinity(size) || size <= 0f)
                throw new ArgumentException("A burst needs the accepted M4 theme and positive cell size.");
            assets.Validate(); theme = assets; cellSize = size;
        }
        public bool Begin(GridBlockView view, ExitPlan plan, Vector3 from, Vector3 to)
        {
            if (IsRunning || !isActiveAndEnabled || theme == null || view == null || !view.isActiveAndEnabled || plan == null ||
                !plan.Block.IsRemoved || plan.Block.Id != view.BlockId) return false;
            Cancel(); // Also retires any resources whose former view was destroyed externally.
            target = view; start = from; end = to;
            originals = view.GetComponentsInChildren<Renderer>(true);
            originalEnabled = new bool[originals.Length];
            for (var i = 0; i < originals.Length; i++) originalEnabled[i] = originals[i].enabled;
            pieces = SoftBurstMotion.Create(view.Footprint, plan.Gate.Edge);
            return true;
        }
        /// <returns>True only when the owner may finish its current exit (including cancellation/disabled effects).</returns>
        public bool Present(float age)
        {
            if (!IsRunning || !isActiveAndEnabled) return true;
            target.SnapTo(Vector3.Lerp(start, end, SoftBurstMotion.OutwardProgress(age)));
            if (age < SoftBurstMotion.OutwardSeconds) return false;
            if (fragmentRoot == null) Scatter();
            var progress = SoftBurstMotion.ScatterProgress(age);
            for (var i = 0; i < fragments.Length; i++)
            {
                fragments[i].localPosition = pieces[i].Position(progress) * cellSize;
                fragments[i].localRotation = Quaternion.Euler(pieces[i].Rotation * SoftBurstMotion.Ease(progress));
                fragments[i].localScale = Vector3.one * (pieces[i].Scale(progress) * cellSize);
            }
            return SoftBurstMotion.IsComplete(age);
        }
        private void Scatter()
        {
            var material = target.CellRenderers[0].sharedMaterial;
            fragmentRoot = new GameObject("Soft Cube Burst").transform;
            fragmentRoot.SetParent(transform, false);
            fragmentRoot.position = framing != null ? framing.VisibleBurstOrigin(end, pieces, cellSize) : end;
            fragments = new Transform[pieces.Length];
            for (var i = 0; i < pieces.Length; i++)
            {
                var obj = new GameObject("Soft Fragment " + i, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(fragmentRoot, false); fragments[i] = obj.transform;
                obj.GetComponent<MeshFilter>().sharedMesh = theme.RoundedBox;
                var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            foreach (var renderer in originals) if (renderer != null) renderer.enabled = false;
        }
        public void Cancel()
        {
            if (originals != null)
                for (var i = 0; i < originals.Length; i++) if (originals[i] != null) originals[i].enabled = originalEnabled[i];
            if (fragmentRoot != null)
            {
                var obj = fragmentRoot.gameObject; obj.SetActive(false);
                if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
            }
            target = null; originals = null; originalEnabled = null; pieces = null; fragments = null; fragmentRoot = null;
        }
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
