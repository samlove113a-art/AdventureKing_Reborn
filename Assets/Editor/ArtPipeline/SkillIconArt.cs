using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Original skill / action icons, painted procedurally in the editor (signed-distance shapes, 2x2 supersampled,
    /// 512 px, transparent outside the disc): an elemental radial backdrop with light rays and sparkles, then the
    /// symbol layers with soft glow, ink outline and a top-left bevel. No text or numbers are baked in: cooldown
    /// sweeps, seconds, MP warnings and the AUTO state are live UI on top. Plus two round gold frames (normal and the
    /// more ornate one for skill 3) drawn as separate sprites so the same icon works in any frame.
    /// STATUS: original in-engine art; NEEDS a commercial art pass (hand-painted) before release.
    /// Output: Assets/Art/UI/Skills/*.png
    /// </summary>
    public static class SkillIconArt
    {
        public const string Dir = "Assets/Art/UI/Skills";
        private const int S = 512;

        public static readonly string[,] SkillIcons =
        {
            { "skill_sword_wind_slash", "skill_sword_whirl", "skill_sword_heaven_blade" },        // Swordsman
            { "skill_mage_fireball", "skill_mage_ice_storm", "skill_mage_meteor" },               // Mage
            { "skill_assassin_dagger", "skill_assassin_clone", "skill_assassin_combo" },          // Assassin
            { "skill_archer_pierce", "skill_archer_rain", "skill_archer_phoenix" },               // Archer
        };

        public static Sprite Get(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        // ------------------------------------------------------------------ SDF toolkit (-1..1, y up)
        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float Box(Vector2 p, Vector2 c, Vector2 h, float round = 0f)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h + Vector2.one * round;
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }
        private static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - w;
        }
        /// <summary>Tapered stroke: width wa at a, wb at b.</summary>
        private static float Taper(Vector2 p, Vector2 a, Vector2 b, float wa, float wb)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - Mathf.Lerp(wa, wb, h);
        }
        private static float Poly(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]), s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
        private static Vector2[] Star(int n, float ro, float ri, float rot = 90f, Vector2 c = default)
        {
            var v = new Vector2[n * 2];
            for (int i = 0; i < n * 2; i++)
            {
                float a = (rot + i * 180f / n) * Mathf.Deg2Rad, r = i % 2 == 0 ? ro : ri;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }
        /// <summary>Arc stroke of radius r around c between angles a0..a1 (degrees, CCW), width tapering w0->w1.</summary>
        private static float Arc(Vector2 p, Vector2 c, float r, float a0, float a1, float w0, float w1)
        {
            Vector2 d = p - c;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float span = Mathf.Repeat(a1 - a0, 360f), rel = Mathf.Repeat(ang - a0, 360f);
            if (rel <= span)
            {
                float k = rel / Mathf.Max(0.001f, span);
                return Mathf.Abs(d.magnitude - r) - Mathf.Lerp(w0, w1, k);
            }
            Vector2 pa = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 pb = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min((p - pa).magnitude - w0, (p - pb).magnitude - w1);
        }
        private static Vector2 Rot(Vector2 p, float deg, Vector2 c = default)
        {
            float a = -deg * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            Vector2 q = p - c;
            return new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs) + c;
        }
        private static float Union(params float[] d) { float m = float.MaxValue; foreach (float x in d) m = Mathf.Min(m, x); return m; }

        // ------------------------------------------------------------------ painter
        private sealed class Layer
        {
            public Func<Vector2, float> Sdf;
            public Func<Vector2, Color> Col;
            public float Glow;          // glow radius (0 = none)
            public Color GlowCol;
            public bool Outline = true;
            public bool Bevel = true;
        }

        private static Layer L(Func<Vector2, float> sdf, Func<Vector2, Color> col, float glow = 0f, Color? glowCol = null, bool outline = true, bool bevel = true)
            => new Layer { Sdf = sdf, Col = col, Glow = glow, GlowCol = glowCol ?? Color.white, Outline = outline, Bevel = bevel };

        private static Func<Vector2, Color> V(Color lo, Color hi) => p => Color.Lerp(lo, hi, Mathf.Clamp01((p.y + 0.8f) / 1.6f));
        private static Func<Vector2, Color> Flat(Color c) => _ => c;

        private static readonly Color Ink = new Color(0.06f, 0.04f, 0.08f);
        private static readonly Vector2 LightDir = new Vector2(-0.55f, 0.83f);

        /// <summary>Elemental backdrop: deep edge colour -> bright core, soft light rays, a few sparkles.</summary>
        private static Color Backdrop(Vector2 p, Color edge, Color core, Color rays, int seed)
        {
            float r = p.magnitude;
            Color c = Color.Lerp(core, edge, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(r)));
            float a = Mathf.Atan2(p.y, p.x);
            float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a * 9f + seed), 6f) * Mathf.Clamp01(1f - r) * 0.35f;
            c = Color.Lerp(c, rays, ray);
            // inner rim shadow + top gloss
            c = Color.Lerp(c, Ink, Mathf.Clamp01((r - 0.78f) / 0.2f) * 0.55f);
            float gloss = Mathf.Clamp01(1f - ((p - new Vector2(-0.1f, 0.55f)) / new Vector2(0.75f, 0.32f)).magnitude);
            c = Color.Lerp(c, Color.white, Mathf.SmoothStep(0f, 1f, gloss) * 0.12f);
            // sparkles
            var rng = new System.Random(seed);
            for (int i = 0; i < 7; i++)
            {
                Vector2 sp = new Vector2((float)rng.NextDouble() * 1.4f - 0.7f, (float)rng.NextDouble() * 1.4f - 0.7f);
                float sz = 0.025f + (float)rng.NextDouble() * 0.035f;
                Vector2 d = p - sp;
                float star = Mathf.Clamp01(1f - (Mathf.Abs(d.x) * Mathf.Abs(d.y) / (sz * sz * 0.08f) + d.magnitude / sz) * 0.5f);
                c = Color.Lerp(c, Color.white, star * 0.8f);
            }
            c.a = 1f;
            return c;
        }

        private static void Paint(string name, Func<Vector2, Color> backdrop, params Layer[] layers)
        {
            var px = new Color[S * S];
            const int ss = 2;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 p = new Vector2((x + (sx + 0.5f) / ss) / S * 2f - 1f, (y + (sy + 0.5f) / ss) / S * 2f - 1f);
                            float r = p.magnitude;
                            if (r > 0.985f) continue; // transparent outside the disc (the frame sprite covers the rim)
                            Color c = backdrop(p);
                            foreach (Layer l in layers)
                            {
                                float d = l.Sdf(p);
                                if (l.Glow > 0f && d > 0f)
                                {
                                    float g = Mathf.Exp(-d / l.Glow) * 0.75f;
                                    c = new Color(Mathf.Min(1f, c.r + l.GlowCol.r * g), Mathf.Min(1f, c.g + l.GlowCol.g * g), Mathf.Min(1f, c.b + l.GlowCol.b * g), 1f);
                                }
                                if (l.Outline && d < 0.028f && d >= 0f) c = Color.Lerp(c, Ink, 0.9f);
                                if (d < 0f)
                                {
                                    Color k = l.Col(p);
                                    Color fill = new Color(k.r, k.g, k.b, 1f);
                                    if (l.Bevel)
                                    {
                                        const float e = 0.01f, bevel = 0.07f;
                                        Vector2 n = new Vector2(l.Sdf(p + new Vector2(e, 0)) - l.Sdf(p - new Vector2(e, 0)), l.Sdf(p + new Vector2(0, e)) - l.Sdf(p - new Vector2(0, e)));
                                        if (n.sqrMagnitude > 1e-8f) n.Normalize();
                                        float edge = Mathf.Clamp01(1f + d / bevel), facing = Vector2.Dot(n, LightDir);
                                        fill = facing > 0f ? Color.Lerp(fill, Color.white, facing * edge * 0.5f) : Color.Lerp(fill, Ink, -facing * edge * 0.35f);
                                    }
                                    c = Color.Lerp(c, fill, k.a);
                                    c.a = 1f;
                                }
                            }
                            acc += c;
                        }
                    px[y * S + x] = acc / (ss * ss);
                }
            Save(name, px);
        }

        private static void Save(string name, Color[] px)
        {
            Directory.CreateDirectory(Dir);
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 512;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ palette
        private static readonly Color Steel = new Color(0.82f, 0.88f, 0.96f), SteelDark = new Color(0.45f, 0.52f, 0.62f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.32f), GoldDark = new Color(0.72f, 0.44f, 0.1f);
        private static readonly Color Wind = new Color(0.55f, 1f, 0.95f), Fire = new Color(1f, 0.55f, 0.12f), FireHot = new Color(1f, 0.93f, 0.55f);
        private static readonly Color Ice = new Color(0.72f, 0.92f, 1f), Shadow = new Color(0.62f, 0.42f, 0.95f), Blood = new Color(0.95f, 0.18f, 0.3f);
        private static readonly Color Leaf = new Color(0.55f, 0.95f, 0.45f), Wood = new Color(0.6f, 0.38f, 0.18f);

        private static Layer Sword(Vector2 tip, Vector2 hilt, float width, Color blade, float glow = 0f, Color? glowCol = null)
        {
            Vector2 dir = (tip - hilt).normalized, side = new Vector2(-dir.y, dir.x);
            Vector2 guard = hilt + dir * 0.02f;
            return L(p => Union(
                    Poly(p, hilt + side * width, tip - dir * width * 2.2f + side * width, tip, tip - dir * width * 2.2f - side * width, hilt - side * width), // blade
                    Seg(p, guard + side * width * 3.2f, guard - side * width * 3.2f, width * 0.75f),   // cross guard
                    Seg(p, hilt, hilt - dir * width * 4f, width * 0.6f),                            // grip
                    Circle(p, hilt - dir * width * 4.6f, width * 0.95f)),                            // pommel
                p => Vector2.Dot(p - hilt, dir) < 0.03f ? Gold : Color.Lerp(SteelDark, blade, Mathf.Clamp01(0.5f + Vector2.Dot(p - hilt, side) / (width * 2f))),
                glow, glowCol);
        }

        private static Layer Arrow(Vector2 tip, Vector2 tail, float w, Color shaft, Color head, Color fletch, float glow = 0f, Color? glowCol = null)
        {
            Vector2 dir = (tip - tail).normalized, side = new Vector2(-dir.y, dir.x);
            return L(p => Union(
                    Seg(p, tail, tip - dir * w * 4f, w * 0.45f),
                    Poly(p, tip, tip - dir * w * 5f + side * w * 2.4f, tip - dir * w * 4f, tip - dir * w * 5f - side * w * 2.4f),
                    Poly(p, tail + dir * w * 4f, tail + side * w * 2.2f, tail - dir * w * 0.5f + side * w * 1.6f, tail + dir * w * 1.5f),
                    Poly(p, tail + dir * w * 4f, tail - side * w * 2.2f, tail - dir * w * 0.5f - side * w * 1.6f, tail + dir * w * 1.5f)),
                p => { float along = Vector2.Dot(p - tail, dir) / (tip - tail).magnitude; return along > 0.8f ? head : along < 0.25f ? fletch : shaft; },
                glow, glowCol);
        }

        private static Layer Flask(Vector2 c, float s, Color liquid)
        {
            return L(p => Union(Circle(p, c + new Vector2(0, -0.12f) * s, 0.5f * s), Box(p, c + new Vector2(0, 0.42f) * s, new Vector2(0.15f, 0.2f) * s, 0.04f * s)),
                p => p.y < c.y + 0.02f * s ? Color.Lerp(liquid * 0.65f, liquid, Mathf.Clamp01((p.y - c.y + 0.6f * s) / (0.6f * s))) : new Color(0.85f, 0.95f, 1f));
        }

        [MenuItem("Adventure King/Art/Build Skill Icons")]
        public static void BuildAll()
        {
            // ------------------------------------------------ Kiếm Sĩ (wind + steel, teal)
            Func<Vector2, Color> teal = p => Backdrop(p, new Color(0.03f, 0.18f, 0.28f), new Color(0.2f, 0.75f, 0.85f), Wind, 11);
            Paint("skill_sword_wind_slash", teal,
                L(p => Union(Arc(p, new Vector2(0.25f, -0.25f), 0.78f, 100f, 200f, 0.012f, 0.11f), Arc(p, new Vector2(0.32f, -0.32f), 0.55f, 105f, 195f, 0.01f, 0.08f)),
                    V(new Color(0.45f, 0.95f, 1f), Color.white), 0.12f, Wind, bevel: false),
                Sword(new Vector2(0.55f, 0.55f), new Vector2(-0.25f, -0.25f), 0.085f, Steel, 0.05f, Wind));
            Paint("skill_sword_whirl", teal,
                L(p => Union(Arc(p, Vector2.zero, 0.7f, 20f, 160f, 0.01f, 0.09f), Arc(p, Vector2.zero, 0.7f, 200f, 340f, 0.01f, 0.09f), Arc(p, Vector2.zero, 0.45f, 290f, 70f, 0.01f, 0.07f), Arc(p, Vector2.zero, 0.45f, 110f, 250f, 0.01f, 0.07f)),
                    V(new Color(0.4f, 0.9f, 1f), Color.white), 0.1f, Wind, bevel: false),
                Sword(new Vector2(0.0f, 0.62f), new Vector2(0.0f, -0.25f), 0.08f, Steel, 0.06f, Wind));
            Paint("skill_sword_heaven_blade", p => Backdrop(p, new Color(0.2f, 0.12f, 0.02f), new Color(1f, 0.85f, 0.45f), Color.white, 13),
                L(p => Poly(p, Star(12, 0.82f, 0.42f, 90f, new Vector2(0, 0.25f))), V(new Color(1f, 0.85f, 0.4f, 0.55f), new Color(1f, 1f, 0.85f, 0.55f)), outline: false, bevel: false),
                Sword(new Vector2(0.0f, -0.78f), new Vector2(0.0f, 0.3f), 0.12f, new Color(1f, 0.97f, 0.85f), 0.14f, new Color(1f, 0.9f, 0.5f)));

            // ------------------------------------------------ Pháp Sư (fire / ice / arcane meteor)
            Paint("skill_mage_fireball", p => Backdrop(p, new Color(0.22f, 0.04f, 0.02f), new Color(0.95f, 0.45f, 0.12f), FireHot, 21),
                L(p => Union(Circle(p, new Vector2(0.15f, 0.12f), 0.38f), Poly(p, new Vector2(-0.12f, 0.42f), new Vector2(-0.75f, 0.62f), new Vector2(-0.35f, 0.3f), new Vector2(-0.82f, 0.2f), new Vector2(-0.3f, 0.05f), new Vector2(-0.7f, -0.25f), new Vector2(-0.05f, -0.2f))),
                    p => Color.Lerp(Fire, FireHot, Mathf.Clamp01(1f - (p - new Vector2(0.2f, 0.15f)).magnitude / 0.45f)), 0.16f, Fire),
                L(p => Circle(p, new Vector2(0.2f, 0.15f), 0.17f), Flat(new Color(1f, 1f, 0.85f)), 0.08f, FireHot, outline: false));
            Paint("skill_mage_ice_storm", p => Backdrop(p, new Color(0.03f, 0.1f, 0.3f), new Color(0.45f, 0.75f, 1f), Ice, 23),
                L(p =>
                {
                    float d = float.MaxValue;
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 q = Rot(p, i * 60f);
                        d = Mathf.Min(d, Union(Seg(q, Vector2.zero, new Vector2(0, 0.68f), 0.055f), Seg(q, new Vector2(0, 0.38f), new Vector2(0.17f, 0.54f), 0.04f), Seg(q, new Vector2(0, 0.38f), new Vector2(-0.17f, 0.54f), 0.04f)));
                    }
                    return Mathf.Min(d, Poly(p, Star(6, 0.2f, 0.12f)));
                }, V(Ice, Color.white), 0.12f, new Color(0.6f, 0.85f, 1f)));
            Paint("skill_mage_meteor", p => Backdrop(p, new Color(0.15f, 0.03f, 0.25f), new Color(0.9f, 0.4f, 0.55f), FireHot, 25),
                L(p => Taper(p, new Vector2(-0.1f, -0.1f), new Vector2(0.75f, 0.75f), 0.3f, 0.02f), p => Color.Lerp(FireHot, new Color(1f, 0.35f, 0.15f, 0.9f), Mathf.Clamp01((p.x + p.y) * 0.6f)), 0.15f, Fire, outline: false, bevel: false),
                L(p => Circle(p, new Vector2(-0.22f, -0.22f), 0.36f) + 0.03f * Mathf.Sin(Mathf.Atan2(p.y + 0.22f, p.x + 0.22f) * 7f),
                    p => Color.Lerp(new Color(0.35f, 0.18f, 0.12f), new Color(0.75f, 0.45f, 0.28f), Mathf.Clamp01(0.5f + p.y - p.x)), 0.12f, Fire),
                L(p => Union(Circle(p, new Vector2(-0.3f, -0.12f), 0.07f), Circle(p, new Vector2(-0.12f, -0.33f), 0.05f), Circle(p, new Vector2(-0.33f, -0.36f), 0.04f)), Flat(new Color(1f, 0.6f, 0.2f)), 0.05f, Fire, outline: false));

            // ------------------------------------------------ Sát Thủ (shadow violet, crimson)
            Func<Vector2, Color> violet = p => Backdrop(p, new Color(0.08f, 0.02f, 0.16f), new Color(0.48f, 0.25f, 0.75f), Shadow, 31);
            Paint("skill_assassin_dagger", violet,
                L(p => Union(Seg(p, new Vector2(-0.75f, -0.05f), new Vector2(-0.25f, 0.2f), 0.025f), Seg(p, new Vector2(-0.7f, -0.3f), new Vector2(-0.2f, -0.05f), 0.02f), Seg(p, new Vector2(-0.6f, 0.2f), new Vector2(-0.3f, 0.35f), 0.018f)), Flat(new Color(0.85f, 0.75f, 1f, 0.8f)), 0.05f, Shadow, outline: false, bevel: false),
                Sword(new Vector2(0.62f, 0.45f), new Vector2(-0.05f, 0.08f), 0.075f, new Color(0.9f, 0.88f, 1f), 0.06f, Shadow));
            Paint("skill_assassin_clone", violet,
                L(p => Union(Circle(p - new Vector2(0.28f, 0.05f), new Vector2(0, 0.32f), 0.2f), Poly(p - new Vector2(0.28f, 0.05f), new Vector2(-0.35f, -0.65f), new Vector2(0.35f, -0.65f), new Vector2(0.2f, 0.15f), new Vector2(-0.2f, 0.15f))),
                    Flat(new Color(0.55f, 0.4f, 0.9f, 0.55f)), 0.1f, Shadow, outline: false, bevel: false),
                L(p => Union(Circle(p + new Vector2(0.12f, 0f), new Vector2(0, 0.32f), 0.21f), Poly(p + new Vector2(0.12f, 0f), new Vector2(-0.36f, -0.68f), new Vector2(0.36f, -0.68f), new Vector2(0.21f, 0.14f), new Vector2(-0.21f, 0.14f))),
                    V(new Color(0.12f, 0.06f, 0.22f), new Color(0.36f, 0.22f, 0.55f)), 0.06f, Shadow),
                L(p => Union(Circle(p, new Vector2(-0.19f, 0.34f), 0.035f), Circle(p, new Vector2(-0.05f, 0.34f), 0.035f)), Flat(new Color(1f, 0.3f, 0.45f)), 0.05f, Blood, outline: false));
            Paint("skill_assassin_combo", p => Backdrop(p, new Color(0.16f, 0.01f, 0.06f), new Color(0.65f, 0.12f, 0.3f), Blood, 33),
                L(p => Union(Taper(p, new Vector2(-0.75f, 0.55f), new Vector2(0.75f, -0.45f), 0.01f, 0.07f), Taper(p, new Vector2(-0.7f, -0.6f), new Vector2(0.7f, 0.6f), 0.01f, 0.06f), Taper(p, new Vector2(-0.8f, 0.05f), new Vector2(0.8f, 0.1f), 0.005f, 0.045f)),
                    Flat(new Color(1f, 0.45f, 0.55f)), 0.12f, Blood, outline: false, bevel: false),
                Sword(new Vector2(0.55f, 0.55f), new Vector2(-0.3f, -0.3f), 0.07f, Steel, 0.04f, Blood),
                Sword(new Vector2(-0.55f, 0.55f), new Vector2(0.3f, -0.3f), 0.07f, Steel, 0.04f, Blood));

            // ------------------------------------------------ Cung Thủ (nature green, phoenix gold-red)
            Func<Vector2, Color> green = p => Backdrop(p, new Color(0.03f, 0.18f, 0.08f), new Color(0.45f, 0.85f, 0.4f), Leaf, 41);
            Paint("skill_archer_pierce", green,
                L(p => Union(Seg(p, new Vector2(-0.82f, -0.05f), new Vector2(0.2f, -0.05f), 0.02f), Seg(p, new Vector2(-0.78f, 0.12f), new Vector2(-0.1f, 0.12f), 0.015f), Seg(p, new Vector2(-0.8f, -0.22f), new Vector2(-0.15f, -0.22f), 0.015f)), Flat(new Color(0.85f, 1f, 0.8f, 0.8f)), 0.04f, Leaf, outline: false, bevel: false),
                L(p => Arc(p, new Vector2(0.2f, -0.05f), 0.36f, 0f, 359.9f, 0.04f, 0.04f), Flat(new Color(1f, 0.95f, 0.6f, 0.9f)), 0.08f, Gold, bevel: false),
                Arrow(new Vector2(0.82f, -0.05f), new Vector2(-0.55f, -0.05f), 0.06f, Wood, Steel, new Color(0.3f, 0.85f, 0.4f), 0.05f, Leaf));
            Paint("skill_archer_rain", green,
                Arrow(new Vector2(-0.38f, -0.62f), new Vector2(-0.5f, 0.55f), 0.045f, Wood, Steel, new Color(0.3f, 0.85f, 0.4f), 0.04f, Leaf),
                Arrow(new Vector2(0.02f, -0.75f), new Vector2(-0.1f, 0.6f), 0.05f, Wood, Steel, new Color(0.3f, 0.85f, 0.4f), 0.04f, Leaf),
                Arrow(new Vector2(0.42f, -0.55f), new Vector2(0.3f, 0.65f), 0.045f, Wood, Steel, new Color(0.3f, 0.85f, 0.4f), 0.04f, Leaf),
                L(p => Union(Circle(p, new Vector2(-0.38f, -0.68f), 0.06f), Circle(p, new Vector2(0.02f, -0.8f), 0.06f), Circle(p, new Vector2(0.42f, -0.6f), 0.06f)), Flat(new Color(1f, 1f, 0.8f, 0.9f)), 0.08f, Gold, outline: false, bevel: false));
            Paint("skill_archer_phoenix", p => Backdrop(p, new Color(0.25f, 0.05f, 0.02f), new Color(1f, 0.6f, 0.2f), FireHot, 43),
                L(p =>
                {
                    Vector2 q = new Vector2(Mathf.Abs(p.x), p.y);
                    return Union(Poly(q, new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.55f), new Vector2(0.85f, 0.45f), new Vector2(0.6f, 0.3f), new Vector2(0.8f, 0.15f), new Vector2(0.55f, 0.05f), new Vector2(0.7f, -0.12f), new Vector2(0.35f, -0.12f), new Vector2(0.1f, -0.2f)));
                }, p => Color.Lerp(new Color(0.95f, 0.25f, 0.1f), FireHot, Mathf.Clamp01(p.y + 0.45f)), 0.14f, Fire),
                Arrow(new Vector2(0f, 0.85f), new Vector2(0f, -0.75f), 0.07f, Gold, new Color(1f, 0.97f, 0.8f), new Color(1f, 0.4f, 0.15f), 0.08f, FireHot));

            // ------------------------------------------------ actions
            Paint("action_attack", p => Backdrop(p, new Color(0.22f, 0.05f, 0.03f), new Color(0.95f, 0.45f, 0.2f), FireHot, 51),
                L(p => Poly(p, Star(8, 0.75f, 0.35f, 22.5f)), Flat(new Color(1f, 0.9f, 0.55f, 0.5f)), outline: false, bevel: false),
                Sword(new Vector2(0.58f, 0.58f), new Vector2(-0.28f, -0.28f), 0.1f, Steel, 0.05f, FireHot),
                Sword(new Vector2(-0.58f, 0.58f), new Vector2(0.28f, -0.28f), 0.1f, Steel, 0.05f, FireHot));
            Paint("action_auto", p => Backdrop(p, new Color(0.04f, 0.12f, 0.3f), new Color(0.35f, 0.65f, 1f), Wind, 53),
                L(p => Union(Arc(p, Vector2.zero, 0.55f, 30f, 170f, 0.09f, 0.09f), Poly(p, new Vector2(-0.72f, 0.12f), new Vector2(-0.36f, 0.12f), new Vector2(-0.54f, -0.15f)),
                        Arc(p, Vector2.zero, 0.55f, 210f, 350f, 0.09f, 0.09f), Poly(p, new Vector2(0.72f, -0.12f), new Vector2(0.36f, -0.12f), new Vector2(0.54f, 0.15f))),
                    V(new Color(0.55f, 0.85f, 1f), Color.white), 0.08f, Wind),
                L(p => Poly(p, new Vector2(0.06f, 0.36f), new Vector2(-0.2f, -0.04f), new Vector2(-0.02f, -0.04f), new Vector2(-0.08f, -0.36f), new Vector2(0.2f, 0.06f), new Vector2(0.02f, 0.06f)), V(Gold, new Color(1f, 1f, 0.8f)), 0.08f, Gold));
            Paint("action_pickup", p => Backdrop(p, new Color(0.2f, 0.12f, 0.02f), new Color(0.95f, 0.75f, 0.3f), FireHot, 55),
                L(p => Union(Circle(p, new Vector2(-0.22f, -0.25f), 0.3f), Circle(p, new Vector2(0.22f, -0.32f), 0.26f), Circle(p, new Vector2(0.05f, 0.08f), 0.3f)),
                    p => Color.Lerp(GoldDark, new Color(1f, 0.92f, 0.5f), Mathf.Clamp01(p.y + 0.6f)), 0.08f, Gold),
                L(p => Union(Poly(p, Star(5, 0.13f, 0.055f, 90f, new Vector2(0.05f, 0.08f))), Poly(p, Star(5, 0.12f, 0.05f, 90f, new Vector2(-0.22f, -0.25f)))), Flat(new Color(1f, 1f, 0.85f)), outline: false),
                L(p => Union(Seg(p, new Vector2(0.45f, 0.62f), new Vector2(0.45f, 0.25f), 0.05f), Poly(p, new Vector2(0.3f, 0.32f), new Vector2(0.6f, 0.32f), new Vector2(0.45f, 0.12f))), Flat(Color.white), 0.06f, Color.white));
            Paint("action_potion_hp", p => Backdrop(p, new Color(0.2f, 0.02f, 0.05f), new Color(0.85f, 0.25f, 0.3f), new Color(1f, 0.7f, 0.7f), 57),
                Flask(new Vector2(0f, -0.05f), 1.2f, new Color(1f, 0.15f, 0.25f)),
                L(p => Box(p, new Vector2(0f, 0.56f), new Vector2(0.14f, 0.07f), 0.03f), Flat(new Color(0.7f, 0.45f, 0.25f))),
                L(p => Union(Box(p, new Vector2(0f, -0.2f), new Vector2(0.06f, 0.18f), 0.02f), Box(p, new Vector2(0f, -0.2f), new Vector2(0.18f, 0.06f), 0.02f)), Flat(new Color(1f, 0.95f, 0.95f)), outline: false));
            Paint("action_potion_mp", p => Backdrop(p, new Color(0.02f, 0.06f, 0.24f), new Color(0.3f, 0.5f, 0.95f), new Color(0.7f, 0.85f, 1f), 59),
                Flask(new Vector2(0f, -0.05f), 1.2f, new Color(0.2f, 0.45f, 1f)),
                L(p => Box(p, new Vector2(0f, 0.56f), new Vector2(0.14f, 0.07f), 0.03f), Flat(new Color(0.7f, 0.45f, 0.25f))),
                L(p => Poly(p, Star(4, 0.2f, 0.07f, 90f, new Vector2(0f, -0.2f))), Flat(new Color(0.9f, 0.97f, 1f)), outline: false));

            // ------------------------------------------------ items (bag cells) + bag button
            Func<Vector2, Color> stone = p => Backdrop(p, new Color(0.1f, 0.1f, 0.16f), new Color(0.42f, 0.45f, 0.6f), new Color(0.85f, 0.9f, 1f), 61);
            Paint("item_weapon_sword", stone, Sword(new Vector2(0.55f, 0.55f), new Vector2(-0.3f, -0.3f), 0.1f, Steel, 0.04f, Color.white));
            Paint("item_weapon_staff", stone,
                L(p => Seg(p, new Vector2(-0.5f, -0.6f), new Vector2(0.3f, 0.35f), 0.07f), V(Wood * 0.8f, Wood * 1.3f)),
                L(p => Circle(p, new Vector2(0.38f, 0.45f), 0.2f), V(new Color(0.4f, 0.6f, 1f), new Color(0.85f, 0.95f, 1f)), 0.12f, new Color(0.5f, 0.75f, 1f)),
                L(p => Arc(p, new Vector2(0.38f, 0.45f), 0.27f, 200f, 70f, 0.035f, 0.035f), V(GoldDark, Gold)));
            Paint("item_weapon_dagger", stone,
                Sword(new Vector2(0.5f, 0.35f), new Vector2(-0.15f, -0.05f), 0.075f, Steel),
                Sword(new Vector2(0.35f, -0.55f), new Vector2(-0.3f, -0.15f), 0.075f, Steel));
            Paint("item_weapon_bow", stone,
                L(p => Arc(p, new Vector2(-0.55f, 0f), 0.85f, -55f, 55f, 0.06f, 0.06f), V(Wood * 0.8f, Wood * 1.35f)),
                L(p => Seg(p, new Vector2(-0.07f, -0.7f), new Vector2(-0.07f, 0.7f), 0.012f), Flat(new Color(0.95f, 0.92f, 0.85f)), outline: false, bevel: false),
                Arrow(new Vector2(0.62f, 0f), new Vector2(-0.5f, 0f), 0.045f, Wood, Steel, new Color(0.3f, 0.85f, 0.4f)));
            Paint("item_armor_vest", stone,
                L(p => Poly(p, new Vector2(-0.3f, 0.62f), new Vector2(-0.1f, 0.5f), new Vector2(0.1f, 0.5f), new Vector2(0.3f, 0.62f), new Vector2(0.6f, 0.4f), new Vector2(0.48f, 0.05f), new Vector2(0.36f, 0.12f), new Vector2(0.36f, -0.62f), new Vector2(-0.36f, -0.62f), new Vector2(-0.36f, 0.12f), new Vector2(-0.48f, 0.05f), new Vector2(-0.6f, 0.4f)),
                    V(new Color(0.45f, 0.3f, 0.18f), new Color(0.75f, 0.55f, 0.35f))),
                L(p => Union(Seg(p, new Vector2(0f, 0.48f), new Vector2(0f, -0.6f), 0.025f), Box(p, new Vector2(0f, -0.25f), new Vector2(0.36f, 0.05f))), V(GoldDark, Gold)));
            Paint("item_charm", stone,
                L(p => Arc(p, new Vector2(0f, 0.35f), 0.28f, 200f, 340f, 0.03f, 0.03f), V(GoldDark, Gold)),
                L(p => Poly(p, new Vector2(-0.3f, 0.12f), new Vector2(0.3f, 0.12f), new Vector2(0.42f, -0.45f), new Vector2(-0.42f, -0.45f)), V(GoldDark, new Color(1f, 0.9f, 0.55f)), 0.08f, Gold),
                L(p => Circle(p, new Vector2(0f, -0.55f), 0.09f), V(new Color(0.2f, 0.6f, 1f), new Color(0.8f, 0.95f, 1f))));
            Paint("item_ticket", p => Backdrop(p, new Color(0.05f, 0.12f, 0.3f), new Color(0.4f, 0.65f, 1f), Wind, 65),
                L(p => Box(p, new Vector2(0f, 0f), new Vector2(0.55f, 0.32f), 0.06f), V(new Color(0.88f, 0.78f, 0.55f), new Color(1f, 0.96f, 0.82f))),
                L(p => Union(Circle(p, new Vector2(-0.55f, 0f), 0.14f), Circle(p, new Vector2(0.55f, 0f), 0.14f)), V(new Color(0.6f, 0.4f, 0.2f), new Color(0.85f, 0.6f, 0.35f))),
                L(p => Box(p, new Vector2(0f, 0f), new Vector2(0.55f, 0.06f)), Flat(new Color(0.85f, 0.2f, 0.25f))),
                L(p => Arc(p, new Vector2(0.05f, 0.02f), 0.2f, 0f, 359.9f, 0.04f, 0.04f), V(new Color(0.3f, 0.6f, 1f), new Color(0.75f, 0.95f, 1f)), 0.08f, Wind, bevel: false));
            Func<Vector2, Color> meadow = p => Backdrop(p, new Color(0.08f, 0.2f, 0.08f), new Color(0.55f, 0.85f, 0.45f), new Color(1f, 1f, 0.85f), 67);
            Paint("item_petfood", meadow,
                L(p => Union(Circle(p, new Vector2(0f, -0.2f), 0.48f)), V(new Color(0.75f, 0.45f, 0.25f), new Color(0.95f, 0.7f, 0.45f))), // bowl
                L(p => Box(p, new Vector2(0f, -0.02f), new Vector2(0.5f, 0.08f), 0.06f), V(new Color(0.55f, 0.3f, 0.15f), new Color(0.8f, 0.5f, 0.3f))),
                L(p => Union(Circle(p, new Vector2(-0.22f, 0.14f), 0.12f), Circle(p, new Vector2(0.02f, 0.2f), 0.13f), Circle(p, new Vector2(0.25f, 0.12f), 0.11f), Circle(p, new Vector2(-0.08f, 0.04f), 0.1f), Circle(p, new Vector2(0.14f, 0.02f), 0.1f)),
                    V(new Color(0.85f, 0.6f, 0.25f), new Color(1f, 0.85f, 0.5f))));
            Paint("item_petfood_feast", p => Backdrop(p, new Color(0.25f, 0.06f, 0.15f), new Color(1f, 0.6f, 0.75f), new Color(1f, 0.95f, 0.85f), 69),
                L(p => Box(p, new Vector2(0f, -0.25f), new Vector2(0.5f, 0.22f), 0.08f), V(new Color(0.9f, 0.65f, 0.4f), new Color(1f, 0.85f, 0.6f))),
                L(p => Box(p, new Vector2(0f, 0.04f), new Vector2(0.44f, 0.12f), 0.1f), V(new Color(1f, 0.75f, 0.85f), Color.white)),
                L(p => Circle(p, new Vector2(0f, 0.3f), 0.13f), V(new Color(0.85f, 0.1f, 0.2f), new Color(1f, 0.45f, 0.5f)), 0.06f, new Color(1f, 0.5f, 0.6f)),
                L(p => Poly(p, Star(4, 0.12f, 0.04f, 45f, new Vector2(0.38f, 0.45f))), Flat(new Color(1f, 1f, 0.85f, 0.9f)), outline: false));
            Paint("action_bag", p => Backdrop(p, new Color(0.18f, 0.1f, 0.04f), new Color(0.75f, 0.52f, 0.28f), new Color(1f, 0.9f, 0.7f), 63),
                L(p => Union(Circle(p, new Vector2(0f, -0.18f), 0.5f), Box(p, new Vector2(0f, 0.35f), new Vector2(0.22f, 0.12f), 0.05f)), V(new Color(0.5f, 0.3f, 0.15f), new Color(0.85f, 0.6f, 0.35f))),
                L(p => Box(p, new Vector2(0f, 0.2f), new Vector2(0.3f, 0.05f), 0.03f), V(GoldDark, Gold)),
                L(p => Poly(p, Star(5, 0.18f, 0.08f, 90f, new Vector2(0f, -0.2f))), V(GoldDark, new Color(1f, 0.95f, 0.6f))));

            Frames();
            AssetDatabase.SaveAssets();
            Debug.Log("[SKILL ICONS] built " + (SkillIcons.Length + 5) + " icons + 2 frames in " + Dir);
        }

        /// <summary>Round gold frames (transparent centre): normal, and the ornate one for skill 3 (gems + wings).</summary>
        private static void Frames()
        {
            Color[] Frame(bool ornate)
            {
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        Color acc = Color.clear;
                        for (int sy = 0; sy < 2; sy++)
                            for (int sx = 0; sx < 2; sx++)
                            {
                                Vector2 p = new Vector2((x + (sx + 0.5f) / 2f) / S * 2f - 1f, (y + (sy + 0.5f) / 2f) / S * 2f - 1f);
                                float r = p.magnitude;
                                Color c = Color.clear;
                                float outer = ornate ? 0.92f : 0.96f, inner = ornate ? 0.8f : 0.86f;
                                if (ornate)
                                {
                                    // wing flourishes left/right + top crest gem
                                    Vector2 q = new Vector2(Mathf.Abs(p.x), p.y);
                                    float wing = Poly(q, new Vector2(0.8f, 0.25f), new Vector2(0.99f, 0.42f), new Vector2(0.95f, 0.2f), new Vector2(0.99f, 0.05f), new Vector2(0.86f, -0.05f));
                                    if (wing < 0f) c = Color.Lerp(GoldDark, Gold, Mathf.Clamp01(p.y + 0.5f));
                                    else if (wing < 0.02f) c = Ink;
                                }
                                if (r < outer + 0.02f && r > inner - 0.02f) c = Ink;
                                if (r < outer && r > inner)
                                {
                                    Vector2 n = p / Mathf.Max(r, 1e-4f);
                                    float facing = Vector2.Dot(n, LightDir), across = Mathf.InverseLerp(outer, inner, r);
                                    Color g = Color.Lerp(GoldDark, Gold, 0.5f + 0.45f * facing * (1f - across * 0.5f));
                                    g = Color.Lerp(g, Color.white, Mathf.Clamp01(facing) * Mathf.Sin(across * Mathf.PI) * 0.4f);
                                    if (Mathf.Abs(r - (outer + inner) / 2f) < 0.006f) g = Color.Lerp(g, GoldDark * 0.6f, 0.7f);
                                    c = g; c.a = 1f;
                                }
                                if (ornate)
                                {
                                    for (int i = 0; i < 4; i++) // four gems on the ring
                                    {
                                        float a = (45f + i * 90f) * Mathf.Deg2Rad;
                                        Vector2 gc = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ((outer + inner) / 2f);
                                        float d = (p - gc).magnitude;
                                        if (d < 0.065f) c = Ink;
                                        if (d < 0.05f) c = Color.Lerp(new Color(0.7f, 0.05f, 0.25f), new Color(1f, 0.6f, 0.75f), Mathf.Clamp01(1f - (p - gc - new Vector2(-0.015f, 0.015f)).magnitude / 0.05f));
                                    }
                                    Vector2 top = new Vector2(0f, outer + 0.0f);
                                    float crest = Poly(p, top + new Vector2(-0.11f, -0.05f), top + new Vector2(0f, 0.075f), top + new Vector2(0.11f, -0.05f), top + new Vector2(0f, -0.12f));
                                    if (crest < 0.015f) c = Ink;
                                    if (crest < 0f) c = Color.Lerp(new Color(0.15f, 0.4f, 0.95f), new Color(0.7f, 0.9f, 1f), Mathf.Clamp01((p.y - top.y + 0.1f) * 5f));
                                }
                                acc += c;
                            }
                        px[y * S + x] = acc / 4f;
                    }
                return px;
            }
            Save("frame_skill", Frame(false));
            Save("frame_skill_ultimate", Frame(true));
        }

        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
