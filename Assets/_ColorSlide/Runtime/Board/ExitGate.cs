using System;
using UnityEngine;

namespace ColorSlide.Board
{
    // None preserves M1's uncolored logical data. M2 sessions require an explicit color.
    public enum BlockColor { None, Blue, Amber, Purple, Green }
    public enum BoardEdge { North, South, East, West }
    public enum MovementStatus
    {
        Unchanged, Moved, BlockedByBlock, BlockedByObstacle, BlockedByBoundary,
        InvalidGate, Exited, InactiveBlock, SessionInactive
    }

    /// <summary>Start/Span use columns on N/S and rows on E/W; the interval is [Start, Start+Span).</summary>
    public sealed class ExitGate
    {
        public string Id { get; }
        public BlockColor ColorId { get; }
        public BoardEdge Edge { get; }
        public int Start { get; }
        public int Span { get; }

        public ExitGate(string id, BlockColor colorId, BoardEdge edge, int start, int span)
        {
            if (string.IsNullOrWhiteSpace(id) || colorId == BlockColor.None ||
                !Enum.IsDefined(typeof(BlockColor), colorId) || !Enum.IsDefined(typeof(BoardEdge), edge) ||
                start < 0 || span <= 0)
                throw new ArgumentException("Gate identity, color, edge and positive aperture must be valid.");
            Id = id;
            ColorId = colorId;
            Edge = edge;
            Start = start;
            Span = span;
        }

        public bool Contains(long perpendicularCell) => perpendicularCell >= Start && perpendicularCell < (long)Start + Span;
        public static bool IsHorizontalEdge(BoardEdge edge) => edge == BoardEdge.North || edge == BoardEdge.South;
        public static Vector2Int Outward(BoardEdge edge)
        {
            switch (edge)
            {
                case BoardEdge.North: return Vector2Int.up;
                case BoardEdge.South: return Vector2Int.down;
                case BoardEdge.East: return Vector2Int.right;
                case BoardEdge.West: return Vector2Int.left;
                default: throw new ArgumentOutOfRangeException(nameof(edge));
            }
        }
    }

    /// <summary>Already validated and committed logical removal; presentation may only follow this straight path.</summary>
    public sealed class ExitPlan
    {
        public GridBlock Block { get; }
        public ExitGate Gate { get; }
        public Vector2Int StartAnchor { get; }
        public Vector2Int EndAnchor { get; }
        internal ExitPlan(GridBlock block, ExitGate gate, Vector2Int end)
        {
            Block = block;
            Gate = gate;
            StartAnchor = block.Anchor;
            EndAnchor = end;
        }
    }

    public readonly struct MovementResult
    {
        public MovementStatus Status { get; }
        public int AcceptedSteps { get; }
        public ExitPlan Exit { get; }
        public string Detail { get; }
        public MovementResult(MovementStatus status, int steps = 0, ExitPlan exit = null, string detail = null)
        {
            Status = status;
            AcceptedSteps = steps;
            Exit = exit;
            Detail = detail;
        }
    }
}
