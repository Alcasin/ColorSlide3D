using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    /// <summary>Explicit, create-once setup. Never runs on import or Editor startup.</summary>
    public static class M0Foundation
    {
        public const string RootPath = "Assets/_ColorSlide";
        public const string ScenePath = RootPath + "/Scenes/Gameplay.unity";
        public const string MaterialPath = RootPath + "/Materials/BoardPlaceholder.mat";
        public const string InputPath = RootPath + "/Input/ColorSlide.inputactions";
        public const string BoardName = "Board Placeholder";
        public const int ReferenceWidth = 1080;
        public const int ReferenceHeight = 1920;

        [MenuItem("ColorSlide/Setup/Apply M0 Foundation")]
        private static void ApplyFromMenu()
        {
            try
            {
                Apply();
                Debug.Log("ColorSlide M0: foundation applied and validated. Existing scene/material " +
                          "values were preserved. Open Gameplay and inspect a 1080 x 1920 Game View.");
            }
            catch (Exception exception)
            {
                Debug.LogError("ColorSlide M0 setup stopped: " + exception.Message);
            }
        }

        /// <summary>
        /// Checks all existing assets before writing anything. A valid existing scene is
        /// inspected in a preview scene and is never resaved or repaired automatically.
        /// </summary>
        public static void Apply()
        {
            RequireEditModeAndSavedScenes();
            ThrowOnErrors(M0Validation.InspectProject(requirePortrait: false));

            RequireAssetType<SceneAsset>(ScenePath);
            RequireAssetType<Material>(MaterialPath);
            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existingMaterial != null && !M0Validation.IsUrpMaterial(existingMaterial))
                throw new InvalidOperationException(MaterialPath + " is not a URP material. " +
                                                    "Resolve the material conflict manually; no values were reset.");
            if (existingMaterial != null && EditorUtility.IsDirty(existingMaterial))
                throw new InvalidOperationException("Save the edited board material before applying setup.");

            var sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (sceneExists)
                ThrowOnErrors(M0Validation.InspectGameplayScene());

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (existingMaterial == null && shader == null)
                throw new InvalidOperationException("URP/Lit shader was not found. Check the installed URP package.");
            if (sceneExists && existingMaterial == null)
                throw new InvalidOperationException("Gameplay already exists but the M0 material asset is missing. " +
                                                    "Resolve this conflict manually; the scene was preserved.");

            EnsureFolder(RootPath + "/Scenes");
            EnsureFolder(RootPath + "/Materials");
            if (!sceneExists)
            {
                var material = existingMaterial;
                if (material == null)
                {
                    material = new Material(shader) { name = "BoardPlaceholder" };
                    material.SetColor("_BaseColor", new Color(0.48f, 0.54f, 0.59f, 1f));
                    material.SetFloat("_Smoothness", 0.2f);
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }

                CreateGameplayScene(material);
            }

            // Supported PlayerSettings APIs only. No platform switch and no serialized hacks.
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            if (PlayerSettings.defaultScreenWidth != ReferenceWidth)
                PlayerSettings.defaultScreenWidth = ReferenceWidth;
            if (PlayerSettings.defaultScreenHeight != ReferenceHeight)
                PlayerSettings.defaultScreenHeight = ReferenceHeight;

            ThrowOnErrors(M0Validation.InspectProject());
            ThrowOnErrors(M0Validation.InspectGameplayScene());
        }

        [MenuItem("ColorSlide/Setup/Open Gameplay")]
        private static void OpenGameplay()
        {
            try
            {
                RequireEditModeAndSavedScenes();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("Run Apply M0 Foundation first.");
                // The guard above refuses unsaved scenes; open alone for unambiguous rendering QA.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError("ColorSlide M0 open stopped: " + exception.Message);
            }
        }

        public static void RequireEditModeAndSavedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for compilation and leave Play Mode before setup.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Leave Prefab Mode before setup.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                    throw new InvalidOperationException("Save or discard your edits to scene '" +
                                                        scene.name + "' before setup. No scene was changed.");
            }
        }

        private static void RequireAssetType<T>(string path) where T : UnityEngine.Object
        {
            if ((File.Exists(path) || Directory.Exists(path) ||
                 !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) &&
                AssetDatabase.LoadAssetAtPath<T>(path) == null)
                throw new InvalidOperationException("An unrecognized or unimported asset occupies " + path +
                                                    ". Resolve the conflict before setup; it was not overwritten.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Could not create asset folder: " + path);
        }

        private static void CreateGameplayScene(Material material)
        {
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);

                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 12f, -6f);
                cameraObject.transform.LookAt(Vector3.zero, Vector3.up);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 6.25f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.16f, 0.21f, 1f);
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

                var lightObject = new GameObject("Directional Light", typeof(Light));
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.shadows = LightShadows.Soft;
                lightObject.AddComponent<UniversalAdditionalLightData>();

                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = BoardName;
                // Temporary visual dimensions only: surface at Y=0, X/Z span the board.
                // These Inspector values define neither a grid nor a future level size.
                board.transform.position = new Vector3(0f, -0.125f, 0f);
                board.transform.localScale = new Vector3(6f, 0.25f, 8f);
                UnityEngine.Object.DestroyImmediate(board.GetComponent<Collider>());
                board.GetComponent<MeshRenderer>().sharedMaterial = material;

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unity could not save " + ScenePath);
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                // Only the scene created by this method is closed. User scenes stay loaded.
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ThrowOnErrors(System.Collections.Generic.List<string> errors)
        {
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));
        }
    }
}
