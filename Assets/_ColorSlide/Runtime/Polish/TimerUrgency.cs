using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Polish
{
    [DefaultExecutionOrder(300)]
    public sealed class TimerUrgency : MonoBehaviour
    {
        [SerializeField] private GameplayHud hud;
        [SerializeField] private RoundedPanel pill;
        [SerializeField] private ResultReveal reveal;
        [SerializeField, Min(0.01f)] private float threshold = FeedbackMotion.WarningSeconds;
        private GameplayController current;
        private Color original;
        private bool captured;
        public bool IsUrgent { get; private set; }
        public GameplayHud Hud => hud;
        public RoundedPanel Pill => pill;
        public ResultReveal Reveal => reveal;
        public float Threshold => threshold;
        public void Configure(GameplayHud owner, RoundedPanel timer, ResultReveal result)
        { hud = owner; pill = timer; reveal = result; result.ConfigureFeedback(owner); }
        private void OnEnable()
        {
            if (pill != null) { original = pill.color; captured = true; }
            if (current != null) current.Restarted += ResetPresentation;
            Refresh();
        }
        private void Bind(GameplayController owner)
        {
            if (current != null) current.Restarted -= ResetPresentation;
            ResetPresentation(); current = owner;
            if (current != null) current.Restarted += ResetPresentation;
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (hud == null || pill == null || reveal == null) return;
            if (!captured) { original = pill.color; captured = true; }
            if (current != hud.Gameplay) Bind(hud.Gameplay);
            if (current == null || !current.isActiveAndEnabled || current.Session == null) { ResetPresentation(); return; }
            var session = current.Session;
            if (session.State == GameplayState.Playing) reveal.ResetCelebration();
            IsUrgent = !float.IsNaN(threshold) && !float.IsInfinity(threshold) && threshold > 0f &&
                FeedbackMotion.IsUrgent(session.State, session.RemainingSeconds, threshold);
            pill.color = IsUrgent ? FeedbackMotion.TimerTint(original, session.RemainingSeconds, threshold) : original;
        }
        public void ResetPresentation()
        {
            IsUrgent = false; if (captured && pill != null) pill.color = original;
            if (reveal != null) reveal.ResetCelebration();
        }
        private void OnDisable()
        {
            if (current != null) current.Restarted -= ResetPresentation;
            IsUrgent = false; if (captured && pill != null) pill.color = original;
            if (reveal != null) reveal.CancelFeedback();
            captured = false; // Keep the per-Win latch across a cosmetic component disable/re-enable.
        }
    }
}
