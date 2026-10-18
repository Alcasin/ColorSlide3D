using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorSlide.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorSlide.Editor
{
    public static class M3Campaign
    {
        public const string ScenePath = M0Foundation.RootPath + "/Scenes/Campaign.unity";
        public const string LevelsPath = M0Foundation.RootPath + "/Levels";
        public static string LevelPath(int index) => LevelsPath + "/Level" + (index + 1).ToString("00") + ".asset";
        private static readonly string[] Palette = { "Blue", "Amber", "Purple", "Green", "TileA", "TileB", "Obstacle" };

        [MenuItem("ColorSlide/Setup/Apply M3 Campaign")]
        private static void ApplyFromMenu()
        {
            try { Apply(); Debug.Log("ColorSlide M3: Campaign assets are ready. Open M3 Campaign and Play in 9:16."); }
            catch (Exception exception) { Debug.LogError("ColorSlide M3 setup stopped: " + exception.Message); }
        }

        public static void Apply()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            ThrowOnErrors(M0Validation.InspectProject());
            RequireCompatibleAsset<SceneAsset>(ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { ThrowOnErrors(M3Validation.InspectSavedScene()); return; }
            ThrowOnErrors(M2Validation.InspectSavedScene());
            var defaults = M3AuthoredLevels.Create();
            var definitions = new LevelDefinition[4];
            var data = new LevelData[4];
            for (var i = 0; i < 4; i++)
            {
                RequireCompatibleAsset<LevelDefinition>(LevelPath(i));
                definitions[i] = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath(i));
                if (definitions[i] != null && EditorUtility.IsDirty(definitions[i]))
                    throw new InvalidOperationException("Save level edits first: " + LevelPath(i));
                data[i] = definitions[i] != null ? definitions[i].Data : defaults[i];
                if (data[i] == null) throw new InvalidOperationException("Missing data in " + LevelPath(i));
            }
            _ = new CampaignSession(data);
            if ((File.Exists(LevelsPath) || Directory.Exists(LevelsPath) || File.Exists(LevelsPath + ".meta") ||
                !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(LevelsPath))) && !AssetDatabase.IsValidFolder(LevelsPath))
                throw new InvalidOperationException("Conflicting level folder: " + LevelsPath);
            var materials = Palette.Select(name => AssetDatabase.LoadAssetAtPath<Material>(M0Foundation.RootPath + "/Materials/M1/" + name + ".mat")).ToArray();
            if (materials.Any(material => !M0Validation.IsUrpMaterial(material) || EditorUtility.IsDirty(material)))
                throw new InvalidOperationException("Save or resolve the accepted M1 material palette first.");

            var preview = EditorSceneManager.OpenPreviewScene(M2GameplayDemo.ScenePath);
            try
            {
                var camera = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                var slab = preview.GetRootGameObjects().Single(root => root.name == M0Foundation.BoardName).GetComponent<MeshRenderer>();
                var center = new Vector3(slab.bounds.center.x, slab.bounds.max.y, slab.bounds.center.z);
                foreach (var level in data)
                    if (!M0Validation.FitsPortraitView(camera, M3Validation.LevelBounds(level, center)))
                        throw new InvalidOperationException("Level '" + level.Id + "' exits do not fit the accepted portrait camera. Adjust level data.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var created = new List<string>();
            var previous = SceneManager.GetActiveScene();
            var copied = false;
            var saved = false;
            try
            {
                if (!AssetDatabase.IsValidFolder(LevelsPath)) AssetDatabase.CreateFolder(M0Foundation.RootPath, "Levels");
                for (var i = 0; i < 4; i++)
                {
                    if (definitions[i] != null) continue;
                    var asset = ScriptableObject.CreateInstance<LevelDefinition>();
                    asset.Configure(data[i]);
                    AssetDatabase.CreateAsset(asset, LevelPath(i));
                    created.Add(LevelPath(i));
                    AssetDatabase.SaveAssetIfDirty(asset);
                    AssetDatabase.ImportAsset(LevelPath(i), ImportAssetOptions.ForceSynchronousImport);
                    definitions[i] = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath(i));
                    if (definitions[i] == null || !AssetDatabase.Contains(definitions[i]))
                        throw new IOException("Could not persist " + LevelPath(i));
                    definitions[i].CreateSession();
                }
                copied = AssetDatabase.CopyAsset(M2GameplayDemo.ScenePath, ScenePath);
                if (!copied) throw new IOException("Could not create Campaign scene copy.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Build(scene, definitions, materials);
                ThrowOnErrors(M3Validation.InspectScene(scene));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save Campaign scene.");
                saved = true;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                var scene = SceneManager.GetSceneByPath(ScenePath);
                if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (!saved && (!scene.IsValid() || !scene.isLoaded))
                {
                    var removed = !copied || AssetDatabase.DeleteAsset(ScenePath);
                    if (removed) foreach (var path in created) AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void Build(Scene scene, LevelDefinition[] definitions, Material[] materials)
        {
            var roots = scene.GetRootGameObjects();
            var old = roots.SelectMany(root => root.GetComponentsInChildren<GameplayController>()).Single();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
            if (camera.transform.IsChildOf(old.transform)) throw new InvalidOperationException("Campaign copy cannot remove the camera's parent.");
            var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>()).Single();
            var fields = new SerializedObject(hud);
            var timer = (Text)fields.FindProperty("timeText").objectReferenceValue;
            var count = (Text)fields.FindProperty("countText").objectReferenceValue;
            var retry = (Button)fields.FindProperty("retry").objectReferenceValue;
            var panel = (GameObject)fields.FindProperty("resultPanel").objectReferenceValue;
            var top = timer.rectTransform.parent.GetComponent<RectTransform>();
            if (count.transform.parent != top) throw new InvalidOperationException("The copied M2 HUD layout needs manual review.");
            var slab = roots.Single(root => root.name == M0Foundation.BoardName).GetComponent<MeshRenderer>();
            var root = new GameObject("M3 Campaign");
            root.transform.position = new Vector3(slab.bounds.center.x, slab.bounds.max.y, slab.bounds.center.z);
            var presentation = root.AddComponent<LevelPresentation>();
            presentation.Configure(camera, AssetDatabase.LoadAssetAtPath<InputActionAsset>(M0Foundation.InputPath), materials);
            var campaign = root.AddComponent<CampaignController>();
            campaign.Configure(definitions, presentation);
            UnityEngine.Object.DestroyImmediate(old.gameObject); // Owned copy only; accepted GameplayDemo is never edited.

            top.sizeDelta = new Vector2(top.sizeDelta.x, 240f);
            timer.rectTransform.anchoredPosition = new Vector2(28f, -76f);
            timer.fontSize = 36; count.fontSize = 32;
            var level = new GameObject("Current Level", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            level.rectTransform.SetParent(top, false);
            level.rectTransform.anchorMin = level.rectTransform.anchorMax = level.rectTransform.pivot = new Vector2(0f, 1f);
            level.rectTransform.anchoredPosition = new Vector2(28f, -8f);
            level.rectTransform.sizeDelta = new Vector2(1000f, 60f);
            level.font = timer.font; level.fontSize = 32; level.color = Color.white;
            level.alignment = TextAnchor.MiddleLeft; level.raycastTarget = false; level.text = "Level 1 / 4 - " + definitions[0].Data.DisplayName;
            // Clone only the existing owned Retry button, preserving the accepted font/style.
            var next = UnityEngine.Object.Instantiate(retry, panel.transform);
            next.name = "Next";
            next.onClick = new Button.ButtonClickedEvent();
            next.GetComponentInChildren<Text>(true).text = "Next";
            retry.GetComponent<RectTransform>().anchorMin = retry.GetComponent<RectTransform>().anchorMax = new Vector2(0.35f, 0.45f);
            retry.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            var nextRect = next.GetComponent<RectTransform>();
            nextRect.anchorMin = nextRect.anchorMax = new Vector2(0.65f, 0.45f);
            nextRect.anchoredPosition = Vector2.zero;
            next.gameObject.SetActive(false);
            panel.SetActive(false);
            hud.ConfigureCampaign(campaign, level, next);
        }

        [MenuItem("ColorSlide/Setup/Open M3 Campaign")]
        private static void OpenFromMenu()
        {
            try
            {
                M0Foundation.RequireEditModeAndSavedScenes();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new InvalidOperationException("Run Apply M3 Campaign first.");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception) { Debug.LogError("ColorSlide M3 open stopped: " + exception.Message); }
        }
        private static void RequireCompatibleAsset<T>(string path) where T : UnityEngine.Object
        {
            if ((File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) &&
                AssetDatabase.LoadAssetAtPath<T>(path) == null)
                throw new InvalidOperationException("Conflicting, incomplete or unimported asset was preserved: " + path);
        }
        private static void ThrowOnErrors(List<string> errors)
        { if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors)); }
    }
}
