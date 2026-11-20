using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using AdventureKing.Gameplay;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Builds the hot-update bundles + catalog.json. Put assets for a new map / event / skin / VFX into a folder, give the folder (or any asset) an AssetBundle name in the Inspector (e.g. "map_c1_m11"),
    /// then run the menu. Output: HotUpdate/&lt;platform&gt;/ (upload the whole folder to Firebase Storage / your host; set its URL in Assets/Resources/HotUpdateUrl.txt).
    ///   Unity -batchmode -nographics -quit -executeMethod AdventureKing.ArtPipeline.HotUpdateBuilder.BuildFromCommandLine -target iOS -baseUrl https://host/path
    /// </summary>
    public static class HotUpdateBuilder
    {
        public static void BuildFromCommandLine()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            string url = a.SkipWhile(x => x != "-baseUrl").Skip(1).FirstOrDefault() ?? "";
            Build(url);
            EditorApplication.Exit(0);
        }

        [MenuItem("AdventureKing/Build/Hot Update Bundles (iOS)")]
        public static void BuildMenu() => Build(File.Exists("Assets/Resources/HotUpdateUrl.txt") ? File.ReadAllText("Assets/Resources/HotUpdateUrl.txt").Trim() : "");

        public static void Build(string baseUrl)
        {
            string dir = "HotUpdate/iOS";
            Directory.CreateDirectory(dir);
            var manifest = BuildPipeline.BuildAssetBundles(dir, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.iOS);
            if (manifest == null) { Debug.LogError("[HOTUPDATE] build failed (no named bundles or no iOS module installed)"); return; }
            int version = 1;
            string catPath = dir + "/catalog.json";
            if (File.Exists(catPath)) version = JsonUtility.FromJson<HotCatalog>(File.ReadAllText(catPath)).version + 1;
            var cat = new HotCatalog { version = version, notes = "Nội dung mới đã sẵn sàng." };
            cat.bundles = manifest.GetAllAssetBundles().Select(n =>
            {
                string p = dir + "/" + n;
                return new HotBundle { name = n, url = baseUrl.TrimEnd('/') + "/" + n, size = new FileInfo(p).Length, kind = n.Split('_')[0], hash = manifest.GetAssetBundleHash(n).ToString() };
            }).ToArray();
            File.WriteAllText(catPath, JsonUtility.ToJson(cat, true));
            Debug.Log($"[HOTUPDATE] catalog v{version}: {cat.bundles.Length} bundles -> {dir}");
        }
    }
}
