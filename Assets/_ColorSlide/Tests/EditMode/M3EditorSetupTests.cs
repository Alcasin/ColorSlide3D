using System;
using System.IO;
using System.Linq;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M3EditorSetupTests
    {
        private static void RequireCampaign()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M3Campaign.ScenePath), Is.Not.Null,
                "Run ColorSlide > Setup > Apply M3 Campaign first.");
        }

        [Test]
        public void PersistedLevelAssetsValidateAndSolveUsingProductionSessions()
        {
            RequireCampaign();
            for (var i = 0; i < 4; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(M3Campaign.LevelPath(i));
                AssetDatabase.ImportAsset(M3Campaign.LevelPath(i), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(M3Campaign.LevelPath(i));
                Assert.That(asset, Is.Not.Null); Assert.That(AssetDatabase.Contains(asset), Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(M3Campaign.LevelPath(i)), Is.EqualTo(guid).And.Not.Empty);
                var session = asset.CreateSession(); session.Start(); CampaignTests.Solve(session, i);
            }
            Assert.That(M3Validation.InspectSavedScene(), Is.Empty);
        }

        [Test]
        public void RepeatedApplyPreservesAuthoredLevelTimersDependenciesAndSceneArrangement()
        {
            RequireCampaign();
            var paths = AssetDatabase.GetDependencies(M3Campaign.ScenePath, true).Where(File.Exists)
                .Concat(Enumerable.Range(0, 4).Select(M3Campaign.LevelPath))
                .Concat(new[] { M0Foundation.ScenePath, M1InteractionDemo.ScenePath, M2GameplayDemo.ScenePath,
                    M0Foundation.InputPath, M2GameplayDemo.UiActionsPath })
                .SelectMany(path => File.Exists(path + ".meta") ? new[] { path, path + ".meta" } : new[] { path }).Distinct().ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            M3Campaign.Apply(); M3Campaign.Apply();
            for (var i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }

        [Test]
        public void ReusablePresentationAdoptsEachSessionAndRetiresOnlyItsOwnedObjects()
        {
            RequireCampaign();
            var active = SceneManager.GetActiveScene();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M3PresentationTest.unity");
            var scene = default(Scene);
            try
            {
                Assert.That(AssetDatabase.CopyAsset(M3Campaign.ScenePath, path), Is.True);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene); // New primitive objects belong only to this owned saved copy.
                var owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignController>()).Single();
                var presentation = owner.Presentation;
                var children = presentation.transform.childCount;
                for (var repeat = 0; repeat < 2; repeat++)
                foreach (var level in owner.Levels)
                {
                    var session = level.CreateSession();
                    var gameplay = presentation.Build(level.Data, session);
                    try
                    {
                        Assert.That(gameplay.gameObject.activeSelf, Is.False, "Input cannot enable while wiring a new board.");
                        Assert.That(gameplay.Session, Is.SameAs(session));
                        Assert.That(gameplay.Board.Model, Is.SameAs(session.Board));
                        Assert.That(gameplay.Drag.Gameplay, Is.SameAs(gameplay));
                        Assert.That(gameplay.GateViews.Count, Is.EqualTo(level.Data.Gates.Length));
                        Assert.That(gameplay.Board.BlockViews.Count, Is.EqualTo(level.Data.Blocks.Length));
                        Assert.That(gameplay.CreateSession().InitialSeconds, Is.EqualTo(level.Data.Seconds));
                        foreach (var renderer in gameplay.GetComponentsInChildren<MeshRenderer>(true))
                            Assert.That(M0Validation.FitsPortraitView(presentation.InteractionCamera, renderer.bounds), Is.True, renderer.name);
                    }
                    finally { LevelPresentation.Retire(gameplay.gameObject); }
                    Assert.That(presentation.transform.childCount, Is.EqualTo(children));
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
        public void DirtySceneRefusalPrecedesCampaignWrites()
        {
            RequireCampaign();
            var active = SceneManager.GetActiveScene();
            var before = File.ReadAllBytes(M3Campaign.ScenePath);
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M3UnsavedSceneTest.unity");
            try
            {
                Assert.That(AssetDatabase.CopyAsset(M3Campaign.ScenePath, path), Is.True);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(Assert.Throws<InvalidOperationException>(() => M3Campaign.Apply()).Message, Does.Contain("Save or discard"));
                Assert.That(scene.isDirty, Is.True); Assert.That(File.ReadAllBytes(M3Campaign.ScenePath), Is.EqualTo(before));
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                scene = SceneManager.GetSceneByPath(path);
                if ((!scene.IsValid() || !scene.isLoaded) && File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
