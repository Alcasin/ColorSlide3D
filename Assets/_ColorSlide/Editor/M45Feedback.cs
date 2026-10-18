using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorSlide.Editor
{
    public static class M45Feedback
    {
        public const string ScenePath = M0Foundation.RootPath + "/Scenes/FeedbackCampaign.unity";
        [MenuItem("ColorSlide/Setup/Apply M4.5 Feedback Polish")]
        private static void ApplyFromMenu()
        {
            try { Apply(); Debug.Log("ColorSlide Phase B is ready. Open Feedback Campaign for Play Mode QA."); }
            catch (Exception exception) { Debug.LogError("ColorSlide feedback setup stopped: " + exception.Message); }
        }
        public static void Apply()
        {
            RequireSafeEditor(); ThrowOnErrors(M0Validation.InspectProject());
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { ThrowOnErrors(M45FeedbackValidation.InspectSavedScene()); return; }
            if (File.Exists(ScenePath) || Directory.Exists(ScenePath) || File.Exists(ScenePath + ".meta") ||
                !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath)))
                throw new InvalidOperationException("Conflicting or incomplete FeedbackCampaign preserved. Resolve it before setup.");
            ThrowOnErrors(M45Validation.InspectSavedScene());
            var previous = SceneManager.GetActiveScene(); var copied = false; var saved = false;
            try
            {
                copied = AssetDatabase.CopyAsset(M45Animation.ScenePath, ScenePath);
                if (!copied) throw new IOException("Could not copy the accepted AnimatedCampaign.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                ConfigureScene(scene); ThrowOnErrors(M45FeedbackValidation.InspectScene(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save FeedbackCampaign.");
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
        // Configure only a caller-owned copy, without rebuilding the approved HUD or resetting its styling.
        public static void ConfigureScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var presentation = roots.SelectMany(root => root.GetComponentsInChildren<LevelPresentation>(true)).Single();
            var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();
            var fields = new SerializedObject(hud);
            var timer = (Text)fields.FindProperty("timeText").objectReferenceValue;
            var pill = timer.transform.parent.GetComponent<RoundedPanel>();
            var reveal = hud.GetComponentsInChildren<ResultReveal>(true).Single();
            if (pill == null) throw new InvalidOperationException("The actual timer must retain its accepted RoundedPanel parent.");
            presentation.ConfigureGameplayFeedback(true);
            var urgency = hud.GetComponent<TimerUrgency>();
            if (urgency == null) urgency = hud.gameObject.AddComponent<TimerUrgency>();
            urgency.Configure(hud, pill, reveal);
        }
        private static void ThrowOnErrors(List<string> errors)
        { if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors)); }
        private static void RequireSafeEditor()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save untitled scenes before feedback setup/open. No assets were changed.");
        }
        [MenuItem("ColorSlide/Setup/Open M4.5 Feedback Campaign")]
        private static void OpenFromMenu()
        {
            try
            {
                RequireSafeEditor();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new InvalidOperationException("Run Apply M4.5 Feedback Polish first.");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception) { Debug.LogError("ColorSlide feedback open stopped: " + exception.Message); }
        }
    }
}
