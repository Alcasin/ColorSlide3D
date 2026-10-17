using System;
using System.IO;
using System.Linq;
using ColorSlide.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M2EditorSetupTests
    {
        private static void RequirePreparedScene()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M2GameplayDemo.ScenePath), Is.Not.Null,
                "Run ColorSlide > Setup > Apply M2 Gameplay Demo first.");
        }

        [Test]
        public void RepeatedM2SetupPreservesAcceptedSourcesAuthoredAssetsAndSceneArrangement()
        {
            RequirePreparedScene();
            var paths = AssetDatabase.GetDependencies(M2GameplayDemo.ScenePath, true).Where(File.Exists)
                .Concat(new[] { M0Foundation.ScenePath, M1InteractionDemo.ScenePath,
                    "ProjectSettings/ProjectSettings.asset", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
                .Distinct().ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var active = SceneManager.GetActiveScene().handle;
            var guid = AssetDatabase.AssetPathToGUID(M2GameplayDemo.ScenePath);
            M2GameplayDemo.Apply(); M2GameplayDemo.Apply();
            for (var i = 0; i < paths.Length; i++)
                Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i] + " was rewritten.");
            Assert.That(AssetDatabase.AssetPathToGUID(M2GameplayDemo.ScenePath), Is.EqualTo(guid));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(active));
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }),
                Is.EqualTo(setup.Select(item => new { item.path, item.isLoaded, item.isActive })));
            Assert.That(M2Validation.InspectSavedScene(), Is.Empty);
        }

        [Test]
        public void UiActionReferencesRemainPersistentAfterImportAndSceneReload()
        {
            RequirePreparedScene();
            var protectedPaths = new[] { M0Foundation.InputPath, M0Foundation.InputPath + ".meta",
                M2GameplayDemo.UiActionsPath, M2GameplayDemo.UiActionsPath + ".meta", M2GameplayDemo.ScenePath };
            var before = protectedPaths.Select(File.ReadAllBytes).ToArray();
            var temporaryPath = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M2UiReferenceTest.unity");
            try
            {
                Assert.That(AssetDatabase.CopyAsset(M2GameplayDemo.ScenePath, temporaryPath), Is.True);
                var identities = InspectUiReferences(temporaryPath);
                // Re-resolve the importer-produced representations, then reload only an owned scene copy.
                AssetDatabase.ImportAsset(M2GameplayDemo.UiActionsPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(temporaryPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Assert.That(InspectUiReferences(temporaryPath), Is.EqualTo(identities));
                Assert.That(M2Validation.InspectSavedScene(), Is.Empty);
                for (var i = 0; i < protectedPaths.Length; i++)
                    Assert.That(File.ReadAllBytes(protectedPaths[i]), Is.EqualTo(before[i]), protectedPaths[i] + " was rewritten.");
            }
            finally
            {
                if (File.Exists(temporaryPath)) Assert.That(AssetDatabase.DeleteAsset(temporaryPath), Is.True);
            }
        }

        private static string[] InspectUiReferences(string path)
        {
            var scene = default(Scene);
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(path);
                var events = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray();
                Assert.That(events.Length, Is.EqualTo(1));
                Assert.That(events[0].GetComponents<BaseInputModule>().Length, Is.EqualTo(1));
                var module = events[0].GetComponent<InputSystemUIInputModule>();
                Assert.That(module, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(module.actionsAsset), Is.EqualTo(M2GameplayDemo.UiActionsPath));
                var names = new[] { "Point", "Click", "Navigate", "Submit", "Cancel", "ScrollWheel", "RightClick", "MiddleClick" };
                var references = new[] { module.point, module.leftClick, module.move, module.submit, module.cancel,
                    module.scrollWheel, module.rightClick, module.middleClick };
                var identities = new string[references.Length];
                for (var i = 0; i < references.Length; i++)
                {
                    var reference = references[i];
                    Assert.That(reference, Is.Not.Null, names[i]);
                    Assert.That(AssetDatabase.Contains(reference), Is.True, names[i]);
                    Assert.That(EditorUtility.IsPersistent(reference), Is.True, names[i]);
                    Assert.That(AssetDatabase.IsSubAsset(reference), Is.True, names[i]);
                    Assert.That(AssetDatabase.GetAssetPath(reference), Is.EqualTo(M2GameplayDemo.UiActionsPath));
                    Assert.That(reference.asset, Is.EqualTo(module.actionsAsset));
                    Assert.That(reference.action, Is.SameAs(module.actionsAsset.FindAction("UI/" + names[i], true)));
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out string guid, out long localId), Is.True);
                    Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(M2GameplayDemo.UiActionsPath)));
                    Assert.That(localId, Is.Not.Zero);
                    identities[i] = guid + ":" + localId;
                }
                Assert.That(module.trackedDevicePosition, Is.Null);
                Assert.That(module.trackedDeviceOrientation, Is.Null);
                return identities;
            }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void M2SetupRejectsDirtyScenesBeforeWritingAnyDemoScene()
        {
            RequirePreparedScene();
            var active = SceneManager.GetActiveScene();
            var before = File.ReadAllBytes(M2GameplayDemo.ScenePath);
            var temporaryPath = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M2UnsavedSceneTest.unity");
            try
            {
                Assert.That(AssetDatabase.CopyAsset(M2GameplayDemo.ScenePath, temporaryPath), Is.True);
                var scene = EditorSceneManager.OpenScene(temporaryPath, OpenSceneMode.Additive);
                Assert.That(EditorSceneManager.MarkSceneDirty(scene), Is.True);
                var exception = Assert.Throws<InvalidOperationException>(() => M2GameplayDemo.Apply());
                Assert.That(exception.Message, Does.Contain("Save or discard"));
                Assert.That(scene.isDirty, Is.True);
                Assert.That(File.ReadAllBytes(M2GameplayDemo.ScenePath), Is.EqualTo(before));
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                var scene = SceneManager.GetSceneByPath(temporaryPath);
                try
                {
                    if (scene.IsValid() && scene.isLoaded) Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
                }
                finally
                {
                    scene = SceneManager.GetSceneByPath(temporaryPath);
                    if ((!scene.IsValid() || !scene.isLoaded) && File.Exists(temporaryPath))
                        Assert.That(AssetDatabase.DeleteAsset(temporaryPath), Is.True);
                }
            }
        }
    }
}
