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
            bool pixel = System.Text.RegularExpressions.Regex.IsMatch(json, "\"pixel\":\\s*true");
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
                ti.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;   // pixel art stays crisp, never smoothed
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
            sr.sharedMaterial = PixelMaterial();      // unlit, no emission, capped under the bloom threshold: no glow haze, colours as painted
            sr.sortingOrder = 5;
            var anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = ov;
            anim.applyRootMotion = false;
            var head = new GameObject("Head").transform;   // the name plate / bubbles find the head by this name
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 0.72f, 0f);
            root.AddComponent<CharacterAnimationEvents>();
            root.AddComponent<Chibi2DMarker>().Renderer = sr;
            root.AddComponent<Paperdoll2D>();          // modular equipment layers (outfit / hat / weapon), one renderer per slot
            string prefabPath = $"{Res}/{name}_Visual2D.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CHIBI2D] {name}: {clips.Count} clips, ppu {ppu:0.0}, pixel {pixel}, prefab {prefabPath}");
            BuildFx(name, ppu);
            BuildOutfits(name, clips, cell, ppu, pivot, pixel);
        }

        /// <summary>The unlit pixel-art sprite material (AdventureKing/PixelSprite), shared by every 2D body and effect.</summary>
        public static Material PixelMaterial()
        {
            const string path = "Assets/Art/Chibi2D/AK_PixelSprite.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(Shader.Find("AdventureKing/PixelSprite")) { name = "AK_PixelSprite" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Paper-doll items: every folder ArtSource/Chibi2D/{Hero}/Outfits/{set} becomes Resources/Chibi2D/Paperdoll/{set}/{Hero}_{Clip}.png,
        /// sliced exactly like the body (same frame size, pivot, point filter) so layers line up pixel for pixel.</summary>
        private static void BuildOutfits(string name, Dictionary<string, ClipMeta> clips, float cell, float ppu, Vector2 pivot, bool pixel)
        {
            string root = $"{Src}/{name}/Outfits";
            if (!Directory.Exists(root)) return;
            foreach (string dir in Directory.GetDirectories(root))
            {
                string set = Path.GetFileName(dir);
                string outDir = $"{Res}/Paperdoll/{set}";
                Directory.CreateDirectory(outDir);
                foreach (var kv in clips)
                {
                    string src = $"{dir}/{kv.Value.file}";
                    if (!File.Exists(src)) continue;
                    string dst = $"{outDir}/{kv.Value.file}";
                    File.Copy(src, dst, true);
                    AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = ppu;
                    ti.mipmapEnabled = false; ti.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear; ti.alphaIsTransparency = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
                    var sheet = new SpriteMetaData[kv.Value.frames];
                    for (int i = 0; i < sheet.Length; i++)
                        sheet[i] = new SpriteMetaData { name = $"{name}_{kv.Key}_{i}", rect = new Rect(i * cell, 0, cell, cell), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable CS0618
                    ti.spritesheet = sheet;
#pragma warning restore CS0618
                    ti.SaveAndReimport();
                }
                Debug.Log($"[CHIBI2D] paper-doll outfit {set} built");
            }
        }

        /// <summary>Skill effects as separate assets: Resources/Chibi2D/Fx/{effect}.prefab (SpriteRenderer + SpriteFlipbook), same pixel scale as the body.</summary>
        private static void BuildFx(string name, float ppu)
        {
            string meta = $"{Src}/{name}/Fx/fx_frames.json";
            if (!File.Exists(meta)) return;
            string json = File.ReadAllText(meta);
            Directory.CreateDirectory($"{Dst}/{name}/Fx");
            Directory.CreateDirectory($"{Res}/Fx");
            var rx = new System.Text.RegularExpressions.Regex("\"(\\w+)\":\\s*\\{\\s*\"frames\":\\s*(\\d+),\\s*\"fps\":\\s*(\\d+),\\s*\"loop\":\\s*(true|false),\\s*\"size\":\\s*(\\d+),\\s*\"file\":\\s*\"([^\"]+)\"");
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(json))
            {
                string fx = m.Groups[1].Value; int frames = int.Parse(m.Groups[2].Value), size = int.Parse(m.Groups[5].Value);
                string dst = $"{Dst}/{name}/Fx/{m.Groups[6].Value}";
                File.Copy($"{Src}/{name}/Fx/{m.Groups[6].Value}", dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = ppu;
                ti.mipmapEnabled = false; ti.filterMode = FilterMode.Point; ti.alphaIsTransparency = true; ti.textureCompression = TextureImporterCompression.Uncompressed;
                var sheet = new SpriteMetaData[frames];
                for (int i = 0; i < frames; i++)
                    sheet[i] = new SpriteMetaData { name = $"{fx}_{i}", rect = new Rect(i * size, 0, size, size), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
#pragma warning disable CS0618
                ti.spritesheet = sheet;
#pragma warning restore CS0618
                ti.SaveAndReimport();
                Sprite[] sp = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
                var go = new GameObject(fx);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = PixelMaterial();
                sr.sortingOrder = 6;
                var fb = go.AddComponent<SpriteFlipbook>();
                fb.Frames = sp; fb.Fps = float.Parse(m.Groups[3].Value); fb.Loop = m.Groups[4].Value == "true"; fb.DestroyOnEnd = !fb.Loop;
                PrefabUtility.SaveAsPrefabAsset(go, $"{Res}/Fx/{fx}.prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("[CHIBI2D] skill effects built");
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
