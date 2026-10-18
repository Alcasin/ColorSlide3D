using System;
using System.IO;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEngine;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M4PolishTests
    {
        [Test]
        public void RoundedMeshHasValidOutwardFacesSmoothNormalsAndUnitBounds()
        {
            var mesh = SoftGeometry.RoundedBox(); mesh.Validate();
            Assert.That(mesh.Vertices.Length, Is.LessThan(1000), "A shared mobile mesh, not a subdivision-heavy asset.");
            for (var i = 0; i < mesh.Vertices.Length; i++)
            {
                var p = mesh.Vertices[i]; var n = mesh.Normals[i];
                Assert.That(Math.Max(Math.Abs(p.x), Math.Max(Math.Abs(p.y), Math.Abs(p.z))), Is.LessThanOrEqualTo(0.50001f));
                Assert.That(Vector3.Dot(p, n), Is.GreaterThan(0f));
                Assert.That(n.sqrMagnitude, Is.EqualTo(1f).Within(0.0001f));
            }
            Assert.That(mesh.Vertices.Max(point => point.x), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mesh.Vertices.Min(point => point.y), Is.EqualTo(-0.5f).Within(0.0001f));
            var invalid = new SoftMeshData(mesh.Vertices, (Vector3[])mesh.Normals.Clone(), mesh.UV, mesh.Colors, mesh.Triangles);
            invalid.Normals[0] = new Vector3(float.NaN, 0f, 0f);
            Assert.Throws<ArgumentException>(invalid.Validate);
        }

        [Test]
        public void RoundedCellsNeverOccupyTheEmptyCornerOfAnyAuthoredFootprint()
        {
            var mesh = SoftGeometry.RoundedBox();
            foreach (var level in M3AuthoredLevels.Create())
            foreach (var block in level.Blocks)
            foreach (var cell in block.Cells)
            foreach (var vertex in mesh.Vertices)
            {
                var x = cell.x + vertex.x * 0.98f; var y = cell.y + vertex.z * 0.98f;
                var occupied = new Vector2Int((int)Math.Floor(x + 0.5), (int)Math.Floor(y + 0.5));
                Assert.That(block.Cells, Does.Contain(occupied), block.Id + " in " + level.Id);
                Assert.That(vertex.y * 0.46f + 0.23f, Is.InRange(-0.00001f, 0.46001f));
            }
        }

        [Test]
        public void ContactShadowHasValidTopologyAndAFullyTransparentOuterRim()
        {
            var mesh = SoftGeometry.ContactShadow(); mesh.Validate();
            for (var i = 0; i < mesh.Vertices.Length; i++)
            {
                var p = mesh.Vertices[i]; Assert.That(p.y, Is.Zero);
                Assert.That(p.sqrMagnitude, Is.LessThanOrEqualTo(0.25001f));
                if (p.sqrMagnitude > 0.24f) Assert.That(mesh.Colors[i].a, Is.Zero);
            }
            Assert.That(mesh.Colors[0].a, Is.EqualTo(1f));
            Assert.That(mesh.Colors.Any(color => color.a > 0f && color.a < 1f), Is.True);
        }

        [Test]
        public void RoundedUiSafeAreaAndPortraitFramingRespectTheirAvailableBounds()
        {
            foreach (var size in new[] { new Vector2(1080f, 280f), new Vector2(300f, 100f), new Vector2(88f, 88f), new Vector2(0.01f, 0.01f) })
            {
                var points = SoftGeometry.RoundedRect(size.x, size.y, 28f);
                foreach (var point in points)
                { Assert.That(Math.Abs(point.x), Is.LessThanOrEqualTo(size.x / 2f + 0.001f)); Assert.That(Math.Abs(point.y), Is.LessThanOrEqualTo(size.y / 2f + 0.001f)); }
                var signedArea = 0f;
                for (var i = 0; i < points.Length; i++) { var next = points[(i + 1) % points.Length]; signedArea += points[i].x * next.y - points[i].y * next.x; }
                Assert.That(signedArea, Is.LessThan(0f), "UGUI fan faces -Z.");
            }
            var safe = SafeAreaPanel.Normalize(new Rect(0f, 80f, 1080f, 1760f), new Vector2(1080f, 1920f));
            Assert.That(safe.yMin, Is.EqualTo(80f / 1920f).Within(0.0001f)); Assert.That(safe.yMax, Is.EqualTo(1840f / 1920f).Within(0.0001f));
            foreach (var aspect in new[] { 9f / 16f, 9f / 19.5f, 9f / 20f })
                Assert.That(PortraitCameraFit.SizeFor(6.25f, aspect) * aspect, Is.EqualTo(6.25f * 9f / 16f).Within(0.0001f));
        }

        [Test]
        public void CuesDeduplicateBlockedGesturesExitsAndResultsThenRearmOnRetry()
        {
            var cues = new PolishCueState(); Assert.That(cues.BeginGesture(), Is.EqualTo(SoundCue.Select));
            Assert.That(cues.Move("Blue", MovementStatus.BlockedByBlock), Is.EqualTo(SoundCue.Blocked));
            for (var frame = 0; frame < 100; frame++) Assert.That(cues.Move("Blue", MovementStatus.InvalidGate), Is.EqualTo(SoundCue.None));
            Assert.That(cues.Move("Blue", MovementStatus.Moved), Is.EqualTo(SoundCue.None));
            cues.BeginGesture(); Assert.That(cues.Move("Blue", MovementStatus.BlockedByObstacle), Is.EqualTo(SoundCue.Blocked));
            Assert.That(cues.Move("Blue", MovementStatus.Exited), Is.EqualTo(SoundCue.Exit));
            Assert.That(cues.Move("Blue", MovementStatus.Exited), Is.EqualTo(SoundCue.None));
            Assert.That(cues.Move("Amber", MovementStatus.Exited), Is.EqualTo(SoundCue.Exit));
            Assert.That(cues.Observe(GameplayState.Won), Is.EqualTo(SoundCue.Win));
            Assert.That(cues.Observe(GameplayState.Won), Is.EqualTo(SoundCue.None));
            Assert.That(cues.Observe(GameplayState.Failed), Is.EqualTo(SoundCue.None));
            cues.Reset(); Assert.That(cues.Move("Blue", MovementStatus.Exited), Is.EqualTo(SoundCue.Exit));
            Assert.That(cues.Observe(GameplayState.Failed), Is.EqualTo(SoundCue.Fail));
            Assert.That(cues.Observe(GameplayState.Failed), Is.EqualTo(SoundCue.None));
        }

        [Test]
        public void SoundRecipesAreDeterministicShortBoundedAndQuietAtTheirEndpoints()
        {
            for (var cue = 1; cue <= 6; cue++)
            {
                var samples = M4SoundDesign.Samples((SoundCue)cue);
                Assert.That(samples, Is.EqualTo(M4SoundDesign.Samples((SoundCue)cue)));
                Assert.That((double)samples.Length / M4SoundDesign.SampleRate, Is.InRange(0.05d, 0.6d));
                Assert.That(samples.Max(sample => Math.Abs(sample)), Is.InRange(0.05f, 0.36001f));
                Assert.That(Math.Abs(samples.Average(sample => (double)sample)), Is.LessThan(0.01d));
                Assert.That(samples[0], Is.Zero); Assert.That(samples[samples.Length - 1], Is.Zero);
            }
        }

        [Test]
        public void PcmWriterProducesACompleteFileAndCannotOverwriteAnExistingSound()
        {
            var path = Path.Combine(Path.GetTempPath(), "ColorSlideM4-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                M4SoundDesign.WriteNewWav(path, SoundCue.Select); M4SoundDesign.ValidateWav(path);
                var before = File.ReadAllBytes(path);
                Assert.Throws<IOException>(() => M4SoundDesign.WriteNewWav(path, SoundCue.Exit));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                Assert.That(before.Length, Is.EqualTo(44 + M4SoundDesign.Samples(SoundCue.Select).Length * 2));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
