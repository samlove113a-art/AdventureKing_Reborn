using System.Collections.Generic;
using System.Linq;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Chương 1 · Map 4-10, built from the generated layout (Tools/server/ch1_maps.py → Ch1MapData): multi-tier decks, ladders and ropes,
    /// the two gates, an explore point, a Mini Boss / Elite / Boss sign, and a per-map theme (light, far landscape, prop tints) so
    /// no two maps look alike. Reuses the Meshy props that already exist (no new credits).
    ///   Unity -batchmode -executeMethod AdventureKing.ArtPipeline.VillageSceneBuilder.BuildChapter1Maps3To10FromCommandLine
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        public static string Ch1ScenePath(string scene) => $"Assets/Scenes/Chapter1/{scene}.unity";

        public static void BuildChapter1Maps3To10FromCommandLine()
        {
            try
            {
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                BuildCh1GlowForest();
                foreach (Ch1MapData.Ch1Map m in Ch1MapData.All) BuildCh1Generated(m);
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private sealed class C1Theme
        {
            public Color Sky0, Sky1, Light, Tint1, Tint2, Tint3; public Color[] Ridge, Glow; public float Intensity = 1f; public int Seed; public bool Stars;
            public string Tree = "ch1_giant_mushroom_tree";
        }

        private static C1Theme ThemeOf(int index)
        {
            switch (index)
            {
                case 4: return new C1Theme { Sky0 = C(0xfb, 0xe0, 0xea), Sky1 = C(0x7f, 0xc4, 0xf0), Light = C(1f, 0.96f, 0.95f), Intensity = 1.3f, Tint1 = C(1f, 0.82f, 0.9f), Tint2 = C(1f, 0.95f, 0.7f), Tint3 = C(0.9f, 1f, 0.86f),
                    Ridge = new[] { C(0.62f, 0.52f, 0.7f), C(0.55f, 0.66f, 0.6f), C(0.5f, 0.78f, 0.55f) }, Glow = new[] { C(1f, 0.7f, 0.82f), C(1f, 0.9f, 0.6f), C(0.9f, 0.78f, 1f) }, Seed = 40404 };
                case 5: return new C1Theme { Sky0 = C(0.18f, 0.2f, 0.34f), Sky1 = C(0.05f, 0.06f, 0.14f), Light = C(0.62f, 0.78f, 1f), Intensity = 0.62f, Tint1 = C(0.6f, 0.9f, 1f), Tint2 = C(0.8f, 0.7f, 1f), Tint3 = C(0.5f, 1f, 0.9f),
                    Ridge = new[] { C(0.1f, 0.1f, 0.18f), C(0.14f, 0.15f, 0.26f), C(0.2f, 0.22f, 0.34f) }, Glow = new[] { C(0.5f, 0.9f, 1f), C(0.7f, 0.6f, 1f), C(0.4f, 1f, 0.8f) }, Seed = 50505, Stars = true };
                case 6: return new C1Theme { Sky0 = C(0.7f, 0.95f, 0.92f), Sky1 = C(0.3f, 0.62f, 0.8f), Light = C(0.9f, 1f, 0.98f), Intensity = 1.2f, Tint1 = C(0.6f, 1f, 0.85f), Tint2 = C(0.7f, 0.9f, 1f), Tint3 = C(0.85f, 1f, 0.8f),
                    Ridge = new[] { C(0.2f, 0.5f, 0.5f), C(0.28f, 0.62f, 0.58f), C(0.4f, 0.75f, 0.66f) }, Glow = new[] { C(0.5f, 1f, 0.9f), C(0.7f, 0.95f, 1f), C(0.8f, 1f, 0.8f) }, Seed = 60606 };
                case 7: return new C1Theme { Sky0 = C(0.4f, 0.28f, 0.52f), Sky1 = C(0.1f, 0.06f, 0.2f), Light = C(0.78f, 0.7f, 1f), Intensity = 0.66f, Tint1 = C(0.75f, 0.6f, 1f), Tint2 = C(0.6f, 0.5f, 0.85f), Tint3 = C(0.9f, 0.6f, 0.9f),
                    Ridge = new[] { C(0.1f, 0.06f, 0.18f), C(0.16f, 0.1f, 0.26f), C(0.24f, 0.16f, 0.36f) }, Glow = new[] { C(0.8f, 0.55f, 1f), C(1f, 0.7f, 0.9f), C(0.6f, 0.6f, 1f) }, Seed = 70707, Stars = true };
                case 8: return new C1Theme { Sky0 = C(0.7f, 0.82f, 0.4f), Sky1 = C(0.25f, 0.3f, 0.2f), Light = C(0.88f, 1f, 0.72f), Intensity = 0.85f, Tint1 = C(0.7f, 1f, 0.45f), Tint2 = C(0.8f, 0.55f, 1f), Tint3 = C(0.9f, 1f, 0.5f),
                    Ridge = new[] { C(0.16f, 0.2f, 0.1f), C(0.22f, 0.3f, 0.14f), C(0.3f, 0.4f, 0.2f) }, Glow = new[] { C(0.8f, 1f, 0.4f), C(0.8f, 0.5f, 1f), C(0.6f, 1f, 0.5f) }, Seed = 80808 };
                case 9: return new C1Theme { Sky0 = C(0.92f, 0.88f, 0.78f), Sky1 = C(0.46f, 0.55f, 0.66f), Light = C(1f, 0.96f, 0.86f), Intensity = 1.05f, Tint1 = C(0.82f, 0.82f, 0.78f), Tint2 = C(0.7f, 0.9f, 0.8f), Tint3 = C(0.95f, 0.85f, 0.7f),
                    Ridge = new[] { C(0.42f, 0.42f, 0.46f), C(0.52f, 0.52f, 0.54f), C(0.6f, 0.62f, 0.6f) }, Glow = new[] { C(0.7f, 1f, 0.9f), C(1f, 0.9f, 0.7f), C(0.8f, 0.8f, 1f) }, Seed = 90909 };
                default: return new C1Theme { Sky0 = C(0.55f, 0.18f, 0.3f), Sky1 = C(0.12f, 0.05f, 0.2f), Light = C(1f, 0.78f, 0.8f), Intensity = 0.8f, Tint1 = C(1f, 0.55f, 0.6f), Tint2 = C(0.85f, 0.55f, 1f), Tint3 = C(1f, 0.8f, 0.55f),
                    Ridge = new[] { C(0.16f, 0.06f, 0.14f), C(0.24f, 0.1f, 0.2f), C(0.34f, 0.14f, 0.26f) }, Glow = new[] { C(1f, 0.5f, 0.6f), C(0.9f, 0.5f, 1f), C(1f, 0.8f, 0.5f) }, Seed = 101010, Stars = true };
            }
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        private static void BuildCh1Generated(Ch1MapData.Ch1Map m)
        {
            SceneTag = "ch1_m" + m.Index.ToString("00");
            C1Theme th = ThemeOf(m.Index);
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");
            float minX = Ch1MapData.Ch1Map.MinX, maxX = Ch1MapData.Ch1Map.MaxX;
            Ground(art, solids, minX, maxX, stream: false);
            foreach (var d in m.Decks) Deck(art, solids, d, d.Top >= 7f ? "deck_high" : "deck");
            foreach (var c in m.Climbs) ClimbZone(art, play, c);

            // ---- west rest stop (safe zone): lamp, signs, gate back
            C1Prop(art, "ch1_mushroom_lamp", -30f, 2.2f, 0.9f, false);
            HallGlow(art, "c1_lamp", new Vector3(-30.42f, 2.3f, 2.0f), 1.3f, new Color(1f, 0.7f, 0.35f) * 0.5f);
            Signboard(art, -33f, 2.6f, m.DisplayName + "\n←\n" + m.Portals[0].DestinationName);
            Signboard(art, -22f, 2.6f, "Hết vùng\nan toàn\n→");
            Prop(art, "crates", -37.5f, 3.2f, 1.6f, true);

            // ---- scenery by theme: giant mushrooms in the back, bushes / fences / lamps along the path, glow lights
            for (int i = 0; i < 9; i++)
            {
                float x = -14f + i * 15.5f;
                Color tint = i % 3 == 0 ? th.Tint1 : i % 3 == 1 ? th.Tint2 : th.Tint3;
                C1Prop(art, th.Tree, x, 10f + (i % 3) * 3f, 0.85f + (i % 2) * 0.3f, i % 2 == 1, tint: tint);
                if (th.Intensity < 0.9f) HallGlow(art, "c1_glow" + i, new Vector3(x, 6f, 7f), 3.4f, tint * 0.4f);
            }
            for (int i = 0; i < 8; i++) C1Prop(art, "ch1_flower_bush", -6f + i * 14f, i % 2 == 0 ? 2.2f : 4.4f, 0.8f, i % 2 == 0, tint: i % 2 == 0 ? th.Tint1 : th.Tint3);
            switch (m.Index)
            {
                case 4: // Đồi Hoa: cherry-pink trees, fences, windmill far away, wildflowers
                    for (int i = 0; i < 5; i++) Prop(art, "banyan_tree", 4f + i * 24f, 14f + (i % 2) * 3f, 9f + (i % 3), i % 2 == 0);
                    for (int i = 0; i < 4; i++) C1Prop(art, "ch1_wood_fence", 10f + i * 26f, 3f, 1f, i % 2 == 1, tint: th.Tint2);
                    Prop(art, "windmill", 96f, 26f, 10f, false);
                    C1Wildflowers(art, m.Decks, minX + 6f, maxX - 4f, 4040, forgeDetail);
                    break;
                case 5: // Hang Nấm: lamps and crystal veins in the dark
                    for (int i = 0; i < 8; i++) { C1Prop(art, "ch1_mushroom_lamp", -2f + i * 14f, 2.6f, 0.8f, i % 2 == 0, tint: th.Tint1); HallGlow(art, "c1_cavelamp" + i, new Vector3(-2.4f + i * 14f, 2.9f, 2.0f), 2.2f, th.Tint1 * 0.45f); }
                    for (int i = 0; i < 6; i++) C1Prop(art, "ch1_crystal_fountain", 8f + i * 17f, 9f + (i % 2) * 3f, 0.55f, i % 2 == 0, tint: i % 2 == 0 ? th.Tint1 : th.Tint2);
                    for (int i = 0; i < 3; i++) Prop(art, "crates", 30f + i * 24f, 2.8f, 1.6f, i % 2 == 0);
                    break;
                case 6: // Thác Ngọc: waterfalls (crystal fountains), banyan roots, jade bushes
                    for (int i = 0; i < 3; i++) C1Prop(art, "ch1_crystal_fountain", 18f + i * 34f, 12f, 1.2f, false, tint: th.Tint1);
                    for (int i = 0; i < 4; i++) Prop(art, "banyan_tree", 2f + i * 30f, 15f, 10f, i % 2 == 0);
                    break;
                case 7: // Rừng U Tối: hanging lamps, twisted trees, fog
                    for (int i = 0; i < 6; i++) { C1Prop(art, "ch1_mushroom_lamp", 4f + i * 18f, 3f, 0.8f, i % 2 == 0, y: 5.6f, tint: th.Tint2); HallGlow(art, "c1_hang" + i, new Vector3(3.6f + i * 18f, 6.2f, 2.2f), 2.4f, th.Tint2 * 0.5f); }
                    for (int i = 0; i < 5; i++) Prop(art, "banyan_tree", 8f + i * 22f, 13f, 12f, i % 2 == 1);
                    break;
                case 8: // Đầm Lầy Nấm Độc: toxic glow, fences as rotten logs
                    for (int i = 0; i < 6; i++) { C1Prop(art, "ch1_giant_mushroom_tree", 6f + i * 17f, 5f, 0.45f, i % 2 == 0, tint: i % 2 == 0 ? th.Tint1 : th.Tint2); HallGlow(art, "c1_toxic" + i, new Vector3(6f + i * 17f, 3f, 4f), 2.4f, (i % 2 == 0 ? th.Tint1 : th.Tint2) * 0.4f); }
                    for (int i = 0; i < 4; i++) C1Prop(art, "ch1_wood_fence", 14f + i * 24f, 3.2f, 1f, i % 2 == 1, tint: C(0.55f, 0.5f, 0.35f));
                    break;
                case 9: // Khu Di Tích Cổ: grey pillars (fountains), mossy stones, crystal gate
                    for (int i = 0; i < 5; i++) C1Prop(art, "ch1_crystal_fountain", 6f + i * 20f, 6f + (i % 2) * 2f, 0.7f, i % 2 == 0, tint: th.Tint1);
                    for (int i = 0; i < 4; i++) Prop(art, "crates", 20f + i * 22f, 2.8f, 1.4f, i % 2 == 0);
                    C1Prop(art, "ch1_crystal_fountain", 66f, 6f, 0.9f, false, y: 11f, tint: th.Tint2);
                    break;
                default: // Đền Vua Nấm: lamps along the aisle, the throne dais
                    for (int i = 0; i < 9; i++) { C1Prop(art, "ch1_mushroom_lamp", -4f + i * 12f, 2.6f, 0.9f, i % 2 == 0, tint: th.Tint1); HallGlow(art, "c1_temple" + i, new Vector3(-4.4f + i * 12f, 2.9f, 2.0f), 2.6f, th.Tint1 * 0.5f); }
                    C1Prop(art, "ch1_crystal_fountain", 60f, 8f, 1.4f, false, tint: th.Tint2);
                    C1Prop(art, "ch1_mushroom_house", 86f, 7f, 0.9f, true, y: 7.5f, tint: C(0.5f, 0.3f, 0.45f));
                    break;
            }

            // ---- explore point: a glowing marker and a sign (the server fires the quest event when the hero walks in)
            float ey = m.ExploreY;
            Signboard(art, m.ExploreX, 2.6f, "✦ " + m.ExploreLabel + " ✦");
            HallGlow(art, "c1_explore", new Vector3(m.ExploreX, ey + 1.2f, 1.6f), 2.2f, th.Tint3 * 0.7f);
            // ---- Mini Boss / Elite / Boss arena marker
            if (!string.IsNullOrEmpty(m.BossType))
            {
                string label = m.BossType == "mushroom_king" ? "BOSS\nVua Nấm Quỷ" : m.BossType == "toxic_elite" ? "Tinh Anh\nNấm Độc" : m.BossType == "jade_guardian" ? "Mini Boss\nHộ Vệ Lam Thạch" : "Mini Boss\nNấm Hắc Ám";
                Signboard(art, m.BossX - 10f, 2.6f, label + "\n→");
                HallGlow(art, "c1_boss", new Vector3(m.BossX, 3f, 3f), 4.2f, th.Tint1 * 0.45f);
            }
            if (m.Index == 10) Signboard(art, 100f, 2.6f, "Cổng Chương 2\n(sắp mở)");

            foreach (var p in m.Portals) Portal(play, p);
            Ch1FarmSetup(play);
            FinishWorld(scene, Ch1ScenePath(m.Scene), world, m.MapId, m.DisplayName, Ch1MapData.Ch1Map.Spawn, minX, maxX, 14f, AdventureMusicStage.MushroomKingdom);
            C1ThemePanorama($"c1_m{m.Index:00}_panorama_v1", th);
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun) { sun.color = th.Light; sun.intensity = th.Intensity; }
            EditorSceneManagerSave(scene);
        }

        /// <summary>One far landscape per map from a palette (sky gradient, three ridges, silhouettes of tall mushrooms, optional spore stars).</summary>
        private static void C1ThemePanorama(string name, C1Theme th)
        {
            if (TryPixelPanorama()) return;   // pixel-art panorama when it exists

            float Ridge(float u, float baseV, float a1, float f1, float a2, float f2, float ph) => baseV + a1 * Mathf.Sin(u * f1 + ph) + a2 * Mathf.Sin(u * f2 + ph * 1.7f);
            var rng = new System.Random(th.Seed);
            var shrooms = new List<(float u, float baseV, float h, float w, int ridge, int tone)>();
            for (int i = 0; i < 30; i++)
            {
                int r = i % 3;
                float tu = (float)rng.NextDouble();
                float crest = Ridge(tu, 0.2f + r * 0.1f, 0.03f, 9f + r * 3f, 0.01f, 31f, 0.7f + r);
                float h = (0.2f - r * 0.04f) * (0.7f + (float)rng.NextDouble() * 0.6f);
                shrooms.Add((tu, crest - 0.01f, h, h * 0.24f, r, rng.Next(3)));
            }
            GeneratedTexture(name, 1024, (u, v) =>
            {
                float[] ridges = { Ridge(u, 0.2f, 0.03f, 9f, 0.01f, 31f, 0.7f), Ridge(u, 0.3f, 0.03f, 12f, 0.01f, 31f, 1.7f), Ridge(u, 0.4f, 0.03f, 15f, 0.01f, 31f, 2.7f) };
                for (int i = 0; i < 3; i++)
                {
                    foreach (var m in shrooms)
                    {
                        if (m.ridge != i) continue;
                        float dx = u - m.u, y = v - m.baseV;
                        if (y < 0f || y > m.h || Mathf.Abs(dx) > m.w * 1.7f) continue;
                        float capLo = m.h * 0.6f;
                        if (y < capLo && Mathf.Abs(dx) < m.w * 0.2f) return Color.Lerp(th.Ridge[2 - i], th.Glow[m.tone], 0.35f);
                        float cy = (y - capLo) / (m.h - capLo);
                        if (y >= capLo && Mathf.Abs(dx) < m.w * 1.6f * Mathf.Sqrt(Mathf.Max(0f, 1f - cy * cy))) return Color.Lerp(th.Glow[m.tone] * 0.85f, th.Sky0, 0.18f * i);
                    }
                    if (v > ridges[i]) continue;
                    return Color.Lerp(th.Ridge[2 - i] * (0.8f + 0.4f * Mathf.Clamp01((ridges[i] - v) / 0.1f)), th.Sky0, 0.14f * i);
                }
                Color sky = Color.Lerp(th.Sky0, th.Sky1, Mathf.Pow(Mathf.Clamp01((v - 0.4f) / 0.6f), 0.7f));
                if (th.Stars)
                {
                    float spore = Mathf.PerlinNoise(u * 60f, v * 60f);
                    sky = Color.Lerp(sky, th.Glow[0], Mathf.SmoothStep(0.86f, 0.93f, spore) * Mathf.SmoothStep(0.55f, 0.85f, v) * 0.6f);
                }
                else
                {
                    float cloud = Mathf.PerlinNoise(u * 8f + 3f, v * 16f) * Mathf.PerlinNoise(u * 2.5f, v * 5f);
                    sky = Color.Lerp(sky, Color.white, Mathf.SmoothStep(0.22f, 0.36f, cloud) * 0.55f * Mathf.SmoothStep(0.5f, 0.7f, v));
                }
                return sky;
            });
            var backdrop = GameObject.Find("Backdrop_Painting");
            if (!backdrop) throw new System.InvalidOperationException("Backdrop_Painting not found after FinishWorld");
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = CutoutMat($"{WorldDir}/Generated/{name}.png", 0f, fogAmount: 0f, cutoff: 0f);
        }
    }
}
