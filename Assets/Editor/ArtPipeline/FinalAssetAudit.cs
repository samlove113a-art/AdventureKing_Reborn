using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Last-pass static audit (no Play Mode): prefabs (missing scripts / references), actor animators (states with sprite clips),
    /// scenes (missing scripts / references, Canvas setup, Grid / Tilemap config) and sprite import settings.
    ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.FinalAssetAudit.Run   -> QA/Logs/final_audit.txt</summary>
    public static class FinalAssetAudit
    {
        static readonly StringBuilder Sb = new StringBuilder();
        static int _errors, _warnings;
        static void Err(string s) { _errors++; Sb.AppendLine("  [ERROR] " + s); }
        static void Warn(string s) { _warnings++; Sb.AppendLine("  [warn]  " + s); }

        public static void Run()
        {
            try
            {
                Sb.AppendLine("# Final asset audit " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                AuditPrefabs();
                AuditActors();
                AuditScenes();
                AuditSpriteImports();
                Sb.AppendLine($"\nSUMMARY: errors={_errors} warnings={_warnings}");
                Directory.CreateDirectory("QA/Logs");
                File.WriteAllText("QA/Logs/final_audit.txt", Sb.ToString());
                Debug.Log("[FINALAUDIT] errors=" + _errors + " warnings=" + _warnings);
                EditorApplication.Exit(_errors == 0 ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
        }

        static int MissingRefs(GameObject root, string where)
        {
            int n = 0;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (!c) continue;
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue == null && !it.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)))
                    { n++; Err($"{where}: missing reference {c.GetType().Name}.{it.propertyPath} on {c.gameObject.name}"); }
                }
            }
            return n;
        }

        static void AuditPrefabs()
        {
            Sb.AppendLine("\n## Prefabs");
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int bad = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int ms = 0;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) ms += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (ms > 0) { Err($"{path}: {ms} missing script(s)"); bad++; }
                    if (MissingRefs(root, path) > 0) bad++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Sb.AppendLine($"  {guids.Length} prefabs checked, {bad} with problems");
        }

        static void AuditActors()
        {
            Sb.AppendLine("\n## Actors (NPC / monsters): Resources/Chibi2D/Actors");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Chibi2D/Actors" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path).Replace("_Visual2D", "");
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var anim = go.GetComponent<Animator>();
                var ctrl = anim ? anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
                if (!ctrl) { Err($"{name}: no AnimatorController"); continue; }
                var states = ctrl.layers[0].stateMachine.states.Select(s => s.state).ToList();
                var info = new List<string>();
                bool hasIdle = false;
                int ppuSeen = 0; float ppu = 0;
                foreach (var st in states)
                {
                    var clip = st.motion as AnimationClip;
                    if (!clip) { Err($"{name}: state {st.name} has no clip"); continue; }
                    var b = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    int frames = b.Length > 0 ? AnimationUtility.GetObjectReferenceCurve(clip, b[0]).Length : 0;
                    if (frames == 0) Err($"{name}: clip {clip.name} has no sprite frames");
                    if (st.name == "Idle") hasIdle = true;
                    info.Add($"{st.name}:{frames}");
                    if (b.Length > 0 && ppuSeen == 0)
                    {
                        var s = AnimationUtility.GetObjectReferenceCurve(clip, b[0])[0].value as Sprite;
                        if (s) { ppu = s.pixelsPerUnit; ppuSeen = 1; }
                    }
                }
                if (!hasIdle) Warn($"{name}: no Idle state");
                bool pixelNew = Math.Abs(ppu - 101.1f) < 1f;
                Sb.AppendLine($"  {name,-18} ppu {ppu,6:0.0} {(pixelNew ? "[new pixel 192]" : "[old style]")}  {string.Join(" ", info)}");
                if (!pixelNew) Warn($"{name}: still the previous art style (ppu {ppu:0.0}, not the new 192-px pixel set)");
            }
        }

        static void AuditScenes()
        {
            Sb.AppendLine("\n## Scenes (Grid / Tilemap / Canvas / missing)");
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes", "Assets/QA_Internal" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            int tilemaps = 0, grids = 0, canvases = 0;
            foreach (string sp in scenes)
            {
                var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                int ms = 0, mr = 0;
                foreach (var root in sc.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) ms += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    mr += MissingRefs(root, sp);
                }
                if (ms > 0) Err($"{sp}: {ms} missing script(s)");
                var tms = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
                var gs = UnityEngine.Object.FindObjectsByType<Grid>(FindObjectsInactive.Include);
                tilemaps += tms.Length; grids += gs.Length;
                foreach (var g in gs)
                    if (Mathf.Abs(g.cellSize.x - g.cellSize.y) > 0.001f || g.cellSize.x <= 0) Warn($"{sp}: Grid '{g.name}' cell {g.cellSize}");
                var cvs = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Where(c => c.isRootCanvas).ToArray();
                canvases += cvs.Length;
                foreach (var c in cvs)
                {
                    var cs = c.GetComponent<CanvasScaler>();
                    string mode = c.renderMode.ToString();
                    if (!cs) Warn($"{sp}: Canvas '{c.name}' has no CanvasScaler");
                    else if (cs.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) Warn($"{sp}: Canvas '{c.name}' ConstantPixelSize ({mode})");
                    else if (cs.referenceResolution.x < 640) Warn($"{sp}: Canvas '{c.name}' reference {cs.referenceResolution}");
                }
                var srs = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include).Where(r => r.sprite == null && r.enabled).Count();
                if (srs > 0) Warn($"{sp}: {srs} enabled SpriteRenderer(s) without a sprite");
                Sb.AppendLine($"  {sp.Replace("Assets/", ""),-42} missingScripts={ms} missingRefs={mr} grids={gs.Length} tilemaps={tms.Length} canvases={cvs.Length}");
            }
            Sb.AppendLine($"  TOTAL grids={grids} tilemaps={tilemaps} canvases={canvases}" + (tilemaps == 0 ? "  (the project has NO Tilemap: maps are built from sprites/decks in code)" : ""));
        }

        static void AuditSpriteImports()
        {
            Sb.AppendLine("\n## Sprite import settings (Resources/Chibi2D, Art/Chibi2D)");
            int bad = 0, total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Chibi2D", "Assets/Resources/Chibi2D" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (!ti || ti.textureType != TextureImporterType.Sprite) continue;
                total++;
                if (ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled)
                { bad++; if (bad <= 15) Warn($"{p}: filter {ti.filterMode}, compression {ti.textureCompression}, mips {ti.mipmapEnabled}"); }
            }
            Sb.AppendLine($"  {total} sprite textures, {bad} not Point/Uncompressed/no-mips");
        }
    }
}
