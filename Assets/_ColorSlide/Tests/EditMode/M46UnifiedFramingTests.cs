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
    public sealed class M46UnifiedFramingTests
    {
        internal static LevelData[] Levels() => M3AuthoredLevels.Create().Concat(AdvancedCampaignLevels.Create()).Concat(MasterCampaignLevels.Create()).ToArray();
        internal static BoardFraming.Frame Baseline(LevelData level, Vector2 screen, Rect safe)
        {
            var aspect = screen.x / screen.y; var minimum = PortraitCameraFit.SizeFor(6.25f, aspect);
            if (level.Height > 10) return M46CameraPolishTests.Frame(level, screen, safe);
            if (level.Width <= 6 && level.Height <= 8) return new BoardFraming.Frame(M46CameraPolishTests.Original, minimum);
            var bounds = BoardFraming.Envelope(level); bounds.center += M46FramingTests.Origin(level);
            return BoardFraming.Fit(bounds, M46CameraPolishTests.Original, Vector3.right, M46CameraPolishTests.Up, aspect,
                BoardFraming.GameplayRegion(screen, safe), minimum);
        }
        internal static BoardFraming.Frame Previous(LevelData level, Vector2 screen, Rect safe)
        {
            var baseline = Baseline(level, screen, safe);
            return level.Height > 10 ? BoardFraming.Zoom(baseline, Projection(level), 1.25f, Vector3.right, M46CameraPolishTests.Up,
                screen.x / screen.y, screen, safe) : baseline;
        }
        internal static Rect Projection(LevelData level) => BoardFraming.Project(M46ZoomTests.Core(level), Vector3.right, M46CameraPolishTests.Up);
        internal static BoardFraming.Frame Frame(LevelData level, Vector2 screen, Rect safe, float control = 1.25f) =>
            BoardFraming.AdaptiveZoom(Baseline(level, screen, safe), Projection(level),
                BoardFraming.Project(new[] { M46CameraPolishTests.Tray }, Vector3.right, M46CameraPolishTests.Up), control,
                Vector3.right, M46CameraPolishTests.Up, screen.x / screen.y, screen, safe);
        internal static float Utilization(BoardFraming.Frame frame, Vector2 screen) => 6f / (2f * frame.Size * screen.x / screen.y);

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryLevelUsesConsistentTrayWidthAndMastersKeepTheirApprovedPositionAndSize(int index)
        {
            var level = Levels()[index];
            foreach (var screen in M46CameraPolishTests.Screens)
            {
                var safe = new Rect(0, 0, screen.x, screen.y); var before = Previous(level, screen, safe); var after = Frame(level, screen, safe);
                foreach (var box in M46ZoomTests.Core(level)) M46CameraPolishTests.Inside(box, after, screen, BoardFraming.GameplayRegion(screen, safe));
                var width = Utilization(after, screen);
                Assert.That(width, Is.InRange(0.895f, BoardFraming.MaximumTrayWidth));
                Assert.That(Vector3.Dot(after.CameraPosition - M46CameraPolishTests.Original, M46CameraPolishTests.Forward), Is.EqualTo(0f).Within(0.00001));
                if (index < 6)
                {
                    Assert.That(width, Is.EqualTo(0.9f).Within(0.00001)); Assert.That(after.Size, Is.LessThan(before.Size));
                    var centerY = 0.5f + (Projection(level).center.y - Vector3.Dot(after.CameraPosition, M46CameraPolishTests.Up)) / (2f * after.Size);
                    Assert.That(centerY, Is.EqualTo(BoardFraming.GameplayRegion(screen, safe).center.y).Within(0.00001), "Balanced below the same HUD-safe region.");
                }
                else { Assert.That(after.Size, Is.EqualTo(before.Size)); Assert.That(after.CameraPosition, Is.EqualTo(before.CameraPosition)); }
                if (index < 4) Assert.That(Baseline(level, screen, safe).Size / after.Size, Is.LessThan(1.1f), "Adaptive size, not a blind 1.25 physical zoom.");
                var pitch = level.CellSize * screen.y / (2f * after.Size);
                Assert.That(pitch * M46CameraPolishTests.Up.z, Is.GreaterThan(60f));
                TestContext.WriteLine($"L{index + 1} {level.DisplayName} {screen.x}x{screen.y}: {Utilization(before, screen):P3} -> {width:P3}; ortho={after.Size:F6}; effective={Baseline(level, screen, safe).Size / after.Size:F6}; pitch={pitch:F3}x{pitch * M46CameraPolishTests.Up.z:F3}px");
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryAdaptiveControlRespectsNotchesHudAndHeightLimitsWithoutZoomJitter(int index)
        {
            var level = Levels()[index];
            foreach (var screen in M46CameraPolishTests.Screens)
            foreach (var limited in new[] { false, true })
            {
                var scale = screen.x / 1080f;
                var safe = limited ? new Rect(0, 400 * scale, screen.x, 800 * scale) :
                    new Rect(40 * scale, 64 * scale, screen.x - 64 * scale, screen.y - 160 * scale);
                var previousSize = float.PositiveInfinity;
                foreach (var control in new[] { 1.01f, 1.18f, 1.25f })
                {
                    var frame = Frame(level, screen, safe, control);
                    foreach (var box in M46ZoomTests.Core(level)) M46CameraPolishTests.Inside(box, frame, screen, BoardFraming.GameplayRegion(screen, safe));
                    Assert.That(frame.Size, Is.LessThanOrEqualTo(previousSize + 0.00001f)); previousSize = frame.Size;
                    var repeated = Frame(level, screen, safe, control);
                    Assert.That(repeated.Size, Is.EqualTo(frame.Size)); Assert.That(repeated.CameraPosition, Is.EqualTo(frame.CameraPosition));
                }
                var close = Frame(level, screen, safe);
                var safeFraction = safe.width / screen.x;
                Assert.That(Utilization(close, screen) / safeFraction, Is.LessThanOrEqualTo(BoardFraming.MaximumTrayWidth + 0.00001));
                if (limited) Assert.That(Utilization(close, screen) / safeFraction, Is.LessThan(0.88f), "Report reduced width when board/tray height is the hard constraint.");
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryFootprintAndGateAlignmentKeepsItsWholeCosmeticBurstReadable(int index)
        {
            var level = Levels()[index]; var origin = M46FramingTests.Origin(level);
            foreach (var screen in M46CameraPolishTests.Screens)
            {
                var safe = new Rect(0, 0, screen.x, screen.y); var frame = Frame(level, screen, safe);
                var region = BoardFraming.PresentationRegion(screen, safe);
                foreach (var block in level.Blocks)
                foreach (var gate in level.Gates.Where(item => item.Color == block.Color))
                {
                    var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                    var extent = ExitGate.IsHorizontalEdge(gate.Edge) ? x : y;
                    for (var along = gate.Start; along + extent < gate.Start + gate.Span; along++)
                    {
                        var anchor = gate.Edge == BoardEdge.North ? new Vector3(along, 0, level.Height) :
                            gate.Edge == BoardEdge.South ? new Vector3(along, 0, -y - 1) :
                            gate.Edge == BoardEdge.East ? new Vector3(level.Width, 0, along) : new Vector3(-x - 1, 0, along);
                        var end = origin + anchor * level.CellSize; var pieces = SoftBurstMotion.Create(block.Cells, gate.Edge);
                        var projection = BoardFraming.Project(BoardFraming.BurstEnvelope(pieces, level.CellSize, Vector3.zero), Vector3.right, M46CameraPolishTests.Up);
                        var cosmetic = BoardFraming.VisibleBurstOrigin(projection, end, frame, Vector3.right, M46CameraPolishTests.Up, screen.x / screen.y, region);
                        Assert.That(cosmetic.y, Is.EqualTo(end.y));
                        foreach (var box in BoardFraming.BurstEnvelope(pieces, level.CellSize, Vector3.zero))
                        { var world = box; world.center += cosmetic; M46CameraPolishTests.Inside(world, frame, screen, region); }
                    }
                }
            }
        }
    }
}
