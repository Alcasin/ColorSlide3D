using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    /// <summary>One authored graybox, shared by scene setup and its solution regression test. Not a level framework.</summary>
    public static class M2DemoLayout
    {
        public const float CellSize = 0.68f; // Leaves room for full exit animations in the accepted portrait camera.
        public const float Seconds = 180f;

        public static GridBoard CreateBoard()
        {
            var board = new GridBoard(6, 8, new[] { new Vector2Int(3, 3), new Vector2Int(3, 4) });
            board.AddBlock("Blue", new Vector2Int(2, 1), new[] { Vector2Int.zero }, BlockColor.Blue);
            board.AddBlock("Amber", new Vector2Int(1, 3), new[] { Vector2Int.zero, Vector2Int.right }, BlockColor.Amber);
            board.AddBlock("Green", new Vector2Int(0, 5), new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up }, BlockColor.Green);
            board.AddBlock("Purple", new Vector2Int(4, 5), new[] { Vector2Int.zero, Vector2Int.up }, BlockColor.Purple);
            return board;
        }

        public static ExitGate[] CreateGates() => new[]
        {
            new ExitGate("Blue North", BlockColor.Blue, BoardEdge.North, 2, 1),
            new ExitGate("Amber South", BlockColor.Amber, BoardEdge.South, 1, 2),
            new ExitGate("Green West", BlockColor.Green, BoardEdge.West, 5, 2),
            new ExitGate("Purple East", BlockColor.Purple, BoardEdge.East, 5, 2)
        };
    }
}
