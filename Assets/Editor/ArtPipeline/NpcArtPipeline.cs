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
        }

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
        };

        public static string PrefabPath(string name) => $"Assets/Prefabs/Npcs/{name}_Visual.prefab";

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
            foreach (string state in new[] { "Idle", "Walk", "Wave", "Talk", "Bow" })
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
            var actor = root.AddComponent<NpcActor>();
            var so = new SerializedObject(actor);
            so.FindProperty("animator").objectReferenceValue = anim;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/Prefabs/Npcs");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(n.Name));
            Object.DestroyImmediate(root);
            Debug.Log($"[NPC] {n.Name}: {clips.Count} clips, prefab {PrefabPath(n.Name)}");
        }
    }
}
