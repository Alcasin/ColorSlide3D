using System;
using UnityEngine;

namespace ColorSlide.Board
{
    /// <summary>Origin is cell (0,0)'s center on the horizontal surface, never a corner.</summary>
    public readonly struct BoardCoordinates
    {
        public Vector3 Origin { get; }
        public float CellSize { get; }

        public BoardCoordinates(Vector3 origin, float cellSize)
        {
            if (!IsFinite(cellSize) || cellSize <= 0f ||
                !IsFinite(origin.x) || !IsFinite(origin.y) || !IsFinite(origin.z))
                throw new ArgumentException("Board origin must be finite and cell size must be finite and positive.");
            Origin = origin;
            CellSize = cellSize;
        }

        public Vector3 CellToWorld(Vector2Int cell) => Origin + new Vector3(cell.x * CellSize, 0f, cell.y * CellSize);

        public Vector2Int WorldToCell(Vector3 world)
        {
            if (!IsFinite(world.x) || !IsFinite(world.z))
                throw new ArgumentException("Pointer coordinates must be finite.", nameof(world));
            // Half-cell thresholds, with exact ties towards the positive axis.
            return new Vector2Int(ToCell(world.x, Origin.x), ToCell(world.z, Origin.z));
        }

        private int ToCell(float world, float origin)
        {
            var rounded = Math.Floor(((double)world - origin) / CellSize + 0.5d);
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, rounded));
        }

        /// <summary>One axis per pointer motion; X wins ties. No blocked-axis fallback.</summary>
        public static GridAxis DominantAxis(Vector3 motion) =>
            Mathf.Abs(motion.x) >= Mathf.Abs(motion.z) ? GridAxis.X : GridAxis.Y;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
