using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorSlide.Gameplay
{
    /// <summary>One effective ordered catalog for FeedbackCampaign. Preserve authored entries and append
    /// missing source-authored levels by identity; never append an already referenced level twice.</summary>
    public static class FeedbackCampaignLevels
    {
        public static LevelData[] Complete(IEnumerable<LevelData> authored)
        {
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            var levels = authored.ToList(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in levels)
                if (level == null || !ids.Add(level.Id)) throw new ArgumentException("Campaign authored entries must have unique non-null identities.");
            foreach (var level in AdvancedCampaignLevels.Create().Concat(MasterCampaignLevels.Create()))
                if (ids.Add(level.Id)) levels.Add(level);
            return levels.ToArray();
        }
    }
}
