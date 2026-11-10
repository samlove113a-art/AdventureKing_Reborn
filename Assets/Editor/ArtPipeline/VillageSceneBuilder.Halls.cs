using System.Collections.Generic;
using System.IO;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Phase D interiors: Sảnh Bang Hội (Guild Hall) and Sảnh Kết Hôn (Wedding Hall). Walkable side-view halls on the
    /// same gameplay plane as the town (z = 0), built from Meshy 3D models (Tools/meshy/make_hall_*.py →
    /// Tools/blender/export_prop.py → Assets/Art/World/Hall/Props). Safe zones: no monsters, portals back to the town.
    /// A model that is not generated yet is skipped (never replaced by a placeholder block) and its spot is marked
    /// HallSpot_* for the next build.
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        public const string GuildHallScenePath = "Assets/Scenes/AK_GuildHall.unity";
        public const string WeddingHallScenePath = "Assets/Scenes/AK_WeddingHall.unity";
        public const string HallPropDir = WorldDir + "/Hall/Props";
        private const string HallTexDir = WorldDir + "/Hall/Textures";

        private static GameObject HallProp(Transform parent, string name, float x, float z, float height, bool flip = false, float y = 0f, bool straight = false)
        {
            if (!File.Exists($"{HallPropDir}/{name}/{name}.fbx"))
            {
                // Not generated yet: leave a marker for the next build, never a stand-in block.
                var spot = new GameObject("HallSpot_" + name).transform;
                spot.SetParent(parent, false);
                spot.position = new Vector3(x, y, z);
                Debug.LogWarning($"[HALL] model not generated yet: {name} (spot kept at x={x})");
                return null;
            }
            // straight = face the camera squarely (walls, pillars); otherwise the usual three-quarter turn
            return Prop(parent, name, x, z, height, flip, y, HallPropDir, straight ? (flip ? -14f : 14f) : 0f);
        }

        /// <summary>Meshy NPC with name plate, "E · Nói chuyện" prompt, speech bubble and lines (no quest event).</summary>
        private static (NpcActor actor, NpcTalkSpot talk) HallNpc(Transform play, string prefab, string display, float x, float y, float z, string[] lines)
        {
            GameObject go = PlaceNpc(play, prefab, display, x, y);
            if (!go) return (null, null);
            go.transform.position = new Vector3(x, y, z);
            var actor = go.GetComponent<NpcActor>();
            var prompt = WorldLabel(go.transform, "TalkPrompt", 1.62f, "E · Nói chuyện", 28, Color.white, new Color(0.26f, 0.56f, 0.62f, 0.92f), new Vector2(300f, 54f), 41);
            prompt.transform.parent.parent.gameObject.SetActive(false);
            var bubble = WorldLabel(go.transform, "SpeechBubble", 1.95f, "", 24, new Color(0.25f, 0.15f, 0.06f), new Color(1f, 0.96f, 0.86f, 0.95f), new Vector2(560f, 110f), 43);
            bubble.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubble.transform.parent.parent.gameObject.SetActive(false);
            var talk = go.AddComponent<NpcTalkSpot>();
            talk.Bind(actor, "", display, prompt.transform.parent.parent.gameObject, bubble, lines);
            return (actor, talk);
        }

        /// <summary>Soft additive glow (brazier crystals, candles) — no real-time light.</summary>
        private static void HallGlow(Transform parent, string key, Vector3 at, float size, Color c)
        {
            Material m = VfxMat("hall_glow_" + key, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/soft_dot.png"), c);
            var q = Prim(PrimitiveType.Quad, parent, "Glow_" + key, at, new Vector3(size, size, 1f), m, shadows: false);
            var pulse = q.AddComponent<AmbientMotion>();
            pulse.bobHeight = 0.03f;
            pulse.bobSpeed = 1.6f;
        }

        /// <summary>
        /// Two-tier hall walls: window modules edge to edge with pillars on the seams, and an upper gallery of the same
        /// modules so the wide cinematic shots never see the sky above a 7.5 m wall. The gameplay camera never frames the
        /// upper tier (it is frustum-culled during play: no cost there).
        /// </summary>
        private static void HallWalls(Transform art, string wall, string pillar, float start, int n, float module, float wallZ, float pillarZ)
        {
            for (int tier = 0; tier < 2; tier++)
            {
                float y = tier * 7.3f;
                for (int i = 0; i < n; i++) HallProp(art, wall, start + i * module, wallZ + tier * 0.2f, 7.5f, straight: true, y: y);
                for (int i = 0; i <= n; i++) HallProp(art, pillar, start + (i - 0.5f) * module, pillarZ + tier * 0.2f, 7f, straight: true, y: y);
            }
        }

        /// <summary>Heights of the surfaces under (x, z) on a placed prop (all hits, low → high), measured with a
        /// temporary mesh collider: lets NPCs stand on the stage and the rings sit on the cushion.</summary>
        private static float[] SurfaceHeights(GameObject prop, float x, float z)
        {
            var cols = new List<MeshCollider>();
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                cols.Add(mc);
            }
            Physics.SyncTransforms();
            var ys = new List<float>();
            foreach (var h in Physics.RaycastAll(new Vector3(x, 30f, z), Vector3.down, 40f))
                if (h.collider is MeshCollider mc && cols.Contains(mc)) ys.Add(h.point.y); // only this prop's surfaces
            foreach (var c in cols) Object.DestroyImmediate(c);
            ys.Sort();
            return ys.ToArray();
        }

        /// <summary>Gold wedding ring (generated torus, smooth normals) — a small detail, no Meshy credit.</summary>
        private static Mesh RingMesh()
        {
            const string path = "Assets/Art/World/Hall/Meshes/wedding_ring.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            const int seg = 36, side = 12;
            const float R = 0.09f, r = 0.016f;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
                for (int j = 0; j <= side; j++)
                {
                    float a = i * Mathf.PI * 2f / seg, b = j * Mathf.PI * 2f / side;
                    var c = new Vector3(Mathf.Cos(a) * R, 0f, Mathf.Sin(a) * R);
                    var d = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    v.Add(c + d * r); n.Add(d);
                }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    t.AddRange(new[] { a0, a1, a0 + 1, a0 + 1, a1, a1 + 1 });
                }
            var m = new Mesh { name = "wedding_ring" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Tiled floor material from a Meshy texture (tile size in metres).</summary>
        private static Material HallFloorMat(string key, string tex, Vector2 tiles, Color tint)
        {
            string path = $"{MatDir}/{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("AdventureKing/Toon")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AdventureKing/Toon");
            string texPath = $"{HallTexDir}/{tex}.png";
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti && (ti.wrapMode != TextureWrapMode.Repeat || ti.maxTextureSize != 1024))
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetTextureScale("_BaseMap", tiles);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_ShadeColor", new Color(0.7f, 0.68f, 0.8f));
            m.SetFloat("_OutlineWidth", 0f);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Interior shell: collision, tiled floor, a stone skirting along the front edge with a gold trim.</summary>
        private static void HallFloor(Transform art, Transform solids, float minX, float maxX, string tex, Color skirting, Color trim)
        {
            Solid(solids, "Ground", minX - 2f, maxX + 2f, 0f, 4f, false);
            Solid(solids, "WallWest", minX - 3f, minX - 1f, 40f, 48f, false);
            Solid(solids, "WallEast", maxX + 1f, maxX + 3f, 40f, 48f, false);
            float x0 = minX - 10f, x1 = maxX + 10f, w = x1 - x0;
            // The camera (z -10.5, pitched down 5°) sees the floor down to z ≈ -5: extend it past that so the bottom of
            // the view is floor, not an empty band below the front edge.
            const float zFront = -7.5f, zBack = 7.6f, tile = 2.6f;
            float depth = zBack - zFront;
            var floor = Prim(PrimitiveType.Quad, art, "HallFloor", new Vector3((x0 + x1) * 0.5f, 0f, (zFront + zBack) * 0.5f),
                new Vector3(w, depth, 1f), HallFloorMat("hall_floor_" + tex, tex, new Vector2(w / tile, depth / tile), new Color(0.8f, 0.8f, 0.84f)), new Vector3(90f, 0f, 0f), shadows: false);
            floor.GetComponent<MeshRenderer>().receiveShadows = true;
            Prim(PrimitiveType.Cube, art, "Skirting", new Vector3((x0 + x1) * 0.5f, -0.9f, zFront + 0.25f), new Vector3(w, 1.8f, 0.5f), Mat("hall_skirting_" + tex, skirting), shadows: false);
            Prim(PrimitiveType.Cube, art, "SkirtingTrim", new Vector3((x0 + x1) * 0.5f, -0.06f, zFront + 0.02f), new Vector3(w, 0.12f, 0.08f), Mat("hall_trim_" + tex, trim, trim * 0.25f), shadows: false);
        }

        /// <summary>Indoor light: softer warm key through the windows, interior ambient colours, dark interior fog colour
        /// instead of sky. (The lamp list marks where warm light sources are; see the note inside.)</summary>
        private static void HallLighting(Color ambientSky, Color ambientEquator, Color ambientGround, Color fog, (float x, float y, float z, Color c)[] lamps, Transform art)
        {
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun)
            {
                sun.intensity = 0.72f; // indoors: the pale stone washed out to white at the town's 1.2
                sun.color = new Color(1f, 0.9f, 0.78f);
                sun.transform.rotation = Quaternion.Euler(38f, -24f, 0f);
                sun.shadowStrength = 0.55f;
            }
            RenderSettings.ambientSkyColor = ambientSky * 0.8f;
            RenderSettings.ambientEquatorColor = ambientEquator * 0.8f;
            RenderSettings.ambientGroundColor = ambientGround * 0.8f;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 90f;
            // No point lights: the project's URP asset (AK_URP) renders additional lights at no quality level (mobile
            // budget), so warmth comes from the key light, the ambient colours and additive glow sprites (HallGlow).
            _ = lamps;
        }

        // ================================================================== Sảnh Bang Hội
        private static void BuildGuildHall()
        {
            SceneTag = "guildhall";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");
            float minX = TownLayout.GuildHall.MinX, maxX = TownLayout.GuildHall.MaxX;
            HallFloor(art, solids, minX, maxX, "gh_floor", new Color(0.78f, 0.8f, 0.84f), new Color(0.95f, 0.78f, 0.36f));

            // Back wall: stained-glass arch modules (6.3 m wide) edge to edge; carved pillars stand on the seams.
            const float wallZ = 6.6f, pillarZ = 5.4f, module = 6.2f;
            int n = Mathf.CeilToInt((maxX - minX + 20f) / module);
            float start = (minX + maxX) * 0.5f - (n - 1) * module * 0.5f;
            HallWalls(art, "gh_wall_arch", "gh_pillar", start, n, module, wallZ, pillarZ);

            // Areas, west → east: return portal, reception + quests (Alliance Captain), ranking board, council table,
            // contribution altar, the Guild Master's throne between two banners.
            HallProp(art, "gh_crystal_brazier", -15.5f, 3.4f, 3.0f);
            HallProp(art, "gh_reception_counter", -11f, 2.9f, 3.2f);
            HallProp(art, "gh_ranking_board", -4f, 3.2f, 3.4f, flip: true);
            HallProp(art, "gh_council_set", 3.5f, 3.9f, 1.5f); // 4 m deep: front chairs end at z ≈ 1.8, behind the walking lane
            HallProp(art, "gh_crystal_brazier", 7.4f, 3.4f, 3.0f, flip: true);
            HallProp(art, "gh_donation_altar", 10.5f, 3.0f, 3.2f);
            HallProp(art, "gh_banner", 14f, 3.8f, 4.2f);
            HallProp(art, "gh_throne", 17.5f, 3.4f, 2.8f, flip: true);
            HallProp(art, "gh_banner", 21f, 3.8f, 4.2f, flip: true);
            // Chandeliers hang low enough to be in the frame (the view top at their depth is ~5.3 m).
            HallProp(art, "hall_chandelier", -7.5f, 2.6f, 2.0f, y: 3.6f);
            HallProp(art, "hall_chandelier", 10.5f, 2.6f, 2.0f, y: 3.6f);
            foreach (float bx in new[] { -15.5f, 7.4f }) HallGlow(art, "gh_crystal", new Vector3(bx, 2.75f, 3.1f), 1.6f, new Color(0.45f, 0.95f, 1f) * 0.5f);
            var (captainActor, captainTalk) = HallNpc(play, "NpcCaptain", "Đội Trưởng Liên Minh", -9f, 0f, 1.6f, new[]
            {
                "Chào {name}! Ta là Đội Trưởng Liên Minh. Lập bang, xin vào bang hay quản lý bang đều ở quầy này.",
                "Bảng xếp hạng bang ở bên phải. Ngai Bang Chủ dành cho người dẫn dắt cả liên minh!",
                "Trong sảnh này, các thành viên cùng bang thấy nhau ở khu sinh hoạt riêng của bang.",
            });
            if (captainActor) captainActor.gameObject.AddComponent<GuildCaptain>().Bind(captainTalk);

            HallLighting(new Color(0.62f, 0.66f, 0.82f), new Color(0.66f, 0.62f, 0.6f), new Color(0.36f, 0.32f, 0.3f), new Color(0.14f, 0.16f, 0.24f),
                new[] { (-11f, 4f, 0.5f, new Color(1f, 0.82f, 0.6f)), (3.5f, 4.5f, 0.5f, new Color(0.7f, 0.95f, 1f)), (17.5f, 4.5f, 0.5f, new Color(1f, 0.85f, 0.6f)) }, art);

            foreach (var p in TownLayout.GuildHall.Portals) Portal(play, p);
            FinishWorld(scene, GuildHallScenePath, world, TownLayout.GuildHall.MapId, TownLayout.GuildHall.DisplayName, TownLayout.GuildHall.Spawn,
                minX, maxX, CamMinY, AdventureMusicStage.Village);
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam) { cam.backgroundColor = new Color(0.14f, 0.16f, 0.24f); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, GuildHallScenePath); }
        }

        // ================================================================== Sảnh Kết Hôn
        private static void BuildWeddingHall()
        {
            SceneTag = "weddinghall";
            Scene scene = NewWorldScene(out Transform world);
            Transform art = Group(world, "Scenery"), solids = Group(world, "Collision"), play = Group(world, "Gameplay");
            float minX = TownLayout.WeddingHall.MinX, maxX = TownLayout.WeddingHall.MaxX;
            HallFloor(art, solids, minX, maxX, "gh_floor", new Color(0.95f, 0.93f, 0.9f), new Color(0.96f, 0.8f, 0.5f));

            // Aisle carpet along the walking line (Meshy blush-pink peach-blossom texture).
            Prim(PrimitiveType.Quad, art, "AisleCarpet", new Vector3(2f, 0.012f, 0.2f), new Vector3(maxX - minX - 6f, 2.4f, 1f),
                HallFloorMat("hall_carpet", "wh_carpet", new Vector2((maxX - minX - 6f) / 2.4f, 1f), Color.white), new Vector3(90f, 0f, 0f), shadows: false);

            const float wallZ = 6.6f, pillarZ = 5.4f, module = 6.2f;
            int n = Mathf.CeilToInt((maxX - minX + 20f) / module);
            float start = (minX + maxX) * 0.5f - (n - 1) * module * 0.5f;
            HallWalls(art, "wh_wall_window", "wh_marble_pillar", start, n, module, wallZ, pillarZ);

            // West → east: return portal, guest benches both sides of the aisle, the flower arch and ceremony stage
            // (Chủ Hôn), the musician with the harp, banquet tables and the wedding cake.
            HallProp(art, "wh_flower_arch", -15f, 2.6f, 4.4f);                // entrance of the aisle
            for (int i = 0; i < 3; i++)
            {
                HallProp(art, "wh_guest_bench", -11.5f + i * 2.6f, 2.4f, 1.3f); // two rows of guests behind the aisle
                HallProp(art, "wh_guest_bench", -11f + i * 2.6f, 4.2f, 1.3f);
            }
            // Round marble stage with the ring cushion and canopy. NOTE: its floor was painted near-black by Meshy and was
            // repainted as marble in wh_altar_stage_baseColor.png (original kept in ArtSource/Meshy/raw/wh_altar_stage_model).
            GameObject stageGo = HallProp(art, "wh_altar_stage", 2f, 3.4f, 3.4f);
            float stageFloor = 0.5f, cushionTop = 1.1f;
            if (stageGo)
            {
                float[] behind = SurfaceHeights(stageGo, 2f, 4.3f);
                if (behind.Length > 0) stageFloor = behind[0];
                float[] centre = SurfaceHeights(stageGo, 2f, 3.4f);
                foreach (float y in centre) if (y > stageFloor + 0.3f && y < 2.2f) cushionTop = y; // highest surface below the canopy
                Debug.Log($"[HALL] stage floor {stageFloor:0.00} m, cushion top {cushionTop:0.00} m");
                // Two gold rings resting on the cushion + a soft sparkle (the "Trao nhẫn" close-up).
                Material gold = Mat("wedding_ring_gold", new Color(1f, 0.82f, 0.36f), new Color(0.35f, 0.24f, 0.05f));
                for (int i = 0; i < 2; i++)
                {
                    var ring = new GameObject("WeddingRing" + i);
                    ring.transform.SetParent(art, false);
                    ring.transform.position = new Vector3(2f - 0.07f + i * 0.14f, cushionTop + 0.035f, 3.4f + (i == 0 ? -0.02f : 0.03f));
                    ring.transform.rotation = Quaternion.Euler(i == 0 ? 72f : 64f, i == 0 ? -20f : 25f, 0f); // leaning against each other
                    ring.AddComponent<MeshFilter>().sharedMesh = RingMesh();
                    var mr = ring.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = gold;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                HallGlow(art, "ring_sparkle", new Vector3(2f, cushionTop + 0.18f, 3.25f), 0.7f, new Color(1f, 0.9f, 0.6f) * 0.45f);
            }
            HallProp(art, "wh_harp", 8.6f, 3.0f, 2.6f, flip: true);
            HallProp(art, "wh_banquet_table", 14f, 3.0f, 1.4f);
            HallProp(art, "wh_wedding_cake", 18.6f, 2.8f, 2.0f, flip: true);
            HallProp(art, "wh_banquet_table", 22.5f, 3.0f, 1.4f, flip: true);
            HallProp(art, "hall_chandelier", -6f, 2.6f, 2.0f, y: 3.6f);
            HallProp(art, "hall_chandelier", 12f, 2.6f, 2.0f, y: 3.6f);
            // The Chủ Hôn stands ON the stage behind the ring pedestal, facing the guests.
            var (officiant, officiantTalk) = HallNpc(play, "NpcOfficiant", "Chủ Hôn", 2f, stageFloor, 4.3f, new[]
            {
                "Chào {name}! Ta sẽ cho con xem diễn tập nghi thức cưới của Sảnh Kết Hôn.",
            });
            var (musician, _) = HallNpc(play, "NpcMusician", "Nhạc Công", 7.2f, 0f, 1.6f, new[]
            {
                "Một bản nhạc cho đôi uyên ương nhé! ♪",
                "Lễ cưới thật sẽ có cả nhạc riêng cho cô dâu chú rể đấy.",
            });
            var petalGo = new GameObject("PetalFall", typeof(ParticleSystem));
            petalGo.transform.SetParent(art, false);
            petalGo.transform.position = new Vector3(0f, 9f, 2f);
            var petals = petalGo.AddComponent<PetalFall>();
            var director = new GameObject("CinematicDirector").AddComponent<CinematicDirector>();
            director.transform.SetParent(play, false);
            var dso = new SerializedObject(director);
            dso.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-Bold.ttf");
            dso.ApplyModifiedPropertiesWithoutUndo();
            var ceremony = director.gameObject.AddComponent<WeddingCeremony>();
            var cso = new SerializedObject(ceremony);
            cso.FindProperty("officiantTalk").objectReferenceValue = officiantTalk;
            cso.FindProperty("officiant").objectReferenceValue = officiant;
            cso.FindProperty("musician").objectReferenceValue = musician;
            cso.FindProperty("petals").objectReferenceValue = petals;
            cso.FindProperty("glassMaterial").objectReferenceValue = Mat("ceremony_glass", new Color(1f, 0.86f, 0.45f), new Color(0.3f, 0.2f, 0.05f)); // gilded goblet: reads against the pale walls
            cso.FindProperty("wineMaterial").objectReferenceValue = Mat("ceremony_wine", new Color(0.78f, 0.12f, 0.24f), new Color(0.2f, 0.02f, 0.05f));
            cso.FindProperty("goldMaterial").objectReferenceValue = Mat("wedding_ring_gold", new Color(1f, 0.82f, 0.36f), new Color(0.35f, 0.24f, 0.05f));
            cso.FindProperty("steelMaterial").objectReferenceValue = Mat("ceremony_steel", new Color(0.86f, 0.9f, 0.96f), new Color(0.15f, 0.17f, 0.2f));
            cso.FindProperty("cakeMaterial").objectReferenceValue = Mat("ceremony_cake", new Color(1f, 0.93f, 0.9f), new Color(0.12f, 0.08f, 0.08f));
            cso.ApplyModifiedPropertiesWithoutUndo();

            HallLighting(new Color(0.86f, 0.78f, 0.84f), new Color(0.8f, 0.72f, 0.7f), new Color(0.46f, 0.4f, 0.38f), new Color(0.24f, 0.18f, 0.24f),
                new[] { (-8f, 4f, 0.5f, new Color(1f, 0.82f, 0.72f)), (2f, 5f, 0.5f, new Color(1f, 0.78f, 0.82f)), (16f, 4f, 0.5f, new Color(1f, 0.86f, 0.66f)) }, art);

            foreach (var p in TownLayout.WeddingHall.Portals) Portal(play, p);
            FinishWorld(scene, WeddingHallScenePath, world, TownLayout.WeddingHall.MapId, TownLayout.WeddingHall.DisplayName, TownLayout.WeddingHall.Spawn,
                minX, maxX, CamMinY, AdventureMusicStage.Wedding);
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam) { cam.backgroundColor = new Color(0.24f, 0.18f, 0.24f); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, WeddingHallScenePath); }
        }
    }
}
