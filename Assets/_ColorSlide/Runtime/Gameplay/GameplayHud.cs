using System;
using UnityEngine;
using UnityEngine.UI;

namespace ColorSlide.Gameplay
{
    public sealed class GameplayHud : MonoBehaviour
    {
        [SerializeField] private GameplayController gameplay;
        [SerializeField] private Text timeText;
        [SerializeField] private Text countText;
        [SerializeField] private Button restart;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private Button retry;
        [SerializeField] private CampaignController campaign;
        [SerializeField] private Text levelText;
        [SerializeField] private Button next;
        [SerializeField] private bool polishedLabels;
        public void ConfigurePolishedLabels() => polishedLabels = true;
        public GameplayController Gameplay => campaign != null ? campaign.Gameplay : gameplay;
        public CampaignController Campaign => campaign;
        public bool HasCampaignReferences => campaign != null && levelText != null && next != null;
        public bool HasReferences => (gameplay != null || campaign != null) && timeText != null && countText != null &&
            restart != null && resultPanel != null && resultText != null && retry != null;

        public void Configure(GameplayController owner, Text timer, Text count, Button restartButton,
            GameObject panel, Text message, Button retryButton)
        {
            gameplay = owner;
            timeText = timer;
            countText = count;
            restart = restartButton;
            resultPanel = panel;
            resultText = message;
            retry = retryButton;
        }

        public void ConfigureCampaign(CampaignController owner, Text level, Button nextButton)
        { campaign = owner; gameplay = null; levelText = level; next = nextButton; }

        private void OnEnable()
        {
            if (restart != null) restart.onClick.AddListener(Restart);
            if (retry != null) retry.onClick.AddListener(Restart);
            if (next != null) next.onClick.AddListener(Next);
        }
        private void OnDisable()
        {
            if (restart != null) restart.onClick.RemoveListener(Restart);
            if (retry != null) retry.onClick.RemoveListener(Restart);
            if (next != null) next.onClick.RemoveListener(Next);
        }
        private void Restart() { if (campaign != null) campaign.Restart(); else if (gameplay != null) gameplay.Restart(); }
        private void Next() { if (campaign != null) campaign.Next(); }

        private void LateUpdate()
        {
            var owner = Gameplay;
            if (!HasReferences || owner == null || owner.Session == null)
                return;
            var session = owner.Session;
            var seconds = (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0d, session.RemainingSeconds)));
            timeText.text = (polishedLabels ? "" : "Time ") + seconds / 60 + ":" + (seconds % 60).ToString("00");
            countText.text = polishedLabels ? session.RemainingBlocks + " left" : "Blocks " + session.RemainingBlocks;
            var terminal = session.State == GameplayState.Won || session.State == GameplayState.Failed;
            resultPanel.SetActive(terminal);
            if (terminal)
                resultText.text = campaign != null && campaign.Campaign.IsComplete ? "Campaign Complete!" :
                    session.State == GameplayState.Won ? "You Win!" : "Time's Up";
            if (HasCampaignReferences && campaign.Campaign != null)
            {
                levelText.text = polishedLabels ? campaign.Campaign.CurrentLevel.DisplayName :
                    "Level " + (campaign.Campaign.CurrentIndex + 1) + " / " + campaign.Campaign.LevelCount + " - " + campaign.Campaign.CurrentLevel.DisplayName;
                next.gameObject.SetActive(campaign.Campaign.CanNext);
                next.interactable = campaign.CanNext;
            }
        }
    }
}
