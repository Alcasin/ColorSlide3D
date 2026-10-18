using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorSlide.Gameplay
{
    /// <summary>Ordered, in-memory progression; the existing PuzzleSession owns all individual puzzle rules.</summary>
    public sealed class CampaignSession
    {
        private readonly LevelData[] levels;
        public int CurrentIndex { get; private set; }
        public int LevelCount => levels.Length;
        public LevelData CurrentLevel => levels[CurrentIndex];
        public PuzzleSession CurrentSession { get; private set; }
        public bool IsComplete => CurrentIndex == levels.Length - 1 && CurrentSession.State == GameplayState.Won;
        public bool CanNext => CurrentSession.State == GameplayState.Won && CurrentIndex + 1 < levels.Length;

        public CampaignSession(IEnumerable<LevelData> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            levels = definitions.ToArray();
            if (levels.Length == 0 || levels.Any(level => level == null))
                throw new ArgumentException("A campaign needs non-null levels.");
            if (levels.Select(level => level.Id).Distinct(StringComparer.Ordinal).Count() != levels.Length)
                throw new ArgumentException("Campaign level identities must be unique.");
            foreach (var level in levels) level.CreateSession(); // Reject invalid later levels before starting.
            CurrentSession = levels[0].CreateSession();
            CurrentSession.Start();
        }

        public bool Next(Action<LevelData, PuzzleSession> prepare = null)
        {
            if (!CanNext) return false;
            var next = levels[CurrentIndex + 1].CreateSession();
            next.Start();
            // Runtime can stage an inactive hierarchy before progression commits. A construction failure keeps this level.
            prepare?.Invoke(levels[CurrentIndex + 1], next);
            CurrentIndex++;
            CurrentSession = next;
            return true;
        }
    }
}
