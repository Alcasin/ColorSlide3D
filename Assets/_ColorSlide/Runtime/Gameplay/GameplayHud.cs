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
        public GameplayController Gameplay => gameplay;
        public bool HasReferences => gameplay != null && timeText != null && countText != null &&
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

        private void OnEnable()
        {
            if (restart != null) restart.onClick.AddListener(Restart);
            if (retry != null) retry.onClick.AddListener(Restart);
        }
        private void OnDisable()
        {
            if (restart != null) restart.onClick.RemoveListener(Restart);
            if (retry != null) retry.onClick.RemoveListener(Restart);
        }
        private void Restart() { if (gameplay != null) gameplay.Restart(); }

        private void LateUpdate()
        {
            if (!HasReferences || gameplay.Session == null)
                return;
            var session = gameplay.Session;
            var seconds = (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0d, session.RemainingSeconds)));
            timeText.text = "Time " + seconds / 60 + ":" + (seconds % 60).ToString("00");
            countText.text = "Blocks " + session.RemainingBlocks;
            var terminal = session.State == GameplayState.Won || session.State == GameplayState.Failed;
            resultPanel.SetActive(terminal);
            if (terminal)
                resultText.text = session.State == GameplayState.Won ? "You Win!" : "Time's Up";
        }
    }
}
