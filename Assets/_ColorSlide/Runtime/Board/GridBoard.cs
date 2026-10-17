using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ColorSlide.Board
{
    public enum GridAxis { X, Y }

    /// <summary>Anchor is the footprint's lower-left occupied cell; offsets are nonnegative.</summary>
    public sealed class GridBlock
    {
        public string Id { get; }
        public BlockColor ColorId { get; }
        public bool IsRemoved { get; internal set; }
        public Vector2Int Anchor { get; internal set; }
        public ReadOnlyCollection<Vector2Int> Cells { get; }
        internal Vector2Int Extent { get; }

        internal GridBlock(string id, Vector2Int anchor, IEnumerable<Vector2Int> footprint, BlockColor colorId)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A block needs a stable nonempty identity.", nameof(id));
            if (footprint == null)
                throw new ArgumentNullException(nameof(footprint));
            if (!Enum.IsDefined(typeof(BlockColor), colorId))
                throw new ArgumentException("Unknown block color identifier.", nameof(colorId));

            var cells = new List<Vector2Int>();
            var unique = new HashSet<Vector2Int>();
            var extent = Vector2Int.zero;
            foreach (var cell in footprint)
            {
                if (cell.x < 0 || cell.y < 0 || !unique.Add(cell))
                    throw new ArgumentException("Footprint offsets must be nonnegative and unique.", nameof(footprint));
                cells.Add(cell);
                extent = new Vector2Int(Math.Max(extent.x, cell.x), Math.Max(extent.y, cell.y));
            }
            if (cells.Count == 0 || !unique.Contains(Vector2Int.zero))
                throw new ArgumentException("A footprint must contain its anchor offset (0,0).", nameof(footprint));

            Id = id;
            ColorId = colorId;
            Anchor = anchor;
            Cells = cells.AsReadOnly();
            Extent = extent;
        }
    }

    /// <summary>Scene-independent occupancy and cardinal movement. Grid Y maps to world Z.</summary>
    public sealed partial class GridBoard
    {
        private readonly Dictionary<string, GridBlock> blocks = new Dictionary<string, GridBlock>(StringComparer.Ordinal);
        private readonly Dictionary<Vector2Int, GridBlock> occupancy = new Dictionary<Vector2Int, GridBlock>();
        private readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

        public int Width { get; }
        public int Height { get; }
        public IEnumerable<GridBlock> Blocks => blocks.Values;

        public GridBoard(int width, int height, IEnumerable<Vector2Int> blockedCells = null)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be positive.");
            Width = width;
            Height = height;
            if (blockedCells == null)
                return;
            foreach (var cell in blockedCells)
                if (!IsInside(cell) || !blocked.Add(cell))
                    throw new ArgumentException("Blocked cells must be unique and inside the board.", nameof(blockedCells));
        }

        public bool IsInside(Vector2Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        public bool IsBlocked(Vector2Int cell) => blocked.Contains(cell);
        public GridBlock OccupantAt(Vector2Int cell) => occupancy.TryGetValue(cell, out var block) ? block : null;
        public GridBlock GetBlock(string id) => blocks[id];

        public GridBlock AddBlock(string id, Vector2Int anchor, IEnumerable<Vector2Int> footprint,
            BlockColor colorId = BlockColor.None)
        {
            var block = new GridBlock(id, anchor, footprint, colorId);
            if (blocks.ContainsKey(id))
                throw new ArgumentException("Duplicate block identity: " + id, nameof(id));
            if (!CanPlace(block, anchor))
                throw new InvalidOperationException("Invalid initial placement for block '" + id + "' at " + anchor + ".");
            blocks.Add(id, block);
            foreach (var offset in block.Cells)
                occupancy.Add(anchor + offset, block);
            return block;
        }

        private bool CanPlace(GridBlock block, Vector2Int anchor)
        {
            foreach (var offset in block.Cells)
            {
                // Widen before adding: malformed extreme coordinates must never wrap into the board.
                var x = (long)anchor.x + offset.x;
                var y = (long)anchor.y + offset.y;
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    return false;
                var cell = new Vector2Int((int)x, (int)y);
                if (blocked.Contains(cell) || (occupancy.TryGetValue(cell, out var other) && other != block))
                    return false;
            }
            return true;
        }

        public bool TryMoveStep(string id, Vector2Int direction)
        {
            if (Math.Abs((long)direction.x) + Math.Abs((long)direction.y) != 1)
                throw new ArgumentException("Movement steps must be one cardinal cell.", nameof(direction));
            var block = blocks[id];
            // A valid anchor plus a cardinal step cannot overflow into a valid opposite edge.
            var nextX = (long)block.Anchor.x + direction.x;
            var nextY = (long)block.Anchor.y + direction.y;
            if (nextX < 0 || nextX >= Width || nextY < 0 || nextY >= Height)
                return false;
            var target = new Vector2Int((int)nextX, (int)nextY);
            if (!CanPlace(block, target))
                return false;

            foreach (var offset in block.Cells)
                occupancy.Remove(block.Anchor + offset);
            block.Anchor = target;
            foreach (var offset in block.Cells)
                occupancy.Add(target + offset, block);
            return true;
        }

        /// <summary>
        /// Walk only the explicitly requested axis, cell by cell. Stop on the first blocked
        /// step. Never try the other axis or find a detour. Returns accepted step count.
        /// </summary>
        public int MoveTowards(string id, Vector2Int pointerTarget, GridAxis axis)
        {
            if (axis != GridAxis.X && axis != GridAxis.Y)
                throw new ArgumentOutOfRangeException(nameof(axis));
            var block = blocks[id];
            var current = axis == GridAxis.X ? block.Anchor.x : block.Anchor.y;
            var maximum = axis == GridAxis.X ? Width - 1 - block.Extent.x : Height - 1 - block.Extent.y;
            var requested = axis == GridAxis.X ? pointerTarget.x : pointerTarget.y;
            var destination = Math.Max(0, Math.Min(maximum, requested));
            var sign = Math.Sign(destination - current);
            var direction = axis == GridAxis.X ? new Vector2Int(sign, 0) : new Vector2Int(0, sign);
            var accepted = 0;
            for (var remaining = Math.Abs(destination - current); remaining > 0; remaining--)
            {
                if (!TryMoveStep(id, direction))
                    break;
                accepted++;
            }
            return accepted;
        }
    }
}
