using System.Linq;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46CampaignTests
    {
        [TestCase(0,7,9,7,11)] [TestCase(1,8,10,8,22)]
        public void AuthoredDataHasLegalVariableDimensionsDistinctFootprintsAndFourGates(int index, int width, int height, int blocks, int obstacles)
        {
            var data = AdvancedCampaignLevels.Create()[index]; var session = data.CreateSession();
            Assert.That(data.Width, Is.EqualTo(width)); Assert.That(data.Height, Is.EqualTo(height));
            Assert.That(session.RemainingBlocks, Is.EqualTo(blocks)); Assert.That(data.Obstacles.Length, Is.EqualTo(obstacles));
            Assert.That(data.Gates.Select(gate => gate.Edge).Distinct().Count(), Is.EqualTo(4));
            Assert.That(data.Gates.Select(gate => gate.Color).Distinct().Count(), Is.EqualTo(4));
            Assert.That(data.Blocks.Any(block => block.Cells.Length == 1), Is.True);
            Assert.That(data.Blocks.Any(block => block.Cells.SequenceEqual(new[] { Vector2Int.zero, Vector2Int.right })), Is.True);
            Assert.That(data.Blocks.Any(block => block.Cells.SequenceEqual(new[] { Vector2Int.zero, Vector2Int.up })), Is.True);
            Assert.That(data.Blocks.Count(block => block.Cells.Length == 3), Is.EqualTo(2));
            CheckOccupancy(session.Board);
            foreach (var block in data.Blocks)
            foreach (var direction in new[] { Vector2Int.left,Vector2Int.right,Vector2Int.down,Vector2Int.up })
            {
                var initial = data.CreateSession(); initial.Start();
                var target = block.Anchor + direction * (width+height);
                Assert.That(initial.MoveTowards(block.Id,target,direction.x == 0 ? GridAxis.Y : GridAxis.X).Status,
                    Is.Not.EqualTo(MovementStatus.Exited), "No piece has a one-action initial exit: " + block.Id);
            }
        }

        [TestCase(0,21,65)] [TestCase(1,32,91)]
        public void WitnessUsesOnlyLegalStraightActionsAndRestartsToAFreshAuthoredSnapshot(int index, int actions, int steps)
        {
            var data = AdvancedCampaignLevels.Create()[index]; var witness = AdvancedLevelSolutions.For(index);
            Assert.That(witness.Length, Is.EqualTo(actions)); Assert.That(AdvancedLevelSolutions.Verify(data, witness), Is.EqualTo(steps));
            var session = data.CreateSession(); session.Start();
            foreach (var action in witness)
            {
                var result = session.MoveTowards(action.Block, action.Target, action.Axis);
                Assert.That(result.Status, Is.EqualTo(action.Exit ? MovementStatus.Exited : MovementStatus.Moved));
                CheckOccupancy(session.Board);
            }
            Assert.That(session.State, Is.EqualTo(GameplayState.Won));
            session.Restart(); Assert.That(session.State, Is.EqualTo(GameplayState.Playing));
            Assert.That(session.RemainingSeconds, Is.EqualTo(data.Seconds));
            foreach (var block in data.Blocks) Assert.That(session.Board.GetBlock(block.Id).Anchor, Is.EqualTo(block.Anchor));
            CheckOccupancy(session.Board);
        }

        [Test]
        public void TrafficLockHasAProvedShortestSolutionWithinASmallSearchBudget()
        {
            var data = AdvancedCampaignLevels.Create()[0];
            var result = PuzzleSolver.Search(data, 5000, 20000);
            Assert.That(result.Solved && result.Optimal, Is.True);
            Assert.That(result.Actions.Length, Is.EqualTo(21)); Assert.That(result.ProvenLowerBound, Is.EqualTo(21));
            Assert.That(result.InitialLowerBound, Is.EqualTo(20));
            AdvancedLevelSolutions.Verify(data, result.Actions);
        }

        [Test]
        public void GrandEscapeBoundedSearchReturnsAWitnessWithoutClaimingOptimality()
        {
            var data = AdvancedCampaignLevels.Create()[1]; var result = PuzzleSolver.Search(data, 5000, 20000, 2);
            Assert.That(result.Solved, Is.True); Assert.That(result.Optimal, Is.False);
            Assert.That(result.InitialLowerBound, Is.EqualTo(22)); Assert.That(result.Actions.Length, Is.EqualTo(32));
            Assert.That(result.Actions.Select(action => action.ToString()), Is.EqualTo(AdvancedLevelSolutions.For(1).Select(action => action.ToString())));
            AdvancedLevelSolutions.Verify(data, result.Actions);
        }

        [Test]
        public void SolverHonorsItsStateBudgetAndNeverReportsAnUnsolvedWitnessAsOptimal()
        {
            var result = PuzzleSolver.Search(AdvancedCampaignLevels.Create()[1], 1, 1);
            Assert.That(result.Solved || result.Optimal, Is.False); Assert.That(result.Actions, Is.Empty);
            Assert.That(result.Expanded, Is.Zero); Assert.That(result.Discovered, Is.EqualTo(1));
            Assert.That(result.Stop, Is.EqualTo("Search budget reached"));
        }

        [Test]
        public void TrafficLockNeedsTheUpperTurnAndTemporaryAmberParking()
        {
            var session = AdvancedCampaignLevels.Create()[0].CreateSession(); session.Start();
            Assert.That(session.MoveTowards("K", new Vector2Int(2,6), GridAxis.X).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            Assert.That(session.MoveTowards("P", new Vector2Int(2,7), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            Assert.That(session.MoveTowards("A", new Vector2Int(0,7), GridAxis.X).Status, Is.EqualTo(MovementStatus.Moved));
            Assert.That(session.MoveTowards("P", new Vector2Int(2,7), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Moved));
            Assert.That(session.MoveTowards("K", new Vector2Int(2,6), GridAxis.X).Status, Is.EqualTo(MovementStatus.Moved));
            Assert.That(session.MoveTowards("P", new Vector2Int(5,7), GridAxis.X).Status, Is.EqualTo(MovementStatus.Moved));
            Assert.That(session.MoveTowards("K", new Vector2Int(2,9), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
            Assert.That(session.MoveTowards("G", new Vector2Int(0,6), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            Assert.That(session.MoveTowards("A", new Vector2Int(0,8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Moved));
            Assert.That(session.MoveTowards("G", new Vector2Int(0,6), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Moved));
        }

        [Test]
        public void GrandEscapeClosesTheUpperBypassAndCreatesATwoWayParkingDependency()
        {
            var session = AdvancedCampaignLevels.Create()[1].CreateSession(); session.Start();
            Assert.That(session.MoveTowards("P", new Vector2Int(2,8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Moved));
            Assert.That(session.MoveTowards("P", new Vector2Int(4,8), GridAxis.X).Status, Is.EqualTo(MovementStatus.BlockedByObstacle));
            session.Restart();
            Assert.That(session.MoveTowards("D", new Vector2Int(4,2), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            session.MoveTowards("P", new Vector2Int(1,5), GridAxis.X); session.MoveTowards("P", new Vector2Int(1,4), GridAxis.Y);
            session.MoveTowards("K", new Vector2Int(2,6), GridAxis.X); session.MoveTowards("K", new Vector2Int(2,10), GridAxis.Y);
            session.MoveTowards("A", new Vector2Int(1,7), GridAxis.X);
            Assert.That(session.MoveTowards("G", new Vector2Int(0,6), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            Assert.That(session.MoveTowards("P", new Vector2Int(1,0), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            Assert.That(session.MoveTowards("G", new Vector2Int(0,0), GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock), "Lower Amber must clear before the L can park.");
        }

        [Test]
        public void SixLevelCampaignRequiresExplicitNextAndFinalRetryCannotCreateLevelSeven()
        {
            var original = M3AuthoredLevels.Create(); var sequence = original.Concat(AdvancedCampaignLevels.Create()).ToArray();
            var campaign = new CampaignSession(sequence);
            Assert.That(campaign.LevelCount, Is.EqualTo(6));
            for (var i = 0; i < 6; i++)
            {
                Assert.That(campaign.CurrentLevel, Is.SameAs(sequence[i])); Assert.That(campaign.CurrentIndex, Is.EqualTo(i));
                Assert.That(campaign.Next(), Is.False);
                var session = campaign.CurrentSession;
                if (i >= 4)
                {
                    Assert.That(session.Advance(sequence[i].Seconds), Is.True); Assert.That(campaign.Next(), Is.False);
                    session.Restart();
                    foreach (var action in AdvancedLevelSolutions.For(i-4)) session.MoveTowards(action.Block, action.Target, action.Axis);
                }
                else CampaignTests.Solve(session,i);
                Assert.That(session.State, Is.EqualTo(GameplayState.Won));
                Assert.That(campaign.IsComplete, Is.EqualTo(i == 5)); Assert.That(campaign.Next(), Is.EqualTo(i < 5));
            }
            campaign.CurrentSession.Restart();
            Assert.That(campaign.IsComplete || campaign.CanNext || campaign.Next(), Is.False);
            Assert.That(campaign.CurrentIndex, Is.EqualTo(5)); Assert.That(campaign.CurrentSession.RemainingBlocks, Is.EqualTo(8));
        }

        [Test]
        public void LevelFactoriesShareNoMutableDataWithAnotherCampaignOrTheAcceptedCatalog()
        {
            var before = M3AuthoredLevels.Create(); var first = AdvancedCampaignLevels.Create();
            first[0].Blocks[0].Cells[0] = new Vector2Int(99,99); first[1].Obstacles[0] = new Vector2Int(99,99);
            foreach (var level in AdvancedCampaignLevels.Create()) level.CreateSession();
            var after = M3AuthoredLevels.Create();
            for (var i = 0; i < 4; i++)
            {
                Assert.That(after[i].Id, Is.EqualTo(before[i].Id)); Assert.That(after[i].Width, Is.EqualTo(6)); Assert.That(after[i].Height, Is.EqualTo(8));
                Assert.That(after[i].Obstacles, Is.EqualTo(before[i].Obstacles));
                Assert.That(after[i].Blocks.Select(block => block.Anchor), Is.EqualTo(before[i].Blocks.Select(block => block.Anchor)));
            }
        }

        private static void CheckOccupancy(GridBoard board)
        {
            var occupied = board.Blocks.SelectMany(block => block.Cells.Select(cell => block.Anchor + cell)).ToArray();
            Assert.That(occupied.Distinct().Count(), Is.EqualTo(occupied.Length));
            foreach (var cell in occupied) { Assert.That(board.IsInside(cell), Is.True); Assert.That(board.IsBlocked(cell), Is.False); Assert.That(board.OccupantAt(cell), Is.Not.Null); }
            for (var x = 0; x < board.Width; x++) for (var y = 0; y < board.Height; y++)
                Assert.That(board.OccupantAt(new Vector2Int(x,y)) != null, Is.EqualTo(occupied.Contains(new Vector2Int(x,y))));
        }
    }
}
