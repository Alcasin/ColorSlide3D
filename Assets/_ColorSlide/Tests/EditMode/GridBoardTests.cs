using System;
using System.Linq;
using ColorSlide.Board;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    /// <summary>All tests exercise the production model without scenes, rendering or input devices.</summary>
    public sealed class GridBoardTests
    {
        private static readonly Vector2Int[] Single = { Vector2Int.zero };
        private static readonly Vector2Int[] Horizontal = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] Elbow = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [TestCase(-1, 0, false)]
        [TestCase(0, -1, false)]
        [TestCase(6, 0, false)]
        [TestCase(0, 8, false)]
        [TestCase(5, 7, true)]
        [TestCase(0, 0, true)]
        public void BoundariesUseConfiguredDimensions(int x, int y, bool inside)
        {
            Assert.That(new GridBoard(6, 8).IsInside(new Vector2Int(x, y)), Is.EqualTo(inside));
        }

        [Test]
        public void PlacementPopulatesEveryFootprintCellAndLeavesTheElbowCornerEmpty()
        {
            var board = new GridBoard(6, 8);
            var block = board.AddBlock("L", new Vector2Int(1, 2), Elbow);
            Assert.That(board.OccupantAt(new Vector2Int(1, 2)), Is.SameAs(block));
            Assert.That(board.OccupantAt(new Vector2Int(2, 2)), Is.SameAs(block));
            Assert.That(board.OccupantAt(new Vector2Int(1, 3)), Is.SameAs(block));
            Assert.That(board.OccupantAt(new Vector2Int(2, 3)), Is.Null);
            AssertOccupancy(board);
        }

        [Test]
        public void RejectedPlacementsAreAtomicIncludingAFarFootprintCell()
        {
            var board = new GridBoard(6, 8, new[] { new Vector2Int(3, 3) });
            var occupant = board.AddBlock("existing", new Vector2Int(2, 1), Single);
            Assert.Throws<InvalidOperationException>(() => board.AddBlock("overlap", new Vector2Int(1, 1), Horizontal));
            Assert.Throws<InvalidOperationException>(() => board.AddBlock("wall", new Vector2Int(2, 3), Horizontal));
            Assert.Throws<InvalidOperationException>(() => board.AddBlock("outside", new Vector2Int(5, 0), Horizontal));
            Assert.That(board.Blocks.Count(), Is.EqualTo(1));
            Assert.That(board.OccupantAt(new Vector2Int(2, 1)), Is.SameAs(occupant));
            Assert.That(board.OccupantAt(new Vector2Int(1, 1)), Is.Null);
            AssertOccupancy(board);
        }

        [Test]
        public void ElbowNotchCanContainAnotherBlockButArmsCannotEnterIt()
        {
            var board = new GridBoard(5, 5);
            var elbow = board.AddBlock("elbow", new Vector2Int(1, 1), Elbow);
            var corner = board.AddBlock("corner", new Vector2Int(2, 2), Single);
            Assert.That(board.TryMoveStep("elbow", Vector2Int.right), Is.False);
            Assert.That(board.TryMoveStep("elbow", Vector2Int.up), Is.False);
            Assert.That(elbow.Anchor, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(board.OccupantAt(new Vector2Int(2, 2)), Is.SameAs(corner));
            AssertOccupancy(board);
        }

        [Test]
        public void OverlappingOwnCellsDoNotBlockAValidRectangleMove()
        {
            var board = new GridBoard(6, 4);
            var block = board.AddBlock("bar", new Vector2Int(1, 1), Horizontal);
            Assert.That(board.TryMoveStep("bar", Vector2Int.right), Is.True);
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(board.OccupantAt(new Vector2Int(1, 1)), Is.Null);
            Assert.That(board.OccupantAt(new Vector2Int(3, 1)), Is.SameAs(block));
            AssertOccupancy(board);
        }

        [Test]
        public void EveryArmMustRemainInsideTheBoard()
        {
            var board = new GridBoard(4, 4);
            var block = board.AddBlock("L", new Vector2Int(2, 2), Elbow);
            Assert.That(board.TryMoveStep("L", Vector2Int.right), Is.False);
            Assert.That(board.TryMoveStep("L", Vector2Int.up), Is.False);
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(2, 2)));
            AssertOccupancy(board);
        }

        [Test]
        public void FastDragStopsBeforeAnOccupiedCellEvenWhenDestinationIsEmpty()
        {
            var board = new GridBoard(8, 3);
            var moving = board.AddBlock("moving", Vector2Int.zero, Single);
            var blocker = board.AddBlock("blocker", new Vector2Int(3, 0), Single);
            Assert.That(board.MoveTowards("moving", new Vector2Int(7, 0), GridAxis.X), Is.EqualTo(2));
            Assert.That(moving.Anchor, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(board.OccupantAt(new Vector2Int(3, 0)), Is.SameAs(blocker));
            Assert.That(board.OccupantAt(new Vector2Int(7, 0)), Is.Null);
            AssertOccupancy(board);
        }

        [Test]
        public void FastElbowDragCannotTunnelAnOffsetArmThroughAStaticObstacle()
        {
            var board = new GridBoard(8, 4, new[] { new Vector2Int(4, 1) });
            var moving = board.AddBlock("L", Vector2Int.zero, Elbow);
            Assert.That(board.MoveTowards("L", new Vector2Int(6, 0), GridAxis.X), Is.EqualTo(3));
            Assert.That(moving.Anchor, Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(board.IsBlocked(new Vector2Int(4, 1)), Is.True);
            Assert.That(board.OccupantAt(new Vector2Int(4, 1)), Is.Null);
            AssertOccupancy(board);
        }

        [Test]
        public void FastReverseDragStopsBeforeTheSameObstacle()
        {
            var board = new GridBoard(8, 3, new[] { new Vector2Int(3, 0) });
            var block = board.AddBlock("single", new Vector2Int(7, 0), Single);
            Assert.That(board.MoveTowards("single", Vector2Int.zero, GridAxis.X), Is.EqualTo(3));
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(4, 0)));
            AssertOccupancy(board);
        }

        [Test]
        public void DirectionChangesAndReversalsKeepOccupancyConsistent()
        {
            var board = new GridBoard(6, 8);
            var block = board.AddBlock("L", new Vector2Int(1, 1), Elbow);
            Assert.That(board.MoveTowards("L", new Vector2Int(3, 7), GridAxis.X), Is.EqualTo(2));
            Assert.That(board.MoveTowards("L", new Vector2Int(3, 4), GridAxis.Y), Is.EqualTo(3));
            Assert.That(board.MoveTowards("L", new Vector2Int(0, 4), GridAxis.X), Is.EqualTo(3));
            Assert.That(board.MoveTowards("L", new Vector2Int(0, 0), GridAxis.Y), Is.EqualTo(4));
            Assert.That(block.Anchor, Is.EqualTo(Vector2Int.zero));
            AssertOccupancy(board);
        }

        [Test]
        public void DiagonalTargetDoesNotCutCornersOrAutomaticallyTryTheOtherAxis()
        {
            var board = new GridBoard(6, 6, new[] { Vector2Int.right, Vector2Int.up });
            var block = board.AddBlock("single", Vector2Int.zero, Single);
            Assert.That(board.MoveTowards("single", new Vector2Int(5, 5), GridAxis.X), Is.Zero);
            Assert.That(board.MoveTowards("single", new Vector2Int(5, 5), GridAxis.Y), Is.Zero);
            Assert.Throws<ArgumentException>(() => board.TryMoveStep("single", Vector2Int.one));
            Assert.That(block.Anchor, Is.EqualTo(Vector2Int.zero));
            AssertOccupancy(board);
        }

        [Test]
        public void AnUnchosenAxisIsNotAnImplicitDetourAroundAWall()
        {
            var board = new GridBoard(6, 6, new[] { new Vector2Int(2, 0) });
            var block = board.AddBlock("single", Vector2Int.zero, Single);
            board.MoveTowards("single", new Vector2Int(5, 5), GridAxis.X);
            Assert.That(board.MoveTowards("single", new Vector2Int(5, 5), GridAxis.X), Is.Zero);
            Assert.That(block.Anchor, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(board.MoveTowards("single", new Vector2Int(1, 2), GridAxis.Y), Is.EqualTo(2),
                "The player can explicitly request an orthogonal direction without ending the drag.");
            AssertOccupancy(board);
        }

        [Test]
        public void ExtremeTargetsClampToFootprintAwareBoundaries()
        {
            var board = new GridBoard(6, 8);
            var block = board.AddBlock("bar", Vector2Int.zero, Horizontal);
            Assert.That(board.MoveTowards("bar", new Vector2Int(int.MaxValue, 0), GridAxis.X), Is.EqualTo(4));
            Assert.That(block.Anchor.x, Is.EqualTo(4));
            Assert.That(board.MoveTowards("bar", new Vector2Int(int.MinValue, 0), GridAxis.X), Is.EqualTo(4));
            Assert.That(block.Anchor, Is.EqualTo(Vector2Int.zero));
            AssertOccupancy(board);
        }

        [Test]
        public void InvalidShapeOrIdentityNeverMutatesExistingOccupancy()
        {
            var board = new GridBoard(6, 8);
            board.AddBlock("original", Vector2Int.zero, Single);
            Assert.Throws<ArgumentNullException>(() => board.AddBlock("null", Vector2Int.one, null));
            Assert.Throws<ArgumentException>(() => board.AddBlock("empty", Vector2Int.one, Array.Empty<Vector2Int>()));
            Assert.Throws<ArgumentException>(() => board.AddBlock("duplicate", Vector2Int.one, new[] { Vector2Int.zero, Vector2Int.zero }));
            Assert.Throws<ArgumentException>(() => board.AddBlock("negative", Vector2Int.one, new[] { Vector2Int.zero, Vector2Int.left }));
            Assert.Throws<ArgumentException>(() => board.AddBlock("missing origin", Vector2Int.one, new[] { Vector2Int.right }));
            Assert.Throws<ArgumentException>(() => board.AddBlock(" ", Vector2Int.one, Single));
            Assert.Throws<ArgumentException>(() => board.AddBlock("original", Vector2Int.one, Single));
            Assert.Throws<InvalidOperationException>(() => board.AddBlock("overflow", Vector2Int.one,
                new[] { Vector2Int.zero, new Vector2Int(int.MaxValue, 0) }));
            Assert.That(board.Blocks.Count(), Is.EqualTo(1));
            AssertOccupancy(board);
        }

        [Test]
        public void InvalidBoardObstaclesAndMoveDirectionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridBoard(0, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridBoard(6, -1));
            Assert.Throws<ArgumentException>(() => new GridBoard(6, 8, new[] { new Vector2Int(6, 0) }));
            Assert.Throws<ArgumentException>(() => new GridBoard(6, 8, new[] { Vector2Int.one, Vector2Int.one }));
            var board = new GridBoard(6, 8);
            board.AddBlock("single", Vector2Int.zero, Single);
            Assert.Throws<ArgumentException>(() => board.TryMoveStep("single", Vector2Int.zero));
            Assert.Throws<ArgumentException>(() => board.TryMoveStep("single", new Vector2Int(2, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.MoveTowards("single", Vector2Int.one, (GridAxis)99));
            AssertOccupancy(board);
        }

        [Test]
        public void FootprintDataIsCopiedSoCallerEditsCannotCorruptTheModel()
        {
            var cells = new[] { Vector2Int.zero, Vector2Int.up };
            var board = new GridBoard(4, 4);
            var block = board.AddBlock("vertical", Vector2Int.zero, cells);
            cells[1] = new Vector2Int(99, 99);
            Assert.That(block.Cells[1], Is.EqualTo(Vector2Int.up));
            Assert.That(board.TryMoveStep("vertical", Vector2Int.up), Is.True);
            AssertOccupancy(board);
        }

        [Test]
        public void CoordinatesMapCellCentersToXZAndIgnoreVisualHeightWhenPicking()
        {
            var coordinates = new BoardCoordinates(new Vector3(-2.25f, 0.04f, -3.15f), 0.9f);
            var cell = new Vector2Int(4, 6);
            var world = coordinates.CellToWorld(cell);
            Assert.That(world.x, Is.EqualTo(1.35f).Within(0.0001f));
            Assert.That(world.z, Is.EqualTo(2.25f).Within(0.0001f));
            Assert.That(world.y, Is.EqualTo(0.04f));
            world.y = 100f;
            Assert.That(coordinates.WorldToCell(world), Is.EqualTo(cell));
            Assert.That(BoardCoordinates.DominantAxis(new Vector3(1f, 0f, 1f)), Is.EqualTo(GridAxis.X));
            Assert.That(BoardCoordinates.DominantAxis(new Vector3(0.1f, 0f, -1f)), Is.EqualTo(GridAxis.Y));
        }

        [Test]
        public void CoordinatesUseHalfCellThresholdsAndClampExtremeFiniteInput()
        {
            var coordinates = new BoardCoordinates(Vector3.zero, 1f);
            Assert.That(coordinates.WorldToCell(new Vector3(0.49f, 8f, -0.51f)), Is.EqualTo(new Vector2Int(0, -1)));
            Assert.That(coordinates.WorldToCell(new Vector3(0.5f, 8f, -0.5f)), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(coordinates.WorldToCell(new Vector3(float.MaxValue, 0f, float.MinValue)),
                Is.EqualTo(new Vector2Int(int.MaxValue, int.MinValue)));
            Assert.Throws<ArgumentException>(() => new BoardCoordinates(Vector3.zero, float.NaN));
            Assert.Throws<ArgumentException>(() => new BoardCoordinates(Vector3.zero, 0f));
            Assert.Throws<ArgumentException>(() => coordinates.WorldToCell(new Vector3(float.PositiveInfinity, 0f, 0f)));
        }

        private static void AssertOccupancy(GridBoard board)
        {
            var expectedCount = 0;
            foreach (var block in board.Blocks)
            foreach (var offset in block.Cells)
            {
                var cell = block.Anchor + offset;
                Assert.That(board.IsInside(cell), Is.True);
                Assert.That(board.IsBlocked(cell), Is.False);
                Assert.That(board.OccupantAt(cell), Is.SameAs(block));
                expectedCount++;
            }
            var actualCount = 0;
            for (var x = 0; x < board.Width; x++)
            for (var y = 0; y < board.Height; y++)
                if (board.OccupantAt(new Vector2Int(x, y)) != null)
                    actualCount++;
            Assert.That(actualCount, Is.EqualTo(expectedCount), "Ghost occupancy or overlapping footprints.");
        }
    }
}
