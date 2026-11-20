using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Tiny square "pixel dust" / fireflies drifting upward inside a UI rect (entry screens, logo intro). Pooled Images, unscaled time,
    /// positions snapped to a 4 px grid so the dust stays pixel-art crisp. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelDust : MonoBehaviour
    {
        [SerializeField, Range(4, 120)] private int count = 42;
        [SerializeField] private Color[] palette = { new Color(1f, 0.82f, 0.45f), new Color(0.7f, 0.95f, 1f), new Color(0.85f, 0.6f, 1f) };
        [SerializeField, Min(1f)] private float pixelSize = 4f;
        [SerializeField, Min(0f)] private float riseSpeed = 22f;
        [SerializeField] private int seed = 7;

        private sealed class Mote { public RectTransform rt; public Image img; public float x, y, speed, phase, drift, size; public Color c; }
        private Mote[] motes;
        private RectTransform area;

        private void Awake()
        {
            area = (RectTransform)transform;
            var rng = new System.Random(seed);
            motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Dust" + i, typeof(RectTransform), typeof(Image));
                go.layer = 5;
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                var m = new Mote
                {
                    rt = (RectTransform)go.transform, img = img,
                    x = (float)rng.NextDouble(), y = (float)rng.NextDouble(),
                    speed = 0.5f + (float)rng.NextDouble(), phase = (float)rng.NextDouble() * 6.28f, drift = 6f + (float)rng.NextDouble() * 14f,
                    size = pixelSize * (rng.NextDouble() < 0.25 ? 2f : 1f), c = palette[rng.Next(palette.Length)],
                };
                m.rt.anchorMin = m.rt.anchorMax = Vector2.zero;
                m.rt.pivot = new Vector2(0.5f, 0.5f);
                m.rt.sizeDelta = Vector2.one * m.size;
                motes[i] = m;
            }
        }

        private void Update()
        {
            if (motes == null) return;
            Rect r = area.rect;
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            foreach (Mote m in motes)
            {
                m.y += riseSpeed * m.speed * dt / Mathf.Max(1f, r.height);
                if (m.y > 1.05f) { m.y = -0.05f; }
                float px = m.x * r.width + Mathf.Sin(t * 0.6f + m.phase) * m.drift;
                float py = m.y * r.height;
                m.rt.anchoredPosition = new Vector2(Mathf.Round(px / pixelSize) * pixelSize, Mathf.Round(py / pixelSize) * pixelSize);
                float blink = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 1.3f * m.speed + m.phase));
                float edge = Mathf.Clamp01(Mathf.Min(m.y, 1f - m.y) * 8f);
                m.img.color = new Color(m.c.r, m.c.g, m.c.b, blink * edge * 0.9f);
            }
        }
    }
}
