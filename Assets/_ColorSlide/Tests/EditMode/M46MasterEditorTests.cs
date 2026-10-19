using System.Linq;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46MasterEditorTests
    {
        [TestCase(0)] [TestCase(1)]
        public void ProductionMasterConstructionOwnsFeedbackAndCleansEveryExitWithoutDuplicateUi(int index)
        {
            M46EditorTests.WithCampaign((owner,scene) =>
            {
                var initialChildren = owner.Presentation.transform.childCount; GameplayController gameplay = null;
                try
                {
                    var level = MasterCampaignLevels.Create()[index]; gameplay = owner.Presentation.Build(level,level.CreateSession());
                    gameplay.gameObject.SetActive(true);
                    Assert.That(gameplay.GetComponents<GateGlow>().Length, Is.EqualTo(1));
                    Assert.That(gameplay.GetComponentsInChildren<PolishedBlockVisual>(true).Length, Is.EqualTo(level.Blocks.Length));
                    Assert.That(gameplay.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                    var burst = gameplay.GetComponent<SoftCubeBurst>(); Assert.That(burst.Theme, Is.SameAs(owner.Presentation.Theme));
                    foreach (var action in MasterLevelSolutions.For(index))
                    {
                        var block = gameplay.Board.BlockViews.Single(view => view.BlockId == action.Block);
                        var move = gameplay.RequestMove(block,action.Target,action.Axis);
                        Assert.That(move.Status, Is.EqualTo(action.Exit ? ColorSlide.Board.MovementStatus.Exited : ColorSlide.Board.MovementStatus.Moved));
                        if (!action.Exit) block.SnapTo(gameplay.Board.Coordinates.CellToWorld(action.Target));
                        else
                        {
                            Assert.That(gameplay.Board.Model.IsActive(action.Block), Is.False);
                            Assert.That(block.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
                            gameplay.AdvanceExitPresentation(SoftBurstMotion.OutwardSeconds);
                            Assert.That(burst.ActiveFragmentCount, Is.EqualTo(10));
                            var fragments = gameplay.transform.Find("Soft Cube Burst");
                            Assert.That(fragments.GetComponentsInChildren<Collider>(true), Is.Empty);
                            Assert.That(fragments.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                            gameplay.AdvanceExitPresentation(SoftBurstMotion.ScatterSeconds);
                            Assert.That(gameplay.IsPresentingExit, Is.False); Assert.That(burst.ActiveFragmentCount, Is.Zero);
                        }
                    }
                    Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                    Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
                    Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).Count(), Is.EqualTo(1));
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
                Assert.That(owner.Presentation.transform.childCount, Is.EqualTo(initialChildren));
            });
        }

        [TestCase(0)] [TestCase(1)]
        public void NewBoardsKeepNativeCameraPointerRaysMappedToEveryGridCell(int index)
        {
            M46EditorTests.WithCampaign((owner,scene) =>
            {
                GameplayController gameplay = null;
                try
                {
                    var level = MasterCampaignLevels.Create()[index]; gameplay = owner.Presentation.Build(level,level.CreateSession());
                    var camera = owner.Presentation.InteractionCamera; var original = camera.transform.position;
                    foreach (var height in new[] { 1920,2340,2400 })
                    {
                        camera.aspect = 1080f/height; var bounds = BoardFraming.Envelope(level); bounds.center += gameplay.transform.position;
                        var fit = BoardFraming.Fit(bounds,original,camera.transform.right,camera.transform.up,camera.aspect,
                            BoardFraming.GameplayRegion(new Vector2(1080,height),new Rect(0,64,1080,height-160)),PortraitCameraFit.SizeFor(6.25f,camera.aspect));
                        camera.transform.position = fit.CameraPosition; camera.orthographicSize = fit.Size;
                        var coordinates = gameplay.Board.Coordinates; var plane = new Plane(Vector3.up,coordinates.Origin);
                        for (var x = 0; x < level.Width; x++) for (var y = 0; y < level.Height; y++)
                        {
                            var cell = new Vector2Int(x,y); var world = coordinates.CellToWorld(cell); var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(world));
                            Assert.That(plane.Raycast(ray,out var distance), Is.True); Assert.That(coordinates.WorldToCell(ray.GetPoint(distance)), Is.EqualTo(cell));
                        }
                    }
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
            });
        }
    }
}
