using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    /// <summary>
    /// M4.6 authored LevelData, appended only by the feedback campaign. Fresh data on each call;
    /// the accepted four persistent LevelDefinition assets are neither replaced nor mutated.
    /// Maps run north to south. Repeated letters describe actual occupied cells, not bounding boxes.
    /// Kept in source so the accepted scene needs no Editor asset-generation / Apply operation.
    /// </summary>
    public static class AdvancedCampaignLevels
    {
        public static LevelData[] Create() => new[] { TrafficLock(), GrandEscape() };

        private static LevelData TrafficLock() => Read("05-traffic-lock", "Traffic Lock", 420f, new[]
        {
            ".......", // y8: upper turning space
            ".AA....",
            "K.P#..#",
            "..P#R.#",
            "G.##RR#",
            "GG##..#",
            ".CC#...",
            ".......", // lower transfer lane
            "..B...."
        }, Gates());

        private static LevelData GrandEscape() => Read("06-grand-escape", "Grand Escape", 600f, new[]
        {
            "##......", // y9: only the right side can access the northern transfer lane
            "##.#DD..",
            "AA.#..##",
            "K.P#..##",
            "..P#R.##",
            "G.##RR##",
            "GG##...#",
            ".CC#....",
            "........",
            "..B....."
        }, Gates());

        private static LevelGate[] Gates() => new[]
        {
            Gate("Blue North", BlockColor.Blue, BoardEdge.North, 2, 1),
            Gate("Amber South", BlockColor.Amber, BoardEdge.South, 0, 2),
            Gate("Green West", BlockColor.Green, BoardEdge.West, 6, 2),
            Gate("Purple East", BlockColor.Purple, BoardEdge.East, 1, 2)
        };
        private static LevelGate Gate(string id, BlockColor color, BoardEdge edge, int start, int span) =>
            new LevelGate { Id = id, Color = color, Edge = edge, Start = start, Span = span };

        private static LevelData Read(string id, string title, float seconds, string[] rows, LevelGate[] gates)
        {
            var width = rows[0].Length; var height = rows.Length;
            if (rows.Any(row => row.Length != width)) throw new InvalidOperationException(id + ": inconsistent authored row widths.");
            var obstacles = new List<Vector2Int>(); var cells = new Dictionary<char, List<Vector2Int>>();
            for (var row = 0; row < height; row++)
            for (var x = 0; x < width; x++)
            {
                var letter = rows[row][x]; var cell = new Vector2Int(x, height - row - 1);
                if (letter == '#') obstacles.Add(cell);
                else if (letter != '.')
                {
                    if (!cells.TryGetValue(letter, out var footprint)) cells.Add(letter, footprint = new List<Vector2Int>());
                    footprint.Add(cell);
                }
            }
            // Fixed ordering also makes authoring searches reproducible across runtimes.
            var order = new[] { 'K', 'B', 'A', 'C', 'G', 'P', 'R', 'D' };
            if (cells.Keys.Except(order).Any()) throw new InvalidOperationException(id + ": unknown authored block letter.");
            var blocks = order.Where(cells.ContainsKey).Select(letter =>
            {
                var occupied = cells[letter]; var anchor = new Vector2Int(occupied.Min(cell => cell.x), occupied.Min(cell => cell.y));
                var color = letter == 'K' || letter == 'B' ? BlockColor.Blue : letter == 'A' || letter == 'C' ? BlockColor.Amber :
                    letter == 'G' || letter == 'R' ? BlockColor.Green : BlockColor.Purple;
                return new LevelBlock { Id = letter.ToString(), Color = color, Anchor = anchor,
                    Cells = occupied.Select(cell => cell - anchor).OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToArray() };
            }).ToArray();
            var data = new LevelData { Id = id, DisplayName = title, Width = width, Height = height, CellSize = 0.68f,
                Seconds = seconds, Blocks = blocks, Obstacles = obstacles.ToArray(), Gates = gates };
            data.CreateSession(); // The same footprint, overlap, obstacle and aperture validation as the SO-backed levels.
            return data;
        }
    }
}
