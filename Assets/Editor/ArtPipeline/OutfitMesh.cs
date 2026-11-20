using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Mesh assembly helper for outfit pieces: positions in the hero body's mesh space, weights indexing the piece bone list.</summary>
    public sealed class OutfitMesh
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Color> C = new List<Color>();
        public readonly List<BoneWeight> W = new List<BoneWeight>();
        public readonly List<int> T = new List<int>();
        public bool ExplicitNormals;

        public int Count => V.Count;

        public int Add(Vector3 p, Vector2 uv, Color c, BoneWeight w, Vector3 n = default)
        {
            V.Add(p); UV.Add(uv); C.Add(c); W.Add(w); N.Add(n);
            return V.Count - 1;
        }

        public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        public void Append(OutfitMesh o)
        {
            int b = V.Count;
            V.AddRange(o.V); N.AddRange(o.N); UV.AddRange(o.UV); C.AddRange(o.C); W.AddRange(o.W);
            T.AddRange(o.T.Select(i => i + b));
        }

        /// <summary>Adds a back face copy (separate vertices, reversed winding) so thin sheets render from both sides.</summary>
        public void MakeDoubleSided(float pushBack = 0f)
        {
            int n = V.Count, tc = T.Count;
            var nrm = ComputeNormals();
            for (int i = 0; i < n; i++)
            {
                V.Add(V[i] - nrm[i] * pushBack); UV.Add(UV[i]); C.Add(C[i]); W.Add(W[i]); N.Add(-nrm[i]);
                N[i] = nrm[i];
            }
            for (int i = 0; i < tc; i += 3) Tri(T[i] + n, T[i + 2] + n, T[i + 1] + n);
            ExplicitNormals = true;
        }

        /// <summary>Flips triangles (from triStart) whose front face (Unity: Cross(b-a, c-a)) does not point along outward(centroid).</summary>
        public void Orient(int triStart, Func<Vector3, Vector3> outward)
        {
            for (int i = triStart; i + 2 < T.Count; i += 3)
            {
                Vector3 a = V[T[i]], b = V[T[i + 1]], c = V[T[i + 2]];
                Vector3 f = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(f, outward((a + b + c) / 3f)) < 0f) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
            }
        }

        public Vector3[] ComputeNormals()
        {
            var acc = new Vector3[V.Count];
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 f = Vector3.Cross(V[T[i + 1]] - V[T[i]], V[T[i + 2]] - V[T[i]]);
                acc[T[i]] += f; acc[T[i + 1]] += f; acc[T[i + 2]] += f;
            }
            for (int i = 0; i < acc.Length; i++) acc[i] = acc[i].sqrMagnitude > 0 ? acc[i].normalized : Vector3.up;
            return acc;
        }

        public Mesh ToMesh(string name, Matrix4x4[] bindposes)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetUVs(0, UV);
            m.SetColors(C);
            m.SetTriangles(T, 0);
            if (ExplicitNormals) m.SetNormals(N); else m.RecalculateNormals();
            m.boneWeights = W.ToArray();
            m.bindposes = bindposes;
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        // ------------------------------------------------------------------ weights
        public static BoneWeight Rigid(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        public static BoneWeight Blend(params (int bone, float w)[] parts)
        {
            var p = parts.Where(x => x.w > 1e-4f).GroupBy(x => x.bone).Select(g => (bone: g.Key, w: g.Sum(x => x.w)))
                .OrderByDescending(x => x.w).Take(4).ToArray();
            float s = p.Sum(x => x.w);
            if (p.Length == 0 || s <= 0) return Rigid(parts[0].bone);
            var bw = new BoneWeight();
            for (int i = 0; i < p.Length; i++)
            {
                float w = p[i].w / s;
                switch (i)
                {
                    case 0: bw.boneIndex0 = p[i].bone; bw.weight0 = w; break;
                    case 1: bw.boneIndex1 = p[i].bone; bw.weight1 = w; break;
                    case 2: bw.boneIndex2 = p[i].bone; bw.weight2 = w; break;
                    case 3: bw.boneIndex3 = p[i].bone; bw.weight3 = w; break;
                }
            }
            return bw;
        }

        // ------------------------------------------------------------------ primitives (all in mesh space)
        /// <summary>Ellipsoid: unit sphere transformed by m, vertex colour by local height (-1..1).</summary>
        public void Ellipsoid(Matrix4x4 m, int seg, int rings, Func<Vector3, Color> color, BoneWeight w, float vMin = -1f)
        {
            int b = V.Count;
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.Lerp(Mathf.Asin(Mathf.Clamp(vMin, -1f, 1f)), Mathf.PI / 2f, r / (float)rings);
                for (int s = 0; s <= seg; s++)
                {
                    float lon = s / (float)seg * Mathf.PI * 2f;
                    var local = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    Add(m.MultiplyPoint3x4(local), new Vector2(s / (float)seg, r / (float)rings), color(local), w);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = b + r * (seg + 1) + s, c = a + seg + 1;
                    Quad(a, c, c + 1, a + 1);
                }
        }

        /// <summary>Tube along a polyline (closed or open), radius per point, colour per point.</summary>
        public void Tube(IList<Vector3> path, IList<float> radius, bool closed, int sides, Func<int, float, Color> color,
            Func<int, BoneWeight> weight, Vector3 upHint, float flatten = 1f)
        {
            int n = path.Count, b = V.Count;
            int rows = closed ? n + 1 : n;
            for (int i = 0; i < rows; i++)
            {
                int k = i % n;
                Vector3 prev = path[(k - 1 + n) % n], next = path[(k + 1) % n];
                if (!closed) { if (k == 0) prev = path[0]; if (k == n - 1) next = path[n - 1]; }
                Vector3 tan = (next - prev).normalized;
                Vector3 side = Vector3.Cross(tan, upHint).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(tan, Vector3.right).normalized;
                Vector3 up = Vector3.Cross(side, tan).normalized;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 off = (side * Mathf.Cos(a) * flatten + up * Mathf.Sin(a)) * radius[k]; // flatten squashes across upHint
                    Add(path[k] + off, new Vector2(s / (float)sides, i / (float)(rows - 1)), color(k, a), weight(k));
                }
            }
            for (int i = 0; i < rows - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = b + i * (sides + 1) + s, c = a + sides + 1;
                    Quad(a, a + 1, c + 1, c);
                }
        }

        /// <summary>Surface of revolution around local +Y (profile: (radius, height)), transformed by m.</summary>
        public void Lathe(Matrix4x4 m, IList<Vector2> profile, int sides, Func<int, Color> color, BoneWeight w)
        {
            int b = V.Count;
            for (int i = 0; i < profile.Count; i++)
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Add(m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x)),
                        new Vector2(s / (float)sides, i / (float)(profile.Count - 1)), color(i), w);
                }
            for (int i = 0; i < profile.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = b + i * (sides + 1) + s, c = a + sides + 1;
                    Quad(a, c, c + 1, a + 1);
                }
        }

        /// <summary>Bipyramid crystal shard along local +Y.</summary>
        public void Shard(Matrix4x4 m, int sides, float baseFrac, Color tip, Color root, BoneWeight w)
        {
            // flat-shaded: separate vertices per face
            var ring = new Vector3[sides];
            for (int s = 0; s < sides; s++) { float a = s / (float)sides * Mathf.PI * 2f; ring[s] = new Vector3(Mathf.Cos(a), baseFrac, Mathf.Sin(a)); }
            Vector3 top = new Vector3(0, 1, 0), bottom = new Vector3(0, 0, 0);
            for (int s = 0; s < sides; s++)
            {
                Vector3 r0 = ring[s], r1 = ring[(s + 1) % sides];
                int a = Add(m.MultiplyPoint3x4(r0), new Vector2(0, 0.3f), Color.Lerp(root, tip, 0.4f), w);
                int c = Add(m.MultiplyPoint3x4(r1), new Vector2(1, 0.3f), Color.Lerp(root, tip, 0.4f), w);
                int t = Add(m.MultiplyPoint3x4(top), new Vector2(0.5f, 1), tip, w);
                Tri(a, t, c);
                int a2 = Add(m.MultiplyPoint3x4(r0), new Vector2(0, 0.3f), Color.Lerp(root, tip, 0.25f), w);
                int c2 = Add(m.MultiplyPoint3x4(r1), new Vector2(1, 0.3f), Color.Lerp(root, tip, 0.25f), w);
                int bt = Add(m.MultiplyPoint3x4(bottom), new Vector2(0.5f, 0), root, w);
                Tri(a2, c2, bt);
            }
        }
    }
}
