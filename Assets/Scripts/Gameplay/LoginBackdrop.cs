using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Fantasy dawn backdrop behind the entry screens (login, register, hero hall): baked depth layers of Thị trấn
    /// Chuông Gió (LoginBackdropBaker) drift slowly at different rates (parallax), with drifting mist, soft light shafts
    /// and fireflies on top. Motion is gentle (a full sway takes ~20 s, a few % of the screen) to avoid dizziness, and
    /// follows the mouse a little on PC. On the "Thấp" graphics preset everything is static and the particles are off.
    /// Layers are envelope-fitted (never stretched, never letterboxed) with overscan so parallax never shows an edge.
    /// </summary>
    public sealed class LoginBackdrop : MonoBehaviour
    {
        [SerializeField] private RawImage[] layers = new RawImage[0];
        [Tooltip("Parallax share per layer (0 = fixed sky … 1 = nearest).")]
        [SerializeField] private float[] depth = new float[0];
        [SerializeField, Range(0f, 0.05f)] private float swayFraction = 0.018f;
        [SerializeField, Min(1f)] private float swayPeriod = 22f;
        [SerializeField, Range(0f, 0.05f)] private float pointerFraction = 0.012f;
        [SerializeField, Range(0, 60)] private int fireflies = 26;

        private readonly List<(RectTransform rt, Image img, Vector2 home, float phase, float speed)> flies = new List<(RectTransform, Image, Vector2, float, float)>();
        private RectTransform mist, rays;
        private Image raysImg;
        private Vector2 pointer;
        private bool reduced;

        public bool Reduced => reduced;
        public int LayerCount => layers.Length;

        private void Start()
        {
            reduced = GraphicsQualityOption.Load() == GraphicsQualityOption.Level.Low;
            BuildEffects();
        }

        private static Texture2D Tex(int w, int h, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        private RectTransform Overlay(string name, Texture2D tex, Color tint, int index)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            go.transform.SetSiblingIndex(index);
            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = tint;
            raw.raycastTarget = false;
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        private void BuildEffects()
        {
            int nearIndex = Mathf.Max(0, layers.Length - 1);
            // light shafts from the sun (upper left), behind the near layer
            Texture2D rayTex = Tex(256, 256, (u, v) =>
            {
                Vector2 d = new Vector2(u - 0.15f, v - 0.95f);
                float a = Mathf.Atan2(d.y, d.x), r = d.magnitude;
                float beams = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(a * 22f), 6f) * Mathf.Clamp01(1.2f - r);
                return new Color(1f, 0.93f, 0.78f, beams * 0.55f);
            });
            rays = Overlay("LightShafts", rayTex, new Color(1f, 1f, 1f, 0.35f), nearIndex);
            raysImg = null;
            // drifting mist band at the hill line
            Texture2D mistTex = Tex(512, 64, (u, v) =>
            {
                float n = Mathf.PerlinNoise(u * 9f, v * 3f) * 0.6f + Mathf.PerlinNoise(u * 23f, v * 7f) * 0.4f;
                float band = Mathf.Exp(-Mathf.Pow((v - 0.5f) * 3.2f, 2f));
                return new Color(1f, 0.95f, 0.92f, Mathf.Clamp01(n * band * 1.1f - 0.15f));
            });
            mist = Overlay("Mist", mistTex, new Color(1f, 1f, 1f, 0.55f), nearIndex);
            mist.anchorMin = new Vector2(-0.3f, 0.22f);
            mist.anchorMax = new Vector2(1.3f, 0.48f);
            if (reduced) return;
            Texture2D dot = Tex(32, 32, (u, v) =>
            {
                float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));
            });
            Sprite sp = Sprite.Create(dot, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
            var rng = new System.Random(11);
            for (int i = 0; i < fireflies; i++)
            {
                var go = new GameObject("Firefly", typeof(RectTransform));
                go.layer = 5;
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                img.sprite = sp;
                img.raycastTarget = false;
                img.color = rng.NextDouble() > 0.5 ? new Color(1f, 0.92f, 0.55f, 0.8f) : new Color(0.7f, 1f, 0.85f, 0.75f);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2((float)rng.NextDouble(), 0.08f + (float)rng.NextDouble() * 0.5f);
                float s = 10f + (float)rng.NextDouble() * 16f;
                r.sizeDelta = new Vector2(s, s);
                flies.Add((r, img, r.anchorMin, (float)rng.NextDouble() * 10f, 0.4f + (float)rng.NextDouble() * 0.8f));
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            var rect = ((RectTransform)transform).rect;
            if (!reduced)
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current is { } m && Screen.width > 0)
                {
                    Vector2 p = m.position.ReadValue();
                    Vector2 target = new Vector2(p.x / Screen.width - 0.5f, p.y / Screen.height - 0.5f) * 2f;
                    target.x = Mathf.Clamp(target.x, -1f, 1f); target.y = Mathf.Clamp(target.y, -1f, 1f);
                    pointer = Vector2.Lerp(pointer, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f));
                }
#endif
                float sway = Mathf.Sin(t * Mathf.PI * 2f / swayPeriod);
                for (int i = 0; i < layers.Length; i++)
                {
                    if (!layers[i]) continue;
                    float d = i < depth.Length ? depth[i] : 1f;
                    Vector2 off = new Vector2((sway * swayFraction - pointer.x * pointerFraction) * rect.width,
                                              (Mathf.Sin(t * 0.21f) * 0.002f - pointer.y * pointerFraction * 0.4f) * rect.height) * d;
                    layers[i].rectTransform.anchoredPosition = off;
                }
                if (mist) mist.anchoredPosition = new Vector2(Mathf.Repeat(t * 6f, rect.width * 0.3f) - rect.width * 0.15f, 0f);
                foreach (var (rt, img, home, phase, speed) in flies)
                {
                    float k = t * speed + phase;
                    rt.anchoredPosition = new Vector2(Mathf.Sin(k * 0.7f) * 40f + Mathf.Sin(k * 1.9f) * 12f, Mathf.Sin(k * 0.5f) * 30f + Mathf.Repeat(k * 6f, 60f));
                    Color c = img.color;
                    c.a = 0.25f + 0.6f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(k * 2.3f), 2f);
                    img.color = c;
                }
            }
            if (rays) rays.GetComponent<RawImage>().color = new Color(1f, 1f, 1f, reduced ? 0.25f : 0.22f + 0.1f * Mathf.Sin(t * 0.4f));
        }
    }
}
