using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Procedural sounds of the enhancement bench, synthesised once (no placeholder files): the hammer strike, the success run (clank, then a
    /// rising sparkle of bell notes), the dull failure thud, the soft protective chime and the shattering crack of a broken piece.
    /// </summary>
    public static class EnhanceFx
    {
        public enum Kind { Hammer, Success, Fail, Protected, Broken }

        public const int SampleRate = 44100;
        private static readonly Dictionary<Kind, AudioClip> clips = new Dictionary<Kind, AudioClip>();

        public static AudioClip Clip(Kind kind)
        {
            if (clips.TryGetValue(kind, out AudioClip c) && c) return c;
            float[] data = Synth(kind);
            c = AudioClip.Create("enhance_" + kind, data.Length, 1, SampleRate, false);
            c.SetData(data, 0);
            clips[kind] = c;
            return c;
        }

        public static void Play(Kind kind, float gain = 0.8f)
        {
            var audio = AdventureAudioDirector.Instance;
            if (audio) audio.PlayEffect(Clip(kind), gain);
        }

        /// <summary>Mono samples (peak 0.8) of a sound.</summary>
        public static float[] Synth(Kind kind)
        {
            float seconds = kind switch { Kind.Hammer => 0.35f, Kind.Success => 1.35f, Kind.Fail => 0.75f, Kind.Protected => 0.95f, _ => 1.1f };
            var d = new float[Mathf.CeilToInt(seconds * SampleRate)];
            var rng = new System.Random(1000 + (int)kind);
            switch (kind)
            {
                case Kind.Hammer: Clank(d, 0f, 1f, rng); break;
                case Kind.Success:
                    Clank(d, 0f, 1f, rng);
                    float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f };   // C6 E6 G6 C7 E7
                    for (int i = 0; i < notes.Length; i++) Bell(d, 0.18f + i * 0.075f, notes[i], 0.5f + 0.08f * i, 5.2f);
                    Bell(d, 0.58f, 3136f, 0.35f, 3.2f);                           // the shimmer on top
                    break;
                case Kind.Fail:
                    Clank(d, 0f, 0.7f, rng);
                    Thud(d, 0.02f, 95f, 1f, 9f);
                    Tone(d, 0.2f, 220f, 0.4f, 0.28f, 6f); Tone(d, 0.42f, 165f, 0.45f, 0.3f, 5f);   // a sinking two-note "aww"
                    break;
                case Kind.Protected:
                    Bell(d, 0.02f, 784f, 0.6f, 4.5f); Bell(d, 0.17f, 1174.7f, 0.6f, 4f);           // G5 D6: a soft shield chime
                    Whoosh(d, 0f, 0.4f, 0.18f, rng);
                    break;
                default:                                                                              // Broken
                    Whoosh(d, 0f, 0.09f, 1f, rng);                                                     // the crack
                    Thud(d, 0.01f, 70f, 1f, 6f);
                    for (int i = 0; i < 9; i++) Bell(d, 0.08f + (float)rng.NextDouble() * 0.5f, 2200f + (float)rng.NextDouble() * 2600f, 0.22f, 9f);   // glass
                    break;
            }
            float peak = 0.001f;
            for (int i = 0; i < d.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.8f;
            return d;
        }

        // ------------------------------------------------------------------ building blocks
        private static void Clank(float[] d, float at, float gain, System.Random rng)
        {
            Whoosh(d, at, 0.03f, 0.9f * gain, rng);                                                    // metal on metal: a click of noise ...
            Bell(d, at, 660f, 0.8f * gain, 11f); Bell(d, at, 1720f, 0.4f * gain, 14f);                 // ... and an anvil ring
        }

        private static void Bell(float[] d, float at, float hz, float gain, float decay)
        {
            int s0 = Mathf.RoundToInt(at * SampleRate);
            for (int i = Mathf.Max(0, s0); i < d.Length; i++)
            {
                float t = (i - s0) / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * decay);
                if (t > 0.02f && env < 0.0008f) break;
                d[i] += gain * env * (Mathf.Sin(6.2831853f * hz * t) + 0.3f * Mathf.Sin(6.2831853f * hz * 2.76f * t) + 0.12f * Mathf.Sin(6.2831853f * hz * 5.4f * t));
            }
        }

        private static void Tone(float[] d, float at, float hz, float gain, float length, float decay)
        {
            int s0 = Mathf.RoundToInt(at * SampleRate), n = Mathf.RoundToInt(length * SampleRate);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * decay);
                float w = Mathf.Sin(6.2831853f * hz * t) + 0.4f * Mathf.Sin(6.2831853f * hz * 2f * t) + 0.2f * Mathf.Sin(6.2831853f * hz * 3f * t);
                d[s0 + i] += gain * env * w;
            }
        }

        private static void Thud(float[] d, float at, float hz, float gain, float decay)
        {
            int s0 = Mathf.RoundToInt(at * SampleRate);
            for (int i = Mathf.Max(0, s0); i < d.Length; i++)
            {
                float t = (i - s0) / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * decay);
                if (t > 0.02f && env < 0.0008f) break;
                d[i] += gain * env * Mathf.Sin(6.2831853f * (hz - 30f * t) * t);
            }
        }

        private static void Whoosh(float[] d, float at, float length, float gain, System.Random rng)
        {
            int s0 = Mathf.RoundToInt(at * SampleRate), n = Mathf.RoundToInt(length * SampleRate);
            float prev = 0f;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = white - prev * 0.85f; prev = white;                                       // crude high-pass: bright crackle
                d[s0 + i] += gain * hp * (1f - t) * (1f - t);
            }
        }
    }
}
