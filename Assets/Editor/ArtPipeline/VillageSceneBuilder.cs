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
    public static partial class VillageSceneBuilder
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
            BuildGuildHall();
            BuildWeddingHall();
            RegisterBuildScenes();
        }

        /// <summary>Entry first (boot scene), then the world maps.</summary>
        public static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(EntrySceneBuilder.ScenePath, true) };
            foreach (string p in new[] { ScenePath, MeadowScenePath, GuildHallScenePath, WeddingHallScenePath })
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
            Signboard(art, 70f, 2.6f, "Sảnh\nBang Hội\n→");
            Signboard(art, -52.5f, 2.6f, "←\nSảnh\nKết Hôn");

            foreach (var spot in TownLayout.Hub.NpcSpots)
            {
                var t = new GameObject("NpcSpot_" + spot.role).transform;
                t.SetParent(play, false);
                t.position = new Vector3(spot.x, spot.y, 0f);
            }
            foreach (var p in TownLayout.Hub.Portals) Portal(play, p);
            PlaceNpc(play, "NpcElder", "Trưởng Làng", 7f, 0f);
            PlaceNpc(play, "NpcSmith", "Thợ Rèn", -46f, 0f);
            // The storekeeper model now runs the fashion stall at the market (Chợ Phiên), in front of the stall prop.
            PlaceNpc(play, "NpcStorekeeper", "Chủ Tiệm Lụa", -30f, 0f);
            PlaceHealer(play, 20f); // Dược Sư Mộc Linh at her herb counter (between the Elder and the tea house)

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
            FarmSetup(play); // server-authoritative monsters + Gold drops (the hub has none: safe zone)

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
            SetupFashionShop(hud);
            SetupHealer(hud);
            SetupQuestNpcs();
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
        private static GameObject PlaceNpc(Transform play, string prefabName, string displayName, float x, float y)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcArtPipeline.PrefabPath(prefabName));
            if (!prefab) { Debug.LogWarning("[WORLD] NPC prefab missing: " + prefabName); return null; }
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
            canvasGo.AddComponent<CanvasGroup>().blocksRaycasts = false;
            canvasGo.AddComponent<NamePlateFade>(); // fades under the HUD corner clusters
            var rt = (RectTransform)canvasGo.transform;
            float plateW = Mathf.Max(300f, displayName.Length * 17f + 40f); // long names ("Đội Trưởng Liên Minh") stay on one line
            rt.sizeDelta = new Vector2(plateW, 54f);
            rt.localScale = Vector3.one * (0.0062f / HeroScale);
            var bg = EntrySceneBuilder.Panel(canvasGo.transform, "Plate", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.1f, 0.18f, 0.78f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(bg.transform, "Name", displayName, 30, new Color(1f, 0.86f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(plateW - 10f, 50f), FontStyle.Bold);
            return npc;
        }

        /// <summary>World-space label above an NPC (name plate, prompt bubble or quest mark).</summary>
        private static Text WorldLabel(Transform parent, string name, float y, string text, int size, Color textColor, Color? plate, Vector2 box, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, -0.3f);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = order;
            go.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = box;
            rt.localScale = Vector3.one * (0.0062f / Mathf.Max(0.01f, parent.lossyScale.y)); // same world size as the NPC name plates
            Transform host = go.transform;
            if (plate.HasValue)
            {
                var bg = EntrySceneBuilder.Panel(go.transform, "Plate", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, plate.Value, EntrySceneBuilder.PanelSprite);
                host = bg.transform;
            }
            Text t = EntrySceneBuilder.Label(host, "Text", text, size, textColor, new Vector2(0.5f, 0.5f), Vector2.zero, box - new Vector2(10f, 4f), FontStyle.Bold);
            return t;
        }

        /// <summary>
        /// Dược Sư Mộc Linh: built in-engine (no Meshy) on the shared chibi rig (the Mage girl model, dressed at runtime
        /// with the lotus crown + dragonfly wings) behind a herb counter made of props: wooden counter and shelf, red HP and
        /// blue MP bottles, green herb jars and rolled teleport-ticket scrolls. NEEDS a dedicated NPC model later.
        /// </summary>
        private static void PlaceHealer(Transform play, float x)
        {
            var root = new GameObject("NpcHealer").transform;
            root.SetParent(play, false);
            root.position = new Vector3(x, 0f, 0.7f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroArtPipeline.PrefabPath(HeroClass.Mage));
            if (!prefab) { Debug.LogWarning("[WORLD] healer base model missing"); return; }
            var pivot = new GameObject("HealerPivot").transform; // she turns through this pivot (the model keeps its own root)
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
            visual.name = "HealerVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * HeroScale;
            foreach (var fx in visual.GetComponentsInChildren<HeroCombatPresentation>(true)) Object.DestroyImmediate(fx, true); // greetings, not fireballs
            // herb counter (props, to her right; she stands at its end)
            Material wood = Mat("healer_wood", new Color(0.62f, 0.4f, 0.22f)), woodDark = Mat("healer_wood_dark", new Color(0.42f, 0.26f, 0.14f));
            Material red = Mat("healer_red", new Color(0.95f, 0.2f, 0.28f), new Color(0.25f, 0.02f, 0.04f));
            Material blue = Mat("healer_blue", new Color(0.25f, 0.5f, 1f), new Color(0.03f, 0.08f, 0.25f));
            Material herb = Mat("healer_herb", new Color(0.35f, 0.8f, 0.4f)), glass = Mat("healer_glass", new Color(0.85f, 0.95f, 1f));
            Material paper = Mat("healer_paper", new Color(0.98f, 0.92f, 0.75f)), cloth = Mat("healer_cloth", new Color(0.35f, 0.65f, 0.45f));
            var counter = new GameObject("HerbCounter").transform;
            counter.SetParent(root, false);
            counter.localPosition = new Vector3(1.55f, 0f, 0.35f);
            Prim(PrimitiveType.Cube, counter, "Body", new Vector3(0f, 0.48f, 0f), new Vector3(1.9f, 0.96f, 0.72f), wood);
            Prim(PrimitiveType.Cube, counter, "Top", new Vector3(0f, 0.99f, 0f), new Vector3(2.05f, 0.08f, 0.82f), woodDark);
            Prim(PrimitiveType.Cube, counter, "Cloth", new Vector3(0f, 0.62f, -0.37f), new Vector3(1.6f, 0.5f, 0.02f), cloth);
            Prim(PrimitiveType.Cube, counter, "ShelfBack", new Vector3(0f, 1.75f, 0.45f), new Vector3(1.9f, 1.5f, 0.08f), woodDark);
            Prim(PrimitiveType.Cube, counter, "Shelf1", new Vector3(0f, 1.45f, 0.32f), new Vector3(1.9f, 0.06f, 0.28f), wood);
            Prim(PrimitiveType.Cube, counter, "Shelf2", new Vector3(0f, 2.05f, 0.32f), new Vector3(1.9f, 0.06f, 0.28f), wood);
            void Bottle(Vector3 at, Material liquid, float s)
            {
                Prim(PrimitiveType.Sphere, counter, "Bottle", at + new Vector3(0f, 0.11f * s, 0f), Vector3.one * 0.22f * s, liquid);
                Prim(PrimitiveType.Cylinder, counter, "Neck", at + new Vector3(0f, 0.26f * s, 0f), new Vector3(0.07f, 0.06f, 0.07f) * s, glass);
                Prim(PrimitiveType.Cylinder, counter, "Cork", at + new Vector3(0f, 0.33f * s, 0f), new Vector3(0.06f, 0.025f, 0.06f) * s, woodDark);
            }
            float[] xs = { -0.75f, -0.45f, -0.15f, 0.15f, 0.45f, 0.75f };
            for (int i = 0; i < xs.Length; i++) Bottle(new Vector3(xs[i], 1.48f, 0.3f), i % 2 == 0 ? red : blue, 1f);
            for (int i = 0; i < xs.Length; i++) Bottle(new Vector3(xs[i] + 0.1f, 2.08f, 0.3f), i % 3 == 0 ? herb : i % 3 == 1 ? blue : red, 0.85f);
            Bottle(new Vector3(-0.6f, 1.03f, -0.1f), red, 1.3f);
            Bottle(new Vector3(-0.25f, 1.03f, -0.05f), blue, 1.3f);
            for (int i = 0; i < 3; i++) // rolled ticket scrolls with a red band
            {
                var scroll = Prim(PrimitiveType.Cylinder, counter, "TicketScroll", new Vector3(0.35f + i * 0.22f, 1.08f, -0.1f), new Vector3(0.09f, 0.16f, 0.09f), paper, new Vector3(0f, 0f, 90f));
                Prim(PrimitiveType.Cylinder, scroll.transform, "Band", Vector3.zero, new Vector3(1.05f, 0.2f, 1.05f), red);
            }
            // plates: name, talk prompt, quest mark
            WorldLabel(root, "NamePlate", 2.05f, "Dược Sư Mộc Linh", 28, new Color(0.7f, 1f, 0.75f), new Color(0.08f, 0.14f, 0.12f, 0.8f), new Vector2(330f, 54f), 40);
            var prompt = WorldLabel(root, "TalkPrompt", 2.4f, "E · Nói chuyện", 28, Color.white, new Color(0.26f, 0.56f, 0.62f, 0.92f), new Vector2(300f, 54f), 41);
            prompt.transform.parent.parent.gameObject.SetActive(false);
            var mark = WorldLabel(root, "QuestMark", 2.78f, "", 64, new Color(1f, 0.85f, 0.25f), null, new Vector2(80f, 80f), 42);
            root.gameObject.AddComponent<NpcQuestMarker>().Bind("Dược Sư Mộc Linh", mark);
            var healer = root.gameObject.AddComponent<HealerNpc>();
            healer.Bind(pivot, visual.transform, visual.GetComponentInChildren<Animator>(), visual.GetComponentInChildren<HeroFace>(), prompt.transform.parent.parent.gameObject, null);
        }

        private static readonly (string name, string sprite)[] ItemIconMap =
        {
            ("item_potion_hp", "action_potion_hp"), ("item_potion_mp", "action_potion_mp"), ("item_weapon_sword", "item_weapon_sword"), ("item_weapon_staff", "item_weapon_staff"),
            ("item_weapon_dagger", "item_weapon_dagger"), ("item_weapon_bow", "item_weapon_bow"), ("item_armor_vest", "item_armor_vest"), ("item_charm", "item_charm"),
            ("item_ticket", "item_ticket"), ("item_petfood", "item_petfood"), ("item_petfood_feast", "item_petfood_feast"),
        };

        private static void SetIcons(SerializedObject so)
        {
            var p = so.FindProperty("icons");
            p.arraySize = ItemIconMap.Length;
            for (int i = 0; i < ItemIconMap.Length; i++)
            {
                p.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = ItemIconMap[i].name;
                p.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = SkillIconArt.Get(ItemIconMap[i].sprite);
            }
        }

        /// <summary>Healer shop window (HUD canvas) wired to the healer NPC placed in the hub.</summary>
        private static void SetupHealer(GameObject hud)
        {
            var healer = Object.FindAnyObjectByType<HealerNpc>(FindObjectsInactive.Include);
            if (!healer) return;
            var go = new GameObject("HealerShopWindow", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(hud.transform, false);
            var ui = go.AddComponent<HealerShopUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panelSprite").objectReferenceValue = FrameArt.Get("shop_window");
            so.FindProperty("cellSprite").objectReferenceValue = FrameArt.Get("item_cell");
            so.FindProperty("buttonSprite").objectReferenceValue = FrameArt.Get("btn_slate");
            so.FindProperty("coinSprite").objectReferenceValue = HudArt.Get("coin");
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            SetIcons(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            var hso = new SerializedObject(healer);
            hso.FindProperty("shop").objectReferenceValue = ui;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Elder: E to talk any time (quest "Gặp / Báo cáo Trưởng Làng") + "!"/"?" marks; tailor gets a mark too.</summary>
        private static void SetupQuestNpcs()
        {
            var actors = Object.FindObjectsByType<NpcActor>(FindObjectsInactive.Include);
            NpcActor elder = actors.FirstOrDefault(a => a.name == "NpcElder");
            if (elder)
            {
                var prompt = WorldLabel(elder.transform, "TalkPrompt", 1.62f, "E · Nói chuyện", 28, Color.white, new Color(0.26f, 0.56f, 0.62f, 0.92f), new Vector2(300f, 54f), 41);
                prompt.transform.parent.parent.gameObject.SetActive(false);
                var bubble = WorldLabel(elder.transform, "SpeechBubble", 1.95f, "", 24, new Color(0.25f, 0.15f, 0.06f), new Color(1f, 0.96f, 0.86f, 0.95f), new Vector2(560f, 110f), 43);
                bubble.horizontalOverflow = HorizontalWrapMode.Wrap;
                bubble.transform.parent.parent.gameObject.SetActive(false);
                var spot = elder.gameObject.AddComponent<NpcTalkSpot>();
                spot.Bind(elder, "talk:elder", "Trưởng Làng", prompt.transform.parent.parent.gameObject, bubble, new[]
                {
                    "Chào {name}! Đồng Cỏ Gió ở phía đông, qua cổng rune. Nhớ mua thuốc ở chỗ Mộc Linh trước khi đi.",
                    "Gió hôm nay lành lắm. Hạ vài con Slime rồi về kể ta nghe nhé.",
                });
                var mark = WorldLabel(elder.transform, "QuestMark", 2.3f, "", 64, new Color(1f, 0.85f, 0.25f), null, new Vector2(80f, 80f), 42);
                elder.gameObject.AddComponent<NpcQuestMarker>().Bind("Trưởng Làng", mark);
            }
            NpcActor tailor = actors.FirstOrDefault(a => a.name == "NpcStorekeeper");
            if (tailor)
            {
                var mark = WorldLabel(tailor.transform, "QuestMark", 2.3f, "", 64, new Color(1f, 0.85f, 0.25f), null, new Vector2(80f, 80f), 42);
                tailor.gameObject.AddComponent<NpcQuestMarker>().Bind("Chủ Tiệm Lụa", mark);
            }
        }

        /// <summary>
        /// Fashion shop: the market-stall merchant (the existing storekeeper NPC model) gets a talk prompt and the
        /// shop/wardrobe UI. The UI itself is built at runtime from the house UI kit assigned here.
        /// </summary>
        private static void SetupFashionShop(GameObject hud)
        {
            NpcActor npc = Object.FindObjectsByType<NpcActor>(FindObjectsInactive.Include).FirstOrDefault(a => a.name == "NpcStorekeeper");
            if (!npc) return;
            EnsureLayer(30, "Preview3D");
            var shopGo = new GameObject("FashionShop");
            var shop = shopGo.AddComponent<FashionShopUI>();
            var so = new SerializedObject(shop);
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-SemiBold.ttf");
            so.FindProperty("boldFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            so.FindProperty("panelSprite").objectReferenceValue = EntrySceneBuilder.PanelSprite;
            so.FindProperty("enamelSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_enamel.png");
            so.FindProperty("rimSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_gilded_rim.png");
            so.FindProperty("pillSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png");
            so.FindProperty("frameSprite").objectReferenceValue = EntrySceneBuilder.FramePanelSprite;
            // purpose-made skins for the shop (looked up by name at runtime)
            string[] shopSkins = { "shop_window", "shop_tab", "shop_tab_on", "chip", "chip_on", "item_cell", "item_cell_on", "item_cell_set", "stage_bezel",
                "popup_card", "purse_gold", "purse_gem", "parchment", "ribbon",
                "btn_gold", "btn_jade", "btn_royal", "btn_slate", "btn_crimson", "btn_white", "btn_azure", "btn_ghost" };
            var sk = so.FindProperty("skins");
            sk.arraySize = shopSkins.Length;
            for (int i = 0; i < shopSkins.Length; i++) sk.GetArrayElementAtIndex(i).objectReferenceValue = FrameArt.Get(shopSkins[i]);
            so.FindProperty("previewLayer").intValue = 30;
            so.FindProperty("coinIcon").objectReferenceValue = HudArt.Get("coin");
            so.FindProperty("diamondIcon").objectReferenceValue = HudArt.Get("diamond");
            var pi = so.FindProperty("poseIcons");
            string[] poses = { "idle", "run", "jump", "attack" };
            pi.arraySize = poses.Length;
            for (int i = 0; i < poses.Length; i++) pi.GetArrayElementAtIndex(i).objectReferenceValue = HudArt.Get("icon_pose_" + poses[i]);
            var bl = so.FindProperty("backdropLayers");
            string[] layers = { "sky", "hills", "mid", "near" };
            bl.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++) bl.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>($"{LoginBackdropBaker.Dir}/{layers[i]}.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            // "E · Nói chuyện" bubble above the name plate (world space; shown only while the hero is in range)
            var promptGo = new GameObject("TalkPrompt", typeof(RectTransform));
            promptGo.transform.SetParent(npc.transform, false);
            promptGo.transform.localPosition = new Vector3(0f, 1.55f, -0.3f);
            var canvas = promptGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 41;
            var rt = (RectTransform)promptGo.transform;
            rt.sizeDelta = new Vector2(300f, 54f);
            rt.localScale = Vector3.one * (0.0062f / HeroScale);
            var bg = EntrySceneBuilder.Panel(promptGo.transform, "Bubble", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.26f, 0.56f, 0.62f, 0.92f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Rim(bg.transform);
            EntrySceneBuilder.Label(bg.transform, "Text", "E · Nói chuyện", 28, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290f, 50f), FontStyle.Bold);
            promptGo.SetActive(false);

            var merchant = npc.gameObject.AddComponent<FashionMerchant>();
            var mso = new SerializedObject(merchant);
            mso.FindProperty("actor").objectReferenceValue = npc;
            mso.FindProperty("shop").objectReferenceValue = shop;
            mso.FindProperty("worldPrompt").objectReferenceValue = promptGo;
            mso.FindProperty("displayName").stringValue = "Chủ Tiệm Lụa";
            mso.ApplyModifiedPropertiesWithoutUndo();
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
            // Phase D halls (Tools/blender/export_prop.py heights; hero = 1.6 m)
            ["gh_throne"] = 2.8f, ["gh_council_set"] = 1.5f, ["gh_pillar"] = 7f, ["gh_wall_arch"] = 7.5f, ["gh_banner"] = 4.2f,
            ["gh_ranking_board"] = 3.4f, ["gh_donation_altar"] = 3.2f, ["gh_reception_counter"] = 3.2f, ["gh_crystal_brazier"] = 3f,
            ["hall_chandelier"] = 2.4f, ["wh_flower_arch"] = 4.4f, ["wh_altar_stage"] = 3.4f, ["wh_guest_bench"] = 1.3f,
            ["wh_banquet_table"] = 1.4f, ["wh_wedding_cake"] = 2f, ["wh_harp"] = 2.6f, ["wh_marble_pillar"] = 7f, ["wh_wall_window"] = 7.5f,
        };

        /// <summary>
        /// Places a Meshy 3D prop (FBX exported by Blender at its real height) with the shared toon material and outline.
        /// <paramref name="height"/> rescales relative to the exported height; <paramref name="flip"/> mirrors for variety.
        /// Missing props are skipped (never replaced by flat paintings in the play area).
        /// </summary>
        private static GameObject Prop(Transform parent, string name, float x, float z, float height, bool flip, float y = 0f, string dir = null, float yawOffset = 0f)
        {
            dir ??= PropDir;
            string fbx = $"{dir}/{name}/{name}.fbx";
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
            float yaw = 180f + (flip ? 14f : -14f) + yawOffset;
            // Keep the model's own root orientation (axis conversion) and add the yaw on top — never overwrite it.
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            go.transform.localScale = new Vector3(flip ? -s : s, s, s);
            Material m = PropMaterial(name, dir);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }

        private static Material PropMaterial(string name, string dir = null)
        {
            dir ??= PropDir;
            string key = "prop_" + name;
            if (mats.TryGetValue(key, out Material cached)) return cached;
            string path = $"{MatDir}/{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("AdventureKing/Toon")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AdventureKing/Toon");
            string texPath = $"{dir}/{name}/{name}_baseColor.png";
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

            // drifting cloud layer between the painting and the town (depth + life; darkens at night via AK_Cutout)
            Material cloudMat = CloudMat();
            var rngC = new System.Random(7);
            for (int i = 0; i < 6; i++)
            {
                float w = 9f + (float)rngC.NextDouble() * 7f;
                var cloud = Prim(PrimitiveType.Quad, world, "Cloud_" + i,
                    new Vector3(-50f + i * 19f + (float)rngC.NextDouble() * 6f, 10.5f + (float)rngC.NextDouble() * 4f, 58f - i % 2 * 4f),
                    new Vector3(w, w * 0.42f, 1f), cloudMat, shadows: false);
                cloud.GetComponent<MeshRenderer>().receiveShadows = false;
                var pl = cloud.AddComponent<ParallaxLayer>();
                pl.Bind(cam, 0.78f, 0.55f);
                pl.SetDrift(0.18f + (float)rngC.NextDouble() * 0.15f, 120f);
            }

            // night sky: stars + moon (additive, faded in by DayNightCycle), drifting with the painting
            var sky = Prim(PrimitiveType.Quad, world, "NightSky", new Vector3(0f, 9f, 71.5f), new Vector3(118f, 66f, 1f), NightSkyMat(), shadows: false);
            sky.GetComponent<MeshRenderer>().receiveShadows = false;
            sky.AddComponent<ParallaxLayer>().Bind(cam, 0.9f, 0.6f);
            var r = sky.GetComponent<MeshRenderer>();
            r.enabled = false;
            var cycle = world.gameObject.AddComponent<DayNightCycle>();
            cycle.Bind(Object.FindAnyObjectByType<Light>(), cam.GetComponent<Camera>(), r);
        }

        // ================================================================== farm (Đợt 5)
        private static Material FarmMat(string name, Color c, Color emission)
        {
            string path = $"{MatDir}/farm_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find(HeroArtPipeline.ToonShader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(HeroArtPipeline.ToonShader);
            m.SetColor("_BaseColor", c);
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_OutlineWidth", 0.006f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject Blob(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>Original chibi slime built from toon primitives: squishy body, big eyes with glints, blush, a topper.</summary>
        private static FarmMonsterView Slime(Transform parent, string name, Color body, Color topper, bool leaf)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var pivot = new GameObject("Body").transform;
            pivot.SetParent(root.transform, false);
            Material bodyMat = FarmMat(name + "_body", body, Color.black);
            Material ink = FarmMat("ink", new Color(0.08f, 0.07f, 0.1f), Color.black);
            Material white = FarmMat("white", Color.white, new Color(0.3f, 0.3f, 0.3f));
            Material blush = FarmMat("blush", new Color(1f, 0.55f, 0.62f), Color.black);
            var rs = new List<Renderer>();
            rs.Add(Blob(pivot, "Jelly", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(1.1f, 0.84f, 1f), bodyMat).GetComponent<Renderer>());
            foreach (float sx in new[] { -1f, 1f })
            {
                rs.Add(Blob(pivot, "Eye", PrimitiveType.Sphere, new Vector3(0.2f * sx, 0.52f, -0.42f), new Vector3(0.15f, 0.2f, 0.1f), ink).GetComponent<Renderer>());
                Blob(pivot, "Glint", PrimitiveType.Sphere, new Vector3(0.2f * sx + 0.035f, 0.57f, -0.48f), Vector3.one * 0.055f, white);
                Blob(pivot, "Blush", PrimitiveType.Sphere, new Vector3(0.36f * sx, 0.4f, -0.36f), new Vector3(0.14f, 0.07f, 0.04f), blush);
            }
            Material top = FarmMat(name + "_top", topper, Color.black);
            if (leaf) Blob(pivot, "Leaf", PrimitiveType.Sphere, new Vector3(0.08f, 0.9f, 0f), new Vector3(0.12f, 0.32f, 0.05f), top, new Vector3(0f, 0f, -30f));
            else Blob(pivot, "Tuft", PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f), new Vector3(0.22f, 0.2f, 0.22f), top);
            var view = root.AddComponent<FarmMonsterView>();
            view.Bind(pivot, rs.ToArray());
            root.transform.localScale = Vector3.one * 1.15f;
            return view;
        }

        private static GoldDropView CoinPile(Transform parent)
        {
            var root = new GameObject("GoldDropTemplate");
            root.transform.SetParent(parent, false);
            Material gold = FarmMat("coin", new Color(1f, 0.78f, 0.25f), new Color(0.25f, 0.16f, 0.02f));
            Blob(root.transform, "Coin0", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.36f, 0.025f, 0.36f), gold, new Vector3(90f, 0f, 0f));
            Blob(root.transform, "Coin1", PrimitiveType.Cylinder, new Vector3(0.16f, -0.08f, 0.05f), new Vector3(0.3f, 0.022f, 0.3f), gold, new Vector3(80f, 20f, 0f));
            Blob(root.transform, "Coin2", PrimitiveType.Cylinder, new Vector3(-0.14f, -0.1f, 0.03f), new Vector3(0.28f, 0.02f, 0.28f), gold, new Vector3(100f, -15f, 0f));
            Blob(root.transform, "Coin3", PrimitiveType.Cylinder, new Vector3(0.02f, 0.14f, -0.02f), new Vector3(0.26f, 0.02f, 0.26f), gold, new Vector3(70f, 40f, 0f));
            // twinkling sparkles (additive soft glow, faces the camera; animated by GoldDropView)
            string sp = $"{MatDir}/farm_sparkle_add.mat";
            var sm = AssetDatabase.LoadAssetAtPath<Material>(sp);
            if (!sm) { sm = new Material(Shader.Find("AdventureKing/VFX")); AssetDatabase.CreateAsset(sm, sp); }
            sm.shader = Shader.Find("AdventureKing/VFX");
            sm.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/soft_dot.png"));
            sm.SetColor("_TintColor", new Color(1f, 0.92f, 0.55f, 1f));
            sm.SetFloat("_Intensity", 2.2f);
            sm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            EditorUtility.SetDirty(sm);
            Vector3[] spots = { new Vector3(0.22f, 0.18f, -0.15f), new Vector3(-0.2f, 0.05f, -0.15f), new Vector3(0.05f, 0.3f, -0.15f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var s = Blob(root.transform, "Sparkle" + i, PrimitiveType.Quad, spots[i], Vector3.one * (0.22f - i * 0.04f), sm);
                s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.transform.localScale = Vector3.one * 1.7f; // must read at gameplay distance
            return root.AddComponent<GoldDropView>();
        }

        private static void FarmSetup(Transform play)
        {
            var farmGo = new GameObject("FarmField");
            farmGo.transform.SetParent(play, false);
            var farm = farmGo.AddComponent<FarmField>();
            var templates = new GameObject("Templates").transform;
            templates.SetParent(farmGo.transform, false);
            var so = new SerializedObject(farm);
            so.FindProperty("puffTemplate").objectReferenceValue = Slime(templates, "SlimeBong", new Color(1f, 0.84f, 0.88f), Color.white, false);
            so.FindProperty("mossTemplate").objectReferenceValue = Slime(templates, "SlimeReu", new Color(0.5f, 0.8f, 0.42f), new Color(0.25f, 0.6f, 0.25f), true);
            so.FindProperty("coinTemplate").objectReferenceValue = CoinPile(templates);
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            so.FindProperty("panelSprite").objectReferenceValue = FrameArt.Get("popup_card");
            so.FindProperty("buttonSprite").objectReferenceValue = FrameArt.Get("btn_jade");
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform t in templates) t.gameObject.SetActive(false);
        }

        /// <summary>Soft toon cloud (procedural: overlapping puffs, flat-shaded underside, light rim) on the cutout shader.</summary>
        private static Material CloudMat()
        {
            const string texPath = WorldDir + "/Town/cloud_puff.png";
            const int W = 512, H = 216;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            (Vector2 c, float r)[] puffs =
            {
                (new Vector2(120, 80), 62), (new Vector2(205, 110), 84), (new Vector2(300, 100), 74), (new Vector2(380, 78), 56), (new Vector2(250, 70), 70),
            };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float best = 1e9f, lit = 0f;
                    foreach (var (c, r) in puffs)
                    {
                        float d = (new Vector2(x, y) - c).magnitude - r;
                        if (d < best) { best = d; lit = (y - c.y) / r; }
                    }
                    if (y < 52) best = Mathf.Max(best, 52 - y); // flat cloud base
                    float a = Mathf.Clamp01(-best / 2.5f);
                    Color col = lit > 0.15f ? new Color(1f, 1f, 1f) : Color.Lerp(new Color(0.8f, 0.86f, 0.96f), Color.white, Mathf.Clamp01((lit + 0.6f) / 0.75f));
                    col.a = a * 0.92f;
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            File.WriteAllBytes(texPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            return CutoutMat(texPath, 0.12f, fogAmount: 0f, cutoff: 0.05f);
        }

        /// <summary>Original night sky (procedural): stars in the upper sky, thinning towards the horizon, and a moon with a halo.</summary>
        private static Material NightSkyMat()
        {
            const string texPath = WorldDir + "/Town/night_sky.png";
            const int W = 1024, H = 576;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            var rng = new System.Random(20261109);
            Vector2 moon = new Vector2(W * 0.74f, H * 0.8f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H;
                    float d = (new Vector2(x, y) - moon).magnitude;
                    float halo = Mathf.Clamp01(1f - d / 120f);
                    Color c = new Color(0.55f, 0.65f, 1f) * (halo * halo * 0.35f);
                    if (d < 30f) c = Color.Lerp(new Color(1f, 0.97f, 0.85f), new Color(0.9f, 0.88f, 0.8f), Mathf.Clamp01((x - moon.x + y - moon.y) / 40f + 0.5f)) * Mathf.Clamp01(30.5f - d);
                    c.a = 1f;
                    px[y * W + x] = c * Mathf.SmoothStep(0f, 1f, (v - 0.42f) / 0.25f);
                }
            for (int i = 0; i < 520; i++)
            {
                int x = rng.Next(W), y = rng.Next(H);
                float v = y / (float)H;
                if (v < 0.45f || (new Vector2(x, y) - moon).magnitude < 60f) continue;
                float b = (float)rng.NextDouble();
                float fade = Mathf.SmoothStep(0f, 1f, (v - 0.45f) / 0.2f);
                int size = b > 0.93f ? 2 : 1;
                for (int dy = -size; dy <= size; dy++)
                    for (int dx = -size; dx <= size; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        float f = Mathf.Clamp01(1f - (Mathf.Abs(dx) + Mathf.Abs(dy)) / (size + 0.8f));
                        px[yy * W + xx] += new Color(0.85f, 0.9f, 1f) * (f * (0.4f + 0.6f * b) * fade);
                    }
            }
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(texPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();

            string path = $"{MatDir}/night_sky_add.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("AdventureKing/VFX")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AdventureKing/VFX");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_TintColor", Color.white);
            m.SetFloat("_Intensity", 1.2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            EditorUtility.SetDirty(m);
            return m;
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
            GameObject gateProp = Prop(root.transform, "portal_gate", p.X, gateZ, 5.4f, false);
            // The stone steps reach forward through the walking line: give them a real walkable surface measured
            // from the mesh itself (z = 0 slice), so the hero climbs the steps instead of sinking into them.
            PortalStepsInfo steps = MeasurePortal(gateProp, p.X);
            if (steps.Profile != null) PortalSteps(root.transform, steps.Profile);

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
            // centre of the ring opening measured from the mesh (falls back to the tuned constant)
            if (steps.RingFound) ringY = steps.RingCenter.y;
            vortexRoot.localPosition = new Vector3(steps.RingFound ? steps.RingCenter.x : 0f, ringY, steps.RingFound ? steps.RingCenter.z - 0.05f : gateZ);
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
            // trigger stands on the top step, under the ring (not buried in the ground in front of the steps)
            float deck = steps.TopHeight;
            col.size = new Vector2(2.6f, 3.2f);
            col.offset = new Vector2(steps.RingFound ? steps.RingCenter.x : 0f, deck + 1.6f);
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
            // Leaving for a monster field asks first; the town and the halls are safe zones (no question).
            so.FindProperty("requireConfirmation").boolValue = p.ToScene == TownLayout.Meadow.Scene;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct PortalStepsInfo
        {
            public List<Vector2> Profile; // walking-line surface, root-local (x, height)
            public float TopHeight;
            public bool RingFound;
            public Vector3 RingCenter;    // root-local centre of the ring
        }

        /// <summary>Measures the gate model: the step surface where it crosses the walking line (z = 0) and the
        /// centre of the ring above it. Temporary MeshColliders are removed again.</summary>
        private static PortalStepsInfo MeasurePortal(GameObject gate, float rootX)
        {
            var info = new PortalStepsInfo();
            if (!gate) return info;
            var temp = new List<MeshCollider>();
            foreach (MeshFilter mf in gate.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
            Physics.SyncTransforms();
            var heights = new List<Vector2>();
            for (float x = -4f; x <= 4.001f; x += 0.05f)
            {
                float h = 0f;
                foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(rootX + x, 1.6f, 0f), Vector3.down, 2.4f))
                    if (temp.Contains(hit.collider as MeshCollider)) h = Mathf.Max(h, hit.point.y);
                heights.Add(new Vector2(x, h));
            }
            // ring opening: shoot rays along +z (the camera's view) on a grid; cells with no hit but gate on all four
            // sides are the hole — its centroid is where the vortex belongs
            const float cell = 0.05f;
            int nx = Mathf.RoundToInt(6f / cell), ny = Mathf.RoundToInt(6f / cell);
            var solid = new bool[nx, ny];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    Vector3 o = new Vector3(rootX - 3f + i * cell, 0.5f + j * cell, -6f);
                    foreach (RaycastHit hit in Physics.RaycastAll(o, Vector3.forward, 14f))
                        if (temp.Contains(hit.collider as MeshCollider)) { solid[i, j] = true; break; }
                }
            float sx = 0f, sy = 0f; int holes = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (solid[i, j]) continue;
                    bool l = false, r = false, u = false, d = false;
                    for (int k = 0; k < i && !l; k++) l = solid[k, j];
                    for (int k = i + 1; k < nx && !r; k++) r = solid[k, j];
                    for (int k = 0; k < j && !d; k++) d = solid[i, k];
                    for (int k = j + 1; k < ny && !u; k++) u = solid[i, k];
                    if (l && r && u && d) { sx += i; sy += j; holes++; }
                }
            foreach (var mc in temp) Object.DestroyImmediate(mc);
            if (holes > 50)
            {
                info.RingFound = true;
                info.RingCenter = new Vector3(-3f + sx / holes * cell, 0.5f + sy / holes * cell, 1.1f);
            }

            int first = heights.FindIndex(v => v.y > 0.04f), last = heights.FindLastIndex(v => v.y > 0.04f);
            if (first >= 0)
            {
                var prof = new List<Vector2> { new Vector2(heights[first].x, -0.3f) };
                float prev = 0f;
                for (int i = first; i <= last; i++)
                {
                    float h = Mathf.Round(Mathf.Max(0f, heights[i].y) / 0.02f) * 0.02f;
                    if (Mathf.Abs(h - prev) > 0.001f) { prof.Add(new Vector2(heights[i].x, prev)); prof.Add(new Vector2(heights[i].x, h)); prev = h; }
                    info.TopHeight = Mathf.Max(info.TopHeight, h);
                }
                prof.Add(new Vector2(heights[last].x + 0.05f, prev));
                prof.Add(new Vector2(heights[last].x + 0.05f, -0.3f));
                prof[1] = new Vector2(prof[1].x, Mathf.Max(prof[1].y, 0f));
                // a riser taller than the hero's step (0.45) gets a half-way ledge 0.14 m out, so both sides walk up
                // (surface risers only: the first and last segments close the outline into the ground)
                for (int i = 1; i + 2 < prof.Count; i++)
                {
                    Vector2 a = prof[i], b = prof[i + 1];
                    if (Mathf.Abs(a.x - b.x) > 0.001f || Mathf.Abs(b.y - a.y) <= 0.4f) continue;
                    float mid = (a.y + b.y) * 0.5f;
                    if (b.y > a.y) // rising (walking right): ledge on the left, low side
                    {
                        prof[i] = new Vector2(a.x - 0.14f, a.y);
                        prof.Insert(i + 1, new Vector2(a.x - 0.14f, mid));
                        prof.Insert(i + 2, new Vector2(a.x, mid));
                    }
                    else // falling: ledge on the right, low side
                    {
                        prof[i + 1] = new Vector2(a.x + 0.14f, b.y);
                        prof.Insert(i + 1, new Vector2(a.x, mid));
                        prof.Insert(i + 2, new Vector2(a.x + 0.14f, mid));
                    }
                    i += 2;
                }
                info.Profile = prof;
            }

            var risers = new List<string>();
            if (info.Profile != null)
                for (int i = 1; i + 1 < info.Profile.Count; i++)
                    if (Mathf.Abs(info.Profile[i].x - info.Profile[i + 1].x) < 0.001f) risers.Add((info.Profile[i + 1].y - info.Profile[i].y).ToString("F2"));
            Debug.Log($"[PORTAL] steps top={info.TopHeight:F2} span={(first >= 0 ? heights[first].x : 0):F2}..{(last >= 0 ? heights[last].x : 0):F2} risers=[{string.Join(",", risers)}] ring={(info.RingFound ? info.RingCenter.ToString("F2") : "n/a")} holeCells={holes}");
            return info;
        }

        /// <summary>Solid stair surface for the portal steps (ground layer: the hero walks up the risers).</summary>
        private static void PortalSteps(Transform root, List<Vector2> profile)
        {
            var go = new GameObject("PortalSteps") { layer = GroundLayer };
            go.transform.SetParent(root, false);
            var poly = go.AddComponent<PolygonCollider2D>();
            var pts = new List<Vector2>(profile);
            pts.Reverse(); // clockwise outline: bottom-right -> surface -> bottom-left
            poly.points = pts.ToArray();
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
            // real character name above the head (server snapshot); frames only when earned (none yet)
            var plateSo = new SerializedObject(go.AddComponent<PlayerNameplate>());
            plateSo.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            plateSo.FindProperty("plateSprite").objectReferenceValue = FrameArt.Get("chip");
            Sprite[] frameSprites = NameplateFrames();
            var fp = plateSo.FindProperty("frames");
            fp.arraySize = frameSprites.Length;
            for (int i = 0; i < frameSprites.Length; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = frameSprites[i];
            plateSo.ApplyModifiedPropertiesWithoutUndo();

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
            canvas.sortingOrder = 60; // above world-space name plates (40/41) in every render mode
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();
            HudArt.BuildAll();
            HudArt.RenderPortraits();

            Color ink = new Color(0.07f, 0.09f, 0.17f, 0.82f), cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.84f, 0.48f);
            // Everything lives inside the safe area (notches / rounded corners), plus a fixed margin from its edges.
            var safe = new GameObject("SafeArea", typeof(RectTransform)) { layer = 5 };
            safe.transform.SetParent(canvasGo.transform, false);
            var srt = (RectTransform)safe.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            safe.AddComponent<SafeAreaFitter>();
            Transform root = safe.transform;
            const float M = 90f; // margin from the safe-area edges (covers simulated notches too)

            // ---------------------------------------------------------------- player frame (top-left)
            var frame = new GameObject("PlayerFrame", typeof(RectTransform)) { layer = 5 };
            frame.transform.SetParent(root, false);
            var fr = (RectTransform)frame.transform;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 1f);
            fr.anchoredPosition = new Vector2(M, -30f);
            fr.sizeDelta = new Vector2(780f, 150f);
            // avatar: round mask + portrait + gilded ring
            var maskGo = new GameObject("Avatar", typeof(RectTransform)) { layer = 5 };
            maskGo.transform.SetParent(frame.transform, false);
            var mr = (RectTransform)maskGo.transform;
            mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(0f, 0.5f);
            mr.anchoredPosition = Vector2.zero;
            mr.sizeDelta = new Vector2(146f, 146f);
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.sprite = HudArt.Get("avatar_mask");
            maskImg.color = new Color(0.16f, 0.2f, 0.34f, 1f);
            maskGo.AddComponent<Mask>().showMaskGraphic = true;
            var portrait = new GameObject("Portrait", typeof(RectTransform)) { layer = 5 };
            portrait.transform.SetParent(maskGo.transform, false);
            var pr = (RectTransform)portrait.transform;
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            var portraitImg = portrait.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            var ring = EntrySceneBuilder.Img(frame.transform, "AvatarRing", new Vector2(0f, 0.5f), new Vector2(166f, 166f), Color.white, HudArt.Get("avatar_ring"));
            ring.type = Image.Type.Simple;
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchoredPosition = new Vector2(73f, 0f);
            // info plate: name + level, HP, MP
            // One block: [avatar] [navy glass plate: name/Lv, HP, MP] with the wallet strip hanging under the plate.
            var info = EntrySceneBuilder.Panel(frame.transform, "InfoPlate", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f + 230f, -62f), new Vector2(460f, 118f), ink, EntrySceneBuilder.PanelSprite);
            info.GetComponent<Image>().raycastTarget = false;
            if (!EntrySceneBuilder.Skin(info, "hud_plate")) EntrySceneBuilder.Rim(info.transform, 0.8f);
            info.transform.SetAsFirstSibling(); // under the avatar ring
            var infoC = new GameObject("Content", typeof(RectTransform)) { layer = 5 };
            infoC.transform.SetParent(info.transform, false);
            var ic = (RectTransform)infoC.transform;
            ic.anchorMin = Vector2.zero; ic.anchorMax = Vector2.one; ic.offsetMin = new Vector2(44f, 12f); ic.offsetMax = new Vector2(-62f, -12f);
            EntrySceneBuilder.VStack(ic, 6f, TextAnchor.MiddleLeft);
            RectTransform nameRow = EntrySceneBuilder.HRow(ic, "NameRow", 36f, 10f, TextAnchor.MiddleLeft);
            Text heroName = EntrySceneBuilder.BlockLabel(nameRow, "HeroName", "", 26, cream, 36f, FontStyle.Bold, TextAnchor.MiddleLeft);
            EntrySceneBuilder.Size(heroName, 36f, 240f, 1f, 140f);
            Text level = EntrySceneBuilder.BlockLabel(nameRow, "Level", "", 22, gold, 36f, FontStyle.Bold, TextAnchor.MiddleRight);
            EntrySceneBuilder.Size(level, 36f, 90f, 0f, 80f);
            Image Bar(string name, Color fill, out Text value)
            {
                var track = EntrySceneBuilder.Panel(ic, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 28f), new Color(0.02f, 0.03f, 0.07f, 0.85f), EntrySceneBuilder.PanelSprite);
                track.GetComponent<Image>().raycastTarget = false;
                EntrySceneBuilder.Skin(track, "bar_track");
                EntrySceneBuilder.Size(track.transform, 28f);
                var f = EntrySceneBuilder.Panel(track.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fill, null);
                var fi = f.GetComponent<Image>();
                Sprite baked = FrameArt.Get(name == "HpBar" ? "bar_hp" : "bar_mp"); // glossy ruby / sapphire capsule
                fi.sprite = baked ? baked : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png");
                if (baked) fi.color = Color.white;
                fi.type = Image.Type.Filled;
                fi.fillMethod = Image.FillMethod.Horizontal;
                fi.fillAmount = 1f;
                fi.raycastTarget = false;
                var fRt = (RectTransform)f.transform;
                fRt.offsetMin = new Vector2(3f, 3f); fRt.offsetMax = new Vector2(-3f, -3f);
                value = EntrySceneBuilder.FillLabel(track.transform, "Value", "", 17, Color.white, FontStyle.Bold, 10f, 1f, 13);
                value.raycastTarget = false;
                var vo = value.gameObject.AddComponent<Outline>(); // readable over both the fill and the empty groove
                vo.effectColor = new Color(0f, 0f, 0f, 0.6f);
                vo.effectDistance = new Vector2(1f, -1f);
                return fi;
            }
            Image hp = Bar("HpBar", new Color(0.86f, 0.22f, 0.24f), out Text hpText);
            Image mp = Bar("MpBar", new Color(0.24f, 0.5f, 0.95f), out Text mpText);
            // wallet strip attached under the plate: bronze coin purse + crystal diamond purse (different on purpose)
            var purse = new GameObject("Purse", typeof(RectTransform)) { layer = 5 };
            purse.transform.SetParent(frame.transform, false);
            purse.transform.SetSiblingIndex(1);
            var pu = (RectTransform)purse.transform;
            pu.anchorMin = pu.anchorMax = pu.pivot = new Vector2(0f, 1f);
            pu.anchoredPosition = new Vector2(150f, -122f);
            pu.sizeDelta = new Vector2(420f, 48f);
            var puh = purse.AddComponent<HorizontalLayoutGroup>();
            puh.spacing = 10f; puh.childAlignment = TextAnchor.MiddleLeft;
            puh.childControlWidth = puh.childControlHeight = true; puh.childForceExpandWidth = puh.childForceExpandHeight = false;
            Text Pill(string name, string icon, string skin, Color amount)
            {
                var p = EntrySceneBuilder.Panel(pu, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 46f), ink, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png"));
                p.GetComponent<Image>().raycastTarget = false;
                EntrySceneBuilder.Skin(p, skin, 0.72f);
                EntrySceneBuilder.Size(p.transform, 46f, 200f, 0f, 170f);
                var ico = EntrySceneBuilder.Img(p.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(44f, 44f), Color.white, HudArt.Get(icon));
                ico.type = Image.Type.Simple;
                ico.rectTransform.anchoredPosition = new Vector2(22f, 0f);
                var t = EntrySceneBuilder.FillLabel(p.transform, "Amount", "", 22, amount, FontStyle.Bold, 0f, 2f, 14);
                t.rectTransform.offsetMin = new Vector2(52f, 2f);
                t.rectTransform.offsetMax = new Vector2(-20f, -2f);
                t.alignment = TextAnchor.MiddleLeft;
                return t;
            }
            Text goldText = Pill("Gold", "coin", "purse_gold", new Color(1f, 0.88f, 0.58f));
            Text diamondText = Pill("Diamonds", "diamond", "purse_gem", new Color(0.8f, 0.95f, 1f));

            // ---------------------------------------------------------------- top-right: settings, minimap, shortcuts
            var right = new GameObject("TopRight", typeof(RectTransform)) { layer = 5 };
            right.transform.SetParent(root, false);
            var rr = (RectTransform)right.transform;
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(1f, 1f);
            rr.anchoredPosition = new Vector2(-M, -24f);
            rr.sizeDelta = new Vector2(500f, 440f);
            var mini = EntrySceneBuilder.Panel(right.transform, "Minimap", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -84f), new Vector2(380f, 168f), new Color(0.06f, 0.09f, 0.16f, 0.78f), EntrySceneBuilder.PanelSprite);
            mini.GetComponent<Image>().raycastTarget = false;
            if (!EntrySceneBuilder.Skin(mini, "minimap_frame")) EntrySceneBuilder.Rim(mini.transform, 0.95f);
            var area = new GameObject("Area", typeof(RectTransform)) { layer = 5 };
            area.transform.SetParent(mini.transform, false);
            var ar = (RectTransform)area.transform;
            ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = new Vector2(24f, 16f); ar.offsetMax = new Vector2(-24f, -50f);
            // title strip: map name (left) · sun/moon + Vietnam time (right)
            var strip = EntrySceneBuilder.Panel(mini.transform, "TitleStrip", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.35f), null);
            var sr = (RectTransform)strip.transform;
            sr.pivot = new Vector2(0.5f, 1f);
            sr.offsetMin = new Vector2(14f, -46f); sr.offsetMax = new Vector2(-14f, -12f);
            strip.GetComponent<Image>().raycastTarget = false;
            Text mapTitle = EntrySceneBuilder.FillLabel(strip.transform, "MapTitle", mapName, 20, gold, FontStyle.Bold, 10f, 1f, 13);
            mapTitle.alignment = TextAnchor.MiddleLeft;
            mapTitle.rectTransform.offsetMax = new Vector2(-122f, -1f);
            Text clock = EntrySceneBuilder.FillLabel(strip.transform, "Clock", "12:00", 20, cream, FontStyle.Bold, 0f, 1f, 13);
            clock.alignment = TextAnchor.MiddleRight;
            clock.rectTransform.offsetMin = new Vector2(250f, 1f); clock.rectTransform.offsetMax = new Vector2(-10f, -1f);
            var phase = EntrySceneBuilder.Img(strip.transform, "Phase", new Vector2(1f, 0.5f), new Vector2(28f, 28f), Color.white, HudArt.Get("icon_sun"));
            phase.type = Image.Type.Simple;
            phase.preserveAspect = true;
            phase.rectTransform.anchoredPosition = new Vector2(-92f, 0f);
            var mm = mini.AddComponent<HudMinimap>();
            var mmso = new SerializedObject(mm);
            mmso.FindProperty("area").objectReferenceValue = ar;
            mmso.FindProperty("dot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_circle.png");
            mmso.FindProperty("portalIcon").objectReferenceValue = HudArt.Get("mini_portal");
            mmso.FindProperty("npcIcon").objectReferenceValue = HudArt.Get("mini_npc");
            mmso.FindProperty("playerIcon").objectReferenceValue = HudArt.Get("mini_player");
            mmso.FindProperty("sunIcon").objectReferenceValue = HudArt.Get("icon_sun");
            mmso.FindProperty("moonIcon").objectReferenceValue = HudArt.Get("icon_moon");
            mmso.FindProperty("mapTitle").objectReferenceValue = mapTitle;
            mmso.FindProperty("clock").objectReferenceValue = clock;
            mmso.FindProperty("phaseIcon").objectReferenceValue = phase;
            mmso.ApplyModifiedPropertiesWithoutUndo();
            Button gear = IconButton(right.transform, "SettingsButton", "icon_settings", "", new Vector2(1f, 1f), new Vector2(-380f - 52f, -50f), 78f);

            // popup used by every shortcut
            var popupGo = EntrySceneBuilder.Panel(canvasGo.transform, "HudPopup", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.45f), null);
            var popupCard = EntrySceneBuilder.Panel(popupGo.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 450f), ink, EntrySceneBuilder.PanelSprite);
            if (!EntrySceneBuilder.Skin(popupCard, "popup_card")) EntrySceneBuilder.Frame(popupCard, 2.8f);
            RectTransform pc = EntrySceneBuilder.FramedContent(popupCard, 0f, topBand: 0f);
            pc.offsetMin = new Vector2(36f, 24f); pc.offsetMax = new Vector2(-36f, -7f); // title sits in the card's crest band (top 58)
            EntrySceneBuilder.VStack(pc, 14f, TextAnchor.UpperCenter);
            Text popTitle = EntrySceneBuilder.BlockLabel(pc, "PopupTitle", "", 32, gold, 44f, FontStyle.Bold);
            Text popMsg = EntrySceneBuilder.BlockLabel(pc, "PopupMessage", "", 24, cream, 180f);
            RectTransform popRow = EntrySceneBuilder.HRow(pc, "PopupButtons", 64f, 12f);
            Button popClose = EntrySceneBuilder.Btn(popRow, "PopupClose", "Đóng", Vector2.zero, new Vector2(220f, 64f), new Color(0.26f, 0.56f, 0.62f), Color.white);
            EntrySceneBuilder.Size(popClose, 64f, 220f);
            var popup = popupGo.AddComponent<HudPopup>();
            var poso = new SerializedObject(popup);
            poso.FindProperty("titleText").objectReferenceValue = popTitle;
            poso.FindProperty("messageText").objectReferenceValue = popMsg;
            poso.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(popClose.onClick, popup.Hide);
            popupGo.SetActive(false);

            // shortcuts: honest about what is not built yet (no fake notification dots)
            var grid = new GameObject("Shortcuts", typeof(RectTransform)) { layer = 5 };
            grid.transform.SetParent(right.transform, false);
            var gr = (RectTransform)grid.transform;
            gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(1f, 1f);
            gr.anchoredPosition = new Vector2(0f, -180f);
            gr.sizeDelta = new Vector2(500f, 250f);
            var gl = grid.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(92f, 112f);
            gl.spacing = new Vector2(10f, 8f);
            gl.childAlignment = TextAnchor.UpperRight;
            gl.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 5;
            const string soon = " chưa được mở: phần máy chủ của tính năng này đang được phát triển. Khi hoàn thành, phần thưởng thật sẽ hiển thị tại đây.";
            (string icon, string label, string title, string msg, bool vip)[] shortcuts =
            {
                ("icon_event", "Sự kiện", "Sự Kiện", "Sự kiện" + soon, false),
                ("icon_checkin", "Điểm danh", "Điểm Danh 7 Ngày", "Điểm danh 7 ngày" + soon, false),
                ("icon_firsttopup", "Nạp đầu", "Quà Nạp Đầu", "Thanh toán thật chưa được kích hoạt (chờ chủ dự án phê duyệt), nên Quà Nạp Đầu đang khóa. Không có giao dịch nào được thực hiện.", false),
                ("icon_welfare", "Phúc lợi", "Phúc Lợi", "Phúc lợi" + soon, false),
                ("icon_mail", "Thư", "Hộp Thư", "Hộp thư" + soon, false),
                ("icon_giftcode", "Giftcode", "Giftcode", "Nhập giftcode" + soon, false),
                ("icon_vip", "VIP", "VIP", "", true),
                ("icon_ranking", "BXH", "Bảng Xếp Hạng", "Bảng xếp hạng" + soon, false),
                ("icon_social", "Xã hội", "Xã Hội", "", false),
                ("icon_more", "Thêm", "Thêm", "Các tính năng khác (bang hội, bạn bè, thú cưng…) sẽ xuất hiện tại đây khi được hoàn thành.", false),
            };
            var secondaryIcons = new System.Collections.Generic.List<GameObject>();
            HudFeatureButton moreInfo = null;
            Button moreButton = null, checkinButton = null;
            var realWindows = new System.Collections.Generic.Dictionary<string, Button>();
            foreach (var (icon, label, title, msg, vip) in shortcuts)
            {
                Button b = IconButton(grid.transform, "Shortcut_" + icon.Replace("icon_", ""), icon, label, new Vector2(0.5f, 0.5f), Vector2.zero, 82f);
                var fb = b.gameObject.AddComponent<HudFeatureButton>();
                var fso = new SerializedObject(fb);
                fso.FindProperty("title").stringValue = title;
                fso.FindProperty("message").stringValue = msg;
                fso.FindProperty("queryVip").boolValue = vip;
                fso.FindProperty("popup").objectReferenceValue = popup;
                fso.ApplyModifiedPropertiesWithoutUndo();
                if (icon == "icon_more") { moreInfo = fb; moreButton = b; continue; }
                if (icon == "icon_checkin") { checkinButton = b; Object.DestroyImmediate(fb); continue; } // real server check-in panel (below)
                if (icon is "icon_event" or "icon_welfare" or "icon_firsttopup" or "icon_mail" or "icon_giftcode" or "icon_ranking" or "icon_social")
                    { realWindows[icon] = b; Object.DestroyImmediate(fb); } // real server windows (below)
                else UnityEventTools.AddPersistentListener(b.onClick, fb.Open);
                if (icon is "icon_welfare" or "icon_giftcode" or "icon_ranking" or "icon_vip") secondaryIcons.Add(b.gameObject); // phones: behind "Thêm"
            }
            var bar = grid.AddComponent<HudShortcutBar>();
            var bso2 = new SerializedObject(bar);
            var sec = bso2.FindProperty("secondary");
            sec.arraySize = secondaryIcons.Count;
            for (int i = 0; i < secondaryIcons.Count; i++) sec.GetArrayElementAtIndex(i).objectReferenceValue = secondaryIcons[i];
            bso2.FindProperty("moreInfo").objectReferenceValue = moreInfo;
            bso2.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(moreButton.onClick, bar.More);
            // arrow tucked into the icon block's top-left corner, right under the settings gear and beside the minimap
            // (never floating over the map): folds the whole block away / back (PC + mobile); the minimap stays
            Button fold = IconButton(right.transform, "ShortcutsFold", "icon_arrow", "", new Vector2(1f, 1f), new Vector2(-468f, -139f), 68f); // >= 40 px touch target on a 720p phone
            bso2.FindProperty("arrowIcon").objectReferenceValue = fold.transform.Find("Icon");
            bso2.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(fold.onClick, bar.ToggleCollapsed);
            if (checkinButton) BuildCheckin(canvasGo.transform, checkinButton);
            BuildServerWindows(canvasGo.transform, realWindows, canvasGo.GetComponent<CheckinPanel>());

            // system / settings menu (graphics, sound, save & exit)
            var menuGo = EntrySceneBuilder.Panel(canvasGo.transform, "SystemMenu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.45f), null);
            var menuCard = EntrySceneBuilder.Panel(menuGo.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 840f), ink, EntrySceneBuilder.PanelSprite);
            if (!EntrySceneBuilder.Skin(menuCard, "settings_card")) EntrySceneBuilder.Frame(menuCard, 2.8f);
            RectTransform mc = EntrySceneBuilder.FramedContent(menuCard, 0f, topBand: 0f);
            EntrySceneBuilder.VStack(mc, 14f, TextAnchor.MiddleCenter);
            EntrySceneBuilder.BlockLabel(mc, "MenuTitle", "Cài Đặt", 34, gold, 46f, FontStyle.Bold);
            Button gfx = EntrySceneBuilder.Btn(mc, "GraphicsButton", "Đồ họa: Cao", Vector2.zero, new Vector2(400f, 66f), new Color(0.2f, 0.28f, 0.42f), Color.white);
            EntrySceneBuilder.Size(gfx, 66f);
            var quality = gfx.gameObject.AddComponent<GraphicsQualityOption>();
            var qso = new SerializedObject(quality);
            qso.FindProperty("label").objectReferenceValue = gfx.GetComponentInChildren<Text>();
            qso.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(gfx.onClick, quality.Cycle);
            Button sound = EntrySceneBuilder.Btn(mc, "SoundButton", "Âm thanh: Bật", Vector2.zero, new Vector2(400f, 66f), new Color(0.2f, 0.28f, 0.42f), Color.white);
            EntrySceneBuilder.Size(sound, 66f);
            Button musicBtn = EntrySceneBuilder.Btn(mc, "MusicButton", "Nhạc nền: Bật", Vector2.zero, new Vector2(400f, 66f), new Color(0.2f, 0.28f, 0.42f), Color.white);
            EntrySceneBuilder.Size(musicBtn, 66f);
            Button musicVol = EntrySceneBuilder.Btn(mc, "MusicVolumeButton", "Âm lượng nhạc: 75%", Vector2.zero, new Vector2(400f, 66f), new Color(0.2f, 0.28f, 0.42f), Color.white);
            EntrySceneBuilder.Size(musicVol, 66f);
            Button sfxVol = EntrySceneBuilder.Btn(mc, "EffectsVolumeButton", "Âm lượng hiệu ứng: 85%", Vector2.zero, new Vector2(400f, 66f), new Color(0.2f, 0.28f, 0.42f), Color.white);
            EntrySceneBuilder.Size(sfxVol, 66f);
            Button timeBtn = EntrySceneBuilder.Btn(mc, "TimeButton", DayNightCycle.ModeLabel, Vector2.zero, new Vector2(400f, 66f), new Color(0.2f, 0.28f, 0.42f), Color.white);
            EntrySceneBuilder.Size(timeBtn, 66f);
            exit = EntrySceneBuilder.Btn(mc, "ExitButton", "Lưu & Thoát", Vector2.zero, new Vector2(400f, 66f), new Color(0.62f, 0.26f, 0.24f), Color.white);
            EntrySceneBuilder.Size(exit, 66f);
            Button menuClose = EntrySceneBuilder.Btn(mc, "MenuClose", "Đóng", Vector2.zero, new Vector2(400f, 62f), new Color(0.36f, 0.36f, 0.42f), Color.white);
            EntrySceneBuilder.Size(menuClose, 62f);
            var sys = canvasGo.AddComponent<HudSystemMenu>();
            var sso = new SerializedObject(sys);
            sso.FindProperty("panel").objectReferenceValue = menuGo;
            sso.FindProperty("soundLabel").objectReferenceValue = sound.GetComponentInChildren<Text>();
            sso.FindProperty("timeLabel").objectReferenceValue = timeBtn.GetComponentInChildren<Text>();
            sso.FindProperty("musicLabel").objectReferenceValue = musicBtn.GetComponentInChildren<Text>();
            sso.FindProperty("musicVolumeLabel").objectReferenceValue = musicVol.GetComponentInChildren<Text>();
            sso.FindProperty("effectsVolumeLabel").objectReferenceValue = sfxVol.GetComponentInChildren<Text>();
            sso.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(musicBtn.onClick, sys.ToggleMusic);
            UnityEventTools.AddPersistentListener(musicVol.onClick, sys.CycleMusicVolume);
            UnityEventTools.AddPersistentListener(sfxVol.onClick, sys.CycleEffectsVolume);
            UnityEventTools.AddPersistentListener(timeBtn.onClick, sys.CycleTimeOfDay);
            UnityEventTools.AddPersistentListener(gear.onClick, sys.Toggle);
            UnityEventTools.AddPersistentListener(sound.onClick, sys.ToggleSound);
            UnityEventTools.AddPersistentListener(menuClose.onClick, sys.Close);
            UnityEventTools.AddPersistentListener(exit.onClick, sys.Close);
            menuGo.SetActive(false);

            // ---------------------------------------------------------------- map banner (on arrival, fades out)
            var banner = EntrySceneBuilder.Panel(root, "MapBanner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(700f, 110f), Color.white,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Fantasy/frame_bar.png"));
            var bimg = banner.GetComponent<Image>();
            bimg.raycastTarget = false;
            bimg.pixelsPerUnitMultiplier = 1.4f;
            EntrySceneBuilder.Skin(banner, "ribbon", 1.15f); // teal banner with forked ends (not the shared gold bar)
            var bannerGroup = banner.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            Text bannerText = EntrySceneBuilder.FillLabel(banner.transform, "MapName", mapName, 40, gold, FontStyle.Bold, 110f, 14f, 24);
            var bo = bannerText.gameObject.AddComponent<Outline>();
            bo.effectColor = new Color(0.2f, 0.1f, 0.03f, 0.9f);
            bo.effectDistance = new Vector2(1.5f, -1.5f);

            // ---------------------------------------------------------------- bottom: keyboard hint + status
            var hint = EntrySceneBuilder.Panel(root, "ControlsHint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(1120f, 46f), new Color(0.07f, 0.09f, 0.17f, 0.6f), AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png"));
            hint.GetComponent<Image>().raycastTarget = false;
            EntrySceneBuilder.FillLabel(hint.transform, "Hint", "A/D đi · Space nhảy · W/S leo · J đánh · 1/2/3 kỹ năng · S nhặt · Q/R thuốc · I túi đồ · E nói chuyện",
                20, cream, FontStyle.Normal, 30f, 4f, 14);
            hint.AddComponent<TouchControlsVisibility>().hideOnTouch = true; // keyboard hints are meaningless on a phone
            status = EntrySceneBuilder.Label(root, "WorldStatus", "", 24, new Color(1f, 0.74f, 0.64f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1200f, 40f));

            var hud = canvasGo.AddComponent<PlayerHud>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("avatar").objectReferenceValue = portraitImg;
            var ps = hso.FindProperty("portraits");
            string[] classes = { "Swordsman", "Mage", "Assassin", "Archer" };
            ps.arraySize = classes.Length;
            for (int i = 0; i < classes.Length; i++) ps.GetArrayElementAtIndex(i).objectReferenceValue = HudArt.Get("portrait_" + classes[i]);
            hso.FindProperty("nameLabel").objectReferenceValue = heroName;
            hso.FindProperty("levelLabel").objectReferenceValue = level;
            hso.FindProperty("hpFill").objectReferenceValue = hp;
            hso.FindProperty("hpText").objectReferenceValue = hpText;
            hso.FindProperty("mpFill").objectReferenceValue = mp;
            hso.FindProperty("mpText").objectReferenceValue = mpText;
            hso.FindProperty("goldText").objectReferenceValue = goldText;
            hso.FindProperty("diamondText").objectReferenceValue = diamondText;
            hso.FindProperty("keyboardHint").objectReferenceValue = hint;
            hso.FindProperty("mapBanner").objectReferenceValue = bannerGroup;
            hso.FindProperty("mapBannerText").objectReferenceValue = bannerText;
            hso.FindProperty("mapDisplayName").stringValue = mapName;
            hso.ApplyModifiedPropertiesWithoutUndo();
            nameLabel = null; // the player frame shows name / level itself

            // ---- touch controls (phones / tablets): joystick left, attack / jump / interact right
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
                var tb = go.AddComponent<TouchButton2D>();
                var tso = new SerializedObject(tb);
                tso.FindProperty("target").objectReferenceValue = playerInput;
                tso.FindProperty("action").enumValueIndex = (int)action;
                tso.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildJoystick(touch.transform, playerInput);
            // (attack lives in the combat cluster below, on PC and touch alike)
            TouchBtn("TouchJump", "Nhảy", new Vector2(1f, 0f), new Vector2(-830f, 130f), 150f, PlayerInput2D.TouchAction.Jump);
            TouchBtn("TouchInteract", "Tương tác", new Vector2(1f, 0f), new Vector2(-830f, 310f), 120f, PlayerInput2D.TouchAction.Interact);

            // ---- combat cluster (PC + touch): attack, 3 class skills, Auto, pickup, potions, bag  + the bag window
            var invGo = new GameObject("InventoryWindow", typeof(RectTransform)) { layer = 5 };
            invGo.transform.SetParent(canvasGo.transform, false);
            var invPanel = invGo.AddComponent<InventoryPanel>();
            var ipso = new SerializedObject(invPanel);
            ipso.FindProperty("panelSprite").objectReferenceValue = FrameArt.Get("shop_window");
            ipso.FindProperty("cellSprite").objectReferenceValue = FrameArt.Get("item_cell");
            ipso.FindProperty("buttonSprite").objectReferenceValue = FrameArt.Get("btn_slate");
            ipso.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            SetIcons(ipso);
            ipso.ApplyModifiedPropertiesWithoutUndo();

            // quest tracker (left, under the player frame)
            var questGo = new GameObject("QuestTracker", typeof(RectTransform)) { layer = 5 };
            questGo.transform.SetParent(root, false);
            var qp = questGo.AddComponent<QuestPanel>();
            var questSo = new SerializedObject(qp);
            questSo.FindProperty("panelSprite").objectReferenceValue = EntrySceneBuilder.PanelSprite;
            questSo.FindProperty("buttonSprite").objectReferenceValue = FrameArt.Get("btn_slate");
            questSo.FindProperty("arrowSprite").objectReferenceValue = HudArt.Get("icon_arrow");
            questSo.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            questSo.ApplyModifiedPropertiesWithoutUndo();

            var barGo = new GameObject("ActionBar", typeof(RectTransform)) { layer = 5 };
            barGo.transform.SetParent(root, false);
            var actionBar = barGo.AddComponent<HeroActionBar>();
            var abso = new SerializedObject(actionBar);
            var si = abso.FindProperty("skillIcons");
            si.arraySize = 12;
            for (int c = 0; c < 4; c++)
                for (int s = 0; s < 3; s++)
                    si.GetArrayElementAtIndex(c * 3 + s).objectReferenceValue = SkillIconArt.Get(SkillIconArt.SkillIcons[c, s]);
            abso.FindProperty("attackIcon").objectReferenceValue = SkillIconArt.Get("action_attack");
            abso.FindProperty("autoIcon").objectReferenceValue = SkillIconArt.Get("action_auto");
            abso.FindProperty("pickupIcon").objectReferenceValue = SkillIconArt.Get("action_pickup");
            abso.FindProperty("hpIcon").objectReferenceValue = SkillIconArt.Get("action_potion_hp");
            abso.FindProperty("mpIcon").objectReferenceValue = SkillIconArt.Get("action_potion_mp");
            abso.FindProperty("bagIcon").objectReferenceValue = SkillIconArt.Get("action_bag");
            abso.FindProperty("frame").objectReferenceValue = SkillIconArt.Get("frame_skill");
            abso.FindProperty("frameUltimate").objectReferenceValue = SkillIconArt.Get("frame_skill_ultimate");
            abso.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            abso.FindProperty("inventory").objectReferenceValue = invPanel;
            // pets: the director (pet in the world) + the window
            var petDirGo = new GameObject("PetDirector") { layer = 5 };
            petDirGo.transform.SetParent(canvasGo.transform, false);
            var petDir = petDirGo.AddComponent<PetDirector>();
            var pdso = new SerializedObject(petDir);
            var pp = pdso.FindProperty("pets");
            pp.arraySize = PetArt.Ids.Length;
            for (int i = 0; i < PetArt.Ids.Length; i++)
            {
                pp.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = PetArt.Ids[i];
                pp.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue = PetArt.Prefab(PetArt.Ids[i]);
                pp.GetArrayElementAtIndex(i).FindPropertyRelative("portrait").objectReferenceValue = PetArt.Portrait(PetArt.Ids[i]);
            }
            pdso.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            pdso.ApplyModifiedPropertiesWithoutUndo();
            var petWinGo = new GameObject("PetWindow", typeof(RectTransform)) { layer = 5 };
            petWinGo.transform.SetParent(canvasGo.transform, false);
            var petPanel = petWinGo.AddComponent<PetPanel>();
            var ppso = new SerializedObject(petPanel);
            ppso.FindProperty("panelSprite").objectReferenceValue = FrameArt.Get("shop_window");
            ppso.FindProperty("cellSprite").objectReferenceValue = FrameArt.Get("item_cell");
            ppso.FindProperty("buttonSprite").objectReferenceValue = FrameArt.Get("btn_slate");
            ppso.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            ppso.FindProperty("bag").objectReferenceValue = invPanel;
            ppso.ApplyModifiedPropertiesWithoutUndo();
            abso.FindProperty("petPanel").objectReferenceValue = petPanel;
            abso.FindProperty("petIcon").objectReferenceValue = PetArt.Portrait("pet_cloud_cat");
            abso.ApplyModifiedPropertiesWithoutUndo();

            popupGo.transform.SetAsLastSibling();
            menuGo.transform.SetAsLastSibling();
            var fadeGo = EntrySceneBuilder.Panel(canvasGo.transform, "ScreenFade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.04f, 0.08f, 1f), null);
            fade = fadeGo.AddComponent<CanvasGroup>();
            fade.alpha = 1f;          // scenes open black and fade in
            fade.blocksRaycasts = false;
            fade.interactable = false;
            return canvasGo;
        }

        /// <summary>Round medallion icon button (procedural HUD art) with an optional caption underneath.</summary>
        /// <summary>"Điểm Danh 7 Ngày" popup (reference: AdventureKing_Checkin_Events_VisualReference.png): 3×2 day cells +
        /// a large day-7 milestone with outfit choices, Claim / Close, a red dot on the HUD icon (server-claimable only).</summary>
        /// <summary>Sự kiện / Phúc lợi / Nạp đầu / Thư / Giftcode / BXH icons open real server windows (built in code at runtime).</summary>
        private static void BuildServerWindows(Transform canvas, System.Collections.Generic.Dictionary<string, Button> buttons, CheckinPanel checkin)
        {
            Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_circle.png");
            T Window<T>(string name) where T : CodePanel
            {
                var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
                go.transform.SetParent(canvas, false);
                var p = go.AddComponent<T>();
                var so = new SerializedObject(p);
                so.FindProperty("panelSprite").objectReferenceValue = FrameArt.Get("shop_window");
                so.FindProperty("cellSprite").objectReferenceValue = FrameArt.Get("item_cell");
                so.FindProperty("buttonSprite").objectReferenceValue = FrameArt.Get("btn_slate");
                so.FindProperty("font").objectReferenceValue = bold;
                so.FindProperty("coinSprite").objectReferenceValue = HudArt.Get("coin");
                so.ApplyModifiedPropertiesWithoutUndo();
                return p;
            }
            GameObject Dot(Button b)
            {
                var dot = EntrySceneBuilder.Img(b.transform, "RedDot", new Vector2(1f, 1f), new Vector2(26f, 26f), new Color(0.92f, 0.18f, 0.2f), circle);
                dot.type = Image.Type.Simple;
                dot.rectTransform.anchoredPosition = new Vector2(-16f, -10f);
                dot.gameObject.SetActive(false);
                return dot.gameObject;
            }
            var welfare = Window<WelfarePanel>("WelfareWindow");
            var wso = new SerializedObject(welfare);
            wso.FindProperty("checkin").objectReferenceValue = checkin;
            var dots = new System.Collections.Generic.List<GameObject>();
            foreach (string k in new[] { "icon_event", "icon_welfare" }) if (buttons.TryGetValue(k, out Button b)) dots.Add(Dot(b));
            var dp = wso.FindProperty("dots");
            dp.arraySize = dots.Count;
            for (int i = 0; i < dots.Count; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
            wso.ApplyModifiedPropertiesWithoutUndo();
            if (buttons.TryGetValue("icon_event", out Button ev)) UnityEventTools.AddStringPersistentListener(ev.onClick, welfare.OpenTab, "event");
            if (buttons.TryGetValue("icon_welfare", out Button wf)) UnityEventTools.AddStringPersistentListener(wf.onClick, welfare.OpenTab, "online");
            if (buttons.TryGetValue("icon_firsttopup", out Button ft)) UnityEventTools.AddStringPersistentListener(ft.onClick, welfare.OpenTab, "firsttopup");

            var mail = Window<MailPanel>("MailWindow");
            if (buttons.TryGetValue("icon_mail", out Button mb))
            {
                var mso = new SerializedObject(mail);
                mso.FindProperty("dot").objectReferenceValue = Dot(mb);
                mso.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddPersistentListener(mb.onClick, mail.Open);
            }
            var gift = Window<GiftcodePanel>("GiftcodeWindow");
            if (buttons.TryGetValue("icon_giftcode", out Button gb)) UnityEventTools.AddPersistentListener(gb.onClick, gift.Open);
            var rank = Window<RankingPanel>("RankingWindow");
            if (buttons.TryGetValue("icon_ranking", out Button rb)) UnityEventTools.AddPersistentListener(rb.onClick, rank.Open);

            // multiplayer heartbeat + social window (chat ticker, friends, guild, marriage)
            var sync = canvas.gameObject.AddComponent<WorldSync>();
            var syso = new SerializedObject(sync);
            syso.FindProperty("font").objectReferenceValue = bold;
            syso.ApplyModifiedPropertiesWithoutUndo();
            var social = Window<SocialPanel>("SocialWindow");
            if (buttons.TryGetValue("icon_social", out Button sb))
            {
                var sso = new SerializedObject(social);
                sso.FindProperty("dot").objectReferenceValue = Dot(sb);
                sso.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddStringPersistentListener(sb.onClick, social.OpenTab, "chat");
            }
        }

        /// <summary>Owner-provided nameplate frames (Assets/Art/Nameplates), imported as sprites; name = server title id.</summary>
        public static Sprite[] NameplateFrames()
        {
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Nameplates" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti.textureType != TextureImporterType.Sprite || ti.alphaIsTransparency == false || ti.mipmapEnabled)
                {
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s) list.Add(s);
            }
            return list.ToArray();
        }

        private static void BuildCheckin(Transform canvas, Button opener)
        {
            Color cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.84f, 0.48f), muted = new Color(0.78f, 0.82f, 0.92f);
            var modal = EntrySceneBuilder.Panel(canvas, "CheckinPopup", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.5f), null);
            var card = EntrySceneBuilder.Panel(modal.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 760f), new Color(0.07f, 0.09f, 0.15f, 0.95f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Skin(card, "popup_card");
            Text title = EntrySceneBuilder.Label(card.transform, "CheckinTitle", "Điểm Danh 7 Ngày", 36, gold, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 50f), FontStyle.Bold);
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0.25f, 0.12f, 0.03f, 0.9f);
            Text status = EntrySceneBuilder.Label(card.transform, "CheckinStatus", "", 22, cream, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1060f, 40f));
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 15; status.resizeTextMaxSize = 22;

            var cells = new CheckinPanel.Cell[7];
            CheckinPanel.Cell MakeCell(int day, Vector2 pos, Vector2 size, float iconSize)
            {
                var frame = EntrySceneBuilder.Panel(card.transform, "Day" + day, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size, Color.white, EntrySceneBuilder.PanelSprite);
                EntrySceneBuilder.Skin(frame, "hall_card");
                frame.GetComponent<Image>().raycastTarget = false;
                var c = new CheckinPanel.Cell { frame = frame.GetComponent<Image>() };
                c.dayLabel = EntrySceneBuilder.Label(frame.transform, "DayLabel", "Ngày " + day, 22, gold, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(size.x - 20f, 32f), FontStyle.Bold);
                c.icon = EntrySceneBuilder.Img(frame.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(iconSize, iconSize), Color.white, HudArt.Get("coin"));
                c.icon.type = Image.Type.Simple; c.icon.preserveAspect = true;
                c.icon.rectTransform.anchoredPosition = new Vector2(0f, -44f - iconSize * 0.5f);
                c.rewardLabel = EntrySceneBuilder.Label(frame.transform, "Reward", "", 18, cream, new Vector2(0.5f, 1f), new Vector2(0f, -54f - iconSize - 18f), new Vector2(size.x - 16f, 44f));
                c.rewardLabel.resizeTextForBestFit = true; c.rewardLabel.resizeTextMinSize = 12; c.rewardLabel.resizeTextMaxSize = 18;
                c.stateLabel = EntrySceneBuilder.Label(frame.transform, "State", "", 16, muted, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(size.x - 16f, 26f));
                c.done = EntrySceneBuilder.Img(frame.transform, "Done", new Vector2(1f, 1f), new Vector2(44f, 44f), Color.white, HudArt.Get("reward_done"));
                c.done.type = Image.Type.Simple;
                c.done.rectTransform.anchoredPosition = new Vector2(-30f, -30f);
                c.done.gameObject.SetActive(false);
                return c;
            }
            // card 1180×760: status band ends at -112; cell rows -132..-368 and -382..-618; buttons -672..-744
            for (int i = 0; i < 6; i++)
                cells[i] = MakeCell(i + 1, new Vector2(180f + (i % 3) * 252f, -250f - (i / 3) * 250f), new Vector2(236f, 236f), 92f);
            cells[6] = MakeCell(7, new Vector2(990f, -375f), new Vector2(340f, 486f), 150f);
            EntrySceneBuilder.Skin(cells[6].frame.gameObject, "hall_card_on");
            var d7 = cells[6].frame.transform;
            var choices = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                choices[i] = EntrySceneBuilder.Btn(d7, "Choice" + i, "Lựa chọn", new Vector2(0f, 190f - i * 66f), new Vector2(300f, 62f), new Color(0.48f, 0.3f, 0.7f), cream, new Vector2(0.5f, 0f)); // 62 units: >= 40 px at 720p
                choices[i].GetComponentInChildren<Text>().fontSize = 18;
            }
            cells[6].stateLabel.rectTransform.anchoredPosition = new Vector2(0f, 12f); // under the choices (lowest spans 27..85)

            Button claim = EntrySceneBuilder.Btn(card.transform, "CheckinClaim", "Nhận Quà", new Vector2(-150f, 56f), new Vector2(280f, 72f), new Color(1f, 0.82f, 0.45f), new Color(0.24f, 0.13f, 0.03f), new Vector2(0.5f, 0f));
            Button close = EntrySceneBuilder.Btn(card.transform, "CheckinClose", "Đóng", new Vector2(170f, 56f), new Vector2(240f, 72f), new Color(0.36f, 0.36f, 0.42f), cream, new Vector2(0.5f, 0f));

            // red dot on the HUD icon: shown only when the server says today's gift can be claimed
            var dot = EntrySceneBuilder.Img(opener.transform, "RedDot", new Vector2(1f, 1f), new Vector2(26f, 26f), new Color(0.92f, 0.18f, 0.2f), AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_circle.png"));
            dot.type = Image.Type.Simple;
            dot.rectTransform.anchoredPosition = new Vector2(-16f, -10f);

            var panel = canvas.gameObject.AddComponent<CheckinPanel>(); // on the always-active canvas: it hides/shows the modal
            var so = new SerializedObject(panel);
            so.FindProperty("root").objectReferenceValue = modal;
            so.FindProperty("statusText").objectReferenceValue = status;
            var cp = so.FindProperty("cells");
            cp.arraySize = 7;
            for (int i = 0; i < 7; i++)
            {
                var e = cp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("frame").objectReferenceValue = cells[i].frame;
                e.FindPropertyRelative("icon").objectReferenceValue = cells[i].icon;
                e.FindPropertyRelative("done").objectReferenceValue = cells[i].done;
                e.FindPropertyRelative("dayLabel").objectReferenceValue = cells[i].dayLabel;
                e.FindPropertyRelative("rewardLabel").objectReferenceValue = cells[i].rewardLabel;
                e.FindPropertyRelative("stateLabel").objectReferenceValue = cells[i].stateLabel;
            }
            so.FindProperty("claimButton").objectReferenceValue = claim;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("claimLabel").objectReferenceValue = claim.GetComponentInChildren<Text>();
            var ch = so.FindProperty("choiceButtons");
            ch.arraySize = 3;
            for (int i = 0; i < 3; i++) ch.GetArrayElementAtIndex(i).objectReferenceValue = choices[i];
            so.FindProperty("redDot").objectReferenceValue = dot.gameObject;
            so.FindProperty("goldIcon").objectReferenceValue = HudArt.Get("coin");
            so.FindProperty("diamondIcon").objectReferenceValue = HudArt.Get("diamond");
            so.FindProperty("chestIcon").objectReferenceValue = HudArt.Get("reward_chest");
            so.FindProperty("skinIcon").objectReferenceValue = HudArt.Get("reward_skin");
            so.FindProperty("cellSprite").objectReferenceValue = FrameArt.Get("hall_card");
            so.FindProperty("cellOnSprite").objectReferenceValue = FrameArt.Get("hall_card_on");
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(opener.onClick, panel.Open);
            modal.SetActive(false);
        }

        private static Button IconButton(Transform parent, string name, string icon, string caption, Vector2 anchor, Vector2 pos, float size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(size + 10f, size + (string.IsNullOrEmpty(caption) ? 0f : 30f));
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // whole cell is the touch target
            var img = EntrySceneBuilder.Img(go.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(size, size), Color.white, HudArt.Get(icon));
            img.type = Image.Type.Simple;
            img.rectTransform.pivot = new Vector2(0.5f, 1f);
            img.rectTransform.anchoredPosition = Vector2.zero;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = cb;
            if (!string.IsNullOrEmpty(caption))
            {
                Text t = EntrySceneBuilder.Label(go.transform, "Caption", caption, 18, new Color(1f, 0.97f, 0.9f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(size + 10f, 28f), FontStyle.Bold);
                t.raycastTarget = false;
                t.gameObject.AddComponent<FitOneLine>().Configure(18, 12);
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.7f);
                o.effectDistance = new Vector2(1.2f, -1.2f);
            }
            return b;
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

        /// <summary>NPC dialog: framed panel along the bottom (never over the hero), speaker banner on the frame edge,
        /// message inside the measured content area, continue button in its own row below the text.</summary>
        private static void BuildNpcDialog(Transform hud, out GameObject dialog, out Text speaker, out Text line, out Button cont)
        {
            Color cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.84f, 0.5f);
            dialog = EntrySceneBuilder.Panel(hud, "NpcDialog", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f),
                new Vector2(1240f, 330f), new Color(0.08f, 0.1f, 0.18f, 0.95f), EntrySceneBuilder.PanelSprite);
            // NPC talk = a warm parchment scroll with dark ink (its own look, not the shared gold frame)
            bool scroll = EntrySceneBuilder.Skin(dialog, "parchment");
            if (!scroll) EntrySceneBuilder.Frame(dialog, 3.2f);
            RectTransform c = EntrySceneBuilder.FramedContent(dialog, scroll ? 0f : 14f, topBand: scroll ? 0f : 50f);
            EntrySceneBuilder.VStack(c, 8f, TextAnchor.UpperCenter, new RectOffset(8, 8, 18, 0));
            // speaker ribbon sits ON the top edge of the scroll
            var banner = EntrySceneBuilder.Panel(dialog.transform, "SpeakerBanner", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, 6f),
                new Vector2(380f, 72f), new Color(0.22f, 0.36f, 0.42f, 1f), EntrySceneBuilder.PanelSprite);
            if (!EntrySceneBuilder.Skin(banner, "ribbon", 0.8f)) EntrySceneBuilder.Rim(banner.transform);
            speaker = EntrySceneBuilder.FillLabel(banner.transform, "Speaker", "", 28, scroll ? cream : gold, FontStyle.Bold, 62f, 14f, 16);
            Color ink = new Color(0.24f, 0.14f, 0.05f);
            line = EntrySceneBuilder.BlockLabel(c, "Line", "", 26, scroll ? ink : cream, 120f, FontStyle.Normal, TextAnchor.UpperLeft);
            var lsh = line.GetComponent<Shadow>();
            if (scroll && lsh) lsh.effectColor = new Color(1f, 0.95f, 0.8f, 0.4f);
            line.supportRichText = true;
            line.lineSpacing = 1.1f;
            line.resizeTextMinSize = 19;
            RectTransform row = EntrySceneBuilder.HRow(c, "Buttons", 62f, 12f, TextAnchor.MiddleRight);
            cont = EntrySceneBuilder.Btn(row, "Continue", "Cảm ơn ông!", Vector2.zero, new Vector2(240f, 62f), new Color(0.26f, 0.56f, 0.62f), Color.white);
            EntrySceneBuilder.Size(cont, 62f, 240f);
            dialog.SetActive(false);
        }

        private static void BuildPortalUi(Transform hud, out GameObject confirm, out Text confirmText, out Button yes, out Button no,
            out GameObject loading, out Text title, out Image bar, out Text tip)
        {
            Color ink = new Color(0.07f, 0.09f, 0.15f, 0.92f), cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.82f, 0.45f);
            // ---- confirmation dialog
            confirm = EntrySceneBuilder.Panel(hud, "PortalConfirm", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.45f), null);
            var card = EntrySceneBuilder.Panel(confirm.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(800f, 400f), ink, EntrySceneBuilder.PanelSprite);
            if (!EntrySceneBuilder.Skin(card, "popup_card")) EntrySceneBuilder.Frame(card, 2.8f);
            RectTransform c = EntrySceneBuilder.FramedContent(card, 0f, topBand: 0f);
            c.offsetMin = new Vector2(36f, 24f); c.offsetMax = new Vector2(-36f, -7f); // title in the crest band
            EntrySceneBuilder.VStack(c, 14f, TextAnchor.UpperCenter);
            EntrySceneBuilder.BlockLabel(c, "Title", "Cổng Dịch Chuyển", 32, gold, 44f, FontStyle.Bold);
            confirmText = EntrySceneBuilder.BlockLabel(c, "Question", "", 28, cream, 92f);
            confirmText.supportRichText = true;
            RectTransform row = EntrySceneBuilder.HRow(c, "Buttons", 66f, 20f);
            yes = EntrySceneBuilder.Btn(row, "Confirm", "Dịch chuyển", Vector2.zero, new Vector2(260f, 66f), new Color(0.26f, 0.56f, 0.62f), Color.white);
            EntrySceneBuilder.Size(yes, 66f, 260f);
            no = EntrySceneBuilder.Btn(row, "Cancel", "Ở lại", Vector2.zero, new Vector2(260f, 66f), new Color(0.36f, 0.36f, 0.42f), Color.white);
            EntrySceneBuilder.Size(no, 66f, 260f);
            confirm.SetActive(false);

            // ---- loading screen: the crisp baked dawn layers (never a stretched low-res painting)
            loading = EntrySceneBuilder.Panel(hud, "Loading", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.04f, 0.08f, 1f), null);
            foreach (string n in new[] { "sky", "hills", "mid", "near" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{LoginBackdropBaker.Dir}/{n}.png");
                if (!tex) continue;
                var raw = new GameObject("Art_" + n, typeof(RectTransform)).AddComponent<RawImage>();
                raw.gameObject.layer = 5;
                raw.transform.SetParent(loading.transform, false);
                raw.texture = tex;
                raw.color = new Color(0.62f, 0.64f, 0.72f, 1f); // dimmed so the title reads
                raw.raycastTarget = false;
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
            }
            title = EntrySceneBuilder.Label(loading.transform, "Destination", "", 56, gold, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 90f), FontStyle.Bold);
            var to = title.gameObject.AddComponent<Outline>();
            to.effectColor = new Color(0.2f, 0.1f, 0.03f, 0.9f);
            to.effectDistance = new Vector2(2f, -2f);
            var track = EntrySceneBuilder.Panel(loading.transform, "BarTrack", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f),
                new Vector2(820f, 26f), new Color(0f, 0f, 0f, 0.55f), EntrySceneBuilder.PanelSprite);
            var fill = EntrySceneBuilder.Panel(track.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.45f, 0.95f, 1f), null);
            bar = fill.GetComponent<Image>();
            bar.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png");
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 0f;
            tip = EntrySceneBuilder.Label(loading.transform, "Tip", "", 26, cream, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1300f, 60f));
            var tipo = tip.gameObject.AddComponent<Outline>();
            tipo.effectColor = new Color(0f, 0f, 0f, 0.8f);
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
