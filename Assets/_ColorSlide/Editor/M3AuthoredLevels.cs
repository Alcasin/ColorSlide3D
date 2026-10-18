using ColorSlide.Board;
using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Editor
{
    /// <summary>Creation defaults only. Runtime loads serialized LevelDefinition assets, never this catalog.</summary>
    public static class M3AuthoredLevels
    {
        public static LevelData[] Create() => new[]
        {
            Level("01-basic-exits", "Basic Exits", 180f, new[] {
                Block("Blue", BlockColor.Blue, 1, 1), Block("Amber", BlockColor.Amber, 4, 6)
            }, new[] { Gate("Blue North", BlockColor.Blue, BoardEdge.North, 1, 1),
                Gate("Amber South", BlockColor.Amber, BoardEdge.South, 4, 1) }),
            Level("02-make-space", "Make Space", 180f, new[] {
                Block("Blue", BlockColor.Blue, 2, 1), Block("Amber", BlockColor.Amber, 1, 3, Vector2Int.right),
                Block("Purple", BlockColor.Purple, 4, 5, Vector2Int.up)
            }, new[] { Gate("Blue North", BlockColor.Blue, BoardEdge.North, 2, 1),
                Gate("Amber South", BlockColor.Amber, BoardEdge.South, 1, 2),
                Gate("Purple East", BlockColor.Purple, BoardEdge.East, 5, 2) }),
            Level("03-irregular-footprints", "Irregular Footprints", 240f, new[] {
                Block("Green", BlockColor.Green, 0, 3, Vector2Int.right, Vector2Int.up),
                Block("Amber", BlockColor.Amber, 0, 5, Vector2Int.right),
                Block("Purple", BlockColor.Purple, 4, 3, Vector2Int.up)
            }, new[] { Gate("Green West", BlockColor.Green, BoardEdge.West, 5, 2),
                Gate("Amber South", BlockColor.Amber, BoardEdge.South, 0, 2),
                Gate("Purple East", BlockColor.Purple, BoardEdge.East, 3, 2) },
                new Vector2Int(2, 3), new Vector2Int(2, 4), new Vector2Int(2, 5), new Vector2Int(2, 6)),
            Level("04-combined-constraints", "Combined Constraints", 300f, new[] {
                Block("Blue", BlockColor.Blue, 2, 0), Block("Amber", BlockColor.Amber, 1, 2, Vector2Int.right),
                Block("Green", BlockColor.Green, 0, 3, Vector2Int.right, Vector2Int.up),
                Block("Key", BlockColor.Blue, 0, 5), Block("Purple", BlockColor.Purple, 2, 5, Vector2Int.up)
            }, new[] { Gate("Blue North", BlockColor.Blue, BoardEdge.North, 2, 1),
                Gate("Amber South", BlockColor.Amber, BoardEdge.South, 1, 2),
                Gate("Green West", BlockColor.Green, BoardEdge.West, 5, 2),
                Gate("Purple East", BlockColor.Purple, BoardEdge.East, 5, 2) },
                new Vector2Int(3, 3), new Vector2Int(3, 4))
        };

        private static LevelBlock Block(string id, BlockColor color, int x, int y, params Vector2Int[] extra)
        {
            var cells = new Vector2Int[extra.Length + 1];
            extra.CopyTo(cells, 1);
            return new LevelBlock { Id = id, Color = color, Anchor = new Vector2Int(x, y), Cells = cells };
        }
        private static LevelGate Gate(string id, BlockColor color, BoardEdge edge, int start, int span) =>
            new LevelGate { Id = id, Color = color, Edge = edge, Start = start, Span = span };
        private static LevelData Level(string id, string title, float seconds, LevelBlock[] blocks, LevelGate[] gates,
            params Vector2Int[] obstacles) => new LevelData { Id = id, DisplayName = title, Seconds = seconds,
                Blocks = blocks, Gates = gates, Obstacles = obstacles };
    }
}
