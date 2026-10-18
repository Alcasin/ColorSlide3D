using System.Collections.Generic;
using ColorSlide.Board;
using ColorSlide.Gameplay;

namespace ColorSlide.Polish
{
    /// <summary>Presentation-only deduplication. It never changes a gameplay session.</summary>
    public sealed class PolishCueState
    {
        private bool blockedReported, terminalReported;
        private readonly HashSet<string> exited = new HashSet<string>();
        public void Reset() { blockedReported = false; terminalReported = false; exited.Clear(); }
        public SoundCue BeginGesture() { blockedReported = false; return SoundCue.Select; }
        public SoundCue Move(string id, MovementStatus status)
        {
            if (status == MovementStatus.Exited) return exited.Add(id) ? SoundCue.Exit : SoundCue.None;
            if (!blockedReported && (status == MovementStatus.BlockedByBlock || status == MovementStatus.BlockedByObstacle ||
                status == MovementStatus.BlockedByBoundary || status == MovementStatus.InvalidGate))
            { blockedReported = true; return SoundCue.Blocked; }
            return SoundCue.None;
        }
        public SoundCue Observe(GameplayState state)
        {
            if (state == GameplayState.Playing || state == GameplayState.Ready) { terminalReported = false; return SoundCue.None; }
            if (terminalReported) return SoundCue.None;
            terminalReported = true;
            return state == GameplayState.Won ? SoundCue.Win : SoundCue.Fail;
        }
    }
}
