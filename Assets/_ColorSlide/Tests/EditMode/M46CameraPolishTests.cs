using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46CameraPolishTests
    {
        internal static readonly Vector3 Up = new Vector3(0, 1, 2) / (float)Math.Sqrt(5);
        internal static readonly Vector3 Forward = new Vector3(0, -2, 1) / (float)Math.Sqrt(5);
        internal static readonly Vector3 Original = new Vector3(0, 12, -6);
        internal static Bounds Tray => new Bounds(new Vector3(0, -0.125f, 0), new Vector3(6, 0.25f, 8));
        internal static readonly Vector2[] Screens = { new Vector2(1080, 1920), new Vector2(1080, 2400), new Vector2(720, 1280) };

        internal static Bounds[] Geometry(LevelData level)
        {
            var origin = M46FramingTests.Origin(level);
            return BoardFraming.PresentationEnvelope(level).Select(local => { local.center += origin; return local; }).Concat(new[] { Tray }).ToArray();
        }
        internal static BoardFraming.Frame Frame(LevelData level, Vector2 screen, Rect safe) =>
            BoardFraming.FitPresentation(BoardFraming.Project(Geometry(level), Vector3.right, Up), Original,
                Vector3.right, Up, screen.x / screen.y, screen, safe);

        [TestCase(0, 1080, 1920)] [TestCase(0, 1080, 2400)] [TestCase(0, 720, 1280)]
        [TestCase(1, 1080, 1920)] [TestCase(1, 1080, 2400)] [TestCase(1, 720, 1280)]
        public void MaximumBoardsUseNearlyAllSafelyAvailableWidthWithoutCroppingDepartures(int index, int width, int height)
        {
            var level = MasterCampaignLevels.Create()[index]; var screen = new Vector2(width, height); var safe = new Rect(0, 0, width, height);
            var geometry = Geometry(level); var frame = Frame(level, screen, safe);
            var region = BoardFraming.PresentationRegion(screen, safe); var core = BoardFraming.GameplayRegion(screen, safe);
            foreach (var box in geometry) Inside(box, frame, screen, region);
            Inside(geometry[0], frame, screen, core); Inside(Tray, frame, screen, core);
            var old = BoardFraming.Envelope(level); old.center += M46FramingTests.Origin(level);
            var oldFrame = BoardFraming.Fit(old, Original, Vector3.right, Up, width / (float)height, core, PortraitCameraFit.SizeFor(6.25f, width / (float)height));
            var trayWidth = 6f / (2f * frame.Size * width / height);
            var gridWidth = level.Width * level.CellSize / (2f * frame.Size * width / height);
            var oldTrayWidth = 6f / (2f * oldFrame.Size * width / height);
            // A two-cell-wide departure on each side requires at least 12*.68 world units.
            // The unchanged 6-unit tray therefore cannot occupy 85% of a fixed viewport.
            var physicalCeiling = 6f / ((level.Width + 4) * level.CellSize);
            Assert.That(physicalCeiling, Is.LessThan(0.85f));
            Assert.That(trayWidth, Is.InRange(physicalCeiling * 0.96f, physicalCeiling));
            Assert.That(trayWidth, Is.GreaterThan(oldTrayWidth * 1.04f));
            var cellPixels = level.CellSize * height / (2f * frame.Size);
            Assert.That(cellPixels * Up.z, Is.GreaterThan(48f), "Minimum vertical cell pitch, including 720x1280.");
            Assert.That(Vector3.Dot(frame.CameraPosition - Original, Forward), Is.EqualTo(0).Within(0.00001f));
            TestContext.WriteLine($"{level.DisplayName} {width}x{height}: tray={trayWidth:P3}, grid={gridWidth:P3}, old tray={oldTrayWidth:P3}, ortho={frame.Size:F6}, pitch={cellPixels:F3}x{cellPixels * Up.z:F3}px");
        }

        [TestCase(0)] [TestCase(1)]
        public void AsymmetricNotchesKeepTrayGatesAndEffectsBelowHudAndInsideSafeArea(int index)
        {
            var level = MasterCampaignLevels.Create()[index]; var geometry = Geometry(level);
            foreach (var screen in Screens)
            {
                var scale = screen.x / 1080f;
                var safe = new Rect(40 * scale, 64 * scale, screen.x - 64 * scale, screen.y - 160 * scale);
                var region = BoardFraming.PresentationRegion(screen, safe); var frame = Frame(level, screen, safe);
                foreach (var box in geometry) Inside(box, frame, screen, region);
                var core = BoardFraming.GameplayRegion(screen, safe);
                Inside(geometry[0], frame, screen, core); Inside(Tray, frame, screen, core);
                var canvasScale = (float)Math.Sqrt(screen.x / 1080f * screen.y / 1920f);
                Assert.That(region.xMin * screen.x, Is.EqualTo(safe.xMin + 8 * canvasScale).Within(0.001));
                Assert.That(region.xMax * screen.x, Is.EqualTo(safe.xMax - 8 * canvasScale).Within(0.001));
                Assert.That(region.yMin * screen.y, Is.EqualTo(safe.yMin + 24 * canvasScale).Within(0.001));
                Assert.That(region.yMax * screen.y, Is.EqualTo(safe.yMax - (280 + 12 + 24) * canvasScale).Within(0.001));
                var trayFraction = 6f / (2f * frame.Size * screen.x / screen.y);
                TestContext.WriteLine($"{level.DisplayName} {screen.x}x{screen.y} inset safe area: tray={trayFraction:P3} of viewport, {trayFraction * screen.x / safe.width:P3} of safe width");
            }
        }

        [Test]
        public void HeightLimitedSafeAreaChoosesCompleteVisibilityOverWidthTarget()
        {
            var level = MasterCampaignLevels.Create()[1]; var screen = Screens[0]; var safe = new Rect(0, 400, 1080, 1200);
            var frame = Frame(level, screen, safe); var region = BoardFraming.PresentationRegion(screen, safe);
            foreach (var box in Geometry(level)) Inside(box, frame, screen, region);
            Assert.That(frame.Size, Is.GreaterThan(Frame(level, screen, new Rect(0, 0, 1080, 1920)).Size));
            var projection = BoardFraming.Project(Geometry(level), Vector3.right, Up);
            Assert.That(frame.Size, Is.EqualTo(projection.height / (2 * region.height) * 1.002f).Within(0.00001));
        }

        [TestCase(0)] [TestCase(1)]
        public void AllLegalExitsKeepFullBlocksAndTheContinuousShrinkingBurstInFrame(int index)
        {
            var level = MasterCampaignLevels.Create()[index]; var origin = M46FramingTests.Origin(level);
            foreach (var screen in Screens)
            {
                var safe = new Rect(0, 0, screen.x, screen.y); var frame = Frame(level, screen, safe);
                var region = BoardFraming.PresentationRegion(screen, safe);
                foreach (var pose in ExitPoses(level))
                {
                    foreach (var cell in pose.Block.Cells)
                    for (var step = 0; step <= 8; step++)
                    {
                        var anchor = Vector3.Lerp(pose.Start, pose.End, SoftBurstMotion.OutwardProgress(step / 8f * SoftBurstMotion.OutwardSeconds));
                        var min = origin + (anchor + new Vector3(cell.x - 0.5f, -0.01f, cell.y - 0.5f)) * level.CellSize;
                        var max = origin + (anchor + new Vector3(cell.x + 0.5f, 0.48f, cell.y + 0.5f)) * level.CellSize;
                        var box = new Bounds(); box.SetMinMax(min, max); Inside(box, frame, screen, region);
                    }
                    foreach (var piece in SoftBurstMotion.Create(pose.Block.Cells, pose.Edge))
                    for (var step = 0; step <= 128; step++)
                    {
                        var u = step / 128f; var center = origin + (pose.End + piece.Position(u)) * level.CellSize;
                        var radius = piece.Scale(u) * level.CellSize * 0.8660254f;
                        // A sphere bounds every rotation of the actual fragment cube.
                        Point(center + Vector3.right * radius, frame, screen, region); Point(center - Vector3.right * radius, frame, screen, region);
                        Point(center + Up * radius, frame, screen, region); Point(center - Up * radius, frame, screen, region);
                    }
                }
            }
        }

        private static IEnumerable<(LevelBlock Block, BoardEdge Edge, Vector3 Start, Vector3 End)> ExitPoses(LevelData level)
        {
            foreach (var block in level.Blocks)
            foreach (var gate in level.Gates.Where(candidate => candidate.Color == block.Color))
            {
                var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                var extent = ExitGate.IsHorizontalEdge(gate.Edge) ? x : y;
                for (var along = gate.Start; along + extent < gate.Start + gate.Span; along++)
                {
                    var start = gate.Edge == BoardEdge.North ? new Vector3(along, 0, level.Height - 1 - y) :
                        gate.Edge == BoardEdge.South ? new Vector3(along, 0, 0) :
                        gate.Edge == BoardEdge.East ? new Vector3(level.Width - 1 - x, 0, along) : new Vector3(0, 0, along);
                    var end = gate.Edge == BoardEdge.North ? new Vector3(along, 0, level.Height) :
                        gate.Edge == BoardEdge.South ? new Vector3(along, 0, -y - 1) :
                        gate.Edge == BoardEdge.East ? new Vector3(level.Width, 0, along) : new Vector3(-x - 1, 0, along);
                    yield return (block, gate.Edge, start, end);
                }
            }
        }
        internal static void Inside(Bounds bounds, BoardFraming.Frame frame, Vector2 screen, Rect region)
        { foreach (var corner in BoardFraming.Corners(bounds)) Point(corner, frame, screen, region); }
        private static void Point(Vector3 point, BoardFraming.Frame frame, Vector2 screen, Rect region)
        {
            var delta = point - frame.CameraPosition;
            Assert.That(0.5f + delta.x / (2 * frame.Size * screen.x / screen.y), Is.InRange(region.xMin - 0.000001f, region.xMax + 0.000001f));
            Assert.That(0.5f + Vector3.Dot(delta, Up) / (2 * frame.Size), Is.InRange(region.yMin - 0.000001f, region.yMax + 0.000001f));
            Assert.That(Vector3.Dot(delta, Forward), Is.InRange(0.1f, 100f));
        }
    }
}
