using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Chương 1 — Vương Quốc Nấm. Map 1 "Làng Nấm" is the reference map for maps 2–10: gameplay geometry comes from
    /// <see cref="TownLayout.Ch1Village"/> (shared with the server's portal graph / safe zone), scenery is reusable Meshy
    /// 3D modules (Tools/meshy/make_ch1_props.py → Tools/blender/export_prop.py → Assets/Art/World/Chapter1/Props),
    /// recoloured per instance instead of re-generated. The rigged Nấm Non (MonsterNamNon) is the farm monster.
    ///   Unity -batchmode -executeMethod AdventureKing.ArtPipeline.VillageSceneBuilder.BuildChapter1FromCommandLine
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        public const string Ch1VillageScenePath = "Assets/Scenes/Chapter1/AK_C1_M01.unity";
        private const string C1PropDir = WorldDir + "/Chapter1/Props";

        public static void BuildChapter1FromCommandLine()
        {
            try
            {
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                BuildMeadow();          // + the east "Cổng Pha Lê" to Làng Nấm (meadow content otherwise unchanged)
                BuildCh1Village();
                BuildCh1GreenMeadow();
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Chapter maps registered after the hub / meadow / halls (see RegisterBuildScenes).</summary>
        private static readonly string[] ChapterScenePaths = { Ch1VillageScenePath, Ch1GreenMeadowScenePath };
        public const string Ch1GreenMeadowScenePath = "Assets/Scenes/Chapter1/AK_C1_M02.unity";

        /// <summary>Only Map 2 (Làng Nấm keeps its last build except when its east gate changes).</summary>
        public static void BuildChapter1Map2FromCommandLine()
        {
            try
            {
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                BuildCh1Village();      // east gate to Map 2 replaces the "sắp mở" sign
                BuildCh1GreenMeadow();
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Meshy module at its exported size × <paramref name="scale"/>, optional tint (recolour, no new credits).</summary>
        private static GameObject C1Prop(Transform parent, string name, float x, float z, float scale = 1f, bool flip = false, float y = 0f, Color? tint = null, float yaw = 0f)
        {
            if (!File.Exists($"{C1PropDir}/{name}/{name}.fbx")) { Debug.LogWarning("[CH1] prop missing: " + name); return null; }
            GameObject go = Prop(parent, name, x, z, 1f, flip, y, C1PropDir, yaw);
            if (!go) return null;
            go.transform.localScale *= scale;
            if (tint.HasValue)
            {
                string key = $"{name}_{ColorUtility.ToHtmlStringRGB(tint.Value)}";
                if (!mats.TryGetValue(key, out Material m))
                {
                    string path = $"{MatDir}/c1_{key}.mat";
                    m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!m) { m = new Material(PropMaterial(name, C1PropDir)); AssetDatabase.CreateAsset(m, path); }
                    m.CopyPropertiesFromMaterial(PropMaterial(name, C1PropDir));
                    m.SetColor("_BaseColor", tint.Value);
                    EditorUtility.SetDirty(m);
                    mats[key] = m;
                }
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
            }
            return go;
        }

        // ================================================================== Map 1 · Làng Nấm
        private static void BuildCh1Village()
        {
            SceneTag = "ch1_m01";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");
            float minX = TownLayout.Ch1Village.MinX, maxX = TownLayout.Ch1Village.MaxX;
            Ground(art, solids, minX, maxX, stream: false);
            foreach (var d in TownLayout.Ch1Village.Decks) Deck(art, solids, d, d.Top >= 6f ? "deck_high" : "deck");
            foreach (var c in TownLayout.Ch1Village.Climbs) ClimbZone(art, play, c);

            // ---- village square (safe zone, west): houses at the back, fountain + elder in the middle, stalls, lamps
            Color warmRed = new Color(1f, 0.92f, 0.88f), amber = new Color(1f, 0.82f, 0.62f), teal = new Color(0.78f, 1f, 0.96f);
            C1Prop(art, "ch1_mushroom_house", -24f, 6.5f, 1f, false);
            C1Prop(art, "ch1_mushroom_house", -11f, 9f, 0.85f, true, tint: amber);
            C1Prop(art, "ch1_giant_mushroom_tree", 14f, 7.5f, 1.05f, true);
            C1Prop(art, "ch1_mushroom_house", 26f, 7f, 0.9f, false, tint: warmRed);
            C1Prop(art, "ch1_mushroom_house", -37f, 12f, 0.8f, true, tint: teal);
            C1Prop(art, "ch1_crystal_fountain", 2f, 3.4f, 1f, false);
            HallGlow(art, "c1_fountain", new Vector3(2f, 2.9f, 3.1f), 2.4f, new Color(0.45f, 0.85f, 1f) * 0.55f);
            C1Prop(art, "ch1_mushroom_stall", -14f, 2.9f, 1f, false);
            C1Prop(art, "ch1_mushroom_stall", 23f, 3.0f, 0.95f, true, tint: teal);
            foreach (float lx in new[] { -30f, -6f, 10f, 30f, 44f, 88f })
            {
                C1Prop(art, "ch1_mushroom_lamp", lx, 2.2f, 0.9f, lx > 0f);
                HallGlow(art, "c1_lamp", new Vector3(lx + (lx > 0f ? 0.42f : -0.42f), 2.3f, 2.0f), 1.3f, new Color(1f, 0.7f, 0.35f) * 0.5f);
            }
            Signboard(art, -33f, 2.6f, "Làng Nấm\n←\nĐồng Cỏ\nGió");
            Signboard(art, 31.5f, 2.6f, "Hết vùng\nan toàn\n→\nRuộng Nấm");

            // ---- farm outskirts (east): giant mushrooms behind the fields, a far village row for depth
            C1Prop(art, "ch1_giant_mushroom_tree", 46f, 13f, 1.2f, false);
            C1Prop(art, "ch1_giant_mushroom_tree", 66f, 10f, 0.95f, true, tint: amber);
            C1Prop(art, "ch1_giant_mushroom_tree", 90f, 12f, 1.1f, false, tint: warmRed);
            C1Prop(art, "ch1_mushroom_house", 58f, 18f, 0.8f, true);
            C1Prop(art, "ch1_mushroom_house", 80f, 17f, 0.75f, false, tint: amber);
            Prop(art, "crates", 50f, 2.6f, 2f, true);
            Prop(art, "planter", 70f, 2.4f, 1.3f, false);
            Signboard(art, 86.5f, 2.6f, "Đồng Cỏ\nXanh\n→"); // Map 2 gate (ch1_village_east_m02, x 92)

            // ---- the chapter guide: Trưởng Làng Nấm by the fountain (quest giver, "!" / "?" from the server log)
            Vector2 e = TownLayout.Ch1Village.ElderSpot;
            GameObject elderGo = PlaceNpc(play, "NpcMushroomElder", "Trưởng Làng Nấm", e.x, e.y);
            if (elderGo)
            {
                elderGo.transform.localScale = Vector3.one * HeroScale * 0.8f; // rigged at 1.4 m: a little taller than the heroes
                var actor = elderGo.GetComponent<NpcActor>();
                var prompt = WorldLabel(elderGo.transform, "TalkPrompt", 1.85f, "E · Nói chuyện", 28, Color.white, new Color(0.26f, 0.56f, 0.62f, 0.92f), new Vector2(300f, 54f), 41);
                prompt.transform.parent.parent.gameObject.SetActive(false);
                var bubble = WorldLabel(elderGo.transform, "SpeechBubble", 2.2f, "", 24, new Color(0.25f, 0.15f, 0.06f), new Color(1f, 0.96f, 0.86f, 0.95f), new Vector2(560f, 110f), 43);
                bubble.horizontalOverflow = HorizontalWrapMode.Wrap;
                bubble.transform.parent.parent.gameObject.SetActive(false);
                elderGo.AddComponent<NpcTalkSpot>().Bind(actor, "talk:mushroom_elder", "Trưởng Làng Nấm", prompt.transform.parent.parent.gameObject, bubble, new[]
                {
                    "Chào mừng {name} tới Làng Nấm! Quảng trường này là vùng an toàn, cứ nghỉ ngơi thoải mái.",
                    "Lũ Nấm Non ngoài cổng đông nghịch lắm. Hạ giúp ta vài đứa, rồi quay lại đây nhé.",
                    "Xa hơn về phía đông là cả Vương Quốc Nấm... và Vua Nấm Quỷ đang thức giấc.",
                });
                var mark = WorldLabel(elderGo.transform, "QuestMark", 2.6f, "", 64, new Color(1f, 0.85f, 0.25f), null, new Vector2(80f, 80f), 42);
                elderGo.AddComponent<NpcQuestMarker>().Bind("Trưởng Làng Nấm", mark);
            }

            foreach (var p in TownLayout.Ch1Village.Portals) Portal(play, p);
            Ch1FarmSetup(play);
            FinishWorld(scene, Ch1VillageScenePath, world, TownLayout.Ch1Village.MapId, TownLayout.Ch1Village.DisplayName, TownLayout.Ch1Village.Spawn,
                minX, maxX, 11f, AdventureMusicStage.MushroomKingdom);
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun) { sun.color = new Color(1f, 0.9f, 0.78f); EditorSceneManagerSave(scene); } // warm red-orange palette (concept)
        }

        // ================================================================== Map 2 · Đồng Cỏ Xanh
        /// <summary>
        /// Fresh green meadow under a blue sky (concept CH01_MAP02): a west-gate rest stop, then three tiers of grass
        /// terraces with a windmill on the top hill, wooden fences and flower bushes. Reuses the town windmill / banyan /
        /// planter / crates and the Map 1 modules (recoloured); new Meshy modules: ch1_wood_fence, ch1_flower_bush.
        /// Monsters: Slime Phấn Hoa (Meshy mesh + procedural hop/squash), Nấm Non (rigged, from Map 1).
        /// </summary>
        private static void BuildCh1GreenMeadow()
        {
            SceneTag = "ch1_m02";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");
            float minX = TownLayout.Ch1GreenMeadow.MinX, maxX = TownLayout.Ch1GreenMeadow.MaxX;
            Ground(art, solids, minX, maxX, stream: false);
            foreach (var d in TownLayout.Ch1GreenMeadow.Decks) Deck(art, solids, d, d.Top >= 7f ? "deck_high" : "deck");
            foreach (var c in TownLayout.Ch1GreenMeadow.Climbs) ClimbZone(art, play, c);

            Color sunny = new Color(1f, 0.97f, 0.82f), fresh = new Color(0.86f, 1f, 0.86f), dusk = new Color(1f, 0.9f, 0.86f);
            // ---- west rest stop (safe zone): gate, lamp, sign, a stall selling nothing (scenery), flowers
            C1Prop(art, "ch1_mushroom_lamp", -30f, 2.2f, 0.9f, false);
            HallGlow(art, "c1_lamp", new Vector3(-30.42f, 2.3f, 2.0f), 1.3f, new Color(1f, 0.7f, 0.35f) * 0.5f);
            Signboard(art, -33f, 2.6f, "Đồng Cỏ Xanh\n←\nLàng Nấm");
            Signboard(art, -22f, 2.6f, "Hết vùng\nan toàn\n→");
            C1Prop(art, "ch1_flower_bush", -26f, 2.0f, 1f, false);
            C1Prop(art, "ch1_flower_bush", -42f, 2.3f, 0.9f, true, tint: dusk);
            Prop(art, "crates", -37.5f, 3.2f, 1.6f, true);

            // ---- fences along the path at the back (low, modular, recoloured for variety)
            float[] fenceX = { -18f, -6f, 24f, 48f, 72f, 100f };
            for (int i = 0; i < fenceX.Length; i++)
                C1Prop(art, "ch1_wood_fence", fenceX[i], 3.0f + (i % 2) * 0.3f, 1f, i % 2 == 1, tint: i % 3 == 2 ? sunny : (Color?)null);
            // flower bushes scattered in front of and behind the path (the near ones stay low, they never hide the hero)
            float[] bushX = { -12f, 2f, 22f, 30f, 46f, 52f, 70f, 78f, 98f };
            for (int i = 0; i < bushX.Length; i++)
                C1Prop(art, "ch1_flower_bush", bushX[i], i % 3 == 0 ? 4.6f : 2.2f, i % 3 == 0 ? 1.25f : 0.85f, i % 2 == 0,
                    tint: i % 4 == 1 ? fresh : i % 4 == 3 ? sunny : (Color?)null);

            // ---- landmarks: windmill on the top hill, trees, a mushroom tree reminding of Map 1, far fields
            Prop(art, "windmill", 38f, 24f, 11.5f, true); // behind the hill (at z 14 it filled half the screen from the path)
            Prop(art, "banyan_tree", -2f, 15f, 11f, false);
            Prop(art, "banyan_tree", 76f, 13f, 12f, true);
            C1Prop(art, "ch1_giant_mushroom_tree", 60f, 18f, 0.9f, false, tint: fresh);
            Prop(art, "windmill", 104f, 26f, 10f, false);
            Prop(art, "planter", 14f, 2.6f, 1.2f, false, y: 4.5f);   // on the low terrace
            Prop(art, "planter", 88f, 2.6f, 1.2f, true, y: 6.5f);    // on the flower knoll
            Prop(art, "crates", 62f, 2.8f, 1.6f, false, y: 5f);
            Signboard(art, 101f, 2.6f, "Rừng Nấm\nSáng\n→\n(sắp mở)");

            C1Wildflowers(art, TownLayout.Ch1GreenMeadow.Decks, minX + 6f, maxX - 4f, 20261111, MapDetail());

            foreach (var p in TownLayout.Ch1GreenMeadow.Portals) Portal(play, p);
            Ch1FarmSetup(play, withPollenSlime: true);
            FinishWorld(scene, Ch1GreenMeadowScenePath, world, TownLayout.Ch1GreenMeadow.MapId, TownLayout.Ch1GreenMeadow.DisplayName,
                TownLayout.Ch1GreenMeadow.Spawn, minX, maxX, 12f, AdventureMusicStage.Ch1GreenMeadow);
            C1MeadowPanorama(); // its own far landscape (was the town's rice-terrace painting)
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun) { sun.color = new Color(1f, 0.97f, 0.9f); sun.intensity = 1.3f; }
            EditorSceneManagerSave(scene); // bright clear day (concept)
        }

        private static int MapDetail() => 2;

        /// <summary>
        /// Wildflowers (idea from AK Map Forge, PR #6): hundreds of small stems + blossoms batched into ONE vertex-colour mesh
        /// (one draw call, mobile-friendly), seeded so rebuilds are identical, standing on whichever surface is under them
        /// (ground or deck top) and kept off the walking lane (z ≥ 2.6) so they never cover the hero or monsters.
        /// </summary>
        private static void C1Wildflowers(Transform art, TownLayout.Deck[] decks, float x0, float x1, int seed, int detail)
        {
            var rng = new System.Random(seed);
            int count = detail == 1 ? 90 : detail == 2 ? 170 : 260;
            Color[] petals = { new Color(1f, 0.78f, 0.3f), new Color(0.97f, 0.5f, 0.64f), new Color(0.92f, 0.88f, 1f), new Color(0.98f, 0.98f, 0.62f), new Color(0.6f, 0.78f, 1f) };
            Color leaf = new Color(0.3f, 0.6f, 0.26f), centre = new Color(1f, 0.86f, 0.35f);
            Mesh flowers = WorldKit.Build($"c1_wildflowers_s{seed}_d{detail}", (v, c, t) =>
            {
                for (int i = 0; i < count; i++)
                {
                    float x = x0 + (float)rng.NextDouble() * (x1 - x0);
                    float z = 2.6f + (float)rng.NextDouble() * 3.4f, y = 0f;
                    foreach (var d in decks)
                        if (x > d.XMin + 0.4f && x < d.XMax - 0.4f && z < 4.2f) { y = d.Top; break; } // deck tops are ~1.6 m deep
                    float h = 0.22f + (float)rng.NextDouble() * 0.38f;
                    Color petal = petals[rng.Next(petals.Length)];
                    WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, y + h * 0.5f, z), new Vector3(0.045f, h, 0.045f), 0.015f, leaf, Quaternion.identity, 1);
                    // five-petal head: a flat disc of petals around a centre, tilted to the camera
                    Quaternion tilt = Quaternion.Euler(-60f + rng.Next(-15, 15), 0f, rng.Next(0, 72));
                    for (int k = 0; k < 5; k++)
                    {
                        Quaternion r = tilt * Quaternion.Euler(0f, 0f, k * 72f);
                        WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, y + h, z) + r * new Vector3(0f, 0.065f, 0f), new Vector3(0.07f, 0.1f, 0.025f), 0.02f, petal, r, 1);
                    }
                    WorldKit.AppendRoundedBox(v, c, t, new Vector3(x, y + h, z - 0.02f), new Vector3(0.05f, 0.05f, 0.04f), 0.02f, centre, tilt, 1);
                }
            });
            MeshObject(art, "Wildflowers_Batched", flowers, Structures, Vector3.zero, false);
        }

        /// <summary>Map 2's own far landscape (procedural, no paid generation): soft sky with clouds, three rolling green
        /// ridges with tree clumps and a tiny distant windmill — replaces the town's rice-terrace painting on this map.</summary>
        private static void C1MeadowPanorama()
        {
            const string name = "c1_m02_meadow_panorama_v3";
            float Ridge(float u, float baseV, float a1, float f1, float a2, float f2, float ph) => baseV + a1 * Mathf.Sin(u * f1 + ph) + a2 * Mathf.Sin(u * f2 + ph * 1.7f);
            Color sky0 = new Color(0.86f, 0.95f, 0.98f), sky1 = new Color(0.45f, 0.72f, 0.98f);
            Color[] hill = { new Color(0.62f, 0.78f, 0.66f), new Color(0.47f, 0.7f, 0.43f), new Color(0.33f, 0.6f, 0.3f) };
            // round tree crowns sitting on the two far ridges (seeded; v1 drew sine bands that read as squares)
            var rng = new System.Random(2026);
            var trees = new List<(float u, float v, float r, int ridge)>();
            for (int i = 0; i < 70; i++)
            {
                int ridge = 1 + i % 2;
                float tu = (float)rng.NextDouble();
                float crest = ridge == 1 ? Ridge(tu, 0.34f, 0.045f, 13f, 0.012f, 37f, 2.1f) : Ridge(tu, 0.45f, 0.035f, 7f, 0.01f, 29f, 4.3f);
                float r = (ridge == 1 ? 0.012f : 0.008f) * (0.7f + (float)rng.NextDouble() * 0.6f);
                trees.Add((tu, crest + r * 0.35f, r, ridge));
            }
            GeneratedTexture(name, 1024, (u, v) =>
            {
                // nearest ridge first
                float[] ridges = { Ridge(u, 0.24f, 0.05f, 9f, 0.015f, 41f, 0.4f), Ridge(u, 0.34f, 0.045f, 13f, 0.012f, 37f, 2.1f), Ridge(u, 0.45f, 0.035f, 7f, 0.01f, 29f, 4.3f) };
                for (int i = 0; i < 3; i++)
                {
                    bool crown = false;
                    foreach (var tr in trees)
                        if (tr.ridge == i && Mathf.Abs(u - tr.u) < tr.r * 0.6f && (new Vector2((u - tr.u) * 1.8f, v - tr.v)).sqrMagnitude < tr.r * tr.r) { crown = true; break; }
                    if (v > ridges[i] && !crown) continue;
                    float shade = Mathf.Clamp01((ridges[i] - v) / 0.12f);
                    Color col = crown ? hill[2 - i] * 0.8f : Color.Lerp(hill[2 - i] * 1.08f, hill[2 - i] * 0.88f, shade);
                    return Color.Lerp(col, sky0, 0.18f * i); // aerial perspective
                }
                // tiny far windmill on the farthest ridge (u ≈ 0.68)
                float wx = u - 0.68f, wy = v - (Ridge(0.68f, 0.45f, 0.035f, 7f, 0.01f, 29f, 4.3f) + 0.02f);
                if (Mathf.Abs(wx) < 0.004f && wy > -0.02f && wy < 0.035f) return new Color(0.78f, 0.82f, 0.86f);
                if (wy > 0.03f && wy < 0.05f && (Mathf.Abs(wx - (wy - 0.04f)) < 0.002f || Mathf.Abs(wx + (wy - 0.04f)) < 0.002f) && Mathf.Abs(wx) < 0.014f) return new Color(0.8f, 0.84f, 0.88f);
                float s = Mathf.Clamp01((v - 0.45f) / 0.55f);
                Color sky = Color.Lerp(sky0, sky1, Mathf.Pow(s, 0.8f));
                float cloud = Mathf.PerlinNoise(u * 9f, v * 18f) * Mathf.PerlinNoise(u * 3f + 5f, v * 6f);
                return Color.Lerp(sky, Color.white, Mathf.SmoothStep(0.22f, 0.36f, cloud) * 0.75f * Mathf.SmoothStep(0.5f, 0.7f, v));
            });
            var backdrop = GameObject.Find("Backdrop_Painting");
            if (!backdrop) throw new System.InvalidOperationException("Backdrop_Painting not found after FinishWorld");
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = CutoutMat($"{WorldDir}/Generated/{name}.png", 0f, fogAmount: 0f, cutoff: 0f);
        }

        /// <summary>Slime Phấn Hoa: the Meshy mesh (exported by Tools/blender/export_prop.py at 1 m) under a "Body" pivot that
        /// FarmMonsterView hops, squashes and breathes procedurally (no rig: it has no limbs).</summary>
        private static FarmMonsterView MeshMonster(Transform parent, string propName, string name, float scale, float barHeight)
        {
            string fbx = $"{C1PropDir}/{propName}/{propName}.fbx";
            if (!File.Exists(fbx)) { Debug.LogWarning("[CH1] monster mesh missing: " + propName); return null; }
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            GameObject vis = Prop(body, propName, 0f, 0f, 1f, false, 0f, C1PropDir);
            if (!vis) { Object.DestroyImmediate(root); return null; }
            vis.transform.localPosition = Vector3.zero; // Prop() already turned it to face the camera (3/4)
            vis.transform.localScale = Vector3.one * scale;
            var view = root.AddComponent<FarmMonsterView>();
            view.BindAnimated(body, vis.GetComponentsInChildren<Renderer>(), null, barHeight);
            return view;
        }

        private static void EditorSceneManagerSave(Scene scene) => UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scene.path);

        /// <summary>Farm client for Chapter 1: the meadow slimes stay as fallbacks, Nấm Non uses the rigged Meshy model.</summary>
        private static void Ch1FarmSetup(Transform play, bool withPollenSlime = false)
        {
            FarmSetup(play);
            var farm = play.GetComponentInChildren<FarmField>();
            Transform templates = farm.transform.Find("Templates");
            var list = new List<(string type, FarmMonsterView view)>();
            FarmMonsterView namNon = RiggedMonster(templates, "MonsterNamNon", "NamNon", 1.15f, 1.45f);
            if (namNon) list.Add(("young_mushroom", namNon));
            if (withPollenSlime)
            {
                FarmMonsterView pollen = MeshMonster(templates, "ch1_pollen_slime", "SlimePhanHoa", 1.05f, 1.35f);
                if (pollen) list.Add(("pollen_slime", pollen));
                else throw new FileNotFoundException("Slime Phấn Hoa mesh missing (export ch1_pollen_slime first) — no stand-in monster");
            }
            var so = new SerializedObject(farm);
            var arr = so.FindProperty("typedTemplates");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("type").stringValue = list[i].type;
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("view").objectReferenceValue = list[i].view;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform t in templates) t.gameObject.SetActive(false);
        }

        /// <summary>Template for a rigged monster prefab (NpcArtPipeline.Chapter1): root (FarmMonsterView) → Body (yaw / pop)
        /// → visual prefab with Animator states Idle / Walk / Attack / Hit / Death.</summary>
        private static FarmMonsterView RiggedMonster(Transform parent, string prefabName, string name, float scale, float barHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcArtPipeline.PrefabPath(prefabName));
            if (!prefab) { Debug.LogWarning("[CH1] monster prefab missing: " + prefabName); return null; }
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, body);
            vis.transform.localScale = Vector3.one * scale;
            var view = root.AddComponent<FarmMonsterView>();
            view.BindAnimated(body, vis.GetComponentsInChildren<Renderer>(), vis.GetComponentInChildren<Animator>(), barHeight);
            return view;
        }
    }
}
