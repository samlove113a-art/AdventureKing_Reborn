using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Face layer for the Meshy heroes. Their rigs have no face bones, eyelids or blendshapes and the eyes/mouth
    /// are only painted into a fragmented texture atlas, so geometry shape keys would just smear paint. Instead:
    ///  1. read the bind-pose mesh, keep the head-weighted, forward-facing vertices (the real face surface);
    ///  2. rasterise that surface from the front with the hero's own texture and locate the painted eyes/mouth;
    ///  3. store each vertex's position on that frontal face plane in UV channel 3 (TEXCOORD3) at import time
    ///     (HeroFaceImport postprocessor) - the FBX file, rig and clips are untouched;
    ///  4. the Toon shader (_FACE keyword) keeps the painted eyes (identity), draws real eyelids + lash line over
    ///     them, and repaints the mouth procedurally, from continuous weights driven by HeroFace at runtime.
    /// </summary>
    public static class HeroFaceRig
    {
        const int R = 384;          // raster resolution of the face window
        static float Window = 1f;   // set per hero from the hips-to-head distance (mesh units are centimetres)
        const float WindowScale = 1.1f, CentreUp = 0.3f; // window = 1.1 x torso, centred 0.3 x torso above the head joint

        /// <summary>Everything the shader needs, in face units (face-plane position / hips-to-head distance).</summary>
        public sealed class FaceData
        {
            public Vector4 EyeA, EyeB;   // cx, cy, rx, ry
            public Vector4 Mouth;        // cx, cy, half width, half height
            public Color Skin, LidA, LidB, Lash, MouthLine;
        }

        static Vector2 ToFace(Vector2 px) => new Vector2((px.x / R - 0.5f) * WindowScale, (px.y / R - 0.5f) * WindowScale + CentreUp);

        /// <summary>Measures the painted face of a hero model (bind pose) with its own texture.</summary>
        public static FaceData Analyse(GameObject model, string texPath, out string error)
        {
            error = null;
            if (!File.Exists(texPath)) { error = "texture missing " + texPath; return null; }
            var (smr, mesh, origin, right, up, fwd, headBones) = Frame(model);
            float torso = HeadScale(mesh, smr);
            Window = torso * WindowScale;
            Texture2D tex = ReadableTexture(texPath);
            Raster r = RasterFace(mesh, tex, origin, right, up, fwd, headBones, new Vector2(0f, torso * CentreUp));
            Object.DestroyImmediate(tex);
            Features f = Detect(r);
            if (!f.Ok) { error = f.Error; return null; }
            Vector4 Box(Rect b, float growX = 1f, float growY = 1f)
            {
                Vector2 c = ToFace(b.center), e = ToFace(b.max) - ToFace(b.min);
                return new Vector4(c.x, c.y, e.x * 0.5f * growX, e.y * 0.5f * growY);
            }
            return new FaceData
            {
                EyeA = Box(f.EyeA), EyeB = Box(f.EyeB), Mouth = Box(f.Mouth),
                Skin = f.Skin, LidA = f.LidA, LidB = f.LidB, Lash = f.Lash, MouthLine = f.MouthLine,
            };
        }

        /// <summary>UV3 = (face x, face y, mask, 0) for every vertex; mask = forward-facing head surface.</summary>
        public static Vector4[] FaceCoordinates(GameObject model)
        {
            var (smr, mesh, origin, right, up, fwd, headBones) = Frame(model);
            float torso = HeadScale(mesh, smr);
            Vector3[] v = mesh.vertices; Vector3[] n = mesh.normals; BoneWeight[] bw = mesh.boneWeights;
            var uv = new Vector4[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                bool face = HeadWeight(bw[i], headBones) > 0.5f && Vector3.Dot(n[i], fwd) > 0.05f;
                uv[i] = face
                    ? new Vector4(Vector3.Dot(v[i] - origin, right) / torso, Vector3.Dot(v[i] - origin, up) / torso, 1f, 0f)
                    : new Vector4(50f, 50f, 0f, 0f); // far from every feature: the face layer never touches it
            }
            return uv;
        }

        /// <summary>Writes the measured face into a hero body material (enables the _FACE layer).</summary>
        public static void Apply(Material m, FaceData d)
        {
            m.EnableKeyword("_FACE");
            m.SetFloat("_FaceLayer", 1f);
            m.SetVector("_EyeA", d.EyeA);
            m.SetVector("_EyeB", d.EyeB);
            m.SetVector("_MouthRect", d.Mouth);
            m.SetColor("_FaceSkin", d.Skin);
            m.SetColor("_LidColorA", d.LidA);
            m.SetColor("_LidColorB", d.LidB);
            m.SetColor("_LashColor", d.Lash);
            m.SetColor("_MouthColor", d.MouthLine);
            EditorUtility.SetDirty(m);
        }

        [MenuItem("AdventureKing/QA (internal)/Diagnose Hero Faces")]
        public static void DiagnoseAll()
        {
            Directory.CreateDirectory("QA/Faces");
            foreach (var h in HeroArtDefinitions.Heroes) Diagnose($"{h.Folder}/{h.Fbx}", h.Class.ToString(), $"{h.Folder}/Textures/{h.Class}_Body_baseColor.png");
        }

        public static void DiagnoseFromCommandLine()
        {
            try { DiagnoseAll(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ measurement
        internal sealed class Raster
        {
            public Color[] Col = new Color[R * R];
            public float[] Depth = Enumerable.Repeat(float.MinValue, R * R).ToArray();
            public bool[] Face = new bool[R * R];
        }

        internal static (SkinnedMeshRenderer smr, Mesh mesh, Vector3 origin, Vector3 right, Vector3 up, Vector3 fwd, HashSet<int> headBones)
            Frame(GameObject model)
        {
            SkinnedMeshRenderer smr = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => !s.name.StartsWith("Weapon_"));
            Mesh mesh = smr.sharedMesh;
            int Bone(string n) => System.Array.FindIndex(smr.bones, b => b && b.name == n);
            Vector3 BindPos(int i) => mesh.bindposes[i].inverse.GetColumn(3); // mesh space
            int head = Bone("Head");
            if (head < 0) throw new System.InvalidOperationException("Head bone missing");
            var headBones = new HashSet<int> { head };
            for (int i = 0; i < smr.bones.Length; i++)
                for (Transform t = smr.bones[i]; t; t = t.parent)
                    if (t == smr.bones[head]) { headBones.Add(i); break; }
            Vector3 right = BindPos(Bone("RightUpLeg")) - BindPos(Bone("LeftUpLeg"));
            Vector3 up = (BindPos(head) - BindPos(Bone("Hips"))).normalized;
            right = Vector3.ProjectOnPlane(right, up).normalized;
            Vector3 fwd = Vector3.Cross(right, up).normalized;
            // Unity is left-handed: right x up = back. Make fwd point out of the face (verified below by normals).
            Vector3 origin = BindPos(head);
            // Check orientation with vertex normals of head vertices: most should face +fwd.
            var bw = mesh.boneWeights; var n = mesh.normals;
            float s = 0f;
            for (int v = 0; v < bw.Length; v++) if (HeadWeight(bw[v], headBones) > 0.5f) s += Vector3.Dot(n[v], fwd);
            if (s < 0f) { fwd = -fwd; }
            right = Vector3.Cross(up, fwd).normalized; // keep right-handedness consistent with image x
            return (smr, mesh, origin, right, up, fwd, headBones);
        }

        /// <summary>Hips-to-head distance in mesh units: the yardstick for everything face-sized.</summary>
        internal static float HeadScale(Mesh mesh, SkinnedMeshRenderer smr)
        {
            int Bone(string n) => System.Array.FindIndex(smr.bones, b => b && b.name == n);
            Vector3 P(int i) => mesh.bindposes[i].inverse.GetColumn(3);
            return Vector3.Distance(P(Bone("Head")), P(Bone("Hips")));
        }

        internal static float HeadWeight(BoneWeight w, HashSet<int> head)
        {
            float s = 0f;
            if (head.Contains(w.boneIndex0)) s += w.weight0;
            if (head.Contains(w.boneIndex1)) s += w.weight1;
            if (head.Contains(w.boneIndex2)) s += w.weight2;
            if (head.Contains(w.boneIndex3)) s += w.weight3;
            return s;
        }

        /// <summary>Front orthographic raster of the head with the hero texture; centre = head bone + offset.</summary>
        internal static Raster RasterFace(Mesh mesh, Texture2D tex, Vector3 origin, Vector3 right, Vector3 up, Vector3 fwd,
            HashSet<int> headBones, Vector2 centre)
        {
            var r = new Raster();
            Vector3[] v = mesh.vertices; Vector2[] uv = mesh.uv; Vector3[] nrm = mesh.normals; BoneWeight[] bw = mesh.boneWeights;
            int[] tri = mesh.triangles;
            Vector2 P(Vector3 p) => new Vector2((Vector3.Dot(p - origin, right) - centre.x) / Window + 0.5f, (Vector3.Dot(p - origin, up) - centre.y) / Window + 0.5f) * R;
            for (int t = 0; t < tri.Length; t += 3)
            {
                int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                Vector2 pa = P(v[a]), pb = P(v[b]), pc = P(v[c]);
                float area = (pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y);
                if (Mathf.Abs(area) < 1e-6f) continue;
                bool face = HeadWeight(bw[a], headBones) > 0.5f && HeadWeight(bw[b], headBones) > 0.5f && HeadWeight(bw[c], headBones) > 0.5f
                            && Vector3.Dot((nrm[a] + nrm[b] + nrm[c]).normalized, fwd) > 0.2f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)))), x1 = Mathf.Min(R - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)))), y1 = Mathf.Min(R - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
                float da = Vector3.Dot(v[a] - origin, fwd), db = Vector3.Dot(v[b] - origin, fwd), dc = Vector3.Dot(v[c] - origin, fwd);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float w0 = ((pb.x - p.x) * (pc.y - p.y) - (pc.x - p.x) * (pb.y - p.y)) / area;
                        float w1 = ((pc.x - p.x) * (pa.y - p.y) - (pa.x - p.x) * (pc.y - p.y)) / area;
                        float w2 = 1f - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        float d = da * w0 + db * w1 + dc * w2;
                        int k = y * R + x;
                        if (d <= r.Depth[k]) continue;
                        r.Depth[k] = d;
                        Vector2 u = uv[a] * w0 + uv[b] * w1 + uv[c] * w2;
                        r.Col[k] = tex.GetPixelBilinear(u.x, u.y);
                        r.Face[k] = face;
                    }
            }
            return r;
        }

        // ------------------------------------------------------------------ feature detection
        public struct Features
        {
            public Rect EyeA, EyeB, Mouth;      // raster pixels
            public Color Skin, LidA, LidB, Lash, MouthLine;
            public bool Ok;
            public string Error;
        }

        static float Lum(Color c) => 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;

        static float Dist(Color a, Color b) => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        static List<List<int>> Components(bool[] on)
        {
            var label = new int[R * R];
            var list = new List<List<int>>();
            var stack = new Stack<int>();
            for (int s = 0; s < R * R; s++)
            {
                if (!on[s] || label[s] != 0) continue;
                var comp = new List<int>();
                label[s] = list.Count + 1;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int k = stack.Pop(); comp.Add(k);
                    int x = k % R, y = k / R;
                    void Try(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= R || ny >= R) return;
                        int q = ny * R + nx;
                        if (!on[q] || label[q] != 0) return;
                        label[q] = list.Count + 1; stack.Push(q);
                    }
                    Try(x + 1, y); Try(x - 1, y); Try(x, y + 1); Try(x, y - 1);
                }
                list.Add(comp);
            }
            return list;
        }

        static Rect Bounds(IEnumerable<int> px)
        {
            int x0 = R, y0 = R, x1 = -1, y1 = -1;
            foreach (int k in px) { int x = k % R, y = k / R; x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            return Rect.MinMaxRect(x0, y0, x1 + 1, y1 + 1);
        }

        static Color Mean(IEnumerable<Color> cs)
        {
            Color s = Color.clear; int n = 0;
            foreach (var c in cs) { s += c; n++; }
            return n > 0 ? s / n : Color.magenta;
        }

        internal static Features Detect(Raster r)
        {
            var f = new Features();
            bool Has(int k) => r.Depth[k] != float.MinValue && r.Face[k];
            // 1. skin: warm, light, moderately saturated face pixels (median per channel)
            var skinPx = new List<Color>();
            for (int k = 0; k < R * R; k++)
            {
                if (!Has(k)) continue;
                Color.RGBToHSV(r.Col[k], out float h, out float s, out float v);
                if (v > 0.78f && s > 0.12f && s < 0.42f && (h < 0.11f || h > 0.97f)) skinPx.Add(r.Col[k]);
            }
            if (skinPx.Count < 500) { f.Error = "too little skin"; return f; }
            float Med(System.Func<Color, float> ch) { var l = skinPx.Select(ch).OrderBy(x => x).ToList(); return l[l.Count / 2]; }
            f.Skin = new Color(Med(c => c.r), Med(c => c.g), Med(c => c.b), 1f);

            // 2. eyes: the two largest sclera (bright, unsaturated) blobs, grown into the connected non-skin paint
            var sclera = new bool[R * R];
            var nonSkin = new bool[R * R];
            for (int k = 0; k < R * R; k++)
            {
                if (!Has(k)) continue;
                Color.RGBToHSV(r.Col[k], out _, out float s, out float v);
                sclera[k] = v > 0.86f && s < 0.13f;
                nonSkin[k] = Dist(r.Col[k], f.Skin) > 0.2f;
            }
            // The iris splits each white into several blobs: merge all sizeable blobs per side of the face.
            var all = Components(sclera).Where(c => c.Count > 25).ToList();
            var leftSide = all.Where(c => Bounds(c).center.x < R * 0.5f).OrderByDescending(c => c.Count).ToList();
            var rightSide = all.Where(c => Bounds(c).center.x >= R * 0.5f).OrderByDescending(c => c.Count).ToList();
            if (leftSide.Count == 0 || rightSide.Count == 0) { f.Error = "eyes not found"; return f; }
            List<int> Merge(List<List<int>> side)
            {
                Rect main = Bounds(side[0]);
                // only blobs at the same height as the biggest one (not a white hair highlight above)
                return side.Where(c => Mathf.Abs(Bounds(c).center.y - main.center.y) < main.height * 1.2f + 6f).SelectMany(c => c).ToList();
            }
            var blobs = new List<List<int>> { Merge(leftSide), Merge(rightSide) };
            Rect GrowEye(List<int> blob)
            {
                Rect b = Bounds(blob);
                Rect lim = Rect.MinMaxRect(b.xMin - b.width * 0.35f, b.yMin - b.height * 0.8f, b.xMax + b.width * 0.35f, b.yMax + b.height * 0.8f);
                var seen = new HashSet<int>(blob);
                var st = new Stack<int>(blob);
                while (st.Count > 0)
                {
                    int k = st.Pop(); int x = k % R, y = k / R;
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (nx < lim.xMin || ny < lim.yMin || nx >= lim.xMax || ny >= lim.yMax) continue;
                        if (nx < 0 || ny < 0 || nx >= R || ny >= R) continue; // eye near the raster edge (wide-faced NPCs)
                        int q = ny * R + nx;
                        if (seen.Contains(q) || !(nonSkin[q] || sclera[q])) continue;
                        seen.Add(q); st.Push(q);
                    }
                }
                return Bounds(seen);
            }
            Rect ea = GrowEye(blobs[0]), eb = GrowEye(blobs[1]);
            if (ea.center.x > eb.center.x) (ea, eb) = (eb, ea);
            f.EyeA = ea; f.EyeB = eb;
            List<Color> In(Rect rc, System.Func<Color, bool> pred)
            {
                var l = new List<Color>();
                for (int y = (int)rc.yMin; y < (int)rc.yMax; y++)
                    for (int x = (int)rc.xMin; x < (int)rc.xMax; x++)
                    {
                        if (x < 0 || y < 0 || x >= R || y >= R) continue;
                        int k = y * R + x;
                        if (Has(k) && pred(r.Col[k])) l.Add(r.Col[k]);
                    }
                return l;
            }
            f.Lash = Mean(In(ea, c => true).Concat(In(eb, c => true)).OrderBy(c => c.r + c.g + c.b).Take(40));
            // Lid colour: skin just above each eye (where the lid would be), skin-classified only.
            Color Lid(Rect e)
            {
                var l = In(Rect.MinMaxRect(e.xMin, e.yMax, e.xMax, e.yMax + e.height * 0.45f), c => Dist(c, f.Skin) < 0.12f);
                if (l.Count < 10) l = In(Rect.MinMaxRect(e.xMin, e.yMin - e.height * 0.4f, e.xMax, e.yMin), c => Dist(c, f.Skin) < 0.12f);
                return l.Count > 0 ? Mean(l) : f.Skin;
            }
            f.LidA = Lid(ea); f.LidB = Lid(eb);

            // 3. mouth: the widest dark-ish non-skin stroke below the eyes, between the eye centres
            float eyeH = Mathf.Max(ea.height, eb.height);
            float below = Mathf.Min(ea.yMin, eb.yMin);
            // Painted mouths are thin lines 0.3–1.6 eye-heights under the eyes; the scarf/collar is further down.
            // Measured on all four heroes: the mouth sits 0.7–1.3 eye-heights under the eyes; the jaw line is lower.
            Rect search = Rect.MinMaxRect(ea.center.x, below - eyeH * 1.45f, eb.center.x, below - eyeH * 0.35f);
            Color.RGBToHSV(f.Skin, out _, out _, out float skinV);
            var mouthOn = new bool[R * R];
            for (int y = Mathf.Max(0, (int)search.yMin); y < Mathf.Min(R, (int)search.yMax); y++)
                for (int x = Mathf.Max(0, (int)search.xMin); x < Mathf.Min(R, (int)search.xMax); x++)
                {
                    int k = y * R + x;
                    if (!Has(k)) continue;
                    // Pale pink mouth lines barely change HSV value (red stays ~1); luminance does drop.
                    mouthOn[k] = Lum(r.Col[k]) < Lum(f.Skin) - 0.065f;
                }
            var pieces = Components(mouthOn).Where(c => c.Count >= 4).Select(c => (c, b: Bounds(c))).ToList();
            var mouths = pieces.Where(m => m.b.width > m.b.height * 2f && m.b.width > eyeH * 0.2f)
                .OrderByDescending(m => m.b.width).ToList(); // the mouth is always wider than the nose
            if (mouths.Count == 0) { f.Error = "mouth not found"; return f; }
            // Faint strokes break into pieces (a smirk's tail): merge other horizontal strokes on the same line.
            Rect m0 = mouths[0].b;
            var line = pieces.Where(p => p.b.width > p.b.height * 1.5f &&
                                         p.b.yMax > m0.yMin - eyeH * 0.1f && p.b.yMin < m0.yMax + eyeH * 0.1f).ToList();
            f.Mouth = Bounds(line.SelectMany(p => p.c));
            f.MouthLine = Mean(line.SelectMany(p => p.c).Select(k => r.Col[k]).OrderBy(c => c.r + c.g + c.b).Take(20));
            f.Ok = true;
            return f;
        }

        internal static Texture2D ReadableTexture(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        static void Diagnose(string fbx, string name, string texPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var (smr, mesh, origin, right, up, fwd, headBones) = Frame(model);
            Texture2D tex = ReadableTexture(texPath);
            float torso = HeadScale(mesh, smr);
            Window = torso * WindowScale;
            // Window centred above the head joint (chibi faces sit high in the skull).
            Raster r = RasterFace(mesh, tex, origin, right, up, fwd, headBones, new Vector2(0f, torso * CentreUp));
            var img = new Texture2D(R, R, TextureFormat.RGB24, false);
            for (int k = 0; k < R * R; k++)
            {
                Color c = r.Depth[k] == float.MinValue ? new Color(0.2f, 0.2f, 0.25f) : r.Col[k];
                if (r.Depth[k] != float.MinValue && !r.Face[k]) c = Color.Lerp(c, Color.black, 0.45f); // not face
                img.SetPixel(k % R, k / R, c);
            }
            Features f = Detect(r);
            void Box(Rect b, Color c)
            {
                for (int x = (int)b.xMin; x <= (int)b.xMax; x++) { Put(x, (int)b.yMin, c); Put(x, (int)b.yMax, c); }
                for (int y = (int)b.yMin; y <= (int)b.yMax; y++) { Put((int)b.xMin, y, c); Put((int)b.xMax, y, c); }
            }
            void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < R && y < R) img.SetPixel(x, y, c); }
            if (f.Ok) { Box(f.EyeA, Color.green); Box(f.EyeB, Color.green); Box(f.Mouth, Color.cyan); }
            for (int i = 0; i < 24; i++) for (int j = 0; j < 24; j++)
            {
                Put(i, j, f.Skin); Put(24 + i, j, f.LidA); Put(48 + i, j, f.LidB); Put(72 + i, j, f.Lash); Put(96 + i, j, f.MouthLine);
            }
            img.Apply();
            Debug.Log($"[FACE] {name}: detect ok={f.Ok} {f.Error} eyes {f.EyeA} {f.EyeB} mouth {f.Mouth}");
            File.WriteAllBytes($"QA/Faces/diag_{name}.png", img.EncodeToPNG());
            Debug.Log($"[FACE] {name}: head origin {origin}, fwd {fwd}, up {up}, headBones {headBones.Count}, scale lossy {model.transform.lossyScale}");
            Object.DestroyImmediate(img); Object.DestroyImmediate(tex);
        }
    }
}
