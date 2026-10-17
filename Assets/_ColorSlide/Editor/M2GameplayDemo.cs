using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorSlide.Editor
{
    public static class M2GameplayDemo
    {
        public const string ScenePath = M0Foundation.RootPath + "/Scenes/GameplayDemo.unity";
        public const string UiActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string MaterialsPath = M0Foundation.RootPath + "/Materials/M1/";
        private static readonly string[] UiActions = { "Point", "Click", "Navigate", "Submit", "Cancel", "ScrollWheel", "RightClick", "MiddleClick" };

        [MenuItem("ColorSlide/Setup/Apply M2 Gameplay Demo")]
        private static void ApplyFromMenu()
        {
            try { Apply(); Debug.Log("ColorSlide M2: GameplayDemo is ready. Open M2 Gameplay Demo, then Play in 9:16."); }
            catch (Exception exception) { Debug.LogError("ColorSlide M2 setup stopped: " + exception.Message); }
        }

        public static void Apply()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            ThrowOnErrors(M0Validation.InspectProject());
            ThrowOnErrors(M1Validation.InspectSavedScene());
            if ((File.Exists(ScenePath) || Directory.Exists(ScenePath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath))) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Conflicting or unimported asset at " + ScenePath + ". It was preserved.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                ThrowOnErrors(M2Validation.InspectSavedScene());
                return; // A valid authored scene is never rebuilt or resaved.
            }

            var materials = new Dictionary<string, Material>();
            foreach (var name in new[] { "Blue", "Amber", "Green", "Purple", "TileA", "TileB", "Obstacle" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + name + ".mat");
                if (!M0Validation.IsUrpMaterial(material) || EditorUtility.IsDirty(material))
                    throw new InvalidOperationException("Save or resolve the existing M1 material: " + name);
                materials.Add(name, material); // Reuse accepted URP materials without changing them.
            }
            var uiAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsPath);
            if (uiAsset == null)
                throw new InvalidOperationException("Missing template UI Input Actions asset: " + UiActionsPath);
            var references = UiActions.ToDictionary(name => name, name => FindUiReference(uiAsset, name));
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                throw new InvalidOperationException("Unity's built-in LegacyRuntime font is unavailable.");

            var source = EditorSceneManager.OpenPreviewScene(M1InteractionDemo.ScenePath);
            Vector3 center;
            float surfaceY;
            try
            {
                var roots = source.GetRootGameObjects();
                var oldBoard = roots.SelectMany(root => root.GetComponentsInChildren<InteractionBoard>()).Single();
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                if (camera.transform.IsChildOf(oldBoard.transform))
                    throw new InvalidOperationException("The accepted camera belongs to the M1 board hierarchy; review the copy workflow manually.");
                var slab = roots.Single(root => root.name == M0Foundation.BoardName).GetComponent<MeshRenderer>();
                center = slab.bounds.center;
                surfaceY = slab.bounds.max.y;
                // Includes the entire two-cell piece after full departure, not just initial geometry.
                var exitBounds = new Bounds(new Vector3(center.x, surfaceY + 0.35f, center.z),
                    new Vector3(10f * M2DemoLayout.CellSize, 0.7f, 10f * M2DemoLayout.CellSize));
                if (!M0Validation.FitsPortraitView(camera, exitBounds))
                    throw new InvalidOperationException("M2 exit paths do not fit the accepted portrait camera. No camera settings were changed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(source); }

            var previous = SceneManager.GetActiveScene();
            var copied = false;
            var saved = false;
            try
            {
                copied = AssetDatabase.CopyAsset(M1InteractionDemo.ScenePath, ScenePath);
                if (!copied) throw new IOException("Could not copy InteractionDemo to GameplayDemo.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var oldBoard = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InteractionBoard>()).Single();
                UnityEngine.Object.DestroyImmediate(oldBoard.gameObject); // Only the newly owned scene copy.
                Build(scene, center, surfaceY, materials, uiAsset, references, font);
                ThrowOnErrors(M2Validation.InspectScene(scene));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                saved = true;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                var scene = SceneManager.GetSceneByPath(ScenePath);
                if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (copied && !saved && (!scene.IsValid() || !scene.isLoaded)) AssetDatabase.DeleteAsset(ScenePath);
            }
        }

        private static InputActionReference FindUiReference(InputActionAsset asset, string name)
        {
            var action = asset.FindAction("UI/" + name, throwIfNotFound: true);
            var reference = AssetDatabase.LoadAllAssetsAtPath(UiActionsPath).OfType<InputActionReference>()
                .SingleOrDefault(candidate => !candidate.hideFlags.HasFlag(HideFlags.HideInHierarchy) && candidate.action == action);
            if (reference == null || !AssetDatabase.Contains(reference) || !AssetDatabase.IsSubAsset(reference) ||
                reference.asset != asset)
                throw new InvalidOperationException("UI/" + name + " needs its imported persistent InputActionReference.");
            return reference;
        }

        private static void Build(Scene scene, Vector3 center, float surfaceY, Dictionary<string, Material> materials,
            InputActionAsset uiAsset, Dictionary<string, InputActionReference> references, Font font)
        {
            var model = M2DemoLayout.CreateBoard();
            var gates = M2DemoLayout.CreateGates();
            model.ConfigureGates(gates);
            var size = M2DemoLayout.CellSize;
            var root = new GameObject("M2 Gameplay Board");
            root.transform.position = new Vector3(center.x - (model.Width - 1) * size / 2f,
                surfaceY + size * 0.04f, center.z - (model.Height - 1) * size / 2f);
            var board = root.AddComponent<InteractionBoard>();
            var surface = new GameObject("Grid Surface").transform;
            surface.SetParent(root.transform, false);
            for (var x = 0; x < model.Width; x++)
            for (var y = 0; y < model.Height; y++)
                Cube("Cell " + x + "," + y, surface, new Vector3(x * size, -size * 0.015f, y * size),
                    new Vector3(size * 0.96f, size * 0.02f, size * 0.96f), materials[(x + y) % 2 == 0 ? "TileA" : "TileB"], false);
            foreach (var cell in model.BlockedCells)
                Cube("Obstacle " + cell.x + "," + cell.y, root.transform,
                    new Vector3(cell.x * size, size * 0.21f, cell.y * size),
                    new Vector3(size * 0.96f, size * 0.42f, size * 0.96f), materials["Obstacle"], true);

            var boundary = new GameObject("M2 Boundary").transform;
            boundary.SetParent(root.transform, false);
            foreach (BoardEdge edge in Enum.GetValues(typeof(BoardEdge)))
            {
                var horizontal = ExitGate.IsHorizontalEdge(edge);
                var length = horizontal ? model.Width : model.Height;
                for (var index = 0; index < length; index++)
                {
                    if (gates.Any(gate => gate.Edge == edge && gate.Contains(index))) continue;
                    Cube("Solid " + edge + " " + index, boundary, BoundaryPosition(model.Width, model.Height, size, edge, index),
                        horizontal ? new Vector3(size, size * 0.3f, size * 0.08f) : new Vector3(size * 0.08f, size * 0.3f, size),
                        materials["Obstacle"], false);
                }
            }
            var gateViews = new List<ExitGateView>();
            foreach (var gate in gates)
            {
                var gateObject = new GameObject(gate.Id);
                gateObject.transform.SetParent(root.transform, false);
                gateObject.transform.localPosition = BoundaryPosition(model.Width, model.Height, size, gate.Edge,
                    gate.Start + (gate.Span - 1) / 2f);
                var horizontal = ExitGate.IsHorizontalEdge(gate.Edge);
                var material = materials[gate.ColorId.ToString()];
                Cube("Gate Floor", gateObject.transform, new Vector3(0f, -size * 0.165f, 0f),
                    horizontal ? new Vector3(gate.Span * size, size * 0.03f, size * 0.35f) :
                    new Vector3(size * 0.35f, size * 0.03f, gate.Span * size), material, false);
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var along = (gate.Span * size + size * 0.08f) / 2f * sign;
                    Cube("Jamb " + sign, gateObject.transform,
                        horizontal ? new Vector3(along, size * 0.2f, 0f) : new Vector3(0f, size * 0.2f, along),
                        new Vector3(size * 0.08f, size * 0.7f, size * 0.08f), material, false);
                }
                var view = gateObject.AddComponent<ExitGateView>();
                view.Configure(gate);
                gateViews.Add(view);
            }

            var views = new List<GridBlockView>();
            foreach (var block in model.Blocks)
            {
                var blockObject = new GameObject(block.Id);
                blockObject.transform.SetParent(root.transform, false);
                blockObject.transform.localPosition = new Vector3(block.Anchor.x * size, 0f, block.Anchor.y * size);
                var material = materials[block.ColorId.ToString()];
                var renderers = new List<MeshRenderer>();
                foreach (var cell in block.Cells)
                    renderers.Add(Cube("Shape Cell " + cell.x + "," + cell.y, blockObject.transform,
                        new Vector3(cell.x * size, size * 0.23f, cell.y * size),
                        new Vector3(size * 0.94f, size * 0.46f, size * 0.94f), material, true));
                var view = blockObject.AddComponent<GridBlockView>();
                view.Configure(block.Id, block.Anchor, block.Cells.ToArray(), material.GetColor("_BaseColor"), renderers.ToArray(), block.ColorId);
                views.Add(view);
            }
            board.Configure(model.Width, model.Height, size, model.BlockedCells.ToArray(), views.ToArray());
            var camera = scene.GetRootGameObjects().SelectMany(obj => obj.GetComponentsInChildren<Camera>()).Single();
            var drag = root.AddComponent<PointerDragController>();
            drag.Configure(board, camera, AssetDatabase.LoadAssetAtPath<InputActionAsset>(M0Foundation.InputPath));
            var gameplay = root.AddComponent<GameplayController>();
            gameplay.Configure(board, drag, gateViews.ToArray());
            drag.ConfigureGameplay(gameplay);
            BuildHud(gameplay, font);

            var eventObject = new GameObject("M2 EventSystem");
            eventObject.SetActive(false);
            eventObject.AddComponent<EventSystem>();
            var module = eventObject.AddComponent<InputSystemUIInputModule>();
            // Reset assigns package defaults even on an inactive object. Changing assets remaps
            // those to transient wrappers, and same-action setters then ignore imported replacements.
            // Clear the old asset first so all slots (including tracked-device defaults) become null.
            module.actionsAsset = null;
            module.actionsAsset = uiAsset;
            module.point = references["Point"];
            module.leftClick = references["Click"];
            module.move = references["Navigate"];
            module.submit = references["Submit"];
            module.cancel = references["Cancel"];
            module.scrollWheel = references["ScrollWheel"];
            module.rightClick = references["RightClick"];
            module.middleClick = references["MiddleClick"];
            eventObject.SetActive(true);
        }

        public static Vector3 BoundaryPosition(int width, int height, float size, BoardEdge edge, float index)
        {
            switch (edge)
            {
                case BoardEdge.North: return new Vector3(index * size, size * 0.15f, (height - 0.5f) * size);
                case BoardEdge.South: return new Vector3(index * size, size * 0.15f, -0.5f * size);
                case BoardEdge.East: return new Vector3((width - 0.5f) * size, size * 0.15f, index * size);
                default: return new Vector3(-0.5f * size, size * 0.15f, index * size);
            }
        }

        private static MeshRenderer Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            if (!collider) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static Text Label(string name, Transform parent, Font font, string value, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Rect(name, parent, anchor, anchor, anchor, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = fontSize;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
        private static Button Button(string name, Transform parent, Font font, Vector2 anchor, Vector2 position)
        {
            var rect = Rect(name, parent, anchor, anchor, anchor, position, new Vector2(240f, 88f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.36f, 0.50f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label("Label", rect, font, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 80f), 36);
            return button;
        }
        private static void BuildHud(GameplayController gameplay, Font font)
        {
            var root = new GameObject("M2 HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); scaler.matchWidthOrHeight = 0.5f;
            var top = Rect("Top HUD", root.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 180f));
            top.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.94f);
            var timer = Label("Time", top, font, "Time 3:00", new Vector2(0f, 1f), new Vector2(28f, -16f), new Vector2(430f, 72f), 40);
            var count = Label("Remaining", top, font, "Blocks 4", new Vector2(0f, 0f), new Vector2(28f, 16f), new Vector2(430f, 72f), 36);
            var restart = Button("Restart", top, font, new Vector2(1f, 0.5f), new Vector2(-28f, 0f));
            var result = Rect("Result Panel", root.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            result.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.88f);
            var message = Label("Result", result, font, "", new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(900f, 160f), 68);
            var retry = Button("Retry", result, font, new Vector2(0.5f, 0.45f), Vector2.zero);
            result.gameObject.SetActive(false);
            root.AddComponent<GameplayHud>().Configure(gameplay, timer, count, restart, result.gameObject, message, retry);
        }

        [MenuItem("ColorSlide/Setup/Open M2 Gameplay Demo")]
        private static void OpenFromMenu()
        {
            try
            {
                M0Foundation.RequireEditModeAndSavedScenes();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("Run Apply M2 Gameplay Demo first.");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception) { Debug.LogError("ColorSlide M2 open stopped: " + exception.Message); }
        }
        private static void ThrowOnErrors(List<string> errors)
        {
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }
    }
}
