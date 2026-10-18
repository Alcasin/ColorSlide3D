using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M45EditorTests
    {
        // EditMode does not provide a running MonoBehaviour loop. Invoke the real lifecycle body explicitly;
        // if Unity already dispatched it, the second call also verifies idempotent cleanup.
        private static void DeliverDisable(MonoBehaviour component) => component.GetType()
            .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null).Invoke(component, null);
        private static void RequireAnimation()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M45Animation.ScenePath), Is.Not.Null, "Run Apply M4.5 Animation Polish first.");
        }
        private static void WithGameplay(Action<GameplayController, GameplayHud> check)
        {
            RequireAnimation(); var active = SceneManager.GetActiveScene();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M45OwnedTest.unity");
            var copied = false; var scene = default(Scene); GameplayController gameplay = null;
            try
            {
                copied = AssetDatabase.CopyAsset(M45Animation.ScenePath, path); Assert.That(copied, Is.True);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignController>(true)).Single();
                var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();
                var level = owner.Levels[0]; gameplay = owner.Presentation.Build(level.Data, level.CreateSession());
                gameplay.gameObject.SetActive(true); // Editor OnEnable keeps devices/audio disabled; session is the real production instance.
                check(gameplay, hud);
            }
            finally
            {
                try { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
                finally
                {
                    if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                    scene = SceneManager.GetSceneByPath(path);
                    if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                    scene = SceneManager.GetSceneByPath(path);
                    if (copied && (!scene.IsValid() || !scene.isLoaded) && File.Exists(path)) AssetDatabase.DeleteAsset(path);
                }
            }
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }
        [Test]
        public void ProductionExitOwnsOneBurstReleasesOccupancyAndDefersTheActualResultHud()
        {
            WithGameplay((gameplay, hud) =>
            {
                var burst = gameplay.GetComponent<SoftCubeBurst>(); Assert.That(burst, Is.Not.Null);
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue");
                var amber = gameplay.Board.BlockViews.Single(view => view.BlockId == "Amber");
                Assert.That(burst.Begin(blue, null, Vector3.zero, Vector3.zero), Is.False);
                Assert.That(gameplay.RequestMove(blue, new Vector2Int(1, 2), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Moved));
                Assert.That(burst.IsRunning, Is.False);
                Assert.That(gameplay.RequestMove(blue, new Vector2Int(1, -1), GridAxis.Y).Status, Is.Not.EqualTo(MovementStatus.Exited));
                Assert.That(burst.IsRunning, Is.False);
                var exit = gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y);
                Assert.That(exit.Status, Is.EqualTo(MovementStatus.Exited));
                Assert.That(gameplay.Board.Model.IsActive("Blue"), Is.False); Assert.That(gameplay.Session.RemainingBlocks, Is.EqualTo(1));
                Assert.That(burst.Begin(blue, exit.Exit, Vector3.zero, Vector3.zero), Is.False, "Duplicate starts must not create another effect.");
                Assert.That(gameplay.RequestMove(amber, new Vector2Int(4, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.SessionInactive));
                Assert.That(blue.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
                gameplay.AdvanceExitPresentation(0.12f); Assert.That(burst.ActiveFragmentCount, Is.Zero);
                Assert.That(blue.CellRenderers.All(renderer => renderer.enabled), Is.True);
                gameplay.AdvanceExitPresentation(0.09f); Assert.That(burst.ActiveFragmentCount, Is.EqualTo(10));
                Assert.That(blue.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True);
                var root = gameplay.transform.Find("Soft Cube Burst"); Assert.That(root, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty); Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That(renderer.sharedMaterial, Is.SameAs(blue.CellRenderers[0].sharedMaterial));
                    Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(burst.Theme.RoundedBox));
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                }
                gameplay.AdvanceExitPresentation(0.4f);
                Assert.That(gameplay.IsPresentingExit, Is.False); Assert.That(gameplay.CanInteract, Is.True);
                Assert.That(gameplay.transform.Find("Soft Cube Burst"), Is.Null); Assert.That(blue.gameObject.activeSelf, Is.False);
                gameplay.AdvanceExitPresentation(1f); Assert.That(gameplay.Session.RemainingBlocks, Is.EqualTo(1));
                Assert.That(gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.InactiveBlock));
                Assert.That(burst.IsRunning, Is.False); Assert.That(burst.Begin(blue, exit.Exit, Vector3.zero, Vector3.zero), Is.False);

                // Drive the real HUD against this real session, without starting an Editor campaign/input loop.
                var fields = new SerializedObject(hud); fields.FindProperty("campaign").objectReferenceValue = null;
                fields.FindProperty("gameplay").objectReferenceValue = gameplay; fields.ApplyModifiedPropertiesWithoutUndo();
                var resultPanel = fields.FindProperty("resultPanel").objectReferenceValue as GameObject;
                Assert.That(gameplay.RequestMove(amber, new Vector2Int(4, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
                Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                hud.Refresh(); Assert.That(resultPanel.activeSelf, Is.False);
                gameplay.AdvanceExitPresentation(0.31f);
                Assert.That(gameplay.IsShowingExitBurst, Is.True); hud.Refresh(); Assert.That(resultPanel.activeSelf, Is.False);
                gameplay.AdvanceExitPresentation(0.2f); hud.Refresh();
                Assert.That(resultPanel.activeSelf, Is.True); Assert.That(gameplay.IsPresentingExit, Is.False);
                Assert.That(gameplay.Session.RemainingBlocks, Is.Zero); Assert.That(gameplay.transform.Find("Soft Cube Burst"), Is.Null);
            });
        }
        [TestCase(0.08f)] [TestCase(0.24f)]
        public void DisablingEffectAndThenItsOwnerClearsEitherPhaseWithoutTouchingTheSession(float age)
        {
            WithGameplay((gameplay, hud) =>
            {
                var burst = gameplay.GetComponent<SoftCubeBurst>();
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue");
                gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y); gameplay.AdvanceExitPresentation(age);
                var model = gameplay.Session.Board; burst.enabled = false;
                DeliverDisable(burst);
                Assert.That(burst.ActiveFragmentCount, Is.Zero); Assert.That(blue.CellRenderers.All(renderer => renderer.enabled), Is.True);
                gameplay.AdvanceExitPresentation(0f); Assert.That(gameplay.IsPresentingExit, Is.False);
                Assert.That(gameplay.Session.Board, Is.SameAs(model)); Assert.That(gameplay.Session.RemainingBlocks, Is.EqualTo(1));
                burst.enabled = true;
                var amber = gameplay.Board.BlockViews.Single(view => view.BlockId == "Amber");
                gameplay.RequestMove(amber, new Vector2Int(4, -1), GridAxis.Y); gameplay.AdvanceExitPresentation(age);
                gameplay.gameObject.SetActive(false);
                DeliverDisable(gameplay);
                Assert.That(burst.IsRunning, Is.False); Assert.That(gameplay.IsPresentingExit, Is.False);
                Assert.That(gameplay.transform.Find("Soft Cube Burst"), Is.Null); Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                burst.Cancel(); burst.Cancel(); Assert.That(burst.ActiveFragmentCount, Is.Zero);
            });
        }
        [Test]
        public void DestroyedDepartingViewStillReleasesPresentationAndOwnedFragments()
        {
            WithGameplay((gameplay, hud) =>
            {
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue");
                gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y); gameplay.AdvanceExitPresentation(0.24f);
                UnityEngine.Object.DestroyImmediate(blue.gameObject);
                Assert.That(gameplay.IsPresentingExit, Is.True);
                gameplay.AdvanceExitPresentation(0f);
                Assert.That(gameplay.IsPresentingExit, Is.False); Assert.That(gameplay.CanInteract, Is.True);
                Assert.That(gameplay.transform.Find("Soft Cube Burst"), Is.Null); Assert.That(gameplay.Session.RemainingBlocks, Is.EqualTo(1));
            });
        }
        [Test]
        public void RepeatedApplyPreservesAllAcceptedResourcesScenesAndEditorArrangement()
        {
            RequireAnimation(); Assert.That(M45Validation.InspectSavedScene(), Is.Empty);
            var paths = AssetDatabase.GetDependencies(M45Animation.ScenePath, true).Where(File.Exists)
                .Concat(new[] { M0Foundation.ScenePath, M1InteractionDemo.ScenePath, M2GameplayDemo.ScenePath, M3Campaign.ScenePath, M4Polish.ScenePath })
                .SelectMany(path => File.Exists(path + ".meta") ? new[] { path, path + ".meta" } : new[] { path }).Distinct().ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            M45Animation.Apply(); M45Animation.Apply();
            for (var i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(bytes[i]), paths[i]);
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }
    }
}
