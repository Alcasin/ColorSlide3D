using System.IO;
using System.Linq;
using ColorSlide.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M1EditorSetupTests
    {
        [Test]
        public void RepeatedM1SetupPreservesM0AndAuthoredM1AssetsAndSceneArrangement()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M1InteractionDemo.ScenePath), Is.Not.Null,
                "Run ColorSlide > Setup > Apply M1 Interaction Demo before this Editor integration test.");
            var paths = AssetDatabase.GetDependencies(M1InteractionDemo.ScenePath, recursive: true)
                .Where(path => path.StartsWith(M0Foundation.RootPath + "/") && File.Exists(path))
                .Concat(new[] { M0Foundation.ScenePath, M0Foundation.MaterialPath, M0Foundation.InputPath,
                    "ProjectSettings/ProjectSettings.asset", "ProjectSettings/GraphicsSettings.asset",
                    "ProjectSettings/QualitySettings.asset" }).Distinct().ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var active = SceneManager.GetActiveScene().handle;
            var m0Guid = AssetDatabase.AssetPathToGUID(M0Foundation.ScenePath);
            var m1Guid = AssetDatabase.AssetPathToGUID(M1InteractionDemo.ScenePath);

            M1InteractionDemo.Apply();
            M1InteractionDemo.Apply();

            for (var i = 0; i < paths.Length; i++)
                Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i] + " was rewritten.");
            Assert.That(AssetDatabase.AssetPathToGUID(M0Foundation.ScenePath), Is.EqualTo(m0Guid));
            Assert.That(AssetDatabase.AssetPathToGUID(M1InteractionDemo.ScenePath), Is.EqualTo(m1Guid));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(active));
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }),
                Is.EqualTo(sceneSetup.Select(item => new { item.path, item.isLoaded, item.isActive })));
            Assert.That(M1Validation.InspectSavedScene(), Is.Empty);
            Assert.That(M0Validation.InspectGameplayScene(), Is.Empty);
        }
    }
}
