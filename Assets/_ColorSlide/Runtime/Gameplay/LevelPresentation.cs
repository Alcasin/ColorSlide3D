using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using ColorSlide.Polish;

namespace ColorSlide.Gameplay
{
    /// <summary>Reconstructs M2 graybox geometry from level data using the existing views and controllers.</summary>
    public sealed class LevelPresentation : MonoBehaviour
    {
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private InputActionAsset pointerActions;
        // Same accepted M1 materials, in this order: Blue, Amber, Purple, Green, TileA, TileB, Obstacle.
        [SerializeField] private Material[] materials;
        [SerializeField] private SoftToyTheme theme;
        [SerializeField] private bool softCubeBursts;
        [SerializeField] private bool gameplayFeedback;
        public bool GameplayFeedback => gameplayFeedback;
        public void ConfigureGameplayFeedback(bool value)
        {
            if (value && (!softCubeBursts || theme == null)) throw new InvalidOperationException("Feedback requires the themed M4.5 burst presentation.");
            gameplayFeedback = value;
        }
        public SoftToyTheme Theme => theme;
        public bool SoftCubeBursts => softCubeBursts;
        public void ConfigureSoftCubeBursts(bool value) { if (value && theme == null) throw new InvalidOperationException("M4.5 requires an M4 theme."); softCubeBursts = value; }
        public void ConfigureTheme(SoftToyTheme value) { value.Validate(); theme = value; materials = value.Palette.ToArray(); }
        public Camera InteractionCamera => interactionCamera;
        public InputActionAsset PointerActions => pointerActions;
        public IReadOnlyList<Material> Materials => materials;

        public void Configure(Camera camera, InputActionAsset input, Material[] palette)
        { interactionCamera = camera; pointerActions = input; materials = (Material[])palette.Clone(); }

        public void ValidateResources()
        {
            if (theme != null) theme.Validate();
            if (softCubeBursts && theme == null) throw new InvalidOperationException("M4.5 requires the accepted M4 theme.");
            if (gameplayFeedback && !softCubeBursts) throw new InvalidOperationException("Feedback requires M4.5 bursts.");
            if (interactionCamera == null || pointerActions == null || materials == null || materials.Length != 7 ||
                materials.Any(material => material == null) || transform.lossyScale != Vector3.one ||
                Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f)
                throw new InvalidOperationException("Campaign presentation needs its camera, pointer asset, seven materials and unscaled XZ root.");
        }

        public GameplayController Build(LevelData level, PuzzleSession session)
        {
            ValidateResources();
            var model = session.Board;
            var size = level.CellSize;
            var root = new GameObject("Level " + level.Id);
            root.SetActive(false); // No lifecycle/input starts before every reference and the owned session are ready.
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-(model.Width - 1) * size / 2f, size * 0.04f,
                -(model.Height - 1) * size / 2f);
            try
            {
                var board = root.AddComponent<InteractionBoard>();
                var surface = new GameObject("Grid Surface").transform;
                surface.SetParent(root.transform, false);
                for (var x = 0; x < model.Width; x++)
                for (var y = 0; y < model.Height; y++)
                    Cube("Cell " + x + "," + y, surface, new Vector3(x * size, -size * 0.015f, y * size),
                        new Vector3(size * 0.96f, size * 0.02f, size * 0.96f), materials[4 + (x + y) % 2], false);
                foreach (var cell in model.BlockedCells)
                    Cube("Obstacle " + cell, root.transform, new Vector3(cell.x * size, size * 0.21f, cell.y * size),
                        new Vector3(size * 0.96f, size * 0.42f, size * 0.96f), materials[6], true);
                var boundary = new GameObject("Boundary").transform;
                boundary.SetParent(root.transform, false);
                foreach (BoardEdge edge in Enum.GetValues(typeof(BoardEdge)))
                {
                    var horizontal = ExitGate.IsHorizontalEdge(edge);
                    var length = horizontal ? model.Width : model.Height;
                    for (var index = 0; index < length; index++)
                    {
                        if (model.Gates.Any(gate => gate.Edge == edge && gate.Contains(index))) continue;
                        Cube("Solid " + edge + " " + index, boundary, BoundaryPosition(model, size, edge, index),
                            horizontal ? new Vector3(size, size * 0.3f, size * 0.08f) : new Vector3(size * 0.08f, size * 0.3f, size), materials[6], false);
                    }
                }
                var gates = new List<ExitGateView>();
                foreach (var gate in model.Gates)
                {
                    var obj = new GameObject(gate.Id);
                    obj.transform.SetParent(root.transform, false);
                    obj.transform.localPosition = BoundaryPosition(model, size, gate.Edge, gate.Start + (gate.Span - 1) / 2f);
                    var horizontal = ExitGate.IsHorizontalEdge(gate.Edge);
                    var material = ColorMaterial(gate.ColorId);
                    Cube("Gate Floor", obj.transform, new Vector3(0f, -size * 0.165f, 0f),
                        horizontal ? new Vector3(gate.Span * size, size * 0.03f, size * 0.35f) :
                        new Vector3(size * 0.35f, size * 0.03f, gate.Span * size), material, false);
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        var along = (gate.Span * size + size * 0.08f) / 2f * sign;
                        Cube("Jamb " + sign, obj.transform, horizontal ? new Vector3(along, size * 0.2f, 0f) : new Vector3(0f, size * 0.2f, along),
                            new Vector3(size * 0.08f, size * 0.7f, size * 0.08f), material, false);
                    }
                    var view = obj.AddComponent<ExitGateView>(); view.Configure(gate); gates.Add(view);
                }
                var views = new List<GridBlockView>();
                foreach (var block in model.Blocks)
                {
                    var obj = new GameObject(block.Id);
                    obj.transform.SetParent(root.transform, false);
                    obj.transform.localPosition = new Vector3(block.Anchor.x * size, 0f, block.Anchor.y * size);
                    var renderers = new List<MeshRenderer>();
                    var material = ColorMaterial(block.ColorId);
                    foreach (var cell in block.Cells)
                        renderers.Add(Cube("Shape Cell " + cell, obj.transform, new Vector3(cell.x * size, size * 0.23f, cell.y * size),
                            new Vector3(size * 0.94f, size * 0.46f, size * 0.94f), material, true, true));
                    var view = obj.AddComponent<GridBlockView>();
                    view.Configure(block.Id, block.Anchor, block.Cells.ToArray(), theme != null ? SoftToyTheme.SelectionSrgb(material) :
                        material.GetColor("_BaseColor"), renderers.ToArray(), block.ColorId);
                    if (theme != null)
                    foreach (var cell in block.Cells)
                    {
                        var shadow = new GameObject("Contact Shadow", typeof(MeshFilter), typeof(MeshRenderer));
                        shadow.transform.SetParent(obj.transform, false);
                        shadow.transform.localPosition = new Vector3(cell.x * size, -0.002f * size, cell.y * size);
                        shadow.transform.localScale = Vector3.one * size;
                        shadow.GetComponent<MeshFilter>().sharedMesh = theme.ContactShadow;
                        var shadowRenderer = shadow.GetComponent<MeshRenderer>(); shadowRenderer.sharedMaterial = theme.Shadow;
                        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        shadowRenderer.receiveShadows = false;
                    }
                    views.Add(view);
                }
                board.Configure(model.Width, model.Height, size, model.BlockedCells.ToArray(), views.ToArray());
                var drag = root.AddComponent<PointerDragController>();
                drag.Configure(board, interactionCamera, pointerActions);
                var gameplay = root.AddComponent<GameplayController>();
                gameplay.Configure(board, drag, gates.ToArray());
                drag.ConfigureGameplay(gameplay);
                gameplay.InitializeSession(session);
                if (softCubeBursts)
                {
                    var burst = root.AddComponent<SoftCubeBurst>(); burst.Configure(theme, size); gameplay.ConfigureExitBurst(burst);
                }
                if (theme != null)
                    foreach (var view in views) view.gameObject.AddComponent<PolishedBlockVisual>().Configure(view, gameplay, size, gameplayFeedback);
                if (gameplayFeedback) root.AddComponent<GateGlow>().Configure(gameplay);
                return gameplay; // Caller retires the old hierarchy before activating this one.
            }
            catch { Retire(root); throw; }
        }

        public static void Retire(GameObject root)
        {
            if (root == null) return;
            root.SetActive(false); // Synchronously cancels drag, disables input clone and completes any old exit.
            root.transform.SetParent(null, true);
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }

        private Material ColorMaterial(BlockColor color) => materials[(int)color - 1];
        private static Vector3 BoundaryPosition(GridBoard board, float size, BoardEdge edge, float index)
        {
            switch (edge)
            {
                case BoardEdge.North: return new Vector3(index * size, size * 0.15f, (board.Height - 0.5f) * size);
                case BoardEdge.South: return new Vector3(index * size, size * 0.15f, -size * 0.5f);
                case BoardEdge.East: return new Vector3((board.Width - 0.5f) * size, size * 0.15f, index * size);
                default: return new Vector3(-size * 0.5f, size * 0.15f, index * size);
            }
        }
        private MeshRenderer Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool blockCell = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            if (!collider)
            {
                var component = obj.GetComponent<Collider>(); component.enabled = false;
                if (Application.isPlaying) Destroy(component); else DestroyImmediate(component);
            }
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            if (theme == null) return renderer;
            if (!blockCell) { obj.GetComponent<MeshFilter>().sharedMesh = theme.RoundedBox; return renderer; }
            renderer.enabled = false; // Primitive transform and BoxCollider retain the accepted dimensions.
            var visual = new GameObject("Soft Visual", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(obj.transform, false);
            if (blockCell) visual.transform.localScale = new Vector3(0.98f / 0.94f, 1f, 0.98f / 0.94f);
            visual.GetComponent<MeshFilter>().sharedMesh = theme.RoundedBox;
            var softRenderer = visual.GetComponent<MeshRenderer>(); softRenderer.sharedMaterial = material;
            return softRenderer;
        }
    }
}
