using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Shared world light state. Night = 0 (day) .. 1 (night). The Vietnam-time day/night cycle (UTC+7, Phase B)
    /// will drive it; until then it stays at day unless QA forces night to verify the lamps.
    /// </summary>
    public static class WorldLighting
    {
        public static float Night { get; set; }
    }

    /// <summary>
    /// Mobile-friendly lantern light: an additive glow on the lantern body plus a soft warm pool on the ground,
    /// no real-time lights. Faint by day, full at night, with a gentle candle flicker.
    /// </summary>
    public sealed class LanternGlow : MonoBehaviour
    {
        [SerializeField] private Renderer halo;
        [SerializeField] private Renderer pool;
        [SerializeField] private Color color = new Color(1f, 0.62f, 0.3f, 1f);
        [SerializeField] private float dayStrength = 0.18f;
        [SerializeField] private float nightStrength = 1f;

        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private MaterialPropertyBlock block;
        private float seed;

        public float Strength { get; private set; }

        public void Bind(Renderer haloRenderer, Renderer poolRenderer)
        {
            halo = haloRenderer;
            pool = poolRenderer;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            float flicker = 1f + (Mathf.PerlinNoise(seed, Time.time * 2.3f) - 0.5f) * 0.12f;
            Strength = Mathf.Lerp(dayStrength, nightStrength, Mathf.Clamp01(WorldLighting.Night)) * flicker;
            Apply(halo, Strength);
            Apply(pool, Strength * 0.6f);
        }

        private void Apply(Renderer r, float s)
        {
            if (!r) return;
            r.GetPropertyBlock(block);
            block.SetColor(TintId, new Color(color.r, color.g, color.b, 1f) * s);
            r.SetPropertyBlock(block);
        }
    }
}
