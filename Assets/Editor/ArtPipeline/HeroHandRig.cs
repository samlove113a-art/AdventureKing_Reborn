using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Finger rig for the Meshy heroes (their skeletons end at the wrist). Step 1 here is a diagnosis: rasterise the
    /// bind-pose hand (vertices weighted to the hand bone) from three directions with the hero's own texture.
    /// </summary>
    public static class HeroHandRig
    {
        const int R = 320;

        public static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        public const int Joints = 3; // proximal, intermediate, distal

        /// <summary>
        /// Adds finger bones to one hand of an imported hero model and re-skins the finger vertices to them.
        /// Fingers are found as the 5 separate mesh pieces left when the hand is cut across at the right height
        /// (scanned from the fingertips down); each gets 3 joints along its own axis. Returns a log line.
        /// </summary>
        public static string AddFingers(SkinnedMeshRenderer smr, string handName)
        {
            Mesh mesh = smr.sharedMesh;
            Transform[] bones = smr.bones;
            int hi = System.Array.FindIndex(bones, b => b && b.name == handName);
            int fi = System.Array.FindIndex(bones, b => b && b.name == handName.Replace("Hand", "ForeArm"));
            if (hi < 0 || fi < 0) return handName + ": no hand/forearm bone";
            if (bones.Any(b => b && b.name == $"{handName}_Index1")) return handName + ": fingers already present";
            Matrix4x4[] bind = mesh.bindposes;
            Vector3 wrist = bind[hi].inverse.GetColumn(3), elbow = bind[fi].inverse.GetColumn(3);
            Vector3 along = (wrist - elbow).normalized;
            Vector3[] v = mesh.vertices; BoneWeight[] bw = mesh.boneWeights; int[] tri = mesh.triangles;
            float HandW(BoneWeight w) => (w.boneIndex0 == hi ? w.weight0 : 0) + (w.boneIndex1 == hi ? w.weight1 : 0) +
                                         (w.boneIndex2 == hi ? w.weight2 : 0) + (w.boneIndex3 == hi ? w.weight3 : 0);
            // ---- welded adjacency of the hand vertices (UV seams duplicate vertices)
            var weld = new Dictionary<Vector3Int, int>();
            var rep = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 200f);
                if (!weld.TryGetValue(key, out int r)) { r = i; weld[key] = i; }
                rep[i] = r;
            }
            var hand = new HashSet<int>(Enumerable.Range(0, v.Length).Where(i => HandW(bw[i]) > 0.5f).Select(i => rep[i]));
            var adj = new Dictionary<int, HashSet<int>>();
            void Link(int a, int b) { if (!adj.TryGetValue(a, out var s)) adj[a] = s = new HashSet<int>(); s.Add(b); }
            for (int t = 0; t < tri.Length; t += 3)
            {
                int a = rep[tri[t]], b = rep[tri[t + 1]], c = rep[tri[t + 2]];
                if (!hand.Contains(a) || !hand.Contains(b) || !hand.Contains(c)) continue;
                Link(a, b); Link(b, a); Link(b, c); Link(c, b); Link(c, a); Link(a, c);
            }
            // Distance across the hand surface from the wrist (Dijkstra over the welded mesh): fingertips and the
            // thumb tip are the far ends whatever direction they point, so one threshold isolates all five.
            float Along(int i) => Vector3.Dot(v[i] - wrist, along);
            float minAlong = hand.Min(Along);
            var geo = new Dictionary<int, float>();
            var pq = new SortedSet<(float d, int i)>();
            foreach (int i in hand) if (Along(i) < minAlong + 0.6f * (hand.Max(Along) - minAlong) * 0.1f) { geo[i] = 0f; pq.Add((0f, i)); }
            while (pq.Count > 0)
            {
                var (d, x) = pq.Min; pq.Remove(pq.Min);
                if (d > geo[x]) continue;
                if (!adj.TryGetValue(x, out var ns)) continue;
                foreach (int n in ns)
                {
                    float nd = d + Vector3.Distance(v[x], v[n]);
                    if (geo.TryGetValue(n, out float old) && old <= nd) continue;
                    if (geo.ContainsKey(n)) pq.Remove((old, n));
                    geo[n] = nd; pq.Add((nd, n));
                }
            }
            float A(int i) => geo.TryGetValue(i, out float g) ? g : 0f;
            float aMax = hand.Max(A);
            List<List<int>> Pieces(float tau)
            {
                var seen = new HashSet<int>(); var list = new List<List<int>>();
                foreach (int s in hand)
                {
                    if (A(s) <= tau || seen.Contains(s)) continue;
                    var comp = new List<int>(); var st = new Stack<int>(); st.Push(s); seen.Add(s);
                    while (st.Count > 0)
                    {
                        int x = st.Pop(); comp.Add(x);
                        if (adj.TryGetValue(x, out var ns)) foreach (int n in ns) if (!seen.Contains(n) && A(n) > tau) { seen.Add(n); st.Push(n); }
                    }
                    if (comp.Count >= 6) list.Add(comp);
                }
                return list;
            }
            // Merge tree (persistence): sweep the surface distance downwards; every fingertip starts a region, regions
            // grow towards the palm, and where two meet (the webbing) the younger one is a finished finger. Short
            // regions (glove seams, knuckle bumps) are noise. Copes with fingers of different lengths and the thumb.
            var order = hand.OrderByDescending(A).ToList();
            var parentOf = new Dictionary<int, int>(); var birth = new Dictionary<int, float>();
            var members = new Dictionary<int, List<int>>(); var firstSig = new Dictionary<int, float>();
            var claimed = new HashSet<int>();
            var found = new List<(List<int> verts, float cut)>();
            float minPers = aMax * 0.07f; // shorter chibi pinkies/thumbs are ~8-10% of the hand path
            int Find(int x) { while (parentOf[x] != x) { parentOf[x] = parentOf[parentOf[x]]; x = parentOf[x]; } return x; }
            void Record(int r, float level)
            {
                float cut = firstSig.TryGetValue(r, out float fs) ? fs : level;
                var verts = members[r].Where(i => A(i) > cut && !claimed.Contains(i)).ToList();
                if (verts.Count < 6) return;
                foreach (int i in verts) claimed.Add(i);
                found.Add((verts, cut));
            }
            foreach (int x in order)
            {
                var roots = new HashSet<int>();
                if (adj.TryGetValue(x, out var ns)) foreach (int n in ns) if (parentOf.ContainsKey(n)) roots.Add(Find(n));
                if (roots.Count == 0) { parentOf[x] = x; birth[x] = A(x); members[x] = new List<int> { x }; continue; }
                int elder = roots.OrderByDescending(r => birth[r]).First();
                foreach (int r in roots)
                {
                    if (r == elder) continue;
                    if (birth[r] - A(x) >= minPers)
                    {
                        Record(r, A(x));
                        if (!firstSig.ContainsKey(elder)) firstSig[elder] = A(x);
                    }
                    parentOf[r] = elder;
                    members[elder].AddRange(members[r]);
                }
                parentOf[x] = elder;
                members[elder].Add(x);
            }
            int last = Find(order[0]);
            if (firstSig.ContainsKey(last)) Record(last, firstSig[last]);

            // Meshy fingers are fused along their sides almost to the tips, so topology alone only finds tips.
            // Profile segmentation: across the hand width, the reach (max distance along the hand) peaks at each
            // fingertip with valleys between fingers; everything past the knuckle line is split at the valleys.
            var handList = hand.ToList();
            Vector3 mean = Centroid(handList);
            // palm normal = smallest-variance axis of the hand (power iteration on the inverse is overkill: test 3 candidates)
            Vector3 c1 = Vector3.Cross(along, Vector3.up), c2 = Vector3.Cross(along, Vector3.forward), c3 = Vector3.Cross(along, Vector3.right);
            Vector3 PickNormal()
            {
                Vector3 bestN = c1; float bestVar = float.MaxValue;
                for (int k = 0; k < 36; k++)
                {
                    Vector3 basis = c1.sqrMagnitude > 0.1f ? c1.normalized : c2.normalized;
                    Vector3 other = Vector3.Cross(along, basis).normalized;
                    float ang = k * 5f * Mathf.Deg2Rad;
                    Vector3 n = basis * Mathf.Cos(ang) + other * Mathf.Sin(ang);
                    float var = handList.Sum(i => { float d = Vector3.Dot(v[i] - mean, n); return d * d; });
                    if (var < bestVar) { bestVar = var; bestN = n; }
                }
                return bestN.normalized;
            }
            Vector3 palmN = PickNormal();
            Vector3 spread = Vector3.Cross(palmN, along).normalized;
            float S(int i) => Vector3.Dot(v[i] - wrist, spread);
            float sMin = handList.Min(S), sMax = handList.Max(S);
            const int Bins = 72;
            var reach = Enumerable.Repeat(float.MinValue, Bins).ToArray();
            int Bin(float s) => Mathf.Clamp((int)((s - sMin) / (sMax - sMin + 1e-6f) * Bins), 0, Bins - 1);
            foreach (int i in handList) { int b = Bin(S(i)); reach[b] = Mathf.Max(reach[b], Along(i)); }
            for (int b = 0; b < Bins; b++) if (reach[b] == float.MinValue) reach[b] = minAlong;
            var smooth = new float[Bins];
            for (int b = 0; b < Bins; b++) { float s = 0; int n = 0; for (int k = 0; k <= 0; k++) if (b + k >= 0 && b + k < Bins) { s += reach[b + k]; n++; } smooth[b] = s / n; }
            float handLength = hand.Max(Along) - minAlong;
            var peaks = new List<int>();
            for (int b = 1; b < Bins - 1; b++)
                if (smooth[b] >= smooth[b - 1] && smooth[b] > smooth[b + 1])
                {
                    // prominence: drop to the lowest point before a higher peak on each side
                    float left = smooth[b], right = smooth[b];
                    for (int k = b - 1; k >= 0 && smooth[k] <= smooth[b]; k--) left = Mathf.Min(left, smooth[k]);
                    for (int k = b + 1; k < Bins && smooth[k] <= smooth[b]; k++) right = Mathf.Min(right, smooth[k]);
                    if (smooth[b] - Mathf.Max(left, right) > handLength * 0.01f || (b == 1 || b == Bins - 2)) peaks.Add(b);
                }
            peaks = peaks.Where(p => smooth[p] > minAlong + handLength * 0.45f).ToList();
            if (peaks.Count < 3)
                return handName + $": fingertips not found (peaks {peaks.Count})";
            if (peaks.Count > 5) peaks = peaks.OrderByDescending(p => smooth[p]).Take(5).OrderBy(p => p).ToList();
            // valleys between consecutive peaks
            var cuts = new List<float> { sMin - 1f };
            for (int k = 0; k + 1 < peaks.Count; k++)
            {
                int vb = peaks[k]; for (int b = peaks[k]; b <= peaks[k + 1]; b++) if (smooth[b] < smooth[vb]) vb = b;
                cuts.Add(sMin + (vb + 0.5f) / Bins * (sMax - sMin));
            }
            cuts.Add(sMax + 1f);
            // thumb: an end peak clearly shorter than its neighbour (when 5 peaks)
            int thumbPeak = -1;
            if (peaks.Count == 5) thumbPeak = smooth[peaks[0]] < smooth[peaks[4]] ? 0 : 4;
            float knuckle = minAlong + handLength * 0.5f, thumbBase = minAlong + handLength * 0.22f;
            var best = new List<List<int>>();
            for (int k = 0; k < peaks.Count; k++)
            {
                float lo = cuts[k], hi2 = cuts[k + 1], from = k == thumbPeak ? thumbBase : knuckle;
                best.Add(handList.Where(i => S(i) > lo && S(i) <= hi2 && Along(i) > from).ToList());
            }
            Debug.Log($"[HAND] {handName}: {peaks.Count} fingertip peaks across the hand, thumb peak {thumbPeak}");

            Vector3 Centroid(IEnumerable<int> ids) { Vector3 s = Vector3.zero; int n = 0; foreach (int i in ids) { s += v[i]; n++; } return n > 0 ? s / n : wrist; }
            // ---- finger axes: base = centroid of the strip's lowest band, tip = its farthest point
            var fingers = best.Where(c => c.Count >= 6).Select(c =>
            {
                float minA = c.Min(Along);
                Vector3 baseP = Centroid(c.Where(i => Along(i) < minA + handLength * 0.06f));
                Vector3 tip = v[c.OrderByDescending(i => (v[i] - baseP).sqrMagnitude).First()];
                return (verts: c, baseP, tip, minA);
            }).ToList();
            var thumb = thumbPeak >= 0 && thumbPeak < fingers.Count ? fingers[thumbPeak] : default;
            var rest = fingers.Where(f => f.verts != thumb.verts).ToList();
            Vector3 spreadAxis = (rest.Last().baseP - rest.First().baseP).normalized;
            if (thumb.verts != null && Vector3.Dot(thumb.baseP - rest[0].baseP, spreadAxis) > 0f) spreadAxis = -spreadAxis;
            rest = rest.OrderBy(f => Vector3.Dot(f.baseP - wrist, spreadAxis)).ToList(); // index .. pinky away from the thumb
            // Decision (QA/Hands/segmentation_all.png): Meshy hands are mittens - the four fingers are fused along their
            // whole length, so per-finger splits give speckled weights and would tear. The four fingers are rigged as
            // one block with knuckle / middle / tip joints (clean curl, no tearing); the thumb stays with the hand.
            float blockFrom = minAlong + handLength * 0.55f;
            // re-skinned region reaches into the palm so the blend fades to 100% hand at its edge (no weight seam)
            var block = handList.Where(i => Along(i) > minAlong + handLength * 0.36f).ToList();
            float topAlong = block.Max(Along);
            Vector3 blockBase = Centroid(block.Where(i => Along(i) > blockFrom && Along(i) < blockFrom + handLength * 0.05f));
            Vector3 blockTip = Centroid(block.Where(i => Along(i) > topAlong - handLength * 0.06f));
            var ordered = new List<(string name, List<int> verts, Vector3 baseP, Vector3 tip)> { ("Fingers", block, blockBase, blockTip) };

            // ---- joints: base (webbing), 40% and 72% of the finger; frames aligned with the hand bone's bind frame
            Matrix4x4 handBind = bind[hi].inverse; // hand bone -> mesh space at bind
            Transform handBone = bones[hi];
            var newBones = new List<Transform>(); var newBind = new List<Matrix4x4>();
            var fingerOfRep = new Dictionary<int, (int finger, float t)>();
            int baseIndex = bones.Length;
            for (int f = 0; f < ordered.Count; f++)
            {
                var (name, verts, baseP, tip) = ordered[f];
                Vector3 dir = tip - baseP; float len = dir.magnitude; dir /= Mathf.Max(1e-5f, len);
                // pull the knuckle slightly into the palm so the bend reads as a knuckle, not the webbing
                Vector3 k0 = baseP - dir * len * 0.12f;
                float[] at = { 0f, 0.4f, 0.72f };
                Transform parent = handBone;
                Matrix4x4 parentBind = handBind;
                for (int j = 0; j < Joints; j++)
                {
                    Vector3 p = k0 + dir * (len * 1.12f) * at[j];
                    Matrix4x4 jointBind = Matrix4x4.TRS(p, handBind.rotation, Vector3.one); // mesh space, hand's orientation
                    var go = new GameObject($"{handName}_{name}{j + 1}");
                    go.transform.SetParent(parent, false);
                    Matrix4x4 local = parentBind.inverse * jointBind;
                    go.transform.localPosition = local.GetColumn(3);
                    go.transform.localRotation = local.rotation;
                    go.transform.localScale = local.lossyScale;
                    newBones.Add(go.transform);
                    newBind.Add(jointBind.inverse);
                    parent = go.transform; parentBind = jointBind;
                }
                foreach (int r in verts)
                {
                    float t = Mathf.Clamp01(Vector3.Dot(v[r] - k0, dir) / (len * 1.12f));
                    fingerOfRep[r] = (f, t);
                }
            }
            // ---- weights: the hand's share of each finger vertex moves to its joints (smooth blends), others kept
            var newW = (BoneWeight[])bw.Clone();
            int changed = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (!fingerOfRep.TryGetValue(rep[i], out var ft)) continue;
                var list = new List<(int bone, float w)>();
                void Add(int b, float w) { if (w > 0f) list.Add((b, w)); }
                float handShare = 0f;
                foreach (var (b, w) in new[] { (bw[i].boneIndex0, bw[i].weight0), (bw[i].boneIndex1, bw[i].weight1), (bw[i].boneIndex2, bw[i].weight2), (bw[i].boneIndex3, bw[i].weight3) })
                    if (b == hi) handShare += w; else Add(b, w);
                int j0 = baseIndex + ft.finger * Joints;
                float t = ft.t;
                // Wide, smooth blend zones (QA/Hands/play_hands.png: narrow ones folded the fused skin into dark
                // creases): hand->proximal 0..0.32, proximal->intermediate 0.3..0.56, intermediate->distal 0.6..0.84.
                float wh = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.32f, t));
                float w2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.56f, t)), w3 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.84f, t));
                float wp = (1f - wh) * (1f - w2), wi = (1f - wh) * w2 * (1f - w3), wd = (1f - wh) * w2 * w3;
                Add(hi, handShare * wh); Add(j0, handShare * wp); Add(j0 + 1, handShare * wi); Add(j0 + 2, handShare * wd);
                list = list.OrderByDescending(x => x.w).Take(4).ToList();
                float sum = list.Sum(x => x.w);
                var nb = new BoneWeight();
                for (int k = 0; k < list.Count; k++)
                {
                    float w = list[k].w / sum;
                    switch (k)
                    {
                        case 0: nb.boneIndex0 = list[k].bone; nb.weight0 = w; break;
                        case 1: nb.boneIndex1 = list[k].bone; nb.weight1 = w; break;
                        case 2: nb.boneIndex2 = list[k].bone; nb.weight2 = w; break;
                        default: nb.boneIndex3 = list[k].bone; nb.weight3 = w; break;
                    }
                }
                newW[i] = nb;
                changed++;
            }
            smr.bones = bones.Concat(newBones).ToArray();
            mesh.bindposes = bind.Concat(newBind).ToArray();
            mesh.boneWeights = newW;
            return $"{handName}: {ordered.Count} fingers ({string.Join(",", ordered.Select(o => o.name + ":" + o.verts.Count))}), {changed} vertices re-skinned";
        }

        public static void DiagnoseFromCommandLine()
        {
            try
            {
                Directory.CreateDirectory("QA/Hands");
                foreach (var h in HeroArtDefinitions.Heroes)
                    foreach (string hand in new[] { "RightHand", "LeftHand" })
                        Diagnose(h, hand);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Diagnose(HeroArtDefinitions.Hero h, string handName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{h.Folder}/{h.Fbx}");
            SkinnedMeshRenderer smr = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => !s.name.StartsWith("Weapon_"));
            Mesh mesh = smr.sharedMesh;
            int hi = System.Array.FindIndex(smr.bones, b => b && b.name == handName);
            int fi = System.Array.FindIndex(smr.bones, b => b && b.name == handName.Replace("Hand", "ForeArm"));
            Vector3 wrist = mesh.bindposes[hi].inverse.GetColumn(3), elbow = mesh.bindposes[fi].inverse.GetColumn(3);
            BoneWeight[] bw = mesh.boneWeights; Vector3[] v = mesh.vertices; Vector2[] uv = mesh.uv; int[] tri = mesh.triangles;
            var handSet = new HashSet<int>(Enumerable.Range(0, smr.bones.Length).Where(k => smr.bones[k] &&
                (smr.bones[k].name == handName || smr.bones[k].name.StartsWith(handName + "_"))));
            float W(int i) => (handSet.Contains(bw[i].boneIndex0) ? bw[i].weight0 : 0) + (handSet.Contains(bw[i].boneIndex1) ? bw[i].weight1 : 0) +
                              (handSet.Contains(bw[i].boneIndex2) ? bw[i].weight2 : 0) + (handSet.Contains(bw[i].boneIndex3) ? bw[i].weight3 : 0);
            var verts = Enumerable.Range(0, v.Length).Where(i => W(i) > 0.3f).ToList();
            Bounds b = new Bounds(v[verts[0]], Vector3.zero);
            foreach (int i in verts) b.Encapsulate(v[i]);
            var tex = HeroFaceRig.ReadableTexture($"{h.Folder}/Textures/{h.Class}_Body_baseColor.png");
            // colour each vertex by the finger its strongest bone belongs to (QA of the automatic segmentation)
            Color[] palette = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 0.3f), new Color(0.3f, 0.6f, 1f), new Color(0.85f, 0.4f, 1f) };
            Color FingerTint(int i)
            {
                BoneWeight w = bw[i];
                int b = w.boneIndex0;
                string n = b < smr.bones.Length && smr.bones[b] ? smr.bones[b].name : "";
                for (int f = 0; f < FingerNames.Length; f++) if (n.Contains("_" + FingerNames[f])) return palette[f];
                return tex.GetPixelBilinear(uv[i].x, uv[i].y);
            }
            Vector3 along = (wrist - elbow).normalized;
            Vector3 side = Vector3.Cross(along, Vector3.forward).normalized;
            if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(along, Vector3.up).normalized;
            Vector3 third = Vector3.Cross(along, side).normalized;
            var views = new[] { (side, third), (third, side), (along, side) };
            var sheet = new Texture2D(R * 3, R, TextureFormat.RGB24, false);
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * 1.15f;
            for (int vi = 0; vi < 3; vi++)
            {
                var (ax, ay) = views[vi];
                Vector3 depthAxis = Vector3.Cross(ax, ay);
                var depth = Enumerable.Repeat(float.MinValue, R * R).ToArray();
                var col = Enumerable.Repeat(new Color(0.2f, 0.22f, 0.28f), R * R).ToArray();
                Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p - b.center, ax) / size + 0.5f, Vector3.Dot(p - b.center, ay) / size + 0.5f) * R;
                for (int t = 0; t < tri.Length; t += 3)
                {
                    int a = tri[t], c = tri[t + 1], d = tri[t + 2];
                    if (W(a) < 0.3f && W(c) < 0.3f && W(d) < 0.3f) continue;
                    Vector2 pa = P(v[a]), pc = P(v[c]), pd = P(v[d]);
                    float area = (pc.x - pa.x) * (pd.y - pa.y) - (pd.x - pa.x) * (pc.y - pa.y);
                    if (Mathf.Abs(area) < 1e-6f) continue;
                    float da = Vector3.Dot(v[a], depthAxis), dc = Vector3.Dot(v[c], depthAxis), dd = Vector3.Dot(v[d], depthAxis);
                    int x0 = Mathf.Max(0, (int)Mathf.Min(pa.x, Mathf.Min(pc.x, pd.x))), x1 = Mathf.Min(R - 1, (int)Mathf.Max(pa.x, Mathf.Max(pc.x, pd.x)) + 1);
                    int y0 = Mathf.Max(0, (int)Mathf.Min(pa.y, Mathf.Min(pc.y, pd.y))), y1 = Mathf.Min(R - 1, (int)Mathf.Max(pa.y, Mathf.Max(pc.y, pd.y)) + 1);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                            float w0 = ((pc.x - p.x) * (pd.y - p.y) - (pd.x - p.x) * (pc.y - p.y)) / area;
                            float w1 = ((pd.x - p.x) * (pa.y - p.y) - (pa.x - p.x) * (pd.y - p.y)) / area;
                            float w2 = 1f - w0 - w1;
                            if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                            float dep = da * w0 + dc * w1 + dd * w2;
                            int k = y * R + x;
                            if (dep <= depth[k]) continue;
                            depth[k] = dep;
                            Vector2 u = uv[a] * w0 + uv[c] * w1 + uv[d] * w2;
                            // simple shading by facing to read the shape
                            Vector3 n = Vector3.Cross(v[c] - v[a], v[d] - v[a]).normalized;
                            float shade = 0.55f + 0.45f * Mathf.Abs(Vector3.Dot(n, depthAxis));
                            col[k] = Color.Lerp(tex.GetPixelBilinear(u.x, u.y), FingerTint(a), 0.65f) * shade;
                        }
                }
                for (int k = 0; k < R * R; k++) sheet.SetPixel(vi * R + k % R, k / R, col[k]);
            }
            sheet.Apply();
            File.WriteAllBytes($"QA/Hands/diag_{h.Class}_{handName}.png", sheet.EncodeToPNG());
            Debug.Log($"[HAND] {h.Class} {handName}: {verts.Count} hand vertices, size {b.size}, wrist {wrist}");
            Object.DestroyImmediate(sheet); Object.DestroyImmediate(tex);
        }
    }
}
