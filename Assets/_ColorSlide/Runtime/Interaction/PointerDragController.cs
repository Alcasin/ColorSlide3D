using System;
using ColorSlide.Board;
using ColorSlide.Input;
using ColorSlide.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColorSlide.Interaction
{
    /// <summary>Single-pointer dragging with per-motion axis choice and authoritative grid steps.</summary>
    public sealed class PointerDragController : MonoBehaviour
    {
        [SerializeField] private InteractionBoard board;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameplayController gameplay;
        private InputActionAsset ownedActions;
        private PointerActions actions;
        private Pointer pointer;
        private GridBlockView selected;
        private Plane dragPlane;
        private Vector3 grabOffset;
        private Vector3 previousPoint;

        public InteractionBoard Board => board;
        public Camera InteractionCamera => interactionCamera;
        public InputActionAsset InputActions => inputActions;
        public GameplayController Gameplay => gameplay;
        public event Action<GridBlockView> SelectionStarted;
        public event Action<GridBlockView, MovementResult> MovementRequested;
        public void ConfigureGameplay(GameplayController session) => gameplay = session;
        public void CancelDrag() => EndDrag();

        public void Configure(InteractionBoard targetBoard, Camera camera, InputActionAsset asset)
        {
            board = targetBoard;
            interactionCamera = camera;
            inputActions = asset;
        }

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
                return;
            try
            {
                if (board == null || interactionCamera == null || inputActions == null)
                    throw new InvalidOperationException("Assign the board, camera and M0 Input Actions asset.");
                if (gameplay != null)
                    gameplay.Initialize();
                board.Initialize();
                // Clone so this component owns enable/disable without altering project-wide input.
                ownedActions = Instantiate(inputActions);
                actions = new PointerActions(ownedActions);
                actions.Map.Enable();
            }
            catch (Exception exception)
            {
                Debug.LogError("ColorSlide M1 input could not start: " + exception.Message, this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (actions == null)
                return;
            if (gameplay != null && !gameplay.CanInteract)
            {
                EndDrag();
                return;
            }
            if (board == null || !board.isActiveAndEnabled || interactionCamera == null || !interactionCamera.isActiveAndEnabled)
            {
                EndDrag();
                return;
            }
            if (selected != null && (!selected.isActiveAndEnabled || pointer == null ||
                !pointer.added || !pointer.press.isPressed || actions.Press.WasReleasedThisFrame()))
            {
                EndDrag();
                return;
            }
            if (selected == null)
            {
                if (actions.Press.WasPressedThisFrame())
                    BeginDrag();
                return;
            }

            if (!ProjectPointer(pointer.position.ReadValue(), out var point))
                return;
            var motion = point - previousPoint;
            previousPoint = point;
            if (motion.sqrMagnitude < 0.000001f * board.CellSize * board.CellSize)
                return; // A stationary pointer never requests a detour after a blocked move.
            var target = board.Coordinates.WorldToCell(point - grabOffset);
            if (gameplay != null)
            {
                var result = gameplay.RequestMove(selected, target, BoardCoordinates.DominantAxis(motion));
                MovementRequested?.Invoke(selected, result);
                if (result.Status == MovementStatus.Exited)
                {
                    EndDrag();
                    return;
                }
            }
            else
                board.Model.MoveTowards(selected.BlockId, target, BoardCoordinates.DominantAxis(motion));
            selected.SnapTo(board.Coordinates.CellToWorld(board.Model.GetBlock(selected.BlockId).Anchor));
        }

        private void BeginDrag()
        {
            // Lock position and held state to the device that pressed, even if a mouse also moves.
            var pressedPointer = actions.Press.activeControl?.device as Pointer;
            if (pressedPointer == null || !pressedPointer.press.isPressed)
                return;
            var position = pressedPointer.position.ReadValue();
            if (gameplay != null && gameplay.IsPointerOverUI(position))
                return;
            Physics.SyncTransforms();
            if (!Physics.Raycast(interactionCamera.ScreenPointToRay(position), out var hit,
                    interactionCamera.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;
            var view = hit.collider.GetComponentInParent<GridBlockView>();
            if (view == null || !view.isActiveAndEnabled || !board.Owns(view))
                return;
            if (gameplay != null && !board.Model.IsActive(view.BlockId))
                return;
            dragPlane = new Plane(Vector3.up, board.Coordinates.Origin);
            if (!ProjectPointer(position, out var point))
                return;
            pointer = pressedPointer;
            selected = view;
            previousPoint = point;
            grabOffset = point - board.Coordinates.CellToWorld(board.Model.GetBlock(view.BlockId).Anchor);
            view.SetSelected(true);
            SelectionStarted?.Invoke(view);
        }

        private bool ProjectPointer(Vector2 position, out Vector3 point)
        {
            var ray = interactionCamera.ScreenPointToRay(position);
            if (dragPlane.Raycast(ray, out var distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        private void EndDrag()
        {
            if (selected != null)
            {
                if (board != null && board.Model != null && board.Model.IsActive(selected.BlockId))
                    selected.SnapTo(board.Coordinates.CellToWorld(board.Model.GetBlock(selected.BlockId).Anchor));
                selected.SetSelected(false);
            }
            selected = null;
            pointer = null;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) EndDrag(); }
        private void OnApplicationPause(bool paused) { if (paused) EndDrag(); }

        private void OnDisable()
        {
            EndDrag();
            actions?.Map.Disable();
            actions = null;
            if (ownedActions != null)
                Destroy(ownedActions);
            ownedActions = null;
        }
    }
}
