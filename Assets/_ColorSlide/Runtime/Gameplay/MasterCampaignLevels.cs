using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    /// <summary>Two source-authored master challenges. Maps run north to south; letters are occupied
    /// cells, not bounding boxes. Fresh LevelData uses the existing production model and presentation.</summary>
    public static class MasterCampaignLevels
    {
        public static LevelData[] Create() => new[]
        {
            Read("07-chain-reaction", "Chain Reaction", 720f, new[]
            {
                "##..EE..",
                "##.#....",
                "##.#DD..",
                "AA.#..##",
                "K.P#..##",
                "..P#R.##",
                "G.##RR##",
                "GG##...#",
                ".CC#F...",
                "....FF..",
                "..B....."
            }),
            Read("08-final-lockdown", "Final Lockdown", 900f, new[]
            {
                "##..EE..",
                "##.#I...",
                "##.#II..",
                "AA.#..##",
                "K.P#DD##",
                "..P#R.##",
                "G.##RR##",
                "GG##...#",
                "BCC#F...",
                "..H.FF..",
                "..H....."
            })
        };

        private static LevelData Read(string id, string title, float seconds, string[] rows)
        {
            var width = rows[0].Length;
            if (rows.Any(row => row.Length != width)) throw new InvalidOperationException(id + ": inconsistent authored row widths.");
            var occupied = new Dictionary<char,List<Vector2Int>>(); var obstacles = new List<Vector2Int>();
            for (var row = 0; row < rows.Length; row++) for (var x = 0; x < width; x++)
            {
                var letter = rows[row][x]; var cell = new Vector2Int(x,rows.Length-row-1);
                if (letter == '#') obstacles.Add(cell);
                else if (letter != '.')
                {
                    if (!occupied.TryGetValue(letter,out var cells)) occupied.Add(letter,cells = new List<Vector2Int>());
                    cells.Add(cell);
                }
            }
            var order = new[] { 'K','B','A','C','G','P','R','D','E','F','H','I' };
            if (occupied.Keys.Except(order).Any()) throw new InvalidOperationException(id + ": unknown block letter.");
            var blocks = order.Where(occupied.ContainsKey).Select(letter =>
            {
                var cells = occupied[letter]; var anchor = new Vector2Int(cells.Min(cell => cell.x),cells.Min(cell => cell.y));
                var color = letter == 'K' || letter == 'B' || letter == 'H' ? BlockColor.Blue :
                    letter == 'A' || letter == 'C' || letter == 'E' || letter == 'I' ? BlockColor.Amber :
                    letter == 'G' || letter == 'R' || letter == 'F' ? BlockColor.Green : BlockColor.Purple;
                return new LevelBlock { Id = letter.ToString(), Color = color, Anchor = anchor,
                    Cells = cells.Select(cell => cell-anchor).OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToArray() };
            }).ToArray();
            var data = new LevelData { Id = id, DisplayName = title, Width = width, Height = rows.Length,
                CellSize = 0.68f, Seconds = seconds, Blocks = blocks, Obstacles = obstacles.ToArray(), Gates = new[]
                {
                    Gate("Blue North",BlockColor.Blue,BoardEdge.North,2,1),
                    Gate("Amber South",BlockColor.Amber,BoardEdge.South,0,2),
                    Gate("Green West",BlockColor.Green,BoardEdge.West,6,2),
                    Gate("Purple East",BlockColor.Purple,BoardEdge.East,1,2)
                } };
            data.CreateSession(); return data;
        }
        private static LevelGate Gate(string id, BlockColor color, BoardEdge edge, int start, int span) =>
            new LevelGate { Id = id, Color = color, Edge = edge, Start = start, Span = span };
    }
}
