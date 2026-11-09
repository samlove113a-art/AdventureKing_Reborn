using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Original background music, composed and synthesised entirely in code (deterministic seeds, no samples, no
    /// melodies from any other work): chord progressions + generated motifs played by small synth instruments
    /// (Karplus-Strong harp, FM bells/marimba, pads, flute, strings, synth drums) through a light stereo reverb.
    /// Each track is rendered twice and the second pass kept, so reverb tails wrap and the loop is seamless.
    /// STATUS: DRAFT — must be listened to and approved by the owner before any commercial release.
    /// </summary>
    public static class MusicComposer
    {
        public const string Dir = "Assets/Audio/BGM";
        private const int SR = 32000;

        public static readonly string[] Tracks = { "BGM_Login", "BGM_Village_Day", "BGM_Village_Night", "BGM_Meadow", "BGM_Boss", "BGM_Wedding" };
        public static string PathOf(string track) => $"{Dir}/{track}_DRAFT.wav";
        public static readonly Dictionary<string, (float seconds, float rmsDb, float seam, float maxJump)> LastReport = new Dictionary<string, (float, float, float, float)>();

        [MenuItem("Adventure King/Audio/Compose Original BGM (draft)")]
        public static void ComposeAll()
        {
            Directory.CreateDirectory(Dir);
            foreach (string t in Tracks) Compose(t);
            AssetDatabase.Refresh();
            foreach (string t in Tracks)
            {
                var ai = (AudioImporter)AssetImporter.GetAtPath(PathOf(t));
                if (!ai) continue;
                var s = ai.defaultSampleSettings;
                s.loadType = AudioClipLoadType.Streaming; // long loops: stream, don't hold decoded PCM in memory
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                ai.defaultSampleSettings = s;
                ai.loadInBackground = true;
                ai.SaveAndReimport();
            }
        }

        public static void ComposeFromCommandLine()
        {
            try { ComposeAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ================================================================== song descriptions
        private sealed class Song
        {
            public float Bpm; public int Beats = 4; public int Root; public int[] Scale; public int[][] Chords; // chord = scale degrees
            public int Seed; public float Reverb = 0.25f;
            public Action<Song, Mix> Arrange;
            public float BarSec => 60f / Bpm * Beats;
            public float BeatSec => 60f / Bpm;
            public int Bars => Chords.Length;
            public float Length => Bars * BarSec;
            public int Note(int degree, int octave = 0)
            {
                int n = Scale.Length, o = Mathf.FloorToInt(degree / (float)n);
                int d = degree - o * n;
                return Root + Scale[d] + 12 * (o + octave);
            }
        }

        private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] HarmMinor = { 0, 2, 3, 5, 7, 8, 11 };
        private static readonly int[] Mixo = { 0, 2, 4, 5, 7, 9, 10 };

        // chord from a scale degree: triad (degree, +2, +4)
        private static int[] C(int d) => new[] { d, d + 2, d + 4 };

        private static Song Describe(string track)
        {
            switch (track)
            {
                case "BGM_Login": // majestic, hopeful: D major, I V vi IV / IV V iii vi / I V vi IV / IV V I I
                    return new Song { Bpm = 84, Root = 62, Scale = Major, Seed = 11, Reverb = 0.38f,
                        Chords = new[] { C(0), C(4), C(5), C(3), C(3), C(4), C(2), C(5), C(0), C(4), C(5), C(3), C(3), C(4), C(0), C(0) },
                        Arrange = (s, m) => { Pad(s, m, 0.22f); HarpArp(s, m, 0.32f, 2); Melody(s, m, Inst.Bell, 0.3f, 1, 2); Bass(s, m, 0.34f, false); Timpani(s, m, 0.22f); } };
                case "BGM_Village_Day": // breezy village of wind chimes: G major, marimba + flute + light shaker
                    return new Song { Bpm = 108, Root = 55, Scale = Major, Seed = 23, Reverb = 0.22f,
                        Chords = new[] { C(0), C(3), C(4), C(0), C(5), C(3), C(1), C(4), C(0), C(3), C(4), C(5), C(3), C(4), C(0), C(0) },
                        Arrange = (s, m) => { Marimba(s, m, 0.3f); Melody(s, m, Inst.Flute, 0.28f, 1, 1); Bass(s, m, 0.36f, true); Shaker(s, m, 0.09f); Chimes(s, m, 0.12f); Pad(s, m, 0.1f); } };
                case "BGM_Village_Night": // calm music box: E minor
                    return new Song { Bpm = 72, Root = 52, Scale = Minor, Seed = 37, Reverb = 0.42f,
                        Chords = new[] { C(0), C(5), C(2), C(6), C(0), C(5), C(3), C(4), C(0), C(5), C(2), C(6), C(3), C(4), C(0), C(0) },
                        Arrange = (s, m) => { Pad(s, m, 0.2f); Melody(s, m, Inst.MusicBox, 0.26f, 2, 2); HarpArp(s, m, 0.16f, 1); Bass(s, m, 0.26f, false); } };
                case "BGM_Meadow": // adventurous: A mixolydian groove
                    return new Song { Bpm = 124, Root = 57, Scale = Mixo, Seed = 41, Reverb = 0.18f,
                        Chords = new[] { C(0), C(6), C(3), C(0), C(0), C(6), C(3), C(4), C(5), C(3), C(6), C(0), C(5), C(3), C(4), C(0) },
                        Arrange = (s, m) => { Drums(s, m, 0.4f, false); Bass(s, m, 0.4f, true); StringStabs(s, m, 0.18f); Melody(s, m, Inst.Flute, 0.3f, 1, 1); Marimba(s, m, 0.14f); } };
                case "BGM_Boss": // driving: C harmonic minor, timpani + string ostinato + brass lead
                    return new Song { Bpm = 150, Root = 48, Scale = HarmMinor, Seed = 53, Reverb = 0.2f,
                        Chords = new[] { C(0), C(5), C(6), C(4), C(0), C(3), C(4), C(4), C(5), C(3), C(0), C(4), C(5), C(6), C(4), C(0) },
                        Arrange = (s, m) => { Drums(s, m, 0.46f, true); Timpani(s, m, 0.32f); Ostinato(s, m, 0.2f); Bass(s, m, 0.42f, true); Melody(s, m, Inst.Brass, 0.27f, 1, 1); } };
                case "BGM_Wedding": // tender waltz: F major 3/4, bells + harp
                    return new Song { Bpm = 96, Beats = 3, Root = 53, Scale = Major, Seed = 67, Reverb = 0.36f,
                        Chords = new[] { C(0), C(5), C(3), C(4), C(0), C(2), C(3), C(4), C(3), C(4), C(2), C(5), C(1), C(4), C(0), C(0) },
                        Arrange = (s, m) => { Waltz(s, m, 0.3f); Melody(s, m, Inst.Flute, 0.27f, 1, 1); Melody(s, m, Inst.Bell, 0.12f, 2, 4); Pad(s, m, 0.14f); } };
            }
            throw new ArgumentException(track);
        }

        // ================================================================== arrangement building blocks
        private enum Inst { Harp, Bell, MusicBox, Marimba, Flute, Pad, Bass, Strings, Brass }

        private static void Pad(Song s, Mix m, float v)
        {
            for (int b = 0; b < s.Bars; b++)
                foreach (int d in s.Chords[b]) m.Note(Inst.Pad, b * s.BarSec, s.BarSec * 1.02f, s.Note(d), v / 3f, (d % 3 - 1) * 0.35f);
        }

        private static void HarpArp(Song s, Mix m, float v, int perBeat)
        {
            int[] shape = { 0, 1, 2, 3, 2, 1 }; // up and down the chord (+ octave)
            for (int b = 0; b < s.Bars; b++)
                for (int i = 0; i < s.Beats * perBeat; i++)
                {
                    int[] ch = s.Chords[b];
                    int k = shape[i % shape.Length];
                    int n = k < 3 ? s.Note(ch[k], 1) : s.Note(ch[0], 2);
                    m.Note(Inst.Harp, b * s.BarSec + i * s.BeatSec / perBeat, s.BeatSec * 1.5f, n, v * (i % perBeat == 0 ? 1f : 0.75f), -0.3f + 0.6f * (k / 3f));
                }
        }

        private static void Marimba(Song s, Mix m, float v)
        {
            int[] pat = { 0, 2, 1, 2, 0, 2, 1, 2 }; // eighths: root, fifth, third, fifth
            for (int b = 0; b < s.Bars; b++)
                for (int i = 0; i < s.Beats * 2; i++)
                {
                    int[] ch = s.Chords[b];
                    m.Note(Inst.Marimba, b * s.BarSec + i * s.BeatSec / 2f, 0.3f, s.Note(ch[pat[i % pat.Length]], 1), v * (i % 2 == 0 ? 1f : 0.7f), 0.25f);
                }
        }

        private static void Waltz(Song s, Mix m, float v)
        {
            for (int b = 0; b < s.Bars; b++)
            {
                int[] ch = s.Chords[b];
                m.Note(Inst.Bass, b * s.BarSec, s.BeatSec * 0.95f, s.Note(ch[0], -1), v * 1.2f, 0f);
                for (int beat = 1; beat < 3; beat++)
                    for (int k = 0; k < 3; k++)
                        m.Note(Inst.Harp, b * s.BarSec + beat * s.BeatSec + k * 0.012f, s.BeatSec, s.Note(ch[k], 1), v * 0.5f, -0.2f + k * 0.2f);
            }
        }

        private static void Bass(Song s, Mix m, float v, bool driving)
        {
            for (int b = 0; b < s.Bars; b++)
            {
                int[] ch = s.Chords[b];
                if (!driving) { m.Note(Inst.Bass, b * s.BarSec, s.BarSec * 0.95f, s.Note(ch[0], -1), v, 0f); continue; }
                for (int i = 0; i < s.Beats * 2; i++)
                {
                    bool fifth = i % 4 == 3;
                    m.Note(Inst.Bass, b * s.BarSec + i * s.BeatSec / 2f, s.BeatSec * 0.45f, s.Note(fifth ? ch[2] : ch[0], -1), v * (i % 2 == 0 ? 1f : 0.7f), 0f);
                }
            }
        }

        private static void StringStabs(Song s, Mix m, float v)
        {
            for (int b = 0; b < s.Bars; b++)
                foreach (float beat in new[] { 0f, 1.5f, 3f })
                    foreach (int d in s.Chords[b]) m.Note(Inst.Strings, b * s.BarSec + beat * s.BeatSec, s.BeatSec * 0.7f, s.Note(d), v / 3f, (d % 3 - 1) * 0.4f);
        }

        private static void Ostinato(Song s, Mix m, float v)
        {
            int[] pat = { 0, 0, 2, 0, 1, 0, 2, 1 };
            for (int b = 0; b < s.Bars; b++)
                for (int i = 0; i < s.Beats * 4; i++)
                    m.Note(Inst.Strings, b * s.BarSec + i * s.BeatSec / 4f, s.BeatSec * 0.22f, s.Note(s.Chords[b][pat[i % pat.Length]]), v * (i % 4 == 0 ? 1f : 0.65f), 0.3f);
        }

        private static void Melody(Song s, Mix m, Inst inst, float v, int octave, int sparsity)
        {
            var rng = new System.Random(s.Seed * 7 + (int)inst);
            // rhythm cells (in beats) per bar; sparsity 2+ = slower, more spacious lines
            float[][] cells4 = { new[] { 1f, 1f, 1f, 1f }, new[] { 1.5f, 0.5f, 1f, 1f }, new[] { 2f, 1f, 1f }, new[] { 1f, 0.5f, 0.5f, 2f }, new[] { 0.5f, 0.5f, 1f, 2f } };
            float[][] cells3 = { new[] { 1f, 1f, 1f }, new[] { 2f, 1f }, new[] { 1.5f, 0.5f, 1f }, new[] { 3f } };
            float[][] cells = s.Beats == 3 ? cells3 : cells4;
            // phrase A (bars 0-3) and B (8-11) are composed; bars 4-7 and 12-15 answer them (A', A'' with cadence)
            var phrases = new Dictionary<int, List<(float t, float len, int deg)>>();
            int prev = 7; // degree around the middle of the range
            List<(float, float, int)> ComposePhrase(int startBar)
            {
                var notes = new List<(float, float, int)>();
                for (int b = 0; b < 4; b++)
                {
                    float[] cell = cells[rng.Next(cells.Length)];
                    if (sparsity > 1 && rng.NextDouble() < 0.5) cell = new[] { (float)s.Beats };
                    float t = 0f;
                    int[] ch = s.Chords[startBar + b];
                    for (int i = 0; i < cell.Length; i++)
                    {
                        int deg;
                        if (i == 0) { deg = Nearest(ch, prev, s.Scale.Length); } // strong beat: chord tone
                        else deg = prev + (rng.NextDouble() < 0.5 ? -1 : 1) * (rng.NextDouble() < 0.75 ? 1 : 2);
                        deg = Mathf.Clamp(deg, 3, 12);
                        notes.Add((b * s.Beats + t, cell[i], deg));
                        prev = deg; t += cell[i];
                    }
                }
                return notes;
            }
            phrases[0] = ComposePhrase(0);
            phrases[8] = ComposePhrase(8);
            void Play(List<(float t, float len, int deg)> ph, int bar, bool cadence, int shift)
            {
                for (int i = 0; i < ph.Count; i++)
                {
                    var (t, len, deg) = ph[i];
                    if (sparsity > 2 && i % 2 == 1) continue;
                    int d = deg + shift;
                    // re-harmonise: a note on a downbeat always sits on the chord actually playing in that bar
                    if (Mathf.Approximately(t % s.Beats, 0f)) d = Nearest(s.Chords[bar + (int)(t / s.Beats)], d, s.Scale.Length);
                    bool last = i == ph.Count - 1;
                    if (cadence && last) d = Nearest(new[] { 0, 7 }, d, s.Scale.Length); // land on the tonic
                    float start = (bar * s.Beats + t) * s.BeatSec;
                    m.Note(inst, start, len * s.BeatSec * 0.92f, s.Note(d, octave - 1), v * (Mathf.Approximately(t % s.Beats, 0f) ? 1f : 0.8f), 0.12f);
                }
            }
            Play(phrases[0], 0, false, 0);
            Play(phrases[0], 4, true, 0);
            Play(phrases[8], 8, false, 0);
            Play(phrases[0], 12, true, 0);
        }

        private static int Nearest(int[] chordDegrees, int around, int scaleLen)
        {
            int best = around, bestDist = int.MaxValue;
            foreach (int c in chordDegrees)
                for (int o = -2; o <= 3; o++)
                {
                    int cand = c + o * scaleLen, dist = Math.Abs(cand - around);
                    if (dist < bestDist) { bestDist = dist; best = cand; }
                }
            return best;
        }

        private static void Drums(Song s, Mix m, float v, bool heavy)
        {
            for (int b = 0; b < s.Bars; b++)
                for (int i = 0; i < s.Beats * 2; i++)
                {
                    float t = b * s.BarSec + i * s.BeatSec / 2f;
                    if (i % 4 == 0 || (heavy && i % 2 == 0) || (!heavy && i == 5)) m.Kick(t, v);
                    if (i % 4 == 2) m.Snare(t, v * 0.7f);
                    m.Hat(t, v * (i % 2 == 0 ? 0.32f : 0.22f));
                }
        }

        private static void Timpani(Song s, Mix m, float v)
        {
            int Low(int midi) { while (midi > 45) midi -= 12; while (midi < 33) midi += 12; return midi; } // 55-110 Hz
            for (int b = 0; b < s.Bars; b++)
            {
                m.Timp(b * s.BarSec, Low(s.Note(s.Chords[b][0])), v);
                if (b % 4 == 3) m.Timp(b * s.BarSec + (s.Beats - 1) * s.BeatSec, Low(s.Note(s.Chords[b][0])), v * 0.7f);
            }
        }

        private static void Shaker(Song s, Mix m, float v)
        {
            for (int b = 0; b < s.Bars; b++)
                for (int i = 0; i < s.Beats * 4; i++) m.Hat(b * s.BarSec + i * s.BeatSec / 4f, v * (i % 2 == 0 ? 0.9f : 0.5f), 0.025f);
        }

        private static void Chimes(Song s, Mix m, float v)
        {
            var rng = new System.Random(s.Seed + 99);
            for (int b = 1; b < s.Bars; b += 2) // a gust of wind chimes every other bar
                for (int k = 0; k < 4; k++)
                {
                    int d = s.Chords[b][rng.Next(3)] + 7 * (1 + rng.Next(2));
                    m.Note(Inst.Bell, b * s.BarSec + s.BeatSec * (2f + k * 0.37f + (float)rng.NextDouble() * 0.1f), 2.4f, s.Note(d, 1), v * (1f - k * 0.18f), (float)rng.NextDouble() * 1.2f - 0.6f);
                }
        }

        // ================================================================== mixer + instruments
        private sealed class Mix
        {
            public readonly float[] L, R, VerbIn;
            public readonly float Loop;
            private readonly int n;
            private readonly System.Random noise = new System.Random(5);

            public Mix(float loopSec)
            {
                Loop = loopSec;
                n = Mathf.CeilToInt(loopSec * 2f * SR) + SR;
                L = new float[n]; R = new float[n]; VerbIn = new float[n];
            }

            private void Put(int i, float x, float pan, float send)
            {
                if (i < 0 || i >= n) return;
                float pl = Mathf.Sqrt(0.5f * (1f - pan)), pr = Mathf.Sqrt(0.5f * (1f + pan));
                L[i] += x * pl; R[i] += x * pr; VerbIn[i] += x * send;
            }

            private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

            /// <summary>Plays the note in both passes of the loop (the second pass is the one exported).</summary>
            public void Note(Inst inst, float t, float dur, int midi, float vel, float pan)
            {
                for (int pass = 0; pass < 2; pass++) Render(inst, t + pass * Loop, dur, midi, vel, pan);
            }

            private void Render(Inst inst, float t, float dur, int midi, float vel, float pan)
            {
                float f = Hz(midi);
                int s0 = Mathf.RoundToInt(t * SR);
                switch (inst)
                {
                    case Inst.Harp: Pluck(s0, f, dur + 1.2f, vel, pan, 0.996f); return;
                    case Inst.Bass: Tone(s0, dur, vel, pan, 0.05f, k => Mathf.Sin(k * f) + 0.3f * Mathf.Sin(2f * k * f) + 0.08f * Mathf.Sin(3f * k * f), 0.006f, 0.12f, 0.6f); return;
                    case Inst.Bell: Fm(s0, f, 3.5f, 2.6f, 2.2f + dur * 0.3f, vel, pan, 0.4f); return;
                    case Inst.MusicBox: Fm(s0, f, 5.0f, 1.4f, 1.6f, vel, pan, 0.45f); return;
                    case Inst.Marimba: Fm(s0, f, 4.0f, 1.8f, 0.45f, vel, pan, 0.15f); return;
                    case Inst.Flute:
                        Tone(s0, dur, vel, pan, 0.25f, (k, sec) => Mathf.Sin(k * f * (1f + 0.004f * Mathf.Sin(sec * 33f) * Mathf.Clamp01(sec * 3f - 0.6f))) + 0.18f * Mathf.Sin(2f * k * f) + 0.06f * Mathf.Sin(3f * k * f), 0.06f, 0.18f);
                        return;
                    case Inst.Pad:
                        Tone(s0, dur, vel, pan, 0.5f, k => 0.5f * (Mathf.Sin(k * f * 1.003f) + Mathf.Sin(k * f * 0.997f)) + 0.25f * Mathf.Sin(2f * k * f) + 0.1f * Mathf.Sin(3f * k * f * 1.002f), 0.5f, 0.7f);
                        return;
                    case Inst.Strings:
                        Tone(s0, dur, vel, pan, 0.3f, k => Saw(k * f * 1.004f, 7) * 0.5f + Saw(k * f * 0.996f, 7) * 0.5f, 0.03f, 0.15f);
                        return;
                    case Inst.Brass:
                        Tone(s0, dur, vel, pan, 0.2f, (k, sec) => Saw(k * f, 4 + Mathf.RoundToInt(6f * Mathf.Clamp01(sec * 6f))), 0.04f, 0.12f);
                        return;
                }
            }

            private static float Saw(float ph, int harmonics)
            {
                float x = 0f;
                for (int h = 1; h <= harmonics; h++) x += Mathf.Sin(ph * h) / h;
                return x * 0.6f;
            }

            private void Tone(int s0, float dur, float vel, float pan, float send, Func<float, float> wave, float attack, float release, float decayTo = 1f)
                => Tone(s0, dur, vel, pan, send, (k, _) => wave(k), attack, release, decayTo);

            private void Tone(int s0, float dur, float vel, float pan, float send, Func<float, float, float> wave, float attack, float release, float decayTo = 1f)
            {
                int len = Mathf.RoundToInt((dur + release) * SR);
                for (int i = 0; i < len; i++)
                {
                    float sec = i / (float)SR;
                    float env = Mathf.Clamp01(sec / Mathf.Max(0.001f, attack));
                    env *= Mathf.Lerp(1f, decayTo, Mathf.Clamp01(sec / Mathf.Max(0.01f, dur)));
                    if (sec > dur) env *= Mathf.Clamp01(1f - (sec - dur) / release);
                    float k = 2f * Mathf.PI * sec;
                    Put(s0 + i, wave(k, sec) * env * vel, pan, send);
                }
            }

            private void Fm(int s0, float f, float ratio, float index, float decay, float vel, float pan, float send)
            {
                int len = Mathf.RoundToInt(decay * 4f * SR);
                for (int i = 0; i < len; i++)
                {
                    float sec = i / (float)SR;
                    float env = Mathf.Exp(-sec / decay * 2.2f) * Mathf.Clamp01(sec / 0.002f);
                    float idx = index * Mathf.Exp(-sec / decay * 4f);
                    float x = Mathf.Sin(2f * Mathf.PI * f * sec + idx * Mathf.Sin(2f * Mathf.PI * f * ratio * sec));
                    Put(s0 + i, x * env * vel, pan, send);
                }
            }

            private void Pluck(int s0, float f, float dur, float vel, float pan, float damp)
            {
                int period = Mathf.Max(2, Mathf.RoundToInt(SR / f));
                var buf = new float[period];
                var rng = new System.Random(period * 31 + s0);
                for (int i = 0; i < period; i++) buf[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 1; i < period; i++) buf[i] = 0.5f * (buf[i] + buf[i - 1]); // softer attack (rounded harp)
                int len = Mathf.RoundToInt(dur * SR), p = 0;
                for (int i = 0; i < len; i++)
                {
                    float x = buf[p];
                    int q = (p + 1) % period;
                    buf[p] = damp * 0.5f * (buf[p] + buf[q]);
                    p = q;
                    float fade = (i > len - 400 ? (len - i) / 400f : 1f) * Mathf.Clamp01(i / (0.003f * SR)); // 3 ms attack ramp
                    Put(s0 + i, x * vel * fade * 0.8f, pan, 0.35f);
                }
            }

            public void Kick(float t, float v) { for (int pass = 0; pass < 2; pass++) Drum(t + pass * Loop, v, 0); }
            public void Snare(float t, float v) { for (int pass = 0; pass < 2; pass++) Drum(t + pass * Loop, v, 1); }
            public void Hat(float t, float v, float decay = 0.04f) { for (int pass = 0; pass < 2; pass++) HatAt(t + pass * Loop, v, decay); }
            public void Timp(float t, int midi, float v) { for (int pass = 0; pass < 2; pass++) TimpAt(t + pass * Loop, Hz(midi), v); }

            private void Drum(float t, float v, int kind)
            {
                int s0 = Mathf.RoundToInt(t * SR);
                int len = (int)(0.35f * SR);
                float phase = 0f;
                for (int i = 0; i < len; i++)
                {
                    float sec = i / (float)SR;
                    float x;
                    if (kind == 0)
                    {
                        float f = Mathf.Lerp(45f, 130f, Mathf.Exp(-sec * 30f));
                        phase += 2f * Mathf.PI * f / SR;
                        x = Mathf.Sin(phase) * Mathf.Exp(-sec * 9f) * 1.1f;
                    }
                    else
                    {
                        phase += 2f * Mathf.PI * 190f / SR;
                        x = (0.55f * (float)(noise.NextDouble() * 2 - 1) * Mathf.Exp(-sec * 22f)) + 0.4f * Mathf.Sin(phase) * Mathf.Exp(-sec * 30f);
                    }
                    Put(s0 + i, x * v, 0f, kind == 1 ? 0.2f : 0.05f);
                }
            }

            private void HatAt(float t, float v, float decay)
            {
                int s0 = Mathf.RoundToInt(t * SR), len = (int)(decay * 6f * SR);
                float last = 0f;
                for (int i = 0; i < len; i++)
                {
                    float w = (float)(noise.NextDouble() * 2 - 1);
                    float hp = w - last; last = w; // crude high-pass
                    Put(s0 + i, hp * 0.5f * v * Mathf.Exp(-(i / (float)SR) / decay), 0.3f, 0.05f);
                }
            }

            private void TimpAt(float t, float f, float v)
            {
                int s0 = Mathf.RoundToInt(t * SR), len = (int)(1.6f * SR);
                for (int i = 0; i < len; i++)
                {
                    float sec = i / (float)SR;
                    float x = Mathf.Sin(2f * Mathf.PI * f * sec) * Mathf.Exp(-sec * 2.2f) + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 1.5f * sec) * Mathf.Exp(-sec * 4f)
                              + 0.3f * (float)(noise.NextDouble() * 2 - 1) * Mathf.Exp(-sec * 40f);
                    Put(s0 + i, x * v, -0.1f, 0.3f);
                }
            }
        }

        // ================================================================== render
        private static void Compose(string track)
        {
            Song s = Describe(track);
            var m = new Mix(s.Length);
            s.Arrange(s, m);
            Reverb(m, s.Reverb);
            int start = Mathf.RoundToInt(s.Length * SR), len = Mathf.RoundToInt(s.Length * SR);
            // gentle master: normalise to -2 dBFS peak, soft-saturate the rare overs
            float peak = 0f;
            for (int i = start; i < start + len; i++) peak = Mathf.Max(peak, Mathf.Abs(m.L[i]), Mathf.Abs(m.R[i]));
            float g = peak > 0f ? 0.79f / peak : 1f;
            var pcm = new short[len * 2];
            double sq = 0;
            for (int i = 0; i < len; i++)
            {
                float l = (float)Math.Tanh(m.L[start + i] * g * 1.05f), r = (float)Math.Tanh(m.R[start + i] * g * 1.05f);
                pcm[i * 2] = (short)Mathf.Clamp(Mathf.RoundToInt(l * 32767f), -32767, 32767);
                pcm[i * 2 + 1] = (short)Mathf.Clamp(Mathf.RoundToInt(r * 32767f), -32767, 32767);
                sq += l * l + r * r;
            }
            WriteWav(PathOf(track), pcm, 2);
            float rms = (float)Math.Sqrt(sq / (len * 2));
            // loop seam: the jump between the last and first sample pair (small = no click when looping)
            float seam = Mathf.Max(Mathf.Abs(pcm[pcm.Length - 2] - pcm[0]), Mathf.Abs(pcm[pcm.Length - 1] - pcm[1])) / 32767f;
            // compare with the jumps the music itself makes at every downbeat (attacks): a seam no bigger than the
            // largest in-loop jump is inaudible as a click
            float maxJump = 0f;
            for (int i = 2; i < pcm.Length; i++) maxJump = Mathf.Max(maxJump, Mathf.Abs(pcm[i] - pcm[i - 2]) / 32767f);
            Debug.Log($"[BGM] {track}: {s.Length:F1}s {s.Bpm}bpm rmsDb={20f * Mathf.Log10(rms):F1} seam={seam:F4} maxInLoopJump={maxJump:F4} seamOk={seam <= maxJump}");
            LastReport[track] = (s.Length, 20f * Mathf.Log10(rms), seam, maxJump);
        }

        /// <summary>Freeverb-style stereo reverb (4 combs + 2 allpasses per side) on the send bus.</summary>
        private static void Reverb(Mix m, float wet)
        {
            int[] combL = { 1116, 1188, 1277, 1356 }, combR = { 1139, 1211, 1300, 1379 };
            int[] apL = { 556, 441 }, apR = { 579, 464 };
            float scale = SR / 44100f;
            void Side(float[] dst, int[] combs, int[] aps)
            {
                int n = m.VerbIn.Length;
                var outBuf = new float[n];
                foreach (int c0 in combs)
                {
                    int c = Mathf.RoundToInt(c0 * scale);
                    var buf = new float[c]; int p = 0; float store = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float y = buf[p];
                        store = y * 0.75f + store * 0.25f; // damping
                        buf[p] = m.VerbIn[i] * 0.015f + store * 0.84f;
                        outBuf[i] += y;
                        p = (p + 1) % c;
                    }
                }
                foreach (int a0 in aps)
                {
                    int a = Mathf.RoundToInt(a0 * scale);
                    var buf = new float[a]; int p = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float b = buf[p];
                        float y = -outBuf[i] + b;
                        buf[p] = outBuf[i] + b * 0.5f;
                        outBuf[i] = y;
                        p = (p + 1) % a;
                    }
                }
                for (int i = 0; i < n; i++) dst[i] += outBuf[i] * wet * 3f;
            }
            Side(m.L, combL, apL);
            Side(m.R, combR, apR);
        }

        private static void WriteWav(string path, short[] pcm, int channels)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int dataLen = pcm.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataLen); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(SR);
            w.Write(SR * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataLen);
            foreach (short v in pcm) w.Write(v);
        }

        /// <summary>Loads a composed track (null if not composed yet).</summary>
        public static AudioClip Clip(string track) => AssetDatabase.LoadAssetAtPath<AudioClip>(PathOf(track));
    }
}
