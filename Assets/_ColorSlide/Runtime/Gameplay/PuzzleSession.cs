using System;
using System.Linq;
using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    public enum GameplayState { Ready, Playing, Won, Failed }

    /// <summary>Deterministic single-level state. Call movement before Advance for exit-before-timeout precedence.</summary>
    public sealed class PuzzleSession
    {
        private readonly int width;
        private readonly int height;
        private readonly Vector2Int[] obstacles;
        private readonly GridBlock[] initialBlocks;
        private readonly Vector2Int[] initialAnchors;
        private readonly ExitGate[] initialGates;
        public double InitialSeconds { get; }
        public double RemainingSeconds { get; private set; }
        public GameplayState State { get; private set; } = GameplayState.Ready;
        public GridBoard Board { get; private set; }
        public int RemainingBlocks => Board.ActiveBlockCount;

        public PuzzleSession(GridBoard board, System.Collections.Generic.IEnumerable<ExitGate> gates, double seconds)
        {
            if (board == null || gates == null)
                throw new ArgumentNullException(board == null ? nameof(board) : nameof(gates));
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            width = board.Width;
            height = board.Height;
            obstacles = board.BlockedCells.ToArray();
            initialBlocks = board.Blocks.ToArray();
            initialAnchors = initialBlocks.Select(block => block.Anchor).ToArray();
            initialGates = gates.ToArray();
            if (initialBlocks.Length == 0 || initialBlocks.Any(block => block.ColorId == BlockColor.None ||
                !initialGates.Any(gate => gate != null && gate.ColorId == block.ColorId)))
                throw new ArgumentException("A session needs colored blocks with matching gate colors.");
            // Own a fresh model rather than allowing caller mutations to corrupt the snapshot.
            Board = RestoreBoard();
            InitialSeconds = seconds;
            RemainingSeconds = seconds;
        }

        private GridBoard RestoreBoard()
        {
            var board = new GridBoard(width, height, obstacles);
            for (var i = 0; i < initialBlocks.Length; i++)
            {
                var block = initialBlocks[i];
                board.AddBlock(block.Id, initialAnchors[i], block.Cells, block.ColorId);
            }
            board.ConfigureGates(initialGates);
            return board;
        }

        public bool Start()
        {
            if (State != GameplayState.Ready)
                return false;
            State = GameplayState.Playing;
            return true;
        }

        public MovementResult MoveTowards(string id, Vector2Int target, GridAxis axis)
        {
            if (State != GameplayState.Playing)
                return new MovementResult(MovementStatus.SessionInactive);
            var result = Board.MoveWithExits(id, target, axis);
            if (result.Status == MovementStatus.Exited && RemainingBlocks == 0)
                State = GameplayState.Won;
            return result;
        }

        /// <returns>True only for the update that transitions Playing to Failed.</returns>
        public bool Advance(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (State != GameplayState.Playing)
                return false;
            RemainingSeconds = Math.Max(0d, RemainingSeconds - elapsedSeconds);
            if (RemainingSeconds > 0d)
                return false;
            State = GameplayState.Failed;
            return true;
        }

        public void Restart()
        {
            Board = RestoreBoard();
            RemainingSeconds = InitialSeconds;
            State = GameplayState.Playing;
        }
    }
}
