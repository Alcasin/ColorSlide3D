using ColorSlide.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ColorSlide.Polish
{
    [DefaultExecutionOrder(200)]
    public sealed class PolishedHudEffects : MonoBehaviour
    {
        [SerializeField] private GameplayHud hud;
        [SerializeField] private Text levelNumber;
        [SerializeField] private Text resultCaption;
        [SerializeField] private RectTransform retryButton;
        [SerializeField] private Button nextButton;
        public bool HasReferences => hud != null && levelNumber != null && resultCaption != null && retryButton != null && nextButton != null;
        public void Configure(GameplayHud owner, Text number, Text caption, Button retry, Button next)
        { hud = owner; levelNumber = number; resultCaption = caption; retryButton = retry.GetComponent<RectTransform>(); nextButton = next; }
        private void LateUpdate()
        {
            if (hud == null || hud.Campaign == null || hud.Campaign.Campaign == null || hud.Gameplay == null) return;
            var campaign = hud.Campaign.Campaign;
            levelNumber.text = "LEVEL " + (campaign.CurrentIndex + 1).ToString("00") + " / " + campaign.LevelCount.ToString("00");
            resultCaption.text = campaign.IsComplete ? "Four little puzzles, beautifully solved." :
                hud.Gameplay.Session.State == GameplayState.Failed ? "Take a breath, then try again." : "Every piece found its way home.";
            var hasNext = nextButton.gameObject.activeSelf;
            var min = new Vector2(hasNext ? 0.08f : 0.30f, 0.12f); var max = new Vector2(hasNext ? 0.46f : 0.70f, 0.30f);
            if (retryButton.anchorMin != min || retryButton.anchorMax != max)
            { retryButton.anchorMin = min; retryButton.anchorMax = max; retryButton.offsetMin = retryButton.offsetMax = Vector2.zero; }
        }
    }
}
