using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace ColorSlide.Polish
{
    [DefaultExecutionOrder(100)]
    public sealed class PolishFeedback : MonoBehaviour
    {
        [SerializeField] private CampaignController campaign;
        [SerializeField] private SoftToyTheme theme;
        [SerializeField] private AudioSource source;
        [SerializeField] private Button[] buttons;
        private GameplayController current;
        private readonly PolishCueState cues = new PolishCueState();
        public SoftToyTheme Theme => theme;
        public CampaignController Campaign => campaign;
        public AudioSource Source => source;
        public bool HasReferences => campaign != null && theme != null && source != null && buttons != null &&
            buttons.Length == 3 && buttons.All(button => button != null) && buttons.Distinct().Count() == 3;
        public void Configure(CampaignController owner, SoftToyTheme assets, AudioSource audio, Button[] uiButtons)
        { campaign = owner; theme = assets; source = audio; buttons = uiButtons.Distinct().ToArray(); }
        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject) || !HasReferences) return;
            campaign.GameplayChanged += Bind;
            Bind(campaign.Gameplay);
            foreach (var button in buttons) if (button != null) button.onClick.AddListener(UiClick);
        }
        private void OnDisable()
        {
            if (campaign != null) campaign.GameplayChanged -= Bind;
            Bind(null);
            if (buttons != null) foreach (var button in buttons) if (button != null) button.onClick.RemoveListener(UiClick);
            if (source != null) source.Stop();
        }
        private void Bind(GameplayController owner)
        {
            if (current != null)
            {
                current.Drag.SelectionStarted -= Selected; current.Drag.MovementRequested -= Moved; current.Restarted -= ResetCues;
            }
            current = owner; ResetCues();
            if (current != null)
            {
                current.Drag.SelectionStarted += Selected; current.Drag.MovementRequested += Moved; current.Restarted += ResetCues;
            }
        }
        private void ResetCues() { cues.Reset(); if (source != null) source.Stop(); }
        private void Selected(GridBlockView view) => Play(cues.BeginGesture());
        private void Moved(GridBlockView view, MovementResult result) => Play(cues.Move(view.BlockId, result.Status));
        private void UiClick() => Play(SoundCue.UiClick);
        private void LateUpdate() { if (current != null && current.Session != null) Play(cues.Observe(current.Session.State)); }
        private void Play(SoundCue cue)
        {
            if (cue == SoundCue.None || source == null || theme == null) return;
            var clip = theme.Clip(cue); if (clip != null) source.PlayOneShot(clip);
        }
    }
}
