using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Procedural, vertex-coloured toon geometry for the world (Docs/ART_DIRECTION_TOWN.md §4): sculpted ground strips
    /// (grass top with a stone path, drooping grass lip, earth face with bulging rocks), rounded boxes, plank decks and
    /// curved-tile eaves. Everything renders with AdventureKing/Toon + vertex colours + the shared outline, so terrain
    /// shares the heroes' material language instead of being flat textured boxes.
    /// </summary>
    public static class WorldKit
    {
        private const string MeshDir = "Assets/Art/World/Meshes";
        public static readonly Color OutlineColor = new Color(0.13f, 0.09f, 0.12f);

        // Palette (shared with Docs/ART_DIRECTION_TOWN.md)
        static readonly Color GrassA = new(0.44f, 0.7f, 0.33f), GrassB = new(0.58f, 0.8f, 0.36f), GrassDark = new(0.3f, 0.52f, 0.27f);
        static readonly Color PathA = new(0.86f, 0.78f, 0.6f), PathB = new(0.76f, 0.66f, 0.5f);
        static readonly Color SoilA = new(0.6f, 0.42f, 0.29f), SoilB = new(0.5f, 0.34f, 0.24f), SoilDeep = new(0.38f, 0.26f, 0.2f);
        static readonly Color RockA = new(0.66f, 0.63f, 0.6f), RockB = new(0.5f, 0.48f, 0.47f);

        public static Material VertexColorMaterial(string name, float outline)
        {
            string path = $"Assets/Art/World/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                m = new Material(Shader.Find("AdventureKing/Toon"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("AdventureKing/Toon");
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_ShadeColor", new Color(0.66f, 0.62f, 0.76f));
            m.SetFloat("_UseVertexColor", 1f);
            m.EnableKeyword("_VERTEX_COLOR");
            m.SetFloat("_OutlineWidth", outline);
            m.SetColor("_OutlineColor", OutlineColor);
            m.SetShaderPassEnabled("SRPDefaultUnlit", outline > 0f);
            m.SetFloat("_RimStrength", 0.18f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Mesh Save(Mesh mesh, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static float N(float x, float y, float s) => Mathf.PerlinNoise(x * s + 13.1f, y * s + 7.3f);

        // ------------------------------------------------------------------ ground strip
        /// <summary>
        /// Sculpted ground between x0..x1: top surface (walk line at y=0 is flat for the collider), grass lip that
        /// droops over the front, an earth face whose rocks bulge toward the camera, darker toward the bottom.
        /// </summary>
        public static Mesh GroundStrip(string name, float x0, float x1, int seed, float front = -1.4f, float back = 16f, float bottom = -5f)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            const float step = 0.45f;
            int cols = Mathf.CeilToInt((x1 - x0) / step) + 1;
            float o = seed * 17.3f;

            // --- top: rows from the lip edge back to the horizon
            float[] zs = { front, front + 0.25f, -0.9f, -0.5f, 0f, 0.5f, 0.9f, 1.6f, 2.6f, 4f, 6f, 8.5f, 11.5f, back };
            int rowStart = v.Count;
            foreach (float z in zs)
                for (int i = 0; i < cols; i++)
                {
                    float x = Mathf.Min(x1, x0 + i * step);
                    float h = 0f;
                    if (z > 1.2f) h = (N(x + o, z, 0.18f) - 0.35f) * 0.6f * Mathf.InverseLerp(1.2f, 5f, z); // gentle mounds behind
                    if (z < front + 0.3f) h = 0.04f;                                                          // lip roll
                    v.Add(new Vector3(x, h, z));
                    bool path = Mathf.Abs(z) < 0.7f;
                    float n = N(x + o, z, 0.9f);
                    Color col = path ? Color.Lerp(PathB, PathA, n) : Color.Lerp(GrassA, GrassB, N(x + o, z, 0.35f));
                    if (!path && n > 0.72f) col = Color.Lerp(col, GrassDark, 0.5f);
                    if (path && N(x * 2.3f + o, z * 2.3f, 1.7f) > 0.68f) col = Color.Lerp(col, RockA, 0.6f);   // flagstones
                    c.Add(col);
                }
            Grid(t, rowStart, zs.Length, cols, flip: false);

            // --- front face: grass lip drooping, then soil and rocks
            float[] ys = { 0.04f, -0.12f, -0.3f, -0.55f, -0.85f, -1.2f, -1.6f, -2.05f, -2.55f, -3.1f, -3.7f, -4.3f, bottom };
            int faceStart = v.Count;
            foreach (float y in ys)
                for (int i = 0; i < cols; i++)
                {
                    float x = Mathf.Min(x1, x0 + i * step);
                    float lipDepth = 0.28f + 0.32f * N(x + o, 3f, 1.3f);                  // how far the grass hangs down
                    float rock = N(x + o, y, 0.55f);
                    float bulge = rock > 0.58f ? (rock - 0.58f) * 1.6f : 0f;              // rocks push toward the camera
                    float z = front - 0.06f - 0.12f * N(x + o, y, 1.1f) - bulge;
                    if (y > -0.35f) z = front - 0.1f;                                      // lip overhang
                    v.Add(new Vector3(x, y, z));
                    Color col;
                    if (-y < lipDepth) col = Color.Lerp(GrassDark, GrassA, Mathf.InverseLerp(lipDepth, 0f, -y));
                    else if (bulge > 0.05f) col = Color.Lerp(RockB, RockA, N(x * 3f + o, y * 3f, 1f));
                    else col = Color.Lerp(SoilA, SoilB, N(x + o, y * 2f, 0.8f));
                    col = Color.Lerp(col, SoilDeep, Mathf.InverseLerp(-1.6f, bottom, y) * 0.75f);
                    if (-y > lipDepth && -y < lipDepth + 0.25f) col *= 0.82f;              // contact shade under the grass
                    c.Add(col);
                }
            Grid(t, faceStart, ys.Length, cols, flip: true);

            // --- end caps so the strip reads as a solid block at the edges
            Cap(v, c, t, x0, front, back, bottom, true);
            Cap(v, c, t, x1, front, back, bottom, false);

            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return Save(m, name);
        }

        private static void Grid(List<int> t, int start, int rows, int cols, bool flip)
        {
            for (int r = 0; r < rows - 1; r++)
                for (int i = 0; i < cols - 1; i++)
                {
                    int a = start + r * cols + i, b = a + 1, d = a + cols, e = d + 1;
                    if (!flip) { t.Add(a); t.Add(d); t.Add(b); t.Add(b); t.Add(d); t.Add(e); }
                    else { t.Add(a); t.Add(b); t.Add(d); t.Add(b); t.Add(e); t.Add(d); }
                }
        }

        private static void Cap(List<Vector3> v, List<Color> c, List<int> t, float x, float front, float back, float bottom, bool left)
        {
            int s = v.Count;
            v.Add(new Vector3(x, 0.04f, front)); v.Add(new Vector3(x, 0.04f, back)); v.Add(new Vector3(x, bottom, back)); v.Add(new Vector3(x, bottom, front));
            for (int i = 0; i < 4; i++) c.Add(i < 2 ? GrassA : SoilDeep);
            if (left) { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
            else { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
        }

        // ------------------------------------------------------------------ rounded box
        /// <summary>Box with bevelled (rounded) edges — reads as hand-made, takes the toon bands and outline cleanly.</summary>
        public static void AppendRoundedBox(List<Vector3> v, List<Color> c, List<int> t, Vector3 centre, Vector3 size, float bevel,
            Color col, Quaternion rot, int seg = 2)
        {
            Vector3 half = size * 0.5f;
            bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.95f);
            Vector3 inner = half - Vector3.one * bevel;
            int res = 2 + seg * 2; // samples per face edge
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in normals)
            {
                Vector3 u = n.x != 0 ? Vector3.forward : Vector3.right;
                Vector3 w = Vector3.Cross(n, u);
                int start = v.Count;
                for (int j = 0; j <= res; j++)
                    for (int i = 0; i <= res; i++)
                    {
                        float a = i / (float)res * 2f - 1f, b = j / (float)res * 2f - 1f;
                        Vector3 p = n + u * a + w * b;                       // point on the unit cube face
                        Vector3 q = Vector3.Scale(p, half);                   // on the box surface
                        Vector3 clamp = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                        Vector3 dir = q - clamp;
                        Vector3 surf = dir.sqrMagnitude > 1e-8f ? clamp + dir.normalized * bevel : q;
                        v.Add(centre + rot * surf);
                        c.Add(col);
                    }
                for (int j = 0; j < res; j++)
                    for (int i = 0; i < res; i++)
                    {
                        int a0 = start + j * (res + 1) + i, a1 = a0 + 1, b0 = a0 + res + 1, b1 = b0 + 1;
                        // Cross(u, w) = n: wind (a0, a1, b0) so Unity's face normal points outward.
                        t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0);
                    }
            }
        }

        public static Mesh Build(string name, System.Action<List<Vector3>, List<Color>, List<int>> fill)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            fill(v, c, t);
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return Save(m, name);
        }

        // ------------------------------------------------------------------ deck (planks + beams + curved-tile eave)
        public static Mesh Deck(string name, float width, float depth, bool redRoof, int seed)
        {
            var rng = new System.Random(seed);
            Color woodA = new(0.74f, 0.54f, 0.35f), woodB = new(0.64f, 0.45f, 0.29f), dark = new(0.42f, 0.29f, 0.2f);
            Color tileA = redRoof ? new Color(0.86f, 0.42f, 0.3f) : new Color(0.33f, 0.66f, 0.64f);
            Color tileB = redRoof ? new Color(0.7f, 0.32f, 0.24f) : new Color(0.24f, 0.52f, 0.52f);
            return Build(name, (v, c, t) =>
            {
                // planks run front-to-back, ~0.55 m wide, slight height/rotation jitter
                for (float x = -width * 0.5f + 0.3f; x < width * 0.5f; x += 0.56f)
                {
                    float jitter = (float)rng.NextDouble();
                    Color col = Color.Lerp(woodA, woodB, jitter);
                    AppendRoundedBox(v, c, t, new Vector3(x, -0.11f + jitter * 0.02f, depth * 0.5f - 0.6f), new Vector3(0.52f, 0.2f, depth),
                        0.05f, col, Quaternion.Euler(0f, (jitter - 0.5f) * 1.5f, 0f), 1);
                }
                // front and back beams
                AppendRoundedBox(v, c, t, new Vector3(0f, -0.3f, -0.62f), new Vector3(width + 0.2f, 0.26f, 0.22f), 0.06f, dark, Quaternion.identity, 1);
                AppendRoundedBox(v, c, t, new Vector3(0f, -0.3f, depth - 0.7f), new Vector3(width + 0.2f, 0.26f, 0.22f), 0.06f, dark, Quaternion.identity, 1);
                // curved-tile eave along the front: overlapping half-round tiles
                for (float x = -width * 0.5f - 0.2f; x < width * 0.5f + 0.2f; x += 0.42f)
                {
                    Color col = Color.Lerp(tileA, tileB, (float)rng.NextDouble() * 0.6f);
                    AppendRoundedBox(v, c, t, new Vector3(x, -0.5f, -0.86f), new Vector3(0.4f, 0.14f, 0.5f), 0.07f, col, Quaternion.Euler(-24f, 0f, 0f), 2);
                }
                AppendRoundedBox(v, c, t, new Vector3(0f, -0.4f, -0.68f), new Vector3(width + 0.5f, 0.08f, 0.1f), 0.03f, tileB * 0.8f, Quaternion.identity, 1);
            });
        }

        public static Mesh Post(string name, float height)
        {
            Color dark = new(0.42f, 0.29f, 0.2f), cap = new(0.32f, 0.22f, 0.16f);
            return Build(name, (v, c, t) =>
            {
                AppendRoundedBox(v, c, t, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.3f, height, 0.3f), 0.1f, dark, Quaternion.identity, 2);
                AppendRoundedBox(v, c, t, new Vector3(0f, height - 0.1f, 0f), new Vector3(0.42f, 0.18f, 0.42f), 0.06f, cap, Quaternion.identity, 1);
                AppendRoundedBox(v, c, t, new Vector3(0f, 0.08f, 0f), new Vector3(0.44f, 0.16f, 0.44f), 0.06f, new Color(0.6f, 0.58f, 0.56f), Quaternion.identity, 1);
            });
        }

        public static Mesh GrassTuft(string name, int seed)
        {
            var rng = new System.Random(seed);
            return Build(name, (v, c, t) =>
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = (float)rng.NextDouble() * 360f, h = 0.25f + (float)rng.NextDouble() * 0.25f;
                    Color col = Color.Lerp(GrassA, GrassB, (float)rng.NextDouble());
                    AppendRoundedBox(v, c, t, new Vector3(((float)rng.NextDouble() - 0.5f) * 0.4f, h * 0.45f, ((float)rng.NextDouble() - 0.5f) * 0.25f),
                        new Vector3(0.07f, h, 0.07f), 0.03f, col, Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 40f, a, ((float)rng.NextDouble() - 0.5f) * 40f), 1);
                }
            });
        }
    }
}
