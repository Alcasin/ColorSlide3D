using System;
using System.Linq;
using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    [Serializable]
    public sealed class LevelBlock
    {
        public string Id;
        public BlockColor Color;
        public Vector2Int Anchor;
        public Vector2Int[] Cells;
    }

    [Serializable]
    public sealed class LevelGate
    {
        public string Id;
        public BlockColor Color;
        public BoardEdge Edge;
        public int Start;
        public int Span;
        public ExitGate CreateGate() => new ExitGate(Id, Color, Edge, Start, Span);
    }

    /// <summary>Serializable authoring data. Validation delegates collision/exit rules to the accepted board model.</summary>
    [Serializable]
    public sealed class LevelData
    {
        public string Id;
        public string DisplayName;
        public int Width = 6;
        public int Height = 8;
        public float CellSize = 0.68f;
        public float Seconds = 180f;
        public LevelBlock[] Blocks;
        public Vector2Int[] Obstacles = Array.Empty<Vector2Int>();
        public LevelGate[] Gates;

        public PuzzleSession CreateSession()
        {
            var label = "Level '" + (string.IsNullOrWhiteSpace(Id) ? "<missing id>" : Id) + "'";
            try
            {
                if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName))
                    throw new ArgumentException("Identity and display name are required.");
                if (float.IsNaN(CellSize) || float.IsInfinity(CellSize) || CellSize <= 0f)
                    throw new ArgumentException("Cell size must be finite and positive.");
                if (Blocks == null || Blocks.Length == 0 || Obstacles == null || Gates == null || Gates.Length == 0)
                    throw new ArgumentException("Blocks, obstacles and gates must be explicitly specified; blocks and gates cannot be empty.");
                GridBoard board;
                try { board = new GridBoard(Width, Height, Obstacles); }
                catch (ArgumentException exception)
                { throw new ArgumentException("Board " + Width + "x" + Height + ", obstacles [" + string.Join(", ", Obstacles.Select(cell => cell.ToString())) + "]: " + exception.Message, exception); }
                foreach (var block in Blocks)
                {
                    if (block == null) throw new ArgumentException("A block definition is missing.");
                    try
                    {
                        if (block.Color == BlockColor.None) throw new ArgumentException("An explicit gameplay color is required.");
                        board.AddBlock(block.Id, block.Anchor, block.Cells, block.Color);
                    }
                    catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                    { throw new ArgumentException("Block '" + block.Id + "': " + exception.Message, exception); }
                }
                var gates = Gates.Select(gate =>
                {
                    if (gate == null) throw new ArgumentException("A gate definition is missing.");
                    try { return gate.CreateGate(); }
                    catch (ArgumentException exception) { throw new ArgumentException("Gate '" + gate.Id + "': " + exception.Message, exception); }
                }).ToArray();
                try { board.ConfigureGates(gates); }
                catch (ArgumentException exception)
                { throw new ArgumentException("Gates [" + string.Join(", ", gates.Select(gate => gate.Id)) + "]: " + exception.Message, exception); }
                foreach (var block in board.Blocks)
                    if (!gates.Any(gate => gate.ColorId == block.ColorId && block.Cells.Max(cell =>
                        ExitGate.IsHorizontalEdge(gate.Edge) ? cell.x : cell.y) < gate.Span))
                        throw new ArgumentException("Block '" + block.Id + "' has no matching gate wide enough for its full footprint.");
                return new PuzzleSession(board, gates, Seconds);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { throw new ArgumentException(label + ": " + exception.Message, exception); }
        }
    }

    [CreateAssetMenu(menuName = "ColorSlide/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private LevelData data;
        public LevelData Data => data;
        public PuzzleSession CreateSession()
        {
            if (data == null) throw new InvalidOperationException("Level asset '" + name + "' has no data.");
            return data.CreateSession();
        }
        // Used only while creating a new asset. Existing assets are never reset by campaign setup.
        public void Configure(LevelData value) { value.CreateSession(); data = value; }
    }
}
