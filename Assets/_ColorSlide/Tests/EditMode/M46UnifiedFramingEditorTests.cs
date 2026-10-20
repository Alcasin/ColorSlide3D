using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46UnifiedFramingEditorTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void ActualCampaignLevelsKeepTrayGatesTouchRaysAndWitnessBurstsVisible(int index)
        {
            M46EditorTests.WithCampaign((owner, scene) =>
            {
                GameplayController gameplay = null;
                try
                {
                    var level = index < 4 ? owner.Levels[index].Data : M46UnifiedFramingTests.Levels()[index];
                    var camera = owner.Presentation.InteractionCamera; var fitter = camera.GetComponent<PortraitCameraFit>();
                    var position = camera.transform.position; var rotation = camera.transform.rotation; var size = camera.orthographicSize;
                    gameplay = owner.Presentation.Build(level, level.CreateSession());
                    Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.orthographicSize, Is.EqualTo(size));
                    gameplay.gameObject.SetActive(true); Assert.That(fitter.ZoomMultiplier, Is.EqualTo(1.25f));
                    var coordinates = gameplay.Board.Coordinates; var plane = new Plane(Vector3.up, coordinates.Origin);
                    BoardFraming.Frame frame = default; Vector2 screen = default;
                    foreach (var dimensions in M46CameraPolishTests.Screens)
                    {
                        screen = dimensions; var safe = new Rect(0, 0, screen.x, screen.y);
                        camera.aspect = screen.x / screen.y; camera.pixelRect = new Rect(0, 0, screen.x, screen.y);
                        frame = fitter.CalculateFrame(screen, safe, camera.aspect); camera.transform.position = frame.CameraPosition; camera.orthographicSize = frame.Size;
                        var expected = M46UnifiedFramingTests.Frame(level, screen, safe);
                        Assert.That(frame.Size, Is.EqualTo(expected.Size).Within(0.00001));
                        Assert.That((frame.CameraPosition - expected.CameraPosition).sqrMagnitude, Is.LessThan(0.0000001f));
                        Assert.That(camera.transform.rotation, Is.EqualTo(rotation)); Assert.That(camera.orthographic, Is.True);
                        var region = BoardFraming.GameplayRegion(screen, safe);
                        Visible(gameplay.GetComponentsInChildren<MeshRenderer>(true), frame, screen, region);
                        Visible(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                            .Where(renderer => renderer.sharedMaterial == owner.Presentation.Theme.Tray), frame, screen, region);
                        Physics.SyncTransforms();
                        foreach (var collider in gameplay.Board.BlockViews.SelectMany(block => block.GetComponentsInChildren<BoxCollider>(true)))
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
                        TestContext.WriteLine($"Native L{index + 1} {screen.x}x{screen.y}: tray={fitter.TrayViewportUtilization(frame, camera.aspect):P3}");
                    }
                    foreach (var action in Witness(index))
                    {
                        var block = gameplay.Board.BlockViews.Single(view => view.BlockId == action.Block);
                        var move = gameplay.RequestMove(block, action.Target, action.Axis);
                        Assert.That(move.Status, Is.EqualTo(action.Exit ? MovementStatus.Exited : MovementStatus.Moved));
                        if (!action.Exit) { block.SnapTo(coordinates.CellToWorld(action.Target)); continue; }
                        Assert.That(gameplay.Board.Model.IsActive(action.Block), Is.False);
                        Assert.That(block.GetComponentsInChildren<Collider>(true).All(item => !item.enabled), Is.True);
                        var realEnd = coordinates.CellToWorld(move.Exit.EndAnchor);
                        gameplay.AdvanceExitPresentation(0.15f); gameplay.GetComponent<GateGlow>().Refresh();
                        Visible(gameplay.GateViews.SelectMany(gate => gate.GetComponentsInChildren<MeshRenderer>(true)), frame, screen,
                            BoardFraming.GameplayRegion(screen, new Rect(0, 0, screen.x, screen.y)));
                        gameplay.AdvanceExitPresentation(SoftBurstMotion.OutwardSeconds - 0.15f);
                        var fragments = gameplay.transform.Find("Soft Cube Burst"); Assert.That(fragments, Is.Not.Null);
                        Assert.That(fragments.childCount, Is.EqualTo(SoftBurstMotion.FragmentCount)); Assert.That(fragments.position.y, Is.EqualTo(realEnd.y));
                        var pieces = SoftBurstMotion.Create(block.Footprint, move.Exit.Gate.Edge);
                        for (var phase = 0; phase < 2; phase++)
                        {
                            if (phase == 1) gameplay.AdvanceExitPresentation(SoftBurstMotion.ScatterSeconds / 2f);
                            for (var i = 0; i < pieces.Length; i++)
                            {
                                Assert.That((fragments.GetChild(i).localPosition - pieces[i].Position(phase * 0.5f) * level.CellSize).sqrMagnitude, Is.LessThan(0.0000001f));
                                Assert.That(fragments.GetChild(i).GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(block.CellRenderers[0].sharedMaterial));
                            }
                            Visible(fragments.GetComponentsInChildren<MeshRenderer>(true), frame, screen,
                                BoardFraming.PresentationRegion(screen, new Rect(0, 0, screen.x, screen.y)));
                            var stable = fitter.CalculateFrame(screen, new Rect(0, 0, screen.x, screen.y), camera.aspect);
                            Assert.That(stable.CameraPosition, Is.EqualTo(frame.CameraPosition)); Assert.That(stable.Size, Is.EqualTo(frame.Size));
                        }
                        gameplay.AdvanceExitPresentation(SoftBurstMotion.ScatterSeconds / 2f);
                        Assert.That(gameplay.IsPresentingExit, Is.False); Assert.That(block.transform.position, Is.EqualTo(realEnd));
                    }
                    Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Won));
                    Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
                    Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).Count(), Is.EqualTo(1));
                }
                finally { if (gameplay != null) LevelPresentation.Retire(gameplay.gameObject); }
            });
        }
        private static void Visible(System.Collections.Generic.IEnumerable<MeshRenderer> renderers, BoardFraming.Frame frame, Vector2 screen, Rect region)
        {
            foreach (var renderer in renderers.Where(item => item.enabled && item.gameObject.activeInHierarchy))
                M46CameraPolishTests.Inside(renderer.bounds, frame, screen, region);
        }
        private static PuzzleAction[] Witness(int index)
        {
            if (index >= 6) return MasterLevelSolutions.For(index - 6);
            if (index >= 4) return AdvancedLevelSolutions.For(index - 4);
            // Reuse the accepted M3 regression witness instead of introducing another solution catalog.
            var method = typeof(CampaignTests).GetMethod("Solution", BindingFlags.NonPublic | BindingFlags.Static);
            var moves = (IEnumerable)method.Invoke(null, new object[] { index });
            return moves.Cast<object>().Select(move =>
            {
                var type = move.GetType();
                return new PuzzleAction((string)type.GetField("Id").GetValue(move),
                    new Vector2Int((int)type.GetField("X").GetValue(move), (int)type.GetField("Y").GetValue(move)),
                    (GridAxis)type.GetField("Axis").GetValue(move), (bool)type.GetField("Exit").GetValue(move));
            }).ToArray();
        }
    }
}
