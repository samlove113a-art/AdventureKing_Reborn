using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Original 9-slice UI skin, painted procedurally (signed distances, 3x3 supersampled, no external art).
    /// One frame FAMILY per job instead of one gold frame copied everywhere:
    ///   auth_card     midnight glass, double gold hairline, leaf corners          (login / register)
    ///   btn_*         glossy gem buttons: gold (primary), jade, royal, slate, crimson, white (Google), azure (Facebook), ghost
    ///   field         recessed ink well with inner shadow                          (text inputs)
    ///   hall_panel    violet dusk glass, silver brackets                           (hero hall panels)
    ///   hall_card     steel-blue enamel tile, silver rim                           (class cards / slots)
    ///   hud_plate     navy glass plate with a slanted tail and a gold top edge     (player frame)
    ///   bar_track / bar_hp / bar_mp   recessed groove + glossy ruby / sapphire fills
    ///   purse_gold    bronze coin-rim purse;  purse_gem  faceted crystal-rim purse
    ///   minimap_frame bronze bezel with rivets
    ///   parchment     warm scroll with burnt edge and ink rule                     (NPC dialogue)
    ///   shop_window   violet velvet with gold fleur corners; shop_tab(_on), chip(_on), item_cell
    ///   popup_card    navy card with a gilded crest line;  settings_card  steel slate with silver rivets
    ///   ribbon        teal banner with forked ends                                 (map name / speaker)
    /// Output: Assets/Art/UI/Skin/*.png (Single sprites with borders set).
    /// </summary>
    public static class FrameArt
    {
        public const string Dir = "Assets/Art/UI/Skin";

        public static Sprite Get(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        // ------------------------------------------------------------------ painting core
        /// <summary>Per-pixel context: p = position from the centre (px, y up), hs = half size, v = 0 bottom .. 1 top.</summary>
        private struct Px { public Vector2 p, hs; public float v, u; }

        private static float RRect(Vector2 p, Vector2 hs, float r)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - hs + Vector2.one * r;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
        }
        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float Diamond(Vector2 p, Vector2 c, float r) => Mathf.Abs(p.x - c.x) + Mathf.Abs(p.y - c.y) - r;

        private static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return Color.clear;
            Color c = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            c.a = a;
            return c;
        }
        private static Color A(Color c, float a) { c.a = a; return c; }
        private static Color V(Color lo, Color hi, float v) => Color.Lerp(lo, hi, v);

        private static void Bake(string name, int w, int h, Vector4 border, Func<Px, Color> shade, bool mips = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            var hs = new Vector2(w * 0.5f, h * 0.5f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            var q = new Px { p = new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f) - hs, hs = hs };
                            q.v = (q.p.y + hs.y) / h; q.u = (q.p.x + hs.x) / w;
                            Color c = shade(q);
                            acc.r += c.r * c.a; acc.g += c.g * c.a; acc.b += c.b * c.a; acc.a += c.a;
                        }
                    px[y * w + x] = acc.a > 0f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a / 9f) : Color.clear;
                }
            t.SetPixels(px);
            t.Apply();
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spriteBorder = border;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = mips;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // crisp hairlines
            ti.SaveAndReimport();
        }

        /// <summary>Line of width w centred at depth `at` inside the outline (d = signed distance, negative inside).</summary>
        private static bool Line(float d, float at, float w) => d <= -at + w * 0.5f && d >= -at - w * 0.5f;

        private static readonly Color GoldDk = new Color(0.36f, 0.22f, 0.06f), Gold = new Color(0.88f, 0.66f, 0.27f), GoldHi = new Color(1f, 0.92f, 0.62f);
        private static readonly Color Silver = new Color(0.78f, 0.82f, 0.92f), SilverDk = new Color(0.3f, 0.33f, 0.42f);

        /// <summary>Metallic band: dark edge, bright crest in the middle, lit more at the top.</summary>
        private static Color Metal(float d, float from, float width, Color dk, Color mid, Color hi, float v)
        {
            float t = Mathf.Clamp01((-d - from) / width);
            Color c = t < 0.2f ? Color.Lerp(dk, mid, t / 0.2f) : t < 0.55f ? Color.Lerp(mid, hi, (t - 0.2f) / 0.35f) : Color.Lerp(hi, mid * 0.85f, (t - 0.55f) / 0.45f);
            return Color.Lerp(c * 0.78f, c, v);
        }

        [MenuItem("AdventureKing/Art/Bake UI Frame Skins")]
        public static void BuildMenu() => BuildAll();

        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ families
        public static void BuildAll()
        {
            AuthCard();
            Buttons();
            Field();
            HallPanel();
            HallCard();
            HudPlate();
            Bars();
            Purses();
            Minimap();
            Parchment();
            Shop();
            Popup();
            Settings();
            Ribbon();
            AssetDatabase.SaveAssets();
        }

        private static void AuthCard()
        {
            const int S = 256; const float R = 34f;
            Bake("auth_card", S, S, new Vector4(70, 70, 70, 70), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, R);
                if (d > 0f) return Color.clear;
                Color c = A(V(new Color(0.05f, 0.07f, 0.14f), new Color(0.11f, 0.14f, 0.27f), q.v), 0.9f);
                if (-d < 14f) c = Over(c, new Color(0.02f, 0.03f, 0.07f, 0.35f * (1f - -d / 14f))); // soft inner vignette
                if (-d < 1.6f) return new Color(0.12f, 0.07f, 0.02f, 0.95f);                          // outer ink line
                if (-d < 5f) return A(Metal(d, 1.6f, 3.4f, GoldDk, Gold, GoldHi, q.v), 1f);           // gold band
                if (Line(d, 12f, 1.2f)) c = Over(c, A(Gold, 0.55f));                                   // inner hairline
                // leaf (rhombus) ornaments at each inner corner + a small dot beside it
                Vector2 a = new Vector2(Mathf.Abs(q.p.x), Mathf.Abs(q.p.y)) - (q.hs - Vector2.one * 24f);
                if (Diamond(a, Vector2.zero, 7.5f) < 0f) c = Over(c, A(Color.Lerp(Gold, GoldHi, q.v), 1f));
                if (Diamond(a, Vector2.zero, 3.4f) < 0f) c = Over(c, new Color(0.35f, 0.9f, 0.85f, 1f));
                if (Circle(a, new Vector2(16f, 0f), 2.2f) < 0f || Circle(a, new Vector2(0f, 16f), 2.2f) < 0f) c = Over(c, A(Gold, 0.9f));
                return c;
            });
        }

        /// <summary>Gem button: outline, coloured body with a soft top gloss and a darker bottom lip.</summary>
        private static void Gem(string name, Color lo, Color hi, Color rim, Color rimHi, float alpha = 1f)
        {
            const int W = 192, H = 96; const float R = 30f;
            Bake(name, W, H, new Vector4(40, 38, 40, 38), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, R);
                if (d > 0f) return Color.clear;
                if (-d < 2f) return A(rim, alpha);
                if (-d < 4f) return A(Color.Lerp(rimHi, rim, 1f - q.v), alpha);
                Color c = V(lo, hi, Mathf.SmoothStep(0f, 1f, q.v));
                float lip = q.p.y + q.hs.y;              // px from the bottom
                if (lip < 12f) c = Color.Lerp(c * 0.72f, c, lip / 12f);
                // gloss: an ellipse in the upper half
                Vector2 g = new Vector2(q.p.x / (q.hs.x - 14f), (q.p.y - q.hs.y * 0.42f) / (q.hs.y * 0.42f));
                if (g.sqrMagnitude < 1f) c = Color.Lerp(c, Color.white, 0.22f * (1f - g.sqrMagnitude));
                c.a = alpha;
                return c;
            });
        }

        private static void Buttons()
        {
            Gem("btn_gold", new Color(0.78f, 0.47f, 0.12f), new Color(1f, 0.84f, 0.42f), new Color(0.32f, 0.18f, 0.04f), new Color(1f, 0.95f, 0.72f));
            Gem("btn_jade", new Color(0.05f, 0.4f, 0.36f), new Color(0.26f, 0.76f, 0.6f), new Color(0.02f, 0.17f, 0.15f), new Color(0.7f, 1f, 0.88f));
            Gem("btn_royal", new Color(0.2f, 0.2f, 0.5f), new Color(0.42f, 0.46f, 0.88f), new Color(0.08f, 0.07f, 0.22f), new Color(0.78f, 0.82f, 1f));
            Gem("btn_slate", new Color(0.13f, 0.16f, 0.25f), new Color(0.27f, 0.32f, 0.46f), new Color(0.05f, 0.06f, 0.11f), new Color(0.66f, 0.72f, 0.86f));
            Gem("btn_crimson", new Color(0.5f, 0.1f, 0.12f), new Color(0.9f, 0.32f, 0.3f), new Color(0.22f, 0.03f, 0.04f), new Color(1f, 0.75f, 0.7f));
            Gem("btn_white", new Color(0.85f, 0.87f, 0.91f), new Color(1f, 1f, 1f), new Color(0.48f, 0.51f, 0.58f), new Color(1f, 1f, 1f));
            Gem("btn_azure", new Color(0.08f, 0.3f, 0.72f), new Color(0.3f, 0.56f, 1f), new Color(0.03f, 0.12f, 0.35f), new Color(0.75f, 0.86f, 1f));
            // ghost: no body, a thin light outline (text links / toggles)
            Bake("btn_ghost", 192, 96, new Vector4(40, 38, 40, 38), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 30f);
                if (d > 0f) return Color.clear;
                if (-d < 1.6f) return new Color(0.95f, 0.9f, 0.78f, 0.55f);
                return new Color(0.05f, 0.07f, 0.14f, 0.28f);
            });
        }

        private static void Field()
        {
            Bake("field", 128, 64, new Vector4(24, 22, 24, 22), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 16f);
                if (d > 0f) return Color.clear;
                if (-d < 1.4f) return q.v < 0.5f ? new Color(0.62f, 0.68f, 0.82f, 0.55f) : new Color(0.01f, 0.01f, 0.03f, 0.9f);
                Color c = new Color(0.04f, 0.05f, 0.1f, 0.86f);
                float top = q.hs.y - q.p.y;                 // px from the top: inner shadow
                if (top < 10f) c = Color.Lerp(new Color(0f, 0f, 0.02f, 0.95f), c, top / 10f);
                return c;
            });
        }

        private static void HallPanel()
        {
            Bake("hall_panel", 256, 256, new Vector4(56, 56, 56, 56), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 26f);
                if (d > 0f) return Color.clear;
                Color c = A(V(new Color(0.07f, 0.05f, 0.15f), new Color(0.15f, 0.11f, 0.27f), q.v), 0.84f);
                if (-d < 1.4f) return new Color(0.03f, 0.02f, 0.07f, 0.95f);
                if (-d < 3.6f) return A(Metal(d, 1.4f, 2.2f, SilverDk, Silver, Color.white, q.v), 1f);
                if (Line(d, 9f, 1f)) c = Over(c, new Color(0.75f, 0.68f, 1f, 0.35f));
                // silver L-brackets in the corners
                Vector2 a = new Vector2(Mathf.Abs(q.p.x), Mathf.Abs(q.p.y)) - (q.hs - Vector2.one * 16f);
                bool br = (a.x > -26f && a.x < 0f && a.y > -3f && a.y < 0f) || (a.y > -26f && a.y < 0f && a.x > -3f && a.x < 0f);
                if (br) c = Over(c, A(Color.Lerp(Silver, Color.white, q.v), 1f));
                if (Circle(a, Vector2.one * -1.5f, 3.2f) < 0f) c = Over(c, new Color(0.72f, 0.55f, 1f, 1f));
                return c;
            });
        }

        private static void HallCard()
        {
            Bake("hall_card", 128, 128, new Vector4(30, 30, 30, 30), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 20f);
                if (d > 0f) return Color.clear;
                if (-d < 1.4f) return new Color(0.02f, 0.03f, 0.06f, 0.95f);
                if (-d < 3f) return A(Color.Lerp(SilverDk, Silver, q.v), 0.95f);
                Color c = V(new Color(0.1f, 0.13f, 0.22f), new Color(0.2f, 0.25f, 0.38f), q.v);
                if (q.v > 0.7f) c = Color.Lerp(c, Color.white, 0.07f * (q.v - 0.7f) / 0.3f);
                c.a = 0.96f;
                return c;
            });
            // selected: bright gold rim, warm inner glow, lifted body
            Bake("hall_card_on", 128, 128, new Vector4(30, 30, 30, 30), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 20f);
                if (d > 0f) return Color.clear;
                if (-d < 1.4f) return new Color(0.2f, 0.11f, 0.02f, 1f);
                if (-d < 4.4f) return A(Metal(d, 1.4f, 3f, GoldDk, Gold, GoldHi, q.v), 1f);
                Color c = V(new Color(0.16f, 0.17f, 0.3f), new Color(0.3f, 0.3f, 0.48f), q.v);
                if (-d < 14f) c = Color.Lerp(new Color(1f, 0.78f, 0.36f), c, Mathf.SmoothStep(0f, 1f, (-d - 4.4f) / 9.6f) * 0.65f + 0.35f);
                c.a = 0.98f;
                return c;
            });
        }

        private static void HudPlate()
        {
            // navy glass plate; the right end is cut on a slant (the cut lives inside the right border)
            const int W = 256, H = 128;
            Bake("hud_plate", W, H, new Vector4(36, 30, 72, 30), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 18f);
                float slant = (q.p.x - (q.hs.x - 6f)) + (q.hs.y - q.p.y) * 0.42f; // cut the top-right to bottom-right slope
                d = Mathf.Max(d, slant);
                if (d > 0f) return Color.clear;
                Color c = A(V(new Color(0.03f, 0.05f, 0.11f), new Color(0.09f, 0.13f, 0.25f), q.v), 0.86f);
                if (-d < 1.4f) return new Color(0.01f, 0.02f, 0.05f, 0.9f);
                float top = q.hs.y - 2f - q.p.y;
                if (top < 4.5f && -d >= 1.4f) return A(Metal(-top, 0f, 4.5f, GoldDk, Gold, GoldHi, 1f), 1f);   // gold top edge
                float bot = q.p.y + q.hs.y - 2f;
                if (bot < 2.6f) c = Over(c, new Color(0.3f, 0.85f, 0.85f, 0.75f));                              // teal underline
                return c;
            });
        }

        private static void Bars()
        {
            Bake("bar_track", 128, 32, new Vector4(16, 14, 16, 14), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 14f);
                if (d > 0f) return Color.clear;
                if (-d < 1.3f) return q.v < 0.5f ? new Color(0.7f, 0.74f, 0.86f, 0.5f) : new Color(0f, 0f, 0f, 0.95f);
                Color c = new Color(0.02f, 0.02f, 0.05f, 0.9f);
                float top = q.hs.y - q.p.y;
                if (top < 8f) c = Color.Lerp(new Color(0f, 0f, 0f, 1f), c, top / 8f);
                return c;
            });
            void Fill(string name, Color lo, Color mid, Color hi)
            {
                // full-width capsule, drawn Filled-horizontal over the track (no stretching of the round ends at fill 1)
                Bake(name, 320, 32, Vector4.zero, q =>
                {
                    float d = RRect(q.p, q.hs - Vector2.one, 15f);
                    if (d > 0f) return Color.clear;
                    Color c = q.v < 0.5f ? Color.Lerp(lo, mid, q.v / 0.5f) : Color.Lerp(mid, hi, (q.v - 0.5f) / 0.5f);
                    if (q.v > 0.62f && q.v < 0.84f) c = Color.Lerp(c, Color.white, 0.32f * Mathf.Sin((q.v - 0.62f) / 0.22f * Mathf.PI)); // gloss streak
                    if (-d < 1.2f) c *= 0.7f;
                    c.a = 1f;
                    return c;
                });
            }
            Fill("bar_hp", new Color(0.45f, 0.03f, 0.08f), new Color(0.86f, 0.14f, 0.18f), new Color(1f, 0.45f, 0.4f));
            Fill("bar_mp", new Color(0.04f, 0.15f, 0.5f), new Color(0.16f, 0.45f, 0.95f), new Color(0.5f, 0.8f, 1f));
        }

        private static void Purses()
        {
            const int W = 192, H = 64;
            // gold: warm brown leather body, bronze-gold double rim, rivet dots at the ends
            Bake("purse_gold", W, H, new Vector4(36, 28, 36, 28), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 28f);
                if (d > 0f) return Color.clear;
                if (-d < 1.4f) return new Color(0.16f, 0.08f, 0.01f, 0.95f);
                if (-d < 4.4f) return A(Metal(d, 1.4f, 3f, GoldDk, new Color(0.8f, 0.55f, 0.22f), GoldHi, q.v), 1f);
                Color c = A(V(new Color(0.16f, 0.09f, 0.04f), new Color(0.3f, 0.19f, 0.08f), q.v), 0.92f);
                if (Line(d, 7f, 1f)) c = Over(c, new Color(0.95f, 0.7f, 0.3f, 0.45f));
                float ex = Mathf.Abs(q.p.x) - (q.hs.x - 14f);
                if (Circle(new Vector2(ex, q.p.y), Vector2.zero, 2.6f) < 0f && Mathf.Abs(q.p.x) > q.hs.x - 20f) c = Over(c, A(GoldHi, 1f));
                return c;
            });
            // diamond: deep indigo body, crystal rim with alternating facets and a sparkle
            Bake("purse_gem", W, H, new Vector4(36, 28, 36, 28), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 28f);
                if (d > 0f) return Color.clear;
                if (-d < 1.4f) return new Color(0.02f, 0.04f, 0.16f, 0.95f);
                if (-d < 4.6f)
                {
                    float ang = Mathf.Atan2(q.p.y, q.p.x * 0.35f);
                    float facet = Mathf.Repeat(ang * 6f / Mathf.PI, 1f) < 0.5f ? 1f : 0.72f;
                    Color cr = Color.Lerp(new Color(0.25f, 0.6f, 0.95f), new Color(0.8f, 1f, 1f), q.v) * facet;
                    cr.a = 1f;
                    return cr;
                }
                Color c = A(V(new Color(0.05f, 0.05f, 0.2f), new Color(0.14f, 0.12f, 0.38f), q.v), 0.92f);
                if (Line(d, 7.2f, 1f)) c = Over(c, new Color(0.55f, 0.9f, 1f, 0.45f));
                // sparkle star near the right end
                Vector2 s = q.p - new Vector2(q.hs.x - 18f, q.hs.y - 18f);
                float star = Mathf.Min(Mathf.Abs(s.x) * 4f + Mathf.Abs(s.y), Mathf.Abs(s.y) * 4f + Mathf.Abs(s.x));
                if (star < 7f) c = Over(c, new Color(0.9f, 1f, 1f, 1f - star / 7f));
                return c;
            });
        }

        private static void Minimap()
        {
            Bake("minimap_frame", 256, 160, new Vector4(48, 44, 48, 44), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 24f);
                if (d > 0f) return Color.clear;
                if (-d < 1.6f) return new Color(0.12f, 0.06f, 0.02f, 0.95f);
                if (-d < 9f)
                {
                    Color b = Metal(d, 1.6f, 7.4f, new Color(0.3f, 0.17f, 0.07f), new Color(0.66f, 0.42f, 0.2f), new Color(0.95f, 0.76f, 0.46f), q.v);
                    Vector2 a = new Vector2(Mathf.Abs(q.p.x), Mathf.Abs(q.p.y)) - (q.hs - Vector2.one * 13f);
                    if (Circle(a, Vector2.zero, 3.6f) < 0f) b = Color.Lerp(GoldHi, new Color(0.5f, 0.3f, 0.1f), Circle(a, new Vector2(-1f, 1f), 3.6f) + 1f > 2f ? 0.6f : 0f);
                    b.a = 1f;
                    return b;
                }
                if (-d < 10.5f) return new Color(0.04f, 0.03f, 0.02f, 0.95f);
                Color c = A(V(new Color(0.04f, 0.1f, 0.13f), new Color(0.08f, 0.18f, 0.22f), q.v), 0.82f);
                float top = q.hs.y - 10.5f - q.p.y;
                if (top < 8f) c = Color.Lerp(new Color(0f, 0f, 0f, 0.9f), c, top / 8f);
                return c;
            });
        }

        private static float Hash(Vector2 p) => Mathf.Repeat(Mathf.Sin(Vector2.Dot(p, new Vector2(12.9898f, 78.233f))) * 43758.5453f, 1f);

        private static void Parchment()
        {
            Bake("parchment", 256, 256, new Vector4(48, 48, 48, 48), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 3f, 14f);
                // ragged edge
                d += (Hash(new Vector2(Mathf.Floor(q.p.x / 3f), Mathf.Floor(q.p.y / 3f))) - 0.5f) * 1.6f;
                if (d > 0f) return Color.clear;
                Color c = V(new Color(0.9f, 0.81f, 0.62f), new Color(0.98f, 0.93f, 0.8f), q.v);
                c = Color.Lerp(c, new Color(0.85f, 0.74f, 0.55f), Hash(new Vector2(Mathf.Floor(q.p.x), Mathf.Floor(q.p.y))) * 0.08f); // fibre
                if (-d < 12f) c = Color.Lerp(new Color(0.6f, 0.42f, 0.22f), c, Mathf.SmoothStep(0f, 1f, -d / 12f));     // burnt edge
                if (Line(d, 17f, 1.4f)) c = Color.Lerp(c, new Color(0.45f, 0.28f, 0.12f), 0.75f);                       // ink rule
                c.a = 0.98f;
                return c;
            });
        }

        private static void Shop()
        {
            Bake("shop_window", 256, 256, new Vector4(64, 64, 64, 64), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 28f);
                if (d > 0f) return Color.clear;
                Color c = A(V(new Color(0.07f, 0.04f, 0.14f), new Color(0.16f, 0.09f, 0.28f), q.v), 0.97f);
                if (-d < 1.6f) return new Color(0.08f, 0.04f, 0.02f, 0.95f);
                if (-d < 4.4f) return A(Metal(d, 1.6f, 2.8f, GoldDk, Gold, GoldHi, q.v), 1f);
                if (Line(d, 8.5f, 1f)) c = Over(c, A(Gold, 0.7f));
                // fleur: three petals in each corner
                Vector2 a = new Vector2(Mathf.Abs(q.p.x), Mathf.Abs(q.p.y)) - (q.hs - Vector2.one * 22f);
                float petals = Mathf.Min(Circle(a, new Vector2(-1f, -1f), 5f), Mathf.Min(Circle(a, new Vector2(-12f, 0f), 3.4f), Circle(a, new Vector2(0f, -12f), 3.4f)));
                if (petals < 0f) c = Over(c, A(Color.Lerp(Gold, GoldHi, q.v), 1f));
                if (Circle(a, new Vector2(-1f, -1f), 2f) < 0f) c = Over(c, new Color(0.85f, 0.3f, 0.55f, 1f));
                return c;
            });
            void Tab(string name, Color lo, Color hi, Color edge)
            {
                Bake(name, 128, 64, new Vector4(26, 10, 26, 24), q =>
                {
                    // rounded on top only
                    Vector2 p = q.p; p.y -= 12f;
                    float d = RRect(p, new Vector2(q.hs.x - 1f, q.hs.y + 11f), 18f);
                    if (d > 0f || q.p.y < -q.hs.y + 1f) return Color.clear;
                    if (-d < 1.6f) return A(edge, 1f);
                    Color c = V(lo, hi, q.v); c.a = 0.97f;
                    return c;
                });
            }
            Tab("shop_tab", new Color(0.1f, 0.07f, 0.2f), new Color(0.2f, 0.15f, 0.36f), new Color(0.55f, 0.45f, 0.8f));
            Tab("shop_tab_on", new Color(0.62f, 0.38f, 0.12f), new Color(1f, 0.83f, 0.42f), new Color(1f, 0.95f, 0.75f));
            Bake("chip", 96, 48, new Vector4(22, 22, 22, 22), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 22f);
                if (d > 0f) return Color.clear;
                if (-d < 1.6f) return new Color(0.78f, 0.7f, 1f, 0.55f);
                return new Color(0.1f, 0.07f, 0.22f, 0.85f);
            });
            Bake("chip_on", 96, 48, new Vector4(22, 22, 22, 22), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 22f);
                if (d > 0f) return Color.clear;
                if (-d < 1.6f) return new Color(1f, 0.95f, 0.75f, 1f);
                Color c = V(new Color(0.72f, 0.45f, 0.14f), new Color(1f, 0.82f, 0.4f), q.v); c.a = 1f;
                return c;
            });
            void Cell(string name, Color rim, float rimW, Color glow)
            {
                Bake(name, 128, 160, new Vector4(28, 28, 28, 28), q =>
                {
                    float d = RRect(q.p, q.hs - Vector2.one, 18f);
                    if (d > 0f) return Color.clear;
                    if (-d < rimW) return A(Color.Lerp(rim * 0.75f, rim, q.v), rim.a);
                    Color c = A(V(new Color(0.08f, 0.06f, 0.18f), new Color(0.17f, 0.14f, 0.32f), q.v), 0.95f);
                    if (-d < rimW + 8f) c = Over(c, A(glow, glow.a * (1f - (-d - rimW) / 8f)));
                    if (q.v > 0.86f) c = Over(c, new Color(1f, 1f, 1f, 0.06f));
                    return c;
                });
            }
            Cell("item_cell", new Color(0.62f, 0.56f, 0.88f, 0.7f), 1.5f, new Color(0f, 0f, 0f, 0f));
            Cell("item_cell_on", new Color(1f, 0.86f, 0.5f, 1f), 3f, new Color(1f, 0.8f, 0.4f, 0.35f)); // selected: gold rim + inner glow
            Cell("item_cell_set", new Color(0.86f, 0.55f, 1f, 0.9f), 2f, new Color(0.7f, 0.4f, 1f, 0.18f)); // full sets: orchid rim
            // fitting-room bezel: clear inside, slim lavender line + gold corner ticks
            Bake("stage_bezel", 128, 128, new Vector4(30, 30, 30, 30), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one, 20f);
                if (d > 0f) return Color.clear;
                if (-d < 1.5f) return new Color(0.04f, 0.02f, 0.08f, 0.9f);
                if (-d < 3.5f) return new Color(0.7f, 0.62f, 0.95f, 0.9f);
                Vector2 a = new Vector2(Mathf.Abs(q.p.x), Mathf.Abs(q.p.y)) - (q.hs - Vector2.one * 9f);
                bool tick = (a.x > -16f && a.x < 0f && a.y > -2.5f && a.y < 0f) || (a.y > -16f && a.y < 0f && a.x > -2.5f && a.x < 0f);
                if (tick) return A(GoldHi, 1f);
                return Color.clear;
            });
        }

        private static void Popup()
        {
            Bake("popup_card", 256, 256, new Vector4(60, 60, 60, 72), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 22f);
                if (d > 0f) return Color.clear;
                Color c = A(V(new Color(0.05f, 0.07f, 0.15f), new Color(0.12f, 0.16f, 0.3f), q.v), 0.96f);
                if (-d < 1.5f) return new Color(0.08f, 0.05f, 0.02f, 0.95f);
                if (-d < 4f) return A(Metal(d, 1.5f, 2.5f, GoldDk, Gold, GoldHi, q.v), 1f);
                // crest: gilded line under the title band with a centre-less diamond at each end
                float top = q.hs.y - q.p.y;
                if (top > 58f && top < 60f && Mathf.Abs(q.p.x) < q.hs.x - 14f) c = Over(c, A(Gold, 0.8f));
                Vector2 e = new Vector2(Mathf.Abs(q.p.x) - (q.hs.x - 14f), top - 59f);
                if (Diamond(e, Vector2.zero, 5f) < 0f) c = Over(c, A(GoldHi, 1f));
                if (top < 58f) c = Over(c, new Color(0.3f, 0.36f, 0.7f, 0.12f));        // lighter title band
                return c;
            });
        }

        private static void Settings()
        {
            Bake("settings_card", 256, 256, new Vector4(52, 52, 52, 52), q =>
            {
                float d = RRect(q.p, q.hs - Vector2.one * 2f, 16f);
                if (d > 0f) return Color.clear;
                Color c = A(V(new Color(0.09f, 0.1f, 0.13f), new Color(0.18f, 0.2f, 0.26f), q.v), 0.97f);
                if (-d < 1.4f) return new Color(0.02f, 0.02f, 0.03f, 0.95f);
                if (-d < 5f) return A(Metal(d, 1.4f, 3.6f, SilverDk, Silver, Color.white, q.v), 1f);
                // brushed-steel streaks + rivets
                c = Color.Lerp(c, c * 1.15f, Hash(new Vector2(0f, Mathf.Floor(q.p.y / 2f))) * 0.4f);
                Vector2 a = new Vector2(Mathf.Abs(q.p.x), Mathf.Abs(q.p.y)) - (q.hs - Vector2.one * 15f);
                float rv = Circle(a, Vector2.zero, 4.5f);
                if (rv < 0f) c = Color.Lerp(Color.white, SilverDk, Mathf.Clamp01((a.y + a.x) / 9f + 0.5f));
                c.a = Mathf.Max(c.a, rv < 0f ? 1f : c.a);
                return c;
            });
        }

        private static void Ribbon()
        {
            Bake("ribbon", 256, 96, new Vector4(70, 20, 70, 20), q =>
            {
                float x = Mathf.Abs(q.p.x), y = q.p.y;
                float body = Mathf.Max(Mathf.Abs(y) - 30f, x - (q.hs.x - 2f));
                float notch = (q.hs.x - 2f - x) - (30f - Mathf.Abs(y)) * 0.7f;          // forked tail cut
                float d = Mathf.Max(body, -notch);
                if (d > 0f) return Color.clear;
                Color c = V(new Color(0.04f, 0.3f, 0.34f), new Color(0.16f, 0.58f, 0.6f), q.v);
                float e = 30f - Mathf.Abs(y);
                if (e < 1.6f) return new Color(0.03f, 0.08f, 0.08f, 1f);
                if (e < 4.4f) return A(Metal(-e, 1.6f, 2.8f, GoldDk, Gold, GoldHi, q.v), 1f);
                if (x > q.hs.x - 52f) c *= 0.8f;                                             // folded ends darker
                c.a = 1f;
                return c;
            });
        }
    }
}
