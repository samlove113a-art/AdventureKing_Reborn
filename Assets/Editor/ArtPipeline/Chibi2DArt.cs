using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// 2D chibi bodies baked from the rigged 3D models (Tools/blender/bake_chibi2d.py → ArtSource/Chibi2D/{Name}/*.png).
    /// Imports the strips as sprite sheets, builds one sprite AnimationClip per state, an AnimatorOverrideController on top of the
    /// hero's EXISTING controller (same states, parameters and transitions, so CharacterVisual3D, remote players and the skill
    /// director work unchanged) and the prefab Resources/Chibi2D/{Class}_Visual2D. The 3D model and controller are never modified.
    ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.Chibi2DArt.BuildMageFromCommandLine
    /// </summary>
    public static class Chibi2DArt
    {
        private const string Src = "ArtSource/Chibi2D", Dst = "Assets/Art/Chibi2D", Res = "Assets/Resources/Chibi2D";

        [Serializable] private class ClipMeta { public int frames; public float fps; public bool loop; public string file; }
        private static readonly Dictionary<string, (string state, (float at, string fn)[] events)> Hooks = new()
        {
            // animation events keep the gameplay timing of the 3D clips (fractions of the clip): impact / cast frames
            ["Attack"] = ("Attack", new[] { (0.35f, "OnAttackImpact") }),
            ["Skill"] = ("Skill", new[] { (0.1f, "OnSkillCast"), (0.68f, "OnSkillImpact") }),
        };

        public static void BuildMageFromCommandLine()
        {
            try { Build(HeroClass.Mage); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/Art/Build 2D Chibi (Mage)")]
        public static void BuildMage() => Build(HeroClass.Mage);

        public static void Build(HeroClass cls)
        {
            string name = cls.ToString();
            string metaPath = $"{Src}/{name}/{name}_frames.json";
            if (!File.Exists(metaPath)) throw new FileNotFoundException("run bake_chibi2d.py first", metaPath);
            string json = File.ReadAllText(metaPath);
            float cell = ReadFloat(json, "cell"), frameM = ReadFloat(json, "frame_m"), ground = ReadFloat(json, "ground_from_bottom_m");
            var clips = ParseClips(json);
            Directory.CreateDirectory($"{Dst}/{name}");
            Directory.CreateDirectory(Res);
            float ppu = cell / frameM;                     // 1 model metre = ppu px: same scale as the 3D body
            Vector2 pivot = new Vector2(0.5f, ground / frameM);  // feet on the baseline

            var spriteClips = new Dictionary<string, AnimationClip>();
            foreach (var kv in clips)
            {
                string dst = $"{Dst}/{name}/{kv.Value.file}";
                File.Copy($"{Src}/{name}/{kv.Value.file}", dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Multiple;
                ti.spritePixelsPerUnit = ppu;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 4096;
                var sheet = new SpriteMetaData[kv.Value.frames];
                for (int i = 0; i < sheet.Length; i++)
                    sheet[i] = new SpriteMetaData { name = $"{name}_{kv.Key}_{i}", rect = new Rect(i * cell, 0, cell, cell), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable CS0618
                ti.spritesheet = sheet;
#pragma warning restore CS0618
                ti.SaveAndReimport();
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
                spriteClips[kv.Key] = MakeClip($"{Dst}/{name}/{name}_{kv.Key}.anim", $"{name}2D_{kv.Key}", frames, kv.Value);
            }

            // the hero's existing controller: same states / parameters / transitions, only the motions are swapped
            var baseCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Art/Characters/{name}/{name}.controller");
            if (!baseCtrl) throw new FileNotFoundException("hero controller missing", name);
            var ov = new AnimatorOverrideController(baseCtrl);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            ov.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
                if (spriteClips.TryGetValue(pairs[i].Key.name, out AnimationClip sc)) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, sc);
            ov.ApplyOverrides(pairs);
            string ovPath = $"{Dst}/{name}/{name}2D.overrideController";
            AssetDatabase.DeleteAsset(ovPath);
            AssetDatabase.CreateAsset(ov, ovPath);

            // prefab: root (Animator + events + marker) / Sprite
            var root = new GameObject($"{name}_Visual2D");
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = spriteClips["Idle"] ? AssetDatabase.LoadAllAssetsAtPath($"{Dst}/{name}/{clips["Idle"].file}").OfType<Sprite>().OrderBy(s => s.name).First() : null;
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.sortingOrder = 5;
            var anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = ov;
            anim.applyRootMotion = false;
            root.AddComponent<CharacterAnimationEvents>();
            root.AddComponent<Chibi2DMarker>().Renderer = sr;
            string prefabPath = $"{Res}/{name}_Visual2D.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CHIBI2D] {name}: {clips.Count} clips, ppu {ppu:0.0}, prefab {prefabPath}");
        }

        private static AnimationClip MakeClip(string path, string clipName, Sprite[] frames, ClipMeta m)
        {
            AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { name = clipName, frameRate = m.fps };
            var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "Sprite", propertyName = "m_Sprite" };
            var keys = new ObjectReferenceKeyframe[frames.Length + (m.loop ? 1 : 0)];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / m.fps, value = frames[i] };
            if (m.loop) keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / m.fps, value = frames[0] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = m.loop;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            string key = clipName.Substring(clipName.IndexOf('_') + 1);
            if (Hooks.TryGetValue(key, out var h))
            {
                float len = frames.Length / m.fps;
                AnimationUtility.SetAnimationEvents(clip, h.events.Select(e => new AnimationEvent { time = e.at * len, functionName = e.fn }).ToArray());
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // tiny JSON readers (the strips file is machine-written, flat and small)
        private static float ReadFloat(string json, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\":\\s*([0-9.]+)");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, ClipMeta> ParseClips(string json)
        {
            var result = new Dictionary<string, ClipMeta>();
            var rx = new System.Text.RegularExpressions.Regex("\"(\\w+)\":\\s*\\{\\s*\"frames\":\\s*(\\d+),\\s*\"fps\":\\s*(\\d+),\\s*\"loop\":\\s*(true|false),\\s*\"file\":\\s*\"([^\"]+)\"");
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(json))
                result[m.Groups[1].Value] = new ClipMeta { frames = int.Parse(m.Groups[2].Value), fps = float.Parse(m.Groups[3].Value), loop = m.Groups[4].Value == "true", file = m.Groups[5].Value };
            return result;
        }
    }
}
