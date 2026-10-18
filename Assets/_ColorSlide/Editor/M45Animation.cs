using System;
using System.IO;
using System.Linq;
using ColorSlide.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    public static class M45Animation
    {
        public const string ScenePath = M0Foundation.RootPath + "/Scenes/AnimatedCampaign.unity";
        [MenuItem("ColorSlide/Setup/Apply M4.5 Animation Polish")]
        private static void ApplyFromMenu()
        {
            try { Apply(); Debug.Log("ColorSlide M4.5 is ready. Open Animated Campaign and verify the exit burst in Play Mode."); }
            catch (Exception exception) { Debug.LogError("ColorSlide M4.5 setup stopped: " + exception.Message); }
        }
        public static void Apply()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            ThrowOnErrors(M0Validation.InspectProject());
            var existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existing != null) { ThrowOnErrors(M45Validation.InspectSavedScene()); return; }
            if (File.Exists(ScenePath) || Directory.Exists(ScenePath) || File.Exists(ScenePath + ".meta") ||
                !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath)))
                throw new InvalidOperationException("Conflicting or incomplete AnimatedCampaign asset preserved. Resolve it before setup.");
            ThrowOnErrors(M4Validation.InspectSavedScene());
            var previous = SceneManager.GetActiveScene(); var copied = false; var saved = false;
            try
            {
                copied = AssetDatabase.CopyAsset(M4Polish.ScenePath, ScenePath);
                if (!copied) throw new IOException("Could not copy the accepted PolishedCampaign scene.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var presentation = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelPresentation>(true)).Single();
                presentation.ConfigureSoftCubeBursts(true);
                ThrowOnErrors(M45Validation.InspectScene(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save AnimatedCampaign.");
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
        private static void ThrowOnErrors(System.Collections.Generic.List<string> errors)
        { if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors)); }
        [MenuItem("ColorSlide/Setup/Open M4.5 Animated Campaign")]
        private static void OpenFromMenu()
        {
            try
            {
                M0Foundation.RequireEditModeAndSavedScenes();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new InvalidOperationException("Run Apply M4.5 Animation Polish first.");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception) { Debug.LogError("ColorSlide M4.5 open stopped: " + exception.Message); }
        }
    }
}
