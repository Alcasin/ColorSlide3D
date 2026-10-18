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
    public static class M4Validation
    {
        public static List<string> InspectTheme(SoftToyTheme theme)
        {
            var errors = new List<string>();
            try
            {
                if (theme == null || !AssetDatabase.Contains(theme)) throw new InvalidOperationException("Save a persistent M4 theme asset.");
                theme.Validate();
                var dependencies = new UnityEngine.Object[] { theme, theme.RoundedBox, theme.ContactShadow, theme.Tray, theme.Shadow }
                    .Concat(theme.Palette.Cast<UnityEngine.Object>()).Concat(Enumerable.Range(1, 6).Select(index => (UnityEngine.Object)theme.Clip((SoundCue)index)));
                foreach (var asset in dependencies)
                    if (!AssetDatabase.Contains(asset) || EditorUtility.IsDirty(asset)) errors.Add("Save the persistent M4 resource: " + asset.name);
                foreach (var material in theme.Palette.Concat(new[] { theme.Tray, theme.Shadow }))
                    if (!M0Validation.IsUrpMaterial(material) || ShaderUtil.ShaderHasError(material.shader))
                        errors.Add("M4 resource needs a compiling URP shader: " + material.name);
                foreach (var material in theme.Palette.Concat(new[] { theme.Tray }))
                    if (!material.HasProperty("_BaseColor") || material.GetFloat("_Surface") != 0f || material.GetFloat("_Metallic") > 0.1f)
                        errors.Add("M4 surface must be opaque soft plastic: " + material.name);
                InspectMesh(theme.RoundedBox); InspectMesh(theme.ContactShadow);
                var bounds = theme.RoundedBox.bounds;
                if ((bounds.center - Vector3.zero).sqrMagnitude > 0.00001f || (bounds.size - Vector3.one).sqrMagnitude > 0.00001f)
                    errors.Add("Rounded box must remain centered inside its unit bounds.");
                for (var index = 1; index <= 6; index++)
                {
                    var clip = theme.Clip((SoundCue)index);
                    if (clip.length <= 0f || clip.length > 2f || clip.channels != 1 || clip.frequency < 22050)
                        errors.Add("M4 effect must be a short imported mono clip: " + clip.name);
                    M4SoundDesign.ValidateWav(AssetDatabase.GetAssetPath(clip));
                }
            }
            catch (Exception exception) { errors.Add("Invalid M4 theme: " + exception.Message); }
            return errors;
        }
        private static void InspectMesh(Mesh mesh)
        {
            if (mesh.vertexCount > 5000) throw new InvalidOperationException("M4 shared mesh exceeds the small mobile budget: " + mesh.name);
            new SoftMeshData(mesh.vertices, mesh.normals, mesh.uv, mesh.colors, mesh.triangles).Validate();
        }
        public static List<string> InspectSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return new List<string> { "Leave Play Mode before M4 validation." };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M4Polish.ScenePath) == null) return new List<string> { "Run Apply M4 Polish first." };
            var scene = default(Scene);
            try { scene = EditorSceneManager.OpenPreviewScene(M4Polish.ScenePath); return InspectScene(scene); }
            catch (Exception exception) { return new List<string> { "Cannot inspect M4: " + exception.Message }; }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static List<string> InspectScene(Scene scene)
        {
            var errors = M3Validation.InspectScene(scene); // Retains the imported UI-reference and level-data guards.
            var roots = scene.GetRootGameObjects();
            var presentations = roots.SelectMany(root => root.GetComponentsInChildren<LevelPresentation>(true)).ToArray();
            var feedback = roots.SelectMany(root => root.GetComponentsInChildren<PolishFeedback>(true)).ToArray();
            var sources = roots.SelectMany(root => root.GetComponentsInChildren<AudioSource>(true)).ToArray();
            var listeners = roots.SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).ToArray();
            if (presentations.Length != 1 || feedback.Length != 1 || sources.Length != 1 || listeners.Length != 1)
            { errors.Add("M4 needs one themed presentation, feedback component, AudioSource and original AudioListener."); return errors; }
            var view = presentations[0]; var theme = view.Theme;
            errors.AddRange(InspectTheme(theme));
            if (!feedback[0].HasReferences || feedback[0].Theme != theme || feedback[0].Source != sources[0] ||
                feedback[0].Campaign.Presentation != view || !feedback[0].isActiveAndEnabled)
                errors.Add("M4 feedback must reference its campaign, theme, source and three distinct HUD buttons.");
            if (!sources[0].isActiveAndEnabled || !listeners[0].isActiveAndEnabled || sources[0].playOnAwake || sources[0].loop ||
                sources[0].spatialBlend != 0f || sources[0].volume <= 0f || sources[0].volume > 0.65f)
                errors.Add("M4 audio must be quiet, nonlooping, manual and nonspatial.");
            var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).SingleOrDefault();
            if (hud == null || hud.GetComponent<PolishedHudEffects>() == null || !hud.GetComponent<PolishedHudEffects>().HasReferences ||
                hud.GetComponentsInChildren<SafeAreaPanel>(true).Length != 2 || hud.GetComponentsInChildren<ResultReveal>(true).Length != 1 ||
                hud.GetComponentsInChildren<RoundedPanel>(true).Length < 7)
                errors.Add("M4 HUD needs its rounded panels, two safe-area containers and one result reveal.");
            if (hud != null)
            {
                foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<CanvasRenderer>() == null)
                        errors.Add("M4 graphic needs a CanvasRenderer: " + graphic.name + ". Run Apply M4 Polish to repair the saved HUD.");
                var hudFields = new SerializedObject(hud);
                var result = hudFields.FindProperty("resultPanel").objectReferenceValue as GameObject;
                foreach (var panel in hud.GetComponentsInChildren<RoundedPanel>(true))
                    if (panel.gameObject != result && panel.GetComponent<Button>() == null && panel.raycastTarget)
                        errors.Add("Decorative M4 panel must not intercept pointer input: " + panel.name);
                if (!hudFields.FindProperty("polishedLabels").boolValue) errors.Add("M4 HUD must use the compact presentation labels.");
                var buttons = new[] { "restart", "retry", "next" }.Select(name => hudFields.FindProperty(name).objectReferenceValue as Button).ToArray();
                var assigned = new SerializedObject(feedback[0]).FindProperty("buttons");
                if (assigned.arraySize != buttons.Length || buttons.Any(button => button == null || !(button.targetGraphic is RoundedPanel) || !button.targetGraphic.raycastTarget) ||
                    !Enumerable.Range(0, Math.Min(assigned.arraySize, buttons.Length)).All(index => assigned.GetArrayElementAtIndex(index).objectReferenceValue == buttons[index]))
                    errors.Add("M4 audio must bind the actual rounded Restart, Retry and Next buttons exactly once, with raycastable button graphics.");
            }
            var camera = view.InteractionCamera;
            if (camera != null && (camera.GetComponent<PortraitCameraFit>() == null || !camera.orthographic || camera.clearFlags != CameraClearFlags.SolidColor))
                errors.Add("M4 camera needs portrait-only adaptive framing and a calm solid background.");
            else if (camera != null && (float.IsNaN(camera.GetComponent<PortraitCameraFit>().ReferenceSize) ||
                float.IsInfinity(camera.GetComponent<PortraitCameraFit>().ReferenceSize) ||
                camera.GetComponent<PortraitCameraFit>().ReferenceSize <= 0f ||
                Math.Abs(camera.GetComponent<PortraitCameraFit>().ReferenceSize - camera.orthographicSize) > 0.001f))
                errors.Add("Save matching positive camera and adaptive reference sizes to preserve approved framing.");
            return errors;
        }
        [MenuItem("ColorSlide/Setup/Validate M4 Polish")]
        private static void ValidateFromMenu()
        {
            var errors = M0Validation.InspectProject(); errors.AddRange(InspectSavedScene());
            if (errors.Count == 0) Debug.Log("ColorSlide M4 saved assets are valid. Render, audio and Play Mode QA remain manual.");
            else Debug.LogError("ColorSlide M4 validation:\n- " + string.Join("\n- ", errors));
        }
    }
}
