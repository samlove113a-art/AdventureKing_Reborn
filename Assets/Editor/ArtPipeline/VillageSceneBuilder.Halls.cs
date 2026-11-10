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

        /// <summary>Indoor light: warm key through the windows, cool fill, a few warm point lights (one per area; the
        /// Low graphics setting drops additional lights), dark interior fog colour instead of sky.</summary>
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
            foreach (var l in lamps)
            {
                var go = new GameObject("HallLight");
                go.transform.SetParent(art, false);
                go.transform.position = new Vector3(l.x, l.y, l.z);
                var pl = go.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = l.c;
                pl.range = 8f;
                pl.intensity = 0.9f;
                pl.shadows = LightShadows.None;
                pl.renderMode = LightRenderMode.Auto;
            }
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
            for (int i = 0; i < n; i++) HallProp(art, "gh_wall_arch", start + i * module, wallZ, 7.5f, straight: true);
            for (int i = 0; i <= n; i++) HallProp(art, "gh_pillar", start + (i - 0.5f) * module, pillarZ, 7f, straight: true);

            // Areas, west → east: return portal, reception + quests (Alliance Captain), ranking board, council table,
            // contribution altar, the Guild Master's throne between two banners.
            HallProp(art, "gh_reception_counter", -11f, 2.8f, 3.2f);
            HallProp(art, "gh_crystal_brazier", -15f, 3.6f, 3.2f);
            HallProp(art, "gh_ranking_board", -4f, 3.2f, 3.4f, flip: true);
            HallProp(art, "gh_council_set", 3.5f, 3.9f, 1.5f); // 4 m deep: front chairs end at z ≈ 1.8, behind the walking lane
            HallProp(art, "gh_crystal_brazier", 7f, 3.6f, 3.2f, flip: true);
            HallProp(art, "gh_donation_altar", 10.5f, 3.0f, 3.2f);
            HallProp(art, "gh_banner", 14f, 3.8f, 4.2f);
            HallProp(art, "gh_throne", 17.5f, 3.4f, 2.8f, flip: true);
            HallProp(art, "gh_banner", 21f, 3.8f, 4.2f, flip: true);
            HallProp(art, "hall_chandelier", -7.5f, 3.5f, 2.4f, y: 6.2f);
            HallProp(art, "hall_chandelier", 10.5f, 3.5f, 2.4f, y: 6.2f);
            var captain = new GameObject("NpcSpot_DoiTruongLienMinh").transform; // Đội Trưởng Liên Minh (model pending)
            captain.SetParent(play, false);
            captain.position = new Vector3(-9f, 0f, 0f);

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
            for (int i = 0; i < n; i++) HallProp(art, "wh_wall_window", start + i * module, wallZ, 7.5f, straight: true);
            for (int i = 0; i <= n; i++) HallProp(art, "wh_marble_pillar", start + (i - 0.5f) * module, pillarZ, 7f, straight: true);

            // West → east: return portal, guest benches both sides of the aisle, the flower arch and ceremony stage
            // (Chủ Hôn), the musician with the harp, banquet tables and the wedding cake.
            for (int i = 0; i < 3; i++) HallProp(art, "wh_guest_bench", -12f + i * 3.2f, 3.0f, 1.3f);
            HallProp(art, "wh_flower_arch", 2f, 3.4f, 4.4f);
            HallProp(art, "wh_altar_stage", 2f, 3.2f, 3.4f);
            HallProp(art, "wh_harp", 8.5f, 3.2f, 2.6f, flip: true);
            HallProp(art, "wh_banquet_table", 14f, 3.0f, 1.4f);
            HallProp(art, "wh_wedding_cake", 19f, 2.8f, 2.0f, flip: true);
            HallProp(art, "wh_banquet_table", 22.5f, 3.0f, 1.4f, flip: true);
            HallProp(art, "hall_chandelier", -6f, 3.5f, 2.4f, y: 6.2f);
            HallProp(art, "hall_chandelier", 12f, 3.5f, 2.4f, y: 6.2f);
            foreach (var (role, x) in new[] { ("ChuHon", 2f), ("NhacCong", 7f) })
            {
                var spot = new GameObject("NpcSpot_" + role).transform; // models pending
                spot.SetParent(play, false);
                spot.position = new Vector3(x, 0f, 0f);
            }

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
