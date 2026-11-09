using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Bakes the login screen background from the game's own world (original project art only, no new Meshy
    /// credits): Thị trấn Chuông Gió at dawn, re-lit warm, with a cluster of glowing toon mushrooms in front, rendered
    /// at 2816x1584 (2560x1440 + 10% overscan for parallax) into separate depth layers with transparent backgrounds:
    ///   sky   — procedural dawn gradient, sun glow and soft clouds;
    ///   hills — procedural layered far hills with aerial haze (vector-like, crisp at any size);
    ///   mid   — the village (houses, bell tower, trees, windmill) beyond the split distance;
    ///   near  — foreground ground, grass, lanterns and mushrooms.
    /// LoginBackdrop moves them at different rates at runtime; mist, light shafts and fireflies are added live.
    /// The village scene is opened read-only (never saved).
    /// </summary>
    public static class LoginBackdropBaker
    {
        public const string Dir = "Assets/Art/UI/LoginBackdrop";
        public const int W = 2816, H = 1584;

        public struct View
        {
            public string Name;
            public Vector3 Pos;
            public Vector3 Euler;
            public float Fov, Split;
        }

        public static readonly View Chosen = new View { Name = "stream", Pos = new Vector3(-14f, 2.3f, -8.8f), Euler = new Vector3(-4f, 8f, 0f), Fov = 46f, Split = 9.5f };

        public static void PreviewFromCommandLine()
        {
            try
            {
                var views = new[]
                {
                    Chosen,
                    new View { Name = "stream", Pos = new Vector3(-14f, 2.2f, -8.5f), Euler = new Vector3(-3f, 8f, 0f), Fov = 46f, Split = 9.5f },
                    new View { Name = "teahouse", Pos = new Vector3(36f, 2.4f, -9f), Euler = new Vector3(-3f, 0f, 0f), Fov = 46f, Split = 9.5f },
                };
                foreach (View v in views) Bake(v, preview: true);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BakeFromCommandLine()
        {
            try { Bake(Chosen, preview: false); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/Art/Bake Login Backdrop")]
        public static void BakeMenu() => Bake(Chosen, false);

        public static void Bake(View view, bool preview)
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory("QA/UI");
            EditorSceneManager.OpenScene(VillageSceneBuilder.ScenePath, OpenSceneMode.Single);
            // only the world: no HUD, player, NPCs, portals, painted far backdrop (replaced by crisp procedural layers)
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)) c.gameObject.SetActive(false);
            foreach (string n in new[] { "Gameplay", "Backdrop_Painting", "Player", "MainCamera", "Waterfall" })
                foreach (GameObject g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name == n).Select(t => t.gameObject).ToArray())
                    g.SetActive(false);
            foreach (Volume vol in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)) vol.enabled = false; // keep alpha in the layers

            // ---- dawn light
            Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).First(l => l.type == LightType.Directional);
            sun.transform.rotation = Quaternion.Euler(16f, -58f, 0f);
            sun.color = new Color(1f, 0.8f, 0.6f);
            sun.intensity = 1.2f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.74f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.8f, 0.68f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.34f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.98f, 0.84f, 0.74f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 95f;

            Mushrooms(view);

            var camGo = new GameObject("BakeCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.SetPositionAndRotation(view.Pos, Quaternion.Euler(view.Euler));
            cam.fieldOfView = view.Fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            int w = preview ? W / 2 : W, h = preview ? H / 2 : H;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;

            Texture2D Render(float near, float far)
            {
                cam.nearClipPlane = near;
                cam.farClipPlane = far;
                cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                t.Apply();
                RenderTexture.active = null;
                return t;
            }
            Texture2D nearL = Render(0.3f, view.Split), midL = Render(view.Split, 400f);
            Texture2D sky = Sky(w, h), hills = Hills(w, h);
            cam.targetTexture = null;
            rt.Release();

            if (preview)
            {
                Texture2D comp = Composite(sky, hills, midL, nearL);
                File.WriteAllBytes($"QA/UI/login_bg_preview_{view.Name}.png", comp.EncodeToPNG());
                Debug.Log("[LOGIN-BG] preview " + view.Name);
            }
            else
            {
                Save(sky, "sky"); Save(hills, "hills"); Save(midL, "mid"); Save(nearL, "near");
                File.WriteAllBytes("QA/UI/login_bg_composite.png", Composite(sky, hills, midL, nearL).EncodeToPNG());
                AssetDatabase.Refresh();
                foreach (string n in new[] { "sky", "hills", "mid", "near" })
                {
                    string p = $"{Dir}/{n}.png";
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.maxTextureSize = 4096;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    ti.SaveAndReimport();
                }
                Debug.Log("[LOGIN-BG] baked layers into " + Dir);
            }
        }

        private static void Save(Texture2D t, string name) => File.WriteAllBytes($"{Dir}/{name}.png", t.EncodeToPNG());

        private static Texture2D Composite(params Texture2D[] layers)
        {
            int w = layers[0].width, h = layers[0].height;
            var o = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color[w * h];
            foreach (Texture2D l in layers)
            {
                Color[] c = l.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(px[i], new Color(c[i].r, c[i].g, c[i].b, 1f), c[i].a);
            }
            o.SetPixels(px);
            o.Apply();
            return o;
        }

        // ------------------------------------------------------------------ procedural sky + hills
        private static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);
        private static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
        }
        private static float Fbm(float x, float y) { float s = 0, a = 0.5f; for (int i = 0; i < 5; i++) { s += a * Noise(x, y); x *= 2.03f; y *= 2.03f; a *= 0.5f; } return s; }

        public static readonly Vector2 SunUv = new Vector2(0.27f, 0.6f);

        private static Texture2D Sky(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            Color top = new Color(0.36f, 0.52f, 0.86f), mid = new Color(0.98f, 0.74f, 0.66f), low = new Color(1f, 0.88f, 0.66f);
            float aspect = w / (float)h;
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                Color baseC = v > 0.55f ? Color.Lerp(mid, top, Mathf.SmoothStep(0f, 1f, (v - 0.55f) / 0.45f)) : Color.Lerp(low, mid, Mathf.SmoothStep(0f, 1f, v / 0.55f));
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    Vector2 d = new Vector2((u - SunUv.x) * aspect, v - SunUv.y);
                    float r = d.magnitude;
                    Color c = baseC + new Color(1f, 0.85f, 0.6f) * (Mathf.Exp(-r * 3.2f) * 0.55f) + new Color(1f, 0.97f, 0.88f) * Mathf.Clamp01(1f - r / 0.045f);
                    // soft cumulus bands
                    float cl = Fbm(u * 5f * aspect / 1.8f, v * 9f);
                    float band = Mathf.SmoothStep(0.15f, 0.55f, v) * (1f - Mathf.SmoothStep(0.8f, 1f, v));
                    float cloud = Mathf.SmoothStep(0.52f, 0.72f, cl) * band;
                    Color cloudC = Color.Lerp(new Color(1f, 0.86f, 0.82f), new Color(1f, 0.97f, 0.93f), Mathf.Clamp01(cl * 1.4f - 0.3f + (1f - r) * 0.3f));
                    c = Color.Lerp(c, cloudC, cloud * 0.85f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Hills(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            // three ridges, far -> near: (baseline v, amplitude, frequency, colour)
            var ridges = new (float baseV, float amp, float freq, Color col, float seed)[]
            {
                (0.50f, 0.10f, 2.2f, new Color(0.72f, 0.66f, 0.78f), 1.7f),
                (0.43f, 0.08f, 3.1f, new Color(0.58f, 0.62f, 0.66f), 5.3f),
                (0.36f, 0.06f, 4.3f, new Color(0.47f, 0.6f, 0.52f), 9.1f),
            };
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                for (int ri = 0; ri < ridges.Length; ri++)
                {
                    var (baseV, amp, freq, col, seed) = ridges[ri];
                    float top = baseV + amp * (Fbm(u * freq + seed, seed) - 0.35f) * 2f;
                    int ty = Mathf.Clamp(Mathf.RoundToInt(top * (h - 1)), 0, h - 1);
                    for (int y = 0; y <= ty; y++)
                    {
                        float v = y / (float)(h - 1);
                        float edge = Mathf.Clamp01((top - v) * h / 1.5f); // 1.5 px anti-aliased ridge line
                        Color c = Color.Lerp(col, new Color(1f, 0.86f, 0.78f), Mathf.Clamp01((v - (baseV - 0.2f)) / 0.3f) * 0.35f); // haze near the crest
                        c.a = 1f;
                        Color prev = px[y * w + x];
                        px[y * w + x] = Color.Lerp(prev, c, edge);
                    }
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ mushroom grove (foreground, original)
        private static void Mushrooms(View view)
        {
            var root = new GameObject("LoginMushrooms").transform;
            Shader toon = Shader.Find(HeroArtPipeline.ToonShader);
            Material Mat(string name, Color emission)
            {
                string p = $"{Dir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(toon); AssetDatabase.CreateAsset(m, p); }
                m.shader = toon;
                m.SetFloat("_UseVertexColor", 1f);
                m.EnableKeyword("_VERTEX_COLOR");
                m.SetColor("_EmissionColor", emission);
                m.SetFloat("_OutlineWidth", 0.012f);
                EditorUtility.SetDirty(m);
                return m;
            }
            Material plain = Mat("mushroom_toon", Color.black), glow = Mat("mushroom_glow", new Color(0.12f, 0.3f, 0.32f));
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            // a small enchanted grove: one cluster at the bank left of the bridge, one in the right foreground,
            // a few glowing ones under the banyan (positions in front of the stream view)
            var spots = new List<(Vector3 p, float s, bool glowCap)>();
            void Cluster(float cx, float cz, int count, float big, float spread)
            {
                for (int i = 0; i < count; i++)
                    spots.Add((new Vector3(cx + (R() - 0.5f) * spread * 2f, 0f, cz + (R() - 0.5f) * spread), (i == 0 ? big : 0.35f + R() * big * 0.55f), R() > 0.5f));
            }
            Cluster(view.Pos.x - 5.6f, view.Pos.z + 7.2f, 7, 1.25f, 1.3f);
            Cluster(view.Pos.x + 4.2f, view.Pos.z + 5.8f, 6, 1.0f, 1.1f);
            Cluster(view.Pos.x + 3.0f, view.Pos.z + 17.5f, 5, 0.9f, 1.6f);
            foreach (var (p, s, g) in spots)
            {
                var om = new OutfitMesh();
                Color capA = g ? new Color(0.35f, 0.85f, 0.85f) : new Color(0.92f, 0.36f, 0.3f), capB = g ? new Color(0.75f, 1f, 0.95f) : new Color(1f, 0.6f, 0.45f);
                // stem (lathe), cap (half ellipsoid), spots
                var stem = new List<Vector2> { new Vector2(0.001f, 0f), new Vector2(0.17f, 0f), new Vector2(0.14f, 0.3f), new Vector2(0.12f, 0.62f), new Vector2(0.15f, 0.72f), new Vector2(0.001f, 0.74f) };
                om.Lathe(Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * s), stem, 14, i => Color.Lerp(new Color(0.95f, 0.9f, 0.8f), new Color(1f, 0.97f, 0.92f), i / 5f), new BoneWeight());
                om.Orient(0, q => { Vector3 d = q - p; d.y = 0; return d.sqrMagnitude > 1e-6f ? d : Vector3.down; });
                Vector3 capC = p + Vector3.up * 0.66f * s;
                int t0 = om.T.Count;
                om.Ellipsoid(Matrix4x4.TRS(capC, Quaternion.Euler(R() * 14f - 7f, R() * 360f, R() * 14f - 7f), new Vector3(0.48f, 0.34f, 0.48f) * s), 18, 8,
                    l => Color.Lerp(capA, capB, Mathf.Clamp01(l.y)), new BoneWeight(), -0.15f);
                om.Orient(t0, q => q - capC);
                for (int k = 0; k < 6; k++)
                {
                    float a = R() * Mathf.PI * 2f, el = 0.35f + R() * 0.5f;
                    Vector3 dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(el) * 0.48f, Mathf.Sin(el) * 0.34f, Mathf.Sin(a) * Mathf.Cos(el) * 0.48f);
                    Vector3 sp = capC + dir * s;
                    int ts = om.T.Count;
                    om.Ellipsoid(Matrix4x4.TRS(sp, Quaternion.LookRotation(dir.normalized), new Vector3(0.06f, 0.06f, 0.02f) * s), 8, 4, _ => new Color(1f, 0.98f, 0.92f), new BoneWeight());
                    om.Orient(ts, q => q - sp);
                }
                Mesh mesh = om.ToMesh("mushroom", new Matrix4x4[0]);
                mesh.boneWeights = new BoneWeight[0];
                var go = new GameObject("Mushroom");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = g ? glow : plain;
            }
        }
    }
}
