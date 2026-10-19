using System.Linq;
using System.Reflection;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46ZoomEditorTests
    {
        private static BoardFraming.Frame Apply(Camera camera, PortraitCameraFit fitter, Vector2 screen)
        {
            camera.aspect = screen.x / screen.y; camera.pixelRect = new Rect(0, 0, screen.x, screen.y);
            var frame = fitter.CalculateFrame(screen, new Rect(0, 0, screen.x, screen.y), camera.aspect);
            camera.transform.position = frame.CameraPosition; camera.orthographicSize = frame.Size;
            return frame;
        }
        private static void Visible(System.Collections.Generic.IEnumerable<MeshRenderer> renderers, BoardFraming.Frame frame, Vector2 screen, Rect region)
        {
            foreach (var renderer in renderers.Where(item => item.enabled && item.gameObject.activeInHierarchy))
                M46CameraPolishTests.Inside(renderer.bounds, frame, screen, region);
        }

        [TestCase(0)] [TestCase(1)]
        public void DefaultAndExploratoryZoomPreserveRealBoardGatesRaysAndSuccessfulExitOwnership(int index)
        {
            M46EditorTests.WithCampaign((owner, scene) =>
            {
                GameplayController gameplay = null;
                try
                {
                    var level = MasterCampaignLevels.Create()[index]; gameplay = owner.Presentation.Build(level, level.CreateSession()); gameplay.gameObject.SetActive(true);
                    var camera = owner.Presentation.InteractionCamera; var fitter = camera.GetComponent<PortraitCameraFit>();
                    Assert.That(fitter.LargeBoardZoom, Is.EqualTo(1.25f)); Assert.That(fitter.ZoomMultiplier, Is.EqualTo(1.25f));
                    Apply(camera, fitter, M46CameraPolishTests.Screens[0]);
                    Assert.That(fitter.EffectiveZoom, Is.EqualTo(1.25f).Within(0.00001f));
                    var rotation = camera.transform.rotation; var coordinates = gameplay.Board.Coordinates; var plane = new Plane(Vector3.up, coordinates.Origin);
                    BoardFraming.Frame frame = default; Vector2 screen = default;
                    foreach (var dimensions in M46CameraPolishTests.Screens)
                    foreach (var zoom in new[] { 1.18f, 1.25f })
                    {
                        screen = dimensions; fitter.SetEditorZoom(zoom); frame = Apply(camera, fitter, screen);
                        var region = BoardFraming.GameplayRegion(screen, new Rect(0, 0, screen.x, screen.y));
                        Assert.That(camera.orthographic, Is.True); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
                        Assert.That(fitter.EffectiveZoom, Is.EqualTo(zoom).Within(0.00001f));
                        Visible(gameplay.GetComponentsInChildren<MeshRenderer>(true), frame, screen, region);
                        Visible(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                            .Where(renderer => renderer.sharedMaterial == owner.Presentation.Theme.Tray), frame, screen, region);
                        Physics.SyncTransforms();
                        foreach (var collider in gameplay.Board.BlockViews.SelectMany(view => view.GetComponentsInChildren<BoxCollider>(true)))
                        {
                            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(collider.bounds.center));
                            Assert.That(collider.Raycast(ray, out var hit, camera.farClipPlane), Is.True); Assert.That(hit.collider, Is.SameAs(collider));
                        }
                        for (var x = 0; x < level.Width; x++) for (var y = 0; y < level.Height; y++)
                        foreach (var offset in new[] { Vector3.zero, new Vector3(-0.4f, 0, 0.4f), new Vector3(0.4f, 0, -0.4f) })
                        {
                            var cell = new Vector2Int(x, y); var world = coordinates.CellToWorld(cell) + offset * level.CellSize;
                            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(world));
                            Assert.That(plane.Raycast(ray, out var distance), Is.True); Assert.That(coordinates.WorldToCell(ray.GetPoint(distance)), Is.EqualTo(cell));
                        }
                    }
                    var relocated = false;
                    foreach (var action in MasterLevelSolutions.For(index))
                    {
                        var block = gameplay.Board.BlockViews.Single(view => view.BlockId == action.Block);
                        var move = gameplay.RequestMove(block, action.Target, action.Axis);
                        Assert.That(move.Status, Is.EqualTo(action.Exit ? ColorSlide.Board.MovementStatus.Exited : ColorSlide.Board.MovementStatus.Moved));
                        if (!action.Exit) { block.SnapTo(coordinates.CellToWorld(action.Target)); continue; }
                        Assert.That(gameplay.Board.Model.IsActive(action.Block), Is.False); Assert.That(gameplay.CanInteract, Is.False);
                        var realEnd = coordinates.CellToWorld(move.Exit.EndAnchor);
                        gameplay.AdvanceExitPresentation(0.15f); gameplay.GetComponent<GateGlow>().Refresh();
                        Visible(gameplay.GateViews.SelectMany(gate => gate.GetComponentsInChildren<MeshRenderer>(true)), frame, screen,
                            BoardFraming.GameplayRegion(screen, new Rect(0, 0, screen.x, screen.y)));
                        gameplay.AdvanceExitPresentation(SoftBurstMotion.OutwardSeconds - 0.15f);
                        var fragments = gameplay.transform.Find("Soft Cube Burst"); Assert.That(fragments, Is.Not.Null);
                        Assert.That(fragments.childCount, Is.EqualTo(10)); Assert.That(fragments.position.y, Is.EqualTo(realEnd.y));
                        relocated |= (fragments.position - realEnd).sqrMagnitude > 0.00001f;
                        var pieces = SoftBurstMotion.Create(block.Footprint, move.Exit.Gate.Edge);
                        for (var phase = 0; phase < 2; phase++)
                        {
                            if (phase == 1) gameplay.AdvanceExitPresentation(SoftBurstMotion.ScatterSeconds / 2f);
                            var progress = phase * 0.5f;
                            for (var i = 0; i < pieces.Length; i++)
                            {
                                Assert.That((fragments.GetChild(i).localPosition - pieces[i].Position(progress) * level.CellSize).sqrMagnitude, Is.LessThan(0.0000001f));
                                Assert.That(fragments.GetChild(i).GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(block.CellRenderers[0].sharedMaterial));
                            }
                            Visible(fragments.GetComponentsInChildren<MeshRenderer>(true), frame, screen,
                                BoardFraming.PresentationRegion(screen, new Rect(0, 0, screen.x, screen.y)));
                            var stable = fitter.CalculateFrame(screen, new Rect(0, 0, screen.x, screen.y), camera.aspect);
                            Assert.That(stable.Size, Is.EqualTo(frame.Size)); Assert.That(stable.CameraPosition, Is.EqualTo(frame.CameraPosition));
                        }
                        gameplay.AdvanceExitPresentation(SoftBurstMotion.ScatterSeconds / 2f);
                        Assert.That(gameplay.IsPresentingExit, Is.False); Assert.That(block.transform.position, Is.EqualTo(realEnd));
                    }
                    Assert.That(relocated, Is.True); Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
            });
        }

        [Test]
        public void EditorOverrideSurvivesFiveThroughEightReplacementsWithoutSerializingItOrChangingDefaults()
        {
            M46EditorTests.WithCampaign((owner, scene) =>
            {
                var fitter = owner.Presentation.InteractionCamera.GetComponent<PortraitCameraFit>(); var serialized = EditorJsonUtility.ToJson(fitter);
                var campaign = owner.CreateCampaign(); GameplayController current = null;
                try
                {
                    fitter.SetEditorZoom(1.21f);
                    foreach (var index in new[] { 7, 4, 5, 6, 7 })
                    {
                        GameplayController replacement = null;
                        Assert.That(campaign.StartEditorLevel(index, (level, session) => replacement = owner.Presentation.Build(level, session)), Is.True);
                        if (current != null) LevelPresentation.Retire(current.gameObject); current = replacement;
                        Assert.That(fitter.ZoomMultiplier, Is.EqualTo(1.21f));
                        var screen = M46CameraPolishTests.Screens[0]; var frame = fitter.CalculateFrame(screen, new Rect(0, 0, screen.x, screen.y), screen.x / screen.y);
                        Assert.That(fitter.EffectiveZoom, Is.EqualTo(1.21f).Within(0.00001f));
                        Assert.That(frame.Size, Is.GreaterThan(0)); Assert.That(campaign.CurrentSession.State, Is.EqualTo(GameplayState.Playing));
                    }
                    fitter.ResetEditorZoom(); Assert.That(fitter.ZoomMultiplier, Is.EqualTo(1.25f));
                    Assert.That(campaign.StartEditorLevel(4, (level, session) =>
                    {
                        var replacement = owner.Presentation.Build(level, session);
                        LevelPresentation.Retire(current.gameObject); current = replacement;
                    }), Is.True);
                    Assert.That(fitter.ZoomMultiplier, Is.EqualTo(1f)); Assert.That(EditorJsonUtility.ToJson(fitter), Is.EqualTo(serialized));
                }
                finally { if (current != null) LevelPresentation.Retire(current.gameObject); }
            });
        }

        [Test]
        public void ZoomChangesWaitUntilSelectionOrExitFinishes()
        {
            M46EditorTests.WithCampaign((owner, scene) =>
            {
                GameplayController gameplay = null;
                try
                {
                    var level = MasterCampaignLevels.Create()[1]; gameplay = owner.Presentation.Build(level, level.CreateSession()); gameplay.gameObject.SetActive(true);
                    var camera = owner.Presentation.InteractionCamera; var fitter = camera.GetComponent<PortraitCameraFit>(); var screen = M46CameraPolishTests.Screens[0];
                    fitter.SetEditorZoom(1.18f); // A distinct starting value keeps the deferred-change regression meaningful.
                    var initial = Apply(camera, fitter, screen); var selected = gameplay.Board.BlockViews[0]; selected.SetSelected(true);
                    fitter.SetEditorZoom(1.25f); var held = Apply(camera, fitter, screen);
                    Assert.That(fitter.ZoomPending, Is.True); Assert.That(held.Size, Is.EqualTo(initial.Size)); Assert.That(held.CameraPosition, Is.EqualTo(initial.CameraPosition));
                    selected.SetSelected(false); var enlarged = Apply(camera, fitter, screen);
                    Assert.That(fitter.ZoomPending, Is.False); Assert.That(enlarged.Size, Is.LessThan(initial.Size));
                    var checkedExit = false;
                    foreach (var action in MasterLevelSolutions.For(1))
                    {
                        var block = gameplay.Board.BlockViews.Single(view => view.BlockId == action.Block);
                        var move = gameplay.RequestMove(block, action.Target, action.Axis);
                        Assert.That(move.Status, Is.EqualTo(action.Exit ? ColorSlide.Board.MovementStatus.Exited : ColorSlide.Board.MovementStatus.Moved));
                        if (!action.Exit) { block.SnapTo(gameplay.Board.Coordinates.CellToWorld(action.Target)); continue; }
                        fitter.SetEditorZoom(1f); held = Apply(camera, fitter, screen);
                        Assert.That(fitter.ZoomPending, Is.True); Assert.That(held.Size, Is.EqualTo(enlarged.Size)); Assert.That(held.CameraPosition, Is.EqualTo(enlarged.CameraPosition));
                        gameplay.AdvanceExitPresentation(SoftBurstMotion.Duration);
                        var safe = Apply(camera, fitter, screen); Assert.That(fitter.ZoomPending, Is.False);
                        Assert.That(safe.Size / enlarged.Size, Is.EqualTo(1.25f).Within(0.00001f)); checkedExit = true; break;
                    }
                    Assert.That(checkedExit, Is.True);
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
            });
        }

        [TestCase(Key.Equals, false, false, 1)] [TestCase(Key.Equals, true, false, 1)]
        [TestCase(Key.NumpadPlus, false, false, 1)] [TestCase(Key.Minus, false, false, -1)]
        [TestCase(Key.NumpadMinus, false, false, -1)] [TestCase(Key.Equals, false, true, 0)]
        public void RealInputSystemKeyboardSupportsPlusEqualsMinusNumpadAndModifierRules(Key key, bool shift, bool control, int expected)
        {
            var previous = Keyboard.current; Keyboard owned = null;
            try
            {
                owned = InputSystem.AddDevice<Keyboard>();
                var keys = new[] { key }.Concat(shift ? new[] { Key.LeftShift } : new Key[0]).Concat(control ? new[] { Key.LeftCtrl } : new Key[0]).ToArray();
                InputSystem.QueueStateEvent(owned, new KeyboardState(keys)); InputSystem.Update();
                var method = typeof(M45Feedback).Assembly.GetType("ColorSlide.Editor.CampaignPlayModeShortcuts").GetMethod("ZoomDirection", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null); Assert.That(method.Invoke(null, new object[] { owned }), Is.EqualTo(expected));
            }
            finally
            {
                if (owned != null && owned.added) InputSystem.RemoveDevice(owned);
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }
    }
}
