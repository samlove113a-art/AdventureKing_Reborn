using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Thị trấn Chuông Gió (hub) as a PURE 2D side-scrolling pixel-art map: orthographic camera, four parallax layers, sprite props with fixed
    /// sorting rows, Collider2D platforms from TownLayout.Hub, sprite portals, 2D NPC bodies and a few map gimmicks (jump pad, moving platform,
    /// secret doorway). Every picture comes from Assets/Art/World/Village2D (Meshy 2D PNG); nothing is drawn by code, no 3D object is created.
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        private const string V2D = "Assets/Art/World/Village2D";
        public const float OrthoSize = 3.65f;
        private static bool Pure2DWorld;

        /// <summary>Rebuilds only the hub (AK_Village) as the pure 2D map.   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.VillageSceneBuilder.BuildHub2DFromCommandLine</summary>
        public static void BuildHub2DFromCommandLine()
        {
            try { EnsureLayer(OneWayLayer, "OneWayPlatform"); BuildTown2D(); RegisterBuildScenes(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static Sprite S2(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{V2D}/{name}.png");

        private static Scene NewWorldScene2D(out Transform world)
        {
            mats.Clear();
            EntrySceneBuilder.InitUi();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            world = new GameObject("World").transform;
            return scene;
        }

        // ------------------------------------------------------------------ small sprite helpers
        private static SpriteRenderer Spr(Transform parent, string name, Sprite sprite, Vector3 pos, int order, bool flip = false, float scale = 1f, Color? tint = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3((flip ? -1f : 1f) * scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = tint ?? Color.white;
            return sr;
        }

        private static SpriteRenderer Tiled(Transform parent, string name, Sprite sprite, float x0, float x1, float baseY, int order)
        {
            var sr = Spr(parent, name, sprite, new Vector3((x0 + x1) * 0.5f, baseY, 0f), order);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(x1 - x0, sprite.bounds.size.y);
            return sr;
        }

        private struct Row { public int Order; public float Scale, Lift; public Color Tint; public Row(int o, float s, float l, Color t) { Order = o; Scale = s; Lift = l; Tint = t; } }
        private static readonly Row Back = new Row(-60, 0.78f, 0.9f, new Color(0.62f, 0.66f, 0.86f));    // far buildings: smaller, higher, hazier
        private static readonly Row Mid = new Row(-40, 0.92f, 0.1f, new Color(0.82f, 0.84f, 0.95f));
        private static readonly Row Front = new Row(-20, 1f, 0f, Color.white);

        private static SpriteRenderer Prop2(Transform art, string name, float x, Row row, bool flip = false, float lift = 0f)
        {
            Sprite s = S2(name);
            if (!s) { Debug.LogWarning("[WORLD2D] missing picture " + name); return null; }
            return Spr(art, name, s, new Vector3(x, row.Lift + lift, 0f), row.Order, flip, row.Scale, row.Tint);
        }

        // ------------------------------------------------------------------ the map
        private static void BuildTown2D()
        {
            SceneTag = "hub";
            Pure2DWorld = true;
            Scene scene = NewWorldScene2D(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay"), gimmicks = Group(world, "Gimmicks");

            float minX = TownLayout.Hub.MinX, maxX = TownLayout.Hub.MaxX;
            // ---- terrain: grass + earth strip (tiled), invisible wall colliders at both ends
            Solid(solids, "Ground", minX - 2f, maxX + 2f, 0f, 4f, false);
            Solid(solids, "WallWest", minX - 3f, minX - 1f, 40f, 48f, false);
            Solid(solids, "WallEast", maxX + 1f, maxX + 3f, 40f, 48f, false);
            Sprite ground = S2("tile_ground");
            if (ground) Tiled(art, "Ground", ground, minX - 14f, maxX + 14f, 0.2f - ground.bounds.size.y, -10);   // the grass blades start a little under the sprite top: raise it so the walking line is the visible surface

            // ---- decks: one-way plank platforms (the plank surface lines up with the collider top)
            Sprite deck = S2("tile_deck");
            foreach (var d in TownLayout.Hub.Decks)
            {
                Solid(solids, d.Name, d.XMin, d.XMax, d.Top, 0.4f, true);
                if (deck) Tiled(art, "Deck_" + d.Name, deck, d.XMin, d.XMax, d.Top - deck.bounds.size.y * DeckPlankTopFrac, -8);
            }
            foreach (var c in TownLayout.Hub.Climbs) ClimbZone2D(play, c);

            // ---- scenery rows, far -> near
            Prop2(art, "windmill", 46f, Back);
            Prop2(art, "banyan_tree", -10f, Back, true);
            Prop2(art, "banyan_tree", 22f, Back);
            Prop2(art, "cottage", -44f, Mid, true);
            Prop2(art, "bell_tower", 3f, Mid);
            Prop2(art, "teahouse", 40f, Mid);
            Prop2(art, "cottage", 57f, Mid);
            Prop2(art, "cottage", -58f, Mid, true);
            Prop2(art, "forge", -45f, Mid);
            Prop2(art, "banner_arch", 12f, Mid);
            Prop2(art, "hero_statue", 3f, Front);
            Prop2(art, "bamboo", -24f, Front);
            Prop2(art, "bamboo", 62f, Front, true);
            Prop2(art, "market_stall", -30f, Front);
            Prop2(art, "market_stall", 20f, Front, true);              // Dược Sư Mộc Linh's herb counter (potion stall)
            Prop2(art, "crates", -25f, Front, true);
            Prop2(art, "crates", 52f, Front);
            Prop2(art, "stone_lantern", -8f, Front);
            Prop2(art, "stone_lantern", 17f, Front, true);
            Prop2(art, "lantern_post", -1.5f, Front);
            Prop2(art, "lantern_post", 29f, Front, true);
            Prop2(art, "lantern_post", 63f, Front);
            Prop2(art, "planter", 9f, Front);
            Prop2(art, "planter", -35f, Front, true);
            Sign2(art, -38f, "Phố\nThợ Rèn\n& Chợ");
            Sign2(art, 34f, "Quán Trà\nGió Chiều");
            Sign2(art, 59f, "Cổng → Đồng\nCỏ Gió");
            Sign2(art, 70f, "Sảnh Bang\nHội →");
            Sign2(art, -52.5f, "← Sảnh\nKết Hôn");

            // ---- NPC spots, portals, NPCs
            foreach (var spot in TownLayout.Hub.NpcSpots)
            {
                var t = new GameObject("NpcSpot_" + spot.role).transform;
                t.SetParent(play, false);
                t.position = new Vector3(spot.x, spot.y, 0f);
            }
            foreach (var p in TownLayout.Hub.Portals) Portal2D(play, p);
            PlaceNpc2D(play, "NpcElder", "Trưởng Làng", 7f, 0f);
            PlaceNpc2D(play, "NpcSmith", "Thợ Rèn", -46f, 0f);
            PlaceNpc2D(play, "NpcStorekeeper", "Chủ Tiệm Lụa", -30f, 0f);
            PlaceHealer2D(play, 20f);
            PlaceKeeper2D(play, "NpcPetKeeper", "NpcMushroomElder", "Bác Chăn Thú", 26f, new Color(0.85f, 1f, 0.8f), "Chào {0}! Pet nào cũng cần ăn đủ bữa — mua thức ăn cho bé ở đây nhé.");
            PlaceKeeper2D(play, "NpcJeweler", "NpcMusician", "Thợ Kim Hoàn", -37f, new Color(1f, 0.9f, 0.7f), "Chào {0}! Nhẫn và dây chuyền ta chế tác đều có thể đem ra Thợ Rèn để cường hóa.");

            // ---- map gimmicks
            Gimmicks2D(gimmicks, art);

            FinishWorld(scene, ScenePath, world, TownLayout.Hub.MapId, TownLayout.Hub.DisplayName, TownLayout.Hub.Spawn, minX, maxX, 14f, AdventureMusicStage.Village);
            Pure2DWorld = false;
        }

        /// <summary>Where the plank surface sits inside the deck picture (fraction of its height measured from the bottom edge).</summary>
        private const float DeckPlankTopFrac = 0.62f;

        private static void Sign2(Transform art, float x, string text)
        {
            Sprite s = S2("signboard");
            if (!s) return;
            var sr = Spr(art, "Signboard", s, new Vector3(x, 0f, 0f), -20);
            WorldLabel(sr.transform, "Text", 1.15f, text, 20, new Color(1f, 0.88f, 0.55f), null, new Vector2(250f, 150f), -19);
        }

        private static void ClimbZone2D(Transform play, TownLayout.Climb c)
        {
            var go = new GameObject((c.Rope ? "Rope_" : "Ladder_") + c.Name);
            go.transform.SetParent(play, false);
            go.transform.position = new Vector3(c.X, (c.Bottom + c.Top) * 0.5f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(c.Rope ? 0.9f : 1.1f, c.Top - c.Bottom);
            go.AddComponent<LadderZone2D>();
            // the ladder / rope picture is added once those 2D sprites exist (Village2D/ladder, Village2D/rope)
            Sprite pic = S2(c.Rope ? "rope" : "ladder");
            if (!pic) return;
            float h = c.VisualTop - c.Bottom;
            var sr = Spr(go.transform, "Visual", pic, new Vector3(c.X, c.Bottom, 0f), -6);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(pic.bounds.size.x, h);
        }

        private static void Portal2D(Transform play, TownLayout.PortalSpot p)
        {
            var root = new GameObject("Portal_" + p.Id);
            root.transform.SetParent(play, false);
            root.transform.position = new Vector3(p.X, 0f, 0f);
            Sprite gate = S2("portal_gate");
            Transform vortex = root.transform;
            if (gate)
            {
                var sr = Spr(root.transform, "Gate", gate, new Vector3(p.X, 0f, 0f), -15);
                vortex = sr.transform;
                var pulse = sr.gameObject.AddComponent<AmbientMotion>();       // the painted vortex breathes (scale pulse is done by the gate script)
                pulse.bobHeight = 0.04f; pulse.bobSpeed = 1.6f;
            }
            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 3.2f);
            col.offset = new Vector2(0f, 1.6f);
            var gateComp = root.AddComponent<PortalGate2D>();
            var hum = root.AddComponent<AudioSource>();
            hum.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Portal_Hum.wav");
            hum.loop = true; hum.playOnAwake = true; hum.spatialBlend = 1f;
            hum.rolloffMode = AudioRolloffMode.Linear; hum.minDistance = 2f; hum.maxDistance = 22f; hum.volume = 0.55f;
            GameObject prompt = PortalPrompt(root.transform, 4.2f, out Text label);
            var so = new SerializedObject(gateComp);
            so.FindProperty("portalId").stringValue = p.Id;
            so.FindProperty("destinationName").stringValue = p.DestinationName;
            so.FindProperty("prompt").objectReferenceValue = prompt;
            so.FindProperty("promptLabel").objectReferenceValue = label;
            so.FindProperty("hum").objectReferenceValue = hum;
            so.FindProperty("vortex").objectReferenceValue = vortex;
            so.FindProperty("requireConfirmation").boolValue = p.ToScene == TownLayout.Meadow.Scene;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>NPC container with no 3D model: NpcActor swaps its (empty) model slot for the 2D sprite body Resources/Chibi2D/Actors/{name}_Visual2D at start.</summary>
        private static GameObject PlaceNpc2D(Transform play, string actorName, string displayName, float x, float y)
        {
            var npc = new GameObject(actorName);
            npc.transform.SetParent(play, false);
            npc.transform.position = new Vector3(x, y, 0f);
            var slot = new GameObject("Model").AddComponent<Animator>();
            slot.transform.SetParent(npc.transform, false);
            npc.AddComponent<NpcActor>();
            var canvasGo = new GameObject("NamePlate", typeof(RectTransform));
            canvasGo.transform.SetParent(npc.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            canvasGo.AddComponent<CanvasGroup>().blocksRaycasts = false;
            canvasGo.AddComponent<NamePlateFade>();
            var rt = (RectTransform)canvasGo.transform;
            float plateW = Mathf.Max(300f, displayName.Length * 17f + 40f);
            rt.sizeDelta = new Vector2(plateW, 54f);
            rt.localScale = Vector3.one * 0.0062f;
            var bg = EntrySceneBuilder.Panel(canvasGo.transform, "Plate", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.08f, 0.1f, 0.18f, 0.78f), EntrySceneBuilder.PanelSprite);
            EntrySceneBuilder.Label(bg.transform, "Name", displayName, 30, new Color(1f, 0.86f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(plateW - 10f, 50f), FontStyle.Bold);
            return npc;
        }

        /// <summary>A shop NPC of one goods group (the 2D sprite body of a spare actor, tinted) with its name plate and "E · Nói chuyện" prompt; the shop window is wired by SetupShopKeepers.</summary>
        private static void PlaceKeeper2D(Transform play, string objectName, string actorBody, string displayName, float x, Color tint, string hello)
        {
            GameObject npc = PlaceNpc2D(play, actorBody, displayName, x, 0f);
            npc.name = objectName;
            var prompt = WorldLabel(npc.transform, "TalkPrompt", 2.85f, "E · Nói chuyện", 28, Color.white, new Color(0.26f, 0.56f, 0.62f, 0.92f), new Vector2(300f, 54f), 41);
            prompt.transform.parent.parent.gameObject.SetActive(false);
            var keeper = npc.AddComponent<ShopKeeperNpc>();
            keeper.Bind(npc.GetComponent<NpcActor>(), null, prompt.transform.parent.parent.gameObject, hello, tint);
        }

        /// <summary>Dược Sư Mộc Linh as a 2D body (the Mage sprite) behind the potion stall prop.</summary>
        private static void PlaceHealer2D(Transform play, float x)
        {
            var root = new GameObject("NpcHealer").transform;
            root.SetParent(play, false);
            root.position = new Vector3(x - 1.6f, 0f, 0f);
            var prefab = Resources.Load<GameObject>("Chibi2D/Mage_Visual2D");
            if (!prefab) { Debug.LogWarning("[WORLD2D] Mage_Visual2D missing"); return; }
            var pivot = new GameObject("HealerPivot").transform;
            pivot.SetParent(root, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
            visual.name = "HealerVisual";
            visual.transform.localPosition = Vector3.zero;
            WorldLabel(root, "NamePlate", 2.45f, "Dược Sư Mộc Linh", 28, new Color(0.7f, 1f, 0.75f), new Color(0.08f, 0.14f, 0.12f, 0.8f), new Vector2(330f, 54f), 40);
            var prompt = WorldLabel(root, "TalkPrompt", 2.85f, "E · Nói chuyện", 28, Color.white, new Color(0.26f, 0.56f, 0.62f, 0.92f), new Vector2(300f, 54f), 41);
            prompt.transform.parent.parent.gameObject.SetActive(false);
            var mark = WorldLabel(root, "QuestMark", 3.25f, "", 64, new Color(1f, 0.85f, 0.25f), null, new Vector2(80f, 80f), 42);
            root.gameObject.AddComponent<NpcQuestMarker>().Bind("Dược Sư Mộc Linh", mark);
            var healer = root.gameObject.AddComponent<HealerNpc>();
            healer.Bind(pivot, visual.transform, visual.GetComponentInChildren<Animator>(), null, prompt.transform.parent.parent.gameObject, null);
        }

        // ------------------------------------------------------------------ gimmicks
        private static void Gimmicks2D(Transform gimmicks, Transform art)
        {
            // jump pad on the ground below the rope bridge: launches the hero up onto the bridge (deck top 5 m)
            Sprite pad = S2("jump_pad");
            if (pad)
            {
                var g = new GameObject("JumpPad_Bridge");
                g.transform.SetParent(gimmicks, false);
                g.transform.position = new Vector3(13f, 0f, 0f);
                var vis = Spr(g.transform, "Visual", pad, g.transform.position, -12);
                var col = g.AddComponent<BoxCollider2D>();
                col.isTrigger = true; col.size = new Vector2(1.1f, 0.6f); col.offset = new Vector2(0f, 0.45f);
                var jp = g.AddComponent<JumpPad2D>();
                new SerializedObject(jp).FindProperty("visual").objectReferenceValue = vis.transform;
                var so = new SerializedObject(jp);
                so.FindProperty("visual").objectReferenceValue = vis.transform;
                so.FindProperty("launchSpeed").floatValue = 19f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // moving platform: ferries the hero from the tea-house veranda up to the windmill lookout
            Sprite plat = S2("moving_platform");
            if (plat)
            {
                var g = new GameObject("MovingPlatform_Windmill");
                g.transform.SetParent(gimmicks, false);
                g.layer = OneWayLayer;
                var vis = Spr(g.transform, "Visual", plat, Vector3.zero, -9);
                float w = plat.bounds.size.x, h = plat.bounds.size.y;
                vis.transform.localPosition = new Vector3(0f, -h * 0.18f, 0f);
                var col = g.AddComponent<BoxCollider2D>();
                col.size = new Vector2(w * 0.9f, 0.35f); col.offset = new Vector2(0f, -0.17f);
                col.usedByEffector = true;
                var eff = g.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true; eff.surfaceArc = 160f; eff.useColliderMask = false;
                var mp = g.AddComponent<MovingPlatform2D>();
                mp.Configure(new Vector2(35.5f, 5.4f), new Vector2(35.5f, 9.6f), 5f);
                g.transform.position = new Vector3(35.5f, 5.4f, 0f);
            }
            // secret doorway: hollow of the banyan tree -> the wind lookout above the bell terrace
            Sprite hid = S2("hidden_portal");
            if (hid)
            {
                SecretPortal2D MakeDoor(string n, Vector2 at, bool flip)
                {
                    var g = new GameObject(n);
                    g.transform.SetParent(gimmicks, false);
                    g.transform.position = at;
                    var sr = Spr(g.transform, "Visual", hid, at, -14, flip, 0.9f);
                    var col = g.AddComponent<BoxCollider2D>();
                    col.isTrigger = true; col.size = new Vector2(1.4f, 2.4f); col.offset = new Vector2(0f, 1.2f);
                    var sp = g.AddComponent<SecretPortal2D>();
                    new SerializedObject(sp).FindProperty("glow").objectReferenceValue = sr;
                    new SerializedObject(sp).ApplyModifiedPropertiesWithoutUndo();
                    return sp;
                }
                SecretPortal2D a = MakeDoor("SecretDoor_Banyan", new Vector2(-14.5f, 0f), false);
                SecretPortal2D b = MakeDoor("SecretDoor_Lookout", new Vector2(9f, 10f), true);
                a.SetTwin(b); b.SetTwin(a);
                new SerializedObject(a).FindProperty("twin").objectReferenceValue = b;
                new SerializedObject(b).FindProperty("twin").objectReferenceValue = a;
                var sa = new SerializedObject(a); sa.FindProperty("twin").objectReferenceValue = b; sa.ApplyModifiedPropertiesWithoutUndo();
                var sb = new SerializedObject(b); sb.FindProperty("twin").objectReferenceValue = a; sb.ApplyModifiedPropertiesWithoutUndo();
            }
            // training yard and ranking board
            Sprite dummy = S2("training_dummy");
            if (dummy)
            {
                var g = new GameObject("TrainingDummy");
                g.transform.SetParent(gimmicks, false);
                g.transform.position = new Vector3(-20f, 0f, 0f);
                var vis = Spr(g.transform, "Visual", dummy, g.transform.position, -18);
                var td = g.AddComponent<TrainingDummy2D>();
                var so = new SerializedObject(td);
                so.FindProperty("visual").objectReferenceValue = vis.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                WorldLabel(g.transform, "Label", 2.7f, "Bãi Tập", 26, new Color(1f, 0.85f, 0.5f), new Color(0.08f, 0.1f, 0.18f, 0.75f), new Vector2(210f, 48f), 30);
            }
            Sprite board = S2("leaderboard");
            if (board)
            {
                var g = Spr(gimmicks, "Leaderboard", board, new Vector3(27f, 0f, 0f), -18);
                WorldLabel(g.transform, "Label", 3.6f, "Bảng Xếp Hạng", 26, new Color(1f, 0.85f, 0.5f), new Color(0.08f, 0.1f, 0.18f, 0.75f), new Vector2(270f, 48f), 30);
            }
        }

        // ------------------------------------------------------------------ backdrop
        private static void Backdrop2D(Transform world, Camera cam)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(world, false);
            var bg = go.AddComponent<ParallaxBackground4>();
            var so = new SerializedObject(bg);
            so.FindProperty("cam").objectReferenceValue = cam;
            SerializedProperty layers = so.FindProperty("layers");
            layers.arraySize = 4;
            void Set(int i, ParallaxBackground4.Slot slot, string pic, float fx, float fy, float baseY, float z, int order, float auto, Color tint)
            {
                SerializedProperty l = layers.GetArrayElementAtIndex(i);
                l.FindPropertyRelative("slot").enumValueIndex = (int)slot;
                l.FindPropertyRelative("sprite").objectReferenceValue = S2(pic);
                l.FindPropertyRelative("factorX").floatValue = fx;
                l.FindPropertyRelative("factorY").floatValue = fy;
                l.FindPropertyRelative("baseY").floatValue = baseY;
                l.FindPropertyRelative("z").floatValue = z;
                l.FindPropertyRelative("sortingOrder").intValue = order;
                l.FindPropertyRelative("autoScrollX").floatValue = auto;
                l.FindPropertyRelative("tint").colorValue = tint;
            }
            float camY = TownLayout.Hub.Spawn.y + CamOffsetY;
            Set(0, ParallaxBackground4.Slot.Sky, "bg_sky_m", 0.03f, 1f, camY, 40f, -400, 0.04f, new Color(0.62f, 0.58f, 0.8f));
            Set(1, ParallaxBackground4.Slot.FarMountains, "bg_far_m", 0.18f, 0.96f, -0.7f + 3.06f, 30f, -300, 0f, new Color(0.9f, 0.9f, 1f, 0.95f));
            Set(2, ParallaxBackground4.Slot.MidForest, "bg_mid_m", 0.42f, 0.98f, -2.2f + 2.3f, 15f, -200, 0f, new Color(0.85f, 0.87f, 1f));
            Set(3, ParallaxBackground4.Slot.Foreground, "bg_near_m", 1.18f, 1f, -2.2f + 2.95f, 5f, -80, 0f, new Color(0.7f, 0.75f, 0.85f, 0.5f));
            so.ApplyModifiedPropertiesWithoutUndo();
            AddFireflies(cam.transform);
        }

        /// <summary>Fireflies and drifting dust around the camera: a particle system that uses the project's own soft-dot picture (no generated texture).</summary>
        private static void AddFireflies(Transform camera)
        {
            Material add = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/dot_add.mat");
            var go = new GameObject("Fireflies");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = new Vector3(0f, -0.5f, 5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = 7f; main.startSpeed = 0.15f; main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.45f, 0.9f), new Color(0.6f, 1f, 0.8f, 0.8f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 8f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(15f, 6f, 1f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.35f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = add;
            r.sortingOrder = 2;
        }
    }
}
