using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    [DefaultExecutionOrder(-100)]
    public sealed class CampaignController : MonoBehaviour
    {
        [SerializeField] private LevelDefinition[] levels;
        [SerializeField] private LevelPresentation presentation;
        public IReadOnlyList<LevelDefinition> Levels => levels;
        public LevelPresentation Presentation => presentation;
        public CampaignSession Campaign { get; private set; }
        public GameplayController Gameplay { get; private set; }
        public bool CanNext => isActiveAndEnabled && Campaign != null && Campaign.CanNext && Gameplay != null && !Gameplay.IsPresentingExit;
        public void Configure(LevelDefinition[] definitions, LevelPresentation view)
        { levels = (LevelDefinition[])definitions.Clone(); presentation = view; }

        public CampaignSession CreateCampaign()
        {
            if (levels == null || levels.Length != 4 || levels.Any(level => level == null) || presentation == null)
                throw new InvalidOperationException("M3 requires four ordered LevelDefinition assets and its presentation.");
            presentation.ValidateResources();
            foreach (var level in levels) level.CreateSession();
            return new CampaignSession(levels.Select(level => level.Data));
        }

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            try
            {
                if (Campaign == null) { Campaign = CreateCampaign(); ShowCurrent(); }
                else if (Gameplay != null) Gameplay.gameObject.SetActive(true);
            }
            catch (Exception exception) { Debug.LogError("ColorSlide M3 could not start: " + exception.Message, this); enabled = false; }
        }
        private void ShowCurrent()
        {
            var replacement = presentation.Build(Campaign.CurrentLevel, Campaign.CurrentSession);
            ActivateReplacement(replacement);
        }
        private void ActivateReplacement(GameplayController replacement)
        {
            var previous = Gameplay;
            if (previous != null) LevelPresentation.Retire(previous.gameObject);
            Gameplay = replacement;
            replacement.gameObject.SetActive(true);
        }
        public bool Next()
        {
            if (!Application.IsPlaying(gameObject) || !CanNext) return false;
            try
            {
                GameplayController replacement = null;
                if (!Campaign.Next((data, session) => replacement = presentation.Build(data, session))) return false;
                ActivateReplacement(replacement);
                return true;
            }
            catch (Exception exception)
            { Debug.LogError("ColorSlide M3 transition stopped: " + exception.Message, this); return false; }
        }
        public void Restart() { if (Gameplay != null) Gameplay.Restart(); }
        private void OnDisable()
        {
            if (Gameplay != null) Gameplay.gameObject.SetActive(false);
        }
    }
}
