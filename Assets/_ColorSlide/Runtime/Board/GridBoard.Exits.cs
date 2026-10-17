using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace ColorSlide.Board
{
    public sealed partial class GridBoard
    {
        private ReadOnlyCollection<ExitGate> gates = Array.AsReadOnly(Array.Empty<ExitGate>());
        private bool gatesConfigured;
        public ReadOnlyCollection<ExitGate> Gates => gates;
        public int ActiveBlockCount => blocks.Count;
        public bool IsActive(string id) => id != null && blocks.ContainsKey(id);
        public IReadOnlyCollection<Vector2Int> BlockedCells => Array.AsReadOnly(blocked.ToArray());

        public void ConfigureGates(IEnumerable<ExitGate> definitions)
        {
            if (gatesConfigured)
                throw new InvalidOperationException("Gates are fixed for one board instance. Restart with a fresh board.");
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            var result = new List<ExitGate>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gate in definitions)
            {
                if (gate == null || !ids.Add(gate.Id))
                    throw new ArgumentException("Gate references and identities must be unique.", nameof(definitions));
                var length = ExitGate.IsHorizontalEdge(gate.Edge) ? Width : Height;
                if ((long)gate.Start + gate.Span > length)
                    throw new ArgumentException("Gate '" + gate.Id + "' extends beyond the board edge.");
                if (result.Any(other => other.Edge == gate.Edge && gate.Start < (long)other.Start + other.Span &&
                    other.Start < (long)gate.Start + gate.Span))
                    throw new ArgumentException("Overlapping gate apertures are ambiguous, even if their colors match.");
                result.Add(gate);
            }
            gates = result.AsReadOnly();
            gatesConfigured = true;
        }

        /// <summary>M1 movement remains unchanged; M2 explicitly opts into outward requests here.</summary>
        public MovementResult MoveWithExits(string id, Vector2Int pointerTarget, GridAxis axis)
        {
            if (axis != GridAxis.X && axis != GridAxis.Y)
                throw new ArgumentOutOfRangeException(nameof(axis));
            if (!IsActive(id))
                return new MovementResult(MovementStatus.InactiveBlock);
            var block = blocks[id];
            var current = axis == GridAxis.X ? block.Anchor.x : block.Anchor.y;
            var requested = axis == GridAxis.X ? pointerTarget.x : pointerTarget.y;
            var sign = Math.Sign((long)requested - current);
            var direction = axis == GridAxis.X ? new Vector2Int(sign, 0) : new Vector2Int(0, sign);
            var accepted = 0;
            for (var remaining = Math.Abs((long)requested - current); remaining > 0; remaining--)
            {
                if (TryMoveStep(id, direction))
                {
                    accepted++;
                    continue;
                }
                var outside = false;
                foreach (var offset in block.Cells)
                {
                    var x = (long)block.Anchor.x + direction.x + offset.x;
                    var y = (long)block.Anchor.y + direction.y + offset.y;
                    if (x < 0 || x >= Width || y < 0 || y >= Height)
                    {
                        outside = true;
                        continue;
                    }
                    var obstruction = CellObstruction(block, new Vector2Int((int)x, (int)y));
                    if (obstruction != MovementStatus.Unchanged)
                        return new MovementResult(obstruction, accepted);
                }
                if (!outside)
                    throw new InvalidOperationException("A rejected step had no boundary or cell obstruction.");
                var edge = axis == GridAxis.X ? (sign > 0 ? BoardEdge.East : BoardEdge.West) :
                    (sign > 0 ? BoardEdge.North : BoardEdge.South);
                return TryCommitExit(block, edge, accepted);
            }
            return new MovementResult(accepted > 0 ? MovementStatus.Moved : MovementStatus.Unchanged, accepted);
        }

        private MovementStatus CellObstruction(GridBlock block, Vector2Int cell)
        {
            if (blocked.Contains(cell))
                return MovementStatus.BlockedByObstacle;
            if (occupancy.TryGetValue(cell, out var other) && other != block)
                return MovementStatus.BlockedByBlock;
            return MovementStatus.Unchanged;
        }

        private MovementResult TryCommitExit(GridBlock block, BoardEdge edge, int accepted)
        {
            var horizontal = ExitGate.IsHorizontalEdge(edge);
            var edgeGates = gates.Where(gate => gate.Edge == edge).ToArray();
            var gate = edgeGates.FirstOrDefault(candidate => block.Cells.All(offset =>
                candidate.Contains(horizontal ? (long)block.Anchor.x + offset.x : (long)block.Anchor.y + offset.y)));
            if (gate == null)
            {
                var touchesOpening = edgeGates.Any(candidate => block.Cells.Any(offset =>
                    candidate.Contains(horizontal ? (long)block.Anchor.x + offset.x : (long)block.Anchor.y + offset.y)));
                return new MovementResult(touchesOpening ? MovementStatus.InvalidGate : MovementStatus.BlockedByBoundary,
                    accepted, detail: touchesOpening ? "The full perpendicular footprint does not fit one aperture." : "Solid boundary.");
            }
            if (gate.ColorId != block.ColorId)
                return new MovementResult(MovementStatus.InvalidGate, accepted, detail: "Gate and block color identifiers differ.");

            var end = block.Anchor;
            switch (edge)
            {
                case BoardEdge.North: end.y = Height; break;
                case BoardEdge.South: end.y = -block.Extent.y - 1; break;
                case BoardEdge.East: end.x = Width; break;
                case BoardEdge.West: end.x = -block.Extent.x - 1; break;
            }
            var direction = ExitGate.Outward(edge);
            var distance = horizontal ? Math.Abs((long)end.y - block.Anchor.y) : Math.Abs((long)end.x - block.Anchor.x);
            // No mutation until EVERY cell in EVERY intermediate footprint is cleared.
            for (long step = 1; step <= distance; step++)
            foreach (var offset in block.Cells)
            {
                var x = block.Anchor.x + step * direction.x + offset.x;
                var y = block.Anchor.y + step * direction.y + offset.y;
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                {
                    var obstruction = CellObstruction(block, new Vector2Int((int)x, (int)y));
                    if (obstruction != MovementStatus.Unchanged)
                        return new MovementResult(obstruction, accepted, detail: "The departing shape's swept path is obstructed.");
                }
                else
                {
                    var correctSide = edge == BoardEdge.North ? y >= Height : edge == BoardEdge.South ? y < 0 :
                        edge == BoardEdge.East ? x >= Width : x < 0;
                    if (!correctSide || !gate.Contains(horizontal ? x : y))
                        return new MovementResult(MovementStatus.InvalidGate, accepted, detail: "A footprint cell clips a solid boundary segment.");
                }
            }

            var plan = new ExitPlan(block, gate, end);
            foreach (var offset in block.Cells)
                occupancy.Remove(block.Anchor + offset);
            blocks.Remove(block.Id);
            block.IsRemoved = true;
            return new MovementResult(MovementStatus.Exited, accepted, plan);
        }
    }
}
