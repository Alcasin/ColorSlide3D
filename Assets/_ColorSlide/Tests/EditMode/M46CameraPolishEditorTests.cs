using System.Linq;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46CameraPolishEditorTests
    {
        [Test]
        public void FirstSixProductionLevelsKeepTheirAcceptedFramesAndDoNotEditTheSceneCamera()
        {
            M46EditorTests.WithCampaign((owner, scene) =>
            {
                var camera = owner.Presentation.InteractionCamera; var position = camera.transform.position; var rotation = camera.transform.rotation;
                var size = camera.orthographicSize; var fitter = camera.GetComponent<PortraitCameraFit>();
                foreach (var level in owner.Levels.Select(definition => definition.Data).Concat(AdvancedCampaignLevels.Create()))
                {
                    GameplayController gameplay = null;
                    try
                    {
                        gameplay = owner.Presentation.Build(level, level.CreateSession());
                        foreach (var screen in M46CameraPolishTests.Screens)
                        {
                            var safe = new Rect(0, 64 * screen.x / 1080, screen.x, screen.y - 160 * screen.x / 1080); var aspect = screen.x / screen.y;
                            var minimum = PortraitCameraFit.SizeFor(fitter.ReferenceSize, aspect);
                            var expected = new BoardFraming.Frame(position, minimum);
                            if (level.Width > 6 || level.Height > 8)
                            {
                                var bounds = BoardFraming.Envelope(level); bounds.center += gameplay.transform.position;
                                expected = BoardFraming.Fit(bounds, position, camera.transform.right, camera.transform.up, aspect,
                                    BoardFraming.GameplayRegion(screen, safe), minimum);
                            }
                            var actual = fitter.CalculateFrame(screen, safe, aspect);
                            Assert.That(actual.Size, Is.EqualTo(expected.Size)); Assert.That(actual.CameraPosition, Is.EqualTo(expected.CameraPosition));
                        }
                        Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
                        Assert.That(camera.orthographicSize, Is.EqualTo(size));
                    }
                    finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
                }
            });
        }

        [TestCase(0)] [TestCase(1)]
        public void ProductionFrameKeepsRealRenderersExitsAndNativePointerRaysValid(int index)
        {
            M46EditorTests.WithCampaign((owner, scene) =>
            {
                GameplayController gameplay = null;
                try
                {
                    var level = MasterCampaignLevels.Create()[index]; gameplay = owner.Presentation.Build(level, level.CreateSession());
                    gameplay.gameObject.SetActive(true);
                    var camera = owner.Presentation.InteractionCamera; var fitter = camera.GetComponent<PortraitCameraFit>();
                    // Retain the full-departure regression at the explicitly supported 1x safe setting.
                    fitter.SetEditorZoom(1f);
                    var rotation = camera.transform.rotation; var coordinates = gameplay.Board.Coordinates; var plane = new Plane(Vector3.up, coordinates.Origin);
                    BoardFraming.Frame frame = default; Vector2 screen = default; Rect region = default;
                    foreach (var dimensions in M46CameraPolishTests.Screens)
                    {
                        screen = dimensions; var safe = new Rect(0, 0, screen.x, screen.y); camera.aspect = screen.x / screen.y;
                        camera.pixelRect = new Rect(0, 0, screen.x, screen.y);
                        frame = fitter.CalculateFrame(screen, safe, camera.aspect);
                        camera.transform.position = frame.CameraPosition; camera.orthographicSize = frame.Size;
                        Assert.That(camera.orthographic, Is.True); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
                        region = BoardFraming.PresentationRegion(screen, safe);
                        Visible(gameplay.GetComponentsInChildren<MeshRenderer>(true), frame, screen, region);
                        Visible(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                            .Where(renderer => renderer.sharedMaterial == owner.Presentation.Theme.Tray), frame, screen, BoardFraming.GameplayRegion(screen, safe));
                        var gateRegion = BoardFraming.GameplayRegion(screen, safe);
                        Visible(gameplay.GateViews.SelectMany(gate => gate.GetComponentsInChildren<MeshRenderer>(true)), frame, screen, gateRegion);
                        Physics.SyncTransforms();
                        foreach (var collider in gameplay.Board.BlockViews.SelectMany(block => block.GetComponentsInChildren<BoxCollider>(true)))
                        {
                            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(collider.bounds.center));
                            // Query only the owned collider: an additive user scene may occupy the
                            // same world positions and must never be disabled to make a test pass.
                            Assert.That(collider.Raycast(ray, out var hit, camera.farClipPlane), Is.True);
                            Assert.That(hit.collider, Is.SameAs(collider));
                        }
                        for (var x = 0; x < level.Width; x++) for (var y = 0; y < level.Height; y++)
                        foreach (var offset in new[] { Vector3.zero, new Vector3(0.4f, 0, -0.4f), new Vector3(-0.4f, 0, 0.4f) })
                        {
                            var cell = new Vector2Int(x, y); var world = coordinates.CellToWorld(cell) + offset * level.CellSize;
                            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(world));
                            Assert.That(plane.Raycast(ray, out var distance), Is.True);
                            Assert.That(coordinates.WorldToCell(ray.GetPoint(distance)), Is.EqualTo(cell));
                        }
                    }
                    // Real production exit ownership, renderers and mesh bounds. Advance through the
                    // existing public clock, never invoke Awake/OnEnable/Update or SendMessage manually.
                    foreach (var action in MasterLevelSolutions.For(index))
                    {
                        var block = gameplay.Board.BlockViews.Single(view => view.BlockId == action.Block);
                        var result = gameplay.RequestMove(block, action.Target, action.Axis);
                        Assert.That(result.Status, Is.EqualTo(action.Exit ? ColorSlide.Board.MovementStatus.Exited : ColorSlide.Board.MovementStatus.Moved));
                        if (!action.Exit) { block.SnapTo(coordinates.CellToWorld(action.Target)); continue; }
                        var previousAge = 0f;
                        foreach (var age in new[] { 0.09f, 0.179f, SoftBurstMotion.OutwardSeconds, 0.24f, 0.33f, SoftBurstMotion.Duration })
                        {
                            gameplay.AdvanceExitPresentation(age - previousAge); previousAge = age;
                            gameplay.GetComponent<GateGlow>().Refresh();
                            Visible(gameplay.GetComponentsInChildren<MeshRenderer>(true), frame, screen, region);
                        }
                        Assert.That(gameplay.IsPresentingExit, Is.False);
                    }
                    Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
            });
        }
        private static void Visible(System.Collections.Generic.IEnumerable<MeshRenderer> renderers, BoardFraming.Frame frame, Vector2 screen, Rect region)
        {
            foreach (var renderer in renderers.Where(item => item.enabled && item.gameObject.activeInHierarchy))
                M46CameraPolishTests.Inside(renderer.bounds, frame, screen, region);
        }
    }
}
