using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Dumps each hero's skeleton and per-bone vertex extents (bind pose, prefab space) for outfit fitting.</summary>
    public static class OutfitProbe
    {
        public static void Meshes()
        {
            var sb = new StringBuilder();
            foreach (string g in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Art/Outfits/Meshes" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(g));
                if (!m.name.Contains("Swordsman")) continue;
                sb.AppendLine($"{m.name}: v{m.vertexCount} t{m.triangles.Length / 3} bounds {m.bounds} bw{m.boneWeights.Length} bp{m.bindposes.Length} sub{m.subMeshCount}");
            }
            var lib = AssetDatabase.LoadAssetAtPath<AdventureKing.Gameplay.CosmeticLibrary>("Assets/Resources/CosmeticLibrary.asset");
            foreach (var a in lib.items) foreach (var f in a.fits) foreach (var p in f.pieces)
                sb.AppendLine($"{a.itemId} {f.heroClass}: mesh {(p.mesh ? p.mesh.name : "NULL")} mat {(p.material ? p.material.name : "NULL")} bones {p.boneNames.Length} extras {p.extraBones.Length}");
            File.WriteAllText("QA/Outfits/meshes.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (string c in new[] { "Swordsman", "Mage", "Assassin", "Archer" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{c}_Visual.prefab");
                var go = (GameObject)Object.Instantiate(prefab);
                var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => !s.name.StartsWith("Weapon_"));
                Mesh m = smr.sharedMesh;
                sb.AppendLine($"== {c} body '{smr.name}' verts {m.vertexCount} bones {smr.bones.Length} root {smr.rootBone?.name} smrScale {smr.transform.lossyScale} subMeshes {m.subMeshCount}");
                var v = m.vertices; var w = m.boneWeights;
                Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
                for (int b = 0; b < smr.bones.Length; b++)
                {
                    var pts = Enumerable.Range(0, v.Length).Where(i => w[i].boneIndex0 == b && w[i].weight0 > 0.5f).Select(i => toWorld.MultiplyPoint3x4(v[i])).ToArray();
                    Transform t = smr.bones[b];
                    string ext = pts.Length == 0 ? "-" : $"x[{pts.Min(p => p.x):0.000},{pts.Max(p => p.x):0.000}] y[{pts.Min(p => p.y):0.000},{pts.Max(p => p.y):0.000}] z[{pts.Min(p => p.z):0.000},{pts.Max(p => p.z):0.000}]";
                    sb.AppendLine($"  {b,2} {t.name,-22} parent {t.parent?.name,-18} pos {t.position} n={pts.Length} {ext}");
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) sb.AppendLine("  renderer " + r.name + " " + r.GetType().Name + " bounds " + r.bounds);
                Object.DestroyImmediate(go);
            }
            Directory.CreateDirectory("QA/Outfits");
            File.WriteAllText("QA/Outfits/probe.txt", sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
