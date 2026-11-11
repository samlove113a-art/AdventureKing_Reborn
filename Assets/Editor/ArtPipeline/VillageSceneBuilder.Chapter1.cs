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
                BuildCh1GreenMeadow(20261111, 2);
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Chapter maps registered after the hub / meadow / halls (see RegisterBuildScenes).</summary>
        private static readonly string[] ChapterScenePaths = { Ch1VillageScenePath, Ch1GreenMeadowScenePath };

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
            Signboard(art, 84f, 2.6f, "Đồng Cỏ\nXanh\n→\nCổng Pha Lê");

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

        private static void EditorSceneManagerSave(Scene scene) => UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scene.path);

        /// <summary>Farm client for Chapter 1: the meadow slimes stay as fallbacks, Nấm Non uses the rigged Meshy model.</summary>
        private static void Ch1FarmSetup(Transform play)
        {
            FarmSetup(play);
            var farm = play.GetComponentInChildren<FarmField>();
            Transform templates = farm.transform.Find("Templates");
            var list = new List<(string type, FarmMonsterView view)>();
            FarmMonsterView namNon = RiggedMonster(templates, "MonsterNamNon", "NamNon", 1.15f, 1.45f);
            if (namNon) list.Add(("young_mushroom", namNon));
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
