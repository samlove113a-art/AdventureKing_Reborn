using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Shop icons rendered from the real outfit meshes (never concept art), and an in-engine QA sheet of every
    /// hero wearing the outfits in Idle / Run / Jump / Climb / Attack / Skill poses (QA/Outfits/).
    /// </summary>
    public static class OutfitIcons
    {
        private const int IconLayer = 31;

        public static void RenderAll(CosmeticLibrary lib)
        {
            var lightGo = new GameObject("IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.cullingMask = 1 << IconLayer;
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            var camGo = new GameObject("IconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << IconLayer;
            cam.fieldOfView = 20f;
            const int size = 256;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var oldAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.7f);
            try
            {
                var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = false; // keep alpha
                foreach (CosmeticArt art in lib.items)
                    foreach (ClassFit fit in art.fits)
                    {
                        // the item worn by THIS class's hero, framed on the item
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(fit.heroClass)));
                        bool fromBack = art.slot == "back";
                        inst.transform.rotation = Quaternion.Euler(0f, fromBack ? 25f : 200f, 0f);
                        var outfit = inst.GetComponent<HeroOutfit>() ?? inst.AddComponent<HeroOutfit>();
                        var body = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => !s.name.StartsWith("Weapon_"));
                        outfit.Bind(fit.heroClass, body);
                        outfit.Apply(art.slot == "set" ? new OutfitLoadout().WithSet(art.itemId) : new OutfitLoadout().With(art.slot, art.itemId));
                        Animator anim = inst.GetComponentInChildren<Animator>();
                        var idle = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Idle");
                        anim.runtimeAnimatorController = null;
                        if (idle) idle.SampleAnimation(anim.gameObject, 0.2f);
                        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IconLayer;
                        // bounds of the item's pieces in their posed shape (+ the whole hero for full sets / back items)
                        Bounds? bb = null;
                        void Add(SkinnedMeshRenderer s)
                        {
                            var m = new Mesh();
                            s.BakeMesh(m, true);
                            Matrix4x4 w = s.transform.localToWorldMatrix;
                            foreach (Vector3 v in m.vertices)
                            {
                                Vector3 p = w.MultiplyPoint3x4(v);
                                if (bb == null) bb = new Bounds(p, Vector3.zero); else { Bounds x = bb.Value; x.Encapsulate(p); bb = x; }
                            }
                            Object.DestroyImmediate(m);
                        }
                        foreach (GameObject p in outfit.Pieces) { if (!p) continue; var s = p.GetComponent<SkinnedMeshRenderer>(); if (s) Add(s); } // (no "is": editor fake-null)
                        if (art.slot is "set" or "back" or "top" or "bottom") Add(body);
                        if (bb == null) { Object.DestroyImmediate(inst); continue; }
                        Bounds b = bb.Value;
                        Vector3 dir = Quaternion.Euler(-12f, 0f, 0f) * Vector3.back; // camera on the -z side, slightly above
                        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.12f + 0.02f;
                        float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        camGo.transform.position = b.center + dir * dist;
                        camGo.transform.LookAt(b.center);
                        cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 3f);
                        cam.farClipPlane = dist + radius * 3f;
                        cam.Render();
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                        tex.Apply();
                        RenderTexture.active = prev;
                        string path = $"{OutfitArtPipeline.Dir}/Icons/{art.itemId}_{fit.heroClass}.png";
                        File.WriteAllBytes(path, tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                        Object.DestroyImmediate(inst);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.alphaIsTransparency = true;
                        ti.mipmapEnabled = false;
                        ti.SaveAndReimport();
                        fit.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (fit.heroClass == HeroClass.Swordsman || !art.icon) art.icon = fit.icon;
                    }
            }
            finally
            {
                RenderSettings.ambientLight = oldAmbient;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
            }
        }

        // ------------------------------------------------------------------ QA sheet
        public static readonly (string name, OutfitLoadout look)[] QaLooks =
        {
            ("gold_A", new OutfitLoadout().With("head", "head_lotus_crown").With("top", "top_brocade_vest").With("bottom", "bottom_pleat_skirt")
                .With("gloves", "gloves_jade_silk").With("shoes", "shoes_cloud_boots").With("accessory", "acc_silver_bell").With("back", "back_wind_cape")),
            ("gold_B", new OutfitLoadout().With("head", "head_bell_circlet").With("shoes", "shoes_ember_boots").With("back", "back_dragonfly_wings")),
            ("premium", new OutfitLoadout().With("gloves", "gloves_jade_silk").WithSet("set_crystal_zephyr")),
        };

        public static void QaFromCommandLine()
        {
            try { Qa(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildAndQaFromCommandLine()
        {
            int code = 0;
            try { OutfitArtPipeline.BuildAll(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            try { Qa(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("AdventureKing/QA (internal)/Capture Outfit Sheets")]
        public static void Qa()
        {
            Directory.CreateDirectory("QA/Outfits");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.28f);
            var camGo = new GameObject("QACam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.84f, 0.92f);
            cam.fieldOfView = 15f;
            camGo.transform.position = new Vector3(0f, 0.62f, -4.4f);
            camGo.transform.rotation = Quaternion.Euler(3f, 0f, 0f);
            const int cell = 400;
            var rt = new RenderTexture(cell, cell, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var shots = new (string clip, float t, float yaw)[]
            {
                ("Idle", 0.2f, 180f), ("Idle", 0.2f, 110f), ("Idle", 0.2f, 0f), ("Idle", 0.2f, -110f),
                ("Run", 0.25f, 110f), ("Run", 0.75f, 110f), ("Jump", 0.6f, 110f), ("Climb", 0.3f, 0f),
                ("Attack", 0.45f, 110f), ("Skill", 0.5f, 110f),
            };
            foreach (HeroClass c in new[] { HeroClass.Swordsman, HeroClass.Mage, HeroClass.Assassin, HeroClass.Archer })
            {
                var tiles = new List<Texture2D>();
                foreach (var (lookName, look) in QaLooks.Prepend(("none", new OutfitLoadout())))
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(c)));
                    var outfit = inst.GetComponent<HeroOutfit>() ?? inst.AddComponent<HeroOutfit>();
                    var body = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => !s.name.StartsWith("Weapon_"));
                    outfit.Bind(c, body);
                    outfit.Apply(look);
                    Animator anim = inst.GetComponentInChildren<Animator>();
                    var clips = anim.runtimeAnimatorController.animationClips.GroupBy(x => x.name).ToDictionary(g => g.Key, g => g.First());
                    anim.runtimeAnimatorController = null;
                    foreach (var (clip, t, yaw) in shots)
                    {
                        inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        clips[clip].SampleAnimation(anim.gameObject, Mathf.Clamp01(t) * clips[clip].length * 0.999f);
                        cam.Render();
                        tiles.Add(ArtQaCapture.ReadRT(rt));
                    }
                    Debug.Log($"[OUTFIT-QA] {c} {lookName}: {outfit.PieceCount} pieces");
                    Object.DestroyImmediate(inst);
                }
                ArtQaCapture.SaveSheet(tiles, cell, shots.Length, $"QA/Outfits/editor_{c}.png");
            }
            cam.targetTexture = null;
            rt.Release();
        }
    }
}
