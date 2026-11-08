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
    /// Builds the world maps from <see cref="TownLayout"/>:
    ///  - AK_Village = "Thị trấn Chuông Gió", the multi-tier central town (hub);
    ///  - AK_Meadow  = "Đồng Cỏ Gió", the first field, linked by the original rune-crystal portal.
    /// Art direction (original Adventure King): Vietnamese-inspired fantasy — curved tile roofs, wind chimes,
    /// lanterns, banyan and bamboo, a lotus stream, rune crystals. Scenery = Meshy paintings placed as depth
    /// layers (AdventureKing/Cutout) + 3D gameplay structures (decks, ladders, ropes, bridge, portal) + painted
    /// parallax backdrop. Gameplay = 2D physics on the z = 0 plane.
    /// </summary>
    public static class VillageSceneBuilder
    {
        public const string SceneName = "AK_Village";
        public const string ScenePath = "Assets/Scenes/AK_Village.unity";
        public const string MeadowScenePath = "Assets/Scenes/AK_Meadow.unity";
        private const string WorldDir = "Assets/Art/World";
        private const string MatDir = WorldDir + "/Materials";
        private const string CutoutDir = WorldDir + "/Town/Cutouts";
        public const int GroundLayer = 8, PlayerLayer = 9, OneWayLayer = 11;
        public const float RunSpeed = 4.6f, ClimbSpeed = 2.2f, HeroScale = 1.55f;
        private static readonly Color Sky = new Color(0.74f, 0.86f, 0.96f);
        private static readonly Dictionary<string, Material> mats = new();
        private static readonly Dictionary<string, Rect> alphaBounds = new();

        [MenuItem("AdventureKing/Scenes/Rebuild World Maps")]
        public static void BuildMenu() => Build();

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Build()
        {
            EnsureLayer(OneWayLayer, "OneWayPlatform");
            BuildTown();
            BuildMeadow();
            RegisterBuildScenes();
        }

        /// <summary>Entry first (boot scene), then the world maps.</summary>
        public static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(EntrySceneBuilder.ScenePath, true) };
            foreach (string p in new[] { ScenePath, MeadowScenePath })
                if (File.Exists(p)) scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================== Thị trấn Chuông Gió
        private static void BuildTown()
        {
            SceneTag = "hub";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");

            Ground(art, solids, TownLayout.Hub.MinX, TownLayout.Hub.MaxX, stream: true);
            Stream(art, TownLayout.Hub.StreamX, TownLayout.Hub.StreamHalfWidth);
            foreach (var d in TownLayout.Hub.Decks)
                if (d.Name == "CauTreo") RopeBridge(art, solids, d);
                else Deck(art, solids, d, d.Top >= 9f ? "deck_high" : "deck");
            foreach (var c in TownLayout.Hub.Climbs) ClimbZone(art, play, c);

            // ---- painted depth layers (x, z, height, flip, haze). Far → near.
            Prop(art, "windmill", 46f, 24f, 11f, false);
            Prop(art, "cottage", -44f, 22f, 7.5f, true);
            Prop(art, "bell_tower", 3f, 13f, 15f, false);
            Prop(art, "stone_bridge", TownLayout.Hub.StreamX, 10f, 4.2f, false);
            Prop(art, "banyan_tree", -10f, 12f, 11f, true);
            Prop(art, "teahouse", 40f, 8.5f, 10f, false);
            Prop(art, "cottage", 57f, 8f, 7.5f, false);
            Prop(art, "cottage", -58f, 9f, 7.5f, true);
            Prop(art, "banyan_tree", 22f, 7f, 10.5f, false);
            Prop(art, "forge", -45f, 6f, 7f, false);
            Prop(art, "hero_statue", 3f, 4.5f, 5.8f, false);
            Prop(art, "bamboo", -24f, 5.5f, 6.5f, false);
            Prop(art, "bamboo", 62f, 5f, 6f, true);
            Prop(art, "market_stall", -30f, 3.2f, 4.6f, false);
            Prop(art, "crates", -25f, 2.6f, 2.1f, true);
            Prop(art, "crates", 52f, 2.6f, 2f, false);
            Lantern(art, "stone_lantern", -8f, 2.4f, 2f, false);
            Lantern(art, "stone_lantern", 17f, 2.4f, 2f, true);
            Lantern(art, "lantern_post", -1.5f, 2f, 3.6f, false);
            Lantern(art, "lantern_post", 29f, 2f, 3.6f, true);
            Lantern(art, "lantern_post", 63f, 2.2f, 3.6f, false);
            Prop(art, "planter", 9f, 2.2f, 1.3f, false);
            Prop(art, "planter", -35f, 2.2f, 1.3f, true);
            // The parchment panel is portrait (≈0.76 × 0.92 m): short lines, broken by meaning, never by the wrapper.
            Signboard(art, -38f, 2.6f, "Phố\nThợ Rèn\n&\nChợ Phiên");
            Signboard(art, 34f, 2.6f, "Quán Trà\nGió\nChiều");
            Signboard(art, 59f, 2.6f, "Cổng\nDịch Chuyển\n→\nĐồng Cỏ Gió");

            foreach (var spot in TownLayout.Hub.NpcSpots)
            {
                var t = new GameObject("NpcSpot_" + spot.role).transform;
                t.SetParent(play, false);
                t.position = new Vector3(spot.x, spot.y, 0f);
            }
            foreach (var p in TownLayout.Hub.Portals) Portal(play, p);
            PlaceNpc(play, "NpcElder", "Trưởng Làng", 7f, 0f);
            PlaceNpc(play, "NpcSmith", "Thợ Rèn", -46f, 0f);
            PlaceNpc(play, "NpcStorekeeper", "Thủ Kho", -40f, 4.5f);

            FinishWorld(scene, ScenePath, world, TownLayout.Hub.MapId, TownLayout.Hub.DisplayName, TownLayout.Hub.Spawn,
                TownLayout.Hub.MinX, TownLayout.Hub.MaxX, 14f, AdventureMusicStage.Village);
        }

        // ================================================================== Đồng Cỏ Gió
        private static void BuildMeadow()
        {
            SceneTag = "meadow";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");
            Ground(art, solids, TownLayout.Meadow.MinX, TownLayout.Meadow.MaxX, stream: false);
            foreach (var d in TownLayout.Meadow.Decks) Deck(art, solids, d, "deck");
            foreach (var c in TownLayout.Meadow.Climbs) ClimbZone(art, play, c);

            Prop(art, "windmill", 20f, 26f, 12f, true);
            Prop(art, "banyan_tree", -22f, 14f, 12f, false);
            Prop(art, "banyan_tree", 48f, 12f, 11f, true);
            Prop(art, "bamboo", -2f, 9f, 7f, false);
            Prop(art, "bamboo", 34f, 8f, 7f, true);
            Prop(art, "bamboo", 70f, 7f, 6.5f, false);
            Prop(art, "stone_bridge", 62f, 9f, 4f, false);
            Lantern(art, "stone_lantern", -30f, 2.4f, 2f, false);
            Lantern(art, "stone_lantern", 10f, 2.4f, 2f, true);
            Prop(art, "planter", 24f, 2.4f, 1.3f, false);
            Prop(art, "crates", 44f, 2.6f, 2f, true);
            Lantern(art, "lantern_post", -42.5f, 2f, 3.6f, false);
            Signboard(art, -33f, 2.6f, "Đồng Cỏ Gió\n←\nVề\nThị Trấn");
            foreach (var p in TownLayout.Meadow.Portals) Portal(play, p);

            FinishWorld(scene, MeadowScenePath, world, TownLayout.Meadow.MapId, TownLayout.Meadow.DisplayName, TownLayout.Meadow.Spawn,
                TownLayout.Meadow.MinX, TownLayout.Meadow.MaxX, 11f, AdventureMusicStage.Exploration);
        }

        // ================================================================== scene scaffolding
        private static string SceneTag = "hub";

        private static Scene NewWorldScene(out Transform world)
        {
            mats.Clear();
            EntrySceneBuilder.InitUi();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(44f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.83f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.8f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 80f;
            world = new GameObject("World").transform;
            return scene;
        }

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static void FinishWorld(Scene scene, string path, Transform world, string mapId, string displayName, Vector2 spawn,
            float minX, float maxX, float maxCamY, AdventureMusicStage music)
        {
            var spawnT = new GameObject("SpawnPoint").transform;
            spawnT.position = spawn;
            PlayerMotor2D motor = BuildPlayer(spawn, out CharacterVisual3D visual, out Transform mount);

            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            // Closer framing (Docs/ART_DIRECTION_TOWN.md §5): ~7.3 m visible height → the 1.6 m hero fills ~22%.
            camGo.transform.position = new Vector3(spawn.x, spawn.y + CamOffsetY, CamZ);
            camGo.transform.rotation = Quaternion.Euler(CamPitch, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            var follow = camGo.AddComponent<CameraFollow2D>();
            var fso = new SerializedObject(follow);
            fso.FindProperty("target").objectReferenceValue = motor.transform;
            fso.FindProperty("offset").vector2Value = new Vector2(0f, CamOffsetY);
            fso.FindProperty("smoothing").floatValue = 0.14f;
            fso.FindProperty("useBounds").boolValue = true;
            fso.FindProperty("minBounds").vector2Value = new Vector2(minX + 7f, CamMinY);
            fso.FindProperty("maxBounds").vector2Value = new Vector2(maxX - 7f, maxCamY);
            fso.ApplyModifiedPropertiesWithoutUndo();
            PostProcessing(world);
            Backdrop(world, camGo.transform, (minX + maxX) * 0.5f);

            var audio = new GameObject("AudioDirector").AddComponent<AdventureAudioDirector>();
            AudioLibrary.AssignTo(audio);

            var hud = BuildHud(displayName, motor.GetComponent<PlayerInput2D>(), out Text nameLabel, out Text status, out Button exit, out CanvasGroup fade);
            BuildPortalUi(hud.transform, out GameObject confirm, out Text confirmText, out Button yes, out Button no,
                out GameObject loading, out Text loadingTitle, out Image loadingBar, out Text loadingTip);
            NpcActor elder = Object.FindObjectsByType<NpcActor>(FindObjectsInactive.Include).FirstOrDefault(a => a.name == "NpcElder");
            if (elder)
            {
                BuildNpcDialog(hud.transform, out GameObject dialog, out Text speaker, out Text line, out Button cont);
                var greeter = elder.gameObject.AddComponent<ElderGreeter>();
                var gso = new SerializedObject(greeter);
                gso.FindProperty("elder").objectReferenceValue = elder;
                gso.FindProperty("dialog").objectReferenceValue = dialog;
                gso.FindProperty("speaker").objectReferenceValue = speaker;
                gso.FindProperty("line").objectReferenceValue = line;
                gso.FindProperty("continueButton").objectReferenceValue = cont;
                gso.ApplyModifiedPropertiesWithoutUndo();
            }
            var sfx = hud.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            var session = hud.AddComponent<AdventureWorldSession>();
            var so = new SerializedObject(session);
            so.FindProperty("mapId").stringValue = mapId;
            so.FindProperty("spawnPoint").objectReferenceValue = spawnT;
            so.FindProperty("player").objectReferenceValue = motor;
            so.FindProperty("playerVisual").objectReferenceValue = visual;
            so.FindProperty("visualMount").objectReferenceValue = mount;
            so.FindProperty("characterLabel").objectReferenceValue = nameLabel;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("heroScale").floatValue = HeroScale;
            so.FindProperty("killY").floatValue = -12f;
            so.FindProperty("screenFade").objectReferenceValue = fade;
            so.FindProperty("sfx").objectReferenceValue = sfx;
            so.FindProperty("warpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Portal_Warp.wav");
            so.FindProperty("arriveClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Portal_Arrive.wav");
            so.FindProperty("musicStage").enumValueIndex = (int)music;
            so.FindProperty("confirmPanel").objectReferenceValue = confirm;
            so.FindProperty("confirmText").objectReferenceValue = confirmText;
            so.FindProperty("loadingPanel").objectReferenceValue = loading;
            so.FindProperty("loadingTitle").objectReferenceValue = loadingTitle;
            so.FindProperty("loadingBar").objectReferenceValue = loadingBar;
            so.FindProperty("loadingTip").objectReferenceValue = loadingTip;
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
            UnityEventTools.AddPersistentListener(yes.onClick, session.ConfirmPortal);
            UnityEventTools.AddPersistentListener(no.onClick, session.CancelPortal);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[WORLD] Scene built: " + path);
        }

        // ================================================================== materials
        private static Material Mat(string name, Color color, Color? emission = null)
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
            m.shader = Shader.Find("AdventureKing/Toon");
            m.SetTexture("_BaseMap", WhiteTexture());
            m.SetColor("_BaseColor", color);
            m.SetColor("_ShadeColor", Color.Lerp(color, new Color(0.45f, 0.48f, 0.65f), 0.5f));
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false); // hull outlines split on hard-edged boxes
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        private static Material TexturedMat(string name, Texture2D tex, Vector2 tiling, Color tint)
        {
            Material m = Mat(name, tint);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material CutoutMat(string tex, float haze, float fogAmount = 1f, float cutoff = 0.35f)
        {
            string key = $"cutout_{Path.GetFileNameWithoutExtension(tex)}_{Mathf.RoundToInt(haze * 100)}_{Mathf.RoundToInt(fogAmount * 100)}";
            if (mats.TryGetValue(key, out Material cached)) return cached;
            string path = $"{MatDir}/{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("AdventureKing/Cutout"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("AdventureKing/Cutout");
            string texPath = tex.Contains("/") ? tex : $"{CutoutDir}/{tex}.png";
            m.SetTexture("_BaseMap", ImportPainting(texPath));
            Rect r = AlphaBounds(texPath);
            m.SetTextureScale("_BaseMap", r.size);
            m.SetTextureOffset("_BaseMap", r.position);
            m.SetColor("_HazeColor", Sky);
            m.SetFloat("_Haze", haze);
            m.SetFloat("_FogAmount", fogAmount);
            m.SetFloat("_Cutoff", cutoff);
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }

        private static Texture2D ImportPainting(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) throw new FileNotFoundException(path);
            if (ti.wrapMode != TextureWrapMode.Clamp || !ti.alphaIsTransparency || !ti.mipmapEnabled)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = path.Contains("backdrop") ? 2048 : 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>UV rect of the opaque part of a painting (crops the transparent margins).</summary>
        private static Rect AlphaBounds(string path)
        {
            if (alphaBounds.TryGetValue(path, out Rect r)) return r;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            Color32[] px = t.GetPixels32();
            int w = t.width, h = t.height, x0 = w, y0 = h, x1 = -1, y1 = -1;
            bool hasAlpha = false;
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    byte a = px[y * w + x].a;
                    if (a < 255) hasAlpha = true;
                    if (a < 40) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            Object.DestroyImmediate(t);
            r = !hasAlpha || x1 < 0 ? new Rect(0, 0, 1, 1)
                : Rect.MinMaxRect(Mathf.Max(0, x0 - 2) / (float)w, Mathf.Max(0, y0 - 2) / (float)h,
                    Mathf.Min(w, x1 + 3) / (float)w, Mathf.Min(h, y1 + 3) / (float)h);
            alphaBounds[path] = r;
            return r;
        }

        private static float AspectOf(string texPath)
        {
            Rect r = AlphaBounds(texPath);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            float w = tex ? tex.width : 1024, h = tex ? tex.height : 1024;
            // Use the source pixel size (importer may downscale, ratio is unchanged).
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.GetSourceTextureWidthAndHeight(out int sw, out int sh);
            if (sw > 0) { w = sw; h = sh; }
            return r.width * w / (r.height * h);
        }

        private static Texture2D WhiteTexture() => GeneratedTexture("white", 4, (u, v) => Color.white);

        private static Texture2D GeneratedTexture(string name, int size, System.Func<float, float, Color> f, bool repeat = false)
        {
            string path = $"{WorldDir}/Generated/{name}.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        t.SetPixel(x, y, f((x + 0.5f) / size, (y + 0.5f) / size));
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.alphaIsTransparency = true;
                ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ================================================================== primitives
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
            var go = new GameObject(name) { layer = oneWay ? OneWayLayer : GroundLayer };
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
                // Use the global layer matrix (not the effector's own mask) so a climbing player, who ignores the
                // one-way layer, can pass down through the deck.
                eff.useColliderMask = false;
            }
            return go;
        }

        // ================================================================== terrain + structures
        private static void Ground(Transform art, Transform solids, float minX, float maxX, bool stream)
        {
            Solid(solids, "Ground", minX - 2f, maxX + 2f, 0f, 4f, false);
            Solid(solids, "WallWest", minX - 3f, minX - 1f, 40f, 48f, false);
            Solid(solids, "WallEast", maxX + 1f, maxX + 3f, 40f, 48f, false);

            // Sculpted ground (WorldKit): grass top with a worn flagstone path along the walking line, a grass lip that
            // droops over the front, an earth face with rocks bulging toward the camera — real geometry with vertex
            // colours under the same toon shader and outline as the heroes (no flat textured boxes).
            Material earth = WorldKit.VertexColorMaterial("world_terrain", 0.012f);
            bool tiles = File.Exists($"{PropDir}/ground_block/ground_block.fbx");
            void Bank(string n, float x0, float x1, int seed)
            {
                if (tiles) { GroundTiles(art, x0, x1, seed); return; }
                Mesh mesh = WorldKit.GroundStrip($"ground_{SceneTag}_{n}", x0, x1, seed);
                var go = new GameObject("Ground" + n);
                go.transform.SetParent(art, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = earth;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            if (!stream) Bank("all", minX - 14f, maxX + 14f, 3);
            else
            {
                float sx = TownLayout.Hub.StreamX, hw = TownLayout.Hub.StreamHalfWidth;
                Bank("west", minX - 14f, sx - hw, 5);
                Bank("east", sx + hw, maxX + 14f, 9);
                // Bed sits behind the cascade (its front face used to cover the falling water with a flat brown box).
                Prim(PrimitiveType.Cube, art, "StreamBed", new Vector3(sx, -2.9f, 7.25f), new Vector3(hw * 2f, 4f, 17f), Mat("stream_bed", new Color(0.4f, 0.32f, 0.27f)));
                // Mossy stones cap the two cut banks (the ground core's cross-section) where the stream breaks the strip.
                var srng = new System.Random(612);
                Color stoneA = new Color(0.54f, 0.52f, 0.48f), stoneB = new Color(0.4f, 0.41f, 0.43f), mossy = new Color(0.42f, 0.5f, 0.34f);
                foreach (float side in new[] { -1f, 1f })
                {
                    float bx = sx + side * hw;
                    Mesh stones = WorldKit.Build($"bank_stones_{SceneTag}_{(side < 0 ? "w" : "e")}", (v, c, t) =>
                    {
                        // Rounded river stones set half into the bank, under the grass lip (never over the boardwalk).
                        float y = -2.55f;
                        for (int i = 0; i < 4; i++)
                        {
                            float h = 0.5f + (float)srng.NextDouble() * 0.2f, w = 0.6f + (float)srng.NextDouble() * 0.25f;
                            for (float z = -1.5f; z < 1.6f; z += 0.95f)
                            {
                                Color col = Color.Lerp(stoneA, stoneB, (float)srng.NextDouble());
                                if (i == 3) col = Color.Lerp(col, mossy, 0.55f); // top course is mossy
                                WorldKit.AppendRoundedBox(v, c, t, new Vector3(side * (0.18f + (float)srng.NextDouble() * 0.1f), y + h * 0.5f, z),
                                    new Vector3(w, h, 0.95f), 0.24f, col, Quaternion.Euler((float)srng.NextDouble() * 10f, (float)srng.NextDouble() * 24f - 12f, (float)srng.NextDouble() * 12f - 6f), 3);
                            }
                            y += h * 0.9f;
                        }
                    });
                    MeshObject(art, "BankStones", stones, Structures, new Vector3(bx, 0f, 0f));
                }
            }
            // Grass tufts and flowers on the lip and the meadow behind (3D, vertex-coloured).
            Material vc = WorldKit.VertexColorMaterial("world_props_vc", 0f);
            Mesh[] tufts = { WorldKit.GrassTuft("tuft_a", 1), WorldKit.GrassTuft("tuft_b", 2), WorldKit.GrassTuft("tuft_c", 3) };
            Material[] flower = { Mat("flower_pink", new Color(1f, 0.62f, 0.72f)), Mat("flower_yellow", new Color(1f, 0.86f, 0.4f)), Mat("flower_white", new Color(0.97f, 0.96f, 0.92f)) };
            var rng = new System.Random(811);
            for (float x = minX - 6f; x < maxX + 6f; x += 1.1f + (float)rng.NextDouble() * 1.6f)
            {
                if (stream && Mathf.Abs(x - TownLayout.Hub.StreamX) < TownLayout.Hub.StreamHalfWidth + 0.8f) continue;
                foreach (float z in new[] { -1.25f, 1.2f + (float)rng.NextDouble() * 2.5f })
                {
                    var tg = new GameObject("Tuft");
                    tg.transform.SetParent(art, false);
                    tg.transform.position = new Vector3(x, 0.02f, z);
                    tg.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.6f);
                    tg.AddComponent<MeshFilter>().sharedMesh = tufts[rng.Next(tufts.Length)];
                    var tr = tg.AddComponent<MeshRenderer>();
                    tr.sharedMaterial = vc;
                    tr.shadowCastingMode = ShadowCastingMode.Off;
                    if (rng.NextDouble() < 0.45)
                        for (int k = 0; k < 2; k++)
                            Prim(PrimitiveType.Sphere, art, "Flower", new Vector3(x - 0.15f + k * 0.3f, 0.32f, z - 0.05f), Vector3.one * 0.12f, flower[rng.Next(3)], shadows: false);
                }
            }
        }

        private static void Stream(Transform art, float x, float hw)
        {
            Texture2D water = GeneratedTexture("water_flow", 128, (u, v) =>
            {
                float n = Mathf.PerlinNoise(u * 6f, v * 3f);
                float streak = Mathf.Pow(Mathf.Abs(Mathf.Sin((v * 7f + n * 2.2f) * Mathf.PI)), 18f);
                return Color.Lerp(new Color(0.36f, 0.66f, 0.86f), new Color(0.9f, 0.97f, 1f), streak * 0.85f + n * 0.12f);
            }, true);
            Material wm = TexturedMat("stream_water", water, new Vector2(1f, 5f), Color.white);
            wm.SetColor("_ShadeColor", new Color(0.4f, 0.62f, 0.85f));
            var surface = Prim(PrimitiveType.Quad, art, "StreamWater", new Vector3(x, -0.35f, 10f), new Vector3(hw * 2f, 23f, 1f), wm,
                new Vector3(90f, 0f, 0f), shadows: false);
            surface.AddComponent<UvScroll>().speed = new Vector2(0f, -0.35f);
            // The stream spills over the front earth face as a small cascade.
            var spill = Prim(PrimitiveType.Quad, art, "StreamCascade", new Vector3(x, -2.2f, -1.42f), new Vector3(hw * 2f, 4f, 1f), wm, shadows: false);
            spill.AddComponent<UvScroll>().speed = new Vector2(0f, 1.1f);
            var fall = Prim(PrimitiveType.Quad, art, "Waterfall", new Vector3(x, 2.4f, 21f), new Vector3(hw * 2f, 5.5f, 1f), wm, shadows: false);
            fall.AddComponent<UvScroll>().speed = new Vector2(0f, 0.9f);
            Material rock = Mat("river_rock", new Color(0.6f, 0.6f, 0.62f));
            var rng = new System.Random(77);
            for (int i = 0; i < 11; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f, z = 0.5f + i * 2f;
                Prim(PrimitiveType.Sphere, art, "BankRock", new Vector3(x + side * (hw + 0.1f), -0.15f, z),
                    new Vector3(0.9f + (float)rng.NextDouble() * 0.6f, 0.5f, 0.8f), rock);
            }
            Material lotus = Mat("lotus_leaf", new Color(0.32f, 0.62f, 0.34f));
            Material bloom = Mat("lotus_bloom", new Color(1f, 0.7f, 0.82f), new Color(0.15f, 0.05f, 0.08f));
            foreach (float z in new[] { 3f, 8f, 12f })
            {
                float dx = (z % 3f) * 0.4f - 0.6f;
                Prim(PrimitiveType.Cylinder, art, "LotusLeaf", new Vector3(x + dx, -0.32f, z), new Vector3(0.9f, 0.01f, 0.9f), lotus, shadows: false);
                Prim(PrimitiveType.Sphere, art, "LotusBloom", new Vector3(x + dx + 0.2f, -0.2f, z), new Vector3(0.28f, 0.22f, 0.28f), bloom, shadows: false);
            }
            // Boardwalk across the stream on the walking line (the floor collider is continuous).
            Material wood = Mat("wood", new Color(0.62f, 0.45f, 0.3f));
            Material dark = Mat("wood_dark", new Color(0.4f, 0.28f, 0.2f));
            for (float px = x - hw - 0.6f; px <= x + hw + 0.6f; px += 0.55f)
                Prim(PrimitiveType.Cube, art, "BoardwalkPlank", new Vector3(px, -0.08f, 0f), new Vector3(0.48f, 0.16f, 2.6f), wood);
            foreach (float z in new[] { -1.25f, 1.25f })
                Prim(PrimitiveType.Cube, art, "BoardwalkBeam", new Vector3(x, -0.24f, z), new Vector3(hw * 2f + 1.6f, 0.18f, 0.2f), dark);
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material m, Vector3 pos, bool shadows = true)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return g;
        }

        /// <summary>
        /// Ground made of the Meshy 3D ground block (painted grass lip, earth strata, stones) — the same material
        /// language as every prop. Blocks are scaled to a common size, aligned so the grass surface is the walking line
        /// (y = 0) and the earth face sits just in front of it, overlapped and alternately mirrored to hide repeats.
        /// A second row behind fills the meadow up to the horizon.
        /// </summary>
        private static void GroundTiles(Transform art, float x0, float x1, int seed)
        {
            const string fbx = PropDir + "/ground_block/ground_block.fbx";
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi && (mi.bakeAxisConversion || mi.materialImportMode != ModelImporterMaterialImportMode.None))
            {
                mi.bakeAxisConversion = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            // The Meshy block has rounded end caps, so blocks can never abut cleanly. Use its core instead: both ends
            // clipped off exactly (triangles split on the cut planes) and alternate tiles mirrored, so every joint
            // joins two identical cross-sections (same silhouette, texture and normals).
            Mesh mesh = GroundCore(prefab.GetComponentInChildren<MeshFilter>().sharedMesh);
            Vector3[] vs = mesh.vertices;
            Bounds b = mesh.bounds;
            // Grass surface = 80th percentile of vertex heights (ignores tufts/flowers sticking up).
            float[] ys = vs.Select(v => v.y).OrderBy(y => y).ToArray();
            float surface = ys[(int)(ys.Length * 0.8f)];
            Material m = PropMaterial("ground_block");
            m.SetFloat("_OutlineWidth", 0.012f);
            var rng = new System.Random(seed);
            const float tileLen = 7.5f, faceHeight = 2.6f, depthFront = 6.5f;
            // Seamless strip: cores abut exactly (a 0.2% overlap only guards against float cracks) and every other one
            // is mirrored, so each joint meets a mirror copy of the same cross-section.
            // Fit a whole number of cores exactly between x0 and x1 (banks end precisely at the stream edges).
            int count = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / tileLen));
            float fit = (x1 - x0) / count;
            float step = fit * 0.998f;
            void Row(float zFront, float depth, string tag)
            {
                int index = 0;
                for (float x = x0; index < count; x += fit, index++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, art);
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    go.GetComponentInChildren<MeshFilter>().sharedMesh = mesh;
                    go.name = "GroundBlock_" + tag;
                    bool flip = (index & 1) == 1;
                    float sx = fit / b.size.x, sy = faceHeight / (surface - b.min.y), sz = depth / b.size.z;
                    go.transform.localScale = new Vector3(flip ? -sx : sx, sy, sz);
                    // Props face +Z in model space (like the heroes): turn 180° so the earth face looks at the camera.
                    go.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * prefab.transform.localRotation;
                    go.transform.position = new Vector3(0f, -surface * sy + 0.02f, 0f);
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        r.sharedMaterial = m;
                        r.shadowCastingMode = ShadowCastingMode.On;
                    }
                    // Align with the instance's real world bounds (independent of mirroring/rotation conventions).
                    Bounds wb = go.GetComponentInChildren<Renderer>().bounds;
                    go.transform.position += new Vector3(x + fit * 0.5f - wb.center.x, 0f, zFront - wb.min.z);
                }
            }
            Row(-1.65f, depthFront, "front");
            Row(-1.65f + depthFront - 0.6f, 12f, "back");
        }

        /// <summary>
        /// Middle 76% of the ground block along its length: triangles are clipped exactly on the two cut planes
        /// (new vertices interpolated) and normals on the cuts lose their x part, so mirrored neighbours share the
        /// same cross-section and shading. Saved next to the FBX (regenerated on every build).
        /// </summary>
        private static Mesh GroundCore(Mesh src)
        {
            const string path = PropDir + "/ground_block/ground_block_core.asset";
            Bounds b = src.bounds;
            float half = b.size.x * 0.38f, cx = b.center.x;
            Vector3[] sv = src.vertices; Vector3[] sn = src.normals; Vector2[] su = src.uv;
            int[] st = src.triangles;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
            (Vector3 p, Vector3 n, Vector2 u) V(int i) => (sv[i], sn[i], su[i]);
            (Vector3, Vector3, Vector2) Lerp((Vector3 p, Vector3 n, Vector2 u) a, (Vector3 p, Vector3 n, Vector2 u) c, float k) =>
                (Vector3.Lerp(a.p, c.p, k), Vector3.Lerp(a.n, c.n, k).normalized, Vector2.Lerp(a.u, c.u, k));
            // Clip a polygon against x <= limit (sign=+1) or x >= limit (sign=-1).
            List<(Vector3 p, Vector3 n, Vector2 u)> Clip(List<(Vector3 p, Vector3 n, Vector2 u)> poly, float limit, float sign)
            {
                var o = new List<(Vector3, Vector3, Vector2)>();
                for (int i = 0; i < poly.Count; i++)
                {
                    var a = poly[i]; var c = poly[(i + 1) % poly.Count];
                    float da = (a.p.x - limit) * sign, dc = (c.p.x - limit) * sign; // <= 0 inside
                    if (da <= 0f) o.Add(a);
                    if ((da < 0f && dc > 0f) || (da > 0f && dc < 0f)) o.Add(Lerp(a, c, da / (da - dc)));
                }
                return o;
            }
            for (int i = 0; i < st.Length; i += 3)
            {
                var poly = new List<(Vector3 p, Vector3 n, Vector2 u)> { V(st[i]), V(st[i + 1]), V(st[i + 2]) };
                poly = Clip(poly, cx + half, 1f);
                if (poly.Count >= 3) poly = Clip(poly, cx - half, -1f);
                if (poly.Count < 3) continue;
                int start = v.Count;
                foreach (var p in poly)
                {
                    Vector3 nn = p.n;
                    if (Mathf.Abs(Mathf.Abs(p.p.x - cx) - half) < 1e-4f) { nn.x = 0f; nn = nn.sqrMagnitude > 1e-8f ? nn.normalized : Vector3.up; }
                    v.Add(new Vector3(p.p.x - cx, p.p.y, p.p.z)); n.Add(nn); u.Add(p.u);
                }
                for (int k = 1; k + 1 < poly.Count; k++) { t.Add(start); t.Add(start + k); t.Add(start + k + 1); }
            }
            var core = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!core) { core = new Mesh(); AssetDatabase.CreateAsset(core, path); }
            core.Clear();
            core.name = "ground_block_core";
            core.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            core.SetVertices(v); core.SetNormals(n); core.SetUVs(0, u); core.SetTriangles(t, 0);
            core.RecalculateBounds();
            EditorUtility.SetDirty(core);
            Debug.Log($"[WORLD] ground core: {src.vertexCount} -> {core.vertexCount} vertices, length {b.size.x:0.00} -> {core.bounds.size.x:0.00}");
            return core;
        }

        private static Material Structures => WorldKit.VertexColorMaterial("world_structures", 0.01f);

        private static void Deck(Transform art, Transform solids, TownLayout.Deck d, string style)
        {
            Solid(solids, d.Name, d.XMin, d.XMax, d.Top, 0.4f, true);
            float cx = (d.XMin + d.XMax) * 0.5f, w = d.XMax - d.XMin;
            var root = Group(art, "Deck_" + d.Name);
            // Planks, beams and a curved-tile eave as real rounded geometry (WorldKit), one shared toon material.
            Mesh deck = WorldKit.Deck($"deck_{SceneTag}_{d.Name}", w, 3.4f, style == "deck_high", d.Name.Length * 31 + (int)d.XMin);
            MeshObject(root, "DeckMesh", deck, Structures, new Vector3(cx, d.Top, 0f));
            // Supports stand BEHIND the walking line so nothing blocks the view of the hero.
            int posts = Mathf.Max(2, Mathf.RoundToInt(w / 6f) + 1);
            float floorY = FloorBelow(d), h = d.Top - 0.25f - floorY;
            Mesh post = WorldKit.Post($"post_{Mathf.RoundToInt(h * 10)}", h);
            for (int i = 0; i < posts; i++)
                MeshObject(root, "Post", post, Structures, new Vector3(Mathf.Lerp(d.XMin + 0.4f, d.XMax - 0.4f, i / (posts - 1f)), floorY, 2.45f));
            Color dark = new Color(0.42f, 0.29f, 0.2f);
            Mesh rail = WorldKit.Build($"rail_{SceneTag}_{d.Name}", (v, c, t) =>
            {
                WorldKit.AppendRoundedBox(v, c, t, new Vector3(0f, 0.82f, 2.65f), new Vector3(w, 0.11f, 0.11f), 0.04f, dark, Quaternion.identity, 1);
                for (float x = -w * 0.5f + 0.3f; x <= w * 0.5f; x += 1.4f)
                    WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, 0.42f, 2.65f), new Vector3(0.09f, 0.8f, 0.09f), 0.03f, dark, Quaternion.identity, 1);
            });
            MeshObject(root, "BackRail", rail, Structures, new Vector3(cx, d.Top, 0f));
        }

        /// <summary>Highest floor directly under a deck (another deck or the ground), for post lengths.</summary>
        private static float FloorBelow(TownLayout.Deck d)
        {
            float best = 0f;
            foreach (var arr in new[] { TownLayout.Hub.Decks, TownLayout.Meadow.Decks })
                foreach (var o in arr)
                    if (o.Top < d.Top - 1f && o.XMin < d.XMax && o.XMax > d.XMin && o.Top > best &&
                        System.Array.IndexOf(arr, d) >= 0) best = o.Top;
            return best;
        }

        private static void RopeBridge(Transform art, Transform solids, TownLayout.Deck d)
        {
            Solid(solids, d.Name, d.XMin, d.XMax, d.Top, 0.3f, true);
            Material rope = Mat("rope", new Color(0.78f, 0.64f, 0.42f));
            var root = Group(art, "Bridge_" + d.Name);
            float len = d.XMax - d.XMin;
            var rng = new System.Random(4242);
            Mesh planks = WorldKit.Build($"bridge_{SceneTag}_{d.Name}", (v, c, t) =>
            {
                for (float x = d.XMin + 0.3f; x < d.XMax; x += 0.6f)
                {
                    // gentle 6 cm sag only: the walkway collider is flat, feet must stay on the boards
                    float k = (x - d.XMin) / len, sag = Mathf.Sin(k * Mathf.PI) * 0.06f, j = (float)rng.NextDouble();
                    WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, -0.1f - sag, 1f), new Vector3(0.5f, 0.14f, 2.4f), 0.05f,
                        Color.Lerp(new Color(0.74f, 0.54f, 0.35f), new Color(0.6f, 0.42f, 0.28f), j), Quaternion.Euler(0f, (j - 0.5f) * 4f, (j - 0.5f) * 2f), 1);
                }
            });
            MeshObject(root, "Planks", planks, Structures, new Vector3(0f, d.Top, 0f));
            // Continuous sagging hand ropes, a guide rope along the boards, hangers tying them together and stout end
            // posts - one smooth mesh (the old 1 m cylinder pieces were visibly broken at every joint).
            Color ropeA = new Color(0.8f, 0.66f, 0.44f), ropeB = new Color(0.62f, 0.49f, 0.32f), postCol = new Color(0.42f, 0.29f, 0.2f);
            Mesh ropes = WorldKit.Build($"bridge_ropes_{SceneTag}_{d.Name}", (v, c, t) =>
            {
                float Hand(float x) => 1.02f - Mathf.Sin((x - d.XMin) / len * Mathf.PI) * 0.34f;
                foreach (float z in new[] { -0.2f, 2.2f })
                {
                    var hand = new List<Vector3>(); var guide = new List<Vector3>();
                    for (float x = d.XMin; x <= d.XMax + 0.001f; x += 0.4f)
                    {
                        hand.Add(new Vector3(x, Hand(x), z));
                        guide.Add(new Vector3(x, 0.04f - Mathf.Sin((x - d.XMin) / len * Mathf.PI) * 0.06f, z));
                    }
                    WorldKit.AppendTube(v, c, t, hand, 0.045f, ropeA, 8);
                    WorldKit.AppendTube(v, c, t, guide, 0.035f, ropeB, 7);
                    for (float x = d.XMin + 1f; x < d.XMax - 0.5f; x += 1f)
                        WorldKit.AppendTube(v, c, t, new[] { new Vector3(x, Hand(x), z), new Vector3(x, 0.04f, z) }, 0.022f, ropeB, 6);
                    foreach (float x in new[] { d.XMin, d.XMax })
                    {
                        WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, 0.45f, z), new Vector3(0.22f, 1.3f, 0.22f), 0.07f, postCol, Quaternion.identity, 2);
                        WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, 1.12f, z), new Vector3(0.3f, 0.1f, 0.3f), 0.04f, postCol * 0.8f, Quaternion.identity, 1);
                    }
                }
            });
            MeshObject(root, "Ropes", ropes, Structures, new Vector3(0f, d.Top, 0f));
        }

        private static void ClimbZone(Transform art, Transform play, TownLayout.Climb c)
        {
            var go = new GameObject((c.Rope ? "Rope_" : "Ladder_") + c.Name);
            go.transform.SetParent(play, false);
            go.transform.position = new Vector3(c.X, (c.Bottom + c.Top) * 0.5f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(c.Rope ? 0.9f : 1.1f, c.Top - c.Bottom);
            go.AddComponent<LadderZone2D>();

            float vTop = c.VisualTop, h = vTop - c.Bottom, cy = (c.Bottom + vTop) * 0.5f;
            var v = Group(art, go.name + "_Visual");
            if (c.Rope)
            {
                Material rope = Mat("rope", new Color(0.78f, 0.64f, 0.42f));
                Material leaf = Mat("vine_leaf", new Color(0.36f, 0.64f, 0.3f));
                Prim(PrimitiveType.Cylinder, v, "Rope", new Vector3(c.X, cy, 0.4f), new Vector3(0.11f, h * 0.5f, 0.11f), rope);
                for (float y = c.Bottom + 0.5f; y < vTop - 0.2f; y += 0.55f)
                {
                    Prim(PrimitiveType.Sphere, v, "Knot", new Vector3(c.X, y, 0.4f), new Vector3(0.2f, 0.14f, 0.2f), rope, shadows: false);
                    Prim(PrimitiveType.Sphere, v, "VineLeaf", new Vector3(c.X + ((int)(y * 3) % 2 == 0 ? 0.16f : -0.16f), y + 0.2f, 0.32f),
                        new Vector3(0.24f, 0.12f, 0.08f), leaf, new Vector3(0f, 0f, 30f), shadows: false);
                }
            }
            else
            {
                Color wood = new Color(0.72f, 0.53f, 0.35f), dark = new Color(0.42f, 0.29f, 0.2f);
                Mesh ladder = WorldKit.Build($"ladder_{SceneTag}_{c.Name}", (vv, cc, tt) =>
                {
                    foreach (float dx in new[] { -0.42f, 0.42f })
                        WorldKit.AppendRoundedBox(vv, cc, tt, new Vector3(dx, h * 0.5f, 0f), new Vector3(0.13f, h, 0.13f), 0.05f, dark, Quaternion.identity, 1);
                    // rung spacing shared with the climb clip (ClimbClipBaker): hands land on these exact rungs
                    for (float y = ClimbClipBaker.FirstRung; y < h - 0.1f; y += ClimbClipBaker.RungSpacing)
                        WorldKit.AppendRoundedBox(vv, cc, tt, new Vector3(0f, y, 0f), new Vector3(0.84f, 0.08f, 0.1f), 0.03f, wood, Quaternion.identity, 1);
                });
                MeshObject(v, "Ladder", ladder, Structures, new Vector3(c.X, c.Bottom, 0.4f));
            }
        }

        // ================================================================== 3D props (unified toon look)
        public const float CamZ = -10.5f, CamOffsetY = 1.35f, CamPitch = 5f, CamMinY = 2.1f;

        /// <summary>One post-processing look for every map: soft bloom on lanterns/crystals, gentle grade, light vignette.</summary>
        private static void PostProcessing(Transform world)
        {
            const string path = "Assets/Settings/Rendering/AK_WorldLook.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c)) c = profile.Add<T>(true);
                c.active = true;
                return c;
            }
            var bloom = Get<UnityEngine.Rendering.Universal.Bloom>();
            bloom.threshold.Override(0.86f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.62f);
            var grade = Get<UnityEngine.Rendering.Universal.ColorAdjustments>();
            grade.postExposure.Override(0.08f);
            grade.contrast.Override(8f);
            grade.saturation.Override(10f);
            var vignette = Get<UnityEngine.Rendering.Universal.Vignette>();
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);
            var go = new GameObject("WorldLook");
            go.transform.SetParent(world, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        // ================================================================== NPCs
        private static void PlaceNpc(Transform play, string prefabName, string displayName, float x, float y)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcArtPipeline.PrefabPath(prefabName));
            if (!prefab) { Debug.LogWarning("[WORLD] NPC prefab missing: " + prefabName); return; }
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, play);
            npc.name = prefabName;
            npc.transform.position = new Vector3(x, y, 0.7f); // just behind the walking line: the player passes in front
            npc.transform.localScale = Vector3.one * HeroScale;
            // Name plate above the head (world space, readable, never shaking with the camera).
            var canvasGo = new GameObject("NamePlate", typeof(RectTransform));
            canvasGo.transform.SetParent(npc.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.32f, -0.3f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(300f, 54f);
            rt.localScale = Vector3.one * (0.0062f / HeroScale);
            var bg = EntrySceneBuilder.Panel(canvasGo.transform, "Plate", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.1f, 0.18f, 0.78f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(bg.transform, "Name", displayName, 30, new Color(1f, 0.86f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(290f, 50f), FontStyle.Bold);
        }

        public const string PropDir = WorldDir + "/Town/Props";
        public const float PortalRingY = 3.3f, PortalRingSize = 2.8f;

        /// <summary>Exported prop heights (m) — Tools/blender/export_prop.py normalises each Meshy model to these.</summary>
        public static readonly Dictionary<string, float> PropHeights = new()
        {
            ["teahouse"] = 9f, ["cottage"] = 6.5f, ["forge"] = 6f, ["market_stall"] = 4.2f, ["bell_tower"] = 12f,
            ["windmill"] = 10f, ["banyan_tree"] = 10f, ["bamboo"] = 5.5f, ["stone_lantern"] = 1.8f, ["lantern_post"] = 3.4f,
            ["signboard"] = 2.6f, ["hero_statue"] = 5f, ["stone_bridge"] = 2.6f, ["portal_gate"] = 5.4f, ["crates"] = 1.6f,
            ["planter"] = 1.1f,
        };

        /// <summary>
        /// Places a Meshy 3D prop (FBX exported by Blender at its real height) with the shared toon material and outline.
        /// <paramref name="height"/> rescales relative to the exported height; <paramref name="flip"/> mirrors for variety.
        /// Missing props are skipped (never replaced by flat paintings in the play area).
        /// </summary>
        private static GameObject Prop(Transform parent, string name, float x, float z, float height, bool flip, float y = 0f)
        {
            string fbx = $"{PropDir}/{name}/{name}.fbx";
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (!mi) { Debug.LogWarning("[WORLD] prop missing: " + name); return null; }
            if (mi.bakeAxisConversion || mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.animationType != ModelImporterAnimationType.None)
            {
                mi.bakeAxisConversion = false; // conversion is baked into the mesh by export_prop.py
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.SaveAndReimport();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Prop_" + name;
            float baseH = PropHeights.TryGetValue(name, out float h) ? h : height;
            float s = height / baseH;
            go.transform.position = new Vector3(x, y, z);
            // Face the camera with a slight three-quarter turn so volumes read in the side view.
            float yaw = 180f + (flip ? 14f : -14f);
            // Keep the model's own root orientation (axis conversion) and add the yaw on top — never overwrite it.
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            go.transform.localScale = new Vector3(flip ? -s : s, s, s);
            Material m = PropMaterial(name);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }

        private static Material PropMaterial(string name)
        {
            string key = "prop_" + name;
            if (mats.TryGetValue(key, out Material cached)) return cached;
            string path = $"{MatDir}/{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("AdventureKing/Toon")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AdventureKing/Toon");
            string texPath = $"{PropDir}/{name}/{name}_baseColor.png";
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti && (ti.maxTextureSize != 2048 || ti.textureCompression != TextureImporterCompression.CompressedHQ))
            {
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            m.SetTexture("_BaseMap", tex ? tex : WhiteTexture());
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_ShadeColor", new Color(0.66f, 0.62f, 0.76f));
            m.SetFloat("_UseVertexColor", 0f);
            m.DisableKeyword("_VERTEX_COLOR");
            m.SetFloat("_OutlineWidth", 0.01f);
            m.SetColor("_OutlineColor", WorldKit.OutlineColor);
            m.SetShaderPassEnabled("SRPDefaultUnlit", true);
            m.SetFloat("_RimStrength", 0.2f);
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }

        // ================================================================== painted layers (far backdrop only)
        private static GameObject Cutout(Transform parent, string name, float x, float z, float height, bool flip, float haze, float groundY = 0f)
        {
            string texPath = $"{CutoutDir}/{name}.png";
            Material m = CutoutMat(name, haze);
            float aspect = AspectOf(texPath);
            var q = Prim(PrimitiveType.Quad, parent, "Paint_" + name, new Vector3(x, groundY + height * 0.5f - 0.05f, z),
                new Vector3(height * aspect * (flip ? -1f : 1f), height, 1f), m, shadows: false);
            q.GetComponent<MeshRenderer>().receiveShadows = false;
            // Soft contact shadow so the painting sits on the ground.
            var shadowMat = BlobMat();
            Prim(PrimitiveType.Quad, parent, "ContactShadow_" + name, new Vector3(x, groundY + 0.03f, z - 0.2f),
                new Vector3(height * aspect * 0.85f, Mathf.Min(3.2f, height * 0.35f), 1f), shadowMat, new Vector3(90f, 0f, 0f), shadows: false);
            return q;
        }

        private static Material BlobMat()
        {
            if (mats.TryGetValue("blob_shadow", out Material cached)) return cached;
            string path = $"{MatDir}/blob_shadow.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("AdventureKing/BlobShadow"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", GeneratedTexture("blob", 64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                return new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
            }));
            m.SetFloat("_Strength", 0.5f);
            EditorUtility.SetDirty(m);
            mats["blob_shadow"] = m;
            return m;
        }

        /// <summary>
        /// Lantern prop + mobile-friendly light (no real-time light): an additive glow placed on the part that is
        /// painted as the light (red paper / warm window, found on the model's own texture) and a warm pool on the
        /// ground. LanternGlow fades them with WorldLighting.Night.
        /// </summary>
        private static void Lantern(Transform parent, string name, float x, float z, float height, bool flip)
        {
            GameObject prop = Prop(parent, name, x, z, height, flip);
            if (!prop) return;
            System.Func<Color, bool> lit = name == "lantern_post"
                ? (c => { Color.RGBToHSV(c, out float h, out float s, out float v); return (h < 0.035f || h > 0.96f) && s > 0.55f && v > 0.45f; })
                : (c => { Color.RGBToHSV(c, out float h, out float s, out float v); return h > 0.07f && h < 0.17f && s > 0.35f && v > 0.72f; });
            if (!SignPanelMeasure.TryFindRegion(prop, $"{PropDir}/{name}/{name}_baseColor.png", lit, out Vector3 centre, out Vector2 size))
            {
                Debug.LogWarning($"[WORLD] {name} at x={x}: no light region found on the texture, no glow");
                return;
            }
            Material glow = GlowMat();
            float r = Mathf.Max(size.x, size.y);
            var halo = Prim(PrimitiveType.Quad, prop.transform.parent, "LanternHalo", centre + new Vector3(0f, 0f, -0.25f),
                new Vector3(r * 2.6f, r * 2.6f, 1f), glow, shadows: false);
            float groundY = 0.035f;
            var pool = Prim(PrimitiveType.Quad, prop.transform.parent, "LanternPool", new Vector3(centre.x, groundY, centre.z - 0.6f),
                new Vector3(3.4f, 2.2f, 1f), glow, new Vector3(90f, 0f, 0f), shadows: false);
            var lg = halo.AddComponent<LanternGlow>();
            lg.Bind(halo.GetComponent<Renderer>(), pool.GetComponent<Renderer>());
            Debug.Log($"[WORLD] {name} at x={x}: light region {size.x:0.00}x{size.y:0.00} m at {centre}");
        }

        private static Material GlowMat()
        {
            if (mats.TryGetValue("lantern_glow", out Material cached)) return cached;
            string path = $"{MatDir}/lantern_glow_add.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("AdventureKing/VFX")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AdventureKing/VFX");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/soft_dot.png"));
            m.SetColor("_TintColor", new Color(1f, 0.62f, 0.3f, 1f) * 0.18f); // edit-time default = the faint day glow
            m.SetFloat("_Intensity", 1.6f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            mats["lantern_glow"] = m;
            return m;
        }

        private static void Signboard(Transform parent, float x, float z, string text)
        {
            var board = Prop(parent, "signboard", x, z, 2.6f, false);
            if (!board) return;
            // World-space label laid out inside the board's blank parchment panel, measured from the model
            // itself (SignPanelMeasure) so the text can never spill past the panel edges.
            const float unitsPerMetre = 1f / 0.0036f;
            Vector3 pos = new Vector3(x + 0.05f, 1.38f, z - 0.5f);
            Quaternion rot = Quaternion.Euler(0f, -14f, 0f);
            Vector2 panelM = new Vector2(0.72f, 0.28f);
            if (SignPanelMeasure.TryMeasure(board, $"{PropDir}/signboard/signboard_baseColor.png", out var panel))
            {
                pos = panel.Center; rot = panel.Rotation; panelM = panel.Size;
                Debug.Log($"[WORLD] sign panel '{text.Replace('\n', ' ')}' measured {panel.Size.x:0.00} x {panel.Size.y:0.00} m at {panel.Center}");
            }
            else Debug.LogWarning("[WORLD] sign panel not found on model, using the conservative default box");
            var canvasGo = new GameObject("SignText", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.position = pos;
            canvasGo.transform.rotation = rot;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = panelM * unitsPerMetre;
            rt.localScale = Vector3.one * 0.0036f;
            // Safe margin inside the panel: 12% left/right, 14% top/bottom.
            Vector2 box = new Vector2(rt.sizeDelta.x * 0.76f, rt.sizeDelta.y * 0.72f);
            Text t = EntrySceneBuilder.Label(canvasGo.transform, "Text", text, 30, new Color(0.36f, 0.2f, 0.1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, box, FontStyle.Bold);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 8;
            t.resizeTextMaxSize = 32;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Object.DestroyImmediate(t.GetComponent<Shadow>());
            t.lineSpacing = 0.9f;
        }

        private static void Backdrop(Transform world, Transform cam, float centerX)
        {
            const string tex = WorldDir + "/Town/backdrop_far.png";
            Material m = CutoutMat(tex, 0f, fogAmount: 0f, cutoff: 0f);
            var q = Prim(PrimitiveType.Quad, world, "Backdrop_Painting", new Vector3(0f, 9f, 72f), new Vector3(118f, 66f, 1f), m, shadows: false);
            q.GetComponent<MeshRenderer>().receiveShadows = false;
            q.AddComponent<ParallaxLayer>().Bind(cam, 0.9f, 0.6f);
        }

        // ================================================================== portal (original rune-crystal gate)
        private static void Portal(Transform play, TownLayout.PortalSpot p)
        {
            var root = new GameObject("Portal_" + p.Id);
            root.transform.SetParent(play, false);
            root.transform.position = new Vector3(p.X, 0f, 0f);

            // Glowing rune circle on the ground in front of the painted stone steps.
            Material runeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/rune_add.mat");
            if (runeMat)
            {
                var rune = Prim(PrimitiveType.Quad, root.transform, "GroundRunes", new Vector3(0f, 0.04f, 0.2f), new Vector3(3.2f, 2.0f, 1f), runeMat,
                    new Vector3(90f, 0f, 0f), shadows: false);
                rune.AddComponent<AmbientMotion>().spinDegreesPerSecond = new Vector3(0f, 0f, 14f);
            }

            // 3D gate (Meshy model, toon material): lavender rune ring with crystals on hexagonal steps, just behind
            // the walking line so the hero walks in front of it into the vortex.
            const float gateZ = 1.1f;
            Prop(root.transform, "portal_gate", p.X, gateZ, 5.4f, false);

            // Swirling vortex inside the ring: two counter-rotating spiral discs + core glow.
            Texture2D spiral = GeneratedTexture("portal_spiral", 256, (u, v) =>
            {
                Vector2 d = new Vector2(u - 0.5f, v - 0.5f) * 2f;
                float r = d.magnitude, a = Mathf.Atan2(d.y, d.x);
                float arms = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a * 3f + Mathf.Log(r + 0.02f) * 6.5f), 3f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 3f + 0.2f);
                return new Color(1f, 1f, 1f, arms * fade);
            });
            Material vortexA = VfxMat("portal_vortex_cyan", spiral, new Color(0.35f, 0.95f, 1f) * 1.4f);
            Material vortexB = VfxMat("portal_vortex_violet", spiral, new Color(0.75f, 0.45f, 1f) * 1.3f);
            Texture2D dotTex = GeneratedTexture("portal_core", 128, (u, v) =>
            {
                float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
            });
            Material core = VfxMat("portal_core", dotTex, new Color(0.7f, 0.95f, 1f) * 0.9f);
            float ringY = PortalRingY; // ring centre on the 3D gate (tuned against Play Mode captures)
            var vortexRoot = new GameObject("Vortex").transform;
            vortexRoot.SetParent(root.transform, false);
            vortexRoot.localPosition = new Vector3(0f, ringY, gateZ);
            vortexRoot.localRotation = Quaternion.Euler(0f, -14f, 0f); // same three-quarter angle as the gate
            GameObject Disc(string n, Material m, float size, float dz)
            {
                var q = Prim(PrimitiveType.Quad, vortexRoot, n, new Vector3(0f, 0f, dz), new Vector3(size, size, 1f), m, shadows: false);
                q.transform.localPosition = new Vector3(0f, 0f, dz);
                q.transform.localRotation = Quaternion.identity;
                return q;
            }
            Disc("VortexA", vortexA, PortalRingSize, 0.02f).AddComponent<AmbientMotion>().spinDegreesPerSecond = new Vector3(0f, 0f, -95f);
            Disc("VortexB", vortexB, PortalRingSize * 0.8f, 0f).AddComponent<AmbientMotion>().spinDegreesPerSecond = new Vector3(0f, 0f, 140f);
            var pulse = Disc("Core", core, PortalRingSize * 0.62f, -0.02f).AddComponent<AmbientMotion>();
            pulse.bobHeight = 0.05f;
            pulse.bobSpeed = 2.2f;

            // Motes spiralling into the vortex + rising sparkles.
            Motes(root.transform, new Vector3(0f, ringY, 1.1f));

            // Trigger, interaction prompt, ambient hum.
            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 3.2f);
            col.offset = new Vector2(0f, 1.6f);
            var gate = root.AddComponent<PortalGate2D>();
            var hum = root.AddComponent<AudioSource>();
            hum.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Portal_Hum.wav");
            hum.loop = true;
            hum.playOnAwake = true;
            hum.spatialBlend = 1f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.minDistance = 2f;
            hum.maxDistance = 22f;
            hum.volume = 0.55f;
            GameObject prompt = PortalPrompt(root.transform, ringY + PortalRingSize * 0.5f + 0.7f, out Text label);
            var so = new SerializedObject(gate);
            so.FindProperty("portalId").stringValue = p.Id;
            so.FindProperty("destinationName").stringValue = p.DestinationName;
            so.FindProperty("prompt").objectReferenceValue = prompt;
            so.FindProperty("promptLabel").objectReferenceValue = label;
            so.FindProperty("hum").objectReferenceValue = hum;
            so.FindProperty("vortex").objectReferenceValue = vortexRoot;
            // Leaving the safe town for a field asks first; coming home does not.
            so.FindProperty("requireConfirmation").boolValue = p.ToScene != TownLayout.Hub.Scene;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material VfxMat(string name, Texture2D tex, Color tint)
        {
            if (mats.TryGetValue(name, out Material cached)) return cached;
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("AdventureKing/VFX"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("AdventureKing/VFX");
            m.SetTexture("_MainTex", tex);
            m.SetColor("_TintColor", tint);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_Intensity", 1.4f);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        private static Mesh CrystalMesh()
        {
            string path = WorldDir + "/Generated/crystal.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            // Elongated octahedron with flat-shaded faces (6 points, 8 faces, split vertices).
            Vector3[] p = { new(0, 1, 0), new(0, -1, 0), new(0.5f, 0, 0), new(-0.5f, 0, 0), new(0, 0, 0.5f), new(0, 0, -0.5f) };
            int[][] faces = { new[] { 0, 4, 2 }, new[] { 0, 2, 5 }, new[] { 0, 5, 3 }, new[] { 0, 3, 4 }, new[] { 1, 2, 4 }, new[] { 1, 5, 2 }, new[] { 1, 3, 5 }, new[] { 1, 4, 3 } };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var f in faces)
            {
                int b = verts.Count;
                verts.Add(p[f[0]]); verts.Add(p[f[1]]); verts.Add(p[f[2]]);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "crystal" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void Motes(Transform parent, Vector3 at)
        {
            var dot = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/dot_add.mat");
            var star = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/sparkle_add.mat");
            void Make(string name, Material m, Color a, Color b, float rate, float radius, float orbital, float radial, float upward, float size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = at;
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.duration = 4f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 120;
                var em = ps.emission;
                em.rateOverTime = rate;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius;
                sh.radiusThickness = 0.15f;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = orbital;
                vel.radial = radial;
                vel.y = upward;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = m;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                ps.Play();
            }
            if (dot) Make("SwirlMotes", dot, new Color(0.5f, 1f, 1f), new Color(0.8f, 0.6f, 1f), 36f, 1.6f, 2.4f, -0.9f, 0f, 0.16f);
            if (star) Make("RisingSparkles", star, new Color(0.9f, 1f, 1f), new Color(0.6f, 0.9f, 1f), 10f, 1.9f, 0f, 0f, 0.6f, 0.2f);
        }

        private static GameObject PortalPrompt(Transform parent, float y, out Text label)
        {
            var canvasGo = new GameObject("Prompt", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, y, -0.4f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(520f, 76f);
            rt.localScale = Vector3.one * 0.0085f;
            var bg = EntrySceneBuilder.Panel(canvasGo.transform, "Pill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.1f, 0.18f, 0.82f), EntrySceneBuilder.PanelSprite);
            label = EntrySceneBuilder.Label(bg.transform, "Label", "", 32, new Color(0.85f, 1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(500f, 70f), FontStyle.Bold);
            canvasGo.SetActive(false);
            return canvasGo;
        }

        // ================================================================== player + HUD
        private static PlayerMotor2D BuildPlayer(Vector2 spawn, out CharacterVisual3D visual, out Transform mount)
        {
            var go = new GameObject("Player") { layer = PlayerLayer };
            go.transform.position = spawn;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.8f, 1.6f);
            string pmPath = WorldDir + "/Player_NoFriction.physicsMaterial2D";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(pmPath);
            if (!pm)
            {
                Directory.CreateDirectory(WorldDir);
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
            mso.FindProperty("groundMask").intValue = (1 << GroundLayer) | (1 << OneWayLayer);
            mso.FindProperty("oneWayLayer").intValue = OneWayLayer;
            // Chibi proportions: 4.6 m/s keeps the run cadence readable once Run playback matches ground speed.
            mso.FindProperty("runSpeed").floatValue = RunSpeed;
            mso.FindProperty("climbSpeed").floatValue = ClimbSpeed;
            mso.ApplyModifiedPropertiesWithoutUndo();
            var meso = new SerializedObject(melee);
            meso.FindProperty("attackOrigin").objectReferenceValue = origin;
            meso.FindProperty("targets").intValue = ~((1 << PlayerLayer) | (1 << GroundLayer) | (1 << OneWayLayer));
            meso.ApplyModifiedPropertiesWithoutUndo();

            visual = go.AddComponent<CharacterVisual3D>();
            return motor;
        }

        private static GameObject BuildHud(string mapName, PlayerInput2D playerInput, out Text nameLabel, out Text status, out Button exit, out CanvasGroup fade)
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
            // Everything interactive lives inside the safe area (notches / rounded corners on phones).
            var safe = new GameObject("SafeArea", typeof(RectTransform)) { layer = 5 };
            safe.transform.SetParent(canvasGo.transform, false);
            var srt = (RectTransform)safe.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            safe.AddComponent<SafeAreaFitter>();
            Transform root = safe.transform;

            var plate = EntrySceneBuilder.Panel(root, "CharacterPlate", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(250f, -56f), new Vector2(460f, 72f), ink, EntrySceneBuilder.PanelSprite);
            nameLabel = EntrySceneBuilder.Label(plate.transform, "CharacterName", "", 26, cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 60f), FontStyle.Bold);
            var mapPlate = EntrySceneBuilder.Panel(root, "MapPlate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(380f, 56f), ink, EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(mapPlate.transform, "MapName", mapName, 26, gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 50f), FontStyle.Bold);
            exit = EntrySceneBuilder.Btn(root, "ExitButton", "Lưu & Thoát", new Vector2(-130f, -56f), new Vector2(220f, 60f),
                new Color(0.62f, 0.26f, 0.24f), Color.white, new Vector2(1f, 1f));
            Button gfx = EntrySceneBuilder.Btn(root, "GraphicsButton", "Đồ họa: Cao", new Vector2(-370f, -56f), new Vector2(230f, 60f),
                new Color(0.2f, 0.26f, 0.38f), Color.white, new Vector2(1f, 1f));
            var quality = gfx.gameObject.AddComponent<GraphicsQualityOption>();
            var qso = new SerializedObject(quality);
            qso.FindProperty("label").objectReferenceValue = gfx.GetComponentInChildren<Text>();
            qso.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(gfx.onClick, quality.Cycle);

            var hint = EntrySceneBuilder.Panel(root, "ControlsHint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 44f), new Vector2(1180f, 48f), new Color(0.08f, 0.1f, 0.16f, 0.55f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(hint.transform, "Hint", "A / D di chuyển   ·   Space nhảy   ·   W / S leo thang, dây   ·   W tại cổng: dịch chuyển   ·   J tấn công",
                22, cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160f, 44f));
            hint.AddComponent<TouchControlsVisibility>().hideOnTouch = true; // keyboard hints are meaningless on a phone
            status = EntrySceneBuilder.Label(root, "WorldStatus", "", 24, new Color(1f, 0.7f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1200f, 40f));

            // ---- touch controls (phones / tablets): D-pad left, jump + attack right
            var touch = new GameObject("TouchControls", typeof(RectTransform)) { layer = 5 };
            touch.transform.SetParent(root, false);
            var trt = (RectTransform)touch.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            touch.AddComponent<TouchControlsVisibility>();
            Color pad = new Color(0.1f, 0.12f, 0.2f, 0.6f), padText = new Color(1f, 1f, 1f, 0.92f);
            void TouchBtn(string name, string label, Vector2 anchor, Vector2 pos, float size, PlayerInput2D.TouchAction action)
            {
                Button b = EntrySceneBuilder.Btn(touch.transform, name, label, pos, new Vector2(size, size), pad, padText, anchor);
                GameObject go = b.gameObject;
                Object.DestroyImmediate(b); // press/release handled by TouchButton2D (hold semantics)
                Text lbl = go.GetComponentInChildren<Text>();
                if (lbl)
                {
                    lbl.resizeTextForBestFit = true; // labels shrink to fit round buttons, never overflow
                    lbl.resizeTextMinSize = 16;
                    lbl.resizeTextMaxSize = Mathf.RoundToInt(size * 0.2f);
                    ((RectTransform)lbl.transform).sizeDelta = new Vector2(size * 0.78f, size * 0.5f);
                }
                var tb = go.AddComponent<TouchButton2D>();
                var tso = new SerializedObject(tb);
                tso.FindProperty("target").objectReferenceValue = playerInput;
                tso.FindProperty("action").enumValueIndex = (int)action;
                tso.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildJoystick(touch.transform, playerInput);
            // Right thumb: big attack, jump just inside it, interact above. All ≥ 60 units clear of the screen edge.
            TouchBtn("TouchAttack", "Đánh", new Vector2(1f, 0f), new Vector2(-180f, 180f), 200f, PlayerInput2D.TouchAction.Attack);
            TouchBtn("TouchJump", "Nhảy", new Vector2(1f, 0f), new Vector2(-400f, 130f), 160f, PlayerInput2D.TouchAction.Jump);
            TouchBtn("TouchInteract", "Tương tác", new Vector2(1f, 0f), new Vector2(-180f, 400f), 130f, PlayerInput2D.TouchAction.Interact);

            var fadeGo = EntrySceneBuilder.Panel(canvasGo.transform, "ScreenFade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.04f, 0.08f, 1f), null);
            fade = fadeGo.AddComponent<CanvasGroup>();
            fade.alpha = 1f;          // scenes open black and fade in
            fade.blocksRaycasts = false;
            fade.interactable = false;
            return canvasGo;
        }

        /// <summary>Round joystick, bottom-left: gilded ring, dark glass base, bright knob (one finger, multi-touch safe).</summary>
        private static void BuildJoystick(Transform parent, PlayerInput2D playerInput)
        {
            Sprite circle = EntrySceneBuilder.RoundedSprite("ui_circle", 128, 64);
            Image Disc(Transform p, string name, float size, Color c)
            {
                var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
                go.transform.SetParent(p, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(size, size);
                var img = go.AddComponent<Image>();
                img.sprite = circle;
                img.color = c;
                return img;
            }
            var root = new GameObject("TouchJoystick", typeof(RectTransform)) { layer = 5 };
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(250f, 250f); // 100+ units clear of the safe-area corner
            rt.sizeDelta = new Vector2(340f, 340f);         // generous touch target around the visible ring
            var hit = root.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);          // invisible, but catches the thumb
            Disc(root.transform, "Ring", 290f, new Color(1f, 0.84f, 0.5f, 0.85f)).raycastTarget = false;
            Disc(root.transform, "Base", 274f, new Color(0.08f, 0.1f, 0.18f, 0.62f)).raycastTarget = false;
            Disc(root.transform, "InnerGuide", 120f, new Color(1f, 1f, 1f, 0.08f)).raycastTarget = false;
            Image knobRing = Disc(root.transform, "Knob", 132f, new Color(1f, 0.84f, 0.5f, 0.95f));
            knobRing.raycastTarget = false;
            Disc(knobRing.transform, "KnobFace", 116f, new Color(0.93f, 0.95f, 1f, 0.95f)).raycastTarget = false;
            var stick = root.AddComponent<VirtualJoystick2D>();
            var so = new SerializedObject(stick);
            so.FindProperty("target").objectReferenceValue = playerInput;
            so.FindProperty("knob").objectReferenceValue = knobRing.rectTransform;
            so.FindProperty("radius").floatValue = 100f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>NPC dialog: framed panel along the bottom (never over the hero), speaker banner, message, continue.</summary>
        private static void BuildNpcDialog(Transform hud, out GameObject dialog, out Text speaker, out Text line, out Button cont)
        {
            Color cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.84f, 0.5f);
            // 1240 x 290: the gilded frame band is ~97 units wide on screen, so text lives in the inner
            // 990 x 140 box (≥ 28 units clear of the band) and the button row sits below it, never under the text.
            dialog = EntrySceneBuilder.Panel(hud, "NpcDialog", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f),
                new Vector2(1240f, 320f), new Color(0.08f, 0.1f, 0.18f, 0.95f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Frame(dialog, 3.2f);
            var banner = EntrySceneBuilder.Panel(dialog.transform, "SpeakerBanner", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, 6f),
                new Vector2(280f, 56f), new Color(0.22f, 0.36f, 0.42f, 1f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Rim(banner.transform);
            speaker = EntrySceneBuilder.Label(banner.transform, "Speaker", "", 28, gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 50f), FontStyle.Bold);
            // Measured on the 1280x720 capture: the frame's inner dark area starts ~100 units in from the left/right
            // edges and ~40 from the top/bottom. Text box keeps ≥ 40 more inside that, above the button row.
            line = EntrySceneBuilder.Label(dialog.transform, "Line", "", 26, cream, new Vector2(0.5f, 0.5f), new Vector2(-10f, 30f), new Vector2(940f, 160f));
            line.alignment = TextAnchor.UpperLeft;
            line.supportRichText = true;
            line.lineSpacing = 1.1f;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Truncate;
            line.resizeTextForBestFit = true; // long lines shrink inside the box instead of spilling over the frame
            line.resizeTextMinSize = 20;
            line.resizeTextMaxSize = 26;
            cont = EntrySceneBuilder.Btn(dialog.transform, "Continue", "Cảm ơn ông!", new Vector2(-220f, 86f), new Vector2(230f, 62f),
                new Color(0.26f, 0.56f, 0.62f), Color.white, new Vector2(1f, 0f));
            dialog.SetActive(false);
        }

        private static void BuildPortalUi(Transform hud, out GameObject confirm, out Text confirmText, out Button yes, out Button no,
            out GameObject loading, out Text title, out Image bar, out Text tip)
        {
            Color ink = new Color(0.07f, 0.09f, 0.15f, 0.92f), cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.82f, 0.45f);
            // ---- confirmation dialog
            confirm = EntrySceneBuilder.Panel(hud, "PortalConfirm", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.45f), null);
            var card = EntrySceneBuilder.Panel(confirm.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(780f, 340f), ink, EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Frame(card, 2.8f);
            EntrySceneBuilder.Label(card.transform, "Title", "Cổng Dịch Chuyển", 30, gold, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(680f, 50f), FontStyle.Bold);
            confirmText = EntrySceneBuilder.Label(card.transform, "Question", "", 30, cream, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(660f, 100f));
            confirmText.supportRichText = true;
            yes = EntrySceneBuilder.Btn(card.transform, "Confirm", "Dịch chuyển", new Vector2(-150f, 50f), new Vector2(250f, 64f),
                new Color(0.26f, 0.56f, 0.62f), Color.white, new Vector2(0.5f, 0f));
            no = EntrySceneBuilder.Btn(card.transform, "Cancel", "Ở lại", new Vector2(150f, 50f), new Vector2(250f, 64f),
                new Color(0.36f, 0.36f, 0.42f), Color.white, new Vector2(0.5f, 0f));
            confirm.SetActive(false);

            // ---- loading screen (shown over the fade while the destination scene streams in)
            loading = EntrySceneBuilder.Panel(hud, "Loading", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.04f, 0.08f, 1f), null);
            var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldDir + "/Town/backdrop_far.png");
            if (backdrop)
            {
                var raw = new GameObject("Art", typeof(RectTransform)).AddComponent<RawImage>();
                raw.transform.SetParent(loading.transform, false);
                var rr = (RectTransform)raw.transform;
                rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
                raw.texture = backdrop;
                raw.color = new Color(0.55f, 0.6f, 0.7f, 1f);
                raw.raycastTarget = false;
            }
            title = EntrySceneBuilder.Label(loading.transform, "Destination", "", 56, gold, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 90f), FontStyle.Bold);
            var track = EntrySceneBuilder.Panel(loading.transform, "BarTrack", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f),
                new Vector2(820f, 26f), new Color(0f, 0f, 0f, 0.55f), EntrySceneBuilder.PanelSprite);
            var fill = EntrySceneBuilder.Panel(track.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.45f, 0.95f, 1f), null);
            bar = fill.GetComponent<Image>();
            bar.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png");
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 0f;
            tip = EntrySceneBuilder.Label(loading.transform, "Tip", "", 26, cream, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1300f, 60f));
            loading.SetActive(false);
            loading.transform.SetAsLastSibling();
        }

        private static void EnsureLayer(int index, string name)
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var p = tm.FindProperty("layers").GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = name;
            tm.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}