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
    /// Imports rigged + animated NPC FBX files (Tools/blender/export_character.py, Meshy rig/animations) into
    /// prefabs that share the heroes' look: Generic rig, in-place clips, extracted texture, AdventureKing/Toon with
    /// the hero outline, Animator with Idle / Walk / Wave / Talk / Bow states.
    /// </summary>
    public static class NpcArtPipeline
    {
        public sealed class Npc
        {
            public string Name, DisplayName;
            public Dictionary<string, (string take, bool loop)> Clips;
            /// <summary>Animated face layer (blinks + expressions) like the heroes: UV3 at import (HeroFaceImport),
            /// eyes/mouth measured from the painted texture. Off = the painted face only.</summary>
            public bool Face;
            /// <summary>Monster: Animator states = the clip keys (Idle/Walk/Attack/Hit/Death), no NpcActor, saved under
            /// Assets/Prefabs/Monsters (used by FarmField through FarmMonsterView).</summary>
            public bool Monster;
        }

        /// <summary>Chương 1 visuals (built on their own so the approved NPCs are not re-imported).</summary>
        public static readonly Npc[] Chapter1 =
        {
            new Npc { Name = "NpcMushroomElder", DisplayName = "Trưởng Làng Nấm", Clips = StandardClips("Wave_One_Hand") },
            new Npc { Name = "MonsterNamNon", DisplayName = "Nấm Non", Monster = true, Clips = new()
            {
                ["Idle"] = ("Idle", true), ["Walk"] = ("Casual_Walk", true), ["Attack"] = ("Right_Jab_from_Guard", false),
                ["Hit"] = ("Hit_Reaction", false), ["Death"] = ("Dead", false),
            }},
        };

        public static void RebuildChapter1FromCommandLine()
        {
            try
            {
                foreach (Npc n in Chapter1) Build(n, FbxPath(n.Name));
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static string FbxPath(string name) => $"Assets/Art/Characters/{name}/{name}.fbx";

        private static Dictionary<string, (string, bool)> StandardClips(string wave) => new()
        {
            ["Idle"] = ("Idle", true), ["Walk"] = ("Casual_Walk", true), ["Wave"] = (wave, false),
            ["Talk"] = ("Talk_with_Hands_Open", true), ["Bow"] = ("Formal_Bow", false),
        };

        public static readonly Npc[] All =
        {
            new Npc { Name = "NpcElder", DisplayName = "Trưởng Làng", Clips = new()
            {
                ["Idle"] = ("Idle", true), ["Walk"] = ("Casual_Walk", true), ["Wave"] = ("Big_Wave_Hello", false),
                ["Talk"] = ("Talk_with_Hands_Open", true), ["Bow"] = ("Formal_Bow", false),
            }},
            new Npc { Name = "NpcSmith", DisplayName = "Thợ Rèn", Clips = new()
            {
                ["Idle"] = ("Idle", true), ["Walk"] = ("Casual_Walk", true), ["Wave"] = ("Wave_One_Hand", false),
                ["Talk"] = ("Talk_with_Hands_Open", true), ["Bow"] = ("Formal_Bow", false),
            }},
            new Npc { Name = "NpcStorekeeper", DisplayName = "Thủ Kho", Clips = new()
            {
                ["Idle"] = ("Idle", true), ["Walk"] = ("Casual_Walk", true), ["Wave"] = ("Wave_One_Hand", false),
                ["Talk"] = ("Talk_with_Hands_Open", true), ["Bow"] = ("Formal_Bow", false),
            }},
            // Phase D halls (Meshy, 2026-11-10): animated faces from the start
            new Npc { Name = "NpcCaptain", DisplayName = "Đội Trưởng Liên Minh", Clips = StandardClips("Wave_One_Hand"), Face = true },
            new Npc { Name = "NpcOfficiant", DisplayName = "Chủ Hôn", Clips = StandardClips("Big_Wave_Hello"), Face = true },
            new Npc { Name = "NpcMusician", DisplayName = "Nhạc Công", Clips = StandardClips("Wave_One_Hand"), Face = true },
        };

        public static string PrefabPath(string name) => name.StartsWith("Monster") ? $"Assets/Prefabs/Monsters/{name}_Visual.prefab" : $"Assets/Prefabs/Npcs/{name}_Visual.prefab";

        public static void RebuildFromCommandLine()
        {
            try { RebuildAll(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("AdventureKing/Art/Rebuild NPC Visuals")]
        public static void RebuildAll()
        {
            foreach (Npc n in All)
            {
                string fbx = $"Assets/Art/Characters/{n.Name}/{n.Name}.fbx";
                if (!File.Exists(fbx)) { Debug.LogWarning("[NPC] missing " + fbx); continue; }
                Build(n, fbx);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Build(Npc n, string fbx)
        {
            string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            // Texture: extract the embedded base colour once.
            string texDir = dir + "/Textures";
            if (!Directory.Exists(texDir) || Directory.GetFiles(texDir, "*.png").Length == 0)
            {
                Directory.CreateDirectory(texDir);
                mi.ExtractTextures(texDir);
                AssetDatabase.Refresh();
                foreach (string f in Directory.GetFiles(texDir).Where(f => !f.EndsWith(".meta") && Path.GetExtension(f) == ""))
                    File.Move(f, f + ".png");
                AssetDatabase.Refresh();
            }

            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = true;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Transform hips = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Hips"));
            if (hips) mi.motionNodeName = AnimationUtility.CalculateTransformPath(hips, go.transform);
            var defaults = mi.defaultClipAnimations;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var kv in n.Clips)
            {
                var src = defaults.FirstOrDefault(d => d.takeName.EndsWith(kv.Value.take)) ?? defaults.FirstOrDefault(d => d.takeName.Contains(kv.Value.take));
                if (src == null) { Debug.LogWarning($"[NPC] {n.Name}: take {kv.Value.take} not found"); continue; }
                src.name = kv.Key;
                src.loopTime = kv.Value.loop;
                src.lockRootRotation = true; src.keepOriginalOrientation = true;
                src.lockRootPositionXZ = true; src.keepOriginalPositionXZ = true;
                src.lockRootHeightY = false; src.keepOriginalPositionY = true;
                clips.Add(src);
            }
            mi.clipAnimations = clips.ToArray();
            mi.SaveAndReimport();

            // Controller: Idle default; other states entered by name (NpcActor.Play).
            string ctrlPath = $"{dir}/{n.Name}.controller";
            if (File.Exists(ctrlPath)) AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            var anims = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            int i = 0;
            foreach (string state in n.Monster ? n.Clips.Keys.ToArray() : new[] { "Idle", "Walk", "Wave", "Talk", "Bow" })
                if (anims.TryGetValue(state, out AnimationClip clip))
                {
                    var s = sm.AddState(state, new Vector3(260, 60 + i++ * 70));
                    s.motion = clip;
                    if (state == "Idle") sm.defaultState = s;
                }

            // Material: hero toon look (same outline, shade and rim as the four heroes).
            string matPath = $"{dir}/{n.Name}_Toon.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(Shader.Find("AdventureKing/Toon")); AssetDatabase.CreateAsset(m, matPath); }
            string tex = Directory.GetFiles(texDir, "*.png").Select(f => f.Replace('\\', '/')).OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            if (tex != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetFloat("_OutlineWidth", 0.0065f);
            m.SetColor("_OutlineColor", WorldKit.OutlineColor);
            m.SetColor("_ShadeColor", new Color(0.62f, 0.58f, 0.72f));
            m.SetFloat("_RimStrength", 0.28f);
            EditorUtility.SetDirty(m);

            // Prefab: root (NpcActor) → Visual (yaw) → Model (Animator).
            var root = new GameObject(n.Name + "_Visual");
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(go, visual);
            model.name = "Model";
            // Facing from the skin BIND POSE (hip joints → right axis; forward = right × up), like the heroes, so
            // yaw 180 always shows the face to the camera regardless of how Meshy oriented this rig.
            SkinnedMeshRenderer body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First();
            Vector3 BindPos(string bone)
            {
                int bi = System.Array.FindIndex(body.bones, b => b && b.name.EndsWith(bone));
                if (bi < 0) return Vector3.zero;
                return body.transform.TransformPoint(body.sharedMesh.bindposes[bi].inverse.GetColumn(3));
            }
            Vector3 right = BindPos("RightUpLeg") - BindPos("LeftUpLeg");
            right.y = 0f;
            if (right.sqrMagnitude > 1e-6f)
            {
                Vector3 fwd = Vector3.Cross(right.normalized, Vector3.up);
                model.transform.localRotation = Quaternion.FromToRotation(fwd, Vector3.forward) * model.transform.localRotation;
                Debug.Log($"[NPC] {n.Name} bind forward {fwd} -> yaw fix {model.transform.localEulerAngles.y:0}");
            }
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, smr.sharedMesh.subMeshCount)).ToArray();
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var anim = model.GetComponent<Animator>();
            if (!anim) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            if (n.Face)
            {
                // Same face layer as the heroes: measured from this NPC's own painted face; skipped (painted face kept)
                // if the measurement fails, never a broken face.
                HeroFaceRig.FaceData fd = HeroFaceRig.Analyse(go, tex, out string faceError);
                SkinnedMeshRenderer bodySmr = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First();
                int headIdx = System.Array.FindIndex(bodySmr.bones, b => b && b.name == "Head");
                // Plausibility: two eyes mirrored across the face centre at the same height, similar size, mouth centred
                // below them. A lopsided measurement would draw eyelids in the wrong place — keep the painted face instead.
                bool plausible = fd != null
                    && Mathf.Abs(fd.EyeA.x + fd.EyeB.x) < 0.12f && Mathf.Abs(fd.EyeA.y - fd.EyeB.y) < 0.08f
                    && Mathf.Min(fd.EyeA.z, fd.EyeB.z) > 0.07f && Mathf.Max(fd.EyeA.z, fd.EyeB.z) < 2f * Mathf.Min(fd.EyeA.z, fd.EyeB.z)
                    && Mathf.Abs(fd.Mouth.x) < 0.08f && fd.Mouth.y < Mathf.Min(fd.EyeA.y, fd.EyeB.y) - 0.1f && fd.Mouth.z > 0.06f;
                if (fd != null && !plausible) faceError = $"implausible measurement: eyes {fd.EyeA} {fd.EyeB}, mouth {fd.Mouth}";
                if (!plausible || headIdx < 0) Debug.LogWarning($"[NPC] {n.Name}: animated face skipped ({faceError ?? "no Head bone"})");
                else
                {
                    HeroFaceRig.Apply(m, fd);
                    var face = model.AddComponent<HeroFace>();
                    face.Bind(bodySmr);
                    var (_, faceMesh, _, _, faceUp, faceFwd, _) = HeroFaceRig.Frame(go);
                    Matrix4x4 meshToHead = faceMesh.bindposes[headIdx];
                    face.BindHead(bodySmr.bones[headIdx], meshToHead.MultiplyVector(faceFwd).normalized, meshToHead.MultiplyVector(faceUp).normalized);
                    Debug.Log($"[NPC] {n.Name}: animated face, eyes {fd.EyeA} {fd.EyeB}, mouth {fd.Mouth}");
                }
            }
            if (!n.Monster)
            {
                var actor = root.AddComponent<NpcActor>();
                var so = new SerializedObject(actor);
                so.FindProperty("animator").objectReferenceValue = anim;
                so.FindProperty("visual").objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath(n.Name)));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(n.Name));
            Object.DestroyImmediate(root);
            Debug.Log($"[NPC] {n.Name}: {clips.Count} clips, prefab {PrefabPath(n.Name)}");
        }
    }
}
