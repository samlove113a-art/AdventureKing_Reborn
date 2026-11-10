using System.Collections.Generic;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Procedural wedding props sized for the chibi heroes (no Meshy credit): a three-tier cutting cake whose front
    /// slice really separates (sponge and cream layers on the cut faces), a cake knife, gilded goblets with
    /// see-through glass and wine whose level drops while drinking, wedding rings with a gem, a draped cake table,
    /// a plate, and sparkle / glow sprites. All meshes are built once and cached.
    /// </summary>
    public static class WeddingProps
    {
        // ------------------------------------------------------------------ materials
        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        /// <summary>A toon material cloned from the hall's ceremony material (same shader, lighting, look).</summary>
        public static Material Toon(Material template, string key, Color baseColor, Color? emission = null)
        {
            if (mats.TryGetValue(key, out Material m) && m) return m;
            m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "wed_" + key;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", Color.Lerp(baseColor, new Color(0.5f, 0.42f, 0.55f), 0.42f));
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission ?? Color.black);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
            return mats[key] = m;
        }

        /// <summary>See-through glass (URP Lit, transparent, glossy) so the wine inside the goblet is visible.</summary>
        public static Material Glass()
        {
            if (mats.TryGetValue("glass", out Material m) && m) return m;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(lit) { name = "wed_glass" };
            m.SetFloat("_Surface", 1f);               // transparent
            m.SetFloat("_Blend", 0f);                 // alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.92f);
            m.SetFloat("_Metallic", 0f);
            m.SetColor("_BaseColor", new Color(0.86f, 0.93f, 1f, 0.28f));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return mats["glass"] = m;
        }

        // ------------------------------------------------------------------ mesh helpers
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        private static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (meshes.TryGetValue(key, out Mesh m) && m) return m;
            return meshes[key] = make();
        }

        public static Mesh Lathe(string key, Vector2[] profile, int seg = 24) => Cached("lathe_" + key, () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = s * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                    uv.Add(new Vector2(s / (float)seg, i / (float)(profile.Length - 1)));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a0 = i * (seg + 1) + s, b0 = a0 + seg + 1;
                    t.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1 });
                }
            var m = new Mesh { name = "lathe_" + key };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        });

        private static GameObject Part(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Round cake tier sector from angle a0 to a1 (radians, around +Y, 0 = +X). Submesh 0 = frosting
        /// (top, side, bottom), submesh 1 = the cut faces (sponge layers) when the sector is not a full circle.</summary>
        private static Mesh Sector(string key, float r, float h, float a0, float a1, int seg) => Cached(key, () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t0 = new List<int>(); var t1 = new List<int>();
            bool full = a1 - a0 >= Mathf.PI * 2f - 1e-3f;
            // top + bottom fans, sides
            int c0 = v.Count; v.Add(new Vector3(0f, h, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
            int cb = v.Count; v.Add(Vector3.zero); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                v.Add(p + Vector3.up * h); uv.Add(new Vector2(i / (float)seg, 1f)); // top ring
                v.Add(p); uv.Add(new Vector2(i / (float)seg, 0f));                 // bottom ring
                v.Add(p + Vector3.up * h); uv.Add(new Vector2(i / (float)seg, 1f)); // side top (own normals)
                v.Add(p); uv.Add(new Vector2(i / (float)seg, 0f));                 // side bottom
            }
            for (int i = 0; i < seg; i++)
            {
                int b = 2 + i * 4, n = b + 4;
                t0.AddRange(new[] { c0, n, b });             // top (CCW seen from above)
                t0.AddRange(new[] { cb, b + 1, n + 1 });     // bottom
                t0.AddRange(new[] { b + 2, n + 2, n + 3, b + 2, n + 3, b + 3 }); // side
            }
            if (!full)
                foreach (float a in new[] { a0, a1 })
                {
                    Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    int s = v.Count;
                    v.Add(Vector3.zero); v.Add(Vector3.up * h); v.Add(p + Vector3.up * h); v.Add(p);
                    uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f));
                    if (a == a0) t1.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                    else t1.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                }
            var m = new Mesh { name = key, subMeshCount = 2 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t0, 0); m.SetTriangles(t1, 1);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        });

        /// <summary>Little spheres (piped cream beads, roses, pearls) merged into one mesh.</summary>
        private static Mesh Beads(string key, List<(Vector3 p, float r)> beads) => Cached(key, () =>
        {
            Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var parts = new CombineInstance[beads.Count];
            for (int i = 0; i < beads.Count; i++)
                parts[i] = new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(beads[i].p, Quaternion.identity, Vector3.one * beads[i].r * 2f) };
            var m = new Mesh { name = key };
            m.CombineMeshes(parts, true, true);
            m.RecalculateBounds();
            return m;
        });

        private static Texture2D spongeTex;
        /// <summary>Cut-face texture: sponge layers with cream and strawberry filling.</summary>
        private static Texture2D SpongeTexture()
        {
            if (spongeTex) return spongeTex;
            const int W = 32, H = 128;
            spongeTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "wed_sponge" };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)(H - 1);
                    Color c = new Color(0.98f, 0.86f, 0.58f);                     // sponge
                    float n = Mathf.PerlinNoise(x * 0.45f, y * 0.45f);
                    c *= 0.92f + 0.12f * n;
                    if (Mathf.Abs(v - 0.36f) < 0.05f) c = new Color(1f, 0.97f, 0.93f);   // cream
                    if (Mathf.Abs(v - 0.66f) < 0.045f) c = new Color(0.93f, 0.35f, 0.45f); // strawberry
                    if (v > 0.93f) c = new Color(1f, 0.98f, 0.96f);                 // frosting skin
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            spongeTex.SetPixels(px); spongeTex.Apply();
            return spongeTex;
        }

        // ------------------------------------------------------------------ cake
        public sealed class Cake
        {
            public GameObject Root, Slice;
            public Vector3 SliceHome;      // local
            public float Radius, Height, TopOfBottomTier;
            /// <summary>World points: blade enters the frosting at the slice's two cut lines (left / right edge).</summary>
            public Vector3 CutEdge(bool first, float depth01)
            {
                float a = first ? wedgeA0 : wedgeA1;
                Vector3 local = new Vector3(Mathf.Cos(a) * Radius * 0.55f, Mathf.Lerp(TopOfBottomTier + 0.012f, 0.012f, depth01), Mathf.Sin(a) * Radius * 0.55f);
                return Root.transform.TransformPoint(local);
            }
            public Vector3 SliceCentreWorld => Slice.transform.position + Root.transform.rotation * new Vector3(Mathf.Cos(wedgeMid) * Radius * 0.6f, TopOfBottomTier * 0.5f, Mathf.Sin(wedgeMid) * Radius * 0.6f);
            public float wedgeA0, wedgeA1, wedgeMid;
            public GameObject CutLineA, CutLineB;
        }

        /// <summary>Three-tier wedding cake; the bottom tier's front wedge (toward local -Z) is a separate slice.</summary>
        public static Cake BuildCake(Transform parent, Material template, float scale, float wedgeMidDegrees = -110f)
        {
            var cake = new Cake { Root = new GameObject("CuttingCake") };
            cake.Root.transform.SetParent(parent, false);
            Material frost = Toon(template, "frost", new Color(1f, 0.97f, 0.95f));
            Material sponge = Toon(template, "sponge", Color.white);
            if (sponge.HasProperty("_BaseMap")) sponge.SetTexture("_BaseMap", SpongeTexture());
            Material pink = Toon(template, "ribbon", new Color(0.97f, 0.62f, 0.72f));
            Material rose = Toon(template, "rose", new Color(0.95f, 0.42f, 0.55f));
            Material leaf = Toon(template, "leaf", new Color(0.46f, 0.72f, 0.42f));
            Material gold = Toon(template, "cakegold", new Color(1f, 0.82f, 0.4f), new Color(0.25f, 0.17f, 0.03f));
            float r0 = 0.24f * scale, h0 = 0.15f * scale, r1 = 0.17f * scale, h1 = 0.12f * scale, r2 = 0.11f * scale, h2 = 0.1f * scale;
            const float W = 42f * Mathf.Deg2Rad;
            float mid = wedgeMidDegrees * Mathf.Deg2Rad; // which side the first slice is cut from (0 = +X, 90 = +Z)
            cake.wedgeA0 = mid - W * 0.5f; cake.wedgeA1 = mid + W * 0.5f; cake.wedgeMid = mid;
            cake.Radius = r0; cake.TopOfBottomTier = h0; cake.Height = h0 + h1 + h2;
            string k = "s" + scale.ToString("0.00") + "w" + Mathf.RoundToInt(wedgeMidDegrees);
            // bottom tier minus the wedge + the wedge itself
            Part(cake.Root.transform, "Tier0", Sector("tier0" + k, r0, h0, cake.wedgeA1, cake.wedgeA0 + Mathf.PI * 2f, 40), frost, sponge);
            var slice = new GameObject("Slice");
            slice.transform.SetParent(cake.Root.transform, false);
            Part(slice.transform, "Wedge", Sector("wedge" + k, r0, h0, cake.wedgeA0, cake.wedgeA1, 6), frost, sponge);
            cake.Slice = slice;
            cake.SliceHome = slice.transform.localPosition;
            var t1 = Part(cake.Root.transform, "Tier1", Sector("tier1" + k, r1, h1, 0f, Mathf.PI * 2f, 36), frost, sponge);
            t1.transform.localPosition = new Vector3(0f, h0, 0f);
            var t2 = Part(cake.Root.transform, "Tier2", Sector("tier2" + k, r2, h2, 0f, Mathf.PI * 2f, 32), frost, sponge);
            t2.transform.localPosition = new Vector3(0f, h0 + h1, 0f);
            // ribbons around each tier (thin bands) and piped beads on the top edges (the slice carries its own beads)
            foreach (var (r, y, n) in new[] { (r0, h0 * 0.3f, "0"), (r1, h0 + h1 * 0.3f, "1"), (r2, h0 + h1 + h2 * 0.3f, "2") })
            {
                var band = Part(cake.Root.transform, "Ribbon" + n, Lathe("band" + n + k, new[] { new Vector2(r * 1.012f, -0.012f * scale), new Vector2(r * 1.02f, 0f), new Vector2(r * 1.012f, 0.012f * scale) }, 40), pink);
                band.transform.localPosition = new Vector3(0f, y, 0f);
                if (n == "0") band.SetActive(false); // the bottom ribbon would cross the slice gap: piped beads instead
            }
            var beadsCake = new List<(Vector3, float)>(); var beadsSlice = new List<(Vector3, float)>();
            void Ring(float r, float y, int count, float br, bool canSlice)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count;
                    float wrapped = Mathf.Repeat(a - cake.wedgeA0, Mathf.PI * 2f);
                    var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    if (canSlice && wrapped < W) beadsSlice.Add((p, br)); else beadsCake.Add((p, br));
                }
            }
            Ring(r0 * 0.97f, h0, 46, 0.011f * scale, true);
            Ring(r0 * 1.0f, 0.008f * scale, 50, 0.01f * scale, true);
            Ring(r1 * 0.97f, h0 + h1, 34, 0.01f * scale, false);
            Ring(r2 * 0.96f, h0 + h1 + h2, 24, 0.009f * scale, false);
            Part(cake.Root.transform, "Piping", Beads("beads" + k, beadsCake), frost);
            Part(slice.transform, "SlicePiping", Beads("slicebeads" + k, beadsSlice), frost);
            // roses on top + a few on the tiers, leaves, a small gold heart topper
            var roses = new List<(Vector3, float)>(); var leaves = new List<(Vector3, float)>();
            float top = h0 + h1 + h2;
            foreach (var (x, z, r) in new[] { (0f, 0f, 0.03f), (0.045f, 0.02f, 0.022f), (-0.04f, 0.03f, 0.022f), (0.01f, -0.045f, 0.02f) })
            {
                roses.Add((new Vector3(x * scale, top + r * scale * 0.6f, z * scale), r * scale));
                leaves.Add((new Vector3((x + 0.022f) * scale, top + 0.005f * scale, (z - 0.012f) * scale), 0.012f * scale));
            }
            foreach (float a in new[] { 0.6f, 2.3f, 4.1f })
                roses.Add((new Vector3(Mathf.Cos(a) * r1 * 0.98f, h0 + h1 * 0.7f, Mathf.Sin(a) * r1 * 0.98f), 0.02f * scale));
            Part(cake.Root.transform, "Roses", Beads("roses" + k, roses), rose);
            Part(cake.Root.transform, "Leaves", Beads("leaves" + k, leaves), leaf);
            var topper = Part(cake.Root.transform, "Topper", Lathe("topper" + k, new[] { new Vector2(0f, 0f), new Vector2(0.006f * scale, 0f), new Vector2(0.006f * scale, 0.06f * scale), new Vector2(0f, 0.06f * scale) }, 8), gold);
            topper.transform.localPosition = new Vector3(0f, top + 0.02f * scale, 0f);
            // cut lines: thin dark slits that appear where the knife went down
            cake.CutLineA = CutLine(cake, cake.wedgeA0, r0, h0, scale, template);
            cake.CutLineB = CutLine(cake, cake.wedgeA1, r0, h0, scale, template);
            return cake;
        }

        private static GameObject CutLine(Cake cake, float a, float r, float h, float scale, Material template)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "CutLine";
            go.transform.SetParent(cake.Root.transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * r * 0.5f, h + 0.0015f * scale, Mathf.Sin(a) * r * 0.5f);
            go.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            go.transform.localScale = new Vector3(r * 1.02f, 0.003f * scale, 0.006f * scale);
            go.GetComponent<MeshRenderer>().sharedMaterial = Toon(template, "cutline", new Color(0.86f, 0.62f, 0.45f));
            go.SetActive(false);
            return go;
        }

        // ------------------------------------------------------------------ knife
        /// <summary>Cake knife: origin = grip (middle of the handle), +Y along the blade, blade flat in the YZ plane.</summary>
        public static GameObject Knife(Material template, float scale)
        {
            var root = new GameObject("CakeKnife");
            Material steel = Toon(template, "steel", new Color(0.9f, 0.93f, 0.98f));
            Material gold = Toon(template, "knifegold", new Color(1f, 0.83f, 0.42f), new Color(0.22f, 0.15f, 0.03f));
            Material ribbon = Toon(template, "ribbon", new Color(0.97f, 0.62f, 0.72f));
            var handle = Part(root.transform, "Handle", Lathe("knife_handle", new[]
            {
                new Vector2(0f, -0.06f), new Vector2(0.012f, -0.058f), new Vector2(0.013f, -0.03f), new Vector2(0.011f, 0.03f),
                new Vector2(0.016f, 0.045f), new Vector2(0.016f, 0.052f), new Vector2(0f, 0.055f),
            }, 12), gold);
            handle.transform.localScale = Vector3.one * scale;
            // blade: flat, rounded tip, slight curve
            var v = new List<Vector3>(); var t = new List<int>();
            const int N = 10; float len = 0.24f, w = 0.042f, th = 0.0035f;
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N;
                float y = 0.055f + u * len;
                float half = w * 0.5f * (u < 0.75f ? 1f : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((u - 0.75f) / 0.25f, 2f))));
                float spine = 0.004f * Mathf.Sin(u * Mathf.PI);
                v.Add(new Vector3(-th, y, -half + spine)); v.Add(new Vector3(-th, y, half * 0.55f + spine));
                v.Add(new Vector3(th, y, -half + spine)); v.Add(new Vector3(th, y, half * 0.55f + spine));
            }
            for (int i = 0; i < N; i++)
            {
                int a = i * 4, b = a + 4;
                t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });             // side -X
                t.AddRange(new[] { a + 2, a + 3, b + 2, a + 3, b + 3, b + 2 }); // side +X
                t.AddRange(new[] { a, a + 2, b, a + 2, b + 2, b });             // edge
                t.AddRange(new[] { a + 1, b + 1, a + 3, a + 3, b + 1, b + 3 }); // spine
            }
            var bm = new Mesh { name = "knife_blade" };
            bm.SetVertices(v); bm.SetTriangles(t, 0); bm.RecalculateNormals(); bm.RecalculateBounds();
            var blade = Part(root.transform, "Blade", bm, steel);
            blade.transform.localScale = Vector3.one * scale;
            // ribbon bow on the bolster
            var bow = Part(root.transform, "Bow", Beads("knife_bow", new List<(Vector3, float)>
            {
                (new Vector3(0f, 0.05f, 0.02f), 0.012f), (new Vector3(0f, 0.05f, -0.02f), 0.012f), (new Vector3(0f, 0.05f, 0f), 0.008f),
            }), ribbon);
            bow.transform.localScale = Vector3.one * scale;
            return root;
        }

        /// <summary>Blade tip in world space (for the knife-in-cake checks).</summary>
        public static Vector3 KnifeTip(GameObject knife, float scale) => knife.transform.TransformPoint(new Vector3(0f, (0.055f + 0.24f) * scale, 0f));
        public static Vector3 KnifeEdgeMid(GameObject knife, float scale) => knife.transform.TransformPoint(new Vector3(0f, (0.055f + 0.17f) * scale, -0.02f * scale));

        // ------------------------------------------------------------------ goblet
        public sealed class Goblet
        {
            public GameObject Root, Wine;
            public float Level = 1f; // 1 full .. 0 empty
            public float Scale;
            public Vector3 RimWorld => Root.transform.TransformPoint(new Vector3(0f, 0.2f * Scale, 0f));
            public void SetLevel(float level)
            {
                Level = Mathf.Clamp01(level);
                Wine.transform.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1f, Level), 1f);
                Wine.SetActive(Level > 0.02f);
            }
        }

        /// <summary>Gilded goblet with a see-through bowl. Origin = grip on the stem; +Y up.</summary>
        public static Goblet BuildGoblet(Material template, float scale)
        {
            var g = new Goblet { Root = new GameObject("Goblet"), Scale = scale };
            Material gold = Toon(template, "gobletgold", new Color(1f, 0.82f, 0.4f), new Color(0.25f, 0.17f, 0.03f));
            Material wine = Toon(template, "wine", new Color(0.72f, 0.08f, 0.2f), new Color(0.18f, 0.01f, 0.04f));
            var body = new GameObject("Body");
            body.transform.SetParent(g.Root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.05f * scale, 0f); // grip at mid-stem
            body.transform.localScale = Vector3.one * scale;
            Part(body.transform, "Foot+Stem", Lathe("goblet_stem", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.045f, 0f), new Vector2(0.044f, 0.008f), new Vector2(0.012f, 0.018f),
                new Vector2(0.008f, 0.03f), new Vector2(0.011f, 0.05f), new Vector2(0.008f, 0.07f), new Vector2(0.012f, 0.09f), new Vector2(0.02f, 0.098f), new Vector2(0f, 0.098f),
            }, 20), gold);
            Part(body.transform, "Bowl", Lathe("goblet_bowl", new[]
            {
                new Vector2(0.02f, 0.098f), new Vector2(0.04f, 0.11f), new Vector2(0.054f, 0.14f), new Vector2(0.057f, 0.17f), new Vector2(0.054f, 0.2f),
            }, 28), Glass());
            Part(body.transform, "Rim", Lathe("goblet_rim", new[] { new Vector2(0.052f, 0.196f), new Vector2(0.058f, 0.2f), new Vector2(0.052f, 0.204f) }, 28), gold);
            var w = new GameObject("WinePivot");
            w.transform.SetParent(body.transform, false);
            w.transform.localPosition = new Vector3(0f, 0.102f, 0f);
            Part(w.transform, "Wine", Lathe("goblet_wine", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.03f, 0.006f), new Vector2(0.045f, 0.035f), new Vector2(0.049f, 0.068f), new Vector2(0f, 0.068f),
            }, 24), wine);
            g.Wine = w;
            g.SetLevel(1f);
            return g;
        }

        // ------------------------------------------------------------------ ring
        public static Mesh Torus(float R, float r) => Cached($"torus{R:0.000}_{r:0.000}", () =>
        {
            const int seg = 32, side = 12;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
                for (int j = 0; j <= side; j++)
                {
                    float a = i * Mathf.PI * 2f / seg, b = j * Mathf.PI * 2f / side;
                    var c = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f);
                    var d = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b));
                    v.Add(c + d * r); n.Add(d);
                }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    t.AddRange(new[] { a0, a1, a0 + 1, a0 + 1, a1, a1 + 1 });
                }
            var m = new Mesh { name = "ring" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        });

        /// <summary>Gold wedding ring: ring axis = local Z (slides onto a finger along Z), gem on local +Y.</summary>
        public static GameObject Ring(Material template, float scale, Color gem)
        {
            var go = Part(null, "WeddingRing", Torus(0.026f * scale, 0.0065f * scale), Toon(template, "ringgold", new Color(1f, 0.84f, 0.42f), new Color(0.3f, 0.2f, 0.04f)));
            var g = Part(go.transform, "Gem", Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), Toon(template, "gem" + ColorUtility.ToHtmlStringRGB(gem), gem, gem * 0.45f));
            g.transform.localPosition = new Vector3(0f, 0.031f * scale, 0f);
            g.transform.localScale = Vector3.one * 0.018f * scale;
            return go;
        }

        // ------------------------------------------------------------------ table, plate
        /// <summary>Round table with a draped cloth. Top at <paramref name="height"/>.</summary>
        public static GameObject Table(Transform parent, Material template, float radius, float height)
        {
            var root = new GameObject("CakeTable");
            root.transform.SetParent(parent, false);
            Material cloth = Toon(template, "cloth", new Color(1f, 0.98f, 0.96f));
            Material trim = Toon(template, "clothtrim", new Color(0.97f, 0.7f, 0.78f));
            Material gold = Toon(template, "tablegold", new Color(1f, 0.82f, 0.4f), new Color(0.2f, 0.14f, 0.03f));
            string k = $"{radius:0.00}_{height:0.00}";
            Part(root.transform, "Cloth", Lathe("cloth" + k, new[]
            {
                new Vector2(0f, height), new Vector2(radius, height), new Vector2(radius * 1.04f, height - 0.03f),
                new Vector2(radius * 1.05f, height * 0.5f), new Vector2(radius * 1.09f, 0.02f), new Vector2(radius * 1.09f, 0f),
            }, 40), cloth);
            Part(root.transform, "Trim", Lathe("trim" + k, new[] { new Vector2(radius * 1.03f, height - 0.055f), new Vector2(radius * 1.06f, height - 0.04f), new Vector2(radius * 1.05f, height - 0.02f) }, 40), trim);
            Part(root.transform, "Hem", Lathe("hem" + k, new[] { new Vector2(radius * 1.09f, 0.035f), new Vector2(radius * 1.105f, 0.02f), new Vector2(radius * 1.09f, 0.005f) }, 40), gold);
            return root;
        }

        public static GameObject Plate(Material template, float scale)
        {
            Material china = Toon(template, "china", new Color(0.99f, 0.99f, 1f));
            var go = Part(null, "Plate", Lathe("plate", new[] { new Vector2(0f, 0.004f), new Vector2(0.08f, 0.004f), new Vector2(0.11f, 0.014f), new Vector2(0.115f, 0.012f), new Vector2(0.06f, 0f), new Vector2(0f, 0f) }, 32), china);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ------------------------------------------------------------------ sprites
        private static Sprite star, glow;
        public static Sprite Star()
        {
            if (star) return star;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, w = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Abs(u) * Mathf.Abs(w) * 9f + (u * u + w * w) * 0.9f; // 4-point star
                    float a = Mathf.Clamp01(1f - d) ;
                    px[y * n + x] = new Color(1f, 0.95f, 0.75f, a * a);
                }
            tex.SetPixels(px); tex.Apply();
            return star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 256f);
        }

        public static Sprite Glow()
        {
            if (glow) return glow;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, w = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + w * w));
                    px[y * n + x] = new Color(1f, 0.9f, 0.85f, a * a * a);
                }
            tex.SetPixels(px); tex.Apply();
            return glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
