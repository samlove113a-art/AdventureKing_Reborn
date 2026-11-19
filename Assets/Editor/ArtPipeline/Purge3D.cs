using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// The game is 100 % pure 2D pixel art. Report (read-only) and Purge (destructive, recoverable from git tag pre-2d-purge) of every 3D leftover:
    /// model files, 3D-only materials, 3D components on every scene / prefab, 3D lights and perspective cameras.
    ///   Unity -batchmode -nographics -quit -executeMethod AdventureKing.ArtPipeline.Purge3D.ReportFromCommandLine
    /// </summary>
    public static class Purge3D
    {
        private static readonly string[] ModelExt = { ".fbx", ".obj", ".glb", ".gltf", ".blend", ".dae", ".3ds" };
        private static readonly System.Type[] Comps3D = { typeof(MeshRenderer), typeof(MeshFilter), typeof(SkinnedMeshRenderer), typeof(Collider), typeof(Rigidbody), typeof(Light),
                                                           typeof(MeshCollider), typeof(CharacterController), typeof(ReflectionProbe), typeof(LightProbeGroup), typeof(Animator) };

        public static void ReportFromCommandLine()
        {
            try { File.WriteAllText("QA/Logs/purge3d_report.txt", Report()); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            string[] models = AssetDatabase.FindAssets("t:Model", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => ModelExt.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray();
            sb.AppendLine($"MODELS {models.Length}");
            foreach (string p in models) sb.AppendLine("  " + p);

            sb.AppendLine("MATERIALS by shader:");
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).GroupBy(p => AssetDatabase.LoadAssetAtPath<Material>(p) is Material m && m.shader ? m.shader.name : "<none>").OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Count(),4}  {g.Key}   e.g. {g.First()}");

            sb.AppendLine("PREFABS with 3D components:");
            int pc = 0;
            foreach (string p in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!go) continue;
                int n = go.GetComponentsInChildren<Component>(true).Count(c => c != null && Comps3D.Any(t => t.IsInstanceOfType(c)) && !(c is Animator));
                if (n > 0) { pc++; sb.AppendLine($"  {n,4}  {p}"); }
            }
            sb.AppendLine($"  prefabs with 3D: {pc}");

            sb.AppendLine("SCENES:");
            foreach (string p in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                Scene sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                var all = sc.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToList();
                var cams = all.OfType<Camera>().ToList();
                sb.AppendLine($"  {p}: mesh={all.OfType<MeshRenderer>().Count()} skinned={all.OfType<SkinnedMeshRenderer>().Count()} col3D={all.OfType<Collider>().Count()} rb3D={all.OfType<Rigidbody>().Count()} lights={all.OfType<Light>().Count()} cameras={cams.Count} perspective={cams.Count(c => !c.orthographic)}");
            }
            return sb.ToString();
        }
    }
}
