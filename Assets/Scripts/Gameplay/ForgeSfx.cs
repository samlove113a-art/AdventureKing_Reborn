using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Tiny synthesized forge sounds (no audio files): anvil strike + rising chime on success, dull thud + crackle on failure.</summary>
    public static class ForgeSfx
    {
        private static AudioClip success, failure, strike;
        private const int Rate = 44100;

        public static AudioClip Strike => strike ? strike : strike = Make("forge_strike", 0.32f, (t, n) =>
            Mathf.Sin(2f * Mathf.PI * 1180f * t) * Mathf.Exp(-t * 16f) * 0.45f + Mathf.Sin(2f * Mathf.PI * 2360f * t) * Mathf.Exp(-t * 24f) * 0.25f + n * Mathf.Exp(-t * 60f) * 0.5f);

        public static AudioClip Success => success ? success : success = Make("forge_success", 0.9f, (t, n) =>
        {
            float[] notes = { 660f, 830f, 990f, 1320f };
            float v = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float s = t - i * 0.09f;
                if (s > 0f) v += Mathf.Sin(2f * Mathf.PI * notes[i] * s) * Mathf.Exp(-s * 5f) * 0.28f;
            }
            return v;
        });

        public static AudioClip Failure => failure ? failure : failure = Make("forge_failure", 0.7f, (t, n) =>
            Mathf.Sin(2f * Mathf.PI * (150f - 70f * t) * t) * Mathf.Exp(-t * 7f) * 0.6f + n * Mathf.Exp(-t * 14f) * 0.35f);

        private static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, (float)(rng.NextDouble() * 2 - 1)), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(AudioClip clip, float gain = 0.8f)
        {
            if (!clip) return;
            if (AdventureAudioDirector.Instance) AdventureAudioDirector.Instance.PlayEffect(clip, gain);
            else if (Camera.main) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, gain);
        }
    }
}
