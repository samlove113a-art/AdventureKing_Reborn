using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Fitting-room stage dressing (UI only, cheap): a soft warm spotlight behind the hero that breathes slowly,
    /// and a few golden motes drifting up from the dais. Textures are generated once at runtime.
    /// </summary>
    public sealed class ShopStageFx : MonoBehaviour
    {
        private RawImage spot;
        private RectTransform[] motes;
        private float[] seeds;
        private static Texture2D glowTex, moteTex;

        public static Texture2D Radial(int size, float power)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude / h);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - d, power)));
                }
            t.Apply();
            return t;
        }

        /// <summary>Builds the spotlight (behind <paramref name="before"/>) and the motes (in front of it).</summary>
        public void Build(RectTransform stage, Transform before)
        {
            if (!glowTex) glowTex = Radial(128, 1.6f);
            if (!moteTex) moteTex = Radial(32, 2.2f);
            spot = new GameObject("StageSpotlight", typeof(RectTransform)).AddComponent<RawImage>();
            spot.gameObject.layer = 5;
            spot.transform.SetParent(stage, false);
            spot.transform.SetSiblingIndex(before.GetSiblingIndex());
            var sr = spot.rectTransform;
            sr.anchorMin = new Vector2(0.05f, 0.2f); sr.anchorMax = new Vector2(0.95f, 0.95f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;
            spot.texture = glowTex;
            spot.color = new Color(1f, 0.9f, 0.65f, 0.55f);
            spot.raycastTarget = false;
            motes = new RectTransform[14];
            seeds = new float[motes.Length];
            for (int i = 0; i < motes.Length; i++)
            {
                var m = new GameObject("Mote", typeof(RectTransform)).AddComponent<RawImage>();
                m.gameObject.layer = 5;
                m.transform.SetParent(stage, false);
                m.texture = moteTex;
                m.color = new Color(1f, 0.85f, 0.45f, 0f);
                m.raycastTarget = false;
                motes[i] = m.rectTransform;
                motes[i].anchorMin = motes[i].anchorMax = new Vector2(0.5f, 0.22f);
                float s = 8f + (i % 4) * 4f;
                motes[i].sizeDelta = new Vector2(s, s);
                seeds[i] = i * 0.61803f;
            }
        }

        private void Update()
        {
            if (!spot) return;
            float t = Time.unscaledTime;
            spot.color = new Color(1f, 0.9f, 0.65f, 0.45f + 0.12f * Mathf.Sin(t * 1.3f));
            for (int i = 0; i < motes.Length; i++)
            {
                float life = Mathf.Repeat(t * 0.16f + seeds[i], 1f);           // 0..1 rise
                float x = (Mathf.Repeat(seeds[i] * 7.3f, 1f) - 0.5f) * 300f + Mathf.Sin(t * 0.9f + i) * 14f;
                motes[i].anchoredPosition = new Vector2(x, life * 520f);
                var img = motes[i].GetComponent<RawImage>();
                img.color = new Color(1f, 0.85f, 0.45f, Mathf.Sin(life * Mathf.PI) * 0.85f);
            }
        }
    }
}
