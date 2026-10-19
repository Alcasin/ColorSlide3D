using System;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46MasterTests
    {
        [TestCase(0,10,21,3,720)] [TestCase(1,12,26,4,900)]
        public void MasterLayoutsHaveLegalOccupancyAllShapesAndMatchingGates(int index, int blocks, int cells, int elbows, int seconds)
        {
            var level = MasterCampaignLevels.Create()[index]; var session = level.CreateSession();
            Assert.That(level.Width, Is.EqualTo(8)); Assert.That(level.Height, Is.EqualTo(11));
            Assert.That(level.Seconds, Is.EqualTo(seconds)); Assert.That(level.Blocks.Length, Is.EqualTo(blocks));
            Assert.That(level.Obstacles.Length, Is.EqualTo(25)); Assert.That(level.Gates.Length, Is.EqualTo(4));
            Assert.That(level.Blocks.Sum(block => block.Cells.Length), Is.EqualTo(cells));
            Assert.That(level.Blocks.Count(block => block.Cells.Length == 3), Is.EqualTo(elbows));
            Assert.That(level.Blocks.Any(block => block.Cells.SequenceEqual(new[] { Vector2Int.zero,Vector2Int.right })), Is.True);
            Assert.That(level.Blocks.Any(block => block.Cells.SequenceEqual(new[] { Vector2Int.zero,Vector2Int.up })), Is.True);
            Assert.That(level.Blocks.Count(block => block.Cells.Length == 1), Is.EqualTo(2));
            CheckOccupancy(session.Board);
            foreach (var definition in level.Blocks)
            foreach (var direction in new[] { Vector2Int.left,Vector2Int.right,Vector2Int.down,Vector2Int.up })
            {
                var initial = level.CreateSession(); initial.Start();
                Assert.That(initial.MoveTowards(definition.Id,definition.Anchor+direction*30,direction.x == 0 ? GridAxis.Y : GridAxis.X).Status,
                    Is.Not.EqualTo(MovementStatus.Exited), "No initial one-action exit: " + definition.Id);
            }
        }

        [TestCase(0,41,119)] [TestCase(1,64,168)]
        public void StoredWitnessSolvesUsingRealMovesAndExactFootprintOccupancy(int index, int count, int steps)
        {
            var level = MasterCampaignLevels.Create()[index]; var actions = MasterLevelSolutions.For(index);
            Assert.That(actions.Length, Is.EqualTo(count)); Assert.That(AdvancedLevelSolutions.Verify(level,actions), Is.EqualTo(steps));
            var session = level.CreateSession(); session.Start();
            foreach (var action in actions)
            {
                var before = session.RemainingBlocks; var result = session.MoveTowards(action.Block,action.Target,action.Axis);
                Assert.That(result.Status, Is.EqualTo(action.Exit ? MovementStatus.Exited : MovementStatus.Moved));
                if (action.Exit) Assert.That(result.Exit.Block.ColorId, Is.EqualTo(result.Exit.Gate.ColorId));
                else Assert.That(session.Board.GetBlock(action.Block).Anchor, Is.EqualTo(action.Target));
                Assert.That(session.RemainingBlocks, Is.EqualTo(before-(action.Exit ? 1 : 0))); CheckOccupancy(session.Board);
            }
            Assert.That(session.State, Is.EqualTo(GameplayState.Won));
        }

        [TestCase(0)] [TestCase(1)]
        public void WitnessHasNoUnnecessarySingleActionOrActionPair(int index)
        {
            var level = MasterCampaignLevels.Create()[index]; var actions = MasterLevelSolutions.For(index);
            Assert.That(PuzzleSolver.SimplifyWitness(level,actions).Select(action => action.ToString()), Is.EqualTo(actions.Select(action => action.ToString())));
        }

        [Test]
        public void ChainReactionNeedsStagedRightBayAndLeftHandParking()
        {
            var level = MasterCampaignLevels.Create()[0]; var session = level.CreateSession(); session.Start();
            Assert.That(session.MoveTowards("R",new Vector2Int(4,0),GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            Assert.That(session.MoveTowards("E",new Vector2Int(4,1),GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            foreach (var action in MasterLevelSolutions.For(0).Take(14)) session.MoveTowards(action.Block,action.Target,action.Axis);
            Assert.That(session.Board.GetBlock("F").Anchor, Is.EqualTo(new Vector2Int(2,1)));
            Assert.That(session.MoveTowards("E",new Vector2Int(0,1),GridAxis.X).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            Assert.That(session.MoveTowards("F",new Vector2Int(0,1),GridAxis.X).Status, Is.EqualTo(MovementStatus.BlockedByBlock), "C must leave before F can shift left.");
        }

        [Test]
        public void FinalLockdownTallBlueCannotUseTheUpperBypassAndAmberElbowNeedsTheSidePocket()
        {
            var session = MasterCampaignLevels.Create()[1].CreateSession(); session.Start();
            Assert.That(session.MoveTowards("H",new Vector2Int(2,10),GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            Assert.That(session.MoveTowards("I",new Vector2Int(4,-2),GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            session.Restart();
            foreach (var action in MasterLevelSolutions.For(1).Take(15)) session.MoveTowards(action.Block,action.Target,action.Axis);
            Assert.That(session.Board.GetBlock("I").Anchor, Is.EqualTo(new Vector2Int(6,2)));
            Assert.That(session.MoveTowards("I",new Vector2Int(6,0),GridAxis.Y).Status, Is.EqualTo(MovementStatus.BlockedByBlock));
            Assert.That(session.Board.GetBlock("H").Anchor, Is.EqualTo(new Vector2Int(1,0)));
        }

        [Test]
        public void EffectiveCatalogIsEightOrderedUniqueLevelsAndCompletionIsIdempotent()
        {
            var firstSix = M3AuthoredLevels.Create().Concat(AdvancedCampaignLevels.Create()).ToArray();
            var sequence = FeedbackCampaignLevels.Complete(firstSix);
            Assert.That(sequence.Length, Is.EqualTo(8)); Assert.That(sequence.Select(level => level.Id).Distinct().Count(), Is.EqualTo(8));
            Assert.That(sequence.Take(6), Is.EqualTo(firstSix));
            Assert.That(sequence.Skip(6).Select(level => level.DisplayName), Is.EqualTo(new[] { "Chain Reaction","Final Lockdown" }));
            Assert.That(FeedbackCampaignLevels.Complete(sequence), Is.EqualTo(sequence), "Already referenced additions cannot inflate the count.");
            Assert.Throws<ArgumentException>(() => FeedbackCampaignLevels.Complete(new[] { firstSix[0],firstSix[0] }));
        }

        [Test]
        public void AuthoredOverridesAndUnrelatedExistingLevelsArePreservedInsteadOfOverwritten()
        {
            var custom = AdvancedCampaignLevels.Create()[0]; custom.DisplayName = "Authored variation";
            var extra = MasterCampaignLevels.Create()[0]; extra.Id = "existing-extra";
            var baseLevels = M3AuthoredLevels.Create().Concat(new[] { custom,extra }).ToArray();
            var complete = FeedbackCampaignLevels.Complete(baseLevels);
            Assert.That(complete.Take(baseLevels.Length), Is.EqualTo(baseLevels));
            Assert.That(complete.Single(level => level.Id == custom.Id), Is.SameAs(custom));
            Assert.That(complete, Does.Contain(extra)); Assert.That(complete.Length, Is.EqualTo(9));
        }

        [TestCase(0)] [TestCase(1)]
        public void RetryAndEditorSelectionRestoreFreshTimersThenUseNormalNextAndFinality(int index)
        {
            var campaign = new CampaignSession(FeedbackCampaignLevels.Complete(M3AuthoredLevels.Create()));
            Assert.That(campaign.StartEditorLevel(6+index), Is.True); var level = campaign.CurrentLevel;
            var first = campaign.CurrentSession;
            foreach (var action in MasterLevelSolutions.For(index).Take(8)) first.MoveTowards(action.Block,action.Target,action.Axis);
            first.Advance(10000); Assert.That(campaign.Next(), Is.False); first.Restart();
            Assert.That(first.RemainingSeconds, Is.EqualTo(level.Seconds)); Assert.That(first.RemainingBlocks, Is.EqualTo(level.Blocks.Length));
            foreach (var block in level.Blocks) Assert.That(first.Board.GetBlock(block.Id).Anchor, Is.EqualTo(block.Anchor));
            foreach (var action in MasterLevelSolutions.For(index)) first.MoveTowards(action.Block,action.Target,action.Axis);
            Assert.That(first.State, Is.EqualTo(GameplayState.Won)); Assert.That(campaign.IsComplete, Is.EqualTo(index == 1));
            Assert.That(campaign.Next(), Is.EqualTo(index == 0));
            Assert.That(campaign.StartEditorLevel(7), Is.True); Assert.That(campaign.CurrentSession, Is.Not.SameAs(first));
            Assert.That(campaign.CurrentSession.RemainingSeconds, Is.EqualTo(900)); Assert.That(campaign.CanNext, Is.False);
            Assert.That(new CampaignSession(FeedbackCampaignLevels.Complete(M3AuthoredLevels.Create())).CurrentIndex, Is.Zero);
        }

        [Test]
        public void FactoriesReturnIndependentSnapshotsWithoutChangingTheAcceptedSix()
        {
            var baseline = AdvancedCampaignLevels.Create(); var master = MasterCampaignLevels.Create();
            master[0].Blocks[0].Cells[0] = new Vector2Int(100,100); master[1].Obstacles[0] = new Vector2Int(100,100);
            foreach (var data in MasterCampaignLevels.Create()) data.CreateSession();
            var after = AdvancedCampaignLevels.Create();
            for (var i = 0; i < 2; i++)
            { Assert.That(after[i].Obstacles, Is.EqualTo(baseline[i].Obstacles)); Assert.That(after[i].Blocks.Select(block => block.Anchor), Is.EqualTo(baseline[i].Blocks.Select(block => block.Anchor))); }
        }

        private static void CheckOccupancy(GridBoard board)
        {
            var cells = board.Blocks.SelectMany(block => block.Cells.Select(cell => block.Anchor+cell)).ToArray();
            Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Length));
            for (var x = 0; x < board.Width; x++) for (var y = 0; y < board.Height; y++)
            {
                var cell = new Vector2Int(x,y); Assert.That(board.OccupantAt(cell) != null, Is.EqualTo(cells.Contains(cell)));
                if (board.OccupantAt(cell) != null) Assert.That(board.IsBlocked(cell), Is.False);
            }
        }
    }
}
