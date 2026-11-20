using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Original procedural textures + materials for the 12 class skills (SkillVfxKit / SkillVfxDirector). Everything is
    /// computed here (no third-party art): tileable noise, crescent slash, shock ring, spark streak, flame tongue, smoke
    /// puff, feather, ground cracks, 4-point star, light column, magic circle. Written to Assets/Resources/SkillVfx so
    /// every scene gets them without a rebuild.
    ///   Unity -batchmode -executeMethod AdventureKing.ArtPipeline.SkillVfxArt.BuildFromCommandLine
    /// </summary>
    public static class SkillVfxArt
    {
        public const string Dir = "Assets/Resources/SkillVfx";

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/Art/Rebuild Skill VFX Textures")]
        public static void Build()
        {
            Directory.CreateDirectory(Dir);
            Save("noise_cloud", 256, true, (u, v) => G(Fbm(u, v, 4, 5)));
            Save("noise_streak", 256, true, (u, v) => G(Mathf.Pow(Fbm(u * 0.25f, v * 4f, 4, 7, 4f, 1f), 1.4f) * 1.2f));
            Save("slash", 512, false, Slash);
            Save("ring", 256, false, (u, v) => { float r = R(u, v); return A(Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.07f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.2f, 2f)) * 0.35f, r < 1f); });
            Save("glow", 128, false, (u, v) => { float r = R(u, v); return A(Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f), true); });
            Save("spark", 128, false, (u, v) => { float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f; return A(Mathf.Exp(-y * y * 60f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x)), 1.5f), true); });
            Save("flame", 256, false, Flame);
            Save("smoke", 256, false, (u, v) => { float r = R(u, v); float n = Fbm(u * 2f, v * 2f, 4, 11); return A(Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 1.3f) * (0.55f + n * 0.9f) - 0.05f), true); });
            Save("feather", 256, false, Feather);
            Save("cracks", 512, false, Cracks);
            Save("star", 128, false, (u, v) => { float x = Mathf.Abs(u - 0.5f) * 2f, y = Mathf.Abs(v - 0.5f) * 2f; float s = Mathf.Exp(-x * 26f) * Mathf.Clamp01(1f - y) + Mathf.Exp(-y * 26f) * Mathf.Clamp01(1f - x); float c = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 3f); return A(Mathf.Clamp01(s + c), true); });
            Save("beam", 128, false, (u, v) => { float x = Mathf.Abs(u - 0.5f) * 2f; return A(Mathf.Pow(Mathf.Clamp01(1f - x), 2f) * Mathf.SmoothStep(0f, 0.15f, v) * Mathf.SmoothStep(1f, 0.55f, v), true); });
            Save("circle", 512, false, MagicCircle);
            Mat("vfx_add", UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One);
            Mat("vfx_premul", UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            AssetDatabase.SaveAssets();
            Debug.Log("[SKILLVFX] textures + materials written to " + Dir);
        }

        // ------------------------------------------------------------------ helpers
        private static Color G(float g) { g = Mathf.Clamp01(g); return new Color(g, g, g, 1f); }
        private static Color A(float a, bool inside) { a = inside ? Mathf.Clamp01(a) : 0f; return new Color(1f, 1f, 1f, a); }
        private static float R(float u, float v) => new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;

        /// <summary>Tileable value-noise fbm on the unit square (period 1 at every octave).</summary>
        private static float Fbm(float u, float v, int octaves, int seed, float px = 1f, float py = 1f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int f = 4;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(u * f, v * f, Mathf.RoundToInt(f * px), Mathf.RoundToInt(f * py), seed + o * 31);
                norm += amp; amp *= 0.5f; f *= 2;
            }
            return sum / norm;
        }

        private static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 982451653; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }

        private static float Value(float x, float y, int px, int py, int s)
        {
            px = Mathf.Max(1, px); py = Mathf.Max(1, py);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int M(int a, int p) => ((a % p) + p) % p;
            float a00 = Hash(M(x0, px), M(y0, py), s), a10 = Hash(M(x0 + 1, px), M(y0, py), s);
            float a01 = Hash(M(x0, px), M(y0 + 1, py), s), a11 = Hash(M(x0 + 1, px), M(y0 + 1, py), s);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }

        /// <summary>Crescent blade of wind: sharp bright outer edge, soft fading inner side, tapered tips.</summary>
        private static Color Slash(float u, float v)
        {
            float x = (u - 0.5f) * 2f, y = v * 2f - 0.35f;
            float r = Mathf.Sqrt(x * x + y * y), ang = Mathf.Atan2(y, x);
            float along = Mathf.InverseLerp(Mathf.PI * 0.95f, Mathf.PI * 0.05f, ang); // 0 left tip .. 1 right tip
            if (along <= 0f || along >= 1f || y < -0.1f) return A(0f, false);
            float taper = Mathf.Sin(along * Mathf.PI);
            float thick = 0.06f + 0.22f * taper;
            float outer = 0.92f;
            float d = outer - r;
            if (d < 0f) return A(Mathf.Exp(d * 60f), true); // anti-aliased outer edge
            float inner = Mathf.Clamp01(1f - d / thick);
            return A(Mathf.Pow(inner, 1.6f) * taper, d < thick * 1.4f);
        }

        private static Color Flame(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float w = Mathf.Lerp(0.85f, 0.05f, Mathf.Pow(v, 0.8f)) * (v < 0.12f ? Mathf.SmoothStep(0.2f, 1f, v / 0.12f) : 1f);
            float n = Fbm(u * 1.5f, v * 1.2f, 3, 21);
            float e = 1f - Mathf.Abs(x) / Mathf.Max(0.01f, w * (0.75f + 0.5f * n));
            return A(Mathf.Pow(Mathf.Clamp01(e), 0.8f) * Mathf.SmoothStep(1f, 0.6f, v), true);
        }

        private static Color Feather(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float w = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI) * 0.55f * (1f - v * 0.35f);
            if (Mathf.Abs(x) > w || v < 0.04f) return A(0f, false);
            float vane = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin((v * 26f + Mathf.Abs(x) * 7f)));
            float shaft = Mathf.Exp(-x * x * 900f);
            return A(Mathf.Max(vane * Mathf.Clamp01((w - Mathf.Abs(x)) * 9f), shaft), true);
        }

        private static Color Cracks(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f, r = Mathf.Sqrt(x * x + y * y) * 2f, ang = Mathf.Atan2(y, x);
            float best = 0f;
            for (int k = 0; k < 9; k++)
            {
                float a0 = k / 9f * Mathf.PI * 2f + Hash(k, 1, 3) * 0.5f;
                float wob = (Fbm(r * 0.8f, k * 0.11f, 3, 40 + k) - 0.5f) * 0.6f;
                float da = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, (a0 + wob) * Mathf.Rad2Deg) * Mathf.Deg2Rad * r;
                float len = 0.55f + 0.4f * Hash(k, 2, 3);
                float w = 0.012f * (1f - r / len) + 0.002f;
                if (r < len) best = Mathf.Max(best, Mathf.Exp(-da * da / (w * w)));
            }
            float pit = Mathf.Pow(Mathf.Clamp01(1f - r / 0.32f), 1.5f) * 0.8f;
            return A(Mathf.Clamp01(best + pit) * Mathf.SmoothStep(1f, 0.7f, r), r < 1f);
        }

        private static Color MagicCircle(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f, r = Mathf.Sqrt(x * x + y * y) * 2f, ang = Mathf.Atan2(y, x);
            float line(float rr, float w) => Mathf.Exp(-Mathf.Pow((r - rr) / w, 2f));
            float c = line(0.96f, 0.012f) + line(0.86f, 0.008f) + line(0.55f, 0.008f) + line(0.3f, 0.01f);
            // rune ticks between the two outer rings
            float ticks = r > 0.87f && r < 0.95f ? Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 18f)), 18f) : 0f;
            // six-point star (two triangles) inside
            float star = 0f;
            for (int t = 0; t < 6; t++)
            {
                float a = t * Mathf.PI / 3f;
                Vector2 p0 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.43f, p1 = new Vector2(Mathf.Cos(a + Mathf.PI * 2f / 3f), Mathf.Sin(a + Mathf.PI * 2f / 3f)) * 0.43f;
                Vector2 p = new Vector2(x, y), d = p1 - p0;
                float h = Mathf.Clamp01(Vector2.Dot(p - p0, d) / d.sqrMagnitude);
                star = Mathf.Max(star, Mathf.Exp(-((p - p0 - d * h).sqrMagnitude) / 0.00003f));
            }
            float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.25f;
            return A(Mathf.Clamp01(c + ticks + star + glow), r < 1f);
        }

        private static void Save(string name, int size, bool repeat, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f((x + 0.5f) / size, (y + 0.5f) / size);
            t.SetPixels(px);
            t.Apply();
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.alphaIsTransparency = !repeat;
            ti.mipmapEnabled = true;
            ti.sRGBTexture = false;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.maxTextureSize = size;
            ti.SaveAndReimport();
        }

        private static void Mat(string name, UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("AdventureKing/VFXFlow");
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/noise_cloud.png"));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }
    }
}
