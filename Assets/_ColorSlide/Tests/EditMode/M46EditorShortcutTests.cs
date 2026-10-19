using System;
using System.Linq;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using NUnit.Framework;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M46EditorShortcutTests
    {
        [Test]
        public void EditorSelectionStagesFreshSessionsPreservesNextAndCannotPersistOrPartiallyCommit()
        {
            var levels = M3AuthoredLevels.Create().Concat(AdvancedCampaignLevels.Create()).ToArray();
            var campaign = new CampaignSession(levels); var original = campaign.CurrentSession;
            Assert.That(campaign.StartEditorLevel(4, (data, session) =>
            {
                Assert.That(campaign.CurrentIndex, Is.Zero); Assert.That(campaign.CurrentSession, Is.SameAs(original));
                Assert.That(data.DisplayName, Is.EqualTo("Traffic Lock"));
                Assert.That(session.State, Is.EqualTo(GameplayState.Playing)); Assert.That(session.RemainingSeconds, Is.EqualTo(420));
            }), Is.True);
            Assert.That(original.State, Is.EqualTo(GameplayState.Playing), "Skipping does not fabricate wins.");
            Assert.That(campaign.CurrentIndex, Is.EqualTo(4)); Assert.That(campaign.Next(), Is.False);
            var previous = campaign.CurrentSession; previous.Advance(10000);
            Assert.That(campaign.StartEditorLevel(5), Is.True); Assert.That(campaign.CurrentSession, Is.Not.SameAs(previous));
            Assert.That(campaign.CurrentSession.RemainingSeconds, Is.EqualTo(600));
            foreach (var action in AdvancedLevelSolutions.For(1)) campaign.CurrentSession.MoveTowards(action.Block,action.Target,action.Axis);
            Assert.That(campaign.IsComplete, Is.True); Assert.That(campaign.Next(), Is.False);

            Assert.That(campaign.StartEditorLevel(4), Is.True); previous = campaign.CurrentSession;
            Assert.That(previous.State, Is.EqualTo(GameplayState.Playing)); Assert.That(previous.RemainingBlocks, Is.EqualTo(7));
            Assert.That(previous.RemainingSeconds, Is.EqualTo(420)); Assert.That(campaign.IsComplete, Is.False);
            Assert.That(campaign.StartEditorLevel(-1), Is.False); Assert.That(campaign.StartEditorLevel(6), Is.False);
            Assert.Throws<InvalidOperationException>(() => campaign.StartEditorLevel(5, (data, session) => throw new InvalidOperationException("Preparation failed")));
            Assert.That(campaign.CurrentIndex, Is.EqualTo(4)); Assert.That(campaign.CurrentSession, Is.SameAs(previous));
            foreach (var action in AdvancedLevelSolutions.For(0)) previous.MoveTowards(action.Block,action.Target,action.Axis);
            Assert.That(campaign.CanNext, Is.True); Assert.That(campaign.Next(), Is.True);
            Assert.That(campaign.CurrentIndex, Is.EqualTo(5)); Assert.That(campaign.CurrentSession.State, Is.EqualTo(GameplayState.Playing));
            Assert.That(campaign.StartEditorLevel(5), Is.True); Assert.That(campaign.CurrentSession.RemainingSeconds, Is.EqualTo(600));
            Assert.That(new CampaignSession(levels).CurrentIndex, Is.Zero, "The QA selection is only in this live session.");
        }
    }
}
