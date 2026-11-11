using System;
using System.Collections.Generic;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// AK Map Forge: deterministic scenery and hand-authored collision lanes.
    /// Generates only Map 2; map connectivity, rewards and spawn positions remain server-owned.
    /// This partial class reuses the production world builder instead of creating a second scene stack.
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        public const string Ch1GreenMeadowScenePath = "Assets/Scenes/Chapter1/AK_C1_M02.unity";

        public static void ForgeBuildMap2(int seed, int detail)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before generating a map.");
            EnsureLayer(OneWayLayer, "OneWayPlatform");
            BuildCh1GreenMeadow(seed, Mathf.Clamp(detail, 1, 3));
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[AK Map Forge] Map 2 generated; save, compile and play-test with the server before release.");
        }

        private static void BuildCh1GreenMeadow(int seed, int detail)
        {
            SceneTag = "ch1_m02";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery");
            Transform solids = Group(world, "Collision");
            Transform play = Group(world, "Gameplay");
            var layout = TownLayout.Ch1GreenMeadow;

            // Fixed gameplay geometry: portal positions and monster platforms MUST match the server config.
            Ground(art, solids, TownLayout.Ch1GreenMeadow.MinX, TownLayout.Ch1GreenMeadow.MaxX, false);
            foreach (var deck in TownLayout.Ch1GreenMeadow.Decks)
                Deck(art, solids, deck, deck.Top >= 7f ? "deck_high" : "deck");
            foreach (var climb in TownLayout.Ch1GreenMeadow.Climbs)
                ClimbZone(art, play, climb);

            // Existing 3D assets (no Meshy credits). Z layers create actual 2.5D depth.
            Prop(art, "windmill", 53f, 28f, 14f, false);
            Prop(art, "windmill", -16f, 45f, 7f, true);
            Prop(art, "banyan_tree", -18f, 16f, 10f, false);
            Prop(art, "banyan_tree", 18f, 16f, 11f, true);
            Prop(art, "banyan_tree", 79f, 16f, 9.5f, false);
            Prop(art, "bamboo", 101f, 13f, 8f, true);
            Prop(art, "bamboo", 38f, 12f, 7f, false);
            Prop(art, "planter", -13f, 2.6f, 1.45f, false);
            Prop(art, "planter", 22f, 2.6f, 1.5f, true);
            Prop(art, "planter", 89f, 2.7f, 1.6f, false);
            C1Prop(art, "ch1_giant_mushroom_tree", 32f, 16f, .9f, true,
                tint: new Color(.8f, 1f, .73f));
            C1Prop(art, "ch1_giant_mushroom_tree", 96f, 18f, .82f, false,
                tint: new Color(1f, .98f, .77f));
            C1Prop(art, "ch1_mushroom_lamp", -25f, 3.3f, .75f);
            C1Prop(art, "ch1_mushroom_lamp", 99f, 3.3f, .75f, true);

            MapForgeFences(art);
            MapForgeFlowers(art, seed, detail);
            Signboard(art, -33f, 2.6f, "Làng Nấm\n←\nCổng về");
            Signboard(art, 99.5f, 2.6f, "Rừng Nấm\nSáng\n(sắp mở)");

            foreach (var portal in TownLayout.Ch1GreenMeadow.Portals)
                Portal(play, portal);
            MapForgeMonsters(play);

            // Reuse proven hero/controller/HUD/camera/audio pipeline, with a distinct area title.
            FinishWorld(scene, Ch1GreenMeadowScenePath, world,
                TownLayout.Ch1GreenMeadow.MapId, TownLayout.Ch1GreenMeadow.DisplayName,
                TownLayout.Ch1GreenMeadow.Spawn, TownLayout.Ch1GreenMeadow.MinX,
                TownLayout.Ch1GreenMeadow.MaxX, 12f, AdventureMusicStage.Exploration);

            MapForgePaintBackdrop();
            var sun = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (sun) { sun.color = new Color(.98f, 1f, .86f); sun.intensity = 1.25f; }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, Ch1GreenMeadowScenePath);
        }

        private static void MapForgeMonsters(Transform play)
        {
            FarmSetup(play);
            var farm = play.GetComponentInChildren<FarmField>();
            var templates = farm.transform.Find("Templates");
            // A distinct pollen slime visual made entirely in Unity while Meshy model is pending approval.
            var pollen = Slime(templates, "SlimePhanHoa",
                new Color(1f, .89f, .40f), new Color(1f, .49f, .72f), false);
            var namNon = RiggedMonster(templates, "MonsterNamNon", "NamNonMap2", 1.15f, 1.45f);
            var list = new List<(string type, FarmMonsterView view)> { ("pollen_slime", pollen) };
            if (namNon) list.Add(("young_mushroom", namNon));
            var serialized = new SerializedObject(farm);
            var templatesProperty = serialized.FindProperty("typedTemplates");
            templatesProperty.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                var item = templatesProperty.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("type").stringValue = list[i].type;
                item.FindPropertyRelative("view").objectReferenceValue = list[i].view;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform t in templates) t.gameObject.SetActive(false);
        }

        private static void MapForgeFences(Transform art)
        {
            var material = Structures;
            var fence = WorldKit.Build("m02_oak_fences", (verts, colors, tris) =>
            {
                Color oak = new Color(.67f, .43f, .25f), edge = new Color(.46f, .26f, .15f);
                foreach (var span in new[] { new Vector2(-21f, -11f), new Vector2(18f, 26f), new Vector2(78f, 88f) })
                {
                    for (float x = span.x; x <= span.y + .01f; x += 2f)
                        WorldKit.AppendRoundedBox(verts, colors, tris,
                            new Vector3(x, .75f, 3.4f), new Vector3(.28f, 1.45f, .28f),
                            .08f, edge, Quaternion.identity, 1);
                    for (float y = .62f; y < 1.25f; y += .55f)
                        WorldKit.AppendRoundedBox(verts, colors, tris,
                            new Vector3((span.x + span.y) * .5f, y, 3.4f),
                            new Vector3(span.y - span.x + .3f, .17f, .17f),
                            .045f, oak, Quaternion.identity, 1);
                }
            });
            MeshObject(art, "AKForge_OakFenceBatches", fence, material, Vector3.zero);
        }

        private static void MapForgeFlowers(Transform art, int seed, int detail)
        {
            var rng = new System.Random(seed);
            int count = detail == 1 ? 65 : detail == 2 ? 125 : 190;
            var flowers = WorldKit.Build("m02_wildflower_seed_" + seed + "_d" + detail,
                (verts, colors, tris) =>
            {
                Color[] petals = {
                    new Color(1f, .73f, .32f), new Color(.96f, .52f, .66f),
                    new Color(.92f, .87f, 1f), new Color(.95f, 1f, .60f)
                };
                var leaf = new Color(.30f, .62f, .28f);
                for (int i = 0; i < count; i++)
                {
                    float x = -21f + (float)rng.NextDouble() * 123f;
                    float y = 0f;
                    // Raised-platform flowers stay with the geometry; monsters can still spawn on the decks.
                    foreach (var d in TownLayout.Ch1GreenMeadow.Decks)
                        if (x > d.XMin + .5f && x < d.XMax - .5f) { y = d.Top; break; }
                    float height = .20f + (float)rng.NextDouble() * .35f;
                    float z = 2.8f + (float)rng.NextDouble() * 2.7f;
                    Color blossom = petals[rng.Next(petals.Length)];
                    WorldKit.AppendRoundedBox(verts, colors, tris,
                        new Vector3(x, y + height * .5f, z),
                        new Vector3(.045f, height, .045f), .015f,
                        leaf, Quaternion.identity, 1);
                    WorldKit.AppendRoundedBox(verts, colors, tris,
                        new Vector3(x, y + height, z),
                        new Vector3(.18f, .14f, .14f), .06f,
                        blossom, Quaternion.Euler(0f, 0f, rng.Next(-24, 25)), 1);
                }
            });
            MeshObject(art, "AKForge_WildflowersBatched", flowers, Structures, Vector3.zero, false);
        }

        /// <summary>Unique hand-coded meadow panorama. No paid image generation, no flat placeholder background.</summary>
        private static void MapForgePaintBackdrop()
        {
            const string name = "m02_meadow_panorama_v1";
            GeneratedTexture(name, 1024, (u, v) =>
            {
                float ridgeFar = .48f + .04f * Mathf.Sin(u * 15f) + .018f * Mathf.Sin(u * 39f);
                float ridgeNear = .31f + .055f * Mathf.Sin(u * 22f + 1.2f) + .018f * Mathf.Cos(u * 57f);
                if (v < ridgeNear)
                    return Color.Lerp(new Color(.20f, .47f, .28f), new Color(.43f, .68f, .34f), Mathf.Clamp01(v / ridgeNear));
                if (v < ridgeFar)
                    return Color.Lerp(new Color(.46f, .66f, .47f), new Color(.69f, .79f, .64f), (v - ridgeNear) / (ridgeFar - ridgeNear));
                float sky = Mathf.Clamp01((v - ridgeFar) / (1f - ridgeFar));
                return Color.Lerp(new Color(.83f, .95f, .90f), new Color(.42f, .72f, 1f), sky);
            });
            var backdrop = GameObject.Find("Backdrop_Painting");
            if (!backdrop) throw new InvalidOperationException("World backdrop not found after FinishWorld.");
            var renderer = backdrop.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CutoutMat(WorldDir + "/Generated/" + name + ".png", 0f, 0f, 0f);
        }
    }
}
