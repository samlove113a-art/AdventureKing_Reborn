using System;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// AK Map Forge v1: one-click, deterministic, 2.5D Map 2 generator.
    /// The generator NEVER chooses rewards, portal destinations, monster spawn lanes or collision geometry at random.
    /// Those live in TownLayout and in the authoritative ASP.NET server.
    /// </summary>
    public sealed class AKMapForgeWindow : EditorWindow
    {
        private const string SeedKey = "AK.MapForge.Seed";
        private const string DetailKey = "AK.MapForge.Detail";

        private int seed = 20261111;
        private int detail = 2;
        private Vector2 scroll;

        [MenuItem("AdventureKing/Map Forge/Open AK Map Forge", priority = 150)]
        public static void Open()
        {
            var window = GetWindow<AKMapForgeWindow>("AK Map Forge");
            window.minSize = new Vector2(430f, 370f);
            window.Show();
        }

        private void OnEnable()
        {
            seed = EditorPrefs.GetInt(SeedKey, seed);
            detail = EditorPrefs.GetInt(DetailKey, detail);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("AK MAP FORGE", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("AdventureKing_Reborn · Chương 1 · Map 2", EditorStyles.boldLabel);
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Xây Đồng Cỏ Xanh với địa hình nhiều tầng, thang/dây, phối cảnh, cây 3D, hoa, quái, minimap, camera và cổng về Làng Nấm. Không sử dụng API, Meshy hoặc credit.",
                MessageType.Info);
            GUILayout.Space(8f);

            seed = EditorGUILayout.IntField(new GUIContent("Decoration Seed", "Chỉ thay đổi trang trí; không thay vị trí quái, cổng hay collider"), seed);
            detail = EditorGUILayout.IntSlider(new GUIContent("Chi tiết hoa / Mobile", "1 = nhẹ, 3 = nhiều hoa; dùng mesh gộp để giảm draw call"), detail, 1, 3);
            EditorPrefs.SetInt(SeedKey, seed);
            EditorPrefs.SetInt(DetailKey, detail);

            GUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("TẠO MAP 2 + NỐI CỔNG MAP 1", GUILayout.Height(44f)))
                    Generate();
                GUILayout.Space(4f);
                if (GUILayout.Button("MỞ MAP 2", GUILayout.Height(30f)))
                    OpenMap();
                if (GUILayout.Button("KIỂM TRA CẤU HÌNH MAP 2", GUILayout.Height(30f)))
                    Validate();
            }
            GUILayout.Space(9f);
            EditorGUILayout.HelpBox(
                "An toàn: Map 1 không được dựng lại. Chỉ thêm cổng phía đông vào scene cũ; bản sao Map 1 trước khi chỉnh được cất trong AKMapForgeBackups tại thư mục project. Không chạm lễ cưới, shop, pet, Gold/KC.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Giới hạn: Map 2 mới chỉ được tạo khi bấm nút. Chưa kiểm thử trực tiếp trong Unity trên máy bạn; cần chạy server và PlayMode trước khi coi là hoàn chỉnh. Quái Slime Phấn Hoa là model thủ tục tạm thời (DRAFT), có thể thay bằng Meshy sau.",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private static bool Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private void Generate()
        {
            if (!Prepare()) return;
            try
            {
                if (!System.IO.File.Exists(VillageSceneBuilder.Ch1VillageScenePath))
                    throw new System.IO.FileNotFoundException("Không thấy scene Map 1 - hãy mở đúng project AdventureKing_Reborn.");
                VillageSceneBuilder.ForgeBuildMap2(seed, detail);
                bool connected = VillageSceneBuilder.ForgeLinkMap1();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(VillageSceneBuilder.Ch1GreenMeadowScenePath, OpenSceneMode.Single);
                Debug.Log("[AK Map Forge] Map 2 ready in Editor. Map 1 east portal " + (connected ? "added/updated" : "already present") + ".");
                EditorUtility.DisplayDialog("AK Map Forge",
                    "Đã dựng Map 2 và nối cổng Map 1 trong scene Unity.\n\nTiếp theo: chạy server (dotnet run), vào game và thử cổng. Kết quả chưa được kiểm thử trên máy này.", "OK");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("AK Map Forge - lỗi dựng map",
                    "Dừng để bảo vệ dữ liệu. Mở Console để xem lỗi cụ thể.\n" + error.Message, "OK");
            }
        }

        private void OpenMap()
        {
            if (!Prepare()) return;
            if (!System.IO.File.Exists(VillageSceneBuilder.Ch1GreenMeadowScenePath))
            {
                EditorUtility.DisplayDialog("AK Map Forge", "Map 2 chưa được tạo. Bấm TẠO MAP 2 trước.", "OK");
                return;
            }
            EditorSceneManager.OpenScene(VillageSceneBuilder.Ch1GreenMeadowScenePath, OpenSceneMode.Single);
        }

        private void Validate()
        {
            bool scene = System.IO.File.Exists(VillageSceneBuilder.Ch1GreenMeadowScenePath);
            bool map1 = System.IO.File.Exists(VillageSceneBuilder.Ch1VillageScenePath);
            bool meta = TownLayout.For(TownLayout.Ch1GreenMeadow.MapId).Scene == TownLayout.Ch1GreenMeadow.Scene;
            bool travel = AdventureWorldSession.SceneForMap.TryGetValue(TownLayout.Ch1GreenMeadow.MapId, out string s) && s == TownLayout.Ch1GreenMeadow.Scene;
            bool build = Array.Exists(EditorBuildSettings.scenes, entry =>
                entry.enabled && entry.path == VillageSceneBuilder.Ch1GreenMeadowScenePath);
            bool okay = scene && map1 && meta && travel && build;
            string report = "Map 1: " + (map1 ? "OK" : "THIẾU") +
                "\nMap 2 scene: " + (scene ? "OK" : "CHƯA TẠO") +
                "\nTownLayout: " + (meta ? "OK" : "THIẾU") +
                "\nSceneForMap: " + (travel ? "OK" : "THIẾU") +
                "\nBuild Profiles/Scenes: " + (build ? "OK" : "CHƯA ĐĂNG KÝ") +
                "\n\nLưu ý: kiểm tra cấu hình KHÔNG thay thế test server, va chạm và mỹ thuật.";
            EditorUtility.DisplayDialog(okay ? "AK Map Forge - cấu hình ổn" : "AK Map Forge - cần kiểm tra", report, "OK");
        }
    }
}
