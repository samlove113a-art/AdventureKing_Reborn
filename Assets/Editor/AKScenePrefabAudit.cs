using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.EditorTools
{
    /// <summary>
    /// Read-only source audit. Does not open, modify or save scenes/prefabs.
    /// Checks broken MonoBehaviour script GUID references before Unity builds.
    /// This is NOT a substitute for compiling, PlayMode tests or device QA.
    /// </summary>
    public static class AKScenePrefabAudit
    {
        private static readonly Regex ScriptGuid = new Regex(
            @"m_Script:\s*\{[^}]*?guid:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("AdventureKing/QA (internal)/Audit Scenes and Prefabs")]
        public static void RunMenu() { Run(); }

        // -batchmode -executeMethod AdventureKing.EditorTools.AKScenePrefabAudit.RunBatch
        public static void RunBatch()
        {
            int errors = Run();
            if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        public static int Run()
        {
            var problems = new List<string>();
            var warnings = new List<string>();
            var details = new List<string>();
            var enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToArray();

            if (enabledScenes.Length == 0)
                problems.Add("No enabled scenes in Build Settings; a playable build requires a startup scene.");

            foreach (string path in enabledScenes)
            {
                if (!File.Exists(path))
                    problems.Add("Enabled Build Settings scene does not exist: " + path);
                if (path.Replace('\\', '/').Contains("/QA_Internal/"))
                    problems.Add("Internal QA scene is enabled in production Build Settings: " + path);
            }

            var paths = AssetDatabase.FindAssets("t:Scene").Concat(AssetDatabase.FindAssets("t:Prefab"))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(p => p).ToArray();

            int checkedAssets = 0, checkedReferences = 0;
            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    problems.Add("Indexed scene/prefab missing on disk: " + path);
                    continue;
                }

                string content;
                try { content = File.ReadAllText(path); }
                catch (Exception e)
                {
                    warnings.Add("Cannot read " + path + ": " + e.Message);
                    continue;
                }

                // Unity YAML is text; binary scene/prefab files need native Unity inspection.
                if (!content.StartsWith("%YAML", StringComparison.Ordinal) &&
                    !content.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    warnings.Add("Not serialized as YAML, manual inspection required: " + path);
                    continue;
                }

                checkedAssets++;
                var missingInAsset = new HashSet<string>();
                foreach (Match match in ScriptGuid.Matches(content))
                {
                    checkedReferences++;
                    string guid = match.Groups[1].Value;
                    if (guid == "00000000000000000000000000000000") continue;
                    string resolved = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(resolved))
                        missingInAsset.Add(guid);
                }

                foreach (string guid in missingInAsset.OrderBy(g => g))
                    problems.Add("Missing MonoBehaviour script GUID " + guid + " in " + path);
            }

            details.Add("Unity: " + Application.unityVersion);
            details.Add("Enabled build scenes: " + enabledScenes.Length);
            details.Add("Scenes/prefabs scanned as YAML: " + checkedAssets + " of " + paths.Length);
            details.Add("Script references checked: " + checkedReferences);
            details.Add("Errors: " + problems.Count);
            details.Add("Warnings: " + warnings.Count);
            details.Add("");
            details.Add("ERRORS");
            details.AddRange(problems.Count == 0 ? new[] { "None found by static audit." } : problems);
            details.Add("");
            details.Add("WARNINGS");
            details.AddRange(warnings.Count == 0 ? new[] { "None." } : warnings);
            details.Add("");
            details.Add("IMPORTANT: Static checks cannot verify broken Inspector fields, art quality,");
            details.Add("scene runtime wiring, C# compile, networking, input, FPS or mobile builds.");

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dir = Path.Combine(root, "Logs");
            Directory.CreateDirectory(dir);
            string report = Path.Combine(dir, "AKScenePrefabAudit.txt");
            File.WriteAllText(report, string.Join(Environment.NewLine, details), Encoding.UTF8);
            if (problems.Count > 0)
                Debug.LogError("[AK Audit] " + problems.Count + " errors, " + warnings.Count +
                               " warnings. Report: " + report);
            else
                Debug.Log("[AK Audit] No missing script GUIDs in scanned YAML assets. " +
                          warnings.Count + " warnings. Report: " + report);
            return problems.Count;
        }
    }
}
