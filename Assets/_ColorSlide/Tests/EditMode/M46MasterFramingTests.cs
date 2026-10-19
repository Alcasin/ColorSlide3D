using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46MasterFramingTests
    {
        [TestCase(0,1920)] [TestCase(0,2340)] [TestCase(0,2400)]
        [TestCase(1,1920)] [TestCase(1,2340)] [TestCase(1,2400)]
        public void MasterEnvelopeFitsSafeGameplayRegionWithAcceptedCameraDirection(int index, int height)
        {
            var level = MasterCampaignLevels.Create()[index]; var region = BoardFraming.GameplayRegion(new Vector2(1080,height),new Rect(0,64,1080,height-160));
            var bounds = BoardFraming.Envelope(level); bounds.center += M46FramingTests.Origin(level);
            var up = new Vector3(0,1,2)/(float)Math.Sqrt(5); var original = new Vector3(0,12,-6); var aspect = 1080f/height;
            var frame = BoardFraming.Fit(bounds,original,Vector3.right,up,aspect,region,PortraitCameraFit.SizeFor(6.25f,aspect));
            foreach (var corner in BoardFraming.Corners(bounds))
            {
                var delta = corner-frame.CameraPosition;
                Assert.That(0.5f+delta.x/(2*frame.Size*aspect), Is.InRange(region.xMin,region.xMax));
                Assert.That(0.5f+Vector3.Dot(delta,up)/(2*frame.Size), Is.InRange(region.yMin,region.yMax));
                Assert.That(Vector3.Dot(delta,new Vector3(0,-2,1)/(float)Math.Sqrt(5)), Is.InRange(0.1f,100f));
            }
            var pitch = level.CellSize*height/(2*frame.Size);
            Assert.That(pitch, Is.GreaterThan(80f)); Assert.That(pitch*up.z, Is.GreaterThan(73f));
            Assert.That(level.Width*level.CellSize, Is.LessThan(6f)); Assert.That(level.Height*level.CellSize, Is.LessThan(8f), "Fits the accepted tray without modifying it.");
            TestContext.WriteLine(level.DisplayName+" 1080x"+height+": ortho="+frame.Size+", pitch="+pitch+" x "+pitch*up.z+"px");
        }

        [TestCase(0)] [TestCase(1)]
        public void EveryNewFootprintAndGateAlignmentKeepsTheWholeBurstInsideTheEnvelope(int index)
        {
            var level = MasterCampaignLevels.Create()[index]; var bounds = BoardFraming.Envelope(level);
            foreach (var block in level.Blocks)
            foreach (var gate in level.Gates.Where(gate => gate.Color == block.Color))
            {
                var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                var horizontal = ExitGate.IsHorizontalEdge(gate.Edge); var extent = horizontal ? x : y;
                if (extent >= gate.Span) continue;
                for (var along = gate.Start; along+extent < gate.Start+gate.Span; along++)
                {
                    var anchor = gate.Edge == BoardEdge.North ? new Vector2Int(along,level.Height) : gate.Edge == BoardEdge.South ?
                        new Vector2Int(along,-y-1) : gate.Edge == BoardEdge.East ? new Vector2Int(level.Width,along) : new Vector2Int(-x-1,along);
                    foreach (var piece in SoftBurstMotion.Create(block.Cells,gate.Edge))
                    for (var step = 0; step <= 64; step++)
                    {
                        var u = step/64f; var point = (new Vector3(anchor.x,0,anchor.y)+piece.Position(u))*level.CellSize;
                        var radius = Vector3.one*(piece.Scale(u)*level.CellSize*0.8660254f);
                        Inside(bounds,point-radius); Inside(bounds,point+radius);
                    }
                }
            }
        }

        [Test]
        public void ProposedNineByTwelveWouldReduceTouchPitchAndExceedTheAcceptedTray()
        {
            var final = MasterCampaignLevels.Create()[1]; var safe = BoardFraming.GameplayRegion(new Vector2(1080,1920),new Rect(0,64,1080,1760));
            var up = new Vector3(0,1,2)/(float)Math.Sqrt(5); var origin = new Vector3(0,12,-6);
            float Pitch(LevelData level)
            {
                var bounds = BoardFraming.Envelope(level); bounds.center += M46FramingTests.Origin(level);
                return level.CellSize*1920/(2*BoardFraming.Fit(bounds,origin,Vector3.right,up,9f/16f,safe,6.25f).Size);
            }
            var accepted = Pitch(final); final.Width = 9; final.Height = 12;
            var proposed = Pitch(final); Assert.That(proposed, Is.LessThan(accepted));
            Assert.That(final.Width*final.CellSize > 6 && final.Height*final.CellSize > 8, Is.True);
            TestContext.WriteLine("8x11 pitch="+accepted+" x "+accepted*up.z+"; 9x12 proposal="+proposed+" x "+proposed*up.z+"px");
        }
        private static void Inside(Bounds bounds, Vector3 point)
        {
            Assert.That(point.x, Is.InRange(bounds.min.x,bounds.max.x)); Assert.That(point.y, Is.InRange(bounds.min.y,bounds.max.y));
            Assert.That(point.z, Is.InRange(bounds.min.z,bounds.max.z));
        }
    }
}
