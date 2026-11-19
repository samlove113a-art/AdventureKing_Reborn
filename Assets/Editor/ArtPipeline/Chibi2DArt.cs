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
    /// 2D chibi bodies baked from the rigged 3D models (Tools/blender/bake_chibi2d.py â†’ ArtSource/Chibi2D/{Name}/*.png).
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

        /// <summary>Only the Swordsman (hand-processed pixel sprites): Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.Chibi2DArt.BuildSwordsmanFromCommandLine</summary>
        public static void BuildSwordsmanFromCommandLine()
        {
            try { Build(HeroClass.Swordsman); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Mage, Assassin, Archer once their pixel strips are installed (same pipeline as the Swordsman).
        ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.Chibi2DArt.BuildPixelClassesFromCommandLine</summary>
        public static void BuildPixelClassesFromCommandLine()
        {
            try { foreach (HeroClass c in new[] { HeroClass.Mage, HeroClass.Assassin, HeroClass.Archer }) Build(c); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>All four classes (PhÃ¡p SÆ°, Kiáº¿m SÄ©, Cung Thá»§, SÃ¡t Thá»§) from ArtSource/Chibi2D/{Class}.
        ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.Chibi2DArt.BuildAllFromCommandLine</summary>
        public static void BuildAllFromCommandLine()
        {
            try { foreach (HeroClass c in new[] { HeroClass.Mage, HeroClass.Swordsman, HeroClass.Archer, HeroClass.Assassin }) Build(c); BuildAllActors(); BuildPets(); BuildFx2(); EditorApplication.Exit(0); }
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

        // actor (NPC / monster) -> display scale of its sprite (heroes use 1.0): the body is shown at the same px/metre as the heroes
        private static readonly (string name, float scale)[] Actors =
        {
            ("NpcElder", 1f), ("NpcSmith", 1f), ("NpcStorekeeper", 1f), ("NpcCaptain", 1f), ("NpcOfficiant", 1f), ("NpcMusician", 1f), ("NpcMushroomElder", 1f),
            ("MonsterNamNon", 1f), ("PollenSlime", 1f), ("PuffSlime", 1f), ("MossSlime", 0.5f),
            // ChÆ°Æ¡ng 1 Map 3-10 (Tools/chibi2d/make_monsters.py): the frame size already sets the real size, so scale 1
            ("GlowMoss", 1f), ("MushroomChief", 1f), ("FlowerBee", 1f), ("CrystalBat", 1f), ("JadeTurtle", 1f), ("JadeGuardian", 1f), ("ShadowLeaf", 1f),
            ("ToxicSlime", 1f), ("ToxicElite", 1f), ("StoneGuard", 1f), ("DarkMushroom", 1f), ("RoyalGuard", 1f), ("MushroomKing", 1f),
        };

        private static HashSet<string> ActorFilter;

        /// <summary>Only the actors named in env AK_ACTORS (comma list, e.g. NpcElder,NpcSmith): the pixel NPCs are replaced one by one.
        ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.Chibi2DArt.BuildSelectedActorsFromCommandLine</summary>
        public static void BuildSelectedActorsFromCommandLine()
        {
            try
            {
                string list = Environment.GetEnvironmentVariable("AK_ACTORS") ?? "";
                ActorFilter = new HashSet<string>(list.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                if (ActorFilter.Count == 0) throw new InvalidOperationException("AK_ACTORS is empty");
                BuildAllActors();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>NPCs and monsters: strips from bake_chibi_actor.py -> clips, a plain state controller (states = clip names: NpcActor and
        /// FarmMonsterView CrossFade by state name) and Resources/Chibi2D/Actors/{name}_Visual2D.prefab.</summary>
        public static void BuildAllActors()
        {
            foreach (var (name, scale) in Actors)
            {
                if (ActorFilter != null && !ActorFilter.Contains(name)) continue;
                string json = $"{Src}/{name}/{name}_frames.json";
                if (!File.Exists(json)) { Debug.LogWarning("[CHIBI2D] no art for " + name); continue; }
                string text = File.ReadAllText(json);
                float cell = ReadFloat(text, "cell"), frameM = ReadFloat(text, "frame_m"), ground = ReadFloat(text, "ground_from_bottom_m");
                float ppu = cell / frameM; Vector2 pivot = new Vector2(0.5f, ground / frameM);
                Directory.CreateDirectory($"{Dst}/{name}"); Directory.CreateDirectory($"{Res}/Actors");
                var ctrlPath = $"{Dst}/{name}/{name}.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = ctrl.layers[0].stateMachine;
                bool first = true; Sprite firstSprite = null;
                foreach (var kv in ParseClips(text))
                {
                    string dst = $"{Dst}/{name}/{kv.Value.file}";
                    File.Copy($"{Src}/{name}/{kv.Value.file}", dst, true);
                    AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = ppu;
                    ti.mipmapEnabled = false; ti.filterMode = FilterMode.Point; ti.alphaIsTransparency = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
                    var sheet = new SpriteMetaData[kv.Value.frames];
                    for (int i = 0; i < sheet.Length; i++)
                        sheet[i] = new SpriteMetaData { name = $"{name}_{kv.Key}_{i}", rect = new Rect(i * cell, 0, cell, cell), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable CS0618
                    ti.spritesheet = sheet;
#pragma warning restore CS0618
                    ti.SaveAndReimport();
                    Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
                    AnimationClip clip = MakeClip($"{Dst}/{name}/{name}_{kv.Key}.anim", name + "_" + kv.Key, frames, kv.Value, false);
                    AnimatorState st = sm.AddState(kv.Key);
                    st.motion = clip;
                    if (first || kv.Key == "Idle") { sm.defaultState = st; }
                    first = false;
                    if (kv.Key == "Idle" || firstSprite == null) firstSprite = frames[0];
                }
                var root = new GameObject(name + "_Visual2D");
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(root.transform, false);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = firstSprite; sr.sharedMaterial = PixelMaterial(); sr.sortingOrder = 4;
                var an = root.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl; an.applyRootMotion = false;
                var mk = root.AddComponent<Chibi2DMarker>(); mk.Renderer = sr; mk.Scale = scale;
                PrefabUtility.SaveAsPrefabAsset(root, $"{Res}/Actors/{name}_Visual2D.prefab");
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log($"[CHIBI2D] actor {name} built (scale {scale})");
            }
            AssetDatabase.SaveAssets();
        }

        private static readonly (string id, string file)[] Pets =
        {
            ("pet_cloud_cat", "PetCloudCat"), ("pet_chick", "PetChick"), ("pet_jade_dragon", "PetJadeDragon"), ("pet_fire_fox", "PetFireFox"),
        };

        /// <summary>The four pets: ArtSource/Chibi2D/Pets/{Pet}.png (+ _hungry) from Tools/chibi2d/make_pets.py -> Resources/Chibi2D/Pets/{id}_Visual2D.prefab.</summary>
        public static void BuildPets()
        {
            Directory.CreateDirectory($"{Dst}/Pets"); Directory.CreateDirectory($"{Res}/Pets");
            foreach (var (id, file) in Pets)
            {
                Sprite[] two = new Sprite[2];
                string[] names = { file, file + "_hungry" };
                for (int i = 0; i < 2; i++)
                {
                    string dst = $"{Dst}/Pets/{names[i]}.png";
                    File.Copy($"{Src}/Pets/{names[i]}.png", dst, true);
                    AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 128f;
                    ti.spritePivot = new Vector2(0.5f, 0.06f); ti.mipmapEnabled = false; ti.filterMode = FilterMode.Point; ti.alphaIsTransparency = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts); ts.spriteAlignment = (int)SpriteAlignment.Custom; ti.SetTextureSettings(ts);
                    ti.SaveAndReimport();
                    two[i] = AssetDatabase.LoadAssetAtPath<Sprite>(dst);
                }
                var root = new GameObject(id + "_Visual2D");
                var sg = new GameObject("Sprite"); sg.transform.SetParent(root.transform, false);
                var sr = sg.AddComponent<SpriteRenderer>(); sr.sprite = two[0]; sr.sharedMaterial = PixelMaterial(); sr.sortingOrder = 4;
                var body = root.AddComponent<Pet2DBody>(); body.Renderer = sr; body.Normal = two[0]; body.Hungry = two[1];
                PrefabUtility.SaveAsPrefabAsset(root, $"{Res}/Pets/{id}_Visual2D.prefab");
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log("[CHIBI2D] pet " + id);
            }
            AssetDatabase.SaveAssets();
        }

        public static void BuildPetsFromCommandLine()
        {
            try { BuildPets(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>The unlit sprite material for skill effects: like the pixel material but allowed to reach full brightness (a glow, not a painted colour).</summary>
        public static Material FxMaterial()
        {
            const string path = "Assets/Art/Chibi2D/AK_PixelFx.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(Shader.Find("AdventureKing/PixelSprite")) { name = "AK_PixelFx" };
            m.SetFloat("_MaxLuma", 1f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Hero skill effects drawn by Tools/chibi2d/make_fx.py: ArtSource/Chibi2D/Fx/{name}.png strips -> Resources/Chibi2D/Fx2/{name}.prefab (flip-book).</summary>
        public static void BuildFx2()
        {
            string meta = $"{Src}/Fx/fx_frames.json";
            if (!File.Exists(meta)) { Debug.LogWarning("[CHIBI2D] no Fx art: run Tools/chibi2d/make_fx.py"); return; }
            string json = File.ReadAllText(meta);
            Directory.CreateDirectory($"{Dst}/Fx2"); Directory.CreateDirectory($"{Res}/Fx2");
            var rx = new System.Text.RegularExpressions.Regex("\"(\\w+)\":\\s*\\{\\s*\"frames\":\\s*(\\d+),\\s*\"fps\":\\s*(\\d+),\\s*\"loop\":\\s*(true|false)");
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(json))
            {
                string fx = m.Groups[1].Value; int frames = int.Parse(m.Groups[2].Value);
                string dst = $"{Dst}/Fx2/{fx}.png";
                File.Copy($"{Src}/Fx/{fx}.png", dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = 64f;
                ti.mipmapEnabled = false; ti.filterMode = FilterMode.Point; ti.alphaIsTransparency = true; ti.textureCompression = TextureImporterCompression.Uncompressed;
                var sheet = new SpriteMetaData[frames];
                for (int i = 0; i < frames; i++)
                    sheet[i] = new SpriteMetaData { name = $"{fx}_{i}", rect = new Rect(i * 128, 0, 128, 128), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
#pragma warning disable CS0618
                ti.spritesheet = sheet;
#pragma warning restore CS0618
                ti.SaveAndReimport();
                Sprite[] sp = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
                var go = new GameObject(fx);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp[0]; sr.sharedMaterial = FxMaterial(); sr.sortingOrder = 8;
                var fb = go.AddComponent<SpriteFlipbook>();
                fb.Frames = sp; fb.Fps = float.Parse(m.Groups[3].Value); fb.Loop = m.Groups[4].Value == "true"; fb.DestroyOnEnd = !fb.Loop;
                PrefabUtility.SaveAsPrefabAsset(go, $"{Res}/Fx2/{fx}.prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CHIBI2D] skill effects 2 built");
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

        private static AnimationClip MakeClip(string path, string clipName, Sprite[] frames, ClipMeta m, bool events = true)
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
            if (events && Hooks.TryGetValue(key, out var h))
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
