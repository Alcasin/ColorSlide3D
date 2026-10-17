using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class PuzzleSessionTests
    {
        private static PuzzleSession Session(double seconds = 180d)
        {
            var board = new GridBoard(4, 4, new[] { new Vector2Int(1, 1) });
            board.AddBlock("blue", Vector2Int.zero, new[] { Vector2Int.zero }, BlockColor.Blue);
            board.AddBlock("amber", new Vector2Int(3, 3), new[] { Vector2Int.zero }, BlockColor.Amber);
            return new PuzzleSession(board, new[]
            {
                new ExitGate("south", BlockColor.Blue, BoardEdge.South, 0, 1),
                new ExitGate("north", BlockColor.Amber, BoardEdge.North, 3, 1)
            }, seconds);
        }

        [Test]
        public void ReadyStateDoesNotTickOrMoveUntilStarted()
        {
            var session = Session();
            Assert.That(session.State, Is.EqualTo(GameplayState.Ready));
            Assert.That(session.Advance(500d), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(180d));
            Assert.That(session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.SessionInactive));
            Assert.That(session.Start(), Is.True);
            Assert.That(session.Start(), Is.False);
            Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
        }

        [Test]
        public void OnlyTheLastRemovalWinsAndTerminalInputCannotMutateTheBoard()
        {
            var session = Session(); session.Start();
            Assert.That(session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.RemainingBlocks, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
            Assert.That(session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.InactiveBlock));
            Assert.That(session.MoveTowards("amber", new Vector2Int(3, 4), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.State, Is.EqualTo(GameplayState.Won));
            Assert.That(session.RemainingBlocks, Is.Zero);
            Assert.That(session.Advance(500d), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(180d));
            Assert.That(session.MoveTowards("amber", Vector2Int.zero, GridAxis.X).Status, Is.EqualTo(MovementStatus.SessionInactive));
            Assert.That(session.Board.IsBlocked(new Vector2Int(1, 1)), Is.True);
            ExitRulesTests.AssertOccupancy(session.Board);
        }

        [Test]
        public void TimerExpiresOnceClampsAtZeroAndStopsTerminalInput()
        {
            var session = Session(1d); session.Start();
            Assert.That(session.Advance(0.125d), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(0.875d));
            Assert.That(session.Advance(0.875d), Is.True);
            Assert.That(session.State, Is.EqualTo(GameplayState.Failed));
            Assert.That(session.RemainingSeconds, Is.Zero);
            Assert.That(session.Advance(100d), Is.False);
            Assert.That(session.Start(), Is.False);
            Assert.That(session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.SessionInactive));
            Assert.That(session.RemainingBlocks, Is.EqualTo(2));
            Assert.That(session.Board.GetBlock("blue").Anchor, Is.EqualTo(Vector2Int.zero));
            ExitRulesTests.AssertOccupancy(session.Board);
        }

        [Test]
        public void ExitBeforeAdvanceWinsTheTieButAnAlreadyExpiredSessionCannotExit()
        {
            var session = Session(0.1d); session.Start();
            session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y);
            session.MoveTowards("amber", new Vector2Int(3, 4), GridAxis.Y);
            Assert.That(session.Advance(0.1d), Is.False);
            Assert.That(session.State, Is.EqualTo(GameplayState.Won));
            session.Restart();
            Assert.That(session.Advance(0.1d), Is.True);
            Assert.That(session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.SessionInactive));
            Assert.That(session.State, Is.EqualTo(GameplayState.Failed));
            Assert.That(session.RemainingBlocks, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedRestartRestoresBlocksOccupancyGatesObstaclesAndTimer()
        {
            var session = Session(); session.Start();
            for (var i = 0; i < 5; i++)
            {
                var oldBoard = session.Board;
                session.MoveTowards("blue", new Vector2Int(0, -1), GridAxis.Y);
                session.Advance(200d);
                Assert.That(session.State, Is.EqualTo(GameplayState.Failed));
                session.Restart();
                Assert.That(session.Board, Is.Not.SameAs(oldBoard));
                Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
                Assert.That(session.RemainingSeconds, Is.EqualTo(180d));
                Assert.That(session.RemainingBlocks, Is.EqualTo(2));
                Assert.That(session.Board.GetBlock("blue").Anchor, Is.EqualTo(Vector2Int.zero));
                Assert.That(session.Board.GetBlock("amber").Anchor, Is.EqualTo(new Vector2Int(3, 3)));
                Assert.That(session.Board.Gates.Count, Is.EqualTo(2));
                Assert.That(session.Board.IsBlocked(new Vector2Int(1, 1)), Is.True);
                ExitRulesTests.AssertOccupancy(session.Board);
            }
        }

        [Test]
        public void InvalidTimeAndUncoloredOrUngatedSessionsAreRejected()
        {
            foreach (var value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => Session(value));
            var session = Session();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(-1d));
            var board = new GridBoard(2, 2);
            board.AddBlock("uncolored", Vector2Int.zero, new[] { Vector2Int.zero });
            Assert.Throws<ArgumentException>(() => new PuzzleSession(board, new ExitGate[0], 10d));
        }

        [Test]
        public void AuthoredDemoHasTheDocumentedBlockingRelationshipAndSolution()
        {
            var session = new PuzzleSession(M2DemoLayout.CreateBoard(), M2DemoLayout.CreateGates(), M2DemoLayout.Seconds);
            session.Start();
            Assert.That(session.MoveTowards("Blue", new Vector2Int(2, 99), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock),
                "Amber must initially obstruct Blue's northbound route.");
            session.Restart();
            session.MoveTowards("Blue", new Vector2Int(0, 1), GridAxis.X);
            Assert.That(session.MoveTowards("Amber", new Vector2Int(1, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
            session.MoveTowards("Blue", new Vector2Int(2, 1), GridAxis.X);
            Assert.That(session.MoveTowards("Blue", new Vector2Int(2, 8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.MoveTowards("Green", new Vector2Int(-1, 5), GridAxis.X).Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.MoveTowards("Purple", new Vector2Int(6, 5), GridAxis.X).Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.State, Is.EqualTo(GameplayState.Won));
            Assert.That(session.RemainingBlocks, Is.Zero);
            ExitRulesTests.AssertOccupancy(session.Board);
            session.Restart();
            Assert.That(session.Board.Blocks.Sum(block => block.Cells.Count), Is.EqualTo(8));
            Assert.That(session.RemainingBlocks, Is.EqualTo(4));
            Assert.That(session.Board.Gates.Count, Is.EqualTo(4));
            ExitRulesTests.AssertOccupancy(session.Board);
        }
    }
}
