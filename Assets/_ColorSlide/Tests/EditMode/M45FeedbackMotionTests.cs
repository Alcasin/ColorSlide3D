using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M45FeedbackMotionTests
    {
        [TestCase(false)] [TestCase(true)]
        public void ContactAndGateEnvelopesAreBoundedAndReturnExactlyToRest(bool gate)
        {
            var duration = gate ? FeedbackMotion.GateSeconds : FeedbackMotion.BlockedSeconds;
            System.Func<float, float> strength = gate ? FeedbackMotion.GateStrength : FeedbackMotion.BlockedStrength;
            Assert.That(duration, Is.InRange(gate ? 0.25f : 0.16f, gate ? 0.35f : 0.20f));
            Assert.That(strength(-1f), Is.Zero); Assert.That(strength(0f), Is.Zero);
            Assert.That(strength(duration / 2f), Is.EqualTo(1f).Within(0.00001f));
            for (var step = 0; step <= 100; step++) Assert.That(strength(duration * step / 100f), Is.InRange(0f, 1f));
            Assert.That(strength(duration), Is.Zero); Assert.That(strength(100f), Is.Zero);
        }
        [Test]
        public void BlockedSquashIsVisibleInXZAndAlwaysStaysInsideTheOccupiedCell()
        {
            Assert.That(FeedbackMotion.BlockedScale(0f), Is.EqualTo(Vector3.one));
            var peak = FeedbackMotion.BlockedScale(0.09f);
            Assert.That(peak.x, Is.InRange(0.91f, 0.93f)); Assert.That(peak.z, Is.EqualTo(peak.x));
            Assert.That(peak.y, Is.InRange(0.89f, 0.91f));
            for (var step = 0; step <= 100; step++)
            {
                var scale = FeedbackMotion.BlockedScale(FeedbackMotion.BlockedSeconds * step / 100f);
                Assert.That(scale.x, Is.InRange(0.91f, 1f)); Assert.That(scale.z, Is.InRange(0.91f, 1f));
                Assert.That(scale.y, Is.InRange(0.89f, 1f));
            }
            Assert.That(FeedbackMotion.BlockedScale(0.18f), Is.EqualTo(Vector3.one));
        }
        [Test]
        public void UrgencyUsesTheExactAuthoritativeThresholdAndOnlyPlayingState()
        {
            Assert.That(FeedbackMotion.IsUrgent(GameplayState.Playing, 15.00001d, 15f), Is.False);
            Assert.That(FeedbackMotion.IsUrgent(GameplayState.Playing, 15d, 15f), Is.True);
            Assert.That(FeedbackMotion.IsUrgent(GameplayState.Playing, 0.001d, 15f), Is.True);
            Assert.That(FeedbackMotion.IsUrgent(GameplayState.Playing, 0d, 15f), Is.False);
            foreach (var state in new[] { GameplayState.Ready, GameplayState.Won, GameplayState.Failed })
                Assert.That(FeedbackMotion.IsUrgent(state, 10d, 15f), Is.False);
            Assert.That(FeedbackMotion.IsUrgent(GameplayState.Playing, 4d, 4f), Is.True);
        }
        [Test]
        public void TimerTintTransitionsContinuouslyPreservesAlphaAndBreathesSlowly()
        {
            var original = new Color(0.94f, 0.96f, 0.98f, 0.7f);
            Assert.That(FeedbackMotion.TimerTint(original, 15d, 15f), Is.EqualTo(original));
            var previous = original;
            for (var step = 1; step <= 1500; step++)
            {
                var current = FeedbackMotion.TimerTint(original, 15d - step * 0.01d, 15f);
                Assert.That(current.a, Is.EqualTo(original.a));
                Assert.That(Mathf.Abs(current.r - previous.r) + Mathf.Abs(current.g - previous.g) + Mathf.Abs(current.b - previous.b), Is.LessThan(0.03f));
                Assert.That(current.r, Is.GreaterThanOrEqualTo(0.93f)); Assert.That(current.g, Is.GreaterThan(0.79f));
                previous = current;
            }
            var first = FeedbackMotion.TimerTint(original, 14d, 15f); var next = FeedbackMotion.TimerTint(original, 11.6d, 15f);
            Assert.That(first.r, Is.EqualTo(next.r).Within(0.00001f)); Assert.That(first.g, Is.EqualTo(next.g).Within(0.00001f));
            Assert.That(first.b, Is.EqualTo(next.b).Within(0.00001f));
        }
        [Test]
        public void WinScaleEaseIsShortMonotonicAndCannotOvershootTheApprovedCard()
        {
            Assert.That(FeedbackMotion.WinSeconds, Is.InRange(0.5f, 0.7f));
            var previous = 0f;
            for (var step = 0; step <= 100; step++)
            {
                var ease = FeedbackMotion.WinEase(FeedbackMotion.WinSeconds * step / 100f);
                Assert.That(ease, Is.InRange(previous, 1f)); previous = ease;
            }
            Assert.That(previous, Is.EqualTo(1f)); Assert.That(FeedbackMotion.WinEase(100f), Is.EqualTo(1f));
        }
    }
}
