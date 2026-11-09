using System.Collections.Generic;
using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// "ADVENTURE KING / REBORN" title logo, made entirely in-engine (no paid tools, no other game's IP):
    /// a jewelled crown and sunburst drawn procedurally, gold-gradient lettering in Be Vietnam Pro ExtraBold with a
    /// thick dark outline, and a crimson ribbon carrying "REBORN". Rendered off-screen to a transparent PNG.
    /// STATUS: PROPOSAL — waiting for the owner's approval (the owner's SAM logo stays the studio splash).
    /// </summary>
    public static class LogoArt
    {
        public const string OutPath = "Assets/Art/Branding/AK_TitleLogo_PROPOSAL.png";
        private const string PartsDir = "Assets/Art/Branding/LogoParts";
        private const int W = 1600, H = 900;

        [MenuItem("Adventure King/Art/Build Title Logo (proposal)")]
        public static void Build()
        {
            Directory.CreateDirectory(PartsDir);
            Sprite crown = Part("logo_crown", 512, 400, Crown);
            Sprite ribbon = Part("logo_ribbon", 1024, 220, Ribbon);
            Sprite glow = Part("logo_glow", 512, 512, Glow);
            Sprite spark = Part("logo_sparkle", 128, 128, Sparkle);
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-ExtraBold.ttf");

            var prev = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("LogoCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;

            var canvasGo = new GameObject("LogoCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            Transform root = canvasGo.transform;

            Img(root, "Glow", glow, new Vector2(0f, 60f), new Vector2(1500f, 900f), new Color(1f, 0.85f, 0.45f, 0.55f));
            Img(root, "Crown", crown, new Vector2(0f, 290f), new Vector2(330f, 258f), Color.white);
            Word(root, "ADVENTURE", font, 170, new Vector2(0f, 105f), new Color(1f, 0.98f, 0.82f), new Color(1f, 0.82f, 0.32f), new Color(0.88f, 0.46f, 0.1f));
            Word(root, "KING", font, 270, new Vector2(0f, -115f), new Color(1f, 0.96f, 0.7f), new Color(1f, 0.74f, 0.2f), new Color(0.8f, 0.3f, 0.06f));
            Img(root, "Ribbon", ribbon, new Vector2(0f, -305f), new Vector2(900f, 194f), Color.white);
            Text reborn = Word(root, "REBORN", font, 96, new Vector2(0f, -296f), new Color(1f, 1f, 1f), new Color(1f, 0.95f, 0.85f), new Color(1f, 0.84f, 0.62f), 0.3f);
            reborn.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 140f);
            foreach (var (pos, size) in new[] { (new Vector2(-560f, 190f), 90f), (new Vector2(590f, 120f), 70f), (new Vector2(-470f, -170f), 56f), (new Vector2(520f, -190f), 84f), (new Vector2(180f, 330f), 50f) })
                Img(root, "Sparkle", spark, pos, new Vector2(size, size), Color.white);

            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.Render(); // second pass: font atlas populated on the first
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            UnPremultiply(tex);
            File.WriteAllBytes(OutPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(OutPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            Debug.Log("[LOGO] wrote " + OutPath);
            if (!string.IsNullOrEmpty(prev)) EditorSceneManager.OpenScene(prev);
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static Sprite Sprite() => AssetDatabase.LoadAssetAtPath<Sprite>(OutPath);

        // ------------------------------------------------------------------ composition helpers
        private static Image Img(Transform parent, string name, Sprite s, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.preserveAspect = true;
            return img;
        }

        private static Text Word(Transform parent, string text, Font font, int size, Vector2 pos, Color top, Color mid, Color bottom, float outlineScale = 1f)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1560f, size * 1.4f);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Color.white;
            go.AddComponent<UiVerticalGradient>().Set(top, mid, bottom);
            // inner warm rim, thick dark-brown outline, deep drop shadow (order: gradient first, effects after)
            Outline(go, new Color(0.55f, 0.24f, 0.05f, 1f), 3f * outlineScale);
            Outline(go, new Color(0.22f, 0.09f, 0.03f, 1f), 7f * outlineScale);
            Outline(go, new Color(0.22f, 0.09f, 0.03f, 1f), 11f * outlineScale);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.08f, 0.03f, 0.01f, 0.65f);
            sh.effectDistance = new Vector2(0f, -16f * outlineScale);
            return t;
        }

        private static void Outline(GameObject go, Color c, float d)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(d, -d);
            o.useGraphicAlpha = true;
        }

        private static void UnPremultiply(Texture2D t)
        {
            Color[] px = t.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                if (c.a > 0.001f && c.a < 0.999f) { c.r = Mathf.Clamp01(c.r / c.a); c.g = Mathf.Clamp01(c.g / c.a); c.b = Mathf.Clamp01(c.b / c.a); }
                px[i] = c;
            }
            t.SetPixels(px);
            t.Apply();
        }

        // ------------------------------------------------------------------ procedural parts (SDF, anti-aliased)
        private delegate Color Painter(float x, float y, float px); // x,y in texture pixels (y up), px = 1 pixel

        private static Sprite Part(string name, int w, int h, Painter paint)
        {
            string path = $"{PartsDir}/{name}.png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = paint(x + 0.5f, y + 0.5f, 1f);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        /// <summary>Signed distance to a polygon (negative inside).</summary>
        private static float Poly(Vector2 p, IList<Vector2> v)
        {
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i++)
            {
                d = Mathf.Min(d, Seg(p, v[j], v[i]));
                if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x) inside = !inside;
            }
            return inside ? -d : d;
        }

        private static float Cover(float sd) => Mathf.Clamp01(0.5f - sd); // 1 px AA band

        private static Color Over(Color dst, Color src, float a)
        {
            src.a *= a;
            float outA = src.a + dst.a * (1f - src.a);
            if (outA < 0.0001f) return new Color(0, 0, 0, 0);
            Color c = (src * src.a + dst * dst.a * (1f - src.a)) / outA;
            c.a = outA;
            return c;
        }

        private static readonly Color Ink = new Color(0.22f, 0.09f, 0.03f, 1f);

        private static Color Crown(float x, float y, float _)
        {
            const float S = 512f / 1f;
            Vector2 p = new Vector2(x, y);
            Vector2 P(float u, float v) => new Vector2(u * S, v * 400f);
            var body = new List<Vector2> { P(0.12f, 0.12f), P(0.88f, 0.12f), P(0.9f, 0.34f), P(0.97f, 0.8f), P(0.72f, 0.5f), P(0.5f, 0.93f), P(0.28f, 0.5f), P(0.03f, 0.8f), P(0.1f, 0.34f) };
            float sd = Poly(p, body);
            Color c = new Color(0, 0, 0, 0);
            c = Over(c, Ink, Cover(sd - 9f)); // outline
            float k = Mathf.InverseLerp(0.12f * 400f, 0.93f * 400f, y);
            Color gold = Color.Lerp(new Color(0.85f, 0.42f, 0.08f), new Color(1f, 0.93f, 0.55f), Mathf.SmoothStep(0f, 1f, k));
            float shine = Mathf.Exp(-Mathf.Pow((x / S - 0.38f) * 7f, 2f)) * 0.35f;
            gold = Color.Lerp(gold, Color.white, shine);
            c = Over(c, gold, Cover(sd));
            // band
            float band = Poly(p, new List<Vector2> { P(0.1f, 0.1f), P(0.9f, 0.1f), P(0.91f, 0.3f), P(0.09f, 0.3f) });
            c = Over(c, Ink, Cover(band - 6f) * 0.9f);
            Color bandCol = Color.Lerp(new Color(0.78f, 0.36f, 0.07f), new Color(1f, 0.82f, 0.36f), Mathf.InverseLerp(40f, 120f, y));
            c = Over(c, bandCol, Cover(band));
            // gems: tips + band
            (Vector2 at, float r, Color col)[] gems =
            {
                (P(0.5f, 0.93f), 30f, new Color(0.95f, 0.18f, 0.3f)), (P(0.03f, 0.8f), 24f, new Color(0.2f, 0.75f, 0.95f)), (P(0.97f, 0.8f), 24f, new Color(0.2f, 0.75f, 0.95f)),
                (P(0.5f, 0.2f), 30f, new Color(0.25f, 0.85f, 0.55f)), (P(0.27f, 0.2f), 20f, new Color(0.95f, 0.18f, 0.3f)), (P(0.73f, 0.2f), 20f, new Color(0.95f, 0.18f, 0.3f)),
            };
            foreach (var g in gems)
            {
                float d = (p - g.at).magnitude - g.r;
                c = Over(c, Ink, Cover(d - 6f));
                Vector2 hl = g.at + new Vector2(-g.r * 0.35f, g.r * 0.35f);
                Color gc = Color.Lerp(g.col * 0.7f, Color.Lerp(g.col, Color.white, 0.55f), Mathf.Clamp01(1f - (p - hl).magnitude / (g.r * 1.4f)));
                gc.a = 1f;
                c = Over(c, gc, Cover(d));
                c = Over(c, Color.white, Cover((p - hl).magnitude - g.r * 0.22f) * 0.9f);
            }
            return c;
        }

        private static Color Ribbon(float x, float y, float _)
        {
            Vector2 p = new Vector2(x, y);
            float w = 1024f, h = 220f;
            float arc(float u) => Mathf.Sin(u * Mathf.PI) * 18f; // gentle upward bow
            Color c = new Color(0, 0, 0, 0);
            // tails (behind, darker) with a V notch
            foreach (int side in new[] { -1, 1 })
            {
                float cx = side < 0 ? 0f : w;
                var tail = new List<Vector2>
                {
                    new Vector2(cx - side * 0f, 30f), new Vector2(cx - side * 210f, 30f), new Vector2(cx - side * 210f, 150f), new Vector2(cx - side * 0f, 150f), new Vector2(cx - side * 62f, 90f),
                };
                float sd = Poly(p, tail);
                c = Over(c, Ink, Cover(sd - 7f));
                c = Over(c, Color.Lerp(new Color(0.42f, 0.05f, 0.1f), new Color(0.6f, 0.1f, 0.14f), y / h), Cover(sd));
            }
            // main band
            float u0 = Mathf.Clamp01((x - 130f) / (w - 260f));
            float lift = arc(u0);
            var band = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float u = i / 16f; band.Add(new Vector2(130f + u * (w - 260f), 52f + arc(u))); }
            for (int i = 16; i >= 0; i--) { float u = i / 16f; band.Add(new Vector2(130f + u * (w - 260f), 182f + arc(u))); }
            float bd = Poly(p, band);
            c = Over(c, Ink, Cover(bd - 8f));
            float k = Mathf.InverseLerp(52f + lift, 182f + lift, y);
            Color red = Color.Lerp(new Color(0.62f, 0.07f, 0.12f), new Color(0.93f, 0.25f, 0.24f), Mathf.SmoothStep(0f, 1f, k));
            c = Over(c, red, Cover(bd));
            // gold trim lines
            float trimTop = Mathf.Abs(y - (170f + lift)) - 3.5f, trimBottom = Mathf.Abs(y - (64f + lift)) - 3.5f;
            float inside = Cover(bd + 6f);
            c = Over(c, new Color(1f, 0.82f, 0.38f), Cover(trimTop) * inside);
            c = Over(c, new Color(1f, 0.82f, 0.38f), Cover(trimBottom) * inside);
            return c;
        }

        private static Color Glow(float x, float y, float _)
        {
            Vector2 d = new Vector2(x - 256f, y - 256f) / 256f;
            float r = d.magnitude;
            float a = Mathf.Atan2(d.y, d.x);
            float rays = 0.55f + 0.45f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 8f)), 6f);
            float fall = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
            return new Color(1f, 0.93f, 0.7f, fall * rays * 0.8f);
        }

        private static Color Sparkle(float x, float y, float _)
        {
            Vector2 d = new Vector2(x - 64f, y - 64f) / 64f;
            float star = Mathf.Clamp01(1f - (Mathf.Abs(d.x) * Mathf.Abs(d.y) * 40f + d.magnitude * 0.9f));
            float core = Mathf.Pow(Mathf.Clamp01(1f - d.magnitude * 3f), 2f);
            float a = Mathf.Clamp01(star + core);
            return new Color(1f, 0.97f, 0.85f, a);
        }
    }
}
