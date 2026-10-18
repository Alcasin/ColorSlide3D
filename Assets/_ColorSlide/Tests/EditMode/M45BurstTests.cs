using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M45BurstTests
    {
        [TestCase(BoardEdge.North)] [TestCase(BoardEdge.South)] [TestCase(BoardEdge.East)] [TestCase(BoardEdge.West)]
        public void RealExitPlanProducesBoundedDeterministicFragmentsOnlyBeyondItsGate(BoardEdge edge)
        {
            var footprint = new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
            var board = new GridBoard(4, 4, Array.Empty<Vector2Int>());
            board.AddBlock("L", new Vector2Int(1, 1), footprint, BlockColor.Green);
            var session = new PuzzleSession(board, new[] { new ExitGate("Exit", BlockColor.Green, edge, 0, 4) }, 30d);
            session.Start(); var direction = ExitGate.Outward(edge);
            var move = session.MoveTowards("L", new Vector2Int(1, 1) + direction * 10, ExitGate.IsHorizontalEdge(edge) ? GridAxis.Y : GridAxis.X);
            Assert.That(move.Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.State, Is.EqualTo(GameplayState.Won)); Assert.That(session.RemainingBlocks, Is.Zero);
            var pieces = SoftBurstMotion.Create(move.Exit.Block.Cells, edge);
            var again = SoftBurstMotion.Create(move.Exit.Block.Cells, edge);
            Assert.That(pieces.Length, Is.InRange(8, 12)); Assert.That(pieces.Length, Is.EqualTo(SoftBurstMotion.FragmentCount));
            Assert.That(SoftBurstMotion.Direction(edge), Is.EqualTo(new Vector3(direction.x, 0f, direction.y)));
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i]; Assert.That(piece.Origin, Is.EqualTo(again[i].Origin));
                Assert.That(piece.Travel, Is.EqualTo(again[i].Travel)); Assert.That(piece.Rotation, Is.EqualTo(again[i].Rotation));
                Assert.That(footprint.Any(cell => Math.Abs(piece.Origin.x - cell.x) <= 0.161f && Math.Abs(piece.Origin.z - cell.y) <= 0.161f), Is.True,
                    "Origins must occupy a real L cell, never the missing corner or its bounding-box center.");
                Assert.That(Vector3.Dot(piece.Travel, SoftBurstMotion.Direction(edge)), Is.GreaterThan(0f));
                Assert.That(piece.Size, Is.InRange(0.23f, 0.291f)); Assert.That(piece.Rotation.magnitude, Is.LessThan(61f));
                for (var step = 0; step <= 20; step++)
                {
                    var u = step / 20f; var offset = piece.Position(u);
                    var center = new Vector3(move.Exit.EndAnchor.x, 0f, move.Exit.EndAnchor.y) + offset;
                    var boundary = edge == BoardEdge.North || edge == BoardEdge.East ? 3.5f : 0.5f;
                    Assert.That(Vector3.Dot(center, SoftBurstMotion.Direction(edge)) - piece.Scale(u) * 0.867f, Is.GreaterThan(boundary));
                    Assert.That(offset.y, Is.InRange(0.20f, 0.53f));
                }
                Assert.That(piece.Scale(1f), Is.Zero);
            }
            Assert.That(session.Board.ActiveBlockCount, Is.Zero);
            for (var x = 0; x < 4; x++) for (var y = 0; y < 4; y++) Assert.That(session.Board.OccupantAt(new Vector2Int(x, y)), Is.Null);
            Assert.That(session.MoveTowards("L", Vector2Int.zero, GridAxis.X).Status, Is.EqualTo(MovementStatus.SessionInactive));
            session.Restart(); Assert.That(session.RemainingBlocks, Is.EqualTo(1)); Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
        }
        [Test]
        public void OneTimelinePreservesTheShapeBeforeScatterAndCompletesAfterBothPhases()
        {
            Assert.That(SoftBurstMotion.Duration, Is.InRange(0.45f, 0.55f));
            Assert.That(SoftBurstMotion.OutwardProgress(0f), Is.Zero);
            Assert.That(SoftBurstMotion.OutwardProgress(SoftBurstMotion.OutwardSeconds), Is.EqualTo(1f));
            Assert.That(SoftBurstMotion.ScatterProgress(SoftBurstMotion.OutwardSeconds), Is.Zero);
            Assert.That(SoftBurstMotion.IsComplete(0.3f), Is.False, "The accepted old wait must not complete the new effect.");
            Assert.That(SoftBurstMotion.IsComplete(SoftBurstMotion.Duration), Is.True);
            Assert.That(SoftBurstMotion.ScatterProgress(SoftBurstMotion.Duration), Is.EqualTo(1f));
            Assert.Throws<ArgumentException>(() => SoftBurstMotion.Create(Array.Empty<Vector2Int>(), BoardEdge.North));
        }
    }
}
