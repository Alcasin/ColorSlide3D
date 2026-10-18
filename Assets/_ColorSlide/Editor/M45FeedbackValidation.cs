using System;
using System.Collections.Generic;
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
    public static class M45FeedbackValidation
    {
        public static List<string> InspectSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return new List<string> { "Leave Play Mode before feedback validation." };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M45Feedback.ScenePath) == null) return new List<string> { "Run Apply M4.5 Feedback Polish first." };
            var scene = default(Scene);
            try { scene = EditorSceneManager.OpenPreviewScene(M45Feedback.ScenePath); return InspectScene(scene); }
            catch (Exception exception) { return new List<string> { "Cannot inspect feedback: " + exception.Message }; }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static List<string> InspectScene(Scene scene)
        {
            var errors = M45Validation.InspectScene(scene); var roots = scene.GetRootGameObjects();
            var presentation = roots.SelectMany(root => root.GetComponentsInChildren<LevelPresentation>(true)).SingleOrDefault();
            var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).SingleOrDefault();
            var effects = roots.SelectMany(root => root.GetComponentsInChildren<TimerUrgency>(true)).ToArray();
            if (presentation == null || !presentation.GameplayFeedback || hud == null || effects.Length != 1)
            { errors.Add("Phase B needs one optional feedback presentation and one timer/Win lifecycle owner on the actual HUD."); return errors; }
            var effect = effects[0]; var fields = new SerializedObject(hud);
            var timer = fields.FindProperty("timeText").objectReferenceValue as Text;
            var result = fields.FindProperty("resultPanel").objectReferenceValue as GameObject;
            if (!effect.isActiveAndEnabled || effect.gameObject != hud.gameObject || effect.Hud != hud || timer == null ||
                effect.Pill == null || timer.transform.parent != effect.Pill.transform ||
                effect.Reveal == null || effect.Reveal.FeedbackHud != hud || result == null || !effect.Reveal.transform.IsChildOf(result.transform) ||
                float.IsNaN(effect.Threshold) || float.IsInfinity(effect.Threshold) || effect.Threshold <= 0f)
                errors.Add("Phase B must reference the existing timer pill, result reveal and authoritative HUD, with a positive finite warning threshold.");
            if (roots.Any(root => root.GetComponentsInChildren<GateGlow>(true).Length != 0 || root.GetComponentsInChildren<SoftSparkle>(true).Length != 0))
                errors.Add("Gate glow and Win sparkles are runtime-owned; remove authored duplicate effects.");
            return errors;
        }
        [MenuItem("ColorSlide/Setup/Validate M4.5 Feedback")]
        private static void ValidateFromMenu()
        {
            var errors = M0Validation.InspectProject(); errors.AddRange(InspectSavedScene());
            if (errors.Count == 0) Debug.Log("ColorSlide Phase B references/configuration are valid. Native tests, rendering and input QA remain manual.");
            else Debug.LogError("ColorSlide feedback validation:\n- " + string.Join("\n- ", errors));
        }
    }
}
