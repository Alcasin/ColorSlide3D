using System;
using System.Linq;
using ColorSlide.Board;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class ExitRulesTests
    {
        private static readonly Vector2Int[] Single = { Vector2Int.zero };
        private static readonly Vector2Int[] Elbow = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [TestCase(BoardEdge.North)]
        [TestCase(BoardEdge.South)]
        [TestCase(BoardEdge.East)]
        [TestCase(BoardEdge.West)]
        public void MatchingOutwardExitWorksOnEveryEdgeAndRemovesOnlyOnce(BoardEdge edge)
        {
            var board = new GridBoard(4, 4);
            var direction = ExitGate.Outward(edge);
            var horizontal = ExitGate.IsHorizontalEdge(edge);
            var anchor = horizontal ? new Vector2Int(1, direction.y > 0 ? 3 : 0) : new Vector2Int(direction.x > 0 ? 3 : 0, 1);
            var block = board.AddBlock("blue", anchor, Single, BlockColor.Blue);
            board.ConfigureGates(new[] { new ExitGate("exit", BlockColor.Blue, edge, 1, 1) });
            var axis = horizontal ? GridAxis.Y : GridAxis.X;
            var result = board.MoveWithExits("blue", anchor + direction, axis);
            Assert.That(result.Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(result.Exit.EndAnchor, Is.EqualTo(anchor + direction));
            Assert.That(block.IsRemoved, Is.True);
            Assert.That(board.ActiveBlockCount, Is.Zero);
            Assert.That(board.OccupantAt(anchor), Is.Null);
            Assert.That(board.MoveWithExits("blue", anchor + direction, axis).Status, Is.EqualTo(MovementStatus.InactiveBlock));
            AssertOccupancy(board);
        }

        [Test]
        public void GateDataRejectsInvalidIdsColorsEdgesAndSpans()
        {
            Assert.Throws<ArgumentException>(() => new ExitGate("", BlockColor.Blue, BoardEdge.North, 0, 1));
            Assert.Throws<ArgumentException>(() => new ExitGate("x", BlockColor.None, BoardEdge.North, 0, 1));
            Assert.Throws<ArgumentException>(() => new ExitGate("x", (BlockColor)99, BoardEdge.North, 0, 1));
            Assert.Throws<ArgumentException>(() => new ExitGate("x", BlockColor.Blue, (BoardEdge)99, 0, 1));
            Assert.Throws<ArgumentException>(() => new ExitGate("x", BlockColor.Blue, BoardEdge.North, -1, 1));
            Assert.Throws<ArgumentException>(() => new ExitGate("x", BlockColor.Blue, BoardEdge.North, 0, 0));
            Assert.Throws<ArgumentException>(() => new ExitGate("x", BlockColor.Blue, BoardEdge.North, 0, -2));
        }

        [Test]
        public void GateCollectionRejectsOutOfBoundsOverflowDuplicatesAndOverlapAtomically()
        {
            var board = new GridBoard(6, 8);
            Assert.Throws<ArgumentException>(() => board.ConfigureGates(new[] { new ExitGate("north", BlockColor.Blue, BoardEdge.North, 5, 2) }));
            Assert.Throws<ArgumentException>(() => board.ConfigureGates(new[] { new ExitGate("east", BlockColor.Blue, BoardEdge.East, int.MaxValue, int.MaxValue) }));
            Assert.Throws<ArgumentException>(() => board.ConfigureGates(new[]
            {
                new ExitGate("same", BlockColor.Blue, BoardEdge.North, 0, 2),
                new ExitGate("same", BlockColor.Amber, BoardEdge.South, 0, 2)
            }));
            foreach (var color in new[] { BlockColor.Amber, BlockColor.Blue })
                Assert.Throws<ArgumentException>(() => board.ConfigureGates(new[]
                {
                    new ExitGate("first", BlockColor.Blue, BoardEdge.North, 0, 2),
                    new ExitGate("second", color, BoardEdge.North, 1, 2)
                }));
            Assert.That(board.Gates, Is.Empty, "Rejected gate configurations must not partially apply.");
            board.ConfigureGates(new[]
            {
                new ExitGate("first", BlockColor.Blue, BoardEdge.North, 0, 2),
                new ExitGate("adjacent", BlockColor.Amber, BoardEdge.North, 2, 2)
            });
            Assert.That(board.Gates.Count, Is.EqualTo(2));
        }

        [Test]
        public void GateContactAndAcceptedM1ClampingNeverCauseAnExit()
        {
            var board = new GridBoard(4, 4);
            var block = board.AddBlock("blue", new Vector2Int(1, 3), Single, BlockColor.Blue);
            board.ConfigureGates(new[] { new ExitGate("north", BlockColor.Blue, BoardEdge.North, 1, 1) });
            Assert.That(board.MoveWithExits("blue", block.Anchor, GridAxis.Y).Status, Is.EqualTo(MovementStatus.Unchanged));
            Assert.That(board.MoveTowards("blue", new Vector2Int(1, 99), GridAxis.Y), Is.Zero,
                "M1's original API must still clamp at the boundary, even if gates exist.");
            Assert.That(board.TryMoveStep("blue", Vector2Int.up), Is.False);
            Assert.That(block.IsRemoved, Is.False);
            Assert.That(board.ActiveBlockCount, Is.EqualTo(1));
            AssertOccupancy(board);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WrongColorAndInsufficientApertureRejectTheWholeElbow(bool wrongColor)
        {
            var board = new GridBoard(4, 4);
            var block = board.AddBlock("L", new Vector2Int(1, 2), Elbow, BlockColor.Blue);
            board.ConfigureGates(new[] { new ExitGate("north", wrongColor ? BlockColor.Amber : BlockColor.Blue,
                BoardEdge.North, 1, wrongColor ? 2 : 1) });
            var result = board.MoveWithExits("L", new Vector2Int(1, 99), GridAxis.Y);
            Assert.That(result.Status, Is.EqualTo(MovementStatus.InvalidGate));
            Assert.That(result.Exit, Is.Null);
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(block.IsRemoved, Is.False);
            AssertOccupancy(board);
        }

        [Test]
        public void ARectangleFitsByPerpendicularWidthAndFullyDepartsAlongItsLength()
        {
            var board = new GridBoard(4, 4);
            var block = board.AddBlock("bar", new Vector2Int(2, 1), new[] { Vector2Int.zero, Vector2Int.right }, BlockColor.Blue);
            board.ConfigureGates(new[] { new ExitGate("east", BlockColor.Blue, BoardEdge.East, 1, 1) });
            var result = board.MoveWithExits("bar", new Vector2Int(99, 1), GridAxis.X);
            Assert.That(result.Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(result.Exit.StartAnchor, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(result.Exit.EndAnchor, Is.EqualTo(new Vector2Int(4, 1)), "The rear cell must also leave the board.");
            Assert.That(block.IsRemoved, Is.True);
            AssertOccupancy(board);
        }

        [Test]
        public void WideRectangleNeedsATwoCellNorthAperture()
        {
            var board = new GridBoard(4, 4);
            board.AddBlock("bar", new Vector2Int(1, 3), new[] { Vector2Int.zero, Vector2Int.right }, BlockColor.Amber);
            board.ConfigureGates(new[] { new ExitGate("north", BlockColor.Amber, BoardEdge.North, 1, 2) });
            Assert.That(board.MoveWithExits("bar", new Vector2Int(1, 4), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
            AssertOccupancy(board);
        }

        [Test]
        public void ElbowExitFreesItsExactFootprintAndAllowsReuse()
        {
            var board = new GridBoard(5, 5);
            var elbow = board.AddBlock("L", new Vector2Int(0, 2), Elbow, BlockColor.Green);
            var other = board.AddBlock("other", new Vector2Int(3, 2), Single, BlockColor.Blue);
            board.ConfigureGates(new[] { new ExitGate("west", BlockColor.Green, BoardEdge.West, 2, 2) });
            var result = board.MoveWithExits("L", new Vector2Int(-1, 2), GridAxis.X);
            Assert.That(result.Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(result.Exit.EndAnchor.x, Is.EqualTo(-2));
            Assert.That(elbow.IsRemoved, Is.True);
            Assert.That(board.OccupantAt(new Vector2Int(3, 2)), Is.SameAs(other));
            Assert.That(board.MoveTowards("other", Vector2Int.zero, GridAxis.X), Is.EqualTo(3));
            Assert.That(board.OccupantAt(new Vector2Int(0, 2)), Is.SameAs(other));
            Assert.That(board.OccupantAt(new Vector2Int(0, 3)), Is.Null);
            Assert.That(board.MoveWithExits("L", new Vector2Int(-99, 2), GridAxis.X).Status, Is.EqualTo(MovementStatus.InactiveBlock));
            AssertOccupancy(board);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RearCellCollisionAfterLeadingCellsDepartRejectsTheEntireExit(bool staticObstacle)
        {
            var obstruction = new Vector2Int(2, 4);
            var board = new GridBoard(5, 5, staticObstacle ? new[] { obstruction } : null);
            // Long L: the first outward step is free; on step TWO its rear arm hits (2,4).
            var shape = new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), Vector2Int.right };
            var block = board.AddBlock("L", new Vector2Int(1, 2), shape, BlockColor.Blue);
            if (!staticObstacle) board.AddBlock("blocker", obstruction, Single, BlockColor.Amber);
            board.ConfigureGates(new[] { new ExitGate("north", BlockColor.Blue, BoardEdge.North, 1, 2) });
            var result = board.MoveWithExits("L", new Vector2Int(1, int.MaxValue), GridAxis.Y);
            Assert.That(result.Status, Is.EqualTo(staticObstacle ? MovementStatus.BlockedByObstacle : MovementStatus.BlockedByBlock));
            Assert.That(result.Exit, Is.Null);
            Assert.That(block.IsRemoved, Is.False);
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(1, 2)));
            AssertOccupancy(board);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FastInteriorRequestCannotSkipAnObstructionBeforeAGate(bool staticObstacle)
        {
            var cell = new Vector2Int(1, 3);
            var board = new GridBoard(4, 6, staticObstacle ? new[] { cell } : null);
            var block = board.AddBlock("moving", new Vector2Int(1, 0), Single, BlockColor.Blue);
            if (!staticObstacle) board.AddBlock("blocker", cell, Single, BlockColor.Amber);
            board.ConfigureGates(new[] { new ExitGate("north", BlockColor.Blue, BoardEdge.North, 1, 1) });
            var result = board.MoveWithExits("moving", new Vector2Int(1, int.MaxValue), GridAxis.Y);
            Assert.That(result.Status, Is.EqualTo(staticObstacle ? MovementStatus.BlockedByObstacle : MovementStatus.BlockedByBlock));
            Assert.That(result.AcceptedSteps, Is.EqualTo(2));
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(block.IsRemoved, Is.False);
            AssertOccupancy(board);
        }

        [Test]
        public void SolidBoundaryAndUnchosenAxesNeverBecomeExitsOrDetours()
        {
            var board = new GridBoard(4, 4);
            var block = board.AddBlock("blue", Vector2Int.zero, Single, BlockColor.Blue);
            board.ConfigureGates(new[] { new ExitGate("north", BlockColor.Blue, BoardEdge.North, 3, 1) });
            Assert.That(board.MoveWithExits("blue", new Vector2Int(-99, 99), GridAxis.X).Status,
                Is.EqualTo(MovementStatus.BlockedByBoundary));
            Assert.That(block.Anchor, Is.EqualTo(Vector2Int.zero));
            Assert.That(board.MoveWithExits("blue", new Vector2Int(0, -99), GridAxis.Y).Status,
                Is.EqualTo(MovementStatus.BlockedByBoundary));
            AssertOccupancy(board);
        }

        internal static void AssertOccupancy(GridBoard board)
        {
            var expected = board.Blocks.Sum(block => block.Cells.Count);
            foreach (var block in board.Blocks)
            foreach (var offset in block.Cells)
            {
                var cell = block.Anchor + offset;
                Assert.That(board.IsInside(cell), Is.True);
                Assert.That(board.IsBlocked(cell), Is.False);
                Assert.That(board.OccupantAt(cell), Is.SameAs(block));
                Assert.That(block.IsRemoved, Is.False);
            }
            var count = 0;
            for (var x = 0; x < board.Width; x++)
            for (var y = 0; y < board.Height; y++)
                if (board.OccupantAt(new Vector2Int(x, y)) != null) count++;
            Assert.That(count, Is.EqualTo(expected), "Ghost or duplicate occupancy after movement/removal.");
        }
    }
}
