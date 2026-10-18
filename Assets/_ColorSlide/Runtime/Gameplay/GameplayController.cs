using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColorSlide.Gameplay
{
    /// <summary>Scene ownership, short exit presentation and Retry. All decisions belong to PuzzleSession.</summary>
    public sealed class GameplayController : MonoBehaviour
    {
        [SerializeField] private InteractionBoard board;
        [SerializeField] private PointerDragController drag;
        [SerializeField] private ExitGateView[] gateViews;
        [SerializeField, Min(0.01f)] private float timeLimit = M2DemoLayout.Seconds;
        [SerializeField, Range(0.2f, 0.4f)] private float exitSeconds = 0.3f;
        private readonly Dictionary<Collider, bool> initialColliders = new Dictionary<Collider, bool>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private GridBlockView exiting;
        private Vector3 exitStart;
        private Vector3 exitEnd;
        private float exitAge;

        public PuzzleSession Session { get; private set; }
        public InteractionBoard Board => board;
        public PointerDragController Drag => drag;
        public IReadOnlyList<ExitGateView> GateViews => gateViews;
        public bool IsPresentingExit => exiting != null;
        public float ExitProgress => exiting != null ? Mathf.Clamp01(exitAge / exitSeconds) : 0f;
        public event Action Restarted;
        public bool CanInteract => isActiveAndEnabled && Session != null &&
            Session.State == GameplayState.Playing && exiting == null;

        public void Configure(InteractionBoard targetBoard, PointerDragController input, ExitGateView[] exits)
        {
            board = targetBoard;
            drag = input;
            gateViews = (ExitGateView[])exits.Clone();
        }

        public PuzzleSession CreateSession()
        {
            if (board == null || drag == null || gateViews == null || gateViews.Any(gate => gate == null))
                throw new InvalidOperationException("Assign the M2 board, drag controller and all gates.");
            if (float.IsNaN(exitSeconds) || float.IsInfinity(exitSeconds) || exitSeconds < 0.2f || exitSeconds > 0.4f)
                throw new InvalidOperationException("Exit animation duration must be 0.2–0.4 seconds.");
            return new PuzzleSession(board.CreateModel(), gateViews.Select(view => view.CreateDefinition()), timeLimit);
        }

        public void Initialize()
        {
            if (Session != null)
                return;
            InitializeSession(CreateSession());
        }

        // Campaign construction injects its already validated session before this inactive hierarchy is enabled.
        internal void InitializeSession(PuzzleSession session)
        {
            if (Session != null || session == null)
                throw new InvalidOperationException("Initialize a fresh gameplay controller with one session.");
            Session = session;
            timeLimit = (float)session.InitialSeconds;
            board.AdoptModel(Session.Board);
            foreach (var view in board.BlockViews)
            foreach (var collider in view.GetComponentsInChildren<Collider>(true))
                initialColliders.Add(collider, collider.enabled);
            Session.Start();
        }

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
                return;
            try { Initialize(); }
            catch (Exception exception)
            {
                Debug.LogError("ColorSlide M2 could not start: " + exception.Message, this);
                enabled = false;
            }
        }

        public MovementResult RequestMove(GridBlockView view, Vector2Int target, GridAxis axis)
        {
            if (!CanInteract || view == null || !board.Owns(view))
                return new MovementResult(MovementStatus.SessionInactive);
            var result = Session.MoveTowards(view.BlockId, target, axis);
            if (result.Exit != null)
            {
                // Occupancy is already released. Serialize interaction for these 0.3 seconds,
                // so another block cannot visually enter the departing shape's swept cells.
                exiting = view;
                exitStart = board.Coordinates.CellToWorld(result.Exit.StartAnchor);
                exitEnd = board.Coordinates.CellToWorld(result.Exit.EndAnchor);
                exitAge = 0f;
                view.SetSelected(false);
                view.SnapTo(exitStart);
                foreach (var collider in view.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }
            return result;
        }

        private void LateUpdate()
        {
            if (Session == null)
                return;
            // Pointer input happens in Update. A final accepted exit wins over timeout in this frame.
            if (Session.Advance(Time.unscaledDeltaTime))
                drag.CancelDrag();
            if (exiting == null)
                return;
            exitAge += Time.unscaledDeltaTime;
            exiting.SnapTo(Vector3.Lerp(exitStart, exitEnd, Mathf.Clamp01(exitAge / exitSeconds)));
            if (exitAge >= exitSeconds)
                FinishExit();
        }

        private void FinishExit()
        {
            if (exiting != null)
            {
                exiting.SnapTo(exitEnd);
                exiting.gameObject.SetActive(false);
            }
            exiting = null;
            exitAge = 0f;
        }

        public void Restart()
        {
            if (!Application.IsPlaying(gameObject))
                return;
            Initialize();
            drag.CancelDrag();
            exiting = null;
            exitAge = 0f;
            Session.Restart();
            board.AdoptModel(Session.Board);
            foreach (var view in board.BlockViews)
                view.gameObject.SetActive(true);
            foreach (var pair in initialColliders)
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            Restarted?.Invoke();
        }

        public bool IsPointerOverUI(Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null)
                return false;
            uiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = position }, uiHits);
            // Explicit raycast avoids depending on EventSystem.Update order or touch pointer IDs.
            return uiHits.Any(hit => hit.module is GraphicRaycaster);
        }

        private void OnDisable()
        {
            if (drag != null)
                drag.CancelDrag();
            FinishExit();
        }
    }
}
