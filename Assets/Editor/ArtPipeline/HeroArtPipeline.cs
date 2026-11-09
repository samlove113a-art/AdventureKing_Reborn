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
            // Face layer: measured from this hero's own painted face (UV3 is added at import by HeroFaceImport).
            HeroFaceRig.FaceData face = HeroFaceRig.Analyse(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),
                $"{texDir}/{h.Class}_Body_baseColor.png", out string faceError);
            if (face == null) throw new System.InvalidOperationException($"{h.Class} face analysis failed: {faceError}");
            HeroFaceRig.Apply(body, face);
            Debug.Log($"[FACE] {h.Class}: eyes {face.EyeA} {face.EyeB}, mouth {face.Mouth}");

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
            mi.generateSecondaryUV = false; // UV channels are ours (UV3 = face layer)
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
                    loopPose = c.Loop, // closes any residual seam smoothly instead of snapping every cycle
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
            Link(states["Death"], idle, 0.15f, ("Dead", AnimatorConditionMode.IfNot, 0)); // revive stands up by itself
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

        /// <summary>
        /// HeroFingers on the prefab: per hand the 3 finger-block joints (HeroHandRig), the curl axis (towards the
        /// palm: the weapon grip for weapon hands, the body for empty hands) and, for weapon hands, the curl that
        /// closes the fingers onto the grip (scanned on the posed model). Measured in the Idle pose.
        /// </summary>
        private static void SetupFingers(HeroArtDefinitions.Hero h, GameObject root, GameObject model, Animator anim)
        {
            Transform hips = FindDeep(model.transform, "Hips");
            HeroFingers.Hand Hand(string side)
            {
                Transform hand = FindDeep(model.transform, side + "Hand");
                var joints = new[] { 1, 2, 3 }.Select(j => FindDeep(model.transform, $"{side}Hand_Fingers{j}")).ToArray();
                if (!hand || joints.Any(j => !j)) { Debug.LogWarning($"[HAND] {h.Class} {side}: no finger joints"); return null; }
                var data = new HeroFingers.Hand { joints = joints, restLocal = joints.Select(j => j.localRotation).ToArray() };
                Vector3 fingerDir = (joints[2].position - joints[0].position).normalized;
                // palm side: towards this hand's weapon grip, else towards the body
                var weaponName = h.Weapons.FirstOrDefault(w => w.hand == side + "Hand").weapon;
                SkinnedMeshRenderer weapon = weaponName == null ? null :
                    model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == "Weapon_" + weaponName);
                Vector3[] weaponPts = null;
                if (weapon)
                {
                    var baked = new Mesh();
                    weapon.BakeMesh(baked, true);
                    Matrix4x4 m = weapon.transform.localToWorldMatrix;
                    weaponPts = baked.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray();
                    Object.DestroyImmediate(baked);
                }
                float handSize = Vector3.Distance(hand.position, joints[2].position) * 1.6f;
                Vector3 towards = weaponPts != null
                    ? weaponPts.Where(p => Vector3.Distance(p, hand.position) < handSize).DefaultIfEmpty(hips.position).Aggregate(Vector3.zero, (a, p) => a + p)
                      / Mathf.Max(1, weaponPts.Count(p => Vector3.Distance(p, hand.position) < handSize))
                    : hips.position;
                Vector3 palm = Vector3.ProjectOnPlane(towards - hand.position, fingerDir).normalized;
                Vector3 axis = Vector3.Cross(fingerDir, palm).normalized;
                if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * fingerDir, palm) < Vector3.Dot(fingerDir, palm)) axis = -axis;
                data.curlAxisLocal = joints.Select(j => j.InverseTransformDirection(axis)).ToArray();
                if (weaponPts != null)
                {
                    // smallest curl that brings the fingertip onto the grip (stop before passing through it)
                    float bestC = 0.7f, bestD = float.MaxValue;
                    for (float c = 0.2f; c <= 0.95f; c += 0.05f)
                    {
                        HeroFingers.Pose(data, c);
                        Vector3 tip = joints[2].position + (joints[2].position - joints[1].position);
                        float d = weaponPts.Min(p => (p - tip).sqrMagnitude);
                        if (d < bestD - 1e-8f) { bestD = d; bestC = c; }
                    }
                    data.weaponGrip = bestC;
                    HeroFingers.Pose(data, 0f);
                    Debug.Log($"[HAND] {h.Class} {side}: weapon grip curl {bestC:0.00}, fingertip-to-grip {Mathf.Sqrt(bestD) * 100f:0.0} cm");
                }
                return data;
            }
            var fingers = root.AddComponent<HeroFingers>();
            fingers.Configure(anim, Hand("Left"), Hand("Right"));
        }

        /// <summary>
        /// Copy of the imported Climb clip with the hips' steady vertical drift removed (linear detrend), so the last
        /// frame meets the first. Returns the removed rise per cycle in metres at model scale 1.
        /// </summary>
        private static AnimationClip MakeInPlaceClimb(HeroArtDefinitions.Hero h, GameObject model, AnimationClip src, out float rise)
        {
            Transform hips = FindDeep(model.transform, "Hips");
            string hipsPath = AnimationUtility.CalculateTransformPath(hips, model.transform);
            Animator anim = model.GetComponent<Animator>();
            RuntimeAnimatorController keep = anim ? anim.runtimeAnimatorController : null;
            if (anim) anim.runtimeAnimatorController = null;
            src.SampleAnimation(model, 0f);
            float y0 = hips.position.y;
            src.SampleAnimation(model, src.length);
            float y1 = hips.position.y;
            rise = Mathf.Abs(y1 - y0) / Mathf.Max(1e-4f, model.transform.lossyScale.y);
            if (anim) anim.runtimeAnimatorController = keep;

            var clip = new AnimationClip { name = "ClimbBase", frameRate = src.frameRate };
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(src))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(src, b);
                // All position channels of the motion node: with Hips as root-motion node Unity stores its motion as
                // RootT/MotionT curves on the root; the hips' parent may also be axis-converted ("up" != local y).
                bool hipsPos = b.path == hipsPath && b.propertyName.StartsWith("m_LocalPosition.");
                bool rootPos = b.path == "" && (b.propertyName.StartsWith("RootT.") || b.propertyName.StartsWith("MotionT."));
                if ((hipsPos || rootPos) && curve.length > 1)
                {
                    Debug.Log($"[ART] {h.Class} climb detrend '{b.path}' {b.propertyName}: drift {curve.keys[curve.length - 1].value - curve.keys[0].value:0.0000}");
                    float t0 = curve.keys[0].time, t1 = curve.keys[curve.length - 1].time;
                    float drift = curve.keys[curve.length - 1].value - curve.keys[0].value;
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        float k = (keys[i].time - t0) / Mathf.Max(1e-4f, t1 - t0);
                        keys[i].value -= drift * k;
                        keys[i].inTangent -= drift / Mathf.Max(1e-4f, t1 - t0);
                        keys[i].outTangent -= drift / Mathf.Max(1e-4f, t1 - t0);
                    }
                    curve.keys = keys;
                }
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(src));
            var settings = AnimationUtility.GetAnimationClipSettings(src);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // Torso/head source for the rung-accurate climb (ClimbClipBaker writes InPlace/Climb.anim).
            Directory.CreateDirectory($"{h.Folder}/InPlace");
            AssetDatabase.DeleteAsset($"{h.Folder}/{h.Class}_Climb_InPlace.anim"); // earlier name
            string path = $"{h.Folder}/InPlace/ClimbBase.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            Debug.Log($"[ART] {h.Class} climb: removed hip rise {rise:0.000} m/cycle (model scale) -> in-place loop {path}");
            return clip;
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

            var face = root.AddComponent<HeroFace>();
            face.Bind(bodySmr);
            {
                // Face axes in the head bone's local space, from the bind pose (mesh space -> bone space).
                var (_, faceMesh, _, _, faceUp, faceFwd, _) = HeroFaceRig.Frame(model);
                int headIdx = System.Array.FindIndex(bodySmr.bones, b => b && b.name == "Head");
                Matrix4x4 meshToHead = faceMesh.bindposes[headIdx];
                face.BindHead(bodySmr.bones[headIdx], meshToHead.MultiplyVector(faceFwd).normalized, meshToHead.MultiplyVector(faceUp).normalized);
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
            // In-place climb: remove the take's steady hip rise so the loop closes; that rise is exactly how far one
            // cycle climbs, so it becomes the playback reference (hands stay on the rungs at any motor speed).
            AnimationClip inPlace = MakeInPlaceClimb(h, model, climbClip, out float risePerCycle);
            motion.climbAdvancePerCycle = risePerCycle;
            // Rung-accurate climb (ClimbClipBaker): hands/feet placed on the ladder rungs by IK, torso from the take.
            // Its normalized time is driven by the hero's real height on the ladder ("ClimbPhase"), so a gripping
            // hand stays on its rung at any speed, holds still when stopped and reverses when climbing down.
            var baked = ClimbClipBaker.Bake(h, model, inPlace, $"{h.Folder}/InPlace/Climb.anim");
            motion.climbPhaseDriven = true;
            motion.climbGripTop = baked.GripTop;
            motion.rungSpacing = ClimbClipBaker.RungSpacing;
            motion.firstRung = ClimbClipBaker.FirstRung;
            if (!controller.parameters.Any(p => p.name == "ClimbPhase"))
                controller.AddParameter("ClimbPhase", AnimatorControllerParameterType.Float);
            foreach (var st in controller.layers[0].stateMachine.states)
                if (st.state.name == "Climb")
                {
                    st.state.motion = baked.Clip;
                    st.state.speedParameterActive = false;
                    st.state.timeParameterActive = true;
                    st.state.timeParameter = "ClimbPhase";
                }
            EditorUtility.SetDirty(controller);
            anim.runtimeAnimatorController = null;
            controller.animationClips.First(c => c.name == "Idle").SampleAnimation(model, 0f);
            anim.runtimeAnimatorController = controller;
            Debug.Log($"[ART] {h.Class} run stride {motion.runStrideSpeed:0.00} m/s @1x, climb cycle {motion.climbCycleSeconds:0.00}s");

            SetupFingers(h, root, model, anim);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(h.Class));
            Object.DestroyImmediate(root);
        }
    }
}
