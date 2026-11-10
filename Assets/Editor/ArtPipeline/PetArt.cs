using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Four original chibi pets built in-engine (no Meshy, no external art): shaped primitives with the project's toon
    /// shader. Each prefab has named parts the runtime animates procedurally (PetController): Body (squash / stretch,
    /// hop), Head (tilt, bob), EyeL/EyeR (blink, happy squint), Tail*, WingL/WingR, Aura (VIP: orbiting sparkles).
    /// Also renders a portrait PNG per pet for the UI.
    /// STATUS: original procedural models; NEED a commercial model / animation pass before release.
    /// </summary>
    public static class PetArt
    {
        public const string Dir = "Assets/Art/Pets";
        public const string PrefabDir = "Assets/Prefabs/Pets";
        public static readonly string[] Ids = { "pet_cloud_cat", "pet_chick", "pet_jade_dragon", "pet_fire_fox" };
        public static string PrefabPath(string id) => $"{PrefabDir}/{id}.prefab";
        public static string PortraitPath(string id) => $"{Dir}/portrait_{id}.png";

        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        private static Material Mat(string name, Color c, Color? emission = null)
        {
            if (mats.TryGetValue(name, out var m0) && m0) return m0;
            Directory.CreateDirectory(Dir + "/Materials");
            string path = $"{Dir}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("AdventureKing/Toon")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AdventureKing/Toon");
            m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", c);
            m.SetColor("_ShadeColor", Color.Lerp(c, new Color(0.42f, 0.4f, 0.6f), 0.45f));
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>Four-sided cone (ears, beak, horns): base at y=0, tip at y=1.</summary>
        private static Mesh cone;
        private static Mesh Cone()
        {
            if (cone) return cone;
            const int n = 12;
            var v = new List<Vector3>(); var tri = new List<int>();
            v.Add(new Vector3(0, 1, 0)); v.Add(Vector3.zero);
            for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2f / n; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f)); }
            for (int i = 0; i < n; i++) { tri.AddRange(new[] { 0, 3 + i, 2 + i }); tri.AddRange(new[] { 1, 2 + i, 3 + i }); }
            cone = new Mesh { name = "pet_cone", vertices = v.ToArray(), triangles = tri.ToArray() };
            cone.RecalculateNormals();
            string path = $"{Dir}/pet_cone.asset";
            Directory.CreateDirectory(Dir);
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cone, path);
            return cone;
        }

        private static GameObject ConePart(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = Cone();
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Eyes with white highlights + blush (shared chibi face), on the head pivot.</summary>
        private static void Face(Transform head, float spread, float y, float z, float size, Color blush)
        {
            Material ink = Mat("pet_eye", new Color(0.08f, 0.06f, 0.1f)), white = Mat("pet_eye_hi", Color.white, new Color(0.4f, 0.4f, 0.4f));
            Material cheek = Mat("pet_blush_" + ColorUtility.ToHtmlStringRGB(blush), blush);
            foreach (float s in new[] { -1f, 1f })
            {
                var eye = Part(head, s < 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere, new Vector3(spread * s, y, z), new Vector3(size, size * 1.25f, size * 0.6f), ink);
                Part(eye.transform, "Shine", PrimitiveType.Sphere, new Vector3(-0.22f, 0.25f, -0.4f), Vector3.one * 0.38f, white);
                Part(head, "Cheek", PrimitiveType.Sphere, new Vector3((spread + size * 0.9f) * s, y - size * 1.1f, z + 0.02f), new Vector3(size * 1.1f, size * 0.55f, size * 0.3f), cheek);
            }
        }

        private static GameObject Root(string id, out Transform body, out Transform head)
        {
            var root = new GameObject(id);
            Part(root.transform, "Shadow", PrimitiveType.Sphere, new Vector3(0f, 0.01f, 0f), new Vector3(0.7f, 0.02f, 0.5f), Mat("pet_shadow", new Color(0.1f, 0.14f, 0.1f)))
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            body = Pivot(root.transform, "Body", Vector3.zero);
            head = Pivot(body, "Head", new Vector3(0f, 0.5f, 0f));
            return root;
        }

        // ------------------------------------------------------------------ the four pets
        private static GameObject CloudCat()
        {
            var root = Root("pet_cloud_cat", out var body, out var head);
            Material fur = Mat("cat_fur", new Color(0.96f, 0.92f, 0.88f)), pink = Mat("cat_pink", new Color(1f, 0.7f, 0.78f)), cloud = Mat("cat_cloud", new Color(0.8f, 0.9f, 1f), new Color(0.15f, 0.2f, 0.3f));
            Part(body, "Torso", PrimitiveType.Sphere, new Vector3(0f, 0.24f, 0f), new Vector3(0.5f, 0.42f, 0.44f), fur);
            Part(head, "Skull", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.6f, 0.52f, 0.5f), fur);
            foreach (float s in new[] { -1f, 1f })
            {
                ConePart(head, "Ear", new Vector3(0.19f * s, 0.18f, 0f), new Vector3(0.2f, 0.24f, 0.14f), fur, new Vector3(0f, 0f, -18f * s));
                ConePart(head, "EarIn", new Vector3(0.19f * s, 0.19f, -0.03f), new Vector3(0.12f, 0.16f, 0.06f), pink, new Vector3(0f, 0f, -18f * s));
                Part(body, "Paw", PrimitiveType.Sphere, new Vector3(0.14f * s, 0.05f, -0.1f), new Vector3(0.14f, 0.09f, 0.16f), fur);
            }
            Part(head, "Nose", PrimitiveType.Sphere, new Vector3(0f, -0.05f, -0.25f), new Vector3(0.05f, 0.035f, 0.03f), pink);
            Face(head, 0.12f, 0.02f, -0.22f, 0.075f, new Color(1f, 0.62f, 0.72f));
            var tail = Pivot(body, "Tail", new Vector3(0f, 0.22f, 0.22f));
            Part(tail, "TailFluff", PrimitiveType.Capsule, new Vector3(0f, 0.12f, 0.06f), new Vector3(0.1f, 0.16f, 0.1f), fur, new Vector3(-30f, 0f, 0f));
            foreach (var (x, s) in new[] { (-0.22f, 0.18f), (0.2f, 0.16f), (0f, 0.2f) })
                Part(body, "Cloud", PrimitiveType.Sphere, new Vector3(x, 0.04f, 0.08f), new Vector3(s, s * 0.55f, s), cloud);
            return root;
        }

        private static GameObject Chick()
        {
            var root = Root("pet_chick", out var body, out var head);
            Material yellow = Mat("chick_yellow", new Color(1f, 0.86f, 0.32f)), orange = Mat("chick_orange", new Color(1f, 0.55f, 0.15f));
            Part(body, "Torso", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.6f, 0.56f), yellow);
            head.localPosition = new Vector3(0f, 0.38f, 0f);
            Part(head, "Skull", PrimitiveType.Sphere, new Vector3(0f, 0.08f, -0.02f), new Vector3(0.42f, 0.38f, 0.38f), yellow);
            ConePart(head, "Beak", new Vector3(0f, 0.0f, -0.29f), new Vector3(0.12f, 0.12f, 0.1f), orange, new Vector3(-90f, 0f, 0f));
            for (int i = 0; i < 3; i++) Part(head, "Tuft", PrimitiveType.Sphere, new Vector3(-0.04f + i * 0.04f, 0.3f + (i == 1 ? 0.04f : 0f), 0f), new Vector3(0.07f, 0.1f, 0.05f), yellow);
            Face(head, 0.11f, 0.07f, -0.27f, 0.06f, new Color(1f, 0.62f, 0.5f)); // in front of the round body
            foreach (float s in new[] { -1f, 1f })
            {
                var wing = Pivot(body, s < 0 ? "WingL" : "WingR", new Vector3(0.29f * s, 0.32f, 0f));
                Part(wing, "Feather", PrimitiveType.Sphere, new Vector3(0.05f * s, -0.04f, 0f), new Vector3(0.12f, 0.24f, 0.2f), yellow, new Vector3(0f, 0f, 20f * s));
                Part(body, "Foot", PrimitiveType.Sphere, new Vector3(0.12f * s, 0.02f, -0.06f), new Vector3(0.12f, 0.04f, 0.16f), orange);
            }
            return root;
        }

        private static GameObject JadeDragon()
        {
            var root = Root("pet_jade_dragon", out var body, out var head);
            Material jade = Mat("dragon_jade", new Color(0.3f, 0.82f, 0.6f)), belly = Mat("dragon_belly", new Color(0.95f, 0.95f, 0.75f));
            Material gold = Mat("dragon_gold", new Color(1f, 0.82f, 0.3f), new Color(0.25f, 0.18f, 0.02f)), wingM = Mat("dragon_wing", new Color(0.6f, 0.95f, 0.85f), new Color(0.08f, 0.2f, 0.15f));
            Part(body, "Torso", PrimitiveType.Sphere, new Vector3(0f, 0.26f, 0.02f), new Vector3(0.48f, 0.46f, 0.52f), jade);
            Part(body, "Belly", PrimitiveType.Sphere, new Vector3(0f, 0.24f, -0.13f), new Vector3(0.32f, 0.36f, 0.25f), belly);
            Part(head, "Skull", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.52f, 0.46f, 0.48f), jade);
            Part(head, "Snout", PrimitiveType.Sphere, new Vector3(0f, -0.08f, -0.2f), new Vector3(0.28f, 0.18f, 0.2f), jade);
            foreach (float s in new[] { -1f, 1f })
            {
                ConePart(head, "Horn", new Vector3(0.12f * s, 0.18f, 0.06f), new Vector3(0.08f, 0.2f, 0.08f), gold, new Vector3(-20f, 0f, -15f * s));
                var wing = Pivot(body, s < 0 ? "WingL" : "WingR", new Vector3(0.18f * s, 0.42f, 0.18f));
                Part(wing, "Membrane", PrimitiveType.Sphere, new Vector3(0.16f * s, 0.06f, 0.02f), new Vector3(0.34f, 0.2f, 0.04f), wingM, new Vector3(0f, 20f * s, 25f * s));
                Part(body, "Paw", PrimitiveType.Sphere, new Vector3(0.13f * s, 0.05f, -0.1f), new Vector3(0.12f, 0.08f, 0.14f), jade);
            }
            Face(head, 0.11f, 0.06f, -0.2f, 0.07f, new Color(0.6f, 1f, 0.8f));
            var tail = Pivot(body, "Tail", new Vector3(0f, 0.18f, 0.26f));
            Part(tail, "Tail1", PrimitiveType.Capsule, new Vector3(0f, 0.04f, 0.1f), new Vector3(0.14f, 0.14f, 0.14f), jade, new Vector3(70f, 0f, 0f));
            Part(tail, "TailTip", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.26f), new Vector3(0.1f, 0.1f, 0.1f), gold);
            Aura(root.transform, new Color(1f, 0.9f, 0.5f));
            return root;
        }

        private static GameObject FireFox()
        {
            var root = Root("pet_fire_fox", out var body, out var head);
            Material fur = Mat("fox_orange", new Color(1f, 0.5f, 0.18f)), cream = Mat("fox_cream", new Color(1f, 0.95f, 0.85f)), dark = Mat("fox_dark", new Color(0.3f, 0.15f, 0.1f));
            Material flame = Mat("fox_flame", new Color(1f, 0.85f, 0.3f), new Color(0.9f, 0.45f, 0.05f));
            Part(body, "Torso", PrimitiveType.Sphere, new Vector3(0f, 0.25f, 0.03f), new Vector3(0.46f, 0.42f, 0.54f), fur);
            Part(body, "Chest", PrimitiveType.Sphere, new Vector3(0f, 0.27f, -0.15f), new Vector3(0.28f, 0.3f, 0.2f), cream);
            Part(head, "Skull", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.54f, 0.46f, 0.46f), fur);
            Part(head, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, -0.09f, -0.18f), new Vector3(0.24f, 0.16f, 0.18f), cream);
            Part(head, "Nose", PrimitiveType.Sphere, new Vector3(0f, -0.06f, -0.27f), new Vector3(0.05f, 0.04f, 0.04f), dark);
            foreach (float s in new[] { -1f, 1f })
            {
                ConePart(head, "Ear", new Vector3(0.17f * s, 0.16f, 0.02f), new Vector3(0.18f, 0.3f, 0.12f), fur, new Vector3(0f, 0f, -14f * s));
                ConePart(head, "EarTip", new Vector3(0.21f * s, 0.36f, 0.02f), new Vector3(0.08f, 0.12f, 0.06f), dark, new Vector3(0f, 0f, -14f * s));
                Part(body, "Paw", PrimitiveType.Sphere, new Vector3(0.12f * s, 0.05f, -0.12f), new Vector3(0.11f, 0.08f, 0.13f), dark);
            }
            Face(head, 0.11f, 0.03f, -0.2f, 0.07f, new Color(1f, 0.55f, 0.45f));
            for (int i = 0; i < 3; i++)
            {
                var tail = Pivot(body, "Tail" + i, new Vector3((i - 1) * 0.1f, 0.22f, 0.25f));
                tail.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 28f);
                Part(tail, "Brush", PrimitiveType.Capsule, new Vector3(0f, 0.16f, 0.06f), new Vector3(0.13f, 0.2f, 0.13f), fur, new Vector3(-25f, 0f, 0f));
                Part(tail, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.36f, 0.12f), new Vector3(0.12f, 0.16f, 0.12f), flame);
            }
            Aura(root.transform, new Color(1f, 0.55f, 0.15f));
            return root;
        }

        /// <summary>VIP aura: three small glowing orbs (orbited at runtime) and a faint ring on the ground.</summary>
        private static void Aura(Transform root, Color glow)
        {
            var aura = Pivot(root, "Aura", new Vector3(0f, 0.35f, 0f));
            Material orb = Mat("aura_" + ColorUtility.ToHtmlStringRGB(glow), glow, glow * 0.9f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Part(aura, "Orb" + i, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.42f, 0.1f * i, Mathf.Sin(a) * 0.42f), Vector3.one * 0.06f, orb).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Part(root, "AuraRing", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.75f, 0.004f, 0.6f), Mat("aura_ring_" + ColorUtility.ToHtmlStringRGB(glow), glow * 0.8f, glow * 0.6f))
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ build + portraits
        [MenuItem("Adventure King/Art/Build Pets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(Dir);
            Func<GameObject>[] makers = { CloudCat, Chick, JadeDragon, FireFox };
            for (int i = 0; i < makers.Length; i++)
            {
                GameObject go = makers[i]();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(Ids[i]));
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Portraits();
            Debug.Log("[PETS] built " + Ids.Length + " pets");
        }

        private static void Portraits()
        {
            const int size = 256, layer = 31;
            var lightGo = new GameObject("PetLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.05f; light.cullingMask = 1 << layer;
            lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            var camGo = new GameObject("PetCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.cullingMask = 1 << layer; cam.fieldOfView = 22f;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            var ambient = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.7f, 0.68f, 0.72f);
            var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            bool batcher = urp && urp.useSRPBatcher;
            if (urp) urp.useSRPBatcher = false; // in-memory batch renders: see LogoArt
            try
            {
                foreach (string id in Ids)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id)));
                    foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                    inst.transform.rotation = Quaternion.Euler(0f, 25f, 0f); // faces look down -z (toward the camera)
                    camGo.transform.position = new Vector3(0f, 0.5f, -2.6f);
                    camGo.transform.LookAt(new Vector3(0f, 0.38f, 0f));
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(PortraitPath(id), tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                    UnityEngine.Object.DestroyImmediate(inst);
                    AssetDatabase.ImportAsset(PortraitPath(id), ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(PortraitPath(id));
                    ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
                }
            }
            finally
            {
                if (urp) urp.useSRPBatcher = batcher;
                RenderSettings.ambientLight = ambient; RenderSettings.ambientMode = mode;
                cam.targetTexture = null; rt.Release();
                UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(lightGo);
            }
        }

        public static Sprite Portrait(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath(id));
        public static GameObject Prefab(string id) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id));

        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
