using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Fast batch check of the pixel hero prefabs (no Play Mode): prefab loads, no Missing Script, the override controller maps
    /// every gameplay clip to a sprite clip with frames, sprites are Point-filtered and uncompressed.
    ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.PixelHeroVerify.Run</summary>
    public static class PixelHeroVerify
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            bool ok = true;
            foreach (string cls in new[] { "Swordsman", "Mage", "Assassin" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Chibi2D/{cls}_Visual2D.prefab");
                if (!prefab) { sb.AppendLine($"[FAIL] {cls}: prefab missing"); ok = false; continue; }
                var go = UnityEngine.Object.Instantiate(prefab);
                int missing = 0;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                var anim = go.GetComponent<Animator>();
                var ctrl = anim ? anim.runtimeAnimatorController as AnimatorOverrideController : null;
                sb.AppendLine($"[{cls}] missingScripts={missing} animator={(anim ? "yes" : "NO")} overrideController={(ctrl ? "yes" : "NO")}");
                if (missing > 0 || !ctrl) ok = false;
                if (ctrl)
                {
                    var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
                    ctrl.GetOverrides(pairs);
                    foreach (var kv in pairs)
                    {
                        if (!kv.Value) { sb.AppendLine($"   gameplay clip {kv.Key.name}: NOT overridden (falls back to the old clip)"); continue; }
                        var bind = AnimationUtility.GetObjectReferenceCurveBindings(kv.Value);
                        int frames = bind.Length > 0 ? AnimationUtility.GetObjectReferenceCurve(kv.Value, bind[0]).Length : 0;
                        var evs = AnimationUtility.GetAnimationEvents(kv.Value).Select(e => e.functionName);
                        sb.AppendLine($"   {kv.Key.name,-8} -> {kv.Value.name,-22} frames={frames} len={kv.Value.length:0.00}s loop={kv.Value.isLooping} events=[{string.Join(",", evs)}]");
                        if (frames == 0) ok = false;
                    }
                }
                foreach (string clip in new[] { "Idle", "Run", "Jump", "Fall", "Climb", "Hit", "Death", "Attack", "Skill" })
                {
                    string p = $"Assets/Art/Chibi2D/{cls}/{cls}_{clip}.png";
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (!ti) { sb.AppendLine($"   [FAIL] {p} missing"); ok = false; continue; }
                    if (ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled)
                    { sb.AppendLine($"   [FAIL] {p} import settings (filter {ti.filterMode}, compression {ti.textureCompression})"); ok = false; }
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("[PIXELVERIFY]\n" + sb + (ok ? "RESULT: PASS" : "RESULT: FAIL"));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}

