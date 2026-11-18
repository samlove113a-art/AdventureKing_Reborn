using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Item rarity as the server sends it (white &lt; blue &lt; purple &lt; orange &lt; red) → tier, colour and the drop chime.
    /// Tier 2+ (purple, orange, red) is "high": light pillar, bigger bounce and a rising chime on the client.
    /// </summary>
    public static class LootRarity
    {
        public const int High = 2;

        public static int Tier(string rarity) => rarity switch { "blue" => 1, "purple" => 2, "orange" => 3, "red" => 4, _ => 0 };

        /// <summary>One flat colour per tier (bright, saturated, readable on every map).</summary>
        public static Color ColorOf(int tier) => tier switch
        {
            1 => new Color(0.30f, 0.64f, 1f),
            2 => new Color(0.70f, 0.40f, 0.98f),
            3 => new Color(1f, 0.60f, 0.14f),
            4 => new Color(0.95f, 0.18f, 0.22f),
            _ => new Color(0.96f, 0.96f, 0.96f),
        };

        private static readonly Dictionary<int, AudioClip> chimes = new Dictionary<int, AudioClip>();

        /// <summary>
        /// The drop chime, synthesised once per tier: a rising pentatonic run (3 notes purple, 4 orange, 5 red) of bell-like partials,
        /// with a shimmer octave from orange up and a low thump for red. Higher tier = more notes, higher and longer.
        /// </summary>
        public static AudioClip Chime(int tier)
        {
            tier = Mathf.Clamp(tier, High, 4);
            if (chimes.TryGetValue(tier, out AudioClip cached) && cached) return cached;
            float[] data = Synth(tier);
            var clip = AudioClip.Create("loot_chime_t" + tier, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            chimes[tier] = clip;
            return clip;
        }

        public const int SampleRate = 44100;

        /// <summary>The raw mono samples of the chime of a tier (peak 0.8).</summary>
        public static float[] Synth(int tier)
        {
            tier = Mathf.Clamp(tier, High, 4);
            const int rate = SampleRate;
            float[] scale = { 1f, 1.2599f, 1.4983f, 1.8877f, 2f };           // C E G B C (major-seventh run)
            int notes = tier + 1;                                              // 3 / 4 / 5
            float step = 0.075f, ring = 0.55f, baseHz = 523.25f * (tier >= 3 ? 1.122f : 1f);
            int len = Mathf.CeilToInt((step * notes + ring + 0.15f) * rate);
            var data = new float[len];
            for (int n = 0; n < notes; n++)
            {
                float f = baseHz * scale[n], start = n * step;
                int s0 = Mathf.RoundToInt(start * rate);
                for (int i = s0; i < len; i++)
                {
                    float t = (i - s0) / (float)rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 6.2f);   // 4 ms attack, bell decay
                    if (t > 0.01f && env < 0.001f) break;
                    float v = Mathf.Sin(2f * Mathf.PI * f * t) + 0.32f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) + 0.14f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t);
                    if (tier >= 3) v += 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) * (0.5f + 0.5f * Mathf.Sin(t * 38f));   // shimmer
                    data[i] += v * env * (0.5f + 0.1f * n);
                }
            }
            if (tier >= 4)                                                         // low royal thump under the run
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)rate;
                    data[i] += 0.9f * Mathf.Sin(2f * Mathf.PI * (70f - 18f * t) * t) * Mathf.Exp(-t * 7f);
                }
            float peak = 0.001f;
            for (int i = 0; i < len; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < len; i++) data[i] = data[i] / peak * 0.8f;
            return data;
        }

        /// <summary>Plays the chime for a high drop on the effects bus (silent when there is no audio director, e.g. in a bare test scene).</summary>
        public static void PlayDrop(int tier)
        {
            if (tier < High) return;
            var audio = AdventureAudioDirector.Instance;
            if (audio) audio.PlayEffect(Chime(tier), 0.55f + 0.12f * (tier - High));
        }
    }
}
