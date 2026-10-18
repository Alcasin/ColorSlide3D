using System;
using System.IO;
using System.Text;
using ColorSlide.Polish;

namespace ColorSlide.Editor
{
    /// <summary>Original deterministic tonal/plastic SFX. No Unity process or external sound library is required.</summary>
    public static class M4SoundDesign
    {
        public const int SampleRate = 22050;
        public static float[] Samples(SoundCue cue)
        {
            var duration = cue == SoundCue.Select ? 0.09 : cue == SoundCue.Blocked ? 0.14 : cue == SoundCue.Exit ? 0.24 :
                cue == SoundCue.UiClick ? 0.065 : cue == SoundCue.Win ? 0.56 : cue == SoundCue.Fail ? 0.43 : 0d;
            if (duration == 0d) throw new ArgumentOutOfRangeException(nameof(cue));
            var samples = new float[(int)Math.Ceiling(duration * SampleRate)];
            uint noise = 0x51D3A45u + (uint)cue;
            var phase = 0d;
            var interaction = cue == SoundCue.Select || cue == SoundCue.Blocked;
            var noiseLow = 0d; var plasticNoise = 0d;
            var noiseFilter = 1d - Math.Exp(-2d * Math.PI * 650d / SampleRate);
            var notes = cue == SoundCue.Win ? new[] { 523.25, 659.25, 783.99, 1046.5 } : new[] { 329.63, 293.66, 261.63 };
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (double)i / SampleRate; double value;
                if (cue == SoundCue.Win || cue == SoundCue.Fail)
                {
                    value = 0d;
                    for (var note = 0; note < notes.Length; note++)
                    {
                        var local = t - note * 0.09;
                        if (local < 0d) continue;
                        value += Tone(notes[note], local) * Math.Exp(-local * (cue == SoundCue.Win ? 15d : 20d)) * Attack(local);
                    }
                    value *= cue == SoundCue.Win ? 0.22 : 0.17;
                }
                else
                {
                    var frequency = cue == SoundCue.Select ? 280d : cue == SoundCue.Blocked ? 180d :
                        cue == SoundCue.Exit ? 523.25 * Math.Pow(2d, t / duration) : 880d;
                    phase += Math.PI * 2d * frequency / SampleRate;
                    noise = unchecked(noise * 1664525u + 1013904223u);
                    var whiteNoise = (noise >> 8) / 16777215d * 2d - 1d;
                    if (interaction)
                    {
                        // Damped, inharmonic plastic modes; no pitch sweep or bright broadband contact click.
                        noiseLow += noiseFilter * (whiteNoise - noiseLow);
                        plasticNoise += noiseFilter * (noiseLow - plasticNoise);
                        var decay = cue == SoundCue.Select ? 35d : 50d;
                        var mode = cue == SoundCue.Select ? 1.67 : 1.57;
                        var overtone = cue == SoundCue.Select ? 0.12 : 0.08;
                        var attack = Math.Sin(Math.PI / 2d * Math.Min(1d, t / 0.010));
                        value = (Math.Sin(phase) * Math.Exp(-t * decay) +
                            overtone * Math.Sin(phase * mode) * Math.Exp(-t * decay * 1.7) +
                            plasticNoise * 0.08 * Math.Exp(-t * 70d)) * attack * attack;
                        value *= cue == SoundCue.Select ? 0.17 : 0.13;
                    }
                    else
                    {
                        var softNoise = whiteNoise * 0.035;
                        value = (Math.Sin(phase) + 0.22 * Math.Sin(phase * 2d) + softNoise) * Attack(t) * Math.Exp(-t * 12d);
                        value *= cue == SoundCue.UiClick ? 0.18 : 0.25;
                    }
                }
                var release = Math.Sin(Math.PI / 2d * Math.Min(1d, (duration - t) / 0.025));
                samples[i] = (float)(value * release * release);
            }
            samples[0] = samples[samples.Length - 1] = 0f;
            var peak = 0f;
            foreach (var sample in samples) peak = Math.Max(peak, Math.Abs(sample));
            if (peak > 0.36f) for (var i = 0; i < samples.Length; i++) samples[i] *= 0.36f / peak;
            return samples;
        }
        private static double Attack(double time)
        { var value = Math.Sin(Math.PI / 2d * Math.Min(1d, time / 0.006)); return value * value; }
        private static double Tone(double frequency, double time) =>
            Math.Sin(Math.PI * 2d * frequency * time) + 0.16 * Math.Sin(Math.PI * 4d * frequency * time);

        public static void WriteNewWav(string path, SoundCue cue)
        {
            var samples = Samples(cue);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(SampleRate); writer.Write(SampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (var sample in samples) writer.Write((short)Math.Round(sample * short.MaxValue));
            }
        }

        public static void ValidateWav(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "RIFF" || reader.ReadInt32() != reader.BaseStream.Length - 8 || new string(reader.ReadChars(4)) != "WAVE")
                    throw new InvalidDataException("Not a complete RIFF WAV: " + path);
                var format = false; var data = false; var rate = 0; var channels = 0;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    var id = new string(reader.ReadChars(4)); var length = reader.ReadInt32(); var end = reader.BaseStream.Position + length;
                    if (length < 0 || end > reader.BaseStream.Length) throw new InvalidDataException("Truncated WAV chunk: " + path);
                    if (id == "fmt ")
                    {
                        if (length < 16 || reader.ReadInt16() != 1) throw new InvalidDataException("Use uncompressed PCM WAV: " + path);
                        channels = reader.ReadInt16(); rate = reader.ReadInt32(); var bytes = reader.ReadInt32(); var alignment = reader.ReadInt16(); var bits = reader.ReadInt16();
                        format = (channels == 1 || channels == 2) && rate >= 22050 && rate <= 48000 && bits == 16 && alignment == channels * 2 && bytes == rate * alignment;
                    }
                    else if (id == "data")
                    {
                        if (!format || length == 0 || length % (channels * 2) != 0 || length > rate * channels * 4)
                            throw new InvalidDataException("Invalid or oversized short SFX data: " + path);
                        data = true;
                        while (reader.BaseStream.Position < end)
                            if (Math.Abs((int)reader.ReadInt16()) >= 32760) throw new InvalidDataException("Clipping PCM sample: " + path);
                    }
                    reader.BaseStream.Position = end + (length & 1);
                }
                if (!format || !data) throw new InvalidDataException("Missing 16-bit PCM WAV format/data: " + path);
            }
        }
    }
}
