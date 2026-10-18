using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class CampaignTests
    {
        [TestCase(0, 2)] [TestCase(1, 3)] [TestCase(2, 3)] [TestCase(3, 5)]
        public void AuthoredLevelsCreateExactInitialOccupancy(int index, int count)
        {
            var data = M3AuthoredLevels.Create()[index];
            var session = data.CreateSession();
            Assert.That(session.State, Is.EqualTo(GameplayState.Ready));
            Assert.That(session.RemainingBlocks, Is.EqualTo(count));
            Assert.That(session.RemainingSeconds, Is.EqualTo(data.Seconds));
            foreach (var block in data.Blocks)
            {
                var actual = session.Board.GetBlock(block.Id);
                Assert.That(actual.Anchor, Is.EqualTo(block.Anchor)); Assert.That(actual.ColorId, Is.EqualTo(block.Color));
                Assert.That(actual.Cells, Is.EqualTo(block.Cells));
            }
            Assert.That(session.Board.BlockedCells, Is.EquivalentTo(data.Obstacles));
            CheckOccupancy(session.Board);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void AuthoredSolutionsUseLegalProductionMovesAtEveryStep(int index)
        {
            var session = M3AuthoredLevels.Create()[index].CreateSession(); session.Start();
            Solve(session, index);
        }

        [Test]
        public void InvalidPlacementAndRequiredDataReportLevelAndBlock()
        {
            Action<LevelData>[] invalid = {
                level => level.Width = 0, level => level.Height = -1, level => level.CellSize = float.NaN,
                level => level.DisplayName = "", level => level.Seconds = float.PositiveInfinity,
                level => level.Blocks = null, level => level.Obstacles = null,
                level => level.Blocks[0].Cells = Array.Empty<Vector2Int>(),
                level => level.Blocks[0].Cells = new[] { Vector2Int.right },
                level => level.Blocks[0].Cells = new[] { Vector2Int.zero, Vector2Int.zero },
                level => level.Blocks[0].Anchor = new Vector2Int(-1, 0),
                level => level.Blocks[0].Cells = new[] { Vector2Int.zero, new Vector2Int(6, 0) },
                level => level.Blocks[1].Anchor = level.Blocks[0].Anchor,
                level => level.Blocks[1].Id = level.Blocks[0].Id,
                level => level.Blocks[0].Color = BlockColor.None,
                level => level.Obstacles = new[] { level.Blocks[0].Anchor },
                level => level.Obstacles = new[] { new Vector2Int(6, 0) },
                level => level.Obstacles = new[] { Vector2Int.zero, Vector2Int.zero }
            };
            for (var i = 0; i < invalid.Length; i++)
            {
                var level = M3AuthoredLevels.Create()[0]; invalid[i](level);
                Assert.That(Assert.Throws<ArgumentException>(() => level.CreateSession()).Message,
                    Does.Contain(level.Id), "Invalid case " + i);
            }
            var affected = M3AuthoredLevels.Create()[0]; affected.Blocks[0].Cells = null;
            Assert.That(Assert.Throws<ArgumentException>(() => affected.CreateSession()).Message, Does.Contain("Blue"));
        }

        [Test]
        public void InvalidGatesAndMissingExitClearanceAreRejected()
        {
            Action<LevelData>[] invalid = {
                level => level.Gates = null, level => level.Gates = Array.Empty<LevelGate>(),
                level => level.Gates[0].Span = 0, level => level.Gates[0].Start = -1,
                level => level.Gates[0].Span = int.MaxValue,
                level => level.Gates[0].Color = (BlockColor)99,
                level => level.Gates[0].Edge = (BoardEdge)99,
                level => level.Gates[0].Color = BlockColor.Amber,
                level => level.Gates[1].Id = level.Gates[0].Id,
                level => { level.Gates[1].Edge = BoardEdge.North; level.Gates[1].Start = 1; },
                level => level.Blocks[0].Cells = new[] { Vector2Int.zero, Vector2Int.right }
            };
            foreach (var change in invalid)
            {
                var level = M3AuthoredLevels.Create()[0]; change(level);
                Assert.That(Assert.Throws<ArgumentException>(() => level.CreateSession()).Message, Does.Contain(level.Id));
            }
        }

        [Test]
        public void HigherLevelsHaveRealBlockingAndPassageDependencies()
        {
            var two = M3AuthoredLevels.Create()[1].CreateSession(); two.Start();
            Assert.That(two.MoveTowards("Amber", new Vector2Int(1, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            two.Restart();
            Assert.That(two.MoveTowards("Blue", new Vector2Int(2, 8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            var three = M3AuthoredLevels.Create()[2].CreateSession(); three.Start();
            Assert.That(three.MoveTowards("Green", new Vector2Int(3, 3), GridAxis.X).Status, Is.EqualTo(MovementStatus.BlockedByObstacle));
            Assert.That(three.MoveTowards("Amber", new Vector2Int(0, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            var four = M3AuthoredLevels.Create()[3].CreateSession(); four.Start();
            Assert.That(four.MoveTowards("Green", new Vector2Int(0, 5), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            Assert.That(four.MoveTowards("Key", new Vector2Int(2, 5), GridAxis.X).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            four.Restart();
            Assert.That(four.MoveTowards("Blue", new Vector2Int(2, 8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            four.Restart();
            Assert.That(four.MoveTowards("Amber", new Vector2Int(1, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
        }

        [Test]
        public void CampaignRequiresWinAndExplicitNextAndStopsAfterFourthLevel()
        {
            var campaign = new CampaignSession(M3AuthoredLevels.Create());
            for (var index = 0; index < 4; index++)
            {
                Assert.That(campaign.CurrentIndex, Is.EqualTo(index));
                Assert.That(campaign.Next(), Is.False);
                var previous = campaign.CurrentSession;
                var oldBlock = previous.Board.Blocks.First();
                Solve(previous, index);
                Assert.That(campaign.CurrentIndex, Is.EqualTo(index), "No automatic advancement.");
                Assert.That(previous.MoveTowards(oldBlock.Id, Vector2Int.zero, GridAxis.X).Status, Is.EqualTo(MovementStatus.SessionInactive));
                Assert.That(campaign.IsComplete, Is.EqualTo(index == 3));
                Assert.That(campaign.Next(), Is.EqualTo(index < 3));
                Assert.That(campaign.Next(), Is.False, "Repeated Next cannot skip a playing level or load Level 5.");
                if (index < 3)
                {
                    Assert.That(campaign.CurrentSession, Is.Not.SameAs(previous));
                    Assert.That(campaign.CurrentSession.RemainingSeconds, Is.EqualTo(campaign.CurrentLevel.Seconds));
                    Assert.That(campaign.CurrentSession.Board.Blocks.Any(block => ReferenceEquals(block, oldBlock)), Is.False);
                    CheckOccupancy(campaign.CurrentSession.Board);
                }
            }
            campaign.CurrentSession.Restart();
            Assert.That(campaign.IsComplete, Is.False); Assert.That(campaign.CurrentIndex, Is.EqualTo(3));
        }

        [Test]
        public void FailureAndRepeatedRetryPreserveSelectedLevelAndAuthoredSnapshot()
        {
            var campaign = new CampaignSession(M3AuthoredLevels.Create());
            Solve(campaign.CurrentSession, 0); campaign.Next();
            var first = campaign.CurrentSession.Board.GetBlock("Blue");
            for (var repeat = 0; repeat < 4; repeat++)
            {
                var session = campaign.CurrentSession;
                session.MoveTowards("Blue", Vector2Int.zero, GridAxis.X);
                Assert.That(session.Advance(10000d), Is.True);
                Assert.That(campaign.Next(), Is.False);
                session.Restart();
                Assert.That(campaign.CurrentIndex, Is.EqualTo(1));
                Assert.That(session.Board.ActiveBlockCount, Is.EqualTo(3));
                Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
                Assert.That(session.RemainingSeconds, Is.EqualTo(180d));
                Assert.That(session.Board.GetBlock("Blue").Anchor, Is.EqualTo(new Vector2Int(2, 1)));
                Assert.That(session.Board.GetBlock("Blue"), Is.Not.SameAs(first));
                CheckOccupancy(session.Board);
            }
        }

        [Test]
        public void FailedPresentationPreparationCannotAdvanceOrReplaceTheCurrentSession()
        {
            var campaign = new CampaignSession(M3AuthoredLevels.Create());
            Solve(campaign.CurrentSession, 0);
            var current = campaign.CurrentSession;
            Assert.Throws<InvalidOperationException>(() => campaign.Next((data, session) =>
            {
                Assert.That(data.Id, Is.EqualTo("02-make-space"));
                Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
                throw new InvalidOperationException("Presentation could not be built.");
            }));
            Assert.That(campaign.CurrentIndex, Is.Zero); Assert.That(campaign.CurrentSession, Is.SameAs(current));
            Assert.That(campaign.CanNext, Is.True);
            Assert.That(campaign.Next(), Is.True);
        }

        [Test]
        public void CampaignRejectsInvalidLaterLevelsAndDuplicateIdentitiesBeforeStarting()
        {
            var levels = M3AuthoredLevels.Create(); levels[3].Gates[0].Span = 0;
            Assert.Throws<ArgumentException>(() => new CampaignSession(levels));
            levels = M3AuthoredLevels.Create(); levels[3].Id = levels[0].Id;
            Assert.Throws<ArgumentException>(() => new CampaignSession(levels));
        }

        internal static void Solve(PuzzleSession session, int index)
        {
            foreach (var move in Solution(index))
            {
                Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
                var result = session.MoveTowards(move.Id, new Vector2Int(move.X, move.Y), move.Axis);
                Assert.That(result.Status, Is.EqualTo(move.Exit ? MovementStatus.Exited : MovementStatus.Moved), move.Id + " move in level " + (index + 1));
                if (move.Exit) Assert.That(session.Board.IsActive(move.Id), Is.False);
                else Assert.That(session.Board.GetBlock(move.Id).Anchor, Is.EqualTo(new Vector2Int(move.X, move.Y)));
                CheckOccupancy(session.Board);
            }
            Assert.That(session.RemainingBlocks, Is.Zero); Assert.That(session.State, Is.EqualTo(GameplayState.Won));
        }
        private static Move[] Solution(int index)
        {
            switch (index)
            {
                case 0: return new[] { Y("Blue", 1, 8, true), Y("Amber", 4, -1, true) };
                case 1: return new[] { X("Blue", 0, 1), Y("Amber", 1, -1, true), X("Blue", 2, 1), Y("Blue", 2, 8, true), X("Purple", 6, 5, true) };
                case 2: return new[] { Y("Green", 0, 1), X("Green", 3, 1), Y("Amber", 0, -1, true), X("Green", 0, 1), Y("Green", 0, 5), X("Green", -1, 5, true), X("Purple", 6, 3, true) };
                case 3: return new[] { X("Purple", 6, 5, true), X("Key", 2, 5), Y("Key", 2, 8, true), Y("Green", 0, 5), X("Green", -1, 5, true), X("Blue", 0, 0), Y("Amber", 1, -1, true), X("Blue", 2, 0), Y("Blue", 2, 8, true) };
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        private static void CheckOccupancy(GridBoard board)
        {
            var expected = board.Blocks.SelectMany(block => block.Cells.Select(cell => block.Anchor + cell)).ToArray();
            Assert.That(expected.Distinct().Count(), Is.EqualTo(expected.Length));
            for (var x = 0; x < board.Width; x++)
            for (var y = 0; y < board.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                Assert.That(board.OccupantAt(cell) != null, Is.EqualTo(expected.Contains(cell)));
                if (board.OccupantAt(cell) != null) Assert.That(board.IsBlocked(cell), Is.False);
            }
        }
        private readonly struct Move
        {
            public readonly string Id; public readonly int X, Y; public readonly GridAxis Axis; public readonly bool Exit;
            public Move(string id, int x, int y, GridAxis axis, bool exit) { Id = id; X = x; Y = y; Axis = axis; Exit = exit; }
        }
        private static Move X(string id, int x, int y, bool exit = false) => new Move(id, x, y, GridAxis.X, exit);
        private static Move Y(string id, int x, int y, bool exit = false) => new Move(id, x, y, GridAxis.Y, exit);
    }
}
