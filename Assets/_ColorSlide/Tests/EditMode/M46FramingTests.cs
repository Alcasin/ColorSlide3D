using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46FramingTests
    {
        [TestCase(0,1920)] [TestCase(0,2340)] [TestCase(0,2400)]
        [TestCase(1,1920)] [TestCase(1,2340)] [TestCase(1,2400)]
        public void EntireEnvelopeFitsBelowHudInsidePortraitSafeAreaWithoutChangingPerspective(int index, int height)
        {
            var level = AdvancedCampaignLevels.Create()[index]; var screen = new Vector2(1080,height);
            var safe = new Rect(0,64,1080,height-160); var region = BoardFraming.GameplayRegion(screen,safe);
            var root = Origin(level); var envelope = BoardFraming.Envelope(level); envelope.center += root;
            var right = Vector3.right; var up = new Vector3(0,1,2) / (float)Math.Sqrt(5);
            var forward = new Vector3(0,-2,1) / (float)Math.Sqrt(5); var original = new Vector3(0,12,-6);
            var aspect = screen.x / screen.y;
            var frame = BoardFraming.Fit(envelope,original,right,up,aspect,region,PortraitCameraFit.SizeFor(6.25f,aspect));
            foreach (var corner in BoardFraming.Corners(envelope))
            {
                var relative = corner - frame.CameraPosition;
                var viewport = new Vector2(0.5f + Vector3.Dot(relative,right) / (2f * frame.Size * aspect),
                    0.5f + Vector3.Dot(relative,up) / (2f * frame.Size));
                Assert.That(viewport.x, Is.InRange(region.xMin,region.xMax)); Assert.That(viewport.y, Is.InRange(region.yMin,region.yMax));
                Assert.That(Vector3.Dot(relative,forward), Is.InRange(0.1f,100f));
            }
            // The accepted rounded tray is 6x8 world units at y=-.125, independent of grid dimensions.
            var tray = new Bounds(new Vector3(0,-0.125f,0),new Vector3(6,0.25f,8));
            foreach (var corner in BoardFraming.Corners(tray))
            {
                var relative = corner-frame.CameraPosition;
                Assert.That(0.5f+Vector3.Dot(relative,right)/(2f*frame.Size*aspect), Is.InRange(region.xMin,region.xMax));
                Assert.That(0.5f+Vector3.Dot(relative,up)/(2f*frame.Size), Is.InRange(region.yMin,region.yMax));
            }
            var scale = (float)Math.Sqrt(height / 1920f);
            Assert.That(region.yMax * height, Is.EqualTo(height-96-316*scale).Within(0.001));
            Assert.That(region.yMin * height, Is.EqualTo(64+72*scale).Within(0.001));
            Assert.That(Vector3.Dot(frame.CameraPosition-original,forward), Is.EqualTo(0f).Within(0.0001f), "Fit translates in the image plane; depth and pitch stay intact.");
            var cellPixels = level.CellSize * height / (2f * frame.Size);
            Assert.That(cellPixels, Is.GreaterThan(70f), "Minimum measured horizontal cell pitch at 1080px width.");
            TestContext.WriteLine(level.DisplayName + " 1080x" + height + ": size=" + frame.Size + ", cell pitch=" + cellPixels + "px, vertical pitch=" + cellPixels*up.z + "px");
        }

        [TestCase(0)] [TestCase(1)]
        public void EnvelopeContainsEveryGateAlignmentAndContinuousBurstTrajectory(int index)
        {
            var level = AdvancedCampaignLevels.Create()[index]; var bounds = BoardFraming.Envelope(level);
            foreach (var block in level.Blocks)
            foreach (var gate in level.Gates.Where(gate => gate.Color == block.Color))
            {
                var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                var horizontal = ExitGate.IsHorizontalEdge(gate.Edge); var extent = horizontal ? x : y;
                if (extent >= gate.Span) continue;
                for (var along = gate.Start; along + extent < gate.Start + gate.Span; along++)
                {
                    var anchor = gate.Edge == BoardEdge.North ? new Vector2Int(along,level.Height) :
                        gate.Edge == BoardEdge.South ? new Vector2Int(along,-y-1) : gate.Edge == BoardEdge.East ?
                        new Vector2Int(level.Width,along) : new Vector2Int(-x-1,along);
                    var end = new Vector3(anchor.x,0,anchor.y);
                    foreach (var cell in block.Cells)
                        Contains(bounds,(end + new Vector3(cell.x,0.23f,cell.y)) * level.CellSize);
                    foreach (var piece in SoftBurstMotion.Create(block.Cells,gate.Edge))
                    for (var sample = 0; sample <= 64; sample++)
                    {
                        var u = sample / 64f; var point = (end + piece.Position(u)) * level.CellSize;
                        var radius = Vector3.one * (piece.Scale(u) * 0.8660254f * level.CellSize);
                        Contains(bounds,point-radius); Contains(bounds,point+radius);
                    }
                }
            }
        }

        [TestCase(0)] [TestCase(1)]
        public void LargerBoardCellCentersAndGenerousHitOffsetsRoundTripWithoutChangingCoordinates(int index)
        {
            var level = AdvancedCampaignLevels.Create()[index]; var coordinates = new BoardCoordinates(Origin(level),level.CellSize);
            for (var x = 0; x < level.Width; x++) for (var y = 0; y < level.Height; y++)
            {
                var cell = new Vector2Int(x,y); var world = coordinates.CellToWorld(cell);
                foreach (var dx in new[] { -0.45f,0f,0.45f }) foreach (var dz in new[] { -0.45f,0f,0.45f })
                    Assert.That(coordinates.WorldToCell(world + new Vector3(dx,0,dz)*level.CellSize), Is.EqualTo(cell));
            }
        }
        internal static Vector3 Origin(LevelData level) => new Vector3(-(level.Width-1)*level.CellSize/2f,0.04f*level.CellSize,-(level.Height-1)*level.CellSize/2f);
        private static void Contains(Bounds bounds, Vector3 point)
        {
            // Bounds.Contains calls native Unity. Component-wise bounds checks keep this math test managed.
            Assert.That(point.x, Is.InRange(bounds.min.x,bounds.max.x)); Assert.That(point.y, Is.InRange(bounds.min.y,bounds.max.y));
            Assert.That(point.z, Is.InRange(bounds.min.z,bounds.max.z));
        }
    }
}
