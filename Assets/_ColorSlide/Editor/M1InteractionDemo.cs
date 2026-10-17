using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    /// <summary>Create-once M1 scene from the accepted M0 scene. No startup/import hooks.</summary>
    public static class M1InteractionDemo
    {
        public const string ScenePath = M0Foundation.RootPath + "/Scenes/InteractionDemo.unity";
        private const string MaterialFolder = M0Foundation.RootPath + "/Materials/M1";
        private const int Columns = 6;
        private const int Rows = 8;
        private static readonly string[] MaterialNames = { "Blue", "Amber", "Purple", "Green", "TileA", "TileB", "Obstacle" };
        private static readonly Color[] Colors =
        {
            new Color(0.12f, 0.62f, 0.88f), new Color(0.97f, 0.61f, 0.13f),
            new Color(0.66f, 0.35f, 0.85f), new Color(0.15f, 0.73f, 0.40f),
            new Color(0.34f, 0.40f, 0.46f), new Color(0.41f, 0.47f, 0.53f), new Color(0.19f, 0.23f, 0.28f)
        };

        [MenuItem("ColorSlide/Setup/Apply M1 Interaction Demo")]
        private static void ApplyFromMenu()
        {
            try
            {
                Apply();
                Debug.Log("ColorSlide M1: InteractionDemo is ready. Existing authored assets were preserved. " +
                          "Use Open M1 Interaction Demo, then Play in a 9:16 Game View.");
            }
            catch (Exception exception)
            {
                Debug.LogError("ColorSlide M1 setup stopped: " + exception.Message);
            }
        }

        public static void Apply()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            ThrowOnErrors(M0Validation.InspectProject());
            ThrowOnErrors(M0Validation.InspectGameplayScene());
            RequireAssetType<SceneAsset>(ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                // No resave, rebuild, material reset or missing-object repair of an authored scene.
                ThrowOnErrors(M1Validation.InspectSavedScene());
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit is unavailable. Check the installed URP package.");
            foreach (var name in MaterialNames)
            {
                var path = MaterialFolder + "/" + name + ".mat";
                RequireAssetType<Material>(path);
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null && (!M0Validation.IsUrpMaterial(existing) || EditorUtility.IsDirty(existing)))
                    throw new InvalidOperationException("Save or resolve the existing M1 material before setup: " + path);
            }

            // Inspect the protected source before creating assets. Only M1 scale/origin are derived.
            var source = EditorSceneManager.OpenPreviewScene(M0Foundation.ScenePath);
            float size;
            Vector3 origin;
            try
            {
                var board = source.GetRootGameObjects().Single(obj => obj.name == M0Foundation.BoardName);
                if (Quaternion.Angle(board.transform.rotation, Quaternion.identity) > 0.01f)
                    throw new InvalidOperationException("The accepted board is rotated. Review M1 framing manually; M0 was not changed.");
                var bounds = board.GetComponent<MeshRenderer>().bounds;
                size = 0.9f * Mathf.Min(bounds.size.x / Columns, bounds.size.z / Rows);
                if (size <= 0.01f)
                    throw new InvalidOperationException("The accepted board is too small for the M1 demo.");
                origin = new Vector3(bounds.center.x - (Columns - 1) * size / 2f,
                    bounds.max.y + size * 0.04f, bounds.center.z - (Rows - 1) * size / 2f);
                var camera = source.GetRootGameObjects().SelectMany(obj => obj.GetComponentsInChildren<Camera>()).Single();
                var contentBounds = new Bounds(new Vector3(bounds.center.x, origin.y + size * 0.25f, bounds.center.z),
                    new Vector3(Columns * size + size * 0.2f, size * 0.5f, Rows * size + size * 0.2f));
                if (!M0Validation.FitsPortraitView(camera, contentBounds))
                    throw new InvalidOperationException("M1 content does not fit the accepted portrait camera. Review framing; no camera settings were changed.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(source);
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (File.Exists(MaterialFolder) || Directory.Exists(MaterialFolder))
                    throw new InvalidOperationException("Unimported or conflicting folder at " + MaterialFolder);
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(M0Foundation.RootPath + "/Materials", "M1")))
                    throw new IOException("Could not create the M1 materials folder.");
            }
            var materials = new Material[MaterialNames.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                var path = MaterialFolder + "/" + MaterialNames[i] + ".mat";
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (materials[i] != null)
                    continue;
                materials[i] = new Material(shader) { name = MaterialNames[i] };
                materials[i].SetColor("_BaseColor", Colors[i]);
                materials[i].SetFloat("_Smoothness", 0.22f);
                AssetDatabase.CreateAsset(materials[i], path);
            }

            var previous = SceneManager.GetActiveScene();
            var copied = false;
            var complete = false;
            try
            {
                copied = AssetDatabase.CopyAsset(M0Foundation.ScenePath, ScenePath);
                if (!copied)
                    throw new IOException("Could not copy the accepted Gameplay scene to " + ScenePath);
                // Opening a saved copy also works with a clean untitled Editor scene loaded.
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Build(scene, origin, size, materials);
                ThrowOnErrors(M1Validation.InspectScene(scene));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save " + ScenePath);
                complete = true;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                var scene = SceneManager.GetSceneByPath(ScenePath);
                if (copied && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                if (copied && !complete && !SceneManager.GetSceneByPath(ScenePath).isLoaded)
                    AssetDatabase.DeleteAsset(ScenePath); // Only a failed copy created in this call.
            }
        }

        private static void Build(Scene scene, Vector3 origin, float size, Material[] materials)
        {
            var root = new GameObject("M1 Interaction Board");
            root.transform.position = origin;
            var board = root.AddComponent<InteractionBoard>();
            var grid = new GameObject("Grid Surface").transform;
            grid.SetParent(root.transform, false);
            for (var x = 0; x < Columns; x++)
            for (var y = 0; y < Rows; y++)
                Cube("Cell " + x + "," + y, grid, new Vector3(x * size, -size * 0.015f, y * size),
                    new Vector3(size * 0.96f, size * 0.02f, size * 0.96f), materials[4 + (x + y) % 2], false);

            var blocked = new[] { new Vector2Int(2, 3), new Vector2Int(3, 3) };
            var obstacles = new GameObject("Static Obstacles and Boundary").transform;
            obstacles.SetParent(root.transform, false);
            foreach (var cell in blocked)
                Cube("Blocked " + cell.x + "," + cell.y, obstacles,
                    new Vector3(cell.x * size, size * 0.21f, cell.y * size),
                    new Vector3(size * 0.96f, size * 0.42f, size * 0.96f), materials[6], true);

            var width = Columns * size;
            var depth = Rows * size;
            var center = new Vector3((Columns - 1) * size / 2f, size * 0.15f, (Rows - 1) * size / 2f);
            var thickness = size * 0.08f;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                Cube("Boundary X " + sign, obstacles, center + Vector3.right * sign * (width + thickness) / 2f,
                    new Vector3(thickness, size * 0.3f, depth + 2f * thickness), materials[6], true);
                Cube("Boundary Z " + sign, obstacles, center + Vector3.forward * sign * (depth + thickness) / 2f,
                    new Vector3(width, size * 0.3f, thickness), materials[6], true);
            }

            var views = new[]
            {
                Block("Single", new Vector2Int(0, 0), new[] { Vector2Int.zero }, 0, root.transform, size, materials),
                Block("Horizontal", new Vector2Int(2, 1), new[] { Vector2Int.zero, Vector2Int.right }, 1, root.transform, size, materials),
                Block("Vertical", new Vector2Int(5, 1), new[] { Vector2Int.zero, Vector2Int.up }, 2, root.transform, size, materials),
                Block("Elbow", new Vector2Int(1, 5), new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up }, 3, root.transform, size, materials)
            };
            board.Configure(Columns, Rows, size, blocked, views);
            var camera = scene.GetRootGameObjects().SelectMany(obj => obj.GetComponentsInChildren<Camera>()).Single();
            root.AddComponent<PointerDragController>().Configure(board, camera,
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(M0Foundation.InputPath));
        }

        private static GridBlockView Block(string id, Vector2Int anchor, Vector2Int[] footprint,
            int color, Transform parent, float size, Material[] materials)
        {
            var root = new GameObject(id);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(anchor.x * size, 0f, anchor.y * size);
            var renderers = new List<MeshRenderer>();
            foreach (var cell in footprint)
                renderers.Add(Cube("Shape Cell " + cell.x + "," + cell.y, root.transform,
                    new Vector3(cell.x * size, size * 0.23f, cell.y * size),
                    new Vector3(size * 0.94f, size * 0.46f, size * 0.94f), materials[color], true));
            var view = root.AddComponent<GridBlockView>();
            view.Configure(id, anchor, footprint, Colors[color], renderers.ToArray());
            return view;
        }

        private static MeshRenderer Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            if (!collider)
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        [MenuItem("ColorSlide/Setup/Open M1 Interaction Demo")]
        private static void OpenFromMenu()
        {
            try
            {
                M0Foundation.RequireEditModeAndSavedScenes();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("Run Apply M1 Interaction Demo first.");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError("ColorSlide M1 open stopped: " + exception.Message);
            }
        }

        private static void RequireAssetType<T>(string path) where T : UnityEngine.Object
        {
            if ((File.Exists(path) || Directory.Exists(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) &&
                AssetDatabase.LoadAssetAtPath<T>(path) == null)
                throw new InvalidOperationException("Conflicting or unimported asset at " + path + ". It was not overwritten.");
        }

        private static void ThrowOnErrors(List<string> errors)
        {
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));
        }
    }
}
