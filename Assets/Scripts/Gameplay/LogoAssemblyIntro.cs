using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Intro: the official game logo is cut into a grid of tiles that fly in from scattered, rotated, tiny starting poses and click into place
    /// (staggered from the centre outwards), a pixel-dust burst sparks as each wave lands, then a light sweep glides over the finished logo.
    /// ~2.6 s, tap / click / any key skips. Pure UI RawImages, unscaled time, no 3D. Raises <see cref="Finished"/> once (done or skipped).
    /// </summary>
    public sealed class LogoAssemblyIntro : MonoBehaviour
    {
        [SerializeField] private Sprite logo;
        [SerializeField] private RectTransform stage;
        [SerializeField, Range(4, 24)] private int columns = 14, rows = 8;
        [SerializeField, Min(0.5f)] private float duration = 2.6f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.35f;
        [SerializeField] private RectTransform sweep;
        [SerializeField] private CanvasGroup fadeOut;
        [SerializeField] private int seed = 11;

        public event Action Finished;
        public bool Done { get; private set; }
        public float Progress => Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);

        private sealed class Tile { public RectTransform rt; public RawImage img; public Vector2 from, to; public float rotFrom, delay, scaleFrom; }
        private Tile[] tiles;
        private float startedAt;
        private bool built;

        private void OnEnable()
        {
            Done = false;
            if (!built) Build();
            startedAt = Time.unscaledTime;
            if (sweep) sweep.gameObject.SetActive(false);
            if (fadeOut) fadeOut.alpha = 1f;
        }

        private void Build()
        {
            built = true;
            if (!logo || !stage) { tiles = new Tile[0]; return; }
            Texture tex = logo.texture;
            Rect sr = logo.rect;
            Vector2 size = stage.rect.size;
            if (size.x < 2f) size = new Vector2(920f, 518f);
            float tw = size.x / columns, th = size.y / rows;
            var rng = new System.Random(seed);
            tiles = new Tile[columns * rows];
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < columns; cx++)
                {
                    var go = new GameObject($"Tile_{cx}_{cy}", typeof(RectTransform), typeof(RawImage));
                    go.layer = 5;
                    go.transform.SetParent(stage, false);
                    var img = go.GetComponent<RawImage>();
                    img.texture = tex;
                    img.raycastTarget = false;
                    img.uvRect = new Rect((sr.x + sr.width * cx / columns) / tex.width, (sr.y + sr.height * cy / rows) / tex.height,
                        sr.width / columns / tex.width, sr.height / rows / tex.height);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(tw + 1f, th + 1f);          // +1 px: no hairline gaps between tiles
                    Vector2 to = new Vector2((cx + 0.5f) * tw - size.x * 0.5f, (cy + 0.5f) * th - size.y * 0.5f);
                    float ang = (float)(rng.NextDouble() * Math.PI * 2.0), dist = 380f + (float)rng.NextDouble() * 620f;
                    Vector2 centre = new Vector2(cx - (columns - 1) * 0.5f, cy - (rows - 1) * 0.5f);
                    tiles[cy * columns + cx] = new Tile
                    {
                        rt = rt, img = img, to = to,
                        from = to + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist,
                        rotFrom = ((float)rng.NextDouble() - 0.5f) * 540f,
                        scaleFrom = 0.2f + (float)rng.NextDouble() * 0.5f,
                        delay = Mathf.Clamp01(centre.magnitude / (new Vector2(columns, rows).magnitude * 0.5f)) * 0.38f + (float)rng.NextDouble() * 0.06f,
                    };
                }
        }

        private void Update()
        {
            if (Done || tiles == null) return;
            if (SkipRequested()) { Finish(); return; }
            float t = (Time.unscaledTime - startedAt) / duration;
            foreach (Tile tile in tiles)
            {
                float k = Mathf.Clamp01((t - tile.delay) / 0.55f);
                float e = 1f - Mathf.Pow(1f - k, 3f);                         // ease-out cubic
                float snap = k < 1f ? 1f : 0f;
                tile.rt.anchoredPosition = Vector2.LerpUnclamped(tile.from, tile.to, e);
                tile.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(tile.rotFrom, 0f, e) * snap);
                tile.rt.localScale = Vector3.one * Mathf.Lerp(tile.scaleFrom, 1f, e);
                tile.img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
            }
            if (sweep && t > 0.78f && t < 1.05f)
            {
                sweep.gameObject.SetActive(true);
                float s = Mathf.InverseLerp(0.78f, 1.05f, t);
                sweep.anchoredPosition = new Vector2(Mathf.Lerp(-stage.rect.width * 0.62f, stage.rect.width * 0.62f, s), 0f);
            }
            else if (sweep) sweep.gameObject.SetActive(false);
            if (t >= 1f + holdSeconds / duration) Finish();
        }

        private static bool SkipRequested()
        {
            Pointer p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame) return true;
            Keyboard k = Keyboard.current;
            return k != null && k.anyKey.wasPressedThisFrame;
        }

        /// <summary>Snaps the logo into place (if it is not yet) and ends the intro.</summary>
        public void Skip() => Finish();

        private void Finish()
        {
            if (Done) return;
            Done = true;
            if (tiles != null)
                foreach (Tile tile in tiles)
                {
                    tile.rt.anchoredPosition = tile.to; tile.rt.localRotation = Quaternion.identity; tile.rt.localScale = Vector3.one;
                    tile.img.color = Color.white;
                }
            if (sweep) sweep.gameObject.SetActive(false);
            Finished?.Invoke();
        }
    }
}
