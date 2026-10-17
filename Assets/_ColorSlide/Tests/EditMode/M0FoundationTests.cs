using System.IO;
using System.Linq;
using ColorSlide.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ColorSlide.Tests.EditMode
{
    /// <summary>Prerequisite: run Apply M0 Foundation and save all open scenes first.</summary>
    public sealed class M0FoundationTests
    {
        [SetUp]
        public void RequirePreparedFoundation()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M0Foundation.ScenePath), Is.Not.Null,
                "Run ColorSlide > Setup > Apply M0 Foundation before running the M0 tests.");
            M0Foundation.RequireEditModeAndSavedScenes();
        }

        [Test]
        public void ProjectAndPointerContractSupportPortraitTouchAndMouse()
        {
            Assert.That(M0Validation.InspectProject(), Is.Empty,
                "Portrait/URP/input configuration does not meet M0 requirements.");
            Assert.That(InputSystem.IsFirstLayoutBasedOnSecond("Mouse", "Pointer"), Is.True,
                "The installed Mouse layout must resolve the generic pointer bindings.");
            Assert.That(InputSystem.IsFirstLayoutBasedOnSecond("Touchscreen", "Pointer"), Is.True,
                "The installed Touchscreen layout must expose primary-touch pointer controls.");
        }

        [Test]
        public void SavedGameplayContainsAFramedOrthographic3DBoardAndLight()
        {
            Assert.That(M0Validation.InspectGameplayScene(), Is.Empty,
                "The saved Gameplay scene has invalid camera, board, material, or lighting configuration.");
        }

        [Test]
        public void ValidationReportsAuthoredCameraProblemsWithoutResettingThem()
        {
            var scene = EditorSceneManager.OpenPreviewScene(M0Foundation.ScenePath);
            try
            {
                var camera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                camera.orthographic = false;
                Assert.That(M0Validation.InspectScene(scene).Any(error => error.Contains("orthographic")), Is.True);
                Assert.That(camera.orthographic, Is.False, "Validation must not repair authored values.");

                camera.orthographic = true;
                camera.transform.position += Vector3.right * 100f;
                var authoredPosition = camera.transform.position;
                Assert.That(M0Validation.InspectScene(scene).Any(error => error.Contains("9:16")), Is.True,
                    "Moving the camera off the board must fail portrait framing validation.");
                Assert.That(camera.transform.position, Is.EqualTo(authoredPosition));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void RepeatedSetupPreservesSavedAssetsAndLoadedSceneArrangement()
        {
            // Snapshot actual authored bytes, rather than asserting hardcoded creation defaults.
            var paths = new[] { M0Foundation.ScenePath, M0Foundation.MaterialPath, M0Foundation.InputPath };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var guids = paths.Select(path => AssetDatabase.AssetPathToGUID(path)).ToArray();
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var activeHandle = SceneManager.GetActiveScene().handle;

            M0Foundation.Apply();
            M0Foundation.Apply();

            for (var i = 0; i < paths.Length; i++)
            {
                Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i] + " was rewritten.");
                Assert.That(AssetDatabase.AssetPathToGUID(paths[i]), Is.EqualTo(guids[i]), paths[i] + " changed GUID.");
            }
            var afterSetup = EditorSceneManager.GetSceneManagerSetup();
            Assert.That(afterSetup.Select(item => new { item.path, item.isLoaded, item.isActive }),
                Is.EqualTo(sceneSetup.Select(item => new { item.path, item.isLoaded, item.isActive })),
                "Setup changed the loaded scene arrangement.");
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeHandle));
            Assert.That(M0Validation.InspectGameplayScene(), Is.Empty,
                "Repeated setup introduced duplicate objects or invalid scene structure.");
        }

        [Test]
        public void SetupRefusesUnsavedScenesBeforeChangingAssets()
        {
            var originalActive = SceneManager.GetActiveScene();
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var before = File.ReadAllBytes(M0Foundation.ScenePath);
            var temporaryPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/_ColorSlide/Tests/EditMode/M0UnsavedSceneTest.unity");
            try
            {
                // A clean untitled scene can already be open in the Test Runner. Open a
                // saved copy so arrangement does not try to create a second untitled scene.
                Assert.That(AssetDatabase.CopyAsset(M0Foundation.ScenePath, temporaryPath), Is.True,
                    "Could not create the isolated test scene asset.");
                var dirtyScene = EditorSceneManager.OpenScene(temporaryPath, OpenSceneMode.Additive);
                Assert.That(EditorSceneManager.MarkSceneDirty(dirtyScene), Is.True,
                    "Could not arrange the unsaved-scene safety condition.");
                Assert.That(dirtyScene.isDirty, Is.True);

                var exception = Assert.Throws<System.InvalidOperationException>(() => M0Foundation.Apply());
                Assert.That(exception.Message, Does.Contain("Save or discard"));
                Assert.That(dirtyScene.isDirty, Is.True, "Setup discarded unsaved edits.");
                Assert.That(File.ReadAllBytes(M0Foundation.ScenePath), Is.EqualTo(before));
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded)
                    SceneManager.SetActiveScene(originalActive);
                // Resolve by the unique path even if opening the scene failed before returning.
                var temporaryScene = SceneManager.GetSceneByPath(temporaryPath);
                try
                {
                    if (temporaryScene.IsValid() && temporaryScene.isLoaded)
                        Assert.That(EditorSceneManager.CloseScene(temporaryScene, removeScene: true), Is.True,
                            "Could not close the isolated test scene.");
                }
                finally
                {
                    var remainingScene = SceneManager.GetSceneByPath(temporaryPath);
                    if ((!remainingScene.IsValid() || !remainingScene.isLoaded) && File.Exists(temporaryPath))
                        Assert.That(AssetDatabase.DeleteAsset(temporaryPath), Is.True,
                            "Could not delete the temporary test scene asset: " + temporaryPath);
                }
            }

            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(originalActive.handle));
            Assert.That(EditorSceneManager.GetSceneManagerSetup()
                    .Select(item => new { item.path, item.isLoaded, item.isActive }),
                Is.EqualTo(originalSetup.Select(item => new { item.path, item.isLoaded, item.isActive })),
                "Test cleanup changed the original scene arrangement.");
        }
    }
}
