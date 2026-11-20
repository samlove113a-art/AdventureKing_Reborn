using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// The game title logo shown beside the login card: the OWNER'S logo (ArtSource/OwnerProvided/Logo), delivered on a
    /// plain white background. This cuts it out: white backdrop -> transparent (border-connected white plus large
    /// enclosed white areas such as inside the arc), the soft grey drop shadow -> a real semi-transparent dark shadow,
    /// and the light fringe on the logo's edge is un-blended from white. Output: Assets/Art/Branding/AK_TitleLogo.png.
    /// </summary>
    public static class LogoArt
    {
        public const string OwnerSource = "ArtSource/OwnerProvided/Logo/AK_Logo_Owner_white.png";
        public const string OutPath = "Assets/Art/Branding/AK_TitleLogo.png";

        public static Sprite Sprite() => AssetDatabase.LoadAssetAtPath<Sprite>(OutPath);

        [MenuItem("Adventure King/Art/Cut Owner Title Logo")]
        public static void Build()
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(OwnerSource));
            int w = src.width, h = src.height;
            Color[] px = src.GetPixels();
            var lum = new float[px.Length];
            var bgLike = new bool[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                Color p = px[i];
                lum[i] = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
                float chroma = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) - Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                bgLike[i] = lum[i] > 0.62f && chroma < 0.09f; // white backdrop and its light grey shadow
            }
            // backdrop = white regions touching the border, or large enclosed ones (inside the arc)
            var bg = new bool[px.Length];
            var seen = new bool[px.Length];
            var q = new Queue<int>();
            var region = new List<int>();
            for (int s0 = 0; s0 < px.Length; s0++)
            {
                if (!bgLike[s0] || seen[s0]) continue;
                region.Clear(); bool border = false;
                seen[s0] = true; q.Enqueue(s0);
                while (q.Count > 0)
                {
                    int i = q.Dequeue(), x = i % w, y = i / w;
                    region.Add(i);
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) border = true;
                    void Try(int k) { if (bgLike[k] && !seen[k]) { seen[k] = true; q.Enqueue(k); } }
                    if (x > 0) Try(i - 1); if (x < w - 1) Try(i + 1); if (y > 0) Try(i - w); if (y < h - 1) Try(i + w);
                }
                if (border || region.Count >= 800) foreach (int i in region) bg[i] = true;
            }
            var outPx = new Color[px.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (bg[i])
                    {
                        // grey on white = dark shadow over transparency
                        float a = Mathf.Clamp01((1f - lum[i]) * 1.6f);
                        outPx[i] = new Color(0.08f, 0.04f, 0.02f, a < 0.04f ? 0f : a * 0.85f);
                        continue;
                    }
                    Color p = px[i];
                    bool edge = (x > 0 && bg[i - 1]) || (x < w - 1 && bg[i + 1]) || (y > 0 && bg[i - w]) || (y < h - 1 && bg[i + w]);
                    if (edge && lum[i] > 0.45f)
                    {
                        // anti-aliased rim blended with white: estimate coverage and remove the white
                        float a = Mathf.Clamp01((1f - lum[i]) / 0.55f + 0.25f);
                        p = new Color(Mathf.Clamp01((p.r - (1f - a)) / a), Mathf.Clamp01((p.g - (1f - a)) / a), Mathf.Clamp01((p.b - (1f - a)) / a), a);
                    }
                    else p.a = 1f;
                    outPx[i] = p;
                }
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(outPx);
            outTex.Apply();
            if (File.Exists(OutPath)) AssetDatabase.DeleteAsset(OutPath); // fresh import (no stale sprite rect)
            File.WriteAllBytes(OutPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(OutPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            Sprite sp = Sprite();
            Debug.Log($"[LOGO] owner title logo {w}x{h} spriteRect={(sp ? sp.rect.ToString() : "none")}");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
