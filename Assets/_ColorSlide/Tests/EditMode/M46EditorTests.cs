using System;
using System.IO;
using System.Linq;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46EditorTests
    {
        // Copy only the accepted scene into a uniquely owned temporary asset. Never save/close user scenes.
        internal static void WithCampaign(Action<CampaignController,Scene> check)
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            var original = File.ReadAllBytes(M45Feedback.ScenePath); var active = SceneManager.GetActiveScene();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + ":" + item.isLoaded + ":" + item.isActive).ToArray();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M46OwnedTest.unity");
            var copied = false; var scene = default(Scene);
            try
            {
                copied = AssetDatabase.CopyAsset(M45Feedback.ScenePath,path); Assert.That(copied, Is.True);
                scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignController>(true)).Single();
                check(owner,scene);
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                scene = SceneManager.GetSceneByPath(path);
                if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
                scene = SceneManager.GetSceneByPath(path);
                if (copied && (!scene.IsValid() || !scene.isLoaded)) AssetDatabase.DeleteAsset(path);
            }
            Assert.That(File.ReadAllBytes(M45Feedback.ScenePath), Is.EqualTo(original));
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + ":" + item.isLoaded + ":" + item.isActive), Is.EqualTo(setup));
        }

        [Test]
        public void ExistingFeedbackSceneAppendsTwoLevelsWithoutChangingItsFourPersistentDefinitionsOrHud()
        {
            WithCampaign((owner,scene) =>
            {
                var serialized = owner.Levels.Select(EditorJsonUtility.ToJson).ToArray();
                // Keep this historical regression case and its accepted-asset checks; extend finality to the new end.
                var campaign = owner.CreateCampaign(); Assert.That(campaign.LevelCount, Is.EqualTo(8));
                Assert.That(owner.Levels.Count, Is.EqualTo(4));
                for (var i = 0; i < 4; i++)
                {
                    Assert.That(campaign.CurrentLevel, Is.SameAs(owner.Levels[i].Data));
                    Assert.That(EditorUtility.IsPersistent(owner.Levels[i]), Is.True);
                    CampaignTests.Solve(campaign.CurrentSession,i); Assert.That(campaign.Next(), Is.True);
                }
                Assert.That(campaign.CurrentLevel.DisplayName, Is.EqualTo("Traffic Lock"));
                foreach (var action in AdvancedLevelSolutions.For(0)) campaign.CurrentSession.MoveTowards(action.Block,action.Target,action.Axis);
                Assert.That(campaign.Next(), Is.True); Assert.That(campaign.CurrentLevel.DisplayName, Is.EqualTo("Grand Escape"));
                foreach (var action in AdvancedLevelSolutions.For(1)) campaign.CurrentSession.MoveTowards(action.Block,action.Target,action.Axis);
                Assert.That(campaign.IsComplete, Is.False);
                for (var index = 0; index < 2; index++)
                {
                    Assert.That(campaign.Next(), Is.True);
                    Assert.That(campaign.CurrentLevel.Id, Is.EqualTo(MasterCampaignLevels.Create()[index].Id));
                    foreach (var action in MasterLevelSolutions.For(index)) campaign.CurrentSession.MoveTowards(action.Block,action.Target,action.Axis);
                }
                Assert.That(campaign.IsComplete, Is.True); Assert.That(campaign.Next(), Is.False);
                Assert.That(owner.Levels.Select(EditorJsonUtility.ToJson), Is.EqualTo(serialized));
                Assert.That(M45FeedbackValidation.InspectScene(scene), Is.Empty);
            });
        }

        [TestCase(0)] [TestCase(1)]
        public void LargerProductionHierarchyRetainsRealFeedbackAndBurstOwnershipForEveryWitnessExit(int index)
        {
            WithCampaign((owner,scene) =>
            {
                var level = AdvancedCampaignLevels.Create()[index]; GameplayController gameplay = null;
                var childCount = owner.Presentation.transform.childCount;
                var camera = owner.Presentation.InteractionCamera; var position = camera.transform.position; var size = camera.orthographicSize;
                try
                {
                    gameplay = owner.Presentation.Build(level,level.CreateSession()); gameplay.gameObject.SetActive(true);
                    Assert.That(gameplay.Board.Width, Is.EqualTo(level.Width)); Assert.That(gameplay.Board.Height, Is.EqualTo(level.Height));
                    Assert.That(gameplay.transform.position, Is.EqualTo(owner.Presentation.transform.position + M46FramingTests.Origin(level)));
                    Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.orthographicSize, Is.EqualTo(size), "EditMode construction must not mutate the saved camera.");
                    Assert.That(gameplay.GetComponents<GateGlow>().Length, Is.EqualTo(1));
                    var burst = gameplay.GetComponent<SoftCubeBurst>(); Assert.That(burst, Is.Not.Null);
                    Assert.That(gameplay.UsesSoftCubeBurst, Is.True); Assert.That(burst.Theme, Is.SameAs(owner.Presentation.Theme));
                    Assert.That(gameplay.GetComponentsInChildren<PolishedBlockVisual>(true).Length, Is.EqualTo(level.Blocks.Length));
                    Assert.That(gameplay.GateViews.Count, Is.EqualTo(4));
                    Assert.That(gameplay.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                    foreach (var action in AdvancedLevelSolutions.For(index))
                    {
                        var view = gameplay.Board.BlockViews.Single(block => block.BlockId == action.Block);
                        var result = gameplay.RequestMove(view,action.Target,action.Axis);
                        Assert.That(result.Status, Is.EqualTo(action.Exit ? ColorSlide.Board.MovementStatus.Exited : ColorSlide.Board.MovementStatus.Moved));
                        if (!action.Exit) view.SnapTo(gameplay.Board.Coordinates.CellToWorld(action.Target)); // Same visual update as PointerDragController.
                        else
                        {
                            Assert.That(gameplay.Board.Model.IsActive(action.Block), Is.False);
                            Assert.That(view.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
                            Assert.That(gameplay.IsShowingExitBurst, Is.True);
                            gameplay.AdvanceExitPresentation(SoftBurstMotion.OutwardSeconds);
                            Assert.That(burst.ActiveFragmentCount, Is.EqualTo(10));
                            var fragments = burst.transform.Find("Soft Cube Burst");
                            Assert.That(fragments, Is.Not.Null); Assert.That(fragments.childCount, Is.EqualTo(10));
                            Assert.That(fragments.GetComponentsInChildren<Collider>(true), Is.Empty);
                            gameplay.AdvanceExitPresentation(SoftBurstMotion.ScatterSeconds);
                            Assert.That(gameplay.IsPresentingExit, Is.False); Assert.That(burst.ActiveFragmentCount, Is.Zero);
                        }
                    }
                    Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                    Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
                    var module = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).Single();
                    foreach (var action in new[] { module.point,module.leftClick,module.rightClick,module.middleClick,module.scrollWheel,module.move,module.submit,module.cancel })
                        Assert.That(action != null && EditorUtility.IsPersistent(action) && action.action != null && action.action.actionMap.asset == module.actionsAsset, Is.True);
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
                Assert.That(owner.Presentation.transform.childCount, Is.EqualTo(childCount));
            });
        }

        [TestCase(0)] [TestCase(1)]
        public void FittedNativeCameraRaysReturnEveryDisplayedGridCellAtAllPortraitAspects(int index)
        {
            WithCampaign((owner,scene) =>
            {
                var level = AdvancedCampaignLevels.Create()[index]; var view = owner.Presentation; GameplayController gameplay = null;
                try
                {
                    gameplay = view.Build(level,level.CreateSession()); var camera = view.InteractionCamera; var original = camera.transform.position;
                    foreach (var height in new[] { 1920,2340,2400 })
                    {
                        camera.aspect = 1080f/height;
                        var bounds = BoardFraming.Envelope(level); bounds.center += gameplay.transform.position;
                        var fit = BoardFraming.Fit(bounds,original,camera.transform.right,camera.transform.up,camera.aspect,
                            BoardFraming.GameplayRegion(new Vector2(1080,height),new Rect(0,64,1080,height-160)),PortraitCameraFit.SizeFor(6.25f,camera.aspect));
                        camera.transform.position = fit.CameraPosition; camera.orthographicSize = fit.Size;
                        var coordinates = gameplay.Board.Coordinates; var plane = new Plane(Vector3.up,coordinates.Origin);
                        for (var x = 0; x < level.Width; x++) for (var y = 0; y < level.Height; y++)
                        {
                            var cell = new Vector2Int(x,y); var world = coordinates.CellToWorld(cell);
                            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(world));
                            Assert.That(plane.Raycast(ray,out var distance), Is.True);
                            Assert.That(coordinates.WorldToCell(ray.GetPoint(distance)), Is.EqualTo(cell));
                        }
                    }
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
            });
        }
    }
}
