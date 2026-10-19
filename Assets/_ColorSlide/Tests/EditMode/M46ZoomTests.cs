using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46ZoomTests
    {
        internal static Bounds[] Core(LevelData level)
        {
            var bounds = BoardFraming.PlayableEnvelope(level); bounds.center += M46FramingTests.Origin(level);
            return new[] { bounds, M46CameraPolishTests.Tray };
        }
        internal static BoardFraming.Frame Frame(LevelData level, Vector2 screen, Rect safe, float zoom)
        {
            var baseline = M46CameraPolishTests.Frame(level, screen, safe);
            return BoardFraming.Zoom(baseline, BoardFraming.Project(Core(level), Vector3.right, M46CameraPolishTests.Up), zoom,
                Vector3.right, M46CameraPolishTests.Up, screen.x / screen.y, screen, safe);
        }

        [TestCase(0, 1080, 1920)] [TestCase(0, 1080, 2400)] [TestCase(0, 720, 1280)]
        [TestCase(1, 1080, 1920)] [TestCase(1, 1080, 2400)] [TestCase(1, 720, 1280)]
        public void PreferredAndMaximumZoomEnlargeTrayWhileKeepingPlayableBoundsBelowHud(int index, int width, int height)
        {
            var level = MasterCampaignLevels.Create()[index]; var screen = new Vector2(width, height); var safe = new Rect(0, 0, width, height);
            var baseline = Frame(level, screen, safe, 1f);
            Assert.That(BoardFraming.DefaultLargeBoardZoom, Is.EqualTo(1.25f));
            foreach (var zoom in new[] { 1.18f, BoardFraming.DefaultLargeBoardZoom })
            {
                var frame = Frame(level, screen, safe, zoom); var region = BoardFraming.GameplayRegion(screen, safe);
                foreach (var bounds in Core(level)) M46CameraPolishTests.Inside(bounds, frame, screen, region);
                Assert.That(frame.Size, Is.EqualTo(baseline.Size / zoom).Within(0.00001));
                Assert.That(Vector3.Dot(frame.CameraPosition - baseline.CameraPosition, M46CameraPolishTests.Forward), Is.EqualTo(0).Within(0.00001));
                var utilization = 6f / (2f * frame.Size * width / height);
                Assert.That(utilization, Is.InRange(zoom < 1.2f ? 0.85f : 0.90f, zoom < 1.2f ? 0.86f : 0.91f));
                var pitch = level.CellSize * height / (2 * frame.Size);
                Assert.That(pitch * M46CameraPolishTests.Up.z, Is.GreaterThan(60f));
                TestContext.WriteLine($"{level.DisplayName} {width}x{height} {zoom:F2}x: tray={utilization:P3}, grid={utilization * level.Width * level.CellSize / 6:P3}, ortho={frame.Size:F6}, pitch={pitch:F3}x{pitch * M46CameraPolishTests.Up.z:F3}px");
            }
        }

        [TestCase(1f)] [TestCase(1.18f)] [TestCase(1.25f)]
        public void MultiplierDividesSizeAndOneIsTheExactExistingSafeFrame(float zoom)
        {
            var level = MasterCampaignLevels.Create()[1]; var screen = M46CameraPolishTests.Screens[0]; var safe = new Rect(0, 0, screen.x, screen.y);
            var baseline = M46CameraPolishTests.Frame(level, screen, safe); var frame = Frame(level, screen, safe, zoom);
            Assert.That(frame.Size, Is.EqualTo(baseline.Size / zoom));
            if (zoom == 1f) Assert.That(frame.CameraPosition, Is.EqualTo(baseline.CameraPosition));
        }

        [TestCase(0f, 1f)] [TestCase(0.9f, 1f)] [TestCase(1f, 1f)]
        [TestCase(1.18f, 1.18f)] [TestCase(1.25f, 1.25f)] [TestCase(2f, 1.25f)]
        public void ConfigurationUsesSupportedFiniteZoomRange(float requested, float expected) => Assert.That(BoardFraming.ClampZoom(requested), Is.EqualTo(expected));
        [Test]
        public void NonFiniteZoomIsRejected()
        {
            foreach (var zoom in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => BoardFraming.ClampZoom(zoom));
        }

        [TestCase(0)] [TestCase(1)]
        public void NotchesAndShortSafeAreasClampOnlyToThePlayableGeometry(int index)
        {
            var level = MasterCampaignLevels.Create()[index];
            foreach (var screen in M46CameraPolishTests.Screens)
            foreach (var heightLimited in new[] { false, true })
            {
                var scale = screen.x / 1080f;
                var safe = heightLimited ? new Rect(0, 400 * scale, screen.x, 800 * scale) :
                    new Rect(40 * scale, 64 * scale, screen.x - 64 * scale, screen.y - 160 * scale);
                var baseline = Frame(level, screen, safe, 1f); var frame = Frame(level, screen, safe, 1.25f);
                foreach (var bounds in Core(level)) M46CameraPolishTests.Inside(bounds, frame, screen, BoardFraming.GameplayRegion(screen, safe));
                Assert.That(frame.Size, Is.GreaterThanOrEqualTo(baseline.Size / 1.25f));
                if (heightLimited && screen.x / screen.y > 0.5f)
                    Assert.That(baseline.Size / frame.Size, Is.LessThan(1.25f), "HUD/board height, never old exit width, limits this case.");
            }
        }

        [TestCase(0)] [TestCase(1)]
        public void SuccessfulExitsCanLeaveViewportWhileTheirUnchangedBurstRemainsReadable(int index)
        {
            var level = MasterCampaignLevels.Create()[index]; var origin = M46FramingTests.Origin(level);
            foreach (var screen in M46CameraPolishTests.Screens)
            foreach (var zoom in new[] { 1.18f, 1.25f })
            {
                var safe = new Rect(0, 0, screen.x, screen.y); var frame = Frame(level, screen, safe, zoom);
                var region = BoardFraming.PresentationRegion(screen, safe); var session = level.CreateSession(); session.Start();
                var clippedDeparture = false; var relocatedBurst = false;
                foreach (var action in MasterLevelSolutions.For(index))
                {
                    var move = session.MoveTowards(action.Block, action.Target, action.Axis);
                    Assert.That(move.Status, Is.EqualTo(action.Exit ? MovementStatus.Exited : MovementStatus.Moved));
                    if (!action.Exit) continue;
                    Assert.That(session.Board.IsActive(action.Block), Is.False, "Only a successfully removed block can be clipped.");
                    var block = level.Blocks.Single(candidate => candidate.Id == action.Block);
                    var end = origin + new Vector3(move.Exit.EndAnchor.x, 0, move.Exit.EndAnchor.y) * level.CellSize;
                    var pieces = SoftBurstMotion.Create(block.Cells, move.Exit.Gate.Edge);
                    var projection = BoardFraming.Project(BoardFraming.BurstEnvelope(pieces, level.CellSize, Vector3.zero), Vector3.right, M46CameraPolishTests.Up);
                    var cosmetic = BoardFraming.VisibleBurstOrigin(projection, end, frame, Vector3.right, M46CameraPolishTests.Up, screen.x / screen.y, region);
                    Assert.That(cosmetic.y, Is.EqualTo(end.y)); relocatedBurst |= (cosmetic - end).sqrMagnitude > 0.00001f;
                    foreach (var cell in block.Cells)
                    {
                        var center = end + new Vector3(cell.x, 0.23f, cell.y) * level.CellSize;
                        var horizontal = 0.5f + (center.x - frame.CameraPosition.x) / (2 * frame.Size * screen.x / screen.y);
                        clippedDeparture |= horizontal < 0f || horizontal > 1f;
                    }
                    foreach (var piece in pieces)
                    for (var step = 0; step <= 64; step++)
                    {
                        var u = step / 64f; var point = cosmetic + piece.Position(u) * level.CellSize;
                        var radius = piece.Scale(u) * level.CellSize * 0.8660254f;
                        foreach (var axis in new[] { Vector3.right, M46CameraPolishTests.Up })
                        foreach (var sign in new[] { -1, 1 })
                        {
                            var delta = point + axis * radius * sign - frame.CameraPosition;
                            Assert.That(0.5f + delta.x / (2 * frame.Size * screen.x / screen.y), Is.InRange(region.xMin - 0.000001f, region.xMax + 0.000001f));
                            Assert.That(0.5f + Vector3.Dot(delta, M46CameraPolishTests.Up) / (2 * frame.Size), Is.InRange(region.yMin - 0.000001f, region.yMax + 0.000001f));
                        }
                    }
                }
                Assert.That(session.State, Is.EqualTo(GameplayState.Won));
                Assert.That(clippedDeparture, Is.True); Assert.That(relocatedBurst, Is.True);
            }
        }
    }
}
