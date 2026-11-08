using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Builds Assets/Scenes/AK_Village.unity — "Làng Chuông Gió", the first 2.5D map:
    /// gameplay on the XY plane (2D physics), 3D toon-shaded scenery with depth behind and in front,
    /// ground + two raised tiers, ladders, a bridge, houses, trees, sky/hill backdrop and a portal arch.
    /// The player is the selected class's real 3D hero (AdventureWorldSession spawns it from the server
    /// snapshot). Scenery is built from primitives with the project's toon shader: a playable, readable
    /// layout, NOT final environment art.
    /// </summary>
    public static class VillageSceneBuilder
    {
        public const string SceneName = "AK_Village";
        public const string ScenePath = "Assets/Scenes/AK_Village.unity";
        private const string MatDir = "Assets/Art/World/Materials";
        private const int GroundLayer = 8, PlayerLayer = 9;
        public const float RunSpeed = 4.6f, ClimbSpeed = 2.2f, HeroScale = 1.55f;
        private static readonly Dictionary<string, Material> mats = new();

        [MenuItem("AdventureKing/Scenes/Rebuild Village Scene")]
        public static void BuildMenu() => Build();

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Entry first (index 0, the boot scene), then the village.</summary>
        public static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(EntrySceneBuilder.ScenePath, true) };
            if (File.Exists(ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void Build()
        {
            mats.Clear();
            EntrySceneBuilder.InitUi();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------------- light + atmosphere
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.78f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.38f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.88f, 0.96f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 95f;

            var world = new GameObject("World").transform;
            BuildBackdrop(world);
            BuildTerrain(world);
            BuildProps(world);

            // ---------------------------------------------------------------- player
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0f, 1f, 0f);
            PlayerMotor2D motor = BuildPlayer(out CharacterVisual3D visual, out Transform mount);

            // ---------------------------------------------------------------- camera + audio
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            // ~8.6 world units visible vertically: the 1.6 m hero fills ~19% of the screen height.
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 140f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.transform.position = new Vector3(0f, 2.6f, -14f);
            camGo.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow2D>();
            var fso = new SerializedObject(follow);
            fso.FindProperty("target").objectReferenceValue = motor.transform;
            fso.FindProperty("offset").vector2Value = new Vector2(0f, 1.8f);
            fso.FindProperty("smoothing").floatValue = 0.14f;
            fso.FindProperty("useBounds").boolValue = true;
            fso.FindProperty("minBounds").vector2Value = new Vector2(-24f, 2.6f);
            fso.FindProperty("maxBounds").vector2Value = new Vector2(64f, 11f);
            fso.ApplyModifiedPropertiesWithoutUndo();

            var audio = new GameObject("AudioDirector").AddComponent<AdventureAudioDirector>();
            AudioLibrary.AssignTo(audio);

            // ---------------------------------------------------------------- HUD + session
            var hud = BuildHud(out Text nameLabel, out Text status, out Button exit);
            var session = hud.AddComponent<AdventureWorldSession>();
            var so = new SerializedObject(session);
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
            so.FindProperty("player").objectReferenceValue = motor;
            so.FindProperty("playerVisual").objectReferenceValue = visual;
            so.FindProperty("visualMount").objectReferenceValue = mount;
            so.FindProperty("characterLabel").objectReferenceValue = nameLabel;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            var prefabs = so.FindProperty("heroPrefabs");
            string[] classes = { "Swordsman", "Mage", "Assassin", "Archer" };
            prefabs.arraySize = classes.Length;
            for (int i = 0; i < classes.Length; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{classes[i]}_Visual.prefab");
                if (!p) throw new FileNotFoundException("Hero prefab missing - run Rebuild Hero Visuals", classes[i]);
                prefabs.GetArrayElementAtIndex(i).objectReferenceValue = p;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(exit.onClick, session.ExitToLogin);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            RegisterBuildScenes();
            Debug.Log("[VILLAGE] Scene built: " + ScenePath);
        }

        // ================================================================== materials / primitives
        private static Material Mat(string name, Color color, float outline = 0f)
        {
            if (mats.TryGetValue(name, out Material cached)) return cached;
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("AdventureKing/Toon"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", WhiteTexture()); // never leave the slot unbound (renders garbage on some frames)
            m.SetColor("_BaseColor", color);
            m.SetColor("_ShadeColor", Color.Lerp(color, new Color(0.45f, 0.48f, 0.65f), 0.55f));
            m.SetFloat("_OutlineWidth", outline);
            m.SetShaderPassEnabled("SRPDefaultUnlit", outline > 0f); // hull outlines split on hard-edged boxes
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        private static Texture2D WhiteTexture()
        {
            const string path = "Assets/Art/World/white.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                t.SetPixels32(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m,
            Vector3? euler = null, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // gameplay uses 2D colliders only
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private static GameObject Solid(Transform parent, string name, float xMin, float xMax, float top, float thickness, bool oneWay)
        {
            var go = new GameObject(name) { layer = GroundLayer };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((xMin + xMax) * 0.5f, top - thickness * 0.5f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(xMax - xMin, thickness);
            if (oneWay)
            {
                col.usedByEffector = true;
                var eff = go.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true;
                eff.surfaceArc = 160f;
            }
            return go;
        }

        // ================================================================== world
        private static void BuildBackdrop(Transform world)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(world, false);
            Material sky = Mat("sky", new Color(0.62f, 0.8f, 0.97f));
            Prim(PrimitiveType.Quad, root, "Sky", new Vector3(20f, 18f, 75f), new Vector3(260f, 90f, 1f), sky, shadows: false);
            Material hillFar = Mat("hill_far", new Color(0.55f, 0.72f, 0.7f));
            Material hillNear = Mat("hill_near", new Color(0.46f, 0.68f, 0.45f));
            float[] farX = { -40f, -5f, 30f, 62f, 95f };
            for (int i = 0; i < farX.Length; i++)
                Prim(PrimitiveType.Sphere, root, "HillFar" + i, new Vector3(farX[i], -6f, 52f), new Vector3(46f, 26f + (i % 2) * 8f, 10f), hillFar, shadows: false);
            float[] nearX = { -28f, 4f, 36f, 70f };
            for (int i = 0; i < nearX.Length; i++)
                Prim(PrimitiveType.Sphere, root, "HillNear" + i, new Vector3(nearX[i], -7f, 32f), new Vector3(38f, 18f + (i % 2) * 5f, 10f), hillNear, shadows: false);
            Material cloud = Mat("cloud", new Color(1f, 0.99f, 0.97f));
            for (int i = 0; i < 7; i++)
            {
                float x = -30f + i * 15f, y = 20f + (i % 3) * 3f;
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Sphere, root, $"Cloud{i}_{k}", new Vector3(x + k * 2.6f, y + (k == 1 ? 1f : 0f), 60f),
                        new Vector3(4.5f, 2.6f, 2f), cloud, shadows: false);
            }
        }

        private static void BuildTerrain(Transform world)
        {
            var root = new GameObject("Terrain").transform;
            root.SetParent(world, false);
            var solids = new GameObject("Collision").transform;
            solids.SetParent(world, false);

            Material grass = Mat("grass", new Color(0.42f, 0.64f, 0.33f));
            Material dirt = Mat("dirt", new Color(0.55f, 0.4f, 0.29f));
            Material stone = Mat("stone", new Color(0.66f, 0.64f, 0.61f));
            Material wood = Mat("wood", new Color(0.6f, 0.43f, 0.29f));
            Material woodDark = Mat("wood_dark", new Color(0.4f, 0.28f, 0.2f));

            // Main ground: x -32..72, top at y=0, depth from z=-3 (foreground lip) to z=9.
            Solid(solids, "Ground", -32f, 72f, 0f, 4f, false);
            // Grass runs toward the camera (z -9) so the lower screen shows meadow, not a dirt wall.
            Prim(PrimitiveType.Cube, root, "GroundBody", new Vector3(20f, -2f, 0f), new Vector3(104f, 4f, 18f), dirt);
            Prim(PrimitiveType.Cube, root, "GroundGrass", new Vector3(20f, -0.12f, 0f), new Vector3(104f, 0.26f, 18.04f), grass);
            // Path of flat stones along the walking line.
            for (float x = -28f; x < 70f; x += 2.3f)
                Prim(PrimitiveType.Cylinder, root, "PathStone", new Vector3(x, 0.02f, -0.2f + Mathf.Sin(x) * 0.25f),
                    new Vector3(1.4f, 0.04f, 0.9f), stone, shadows: false);

            // World edges.
            Solid(solids, "WallLeft", -33f, -32f, 30f, 34f, false);
            Solid(solids, "WallRight", 72f, 73f, 30f, 34f, false);

            // Tier 1 (y=4): deck x 8..20, rope bridge 20..30, deck 30..40.  Tier 2 (y=8): deck 32..46.
            Deck(root, solids, "DeckWest", 8f, 20f, 4f, wood, woodDark, stone);
            Bridge(root, solids, 20f, 30f, 4f, wood, woodDark);
            Deck(root, solids, "DeckEast", 30f, 40f, 4f, wood, woodDark, stone);
            Deck(root, solids, "DeckHigh", 32f, 46f, 8f, wood, woodDark, stone);

            Ladder(root, 10.5f, 0f, 4.6f, wood, woodDark);
            Ladder(root, 37.5f, 4f, 8.6f, wood, woodDark);
            Ladder(root, 44.5f, 0f, 8.6f, wood, woodDark);
        }

        private static void Deck(Transform root, Transform solids, string name, float xMin, float xMax, float top, Material wood, Material dark, Material stone)
        {
            Solid(solids, name, xMin, xMax, top, 0.4f, true);
            float cx = (xMin + xMax) * 0.5f, w = xMax - xMin;
            Prim(PrimitiveType.Cube, root, name + "_Top", new Vector3(cx, top - 0.18f, 1.2f), new Vector3(w, 0.36f, 4.2f), wood);
            Prim(PrimitiveType.Cube, root, name + "_Trim", new Vector3(cx, top - 0.42f, -0.95f), new Vector3(w + 0.1f, 0.2f, 0.18f), dark);
            for (float x = xMin + 0.5f; x <= xMax - 0.4f; x += Mathf.Max(2f, (w - 1f) / Mathf.Max(1, Mathf.Round(w / 4f))))
            {
                Prim(PrimitiveType.Cube, root, name + "_Post", new Vector3(x, (top - 0.4f) * 0.5f, -0.6f), new Vector3(0.35f, top - 0.4f, 0.35f), stone);
                Prim(PrimitiveType.Cube, root, name + "_PostBack", new Vector3(x, (top - 0.4f) * 0.5f, 3f), new Vector3(0.35f, top - 0.4f, 0.35f), stone);
            }
            // Back railing.
            Prim(PrimitiveType.Cube, root, name + "_Rail", new Vector3(cx, top + 0.75f, 3.1f), new Vector3(w, 0.12f, 0.12f), dark);
            for (float x = xMin + 0.3f; x <= xMax; x += 1.6f)
                Prim(PrimitiveType.Cube, root, name + "_Baluster", new Vector3(x, top + 0.4f, 3.1f), new Vector3(0.1f, 0.75f, 0.1f), dark);
        }

        private static void Bridge(Transform root, Transform solids, float xMin, float xMax, float top, Material wood, Material dark)
        {
            Solid(solids, "Bridge", xMin, xMax, top, 0.3f, true);
            for (float x = xMin + 0.3f; x < xMax; x += 0.62f)
                Prim(PrimitiveType.Cube, root, "BridgePlank", new Vector3(x, top - 0.12f + Mathf.Sin((x - xMin) / (xMax - xMin) * Mathf.PI) * -0.05f, 1f),
                    new Vector3(0.5f, 0.14f, 2.4f), wood, new Vector3(0f, 0f, (x % 1.3f) * 2f - 1f));
            foreach (float z in new[] { -0.2f, 2.2f })
                Prim(PrimitiveType.Cube, root, "BridgeRope", new Vector3((xMin + xMax) * 0.5f, top + 0.8f, z), new Vector3(xMax - xMin, 0.06f, 0.06f), dark);
        }

        private static void Ladder(Transform root, float x, float bottom, float top, Material wood, Material dark)
        {
            var go = new GameObject("Ladder");
            go.transform.SetParent(root.parent, false);
            go.transform.position = new Vector3(x, (bottom + top) * 0.5f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.1f, top - bottom);
            go.AddComponent<LadderZone2D>();
            float h = top - bottom;
            foreach (float dx in new[] { -0.42f, 0.42f })
                Prim(PrimitiveType.Cube, go.transform, "Rail", new Vector3(dx, 0f, 0.35f), new Vector3(0.12f, h + 0.3f, 0.12f), dark);
            for (float y = -h * 0.5f + 0.3f; y < h * 0.5f; y += 0.42f)
                Prim(PrimitiveType.Cube, go.transform, "Rung", new Vector3(0f, y, 0.35f), new Vector3(0.84f, 0.08f, 0.1f), wood);
        }

        private static void BuildProps(Transform world)
        {
            var root = new GameObject("Props").transform;
            root.SetParent(world, false);
            Material wall = Mat("house_wall", new Color(0.96f, 0.9f, 0.78f));
            Material beam = Mat("house_beam", new Color(0.5f, 0.33f, 0.22f));
            Material[] roofs = { Mat("roof_red", new Color(0.83f, 0.36f, 0.3f)), Mat("roof_blue", new Color(0.34f, 0.52f, 0.8f)), Mat("roof_teal", new Color(0.3f, 0.66f, 0.6f)) };
            Material window = Mat("window", new Color(1f, 0.86f, 0.5f));
            float[] houseX = { -22f, -10f, 52f, 63f };
            for (int i = 0; i < houseX.Length; i++)
                House(root, new Vector3(houseX[i], 0f, 6.5f), 5.2f + (i % 2), 3.6f + (i % 2) * 0.6f, wall, beam, roofs[i % roofs.Length], window);

            Material trunk = Mat("tree_trunk", new Color(0.5f, 0.34f, 0.22f));
            Material[] leaves = { Mat("leaves_a", new Color(0.36f, 0.66f, 0.32f)), Mat("leaves_b", new Color(0.46f, 0.74f, 0.34f)), Mat("leaves_c", new Color(0.3f, 0.58f, 0.36f)) };
            var rng = new System.Random(20261108);
            float[] treeX = { -30f, -16f, -4f, 3f, 24f, 48f, 57f, 68f };
            foreach (float x in treeX)
                Tree(root, new Vector3(x + (float)rng.NextDouble() * 2f, 0f, 4.5f + (float)rng.NextDouble() * 4f), 0.9f + (float)rng.NextDouble() * 0.5f, trunk, leaves[rng.Next(leaves.Length)]);
            // Low foreground bushes (below the camera line, never covering the hero).
            for (float x = -30f; x < 70f; x += 7.5f)
                Prim(PrimitiveType.Sphere, root, "Bush", new Vector3(x + (float)rng.NextDouble() * 3f, 0.15f, -2.4f), new Vector3(1.8f, 0.9f, 1.1f), leaves[rng.Next(leaves.Length)]);

            // Lamp posts and the village bell frame.
            Material iron = Mat("iron", new Color(0.25f, 0.27f, 0.3f));
            Material glow = Mat("lamp_glow", new Color(1f, 0.9f, 0.6f));
            foreach (float x in new[] { -14f, 4f, 26f, 50f })
            {
                Prim(PrimitiveType.Cylinder, root, "LampPost", new Vector3(x, 1.4f, 2.2f), new Vector3(0.14f, 1.4f, 0.14f), iron);
                Prim(PrimitiveType.Sphere, root, "LampGlass", new Vector3(x, 2.95f, 2.2f), new Vector3(0.45f, 0.55f, 0.45f), glow, shadows: false);
            }
            Material bellMat = Mat("bell_bronze", new Color(0.86f, 0.64f, 0.32f));
            Prim(PrimitiveType.Cube, root, "BellPostL", new Vector3(-1.6f, 2f, 4f), new Vector3(0.3f, 4f, 0.3f), beam);
            Prim(PrimitiveType.Cube, root, "BellPostR", new Vector3(1.6f, 2f, 4f), new Vector3(0.3f, 4f, 0.3f), beam);
            Prim(PrimitiveType.Cube, root, "BellBeam", new Vector3(0f, 4.1f, 4f), new Vector3(3.8f, 0.35f, 0.4f), beam);
            Prim(PrimitiveType.Sphere, root, "Bell", new Vector3(0f, 3.3f, 4f), new Vector3(1.1f, 1.2f, 1.1f), bellMat);

            // Portal arch at the east end (travel is not implemented yet — visual landmark only).
            Material portalStone = Mat("portal_stone", new Color(0.62f, 0.6f, 0.72f));
            Prim(PrimitiveType.Cube, root, "PortalL", new Vector3(65.4f, 1.8f, 1.5f), new Vector3(0.7f, 3.6f, 0.9f), portalStone);
            Prim(PrimitiveType.Cube, root, "PortalR", new Vector3(69.6f, 1.8f, 1.5f), new Vector3(0.7f, 3.6f, 0.9f), portalStone);
            Prim(PrimitiveType.Cube, root, "PortalTop", new Vector3(67.5f, 3.9f, 1.5f), new Vector3(5f, 0.7f, 0.9f), portalStone);
            var ring = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/rune_add.mat");
            if (ring)
            {
                var q = Prim(PrimitiveType.Quad, root, "PortalGlow", new Vector3(67.5f, 1.85f, 1.5f), new Vector3(3.4f, 3.4f, 1f), ring, shadows: false);
                q.GetComponent<MeshRenderer>().receiveShadows = false;
            }
        }

        private static void House(Transform root, Vector3 at, float width, float height, Material wall, Material beam, Material roof, Material window)
        {
            var h = new GameObject("House").transform;
            h.SetParent(root, false);
            h.localPosition = at;
            Prim(PrimitiveType.Cube, h, "Walls", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 3.6f), wall);
            Prim(PrimitiveType.Cube, h, "Sill", new Vector3(0f, 0.2f, -1.82f), new Vector3(width + 0.2f, 0.4f, 0.1f), beam);
            foreach (float dx in new[] { -width * 0.5f, width * 0.5f })
                Prim(PrimitiveType.Cube, h, "Corner", new Vector3(dx, height * 0.5f, -1.8f), new Vector3(0.28f, height, 0.2f), beam);
            Prim(PrimitiveType.Cube, h, "Door", new Vector3(-width * 0.18f, 1.05f, -1.83f), new Vector3(1.1f, 2.1f, 0.1f), beam);
            Prim(PrimitiveType.Cube, h, "Window", new Vector3(width * 0.22f, height * 0.6f, -1.83f), new Vector3(1.1f, 0.9f, 0.08f), window, shadows: false);
            // Gable roof: two slabs meeting at the ridge.
            float pitch = 34f, half = width * 0.5f + 0.5f, slab = half / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * half;
            Prim(PrimitiveType.Cube, h, "RoofL", new Vector3(-half * 0.5f, height + rise * 0.5f, 0f), new Vector3(slab, 0.3f, 4.3f), roof, new Vector3(0f, 0f, pitch));
            Prim(PrimitiveType.Cube, h, "RoofR", new Vector3(half * 0.5f, height + rise * 0.5f, 0f), new Vector3(slab, 0.3f, 4.3f), roof, new Vector3(0f, 0f, -pitch));
            Prim(PrimitiveType.Cube, h, "Gable", new Vector3(0f, height + rise * 0.35f, -1.55f), new Vector3(width * 0.62f, rise * 0.7f, 0.3f), wall, new Vector3(0f, 0f, 45f));
            Prim(PrimitiveType.Cube, h, "Chimney", new Vector3(half * 0.45f, height + rise * 0.9f, 0.8f), new Vector3(0.5f, 1.4f, 0.5f), beam);
        }

        private static void Tree(Transform root, Vector3 at, float size, Material trunk, Material leaves)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            t.localScale = Vector3.one * size;
            Prim(PrimitiveType.Cylinder, t, "Trunk", new Vector3(0f, 1.4f, 0f), new Vector3(0.45f, 1.4f, 0.45f), trunk);
            Prim(PrimitiveType.Sphere, t, "Crown", new Vector3(0f, 3.5f, 0f), new Vector3(2.8f, 2.4f, 2.8f), leaves);
            Prim(PrimitiveType.Sphere, t, "CrownL", new Vector3(-0.9f, 2.9f, 0.2f), new Vector3(1.8f, 1.6f, 1.8f), leaves);
            Prim(PrimitiveType.Sphere, t, "CrownR", new Vector3(0.95f, 3f, -0.2f), new Vector3(1.9f, 1.7f, 1.9f), leaves);
        }

        // ================================================================== player + HUD
        private static PlayerMotor2D BuildPlayer(out CharacterVisual3D visual, out Transform mount)
        {
            var go = new GameObject("Player") { layer = PlayerLayer };
            go.transform.position = new Vector3(0f, 1f, 0f);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.8f, 1.6f);
            string pmPath = "Assets/Art/World/Player_NoFriction.physicsMaterial2D";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(pmPath);
            if (!pm)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pmPath));
                pm = new PhysicsMaterial2D("Player_NoFriction") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(pm, pmPath);
            }
            capsule.sharedMaterial = pm;

            var health = go.AddComponent<Health2D>();
            var hso = new SerializedObject(health);
            hso.FindProperty("faction").enumValueIndex = (int)CombatFaction.Player;
            hso.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<PlayerInput2D>();
            var motor = go.AddComponent<PlayerMotor2D>();
            var melee = go.AddComponent<PlayerMelee2D>();

            var probe = new GameObject("GroundProbe").transform;
            probe.SetParent(go.transform, false);
            probe.localPosition = new Vector3(0f, -0.82f, 0f);
            var origin = new GameObject("AttackOrigin").transform;
            origin.SetParent(go.transform, false);
            origin.localPosition = new Vector3(0f, 0.1f, 0f);
            mount = new GameObject("VisualMount").transform;
            mount.SetParent(go.transform, false);
            mount.localPosition = new Vector3(0f, -0.8f, 0f); // capsule bottom = hero's feet

            var mso = new SerializedObject(motor);
            mso.FindProperty("groundProbe").objectReferenceValue = probe;
            mso.FindProperty("groundMask").intValue = 1 << GroundLayer;
            // Chibi proportions: 4.6 m/s (~2.9 body heights/s) keeps the run cadence readable once Run playback
            // is matched to ground speed (see HeroMotionProfile); faster speeds need frantic leg cycles.
            mso.FindProperty("runSpeed").floatValue = RunSpeed;
            mso.FindProperty("climbSpeed").floatValue = ClimbSpeed;
            mso.ApplyModifiedPropertiesWithoutUndo();
            var meso = new SerializedObject(melee);
            meso.FindProperty("attackOrigin").objectReferenceValue = origin;
            meso.FindProperty("targets").intValue = ~(1 << PlayerLayer);
            meso.ApplyModifiedPropertiesWithoutUndo();

            visual = go.AddComponent<CharacterVisual3D>();
            return motor;
        }

        private static GameObject BuildHud(out Text nameLabel, out Text status, out Button exit)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            var canvasGo = new GameObject("HUD", typeof(RectTransform)) { layer = 5 };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Color ink = new Color(0.08f, 0.1f, 0.16f, 0.78f), cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.82f, 0.45f);
            var plate = EntrySceneBuilder.Panel(canvasGo.transform, "CharacterPlate", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(250f, -56f), new Vector2(460f, 72f), ink, EntrySceneBuilder.PanelSprite);
            nameLabel = EntrySceneBuilder.Label(plate.transform, "CharacterName", "", 26, cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 60f), FontStyle.Bold);
            var mapPlate = EntrySceneBuilder.Panel(canvasGo.transform, "MapPlate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(340f, 56f), ink, EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(mapPlate.transform, "MapName", "Làng Chuông Gió", 26, gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f), FontStyle.Bold);
            exit = EntrySceneBuilder.Btn(canvasGo.transform, "ExitButton", "Lưu & Thoát", new Vector2(-130f, -56f), new Vector2(220f, 60f),
                new Color(0.62f, 0.26f, 0.24f), Color.white, new Vector2(1f, 1f));
            var hint = EntrySceneBuilder.Panel(canvasGo.transform, "ControlsHint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 44f), new Vector2(980f, 48f), new Color(0.08f, 0.1f, 0.16f, 0.55f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(hint.transform, "Hint", "A / D di chuyển   ·   Space nhảy   ·   W / S leo thang   ·   J tấn công",
                22, cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 44f));
            status = EntrySceneBuilder.Label(canvasGo.transform, "WorldStatus", "", 24, new Color(1f, 0.7f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1200f, 40f));
            return canvasGo;
        }
    }
}
