using System;
using System.Collections.Generic;
using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Polish
{
    public readonly struct SoftBurstPiece
    {
        public readonly Vector3 Origin, Travel, Rotation;
        public readonly float Size, Arc;
        public SoftBurstPiece(Vector3 origin, Vector3 travel, Vector3 rotation, float size, float arc)
        { Origin = origin; Travel = travel; Rotation = rotation; Size = size; Arc = arc; }
        public Vector3 Position(float progress) => Origin + Travel * SoftBurstMotion.Ease(progress) +
            Vector3.up * ((float)Math.Sin(Math.PI * SoftBurstMotion.Clamp(progress)) * Arc + 0.04f * SoftBurstMotion.Clamp(progress));
        public float Scale(float progress) { var u = SoftBurstMotion.Clamp(progress); return Size * (1f - u * u); }
    }

    /// <summary>Presentation coordinates in cell units. No grid mutations, global Random state or native objects.</summary>
    public static class SoftBurstMotion
    {
        public const int FragmentCount = 10;
        public const float OutwardSeconds = 0.18f, ScatterSeconds = 0.30f;
        public const float Duration = OutwardSeconds + ScatterSeconds;
        public static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
        public static float Ease(float value) { var u = 1f - Clamp(value); return 1f - u * u * u; }
        public static float OutwardProgress(float age) { var u = Clamp(age / OutwardSeconds); return u * u * (3f - 2f * u); }
        public static float ScatterProgress(float age) => Clamp((age - OutwardSeconds) / ScatterSeconds);
        public static bool IsComplete(float age) => age >= Duration;
        public static Vector3 Direction(BoardEdge edge)
        { var cell = ExitGate.Outward(edge); return new Vector3(cell.x, 0f, cell.y); }
        public static SoftBurstPiece[] Create(IReadOnlyList<Vector2Int> footprint, BoardEdge edge)
        {
            if (footprint == null || footprint.Count == 0) throw new ArgumentException("A burst needs the real occupied footprint.");
            var forward = Direction(edge); var side = new Vector3(forward.z, 0f, -forward.x);
            var result = new SoftBurstPiece[FragmentCount];
            for (var i = 0; i < result.Length; i++)
            {
                var cell = footprint[i * footprint.Count / result.Length];
                var origin = new Vector3(cell.x + Signed(i, 1) * 0.16f, 0.23f + Signed(i, 2) * 0.025f, cell.y + Signed(i, 3) * 0.16f);
                var travel = forward * (0.12f + Unit(i, 4) * 0.12f) + side * (Signed(i, 5) * 0.20f);
                var rotation = new Vector3(Signed(i, 6), Signed(i, 7), Signed(i, 8)) * 35f;
                result[i] = new SoftBurstPiece(origin, travel, rotation, 0.23f + Unit(i, 9) * 0.06f, 0.16f + Unit(i, 10) * 0.06f);
            }
            return result;
        }
        private static float Signed(int index, uint salt) => Unit(index, salt) * 2f - 1f;
        private static float Unit(int index, uint salt)
        {
            var value = unchecked((uint)(index + 1) * 747796405u + salt * 2891336453u);
            value = unchecked((value ^ (value >> 16)) * 2246822519u);
            return (value & 0xffffu) / 65535f;
        }
    }
}
