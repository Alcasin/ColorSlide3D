using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorSlide.Polish
{
    /// <summary>Managed geometry data, also testable without Unity's native Mesh API.</summary>
    public sealed class SoftMeshData
    {
        public readonly Vector3[] Vertices, Normals;
        public readonly Vector2[] UV;
        public readonly Color[] Colors;
        public readonly int[] Triangles;
        public SoftMeshData(Vector3[] vertices, Vector3[] normals, Vector2[] uv, Color[] colors, int[] triangles)
        { Vertices = vertices; Normals = normals; UV = uv; Colors = colors; Triangles = triangles; }
        public void Validate()
        {
            if (Vertices.Length == 0 || Normals.Length != Vertices.Length || UV.Length != Vertices.Length ||
                Colors.Length != Vertices.Length || Triangles.Length % 3 != 0) throw new ArgumentException("Incomplete soft mesh data.");
            for (var i = 0; i < Vertices.Length; i++)
            {
                var p = Vertices[i]; var n = Normals[i];
                if (float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z) ||
                    float.IsNaN(n.x + n.y + n.z) || float.IsInfinity(n.x + n.y + n.z) ||
                    Math.Abs(n.sqrMagnitude - 1f) > 0.001f) throw new ArgumentException("Nonfinite vertex or invalid normal.");
            }
            for (var i = 0; i < Triangles.Length; i += 3)
            {
                var a = Triangles[i]; var b = Triangles[i + 1]; var c = Triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= Vertices.Length || b >= Vertices.Length || c >= Vertices.Length)
                    throw new ArgumentException("Triangle index outside the mesh.");
                var cross = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                if (cross.sqrMagnitude < 0.0000000001f || Vector3.Dot(cross, Normals[a] + Normals[b] + Normals[c]) <= 0f)
                    throw new ArgumentException("Degenerate triangle or winding disagrees with normals.");
            }
        }
    }

    public static class SoftGeometry
    {
        public static SoftMeshData RoundedBox(float radius = 0.12f, int segments = 3)
        {
            if (radius <= 0f || radius >= 0.5f || float.IsNaN(radius) || segments < 1 || segments > 12)
                throw new ArgumentException("Use a finite bevel below half the unit box and 1–12 segments.");
            var count = 2 * segments + 2;
            var coordinates = new float[count];
            for (var i = 0; i <= segments; i++)
            { coordinates[i] = -0.5f + radius * i / segments; coordinates[count - 1 - i] = 0.5f - radius * i / segments; }
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            var directions = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            var horizontal = new[] { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            var vertical = new[] { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            var inner = 0.5f - radius;
            for (var face = 0; face < 6; face++)
            {
                var start = vertices.Count;
                for (var y = 0; y < count; y++)
                for (var x = 0; x < count; x++)
                {
                    var point = directions[face] * 0.5f + horizontal[face] * coordinates[x] + vertical[face] * coordinates[y];
                    var closest = new Vector3(Clamp(point.x, inner), Clamp(point.y, inner), Clamp(point.z, inner));
                    var difference = point - closest;
                    var normal = difference / (float)Math.Sqrt(difference.sqrMagnitude);
                    vertices.Add(closest + normal * radius); normals.Add(normal);
                    uv.Add(new Vector2((float)x / (count - 1), (float)y / (count - 1))); colors.Add(Color.white);
                }
                for (var y = 0; y < count - 1; y++)
                for (var x = 0; x < count - 1; x++)
                {
                    var a = start + y * count + x; var b = a + 1; var c = a + count; var d = c + 1;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            }
            var result = new SoftMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(), colors.ToArray(), triangles.ToArray());
            result.Validate(); return result;
        }

        public static SoftMeshData ContactShadow(int segments = 32)
        {
            if (segments < 8 || segments > 64) throw new ArgumentOutOfRangeException(nameof(segments));
            var vertices = new Vector3[1 + segments * 2]; var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length]; var triangles = new List<int>();
            colors[0] = Color.white; uv[0] = new Vector2(0.5f, 0.5f);
            for (var ring = 0; ring < 2; ring++)
            for (var i = 0; i < segments; i++)
            {
                var angle = -Math.PI * 2d * i / segments; var radius = ring == 0 ? 0.32f : 0.5f;
                var index = 1 + ring * segments + i;
                vertices[index] = new Vector3((float)Math.Cos(angle) * radius, 0f, (float)Math.Sin(angle) * radius);
                uv[index] = new Vector2(vertices[index].x + 0.5f, vertices[index].z + 0.5f);
                colors[index] = new Color(1f, 1f, 1f, ring == 0 ? 0.55f : 0f);
            }
            for (var i = 0; i < vertices.Length; i++) normals[i] = Vector3.up;
            for (var i = 0; i < segments; i++)
            {
                var a = 1 + i; var b = 1 + (i + 1) % segments; var c = a + segments; var d = b + segments;
                triangles.AddRange(new[] { 0, a, b, a, c, b, b, c, d });
            }
            var result = new SoftMeshData(vertices, normals, uv, colors, triangles.ToArray()); result.Validate(); return result;
        }

        // Clockwise perimeter for UGUI's -Z-facing triangle fan. No texture or external sprite is required.
        public static Vector2[] RoundedRect(float width, float height, float radius, int segments = 6)
        {
            if (width <= 0f || height <= 0f) return Array.Empty<Vector2>();
            if (segments < 1 || segments > 12) throw new ArgumentOutOfRangeException(nameof(segments));
            radius = Math.Max(0f, Math.Min(radius, Math.Min(width, height) / 2f));
            var points = new List<Vector2>();
            var centers = new[] { new Vector2(width / 2 - radius, height / 2 - radius), new Vector2(width / 2 - radius, -height / 2 + radius),
                new Vector2(-width / 2 + radius, -height / 2 + radius), new Vector2(-width / 2 + radius, height / 2 - radius) };
            for (var corner = 0; corner < 4; corner++)
            for (var i = 0; i <= segments; i++)
            {
                var angle = (90d - corner * 90d - i * 90d / segments) * Math.PI / 180d;
                var point = centers[corner] + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.000001f) points.Add(point);
            }
            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude < 0.000001f) points.RemoveAt(points.Count - 1);
            return points.ToArray();
        }
        private static float Clamp(float value, float extent) => Math.Max(-extent, Math.Min(extent, value));
    }
}
