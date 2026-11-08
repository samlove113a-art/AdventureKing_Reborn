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
    /// Builds the four playable hero visuals from the exported FBX files:
    /// import settings + trimmed clips with contact events, toon materials, Animator controllers and
    /// visual-only prefabs (model + swappable weapons on bone sockets). Re-runnable and idempotent.
    /// Menu: AdventureKing/Art/Rebuild Hero Visuals.
    /// </summary>
    public static class HeroArtPipeline
    {
        public const string PrefabFolder = "Assets/Prefabs/Heroes";
        public const string ToonShader = "AdventureKing/Toon";

        [MenuItem("AdventureKing/Art/Rebuild Hero Visuals")]
        public static void RebuildAll()
        {
            foreach (HeroArtDefinitions.Hero h in HeroArtDefinitions.Heroes) Build(h);
            AssetDatabase.SaveAssets();
            Debug.Log("[ART] Hero visuals rebuilt.");
        }

        public static void RebuildFromCommandLine()
        {
            try
            {
                RebuildAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static string PrefabPath(HeroClass c) => $"{PrefabFolder}/{c}_Visual.prefab";

        private static void Build(HeroArtDefinitions.Hero h)
        {
            string fbx = $"{h.Folder}/{h.Fbx}";
            string texDir = $"{h.Folder}/Textures";
            ExtractTextures(fbx, texDir);
            ConfigureModel(fbx, h.Clips);
            Material body = EnsureToonMaterial($"{h.Folder}/{h.Class}_Toon.mat",
                FindTexture(texDir, $"{h.Class}_Body"), 0.0065f);

            // Weapon meshes ship inside the character FBX, skinned to their hand bone
            // (renderer "Weapon_<Name>"); each gets its own toon material.
            var weaponMats = new Dictionary<string, Material>();
            foreach (var (_, weaponName) in h.Weapons)
            {
                string baseName = weaponName.EndsWith("_L") ? weaponName.Substring(0, weaponName.Length - 2) : weaponName;
                if (!weaponMats.ContainsKey(baseName))
                    weaponMats[baseName] = EnsureToonMaterial($"{h.Folder}/{baseName}_Toon.mat",
                        FindTexture(texDir, baseName), 0.004f);
                weaponMats[weaponName] = weaponMats[baseName];
            }

            AnimatorController controller = BuildController(h, fbx);
            BuildPrefab(h, fbx, body, weaponMats, controller);
        }

        // ------------------------------------------------------------------ textures / materials
        private static Texture2D FindTexture(string folder, string baseName)
        {
            string path = $"{folder}/{baseName}_baseColor.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!tex) throw new FileNotFoundException("Texture missing: " + path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.maxTextureSize != 2048 || ti.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 2;
                ti.SaveAndReimport();
            }
            return tex;
        }

        private static void ExtractTextures(string fbx, string folder)
        {
            // Re-extract whenever the FBX is newer than the extracted textures.
            bool stale = !Directory.Exists(folder) ||
                         !Directory.EnumerateFiles(folder, "*.png").Any() ||
                         Directory.EnumerateFiles(folder, "*.png").Max(File.GetLastWriteTimeUtc) < File.GetLastWriteTimeUtc(fbx);
            if (stale)
            {
                if (Directory.Exists(folder)) AssetDatabase.DeleteAsset(folder);
                Directory.CreateDirectory(folder);
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
                if (!mi.ExtractTextures(folder))
                    Debug.LogWarning("[ART] No embedded textures extracted from " + fbx);
                AssetDatabase.Refresh();
            }
            // Blender names glTF-embedded images without an extension; give them one so Unity
            // imports them as textures.
            foreach (string f in Directory.GetFiles(folder))
            {
                if (f.EndsWith(".meta") || Path.HasExtension(f)) continue;
                string dst = f + ".png";
                if (File.Exists(f + ".meta")) File.Delete(f + ".meta");
                File.Move(f, dst);
                AssetDatabase.ImportAsset(dst.Replace('\\', '/'), ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static Material EnsureToonMaterial(string path, Texture2D tex, float outline)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find(ToonShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(ToonShader);
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_OutlineWidth", outline);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ model import
        private static void ConfigureStaticModel(string fbx)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.SaveAndReimport();
        }

        private static void ConfigureModel(string fbx, HeroArtDefinitions.Clip[] clips)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            mi.SaveAndReimport(); // ensure takes are known

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Transform hips = FindDeep(model.transform, "Hips");
            if (!hips) throw new System.InvalidOperationException("Hips not found in " + fbx);
            mi.motionNodeName = AnimationUtility.CalculateTransformPath(hips, model.transform);

            TakeInfo[] takes = mi.importedTakeInfos;
            var list = new List<ModelImporterClipAnimation>();
            foreach (HeroArtDefinitions.Clip c in clips)
            {
                TakeInfo take = takes.FirstOrDefault(t => t.name == c.Take || t.name.EndsWith("|" + c.Take));
                if (string.IsNullOrEmpty(take.name))
                    throw new System.InvalidOperationException($"Take '{c.Take}' missing in {fbx}: " +
                        string.Join(", ", takes.Select(t => t.name)));
                float len = Mathf.Max(1f, c.Last - c.First);
                list.Add(new ModelImporterClipAnimation
                {
                    name = c.Name,
                    takeName = take.name,
                    firstFrame = c.First,
                    lastFrame = c.Last,
                    loopTime = c.Loop,
                    loopPose = false,
                    lockRootRotation = true,
                    keepOriginalOrientation = true,
                    lockRootPositionXZ = true,
                    keepOriginalPositionXZ = true,
                    lockRootHeightY = c.BakeY,
                    keepOriginalPositionY = true,
                    events = c.Events.Select(e => new AnimationEvent
                    {
                        functionName = e.fn,
                        time = Mathf.Clamp01((e.frame - c.First) / len)
                    }).ToArray()
                });
            }
            mi.clipAnimations = list.ToArray();
            mi.SaveAndReimport();
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ animator
        private static AnimatorController BuildController(HeroArtDefinitions.Hero h, string fbx)
        {
            string path = $"{h.Folder}/{h.Class}.controller";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Skill", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
            // Playback multipliers driven by real body speed (CharacterVisual3D + HeroMotionProfile).
            ctrl.AddParameter(new AnimatorControllerParameter { name = "RunPlayback", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "ClimbPlayback", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            int i = 0;
            foreach (HeroArtDefinitions.Clip c in h.Clips)
            {
                AnimatorState s = sm.AddState(c.Name, new Vector3(260 + (i % 3) * 230, 60 + (i / 3) * 80));
                s.motion = clips[c.Name];
                s.speed = c.Speed;
                s.writeDefaultValues = true;
                states[c.Name] = s;
                i++;
            }
            sm.defaultState = states["Idle"];
            AnimatorState idle = states["Idle"], run = states["Run"], jump = states["Jump"], fall = states["Fall"];
            run.speedParameterActive = true;
            run.speedParameter = "RunPlayback";
            states["Climb"].speedParameterActive = true;
            states["Climb"].speedParameter = "ClimbPlayback";

            Link(idle, run, 0.12f, ("Speed", AnimatorConditionMode.Greater, 0.25f), ("Grounded", AnimatorConditionMode.If, 0));
            Link(run, idle, 0.12f, ("Speed", AnimatorConditionMode.Less, 0.25f));
            foreach (AnimatorState from in new[] { idle, run })
            {
                Link(from, jump, 0.06f, ("Grounded", AnimatorConditionMode.IfNot, 0), ("VerticalSpeed", AnimatorConditionMode.Greater, 0.5f));
                Link(from, fall, 0.12f, ("Grounded", AnimatorConditionMode.IfNot, 0), ("VerticalSpeed", AnimatorConditionMode.Less, -1.5f));
            }
            Link(jump, fall, 0.15f, ("VerticalSpeed", AnimatorConditionMode.Less, 0.2f));
            Link(jump, idle, 0.08f, ("Grounded", AnimatorConditionMode.If, 0), ("VerticalSpeed", AnimatorConditionMode.Less, 0.1f));
            Link(fall, idle, 0.08f, ("Grounded", AnimatorConditionMode.If, 0));

            AnyLink(sm, states["Death"], 0.1f, false, ("Dead", AnimatorConditionMode.If, 0));
            AnyLink(sm, states["Climb"], 0.12f, false, ("Climbing", AnimatorConditionMode.If, 0), ("Dead", AnimatorConditionMode.IfNot, 0));
            Link(states["Climb"], idle, 0.12f, ("Climbing", AnimatorConditionMode.IfNot, 0));
            AnyLink(sm, states["Attack"], 0.06f, true, ("Attack", AnimatorConditionMode.If, 0), ("Dead", AnimatorConditionMode.IfNot, 0));
            AnyLink(sm, states["Skill"], 0.08f, false, ("Skill", AnimatorConditionMode.If, 0), ("Dead", AnimatorConditionMode.IfNot, 0));
            AnyLink(sm, states["Hit"], 0.05f, false, ("Hit", AnimatorConditionMode.If, 0), ("Dead", AnimatorConditionMode.IfNot, 0));
            foreach (string n in new[] { "Attack", "Skill", "Hit" }) ExitTo(states[n], idle, 0.92f, 0.12f);
            if (states.TryGetValue("Dodge", out AnimatorState dodge))
            {
                AnyLink(sm, dodge, 0.05f, false, ("Dodge", AnimatorConditionMode.If, 0), ("Dead", AnimatorConditionMode.IfNot, 0));
                ExitTo(dodge, idle, 0.9f, 0.1f);
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static void Link(AnimatorState a, AnimatorState b, float blend, params (string p, AnimatorConditionMode m, float v)[] cond)
        {
            AnimatorStateTransition t = a.AddTransition(b);
            t.hasExitTime = false;
            t.duration = blend;
            t.hasFixedDuration = true;
            foreach (var c in cond) t.AddCondition(c.m, c.v, c.p);
        }

        private static void AnyLink(AnimatorStateMachine sm, AnimatorState b, float blend, bool toSelf,
            params (string p, AnimatorConditionMode m, float v)[] cond)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(b);
            t.hasExitTime = false;
            t.duration = blend;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = toSelf;
            foreach (var c in cond) t.AddCondition(c.m, c.v, c.p);
        }

        private static void ExitTo(AnimatorState a, AnimatorState b, float exitTime, float blend)
        {
            AnimatorStateTransition t = a.AddTransition(b);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = blend;
            t.hasFixedDuration = true;
        }

        // ------------------------------------------------------------------ prefab
        /// <summary>
        /// Average backward speed of whichever foot is planted (lowest 20% of its height range) over the
        /// in-place Run clip, at model scale 1 and 1x playback.
        /// </summary>
        internal static float MeasureStrideSpeed(GameObject model, AnimationClip run)
        {
            Animator anim = model.GetComponent<Animator>();
            RuntimeAnimatorController keep = anim ? anim.runtimeAnimatorController : null;
            if (anim) anim.runtimeAnimatorController = null;
            Transform[] all = model.GetComponentsInChildren<Transform>();
            Vector3 fwd = model.transform.forward; fwd.y = 0f; fwd.Normalize();
            float scale = Mathf.Max(1e-4f, model.transform.lossyScale.y);
            const int N = 120;
            var speeds = new List<float>();
            foreach (string footName in new[] { "RightFoot", "LeftFoot" })
            {
                Transform foot = all.FirstOrDefault(t => t.name.EndsWith(footName));
                if (!foot) continue;
                var pos = new Vector3[N];
                for (int i = 0; i < N; i++) { run.SampleAnimation(model, run.length * i / N); pos[i] = foot.position; }
                float minY = pos.Min(p => p.y), maxY = pos.Max(p => p.y);
                float dt = run.length / N;
                for (int i = 0; i < N; i++)
                {
                    if (pos[i].y > minY + (maxY - minY) * 0.2f) continue;
                    speeds.Add(-Vector3.Dot(pos[(i + 1) % N] - pos[i], fwd) / dt / scale);
                }
            }
            if (anim) anim.runtimeAnimatorController = keep;
            return speeds.Count > 0 ? Mathf.Max(0.2f, speeds.Average()) : 1.4f;
        }

        private static void BuildPrefab(HeroArtDefinitions.Hero h, string fbx, Material body,
            Dictionary<string, Material> weaponMats, AnimatorController controller)
        {
            Directory.CreateDirectory(PrefabFolder);
            var root = new GameObject($"{h.Class}_Visual");
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";

            // Orientation from the skin BIND POSE (independent of whichever take Unity shows by default):
            // the hip joints give the character's right axis; forward = right x up.
            model.transform.localRotation = Quaternion.identity;
            SkinnedMeshRenderer bodySmr = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .First(s => !s.name.StartsWith("Weapon_"));
            Vector3 BindPos(string bone)
            {
                int i = System.Array.FindIndex(bodySmr.bones, b => b && b.name == bone);
                if (i < 0) throw new System.InvalidOperationException(bone + " not skinned");
                Matrix4x4 boneToMesh = bodySmr.sharedMesh.bindposes[i].inverse;
                return bodySmr.transform.TransformPoint(boneToMesh.GetColumn(3));
            }
            Vector3 right = BindPos("RightUpLeg") - BindPos("LeftUpLeg");
            right.y = 0f;
            Vector3 fwd = Vector3.Cross(right.normalized, Vector3.up);
            model.transform.localRotation = Quaternion.FromToRotation(fwd, Vector3.forward);
            Debug.Log($"[ART] {h.Class} bind-pose forward {fwd} -> yaw fix {model.transform.localEulerAngles.y:0.0}");

            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material m = body;
                if (smr.name.StartsWith("Weapon_"))
                {
                    string key = smr.name.Substring("Weapon_".Length);
                    if (!weaponMats.TryGetValue(key, out m))
                        throw new System.InvalidOperationException($"No material for weapon renderer {smr.name}");
                }
                smr.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, smr.sharedMesh.subMeshCount)).ToArray();
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone4;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            Animator anim = model.GetComponent<Animator>();
            if (!anim) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var events = model.AddComponent<CharacterAnimationEvents>();

            var pres = root.AddComponent<HeroCombatPresentation>();
            var so = new SerializedObject(pres);
            so.FindProperty("heroClass").enumValueIndex = (int)h.Class;
            so.FindProperty("events").objectReferenceValue = events;
            so.FindProperty("facing").objectReferenceValue = root.transform;
            Transform mainSocket = FindDeep(model.transform, h.Weapons[0].hand);
            so.FindProperty("weaponSocket").objectReferenceValue = mainSocket;
            so.ApplyModifiedPropertiesWithoutUndo();
            HeroVfxFactory.AssignClassEffects(pres, h.Class);

            // Measure locomotion from this hero's own clips so playback can match real movement.
            var motion = root.AddComponent<HeroMotionProfile>();
            AnimationClip runClip = controller.animationClips.First(c => c.name == "Run");
            AnimationClip climbClip = controller.animationClips.First(c => c.name == "Climb");
            motion.runStrideSpeed = MeasureStrideSpeed(model, runClip);
            HeroArtDefinitions.Clip climbDef = h.Clips.First(c => c.Name == "Climb");
            motion.climbCycleSeconds = climbClip.length / Mathf.Max(0.05f, climbDef.Speed);
            anim.runtimeAnimatorController = null;
            controller.animationClips.First(c => c.name == "Idle").SampleAnimation(model, 0f);
            anim.runtimeAnimatorController = controller;
            Debug.Log($"[ART] {h.Class} run stride {motion.runStrideSpeed:0.00} m/s @1x, climb cycle {motion.climbCycleSeconds:0.00}s");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(h.Class));
            Object.DestroyImmediate(root);
        }
    }
}
