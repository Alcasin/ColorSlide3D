using System.IO;
using System.Linq;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M4EditorSetupTests
    {
        private static void RequirePolish()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M4Polish.ScenePath), Is.Not.Null, "Run ColorSlide > Setup > Apply M4 Polish first.");
        }
        [Test]
        public void ThemeAndSceneRetainPersistentResourcesAndImportedUiReferences()
        {
            RequirePolish();
            var theme = AssetDatabase.LoadAssetAtPath<SoftToyTheme>(M4Polish.ThemePath);
            Assert.That(M4Validation.InspectTheme(theme), Is.Empty);
            Assert.That(M4Validation.InspectSavedScene(), Is.Empty);
            foreach (var material in theme.Palette)
            {
                var srgb = SoftToyTheme.SelectionSrgb(material);
                var stored = QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
                var actual = material.GetColor("_BaseColor");
                Assert.That(stored.r, Is.EqualTo(actual.r).Within(0.001f));
                Assert.That(stored.g, Is.EqualTo(actual.g).Within(0.001f));
                Assert.That(stored.b, Is.EqualTo(actual.b).Within(0.001f));
            }
        }
        [Test]
        public void ProductionHudConstructionAndTargetedRepairSurviveSaveAndReload()
        {
            RequirePolish(); var active = SceneManager.GetActiveScene();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M4HudTest.unity");
            var scene = default(Scene); var copied = false;
            try
            {
                copied = AssetDatabase.CopyAsset(M3Campaign.ScenePath, path); Assert.That(copied, Is.True);
                var guid = AssetDatabase.AssetPathToGUID(path);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var obj = new GameObject("Temporary Rounded Panel", typeof(RectTransform)); obj.SetActive(false);
                var runtimePanel = obj.AddComponent<RoundedPanel>();
                // Runtime-style construction must satisfy the dependency before the lazy getter is read.
                Assert.That(obj.GetComponents<CanvasRenderer>().Length, Is.EqualTo(1));
                runtimePanel.Configure(26f, Color.white); obj.SetActive(true);
                Assert.That(runtimePanel.canvasRenderer, Is.SameAs(obj.GetComponent<CanvasRenderer>()));
                Object.DestroyImmediate(obj);
                var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();
                var buttons = M4HudStyling.Apply(hud, AssetDatabase.LoadAssetAtPath<SoftToyTheme>(M4Polish.ThemePath));
                AssertHudGraphics(scene, hud);
                Assert.That(M4HudStyling.RepairGraphics(hud), Is.False, "New HUDs must already be valid.");

                var pill = hud.GetComponentsInChildren<RoundedPanel>(true).Single(panel => panel.name == "Time Pill");
                pill.raycastTarget = true;
                Assert.That(M4HudStyling.RepairGraphics(hud), Is.True, "Legacy decorative raycast targets must be repaired.");
                Assert.That(pill.raycastTarget, Is.False);
                var fields = new SerializedObject(pill); fields.FindProperty("radius").floatValue = 19f;
                fields.ApplyModifiedPropertiesWithoutUndo(); pill.color = new Color(0.22f, 0.33f, 0.44f, 1f);
                pill.rectTransform.anchoredPosition += new Vector2(3f, 4f);
                var position = pill.rectTransform.anchoredPosition;
                var appearance = EditorJsonUtility.ToJson(pill); var layout = EditorJsonUtility.ToJson(pill.rectTransform);
                var targets = buttons.Select(button => button.targetGraphic).ToArray();
                var module = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).Single();
                var input = EditorJsonUtility.ToJson(module);

                // Inject the old missing-renderer state on the production Pieces Pill, leaving the tuned Time Pill intact.
                // Remove the dependent graphic first; Unity must not be asked to remove a required renderer.
                var legacyPanel = hud.GetComponentsInChildren<RoundedPanel>(true).Single(panel => panel.name == "Pieces Pill");
                var legacyAppearance = EditorJsonUtility.ToJson(legacyPanel);
                var legacyObject = legacyPanel.gameObject; var wasActive = legacyObject.activeSelf;
                legacyObject.SetActive(false);
                var renderer = legacyObject.GetComponent<CanvasRenderer>();
                Object.DestroyImmediate(legacyPanel); Object.DestroyImmediate(renderer);
                var legacy = legacyObject.AddComponent<LegacyHudGraphic>();
                Assert.That(legacy, Is.Not.Null); legacy.raycastTarget = false;
                Assert.That(legacyObject.GetComponent<CanvasRenderer>(), Is.Null);
                Assert.That(M4HudStyling.RepairGraphics(hud), Is.True);
                Assert.That(legacyObject.GetComponents<CanvasRenderer>().Length, Is.EqualTo(1));
                Assert.That(EditorJsonUtility.ToJson(pill), Is.EqualTo(appearance));
                Assert.That(EditorJsonUtility.ToJson(pill.rectTransform), Is.EqualTo(layout));
                Assert.That(buttons.Select(button => button.targetGraphic), Is.EqualTo(targets));
                Assert.That(EditorJsonUtility.ToJson(module), Is.EqualTo(input));
                // Restore only the fault fixture's real component before testing full HUD persistence.
                Object.DestroyImmediate(legacy); var restored = legacyObject.AddComponent<RoundedPanel>();
                Assert.That(restored, Is.Not.Null); EditorJsonUtility.FromJsonOverwrite(legacyAppearance, restored);
                legacyObject.SetActive(wasActive);
                Assert.That(M4HudStyling.RepairGraphics(hud), Is.False, "A second repair must do nothing.");
                AssertHudGraphics(scene, hud);
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                SceneManager.SetActiveScene(active); EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();
                AssertHudGraphics(scene, hud);
                Assert.That(M4HudStyling.RepairGraphics(hud), Is.False);
                pill = hud.GetComponentsInChildren<RoundedPanel>(true).Single(panel => panel.name == "Time Pill");
                Assert.That(new SerializedObject(pill).FindProperty("radius").floatValue, Is.EqualTo(19f));
                Assert.That(pill.color, Is.EqualTo(new Color(0.22f, 0.33f, 0.44f, 1f)));
                Assert.That(pill.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                scene = SceneManager.GetSceneByPath(path);
                if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                scene = SceneManager.GetSceneByPath(path);
                if (copied && (!scene.IsValid() || !scene.isLoaded) && File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }
        private static void AssertHudGraphics(Scene scene, GameplayHud hud)
        {
            var graphics = hud.GetComponentsInChildren<Graphic>(true);
            Assert.That(hud.GetComponentsInChildren<RoundedPanel>(true).Length, Is.EqualTo(8));
            foreach (var graphic in graphics)
            {
                Assert.That(graphic.GetComponent<RectTransform>(), Is.Not.Null, graphic.name);
                Assert.That(graphic.GetComponents<CanvasRenderer>().Length, Is.EqualTo(1), graphic.name);
                Assert.That(graphic.GetComponents<Graphic>().Length, Is.EqualTo(1), graphic.name);
                Assert.That(graphic.canvasRenderer, Is.SameAs(graphic.GetComponent<CanvasRenderer>()), graphic.name);
            }
            foreach (var name in new[] { "Time Pill", "Pieces Pill", "Result Card", "Top HUD" })
                Assert.That(graphics.Single(graphic => graphic.name == name).raycastTarget, Is.False, name);
            var fields = new SerializedObject(hud);
            Assert.That((fields.FindProperty("timeText").objectReferenceValue as Text).transform.parent.name, Is.EqualTo("Time Pill"));
            foreach (var name in new[] { "restart", "retry", "next" })
            {
                var button = fields.FindProperty(name).objectReferenceValue as Button;
                Assert.That(button, Is.Not.Null, name); Assert.That(button.interactable, Is.True, name);
                Assert.That(button.targetGraphic, Is.TypeOf<RoundedPanel>(), name);
                Assert.That(button.targetGraphic.gameObject, Is.SameAs(button.gameObject), name);
                Assert.That(button.targetGraphic.raycastTarget, Is.True, name);
            }
            var roots = scene.GetRootGameObjects();
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Count(), Is.EqualTo(1));
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<BaseInputModule>(true)).Count(), Is.EqualTo(1));
            // Includes the eight persistent UI action references and the accepted portrait raycaster/input setup.
            Assert.That(M3Validation.InspectScene(scene), Is.Empty);
        }
        [Test]
        public void PolishedFactoryPreservesAllAuthoredAnchorsColliderDimensionsAndOccupancy()
        {
            RequirePolish(); var active = SceneManager.GetActiveScene();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M4PresentationTest.unity");
            var scene = default(Scene);
            try
            {
                Assert.That(AssetDatabase.CopyAsset(M4Polish.ScenePath, path), Is.True);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var campaign = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignController>()).Single();
                var presentation = campaign.Presentation; var initialChildren = presentation.transform.childCount;
                foreach (var level in campaign.Levels)
                {
                    var session = level.CreateSession(); var gameplay = presentation.Build(level.Data, session);
                    try
                    {
                        Assert.That(gameplay.Board.Model, Is.SameAs(session.Board));
                        foreach (var block in gameplay.Board.BlockViews)
                        {
                            var cells = block.GetComponentsInChildren<BoxCollider>(true);
                            Assert.That(cells.Length, Is.EqualTo(block.Footprint.Count));
                            Assert.That(block.GetComponent<PolishedBlockVisual>(), Is.Not.Null);
                            var before = cells.Select(cell => cell.transform.localToWorldMatrix).ToArray();
                            block.SetSelected(true); block.SetSelected(false);
                            for (var i = 0; i < cells.Length; i++)
                            {
                                Assert.That(cells[i].transform.localToWorldMatrix, Is.EqualTo(before[i]));
                                var size = Vector3.Scale(cells[i].size, cells[i].transform.lossyScale);
                                Assert.That((size - new Vector3(0.94f, 0.46f, 0.94f) * level.Data.CellSize).sqrMagnitude, Is.LessThan(0.00001f));
                            }
                            foreach (var cell in block.Footprint)
                                Assert.That(session.Board.OccupantAt(block.InitialAnchor + cell).Id, Is.EqualTo(block.BlockId));
                            foreach (var renderer in block.CellRenderers)
                            {
                                Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(presentation.Theme.RoundedBox));
                                Assert.That(renderer.GetComponentsInChildren<Collider>(true), Is.Empty);
                            }
                        }
                        foreach (var gate in gameplay.GateViews) Assert.That(gate.GetComponentsInChildren<Collider>(true), Is.Empty);
                        Assert.That(gameplay.GetComponentsInChildren<AudioSource>(true), Is.Empty, "Audio belongs to the persistent campaign, not each level.");
                    }
                    finally { LevelPresentation.Retire(gameplay.gameObject); }
                    Assert.That(presentation.transform.childCount, Is.EqualTo(initialChildren));
                }
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                scene = SceneManager.GetSceneByPath(path);
                if ((!scene.IsValid() || !scene.isLoaded) && File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
        }
        [Test]
        public void RepeatedPolishSetupPreservesApprovedTuningAllAcceptedScenesAndLevels()
        {
            RequirePolish();
            var paths = AssetDatabase.GetDependencies(M4Polish.ScenePath, true).Where(File.Exists)
                .Concat(new[] { M0Foundation.ScenePath, M1InteractionDemo.ScenePath, M2GameplayDemo.ScenePath, M3Campaign.ScenePath,
                    M0Foundation.InputPath, M2GameplayDemo.UiActionsPath })
                .SelectMany(path => File.Exists(path + ".meta") ? new[] { path, path + ".meta" } : new[] { path }).Distinct().ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            M4Polish.Apply(); M4Polish.Apply();
            for (var i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }
    }

    // Concrete, transient fault fixture only: unlike RoundedPanel, it deliberately does not require a renderer.
    // It replaces only the Pieces Pill's graphic during repair and is never saved into the temporary scene.
    internal sealed class LegacyHudGraphic : Graphic { }
}
