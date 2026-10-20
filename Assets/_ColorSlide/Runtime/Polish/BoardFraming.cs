using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Polish
{
    /// <summary>Conservative continuous bounds and orthographic fit; no native objects or camera mutations.</summary>
    public static class BoardFraming
    {
        public const float DefaultLargeBoardZoom = 1.25f;
        public const float MinimumZoom = 1f, MaximumZoom = 1.25f;
        public const float TargetTrayWidth = 0.90f, MaximumTrayWidth = 0.905f;

        public static float ClampZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom)) throw new ArgumentOutOfRangeException(nameof(zoom));
            return Math.Max(MinimumZoom, Math.Min(MaximumZoom, zoom));
        }

        public readonly struct Frame
        {
            public readonly Vector3 CameraPosition;
            public readonly float Size;
            public Frame(Vector3 position, float size) { CameraPosition = position; Size = size; }
        }

        // Matches the accepted CanvasScaler (1080x1920, match .5), top panel 280 + 12 offset,
        // plus 24 reference pixels of separation. Keep 72 below and 32 at either safe-area edge.
        public static Rect GameplayRegion(Vector2 screen, Rect safeArea)
        {
            if (screen.x <= 0f || screen.y <= 0f) throw new ArgumentOutOfRangeException(nameof(screen));
            var safe = SafeAreaPanel.Normalize(safeArea, screen);
            var scale = (float)Math.Sqrt(screen.x / 1080f * (screen.y / 1920f));
            var region = Rect.MinMaxRect(safe.xMin + 32f * scale / screen.x, safe.yMin + 72f * scale / screen.y,
                safe.xMax - 32f * scale / screen.x, safe.yMax - 316f * scale / screen.y);
            if (region.width <= 0f || region.height <= 0f) throw new ArgumentException("Safe area leaves no gameplay region.");
            return region;
        }

        public static Bounds Envelope(LevelData level)
        {
            var size = level.CellSize;
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-0.65f, -0.05f, -0.65f) * size,
                new Vector3(level.Width - 0.35f, 0.85f, level.Height - 0.35f) * size);
            foreach (var block in level.Blocks)
            foreach (var gate in level.Gates.Where(candidate => candidate.Color == block.Color))
            {
                var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                var horizontal = ExitGate.IsHorizontalEdge(gate.Edge); var extent = horizontal ? x : y;
                if (extent >= gate.Span) continue;
                foreach (var along in new[] { gate.Start, gate.Start + gate.Span - 1 - extent })
                {
                    var anchor = gate.Edge == BoardEdge.North ? new Vector2Int(along, level.Height) :
                        gate.Edge == BoardEdge.South ? new Vector2Int(along, -y - 1) :
                        gate.Edge == BoardEdge.East ? new Vector2Int(level.Width, along) : new Vector2Int(-x - 1, along);
                    var end = new Vector3(anchor.x, 0f, anchor.y);
                    // The whole outward-moving block, including its collider-independent selection lift.
                    bounds.Encapsulate((end + new Vector3(-0.55f, -0.05f, -0.55f)) * size);
                    bounds.Encapsulate((end + new Vector3(x + 0.55f, 0.55f, y + 0.55f)) * size);
                    foreach (var piece in SoftBurstMotion.Create(block.Cells, gate.Edge))
                    {
                        var radius = Vector3.one * (piece.Size * 0.866026f); // Rotation-independent cube radius.
                        var from = end + piece.Origin; var to = from + piece.Travel;
                        // Ease is in [0,1]; sin(pi*u) is in [0,1]. This contains every instant, not just samples.
                        bounds.Encapsulate((Vector3.Min(from, to) - radius) * size);
                        bounds.Encapsulate((Vector3.Max(from, to) + Vector3.up * (piece.Arc + 0.04f) + radius) * size);
                    }
                }
            }
            return bounds;
        }

        // The large-board profile lets departures use the side/bottom gutters, but keeps the
        // same HUD exclusion as GameplayRegion. Values follow the accepted CanvasScaler.
        public static Rect PresentationRegion(Vector2 screen, Rect safeArea)
        {
            var region = GameplayRegion(screen, safeArea);
            var scale = (float)Math.Sqrt(screen.x / 1080f * (screen.y / 1920f));
            return Rect.MinMaxRect(region.xMin - 24f * scale / screen.x, region.yMin - 48f * scale / screen.y,
                region.xMax + 24f * scale / screen.x, region.yMax);
        }

        /// <summary>Local continuous geometry, kept separate to avoid projecting fictitious AABB corners.</summary>
        public static Bounds[] PresentationEnvelope(LevelData level)
        {
            var result = new List<Bounds>(); var size = level.CellSize;
            void Add(Vector3 min, Vector3 max)
            {
                var bounds = new Bounds(); bounds.SetMinMax(min * size, max * size); result.Add(bounds);
            }
            // Floor, walls, gate floors/jambs (including the 18% vertical glow), obstacles,
            // selected blocks and contact shadows at every legal in-board position.
            result.Add(PlayableEnvelope(level));
            foreach (var block in level.Blocks)
            foreach (var gate in level.Gates.Where(candidate => candidate.Color == block.Color))
            {
                var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                var extent = ExitGate.IsHorizontalEdge(gate.Edge) ? x : y;
                if (extent >= gate.Span) continue;
                // Extreme legal alignments suffice: all other alignments are translations between them.
                foreach (var along in new[] { gate.Start, gate.Start + gate.Span - 1 - extent }.Distinct())
                {
                    var anchor = gate.Edge == BoardEdge.North ? new Vector2Int(along, level.Height) :
                        gate.Edge == BoardEdge.South ? new Vector2Int(along, -y - 1) :
                        gate.Edge == BoardEdge.East ? new Vector2Int(level.Width, along) : new Vector2Int(-x - 1, along);
                    var end = new Vector3(anchor.x, 0f, anchor.y);
                    foreach (var cell in block.Cells)
                    {
                        var center = end + new Vector3(cell.x, 0f, cell.y);
                        // Full-size whole block until scatter; includes its .5-cell contact shadow.
                        // The convex projected union of the in-board and end poses contains the sweep.
                        Add(center + new Vector3(-0.5f, -0.01f, -0.5f), center + new Vector3(0.5f, 0.48f, 0.5f));
                    }
                    result.AddRange(BurstEnvelope(SoftBurstMotion.Create(block.Cells, gate.Edge), size, end));
                }
            }
            return result.ToArray();
        }

        public static Bounds PlayableEnvelope(LevelData level)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-0.675f, -0.04f, -0.675f) * level.CellSize,
                new Vector3(level.Width - 0.325f, 0.763f, level.Height - 0.325f) * level.CellSize);
            return bounds;
        }

        public static IEnumerable<Bounds> BurstEnvelope(SoftBurstPiece[] pieces, float size, Vector3 end)
        {
            foreach (var piece in pieces)
            for (var step = 0; step < 32; step++)
            {
                var from = step / 32f; var to = (step + 1) / 32f;
                var a = piece.Origin + piece.Travel * SoftBurstMotion.Ease(from);
                var b = piece.Origin + piece.Travel * SoftBurstMotion.Ease(to);
                var sinA = (float)Math.Sin(Math.PI * from); var sinB = (float)Math.Sin(Math.PI * to);
                var lowArc = Math.Min(sinA, sinB); var highArc = from <= 0.5f && to >= 0.5f ? 1f : Math.Max(sinA, sinB);
                // Continuous interval enclosure: monotonic travel, sine extrema and largest shrinking radius.
                var radius = Vector3.one * (piece.Scale(from) * 0.866026f);
                var bounds = new Bounds();
                bounds.SetMinMax((end + Vector3.Min(a, b) + Vector3.up * (lowArc * piece.Arc + 0.04f * from) - radius) * size,
                    (end + Vector3.Max(a, b) + Vector3.up * (highArc * piece.Arc + 0.04f * to) + radius) * size);
                yield return bounds;
            }
        }

        /// <summary>Zoom about the playable center; clamp only the tray/board/gates to the HUD-safe region.</summary>
        public static Frame Zoom(Frame baseline, Rect playable, float zoom, Vector3 right, Vector3 up,
            float aspect, Vector2 screen, Rect safeArea)
        {
            zoom = ClampZoom(zoom);
            if (zoom == MinimumZoom) return baseline; // Exact historical full-exit frame.
            return AtSize(baseline, playable, baseline.Size / zoom, right, up, aspect, screen, safeArea);
        }

        /// <summary>Shared close framing for every board. The control selects progress from the historical
        /// safe frame to an actual tray-width fit; it is not the same physical multiplier for every level.</summary>
        public static Frame AdaptiveZoom(Frame baseline, Rect playable, Rect tray, float control,
            Vector3 right, Vector3 up, float aspect, Vector2 screen, Rect safeArea)
        {
            control = ClampZoom(control);
            var safeWidth = SafeAreaPanel.Normalize(safeArea, screen).width;
            if (aspect <= 0f || tray.width <= 0f || safeWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(aspect));
            var reference = Zoom(baseline, playable, MaximumZoom, right, up, aspect, screen, safeArea);
            var utilization = tray.width / (2f * reference.Size * aspect * safeWidth);
            // Preserve a composition already at the target, including the approved 8x11 frames.
            // This tolerance is geometric, not a level ID/dimension exception.
            var compact = utilization >= TargetTrayWidth - 0.005f && utilization <= MaximumTrayWidth ? reference :
                CenteredAtSize(baseline, playable, tray.width / (2f * aspect * safeWidth * TargetTrayWidth), right, up, aspect, screen, safeArea);
            // A legacy fixed camera can be closer than the target on a notched/short viewport.
            // Normalize that endpoint too, so increasing the control never zooms outward.
            var start = baseline.Size <= compact.Size ? compact : baseline;
            if (control == MinimumZoom) return start;
            if (control == MaximumZoom) return compact;
            var progress = (control - MinimumZoom) / (MaximumZoom - MinimumZoom);
            var physicalZoom = 1f + progress * (start.Size / compact.Size - 1f);
            var size = start.Size / physicalZoom;
            var blend = start.Size > compact.Size ? (start.Size - size) / (start.Size - compact.Size) : 1f;
            var position = Vector3.Lerp(start.CameraPosition, compact.CameraPosition, blend);
            return AtSize(new Frame(position, size), playable, size, right, up, aspect, screen, safeArea);
        }

        private static Frame CenteredAtSize(Frame baseline, Rect playable, float size, Vector3 right, Vector3 up,
            float aspect, Vector2 screen, Rect safeArea)
        {
            var fit = AtSize(baseline, playable, size, right, up, aspect, screen, safeArea);
            var region = GameplayRegion(screen, safeArea);
            var desired = Vector2.Scale(region.center - Vector2.one * 0.5f, new Vector2(fit.Size * aspect, fit.Size)) * 2f;
            var offset = playable.center - desired - new Vector2(Vector3.Dot(fit.CameraPosition, right), Vector3.Dot(fit.CameraPosition, up));
            return new Frame(fit.CameraPosition + right * offset.x + up * offset.y, fit.Size);
        }

        private static Frame AtSize(Frame baseline, Rect playable, float requestedSize, Vector3 right, Vector3 up,
            float aspect, Vector2 screen, Rect safeArea)
        {
            var region = GameplayRegion(screen, safeArea);
            var minimum = Math.Max(playable.width / (2f * aspect * region.width), playable.height / (2f * region.height)) * 1.002f;
            var size = Math.Max(requestedSize, minimum);
            var camera = new Vector2(Vector3.Dot(baseline.CameraPosition, right), Vector3.Dot(baseline.CameraPosition, up));
            var desired = playable.center + (camera - playable.center) * (size / baseline.Size);
            var half = new Vector2(size * aspect, size);
            var min = playable.max - Vector2.Scale(region.max - Vector2.one * 0.5f, half) * 2f;
            var max = playable.min - Vector2.Scale(region.min - Vector2.one * 0.5f, half) * 2f;
            var x = Math.Max(min.x, Math.Min(max.x, desired.x)); var y = Math.Max(min.y, Math.Min(max.y, desired.y));
            return new Frame(baseline.CameraPosition + right * (x - camera.x) + up * (y - camera.y), size);
        }

        // Move only a cosmetic burst root, in the board's XZ plane. Relative fragment trajectories,
        // rotations, sizes and times are unchanged; the authoritative block still reaches its real end.
        public static Vector3 VisibleBurstOrigin(Rect localProjection, Vector3 origin, Frame frame,
            Vector3 right, Vector3 up, float aspect, Rect region)
        {
            var half = new Vector2(frame.Size * aspect, frame.Size);
            var min = Vector2.Scale(region.min - Vector2.one * 0.5f, half) * 2f - localProjection.min;
            var max = Vector2.Scale(region.max - Vector2.one * 0.5f, half) * 2f - localProjection.max;
            if (min.x > max.x || min.y > max.y) throw new ArgumentException("Burst does not fit the visible presentation region.");
            var point = new Vector2(Vector3.Dot(origin - frame.CameraPosition, right), Vector3.Dot(origin - frame.CameraPosition, up));
            var shift = new Vector2(Math.Max(min.x, Math.Min(max.x, point.x)), Math.Max(min.y, Math.Min(max.y, point.y))) - point;
            var determinant = right.x * up.z - right.z * up.x;
            if (Math.Abs(determinant) < 0.0001f) throw new ArgumentException("Camera must see the XZ board plane.");
            return origin + new Vector3((shift.x * up.z - shift.y * right.z) / determinant, 0f,
                (shift.y * right.x - shift.x * up.x) / determinant);
        }

        public static Rect Project(IEnumerable<Bounds> worldGeometry, Vector3 right, Vector3 up)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var bounds in worldGeometry)
            {
                // Analytic support of an AABB: same extrema as eight corners, without per-box arrays.
                var center = new Vector2(Vector3.Dot(bounds.center, right), Vector3.Dot(bounds.center, up));
                var extent = bounds.extents;
                var radius = new Vector2(Math.Abs(right.x) * extent.x + Math.Abs(right.y) * extent.y + Math.Abs(right.z) * extent.z,
                    Math.Abs(up.x) * extent.x + Math.Abs(up.y) * extent.y + Math.Abs(up.z) * extent.z);
                min = Vector2.Min(min, center - radius); max = Vector2.Max(max, center + radius);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Frame FitPresentation(Rect projection, Vector3 cameraPosition, Vector3 right, Vector3 up,
            float aspect, Vector2 screen, Rect safeArea)
        {
            if (aspect <= 0f || projection.width <= 0f || projection.height <= 0f) throw new ArgumentOutOfRangeException(nameof(aspect));
            var region = PresentationRegion(screen, safeArea);
            var size = Math.Max(projection.width / (2f * aspect * region.width), projection.height / (2f * region.height)) * 1.002f;
            var desired = new Vector2((region.center.x - 0.5f) * 2f * size * aspect, (region.center.y - 0.5f) * 2f * size);
            var offset = projection.center - new Vector2(Vector3.Dot(cameraPosition, right), Vector3.Dot(cameraPosition, up)) - desired;
            return new Frame(cameraPosition + right * offset.x + up * offset.y, size);
        }

        public static Frame Fit(Bounds worldBounds, Vector3 cameraPosition, Vector3 right, Vector3 up,
            float aspect, Rect region, float minimumSize)
        {
            if (aspect <= 0f || region.width <= 0f || region.height <= 0f || minimumSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(aspect));
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in Corners(worldBounds))
            {
                var relative = corner - cameraPosition;
                var point = new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, up));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var size = Math.Max(minimumSize, Math.Max((max.x - min.x) / (2f * aspect * region.width),
                (max.y - min.y) / (2f * region.height)));
            size *= 1.002f; // Small numerical margin outside the conservative envelope.
            var desired = new Vector2((region.center.x - 0.5f) * 2f * size * aspect, (region.center.y - 0.5f) * 2f * size);
            var offset = (min + max) * 0.5f - desired;
            return new Frame(cameraPosition + right * offset.x + up * offset.y, size);
        }

        public static Vector3[] Corners(Bounds bounds)
        {
            var corners = new Vector3[8]; var index = 0;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                corners[index++] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
            return corners;
        }
    }
}
