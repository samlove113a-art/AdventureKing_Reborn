using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Builds the cosmetic catalog's real 3D art, fitted to each hero's OWN skeleton (no new characters, no Meshy):
    ///  - garment shells (top / gloves / boots): a copy of the body region pushed out along its welded normals,
    ///    keeping the body's skin weights (so it deforms exactly with every clip), new UVs + new pattern textures,
    ///    plus a separate cuff/collar rim along the region border;
    ///  - procedural pieces (crowns, skirt, cape, wings, hip charm, premium crystal set) measured from the body in
    ///    bind pose (head cross-section, back depth, waist/thigh profile) so nothing intersects at rest;
    ///  - extra joints for secondary motion (cape chain, wing roots, bell pivots) animated by HeroOutfit.
    /// Output: Assets/Art/Outfits/** and Resources/CosmeticLibrary.asset (+ icons rendered from the meshes).
    /// </summary>
    public static partial class OutfitArtPipeline
    {
        public const string Dir = "Assets/Art/Outfits";
        public const string LibraryPath = "Assets/Resources/CosmeticLibrary.asset";
        private static readonly HeroClass[] Classes = { HeroClass.Swordsman, HeroClass.Mage, HeroClass.Assassin, HeroClass.Archer };

        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ================================================================== hero measurements
        public sealed class Hero
        {
            public HeroClass Cls;
            public GameObject Inst;
            public SkinnedMeshRenderer Smr;
            public Mesh Mesh;
            public Vector3[] V, Nrm;
            public BoneWeight[] W;
            public int[] Tris;
            public string[] Bones;
            public Matrix4x4[] Bind;
            public float H, MinY, MaxY;
            public int[] Weld;          // vertex -> weld group
            public Vector3[] GroupNormal; // weld group -> smooth normal
            public List<int>[] GroupAdj;
            public HashSet<int> ArmBones = new HashSet<int>();
            /// <summary>Mesh space (Z-up Blender export, cm) -> model frame (y up, z forward, metres) and back.</summary>
            public Matrix4x4 ToC, FromC;

            public int B(string name)
            {
                int i = Array.IndexOf(Bones, name);
                if (i < 0) throw new InvalidOperationException($"{Cls}: bone {name} missing");
                return i;
            }

            public Vector3 P(string bone) => ToC.MultiplyPoint3x4(Bind[B(bone)].inverse.GetColumn(3));
            public int Dominant(int v) => W[v].boneIndex0;
            public bool IsArm(int v) => ArmBones.Contains(W[v].boneIndex0);

            public float Wsum(int v, HashSet<int> set)
            {
                BoneWeight w = W[v];
                float s = 0;
                if (set.Contains(w.boneIndex0)) s += w.weight0;
                if (set.Contains(w.boneIndex1)) s += w.weight1;
                if (set.Contains(w.boneIndex2)) s += w.weight2;
                if (set.Contains(w.boneIndex3)) s += w.weight3;
                return s;
            }

            public HashSet<int> Set(params string[] names) => new HashSet<int>(names.Where(n => Array.IndexOf(Bones, n) >= 0).Select(B));

            public static Hero Load(HeroClass c)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(c));
                var h = new Hero { Cls = c, Inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab) };
                h.Smr = h.Inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => !s.name.StartsWith("Weapon_"));
                h.Mesh = h.Smr.sharedMesh;
                h.Inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                h.Inst.transform.localScale = Vector3.one;
                h.ToC = h.Smr.transform.localToWorldMatrix;
                h.FromC = h.ToC.inverse;
                Matrix4x4 toC = h.ToC;
                h.V = h.Mesh.vertices.Select(p => toC.MultiplyPoint3x4(p)).ToArray();
                h.Nrm = h.Mesh.normals.Select(n => toC.MultiplyVector(n).normalized).ToArray();
                h.W = h.Mesh.boneWeights;
                h.Tris = h.Mesh.triangles;
                h.Bones = h.Smr.bones.Select(b => b.name).ToArray();
                h.Bind = h.Mesh.bindposes;
                h.MinY = h.V.Min(p => p.y);
                h.MaxY = h.V.Max(p => p.y);
                h.H = h.MaxY - h.MinY;
                foreach (string side in new[] { "Left", "Right" })
                    foreach (string part in new[] { "Arm", "ForeArm", "Hand", "Hand_Fingers1", "Hand_Fingers2", "Hand_Fingers3" })
                    {
                        int i = Array.IndexOf(h.Bones, side + part);
                        if (i >= 0) h.ArmBones.Add(i);
                    }
                h.BuildWeld();
                return h;
            }

            private void BuildWeld()
            {
                float q = H * 2e-4f;
                var map = new Dictionary<Vector3Int, int>();
                Weld = new int[V.Length];
                for (int i = 0; i < V.Length; i++)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(V[i].x / q), Mathf.RoundToInt(V[i].y / q), Mathf.RoundToInt(V[i].z / q));
                    if (!map.TryGetValue(key, out int g)) map[key] = g = map.Count;
                    Weld[i] = g;
                }
                GroupNormal = new Vector3[map.Count];
                GroupAdj = new List<int>[map.Count];
                for (int g = 0; g < map.Count; g++) GroupAdj[g] = new List<int>(6);
                for (int t = 0; t < Tris.Length; t += 3)
                {
                    int a = Tris[t], b = Tris[t + 1], c = Tris[t + 2];
                    Vector3 f = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                    int ga = Weld[a], gb = Weld[b], gc = Weld[c];
                    GroupNormal[ga] += f; GroupNormal[gb] += f; GroupNormal[gc] += f;
                    Link(ga, gb); Link(gb, gc); Link(gc, ga);
                }
                for (int g = 0; g < GroupNormal.Length; g++) GroupNormal[g] = GroupNormal[g].sqrMagnitude > 0 ? GroupNormal[g].normalized : Vector3.up;
            }

            private void Link(int a, int b)
            {
                if (a == b) return;
                if (!GroupAdj[a].Contains(b)) GroupAdj[a].Add(b);
                if (!GroupAdj[b].Contains(a)) GroupAdj[b].Add(a);
            }

            /// <summary>Most-negative z (the back) of non-arm body vertices around height y within half-width hw.</summary>
            public float BackZ(float y, float band, float hw, float fallback, float percentile = 0.01f)
            {
                var zs = new List<float>();
                for (int i = 0; i < V.Length; i++)
                    if (!IsArm(i) && Mathf.Abs(V[i].y - y) < band && Mathf.Abs(V[i].x) < hw) zs.Add(V[i].z);
                if (zs.Count < 10) return fallback;
                zs.Sort();
                return zs[(int)(zs.Count * percentile)]; // a low percentile ignores thin flaps (scarf tails, feathers)
            }

            // extra joint frame = model frame (x right, y up, z forward) at its position: bind = T(-p) * ToC
            public Matrix4x4[] PieceBindposes(IList<ExtraJoint> extras) => Bind.Concat(extras.Select(e => Matrix4x4.Translate(-e.MeshPos) * ToC)).ToArray();
            public string[] PieceBones(IList<ExtraJoint> extras) => Bones.Concat(extras.Select(e => e.Name)).ToArray();
        }

        /// <summary>An extra joint defined by its bind position in the model frame (MeshPos is model-frame metres despite the name).</summary>
        public sealed class ExtraJoint
        {
            public string Name, Parent;
            public Vector3 MeshPos;
            public int ParentExtra = -1; // index into the extras list when the parent is another extra joint
        }

        private static ExtraBone[] ToExtraBones(Hero h, IList<ExtraJoint> extras)
        {
            var list = new List<ExtraBone>();
            foreach (ExtraJoint e in extras)
            {
                Matrix4x4 local = e.ParentExtra >= 0
                    ? Matrix4x4.Translate(e.MeshPos - extras[e.ParentExtra].MeshPos)
                    : h.Bind[h.B(e.Parent)] * h.FromC * Matrix4x4.Translate(e.MeshPos);
                Vector3 sx = local.GetColumn(0), sy = local.GetColumn(1), sz = local.GetColumn(2);
                list.Add(new ExtraBone
                {
                    name = e.Name, parent = e.Parent, localPosition = local.GetColumn(3),
                    localScale = new Vector3(sx.magnitude, sy.magnitude, sz.magnitude),
                    localRotation = Quaternion.LookRotation(sz / sz.magnitude, sy / sy.magnitude),
                });
            }
            return list.ToArray();
        }

        // ================================================================== build
        private static Material vcMat, crystalMat;
        private static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        [MenuItem("AdventureKing/Art/Rebuild Outfits")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Dir + "/Meshes");
            Directory.CreateDirectory(Dir + "/Materials");
            Directory.CreateDirectory(Dir + "/Textures");
            Directory.CreateDirectory(Dir + "/Icons");
            Directory.CreateDirectory("Assets/Resources");
            texCache.Clear();
            vcMat = ToonMat("Outfit_VertexColor", null, true, Color.black);
            crystalMat = ToonMat("Outfit_Crystal", null, true, new Color(0.05f, 0.14f, 0.24f));
            crystalMat.SetColor("_RimColor", new Color(0.8f, 0.95f, 1f));
            crystalMat.SetFloat("_RimStrength", 0.7f);

            var arts = new Dictionary<string, CosmeticArt>();
            var failures = new List<string>();
            foreach (HeroClass c in Classes)
            {
                Hero h = Hero.Load(c);
                try
                {
                    void Put(string id, string slot, Func<OutfitPiece[]> make, params string[] covers)
                    {
                        OutfitPiece[] pieces;
                        try { pieces = make(); }
                        catch (Exception e) { failures.Add($"{c} {id}: {e.Message}"); Debug.LogError($"[OUTFIT-FAIL] {c} {id}: {e}"); return; }
                        if (!arts.TryGetValue(id, out CosmeticArt a)) arts[id] = a = new CosmeticArt { itemId = id, slot = slot, coversSlots = covers };
                        a.fits = a.fits.Append(new ClassFit { heroClass = c, pieces = pieces }).ToArray();
                    }
                    Put("head_lotus_crown", "head", () => LotusCrown(h));
                    Put("head_bell_circlet", "head", () => BellCirclet(h));
                    Put("top_brocade_vest", "top", () => Top(h));
                    Put("bottom_pleat_skirt", "bottom", () => Skirt(h));
                    Put("gloves_jade_silk", "gloves", () => Gloves(h));
                    Put("shoes_cloud_boots", "shoes", () => Boots(h, "cloud"));
                    Put("shoes_ember_boots", "shoes", () => Boots(h, "ember"));
                    Put("acc_silver_bell", "accessory", () => HipCharm(h));
                    Put("back_wind_cape", "back", () => Cape(h, "wind"));
                    Put("back_dragonfly_wings", "back", () => Wings(h, false));
                    Put("set_crystal_zephyr", "set", () => CrystalSet(h), "head", "back");
                    // themed, funny pieces (OutfitThemes.cs): different silhouettes, not recolours
                    Put("head_duck_buddy", "head", () => DuckBuddy(h));
                    Put("head_frog_eyes", "head", () => FrogEyes(h));
                    Put("head_bunny_ears", "head", () => BunnyEars(h));
                    Put("head_cat_ears", "head", () => CatEars(h));
                    Put("head_panda_ears", "head", () => PandaEars(h));
                    Put("head_mushroom_cap", "head", () => MushroomCap(h));
                    Put("head_chef_toque", "head", () => ChefToque(h));
                    Put("set_little_dino", "set", () => DinoSet(h), "head", "back", "top");
                    Put("set_baby_shark", "set", () => SharkSet(h), "head", "back", "top");
                    Put("set_sweet_dream", "set", () => DreamSet(h), "head", "top");
                    // wedding gifts (granted by the server after the ceremony, never sold): recoloured variants of the
                    // shop pieces above, built procedurally (no Meshy credit)
                    Put("wed_pearl_crown", "head", () => LotusCrown(h, pearl: true));
                    Put("wed_satin_gloves", "gloves", () => Gloves(h, "wed_satin_gloves", "satin"));
                    Put("wed_white_cape", "back", () => Cape(h, "wedding"));
                    Debug.Log($"[OUTFIT] {c}: built {arts.Count} items (H={h.H:0.0} mesh units)");
                }
                finally { Object.DestroyImmediate(h.Inst); }
            }

            var lib = AssetDatabase.LoadAssetAtPath<CosmeticLibrary>(LibraryPath);
            if (!lib) { lib = ScriptableObject.CreateInstance<CosmeticLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
            lib.items = arts.Values.ToList();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            OutfitIcons.RenderAll(lib);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[OUTFIT] library written: " + string.Join(", ", lib.items.Select(i => i.itemId)));
            if (failures.Count > 0) throw new InvalidOperationException("Outfit build failures:\n" + string.Join("\n", failures));
        }

        // ================================================================== assets
        private static Mesh SaveMesh(Mesh m, string name)
        {
            string path = $"{Dir}/Meshes/{name}.asset";
            // Recreate (CopySerialized into an existing Mesh asset loses the vertex buffer); only the library references these.
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static Material ToonMat(string name, Texture2D tex, bool vertexColor, Color emission, float outline = 0.0055f)
        {
            string path = $"{Dir}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find(HeroArtPipeline.ToonShader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(HeroArtPipeline.ToonShader);
            m.SetTexture("_BaseMap", tex ? tex : Texture2D.whiteTexture);
            m.SetFloat("_OutlineWidth", outline);
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            if (vertexColor) m.EnableKeyword("_VERTEX_COLOR"); else m.DisableKeyword("_VERTEX_COLOR");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static OutfitPiece Piece(Hero h, string name, OutfitMesh om, Material mat, IList<ExtraJoint> extras = null, PieceMotion motion = PieceMotion.None, bool declareExtras = true)
        {
            extras ??= new List<ExtraJoint>();
            // geometry was built in the model frame; skinning needs the body's mesh space
            for (int i = 0; i < om.V.Count; i++) om.V[i] = h.FromC.MultiplyPoint3x4(om.V[i]);
            if (om.ExplicitNormals) for (int i = 0; i < om.N.Count; i++) om.N[i] = h.FromC.MultiplyVector(om.N[i]).normalized;
            Mesh mesh = SaveMesh(om.ToMesh($"{name}_{h.Cls}", h.PieceBindposes(extras)), $"{name}_{h.Cls}");
            return new OutfitPiece
            {
                mesh = mesh, material = mat, boneNames = h.PieceBones(extras),
                extraBones = declareExtras ? ToExtraBones(h, extras) : new ExtraBone[0], motion = declareExtras ? motion : PieceMotion.None,
            };
        }

        // ================================================================== garment shells
        private sealed class ShellResult { public OutfitMesh Shell = new OutfitMesh(); public OutfitMesh Rim = new OutfitMesh(); }

        /// <summary>
        /// Copies the body region (decided per weld group, majority-smoothed, tiny islands dropped), pushed out by
        /// <paramref name="offset"/> along smooth normals, with cylindrical UVs (per side when splitSides) and the
        /// body's own skin weights. The region border gets a flared rim (cuff / collar / hem).
        /// </summary>
        private static ShellResult Shell(Hero h, Func<int, bool> pred, float offset, bool splitSides, float uRepeat, float vScale,
            float rimOut, float rimWidth, Color rimA, Color rimB)
        {
            int groups = h.GroupNormal.Length;
            var rep = new int[groups];
            for (int i = h.V.Length - 1; i >= 0; i--) rep[h.Weld[i]] = i;
            var inG = new bool[groups];
            for (int g = 0; g < groups; g++) inG[g] = pred(rep[g]);
            for (int pass = 0; pass < 3; pass++) // majority filter: removes speckles and fills pinholes
            {
                var next = (bool[])inG.Clone();
                for (int g = 0; g < groups; g++)
                {
                    var adj = h.GroupAdj[g];
                    if (adj.Count == 0) continue;
                    int k = adj.Count(a => inG[a]);
                    if (inG[g] && k * 3 < adj.Count) next[g] = false;
                    else if (!inG[g] && k * 3 > adj.Count * 2) next[g] = true;
                }
                inG = next;
            }
            // connected components; drop islands < 8% of the largest
            var comp = Enumerable.Repeat(-1, groups).ToArray();
            var sizes = new List<int>();
            for (int g = 0; g < groups; g++)
            {
                if (!inG[g] || comp[g] >= 0) continue;
                int id = sizes.Count, n = 0;
                var stack = new Stack<int>();
                stack.Push(g); comp[g] = id;
                while (stack.Count > 0)
                {
                    int x = stack.Pop(); n++;
                    foreach (int a in h.GroupAdj[x]) if (inG[a] && comp[a] < 0) { comp[a] = id; stack.Push(a); }
                }
                sizes.Add(n);
            }
            int largest = sizes.Count == 0 ? 0 : sizes.Max();
            for (int g = 0; g < groups; g++) if (inG[g] && sizes[comp[g]] < largest * 0.08f) inG[g] = false;

            var tris = new List<int>();
            for (int t = 0; t < h.Tris.Length; t += 3)
                if (inG[h.Weld[h.Tris[t]]] && inG[h.Weld[h.Tris[t + 1]]] && inG[h.Weld[h.Tris[t + 2]]])
                    tris.AddRange(new[] { h.Tris[t], h.Tris[t + 1], h.Tris[t + 2] });
            if (tris.Count == 0)
            {
                int raw = Enumerable.Range(0, h.V.Length).Count(pred);
                throw new InvalidOperationException($"{h.Cls}: empty shell region (raw {raw} verts, groups kept {inG.Count(x => x)}, comps {sizes.Count})");
            }

            // UV centres
            var used = tris.Distinct().ToArray();
            Vector3 cAll = used.Aggregate(Vector3.zero, (s, i) => s + h.V[i]) / used.Length;
            Vector3 Centre(int i)
            {
                if (!splitSides) return cAll;
                bool left = h.V[i].x < 0;
                var side = used.Where(j => (h.V[j].x < 0) == left).ToArray();
                return side.Aggregate(Vector3.zero, (s, j) => s + h.V[j]) / Mathf.Max(1, side.Length);
            }
            Vector3 cL = Centre(used.FirstOrDefault(i => h.V[i].x < 0)), cR = Centre(used.FirstOrDefault(i => h.V[i].x >= 0));
            float Ang(int i)
            {
                Vector3 c = !splitSides ? cAll : (h.V[i].x < 0 ? cL : cR);
                return Mathf.Atan2(h.V[i].z - c.z, h.V[i].x - c.x) / (2f * Mathf.PI) + 0.5f;
            }

            var res = new ShellResult();
            OutfitMesh s = res.Shell;
            s.ExplicitNormals = true;
            var map = new Dictionary<(int, bool), int>();
            int Vert(int i, bool wrap)
            {
                if (map.TryGetValue((i, wrap), out int k)) return k;
                Vector3 n = h.GroupNormal[h.Weld[i]];
                float u = (Ang(i) + (wrap ? 1f : 0f)) * uRepeat;
                k = s.Add(h.V[i] + n * offset, new Vector2(u, (h.V[i].y - h.MinY) / vScale), Color.white, h.W[i], n);
                map[(i, wrap)] = k;
                return k;
            }
            for (int t = 0; t < tris.Count; t += 3)
            {
                float a0 = Ang(tris[t]), a1 = Ang(tris[t + 1]), a2 = Ang(tris[t + 2]);
                bool cross = Mathf.Max(a0, Mathf.Max(a1, a2)) - Mathf.Min(a0, Mathf.Min(a1, a2)) > 0.5f;
                s.Tri(Vert(tris[t], cross && a0 < 0.5f), Vert(tris[t + 1], cross && a1 < 0.5f), Vert(tris[t + 2], cross && a2 < 0.5f));
            }

            // ---- rim along the region border (welded edges used by exactly one region triangle)
            var edgeCount = new Dictionary<(int, int), int>();
            for (int t = 0; t < tris.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int ga = h.Weld[tris[t + e]], gb = h.Weld[tris[t + (e + 1) % 3]];
                    var key = ga < gb ? (ga, gb) : (gb, ga);
                    edgeCount[key] = edgeCount.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            var away = new Dictionary<int, Vector3>();
            var rimRep = new Dictionary<int, int>();
            var rimEdges = new List<(int, int)>();
            for (int t = 0; t < tris.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int ia = tris[t + e], ib = tris[t + (e + 1) % 3], ic = tris[t + (e + 2) % 3];
                    int ga = h.Weld[ia], gb = h.Weld[ib];
                    if (edgeCount[ga < gb ? (ga, gb) : (gb, ga)] != 1) continue;
                    Vector3 mid = (h.V[ia] + h.V[ib]) * 0.5f, n = h.GroupNormal[ga];
                    Vector3 dir = Vector3.ProjectOnPlane(mid - h.V[ic], n).normalized;
                    away[ga] = (away.TryGetValue(ga, out Vector3 d0) ? d0 : Vector3.zero) + dir;
                    away[gb] = (away.TryGetValue(gb, out Vector3 d1) ? d1 : Vector3.zero) + dir;
                    rimRep[ga] = ia; rimRep[gb] = ib;
                    rimEdges.Add((ga, gb));
                }
            if (rimEdges.Count > 0 && rimWidth > 0f)
            {
                OutfitMesh r = res.Rim;
                var row = new Dictionary<int, int>();
                foreach (var kv in rimRep)
                {
                    int i = kv.Value;
                    Vector3 n = h.GroupNormal[kv.Key], a = away[kv.Key].normalized, p = h.V[i] + n * offset;
                    int r0 = r.Add(p - a * rimWidth * 0.15f, new Vector2(0, 0), rimA, h.W[i]);
                    r.Add(p + n * rimOut + a * rimWidth * 0.45f, new Vector2(0, 0.5f), rimB, h.W[i]);
                    r.Add(p + n * rimOut * 0.55f + a * rimWidth, new Vector2(0, 1), rimA, h.W[i]);
                    row[kv.Key] = r0;
                }
                foreach (var (ga, gb) in rimEdges)
                {
                    int a = row[ga], b = row[gb];
                    r.Quad(a, b, b + 1, a + 1);
                    r.Quad(a + 1, b + 1, b + 2, a + 2);
                }
                r.MakeDoubleSided(0f);
            }
            return res;
        }

        private static OutfitPiece[] Garment(Hero h, string id, ShellResult r, Material shellMat)
        {
            var pieces = new List<OutfitPiece> { Piece(h, id, r.Shell, shellMat) };
            if (r.Rim.Count > 0) pieces.Add(Piece(h, id + "_rim", r.Rim, vcMat));
            return pieces.ToArray();
        }

        private static OutfitPiece[] Top(Hero h) =>
            TopShell(h, "top_brocade_vest", "Top_Brocade", "brocade", new Color(0.95f, 0.76f, 0.3f), new Color(1f, 0.9f, 0.55f));

        /// <summary>Short-sleeved torso garment with a pattern texture and a coloured collar/hem rim.</summary>
        private static OutfitPiece[] TopShell(Hero h, string id, string matName, string pattern, Color rimLo, Color rimHi)
        {
            // Torso by height band (Meshy auto-rigs weight the lower torso to the thighs, so bone sets alone miss it):
            // everything between the top of the legs and the neck that is not head- or arm-driven, plus short sleeves.
            var upper = h.Set("LeftArm", "RightArm");
            var headSet = h.Set("Head", "neck", "headfront");
            float neckY = h.P("neck").y, hipY = (h.P("LeftUpLeg").y + h.P("RightUpLeg").y) * 0.5f + 0.02f * h.H;
            Vector3 la = h.P("LeftArm"), ra = h.P("RightArm"), lf = h.P("LeftForeArm"), rf = h.P("RightForeArm");
            Debug.Log($"[OUTFIT] {h.Cls} top band y {hipY:0.00}..{neckY:0.00} (H {h.H:0.0}, minY {h.MinY:0.00}); head-weighted in band: " +
                      Enumerable.Range(0, h.V.Length).Count(i => h.V[i].y > hipY && h.V[i].y < neckY && h.Wsum(i, headSet) > 0.35f) +
                      ", all in band: " + Enumerable.Range(0, h.V.Length).Count(i => h.V[i].y > hipY && h.V[i].y < neckY));
            bool Pred(int i)
            {
                Vector3 p = h.V[i];
                if (h.Wsum(i, headSet) > 0.35f) return false;
                if (p.y > neckY - 0.012f * h.H || p.y < hipY) return false;
                if (!h.IsArm(i)) return true;
                if (h.Wsum(i, upper) > 0.5f) // short sleeves: first 45% of the upper arm
                {
                    bool left = p.x < 0;
                    Vector3 s = left ? la : ra, e = left ? lf : rf;
                    float t = Vector3.Dot(p - s, (e - s).normalized) / (e - s).magnitude;
                    return t < 0.45f;
                }
                return false;
            }
            var r = Shell(h, Pred, 0.0075f * h.H, false, 3f, 0.16f * h.H, 0.012f * h.H, 0.016f * h.H, rimLo, rimHi);
            return Garment(h, id, r, ToonMat(matName, PatternTex(pattern), false, Color.black));
        }

        private static OutfitPiece[] Gloves(Hero h, string id = "gloves_jade_silk", string pattern = "jade")
        {
            var hands = h.Set("LeftHand", "RightHand", "LeftHand_Fingers1", "LeftHand_Fingers2", "LeftHand_Fingers3",
                "RightHand_Fingers1", "RightHand_Fingers2", "RightHand_Fingers3");
            var fore = h.Set("LeftForeArm", "RightForeArm");
            Vector3 lh = h.P("LeftHand"), rh = h.P("RightHand"), lf = h.P("LeftForeArm"), rf = h.P("RightForeArm");
            bool Pred(int i)
            {
                if (h.Wsum(i, hands) > 0.5f) return true;
                if (h.Wsum(i, fore) > 0.5f)
                {
                    bool left = h.V[i].x < 0;
                    Vector3 hand = left ? lh : rh, elbow = left ? lf : rf;
                    return Vector3.Distance(h.V[i], hand) < 0.42f * Vector3.Distance(hand, elbow);
                }
                return false;
            }
            var r = Shell(h, Pred, 0.0045f * h.H, true, 2f, 0.06f * h.H, 0.011f * h.H, 0.014f * h.H,
                new Color(0.82f, 0.86f, 0.9f), new Color(0.97f, 0.98f, 1f));
            return Garment(h, id, r, ToonMat(pattern == "jade" ? "Gloves_JadeSilk" : "Gloves_WeddingSatin", PatternTex(pattern), false, Color.black));
        }

        private static OutfitPiece[] Boots(Hero h, string style)
        {
            var feet = h.Set("LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase");
            var shin = h.Set("LeftLeg", "RightLeg");
            float footY = (h.P("LeftFoot").y + h.P("RightFoot").y) * 0.5f, kneeY = (h.P("LeftLeg").y + h.P("RightLeg").y) * 0.5f;
            float top = footY + (style == "cloud" ? 0.62f : 0.5f) * (kneeY - footY);
            bool Pred(int i) => h.Wsum(i, feet) > 0.5f || (h.Wsum(i, shin) > 0.5f && h.V[i].y < top);
            bool cloud = style == "cloud";
            var r = Shell(h, Pred, 0.006f * h.H, true, 2f, 0.07f * h.H, (cloud ? 0.016f : 0.011f) * h.H, (cloud ? 0.024f : 0.016f) * h.H,
                cloud ? new Color(0.45f, 0.72f, 0.95f) : new Color(0.78f, 0.55f, 0.22f),
                cloud ? new Color(0.85f, 0.94f, 1f) : new Color(0.98f, 0.8f, 0.45f));
            string id = cloud ? "shoes_cloud_boots" : "shoes_ember_boots";
            return Garment(h, id, r, ToonMat(cloud ? "Boots_Cloud" : "Boots_Ember", PatternTex(style), false, Color.black));
        }

        // ================================================================== head pieces
        private sealed class Ring { public Vector3 Centre; public Vector3[] Points; public Vector3[] Out; }

        /// <summary>
        /// Fits a closed ring around the head (or hat): finds the widest cross-section in the upper head, then for
        /// each of 36 directions takes the outer surface (95th percentile) at that height, tilted front-down.
        /// </summary>
        private static Ring HeadRing(Hero h, float margin, float tilt)
        {
            int head = h.B("Head");
            var idx = Enumerable.Range(0, h.V.Length).Where(i => h.W[i].boneIndex0 == head && h.W[i].weight0 > 0.6f).ToArray();
            float top = idx.Max(i => h.V[i].y);
            Vector3 hc = h.P("Head");
            float band = 0.014f * h.H, bestY = top - 0.15f * h.H, bestR = 0f;
            for (float y = top - 0.3f * h.H; y < top - 0.07f * h.H; y += 0.004f * h.H)
            {
                var d = idx.Where(i => Mathf.Abs(h.V[i].y - y) < band).Select(i => new Vector2(h.V[i].x - hc.x, h.V[i].z - hc.z).magnitude).OrderBy(x => x).ToArray();
                if (d.Length < 30) continue;
                float r = d[(int)(d.Length * 0.9f)];
                if (r >= bestR) { bestR = r; bestY = y; }
            }
            const int bins = 36;
            var pts = new Vector3[bins];
            var outs = new Vector3[bins];
            var rad = new float[bins];
            for (int b = 0; b < bins; b++)
            {
                float a = b / (float)bins * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); // x = right, y(=z) = front
                float y = bestY - tilt * h.H * dir.y;
                var d = idx.Where(i =>
                {
                    Vector3 p = h.V[i];
                    if (Mathf.Abs(p.y - y) > band) return false;
                    Vector2 q = new Vector2(p.x - hc.x, p.z - hc.z);
                    return q.sqrMagnitude > 1e-6f && Vector2.Angle(q, dir) < 360f / bins;
                }).Select(i => new Vector2(h.V[i].x - hc.x, h.V[i].z - hc.z).magnitude).OrderBy(x => x).ToArray();
                rad[b] = d.Length > 3 ? d[(int)(d.Length * 0.95f)] : -1f;
            }
            for (int b = 0; b < bins; b++) if (rad[b] < 0) rad[b] = bestR;
            for (int pass = 0; pass < 2; pass++)
            {
                var sm = new float[bins];
                for (int b = 0; b < bins; b++) sm[b] = Mathf.Max(rad[b], (rad[(b + bins - 1) % bins] + rad[b] * 2f + rad[(b + 1) % bins]) * 0.25f);
                rad = sm;
            }
            for (int b = 0; b < bins; b++)
            {
                float a = b / (float)bins * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                pts[b] = new Vector3(hc.x, bestY - tilt * h.H * dir.z, hc.z) + dir * (rad[b] + margin * h.H);
                outs[b] = dir;
            }
            return new Ring { Centre = new Vector3(hc.x, bestY, hc.z), Points = pts, Out = outs };
        }

        private static IEnumerable<Vector3> Smooth(Vector3[] ring, int sub)
        {
            int n = ring.Length;
            for (int i = 0; i < n; i++)
                for (int s = 0; s < sub; s++)
                {
                    float t = s / (float)sub;
                    Vector3 p0 = ring[(i - 1 + n) % n], p1 = ring[i], p2 = ring[(i + 1) % n], p3 = ring[(i + 2) % n];
                    yield return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                }
        }

        private static Vector3 RingAt(Ring r, float angleDeg, out Vector3 outward)
        {
            float f = Mathf.Repeat(angleDeg / 360f, 1f) * r.Points.Length;
            int i = Mathf.FloorToInt(f) % r.Points.Length, j = (i + 1) % r.Points.Length;
            outward = Vector3.Slerp(r.Out[i], r.Out[j], f - Mathf.Floor(f));
            return Vector3.Lerp(r.Points[i], r.Points[j], f - Mathf.Floor(f));
        }

        private static void Flower(OutfitMesh m, Vector3 centre, Vector3 axis, float size, Color baseC, Color tipC, BoneWeight w)
        {
            axis.Normalize();
            Vector3 t1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized, t2 = Vector3.Cross(axis, t1);
            void Petals(int count, float tiltDeg, float len, float wid, float phase)
            {
                for (int k = 0; k < count; k++)
                {
                    float a = (k + phase) / count * Mathf.PI * 2f;
                    Vector3 radial = t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a);
                    Vector3 dir = (axis * Mathf.Cos(tiltDeg * Mathf.Deg2Rad) + radial * Mathf.Sin(tiltDeg * Mathf.Deg2Rad)).normalized;
                    Quaternion q = Quaternion.LookRotation(Vector3.Cross(dir, radial).normalized, dir);
                    Vector3 c = centre + dir * len * size * 0.5f;
                    int t0 = m.T.Count;
                    m.Ellipsoid(Matrix4x4.TRS(c, q, new Vector3(wid * size, len * size * 0.5f, 0.12f * size)), 10, 6,
                        l => Color.Lerp(baseC, tipC, Mathf.Clamp01((l.y + 1f) * 0.5f)), w);
                    m.Orient(t0, p => p - c);
                }
            }
            Petals(8, 62f, 1.0f, 0.36f, 0f);
            Petals(6, 30f, 0.8f, 0.3f, 0.5f);
            int tc = m.T.Count;
            m.Ellipsoid(Matrix4x4.TRS(centre + axis * size * 0.12f, Quaternion.LookRotation(t1, axis), Vector3.one * size * 0.2f), 8, 5,
                _ => new Color(1f, 0.85f, 0.3f), w);
            m.Orient(tc, p => p - (centre + axis * size * 0.12f));
        }

        private static OutfitPiece[] LotusCrown(Hero h, bool pearl = false)
        {
            Ring ring = HeadRing(h, 0.004f, 0.025f);
            int head = h.B("Head");
            BoneWeight w = OutfitMesh.Rigid(head);
            var m = new OutfitMesh();
            var path = Smooth(ring.Points, 3).ToList();
            int t0 = m.T.Count;
            m.Tube(path, Enumerable.Repeat(0.0065f * h.H, path.Count).ToList(), true, 8,
                (k, a) => pearl ? Color.Lerp(new Color(0.86f, 0.66f, 0.28f), new Color(1f, 0.88f, 0.52f), 0.5f + 0.5f * Mathf.Sin(a)) // gold band
                    : Color.Lerp(new Color(0.24f, 0.52f, 0.2f), new Color(0.45f, 0.72f, 0.3f), 0.5f + 0.5f * Mathf.Sin(k * 1.3f)), _ => w, Vector3.up);
            m.Orient(t0, p => p - ClosestOnPath(path, p));
            // leaves around the vine
            for (int k = 0; k < 14; k++)
            {
                float ang = k * 360f / 14f + 8f;
                Vector3 p = RingAt(ring, ang, out Vector3 o);
                Vector3 tan = Vector3.Cross(Vector3.up, o).normalized;
                Vector3 dir = (tan * (k % 2 == 0 ? 1f : -1f) + Vector3.up * 0.35f + o * 0.3f).normalized;
                Quaternion q = Quaternion.LookRotation(o, dir);
                Vector3 c = p + dir * 0.012f * h.H;
                int tl = m.T.Count;
                if (pearl)
                {
                    // a pearl instead of a leaf: round, pale, with a soft lustre gradient
                    Vector3 pc = p + o * 0.006f * h.H + Vector3.up * 0.004f * h.H;
                    m.Ellipsoid(Matrix4x4.TRS(pc, Quaternion.identity, Vector3.one * 0.0085f * h.H), 10, 6,
                        l => Color.Lerp(new Color(0.86f, 0.84f, 0.88f), new Color(1f, 0.99f, 0.97f), (l.y + 1f) * 0.5f), w);
                    m.Orient(tl, x => x - pc);
                    continue;
                }
                m.Ellipsoid(Matrix4x4.TRS(c, q, new Vector3(0.008f * h.H, 0.016f * h.H, 0.0025f * h.H)), 8, 5,
                    l => Color.Lerp(new Color(0.2f, 0.5f, 0.2f), new Color(0.5f, 0.8f, 0.35f), (l.y + 1f) * 0.5f), w);
                m.Orient(tl, x => x - c);
            }
            // lotus blossoms: front centre largest, then front-sides and sides (none at the back: hidden by the head)
            foreach (var (ang, size) in new[] { (90f, 0.034f), (55f, 0.027f), (125f, 0.027f), (18f, 0.022f), (162f, 0.022f) })
            {
                Vector3 p = RingAt(ring, ang, out Vector3 o);
                Vector3 axis = (o * 0.75f + Vector3.up * 0.65f).normalized;
                Flower(m, p + o * 0.004f * h.H, axis, size * h.H, pearl ? new Color(1f, 0.99f, 0.97f) : new Color(1f, 0.93f, 0.95f),
                    pearl ? new Color(0.96f, 0.8f, 0.42f) : new Color(0.95f, 0.45f, 0.62f), w);
            }
            return new[] { Piece(h, pearl ? "wed_pearl_crown" : "head_lotus_crown", m, vcMat) };
        }

        private static Vector3 ClosestOnPath(IList<Vector3> path, Vector3 p)
        {
            Vector3 best = path[0];
            float bd = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 a = path[i], b = path[(i + 1) % path.Count];
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                Vector3 q = a + ab * t;
                float d = (q - p).sqrMagnitude;
                if (d < bd) { bd = d; best = q; }
            }
            return best;
        }

        private static OutfitPiece[] BellCirclet(Hero h)
        {
            Ring ring = HeadRing(h, 0.003f, 0.03f);
            int head = h.B("Head");
            BoneWeight w = OutfitMesh.Rigid(head);
            Color bronze = new Color(0.86f, 0.62f, 0.26f), bronzeHi = new Color(1f, 0.86f, 0.5f), jade = new Color(0.2f, 0.75f, 0.6f);
            var m = new OutfitMesh();
            var path = Smooth(ring.Points, 3).ToList();
            int t0 = m.T.Count;
            m.Tube(path, Enumerable.Repeat(0.0085f * h.H, path.Count).ToList(), true, 10,
                (k, a) => Color.Lerp(bronze, bronzeHi, Mathf.Clamp01(Mathf.Sin(a) * 0.5f + 0.5f)), _ => w, Vector3.up, 0.35f);
            m.Orient(t0, p => p - ClosestOnPath(path, p));
            // cloud-curl studs along the band
            for (int k = 0; k < 16; k++)
            {
                float ang = k * 22.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang, 90f)) < 20f) continue;
                Vector3 p = RingAt(ring, ang, out Vector3 o) + Vector3.up * 0.006f * h.H;
                int ts = m.T.Count;
                m.Ellipsoid(Matrix4x4.TRS(p + o * 0.002f * h.H, Quaternion.LookRotation(o), new Vector3(0.0055f, 0.0055f, 0.003f) * h.H), 8, 5, _ => bronzeHi, w);
                m.Orient(ts, x => x - p);
            }
            // front medallion with a jade stone
            {
                Vector3 p = RingAt(ring, 90f, out Vector3 o);
                Quaternion q = Quaternion.LookRotation(o, Vector3.up);
                Vector3 c1 = p + o * 0.003f * h.H + Vector3.up * 0.004f * h.H;
                int ts = m.T.Count;
                m.Ellipsoid(Matrix4x4.TRS(c1, q, new Vector3(0.017f, 0.019f, 0.005f) * h.H), 14, 8, l => Color.Lerp(bronze, bronzeHi, (l.y + 1f) * 0.5f), w);
                m.Orient(ts, x => x - c1);
                Vector3 c2 = c1 + o * 0.004f * h.H;
                ts = m.T.Count;
                m.Ellipsoid(Matrix4x4.TRS(c2, q, new Vector3(0.009f, 0.011f, 0.005f) * h.H), 12, 7, l => Color.Lerp(jade * 0.7f, jade + new Color(0.3f, 0.2f, 0.2f), (l.y + 1f) * 0.5f), w);
                m.Orient(ts, x => x - c2);
            }
            var pieces = new List<OutfitPiece> { Piece(h, "head_bell_circlet", m, vcMat) };

            // wind bell hanging at the right temple on its own pivot (swings as the hero moves)
            Vector3 hang = RingAt(ring, 8f, out Vector3 ho) + ho * 0.006f * h.H;
            var extras = new List<ExtraJoint> { new ExtraJoint { Name = "Outfit_CircletBell", Parent = "Head", MeshPos = hang } };
            int bone = h.Bones.Length;
            var b = new OutfitMesh();
            BellMesh(b, hang, 0.026f * h.H, OutfitMesh.Rigid(bone), new Color(0.95f, 0.78f, 0.35f), new Color(1f, 0.93f, 0.65f), new Color(0.85f, 0.15f, 0.15f));
            pieces.Add(Piece(h, "head_bell_circlet_bell", b, vcMat, extras, PieceMotion.Pendulum));
            return pieces.ToArray();
        }

        /// <summary>Small bell hanging below <paramref name="top"/>: loop, cord, bell body (lathe), clapper and a short tassel.</summary>
        private static void BellMesh(OutfitMesh m, Vector3 top, float size, BoneWeight w, Color body, Color hi, Color tassel)
        {
            var loop = new List<Vector3>();
            for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2f; loop.Add(top + new Vector3(Mathf.Cos(a) * 0.12f, -0.1f + Mathf.Sin(a) * 0.12f, 0f) * size); }
            int t0 = m.T.Count;
            m.Tube(loop, Enumerable.Repeat(0.03f * size, loop.Count).ToList(), true, 6, (k, a) => hi, _ => w, Vector3.forward);
            m.Orient(t0, p => p - ClosestOnPath(loop, p));
            Vector3 bellTop = top + Vector3.down * 0.22f * size;
            var profile = new List<Vector2>
            {
                new Vector2(0.001f, 0.0f), new Vector2(0.12f, -0.02f), new Vector2(0.2f, -0.12f), new Vector2(0.23f, -0.32f),
                new Vector2(0.27f, -0.5f), new Vector2(0.36f, -0.62f), new Vector2(0.37f, -0.66f), new Vector2(0.3f, -0.64f),
            }.Select(p => p * size).ToList();
            t0 = m.T.Count;
            m.Lathe(Matrix4x4.Translate(bellTop), profile, 16, i => Color.Lerp(hi, body, i / 7f), w);
            m.Orient(t0, p => { Vector3 d = p - bellTop; d.y = 0; return d.sqrMagnitude > 1e-8f ? d : Vector3.up; });
            Vector3 clap = bellTop + Vector3.down * 0.66f * size;
            t0 = m.T.Count;
            m.Ellipsoid(Matrix4x4.TRS(clap, Quaternion.identity, Vector3.one * 0.07f * size), 8, 5, _ => body * 0.8f, w);
            m.Orient(t0, p => p - clap);
            for (int k = 0; k < 5; k++)
            {
                float a = k / 5f * Mathf.PI * 2f;
                Vector3 s0 = clap + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.03f * size;
                var strand = new List<Vector3> { s0, s0 + new Vector3(Mathf.Cos(a) * 0.04f, -0.3f, Mathf.Sin(a) * 0.04f) * size, s0 + new Vector3(Mathf.Cos(a) * 0.07f, -0.55f, Mathf.Sin(a) * 0.07f) * size };
                t0 = m.T.Count;
                m.Tube(strand, new List<float> { 0.035f * size, 0.03f * size, 0.012f * size }, false, 5, (i, _) => Color.Lerp(tassel, tassel * 0.7f, i / 2f), _ => w, Vector3.forward);
                m.Orient(t0, p => p - ClosestOnPath(strand, p));
            }
        }

        // ================================================================== skirt
        private static OutfitPiece[] Skirt(Hero h)
        {
            Vector3 hips = h.P("Hips"), lu = h.P("LeftUpLeg"), ru = h.P("RightUpLeg"), ll = h.P("LeftLeg"), rl = h.P("RightLeg");
            float legTop = (lu.y + ru.y) * 0.5f, knee = (ll.y + rl.y) * 0.5f;
            float yTop = legTop + 0.02f * h.H, yBot = legTop - 0.5f * (legTop - knee);
            Vector2 c = new Vector2(hips.x, hips.z);
            const int seg = 48, rows = 9;
            int iH = h.B("Hips"), iL = h.B("LeftUpLeg"), iR = h.B("RightUpLeg");
            var headSet = h.Set("Head", "neck", "headfront");
            var body = Enumerable.Range(0, h.V.Length).Where(i => !h.IsArm(i) && h.Wsum(i, headSet) < 0.3f).ToArray();
            var radius = new float[rows, seg];
            float[] prev = null;
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1), y = Mathf.Lerp(yTop, yBot, t), band = 0.02f * h.H;
                var cur = new float[seg];
                var near = body.Where(i => Mathf.Abs(h.V[i].y - y) < band).ToArray();
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var d = near.Select(i => new Vector2(h.V[i].x - c.x, h.V[i].z - c.y)).Where(q => Vector2.Angle(q, dir) < 360f / seg * 1.5f)
                        .Select(q => q.magnitude).OrderBy(x => x).ToArray();
                    cur[s] = d.Length > 2 ? d[(int)(d.Length * 0.97f)] : 0f;
                }
                for (int s = 0; s < seg; s++) if (cur[s] <= 0f) cur[s] = cur.Where(x => x > 0).DefaultIfEmpty(0.1f * h.H).Average();
                var sm = new float[seg];
                for (int s = 0; s < seg; s++) sm[s] = Mathf.Max(cur[s], (cur[(s + seg - 1) % seg] + 2f * cur[s] + cur[(s + 1) % seg]) * 0.25f);
                cur = sm;
                if (prev != null) for (int s = 0; s < seg; s++) cur[s] = Mathf.Max(cur[s], prev[s]);
                for (int s = 0; s < seg; s++) radius[r, s] = cur[s];
                prev = cur;
            }
            var m = new OutfitMesh();
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1), y = Mathf.Lerp(yTop, yBot, t);
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    float rr = radius[r, s % seg] + (0.009f + 0.022f * t) * h.H;
                    rr *= 1f + 0.05f * t * Mathf.Cos(a * 14f); // pleats
                    Vector3 p = new Vector3(c.x + Mathf.Cos(a) * rr, y + 0.004f * h.H * t * Mathf.Cos(a * 14f), c.y + Mathf.Sin(a) * rr);
                    float side = Mathf.Clamp(Mathf.Cos(a) * 1.4f, -1f, 1f); // +1 right side, -1 left side
                    float legW = 0.62f * Mathf.Pow(t, 1.3f);
                    BoneWeight w = OutfitMesh.Blend((iH, 1f - legW), (iL, legW * Mathf.Clamp01(0.5f - side * 0.5f)), (iR, legW * Mathf.Clamp01(0.5f + side * 0.5f)));
                    m.Add(p, new Vector2(s / (float)seg * 7f, 1f - t * 0.999f), Color.white, w);
                }
            }
            for (int r = 0; r < rows - 1; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * (seg + 1) + s, b = a + seg + 1;
                    m.Quad(a, a + 1, b + 1, b);
                }
            m.Orient(0, p => { Vector3 d = p - new Vector3(c.x, p.y, c.y); return d; });
            m.MakeDoubleSided(0.0015f * h.H);
            var mat = ToonMat("Skirt_Pleat", PatternTex("skirt"), false, Color.black);

            // waistband + bow (vertex colour)
            var band2 = new OutfitMesh();
            var ringPts = Enumerable.Range(0, seg).Select(s =>
            {
                float a = s / (float)seg * Mathf.PI * 2f, rr = radius[0, s] + 0.012f * h.H;
                return new Vector3(c.x + Mathf.Cos(a) * rr, yTop, c.y + Mathf.Sin(a) * rr);
            }).ToList();
            int t0 = band2.T.Count;
            band2.Tube(ringPts, Enumerable.Repeat(0.012f * h.H, seg).ToList(), true, 8, (k, a) => Color.Lerp(new Color(0.9f, 0.7f, 0.3f), new Color(1f, 0.88f, 0.55f), Mathf.Sin(a) * 0.5f + 0.5f),
                _ => OutfitMesh.Rigid(iH), Vector3.up, 0.45f);
            band2.Orient(t0, p => p - ClosestOnPath(ringPts, p));
            return new[] { Piece(h, "bottom_pleat_skirt", m, mat), Piece(h, "bottom_pleat_skirt_band", band2, vcMat) };
        }

        // ================================================================== hip charm
        private static OutfitPiece[] HipCharm(Hero h)
        {
            Vector3 hips = h.P("Hips"), ru = h.P("RightUpLeg");
            float y = Mathf.Max(hips.y, ru.y) + 0.02f * h.H;
            // outer surface of the right hip at that height (front-right, so it reads from the side view)
            Vector3 dir = new Vector3(0.75f, 0f, 0.45f).normalized;
            float r = 0f;
            for (int i = 0; i < h.V.Length; i++)
            {
                if (h.IsArm(i) || Mathf.Abs(h.V[i].y - y) > 0.03f * h.H) continue;
                Vector3 q = h.V[i] - new Vector3(hips.x, h.V[i].y, hips.z);
                if (Vector3.Angle(q, dir) < 20f) r = Mathf.Max(r, q.magnitude);
            }
            Vector3 pivot = new Vector3(hips.x, y, hips.z) + dir * (r + 0.03f * h.H); // hangs from the belt line, clear of a skirt's waistband
            var extras = new List<ExtraJoint> { new ExtraJoint { Name = "Outfit_HipCharm", Parent = "Hips", MeshPos = pivot } };
            var m = new OutfitMesh();
            int bone = h.Bones.Length;
            BellMesh(m, pivot, 0.05f * h.H, OutfitMesh.Rigid(bone), new Color(0.78f, 0.82f, 0.88f), new Color(0.97f, 0.98f, 1f), new Color(0.88f, 0.16f, 0.18f));
            // knot + short cord up to the belt
            var cord = new List<Vector3> { pivot + Vector3.up * 0.03f * h.H, pivot + Vector3.up * 0.012f * h.H, pivot };
            int t0 = m.T.Count;
            m.Tube(cord, new List<float> { 0.003f * h.H, 0.0028f * h.H, 0.0025f * h.H }, false, 6, (k, a) => new Color(0.8f, 0.14f, 0.16f), _ => OutfitMesh.Rigid(bone), Vector3.forward);
            m.Orient(t0, p => p - ClosestOnPath(cord, p));
            return new[] { Piece(h, "acc_silver_bell", m, vcMat, extras, PieceMotion.Pendulum) };
        }

        // ================================================================== cape / mantle
        private static OutfitPiece[] Cape(Hero h, string style)
        {
            bool star = style == "star";
            Vector3 la = h.P("LeftArm"), ra = h.P("RightArm"), neck = h.P("neck");
            float kneeY = (h.P("LeftLeg").y + h.P("RightLeg").y) * 0.5f;
            float yTop = neck.y - 0.035f * h.H, yBot = kneeY + (star ? -0.03f : 0.03f) * h.H;
            float hwTop = Mathf.Abs(ra.x - la.x) * 0.5f * 0.95f, hwBot = hwTop * (star ? 1.65f : 1.45f);
            const int rows = 12, cols = 12;
            float cx = (la.x + ra.x) * 0.5f;
            var back = new float[rows];
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1), y = Mathf.Lerp(yTop, yBot, t), hw = Mathf.Lerp(hwTop, hwBot, t);
                float z = h.BackZ(y, 0.03f * h.H, hw + 0.03f * h.H, r > 0 ? back[r - 1] : neck.z - 0.1f * h.H) - 0.01f * h.H;
                back[r] = r > 0 ? Mathf.Min(z, back[r - 1]) : z;
            }
            float zTop = back[0];
            var extras = new List<ExtraJoint>
            {
                new ExtraJoint { Name = "Outfit_CapeRoot", Parent = "Spine", MeshPos = new Vector3(cx, yTop, zTop) },
                new ExtraJoint { Name = "Outfit_Cape1", Parent = "Spine", ParentExtra = 0, MeshPos = new Vector3(cx, Mathf.Lerp(yTop, yBot, 0.4f), back[(int)(0.4f * (rows - 1))]) },
                new ExtraJoint { Name = "Outfit_Cape2", Parent = "Spine", ParentExtra = 1, MeshPos = new Vector3(cx, Mathf.Lerp(yTop, yBot, 0.75f), back[(int)(0.75f * (rows - 1))]) },
            };
            // chain parents for the runtime: Cape1 under CapeRoot, Cape2 under Cape1
            extras[1].Parent = "Outfit_CapeRoot";
            extras[2].Parent = "Outfit_Cape1";
            int j0 = h.Bones.Length, j1 = j0 + 1, j2 = j0 + 2;
            BoneWeight Wt(float t) => t < 0.4f ? OutfitMesh.Blend((j0, 1f - t / 0.4f), (j1, t / 0.4f))
                : t < 0.75f ? OutfitMesh.Blend((j1, 1f - (t - 0.4f) / 0.35f), (j2, (t - 0.4f) / 0.35f)) : OutfitMesh.Rigid(j2);
            var m = new OutfitMesh();
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1), y = Mathf.Lerp(yTop, yBot, t), hw = Mathf.Lerp(hwTop, hwBot, t);
                for (int k = 0; k <= cols; k++)
                {
                    float u = k / (float)cols, x = (u * 2f - 1f);
                    float wave = star ? 0f : 0.006f * h.H * Mathf.Sin(u * Mathf.PI * 5f) * t;
                    float hem = r == rows - 1 ? (star ? -0.03f * h.H * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) : -0.02f * h.H * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4f))) : 0f;
                    var p = new Vector3(cx + x * hw, y + hem, back[r] + Mathf.Lerp(0.09f, 0.05f, t) * h.H * x * x + wave - 0.012f * h.H * t);
                    m.Add(p, new Vector2(u * 0.5f, 1f - t), Color.white, Wt(t));
                }
            }
            for (int r = 0; r < rows - 1; r++)
                for (int k = 0; k < cols; k++)
                {
                    int a = r * (cols + 1) + k, b = a + cols + 1;
                    m.Quad(a, a + 1, b + 1, b);
                }
            m.Orient(0, _ => Vector3.back);  // outer face looks away from the hero's back
            int n = m.Count;
            m.MakeDoubleSided(0.0025f * h.H);
            for (int i = n; i < m.Count; i++) m.UV[i] = new Vector2(0.75f, m.UV[i].y); // lining half of the atlas
            bool wed = style == "wedding";
            Material mat = ToonMat(star ? "Mantle_Star" : wed ? "Cape_Wedding" : "Cape_Wind", PatternTex(star ? "starcape" : wed ? "weddingcape" : "windcape"), false,
                star ? new Color(0.05f, 0.08f, 0.18f) : Color.black);

            // collar trim + clasps (vertex colour), riding the same chain
            var trim = new OutfitMesh();
            Color tA = star ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.92f, 0.72f, 0.3f), tB = star ? new Color(1f, 0.98f, 0.8f) : new Color(1f, 0.9f, 0.55f);
            var top = Enumerable.Range(0, cols + 1).Select(k => m.V[k] + Vector3.up * 0.004f * h.H).ToList();
            int t0 = trim.T.Count;
            trim.Tube(top, Enumerable.Repeat(0.007f * h.H, top.Count).ToList(), false, 8, (k, a) => Color.Lerp(tA, tB, Mathf.Sin(a) * 0.5f + 0.5f), _ => OutfitMesh.Rigid(j0), Vector3.up);
            trim.Orient(t0, p => p - ClosestOnPath(top, p));
            foreach (Vector3 corner in new[] { top[0], top[cols] })
            {
                int tc = trim.T.Count;
                trim.Ellipsoid(Matrix4x4.TRS(corner, Quaternion.identity, Vector3.one * 0.013f * h.H), 10, 6, l => Color.Lerp(tA, tB, (l.y + 1f) * 0.5f), OutfitMesh.Rigid(j0));
                trim.Orient(tc, p => p - corner);
            }
            var hemPts = Enumerable.Range(0, cols + 1).Select(k => m.V[(rows - 1) * (cols + 1) + k]).ToList();
            t0 = trim.T.Count;
            trim.Tube(hemPts, Enumerable.Repeat(0.004f * h.H, hemPts.Count).ToList(), false, 6, (k, a) => tA, _ => OutfitMesh.Rigid(j2), Vector3.up);
            trim.Orient(t0, p => p - ClosestOnPath(hemPts, p));

            string id = star ? "set_crystal_zephyr_mantle" : wed ? "wed_white_cape" : "back_wind_cape";
            return new[]
            {
                Piece(h, id, m, mat, extras, PieceMotion.CapeSway),
                Piece(h, id + "_trim", trim, vcMat, extras, PieceMotion.None, declareExtras: false),
            };
        }

        // ================================================================== wings
        private static OutfitPiece[] Wings(Hero h, bool crystal)
        {
            Vector3 spine = h.P("Spine"), neck = h.P("neck");
            float y = Mathf.Lerp(spine.y, neck.y, 0.2f) - 0.01f * h.H;
            float z = h.BackZ(y, 0.04f * h.H, 0.08f * h.H, spine.z - 0.08f * h.H) - 0.01f * h.H;
            float span = (crystal ? 0.34f : 0.27f) * h.H;
            var extras = new List<ExtraJoint>();
            var pieces = new List<OutfitPiece>();
            var m = new OutfitMesh();
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 root = new Vector3(spine.x + s * 0.02f * h.H, y, z);
                string name = s < 0 ? "Outfit_WingL" : "Outfit_WingR";
                extras.Add(new ExtraJoint { Name = name, Parent = "Spine", MeshPos = root });
                int bone = h.Bones.Length + extras.Count - 1;
                Vector3 d = new Vector3(s * 0.55f, 0.08f, -0.83f).normalized;
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, d).normalized;
                Vector3 nrm = Vector3.Cross(d, up);
                var lobes = crystal
                    ? new[] { (25f, 1.0f, 0.3f), (-5f, 0.85f, 0.24f), (-32f, 0.62f, 0.2f) }
                    : new[] { (22f, 1.0f, 0.26f), (-24f, 0.72f, 0.22f) };
                int li = 0;
                foreach (var (tilt, len, wid) in lobes)
                {
                    Quaternion rot = Quaternion.AngleAxis(-s * tilt, nrm);
                    Vector3 ax = rot * d, ay = rot * up;
                    float L = span * len * 0.5f, Wd = span * wid * 0.5f;
                    Vector3 centre = root + ax * L;
                    int start = m.Count;
                    const int segN = 28;
                    int ci = m.Add(centre + nrm * 0.004f * h.H, new Vector2(0.5f, 0.5f), Color.white, OutfitMesh.Rigid(bone));
                    for (int k = 0; k <= segN; k++)
                    {
                        float a = k / (float)segN * Mathf.PI * 2f;
                        float ex = Mathf.Cos(a), ey = Mathf.Sin(a);
                        float taper = crystal ? 1f - 0.25f * Mathf.Max(0f, -ex) : 1f - 0.55f * Mathf.Max(0f, -ex); // narrower at the root
                        Vector3 p = centre + ax * (ex * L) + ay * (ey * Wd * taper);
                        if (crystal) p += ax * (Mathf.Abs(Mathf.Sin(a * 3f)) * 0.04f * L); // faceted tips
                        Color c = crystal ? Color.Lerp(new Color(0.42f, 0.72f, 0.98f), new Color(0.78f, 0.62f, 1f), (ex + 1f) * 0.5f) : Color.white;
                        m.Add(p, new Vector2(0.5f + ex * 0.5f, 0.5f + ey * 0.5f * taper), c, OutfitMesh.Rigid(bone));
                    }
                    for (int k = 0; k < segN; k++) m.Tri(ci, start + 1 + k, start + 2 + k);
                    m.Orient(m.T.Count - segN * 3, _ => nrm);
                    li++;
                }
            }
            m.MakeDoubleSided(0.0015f * h.H);
            Material mat = crystal ? crystalMat : ToonMat("Wings_Dragonfly", PatternTex("wing"), false, new Color(0.06f, 0.08f, 0.1f), 0.004f);
            string id = crystal ? "set_crystal_zephyr_wings" : "back_dragonfly_wings";
            pieces.Add(Piece(h, id, m, mat, extras, PieceMotion.WingFlap));

            // a small brooch where the wings meet (hides the roots)
            var brooch = new OutfitMesh();
            Vector3 bc = new Vector3(spine.x, y, z + 0.006f * h.H);
            int t0 = brooch.T.Count;
            brooch.Ellipsoid(Matrix4x4.TRS(bc, Quaternion.identity, new Vector3(0.022f, 0.018f, 0.01f) * h.H), 12, 7,
                l => crystal ? Color.Lerp(new Color(0.5f, 0.85f, 1f), Color.white, (l.y + 1) * 0.5f) : Color.Lerp(new Color(0.3f, 0.65f, 0.55f), new Color(0.75f, 0.95f, 0.85f), (l.y + 1) * 0.5f),
                OutfitMesh.Rigid(h.B("Spine")));
            brooch.Orient(t0, p => p - bc);
            pieces.Add(Piece(h, id + "_brooch", brooch, crystal ? crystalMat : vcMat));
            return pieces.ToArray();
        }

        // ================================================================== premium set
        private static OutfitPiece[] CrystalSet(Hero h)
        {
            var pieces = new List<OutfitPiece>();
            pieces.AddRange(Wings(h, true));
            pieces.AddRange(Cape(h, "star"));
            // crystal crown: slim silver band + shards rising from it
            Ring ring = HeadRing(h, 0.003f, 0.02f);
            BoneWeight w = OutfitMesh.Rigid(h.B("Head"));
            var crown = new OutfitMesh();
            var path = Smooth(ring.Points, 3).ToList();
            int t0 = crown.T.Count;
            crown.Tube(path, Enumerable.Repeat(0.006f * h.H, path.Count).ToList(), true, 8, (k, a) => Color.Lerp(new Color(0.75f, 0.85f, 0.95f), Color.white, Mathf.Sin(a) * 0.5f + 0.5f), _ => w, Vector3.up, 0.4f);
            crown.Orient(t0, p => p - ClosestOnPath(path, p));
            foreach (var (ang, height) in new[] { (90f, 0.075f), (66f, 0.05f), (114f, 0.05f), (42f, 0.035f), (138f, 0.035f), (18f, 0.025f), (162f, 0.025f) })
            {
                Vector3 p = RingAt(ring, ang, out Vector3 o);
                Vector3 axis = (Vector3.up + o * 0.25f).normalized;
                Quaternion q = Quaternion.LookRotation(Vector3.Cross(axis, o).normalized, axis);
                crown.Shard(Matrix4x4.TRS(p, q, new Vector3(0.012f, height, 0.012f) * h.H), 6, 0.35f, new Color(0.9f, 0.98f, 1f), new Color(0.35f, 0.7f, 1f), w);
            }
            pieces.Add(Piece(h, "set_crystal_zephyr_crown", crown, crystalMat));
            // aura: sparkles drifting up around the hero + a soft glow at the feet (prefab on the hips)
            pieces.Add(new OutfitPiece { prefab = AuraPrefab(), mountBone = "Hips" });
            return pieces.ToArray();
        }

        private static GameObject AuraPrefab()
        {
            string path = $"{Dir}/Aura_CrystalZephyr.prefab";
            var root = new GameObject("Aura_CrystalZephyr");
            var sparkle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/sparkle_add.mat");
            var dot = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/dot_add.mat");
            void System(string name, Material mat, float rate, float life, Vector2 size, Color a, Color b, float speed, float radius, float yOff, float gravity)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, yOff, 0f);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.duration = 2f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
                main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                main.gravityModifier = gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.maxParticles = 60;
                main.playOnAwake = true;
                var em = ps.emission;
                em.rateOverTime = rate;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius;
                sh.rotation = new Vector3(90f, 0f, 0f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            System("Sparkles", sparkle, 9f, 1.6f, new Vector2(0.05f, 0.11f), new Color(0.7f, 0.92f, 1f, 1f), new Color(1f, 1f, 1f, 1f), 0.15f, 0.42f, -0.3f, -0.05f);
            System("Motes", dot, 6f, 2.2f, new Vector2(0.06f, 0.14f), new Color(0.45f, 0.8f, 1f, 0.6f), new Color(0.8f, 0.7f, 1f, 0.6f), 0.08f, 0.3f, -0.35f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ================================================================== pattern textures
        public static Texture2D PatternTex(string kind)
        {
            if (texCache.TryGetValue(kind, out Texture2D cachedTex) && cachedTex) return cachedTex;
            const int S = 256;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    t.SetPixel(x, y, PatternPixel(kind, (x + 0.5f) / S, (y + 0.5f) / S));
            t.Apply();
            string path = $"{Dir}/Textures/pattern_{kind}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = kind is "windcape" or "starcape" or "weddingcape" or "skirt" or "wing" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (kind == "skirt") { ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; }
            ti.mipmapEnabled = true;
            ti.anisoLevel = 2;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            texCache[kind] = tex;
            return tex;
        }

        private static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);

        private static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
        }

        /// <summary>Distance to a cloud-curl motif (spiral) centred in a tile; 0 on the line.</summary>
        private static float Curl(float u, float v, float cx, float cy, float scale, float turns)
        {
            float dx = (u - cx) / scale, dy = (v - cy) / scale;
            float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
            if (r > 1f) return 1f;
            float best = 1f;
            for (int k = 0; k < turns + 1; k++)
            {
                float spiralR = (a + Mathf.PI) / (2f * Mathf.PI) / turns + k / (float)turns;
                best = Mathf.Min(best, Mathf.Abs(r - spiralR));
            }
            return best * scale;
        }

        private static Color PatternPixel(string kind, float u, float v)
        {
            if (ThemePattern(kind, u, v, out Color themed)) return themed; // OutfitThemes.cs
            switch (kind)
            {
                case "brocade":
                {
                    Color baseC = Color.Lerp(new Color(0.6f, 0.09f, 0.13f), new Color(0.72f, 0.14f, 0.16f), Noise(u * 12f, v * 12f) * 0.6f);
                    Color gold = new Color(0.98f, 0.8f, 0.38f);
                    float d = Mathf.Min(Curl(u, v, 0.27f, 0.3f, 0.2f, 1.5f), Curl(Mathf.Repeat(u + 0.5f, 1f), Mathf.Repeat(v + 0.5f, 1f), 0.27f, 0.3f, 0.2f, 1.5f));
                    float line = Mathf.Clamp01(1f - d / 0.012f);
                    float dots = Mathf.Clamp01(1f - (new Vector2(Mathf.Repeat(u * 8f, 1f) - 0.5f, Mathf.Repeat(v * 8f, 1f) - 0.5f).magnitude) / 0.08f);
                    return Color.Lerp(Color.Lerp(baseC, gold * 0.85f, dots * 0.6f), gold, line);
                }
                case "satin":
                {
                    // white wedding satin: soft sheen bands, fine weave, a gold stitch line
                    float sheen = 0.5f + 0.5f * Mathf.Sin((u * 0.6f + v) * Mathf.PI * 5f);
                    float weave = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 64f)) * Mathf.Abs(Mathf.Sin(v * Mathf.PI * 64f));
                    Color c = Color.Lerp(new Color(0.88f, 0.88f, 0.9f), new Color(1f, 0.99f, 0.98f), sheen * 0.6f + v * 0.2f) * (0.95f + 0.05f * weave);
                    float stitch = Mathf.Repeat(u * 16f, 1f) < 0.55f && Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f) < 0.02f ? 1f : 0f;
                    c.a = 1f;
                    return Color.Lerp(c, new Color(0.95f, 0.78f, 0.4f), stitch * 0.8f);
                }
                case "weddingcape":
                {
                    if (u > 0.5f) return new Color(0.98f, 0.9f, 0.88f); // blush lining
                    float uu = u * 2f;
                    Color c = Color.Lerp(new Color(0.9f, 0.89f, 0.86f), new Color(1f, 0.99f, 0.96f), v * 0.7f + 0.3f * Noise(uu * 8f, v * 8f));
                    float edge = Mathf.Min(uu, 1f - uu, v);
                    if (edge < 0.035f) return new Color(0.93f, 0.76f, 0.38f);   // gold border
                    float d = Curl(Mathf.Repeat(uu * 2f, 1f), Mathf.Repeat(v * 2f, 1f), 0.5f, 0.5f, 0.32f, 1.5f);
                    float line = Mathf.Clamp01(1f - d / 0.02f);                   // gold cloud-curl embroidery
                    return Color.Lerp(c, new Color(0.95f, 0.78f, 0.42f), line * 0.85f);
                }
                case "jade":
                {
                    Color a = new Color(0.16f, 0.58f, 0.52f), b = new Color(0.42f, 0.84f, 0.74f);
                    float sheen = 0.5f + 0.5f * Mathf.Sin((u + v) * Mathf.PI * 6f);
                    float weave = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 64f)) * Mathf.Abs(Mathf.Sin(v * Mathf.PI * 64f));
                    Color c = Color.Lerp(a, b, sheen * 0.45f + v * 0.25f) * (0.93f + 0.07f * weave);
                    float stitch = Mathf.Repeat(u * 16f, 1f) < 0.55f && Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f) < 0.02f ? 1f : 0f;
                    c.a = 1f;
                    return Color.Lerp(c, new Color(0.9f, 0.96f, 0.95f), stitch * 0.6f);
                }
                case "cloud":
                {
                    Color c = Color.Lerp(new Color(0.93f, 0.94f, 0.96f), new Color(0.99f, 0.99f, 1f), Noise(u * 10f, v * 10f));
                    float wave = Mathf.Abs(Mathf.Repeat(v * 3f, 1f) - (0.5f + 0.12f * Mathf.Sin(u * Mathf.PI * 8f)));
                    float dash = Mathf.Repeat(u * 24f, 1f) < 0.6f ? 1f : 0f;
                    return Color.Lerp(c, new Color(0.45f, 0.68f, 0.92f), Mathf.Clamp01(1f - wave / 0.025f) * dash);
                }
                case "ember":
                {
                    float n = Noise(u * 9f, v * 9f) * 0.6f + Noise(u * 23f, v * 23f) * 0.4f;
                    Color c = Color.Lerp(new Color(0.36f, 0.1f, 0.07f), new Color(0.58f, 0.2f, 0.1f), n);
                    float stitch = Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f) < 0.01f && Mathf.Repeat(v * 20f, 1f) < 0.55f ? 1f : 0f;
                    return Color.Lerp(c, new Color(0.95f, 0.72f, 0.4f), stitch);
                }
                case "skirt":
                {
                    // u repeats around the skirt, v: 1 at waist -> 0 at hem; hem band is the bottom 16%
                    float pleat = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 4f);
                    Color c = Color.Lerp(new Color(0.55f, 0.7f, 0.95f), new Color(0.78f, 0.86f, 1f), pleat * 0.6f + v * 0.25f);
                    if (v < 0.16f)
                    {
                        float wave = Mathf.Abs(v - (0.08f + 0.04f * Mathf.Sin(u * Mathf.PI * 8f)));
                        Color band = new Color(0.2f, 0.32f, 0.62f);
                        c = Color.Lerp(band, new Color(1f, 0.92f, 0.65f), Mathf.Clamp01(1f - wave / 0.012f));
                        if (v > 0.145f) c = new Color(0.98f, 0.85f, 0.5f);
                    }
                    return c;
                }
                case "windcape":
                case "starcape":
                {
                    bool starK = kind == "starcape";
                    if (u > 0.5f) return starK ? new Color(0.36f, 0.32f, 0.72f) : new Color(0.72f, 0.14f, 0.16f); // lining
                    float uu = u * 2f;
                    Color c = starK ? Color.Lerp(new Color(0.06f, 0.09f, 0.28f), new Color(0.2f, 0.18f, 0.48f), v)
                                    : Color.Lerp(new Color(0.12f, 0.36f, 0.48f), new Color(0.2f, 0.52f, 0.62f), v);
                    float edge = Mathf.Min(uu, 1f - uu, v);
                    if (edge < 0.035f) return starK ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.9f, 0.75f, 0.35f);
                    if (starK)
                    {
                        for (int k = 0; k < 40; k++)
                        {
                            float sx = Hash(k, 1.3f), sy = Hash(k, 7.1f), sr = 0.006f + 0.012f * Hash(k, 3.7f);
                            float dx = Mathf.Abs(uu - sx), dy = Mathf.Abs(v - sy);
                            float star = Mathf.Clamp01(1f - (dx * dy * 900f + (dx + dy) * 0.4f / sr) * 0.5f);
                            c = Color.Lerp(c, new Color(1f, 0.97f, 0.8f), star);
                        }
                        return c;
                    }
                    float d = Curl(Mathf.Repeat(uu * 2f, 1f), Mathf.Repeat(v * 3f, 1f), 0.5f, 0.5f, 0.35f, 1.5f);
                    return Color.Lerp(c, new Color(0.62f, 0.85f, 0.88f), Mathf.Clamp01(1f - d / 0.02f) * 0.8f);
                }
                case "wing":
                {
                    // iridescent membrane with dark veins radiating from the root (u = 0 side)
                    float x = u, y = v - 0.5f;
                    Color c = Color.Lerp(new Color(0.72f, 0.95f, 0.95f), new Color(0.95f, 0.78f, 0.95f), Mathf.Clamp01(x * 0.9f + y * 0.6f));
                    c = Color.Lerp(c, new Color(0.6f, 0.82f, 1f), Mathf.Clamp01(Mathf.Abs(y) * 1.4f));
                    float ang = Mathf.Atan2(y, x + 0.15f);
                    float vein = Mathf.Abs(Mathf.Sin(ang * 9f)) < 0.07f ? 1f : 0f;
                    float cross = Mathf.Abs(Mathf.Sin(Mathf.Sqrt((x + 0.15f) * (x + 0.15f) + y * y) * 40f)) < 0.06f ? 0.6f : 0f;
                    float rim = new Vector2(x - 0.5f, y).magnitude > 0.47f ? 1f : 0f;
                    return Color.Lerp(c, new Color(0.18f, 0.3f, 0.36f), Mathf.Max(vein, cross, rim) * 0.75f);
                }
            }
            return Color.magenta;
        }
    }
}
