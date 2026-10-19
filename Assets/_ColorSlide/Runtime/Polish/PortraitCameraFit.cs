using UnityEngine;
using ColorSlide.Gameplay;
using System.Collections.Generic;
using System.Linq;

namespace ColorSlide.Polish
{
    [DefaultExecutionOrder(-200)]
    public sealed class PortraitCameraFit : MonoBehaviour
    {
        [SerializeField] private float referenceSize = 6.25f;
        [SerializeField, Range(BoardFraming.MinimumZoom, BoardFraming.MaximumZoom)]
        [InspectorName("Large Board Zoom")]
        [Tooltip("Default gameplay zoom for boards taller than 10 cells. Earlier levels keep 1x unless adjusted during Editor QA.")]
        // Fresh serialization key avoids retaining the old 1.18 default through script hot reload.
        private float largeBoardDefaultZoom = BoardFraming.DefaultLargeBoardZoom;
        private Camera target;
        private Vector3 originalPosition;
        private Transform board;
        private Bounds boardEnvelope;
        private Rect presentationProjection;
        private Rect playableProjection, trayProjection;
        private bool hasTray, useBoardFit;
        private bool usePresentationFit;
        private bool movedCamera;
        private GameplayController gameplay;
        private float appliedZoom = 1f;
        private Vector2 frameScreen;
        private Rect frameSafeArea;
#if UNITY_EDITOR
        private float? editorZoom;
#endif
        public float ReferenceSize => referenceSize;
        public float LargeBoardZoom => BoardFraming.ClampZoom(largeBoardDefaultZoom);
        public float ZoomMultiplier
        {
            get
            {
#if UNITY_EDITOR
                if (editorZoom.HasValue) return editorZoom.Value;
#endif
                return usePresentationFit ? LargeBoardZoom : 1f;
            }
        }
        public float EffectiveZoom { get; private set; } = 1f;
        public bool ZoomPending => ZoomLocked && Mathf.Abs(appliedZoom - ZoomMultiplier) > 0.0001f;
        private bool ZoomLocked
        {
            get
            {
                if (gameplay == null || !gameplay.isActiveAndEnabled) return false;
                if (gameplay.IsPresentingExit) return true;
                var views = gameplay.Board.BlockViews;
                for (var i = 0; i < views.Count; i++) if (views[i] != null && views[i].IsSelected) return true;
                return false;
            }
        }
        public void Configure(float size) => referenceSize = size;
        private void EnsureCamera()
        {
            if (target != null) return;
            target = GetComponent<Camera>();
            if (target != null) originalPosition = target.transform.position;
        }
        public void FrameBoard(Transform root, LevelData level)
        {
            EnsureCamera();
            if (target != null && !movedCamera) originalPosition = target.transform.position;
            board = root; gameplay = root.GetComponent<GameplayController>();
            useBoardFit = level.Width > 6 || level.Height > 8;
            // Preserve the accepted profiles through 8x10. The current maximum-height boards
            // use actual presentation geometry, rather than one inflated world-space box.
            usePresentationFit = useBoardFit && level.Height > 10;
            if (target != null)
            {
                var playable = BoardFraming.PlayableEnvelope(level); playable.center += root.position;
                var core = new List<Bounds> { playable }; var trays = new List<Bounds>();
                // Read the authored tray geometry; never change its transform, mesh or material.
                var theme = root.GetComponentInParent<LevelPresentation>()?.Theme;
                if (theme != null)
                    foreach (var sceneRoot in root.gameObject.scene.GetRootGameObjects())
                    foreach (var renderer in sceneRoot.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMaterials.Contains(theme.Tray)) trays.Add(renderer.bounds);
                core.AddRange(trays); hasTray = trays.Count > 0;
                playableProjection = BoardFraming.Project(core, target.transform.right, target.transform.up);
                trayProjection = hasTray ? BoardFraming.Project(trays, target.transform.right, target.transform.up) : playableProjection;
                if (usePresentationFit)
                {
                    var geometry = new List<Bounds>();
                    foreach (var local in BoardFraming.PresentationEnvelope(level))
                    { var world = local; world.center += root.position; geometry.Add(world); }
                    geometry.AddRange(trays);
                    presentationProjection = BoardFraming.Project(geometry, target.transform.right, target.transform.up);
                }
                else if (useBoardFit) boardEnvelope = BoardFraming.Envelope(level);
            }
            if (Application.IsPlaying(gameObject)) Apply();
        }
        private void OnEnable() { EnsureCamera(); if (Application.IsPlaying(gameObject)) Apply(); }
        private void Update() => Apply();
        // Calculates the same responsive frame without mutating the camera (also usable in EditMode).
        public BoardFraming.Frame CalculateFrame(Vector2 screen, Rect safeArea, float aspect)
        {
            EnsureCamera();
            frameScreen = screen; frameSafeArea = safeArea;
            var size = SizeFor(referenceSize, aspect);
            if (board == null) return new BoardFraming.Frame(originalPosition, size);
            BoardFraming.Frame baseline;
            if (usePresentationFit)
                baseline = BoardFraming.FitPresentation(presentationProjection, originalPosition, target.transform.right, target.transform.up,
                    aspect, screen, safeArea);
            else if (useBoardFit)
            {
                var bounds = boardEnvelope; bounds.center += board.position;
                baseline = BoardFraming.Fit(bounds, originalPosition, target.transform.right, target.transform.up, aspect,
                    BoardFraming.GameplayRegion(screen, safeArea), size);
            }
            else baseline = new BoardFraming.Frame(originalPosition, size);
            if (!ZoomLocked) appliedZoom = ZoomMultiplier;
            var frame = BoardFraming.Zoom(baseline, playableProjection, appliedZoom, target.transform.right, target.transform.up, aspect, screen, safeArea);
            EffectiveZoom = baseline.Size / frame.Size;
            return frame;
        }
        public float TrayViewportUtilization(BoardFraming.Frame frame, float aspect) => trayProjection.width / (2f * frame.Size * aspect);
        public Vector3 VisibleBurstOrigin(Vector3 origin, SoftBurstPiece[] pieces, float cellSize)
        {
            if (target == null || appliedZoom <= 1f || frameScreen.x <= 0f) return origin;
            var right = target.transform.right; var up = target.transform.up;
            var local = BoardFraming.Project(BoardFraming.BurstEnvelope(pieces, cellSize, Vector3.zero), right, up);
            return BoardFraming.VisibleBurstOrigin(local, origin, new BoardFraming.Frame(target.transform.position, target.orthographicSize),
                right, up, target.aspect, BoardFraming.PresentationRegion(frameScreen, frameSafeArea));
        }
#if UNITY_EDITOR
        public void SetEditorZoom(float multiplier)
        {
            editorZoom = BoardFraming.ClampZoom(multiplier);
            if (Application.IsPlaying(gameObject)) Apply();
        }
        public void ResetEditorZoom() => editorZoom = null;
#endif
        private void Apply()
        {
            if (target == null || !target.orthographic) return;
            var fit = CalculateFrame(new Vector2(Screen.width, Screen.height), Screen.safeArea, target.aspect);
            if (board != null)
            {
                target.transform.position = fit.CameraPosition; movedCamera = true;
            }
            else if (movedCamera) { target.transform.position = originalPosition; movedCamera = false; }
            if (Mathf.Abs(target.orthographicSize - fit.Size) > 0.001f) target.orthographicSize = fit.Size;
        }
        private void OnDisable()
        {
#if UNITY_EDITOR
            editorZoom = null;
#endif
            appliedZoom = 1f;
            if (target == null || !movedCamera) return;
            target.transform.position = originalPosition; target.orthographicSize = SizeFor(referenceSize, target.aspect);
            movedCamera = false;
        }
        public static float SizeFor(float size, float aspect) => aspect > 0f ? size * Mathf.Max(1f, (9f / 16f) / aspect) : size;
    }
}
