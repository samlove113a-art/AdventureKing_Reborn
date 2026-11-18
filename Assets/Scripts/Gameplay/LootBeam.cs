using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// The pillar of light over a high-rarity item drop (purple, orange, red): shoots up from the ground where the item comes to rest,
    /// pulses, throws sparks upward and fades when the item is picked up or expires. Flat colour per tier (no rainbow), hard-edged sprites,
    /// one SpriteRenderer for the pillar, one for its core (orange+), one ground ring (red) and a few spark sprites — cheap on phones.
    /// Not a child of the drop: the drop spins in 3D, the pillar must always face the camera.
    /// </summary>
    public sealed class LootBeam : MonoBehaviour
    {
        private const float Height = 8f, Width = 0.95f;
        private static Material material;

        private GoldDropView owner;
        private int tier;
        private Color color;
        private SpriteRenderer body, core, floor;
        private SpriteRenderer[] sparks;
        private float[] sparkPhase;
        private float age, fade = 1f;
        private Vector3 anchor;

        /// <summary>Creates the pillar for <paramref name="drop"/> when its rarity is high; returns null for lower tiers.</summary>
        public static LootBeam Attach(GoldDropView drop, int rarityTier)
        {
            if (!drop || rarityTier < LootRarity.High) return null;
            var go = new GameObject("LootBeam_t" + rarityTier);
            var beam = go.AddComponent<LootBeam>();
            beam.owner = drop; beam.tier = rarityTier; beam.color = LootRarity.ColorOf(rarityTier);
            beam.Build();
            return beam;
        }

        public int Tier => tier;
        public bool Visible => body && body.enabled && age > 0.02f;

        private sealed class TierSprites { public Sprite Pillar, Core, Ring, Dot; }
        private static readonly System.Collections.Generic.Dictionary<int, TierSprites> sprites = new System.Collections.Generic.Dictionary<int, TierSprites>();
        private static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private TierSprites art;

        /// <summary>The tier's colour is baked into its sprites (flat bands, hard edges); only the alpha is animated, through the material's _Color.</summary>
        private static TierSprites SpritesFor(int tier)
        {
            if (material == null)
            {
                Shader sh = Shader.Find("AdventureKing/PixelSprite");
                material = new Material(sh ? sh : Shader.Find("Sprites/Default")) { name = "LootBeam" };
                if (material.HasProperty("_MaxLuma")) material.SetFloat("_MaxLuma", 1f);   // a glow, not a painted colour
            }
            if (sprites.TryGetValue(tier, out TierSprites cached) && cached.Pillar) return cached;
            Color c = LootRarity.ColorOf(tier), light = Color.Lerp(c, Color.white, 0.6f), spark = Color.Lerp(c, Color.white, 0.5f);
            var s = new TierSprites();
            s.Pillar = Pillar(c); s.Core = Pillar(light);
            // ground ring (flattened ellipse outline)
            var r = new Texture2D(64, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "loot_ring_t" + tier };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Mathf.Sqrt(Mathf.Pow((x - 31.5f) / 31f, 2f) + Mathf.Pow((y - 15.5f) / 15f, 2f));
                    r.SetPixel(x, y, new Color(c.r, c.g, c.b, d > 0.82f && d < 1f ? 1f : d <= 0.82f && d > 0.7f ? 0.35f : 0f));
                }
            r.Apply(false, true);
            s.Ring = Sprite.Create(r, new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f), 32f);
            var d4 = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "loot_dot_t" + tier };
            for (int i = 0; i < 16; i++) d4.SetPixel(i % 4, i / 4, spark);
            d4.Apply(false, true);
            s.Dot = Sprite.Create(d4, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
            sprites[tier] = s;
            return s;
        }

        /// <summary>Pillar: 8 px wide, 128 px tall; alpha = three flat bands across the width (cel look) and a vertical fade toward the top.</summary>
        private static Sprite Pillar(Color c)
        {
            var p = new Texture2D(8, 128, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "loot_pillar" };
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 8; x++)
                {
                    float edge = Mathf.Abs(x - 3.5f) / 3.5f;                       // 0 centre .. 1 edge
                    float a = edge < 0.35f ? 1f : edge < 0.75f ? 0.62f : 0.3f;
                    float v = y / 127f;
                    a *= v < 0.04f ? v / 0.04f : Mathf.Pow(1f - Mathf.Clamp01((v - 0.35f) / 0.65f), 1.3f);
                    p.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            p.Apply(false, false);
            return Sprite.Create(p, new Rect(0, 0, 8, 128), new Vector2(0.5f, 0f), 8f);
        }
        private SpriteRenderer Sprite2(string name, Sprite s, int order, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sharedMaterial = material; sr.sortingOrder = order;
            return sr;
        }

        private void Build()
        {
            art = SpritesFor(tier);
            body = Sprite2("Pillar", art.Pillar, 2, Color.white);
            if (tier >= 3) core = Sprite2("Core", art.Core, 3, Color.white);
            if (tier >= 4) floor = Sprite2("Ring", art.Ring, 1, Color.white);
            int n = tier >= 4 ? 7 : tier >= 3 ? 5 : 3;
            sparks = new SpriteRenderer[n]; sparkPhase = new float[n];
            for (int i = 0; i < n; i++) { sparks[i] = Sprite2("Spark" + i, art.Dot, 4, Color.white); sparkPhase[i] = i / (float)n; }
            Place();
            Apply(0f);
        }

        private void Place()
        {
            if (owner) anchor = new Vector3(owner.Ground.x + 0.6f, owner.Ground.y + 0.05f, 0.1f);
            transform.position = anchor;
            transform.rotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            bool gone = !owner || owner.Collecting || owner.Expired;
            if (gone) fade -= Time.deltaTime * 5f; else Place();
            if (fade <= 0f) { Destroy(gameObject); return; }
            // starts when the item lands (the drop is still in the air for the first half second): grows in 0.3 s
            float grow = owner && owner.Landed ? Mathf.Clamp01((age - 0.5f) / 0.3f) : 0f;
            Apply(grow);
        }

        private void Apply(float grow)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            float alpha = fade * (0.6f + 0.25f * pulse) * Mathf.Clamp01(grow * 3f);
            float h = Height * (tier >= 4 ? 1.15f : 1f) * Mathf.SmoothStep(0f, 1f, grow);
            float w = Width * (tier >= 3 ? 1.2f : 1f) * (0.92f + 0.12f * pulse);
            body.transform.localScale = new Vector3(w, h / (128f / 8f), 1f);        // sprite is 16 world units tall at scale 1 (128 px / 8 ppu)
            Tint(body, alpha);
            if (core) { core.transform.localScale = new Vector3(w * 0.45f, h / (128f / 8f), 1f); Tint(core, Mathf.Min(1f, alpha * 1.3f)); }
            if (floor) { floor.transform.localPosition = Vector3.zero; floor.transform.localScale = new Vector3(1.05f + 0.15f * pulse, 1.05f + 0.15f * pulse, 1f); Tint(floor, alpha); }
            for (int i = 0; i < sparks.Length; i++)
            {
                float t = Mathf.Repeat(Time.time * (0.45f + 0.1f * i) + sparkPhase[i], 1f);
                float x = Mathf.Sin((t + i) * 6.1f) * w * 0.38f;
                sparks[i].transform.localPosition = new Vector3(x, t * Mathf.Min(h, 5.5f), -0.01f);
                sparks[i].transform.localScale = Vector3.one * (0.7f - 0.5f * t);
                Tint(sparks[i], alpha * (1f - t) * Mathf.Clamp01(grow * 3f));
            }
        }

        private static void Tint(SpriteRenderer sr, float a)
        {
            sr.GetPropertyBlock(block);
            block.SetColor(ColorId, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            sr.SetPropertyBlock(block);
        }
    }
}
