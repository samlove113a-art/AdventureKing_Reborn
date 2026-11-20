using System.Collections;
using System.IO;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>Hot update: catalog newer than installed -> banner offers the update -> bundles download with progress -> installed version stored. Uses a local folder as the "remote host".</summary>
    public sealed class HotUpdateTests
    {
        [TearDown] public void Down() { HotUpdateManager.UrlOverride = null; PlayerPrefs.DeleteKey("hot_version"); }

        [UnityTest]
        public IEnumerator New_Catalog_Shows_The_Update_Button_Downloads_With_Progress_And_Marks_The_Version()
        {
            string host = Path.Combine(Path.GetTempPath(), "ak_host_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(host);
            File.WriteAllBytes(Path.Combine(host, "map_c1_m11"), new byte[300_000]);
            File.WriteAllBytes(Path.Combine(host, "skin_dream"), new byte[120_000]);
            string uri = new System.Uri(host).AbsoluteUri;
            File.WriteAllText(Path.Combine(host, "catalog.json"), JsonUtility.ToJson(new HotCatalog
            {
                version = 7, notes = "Map 11 và skin mới.",
                bundles = new[] { new HotBundle { name = "map_c1_m11", url = uri + "/map_c1_m11", hash = "h1", size = 300_000, kind = "map" }, new HotBundle { name = "skin_dream", url = uri + "/skin_dream", hash = "h2", size = 120_000, kind = "skin" } },
            }));
            PlayerPrefs.SetInt("hot_version", 3);
            foreach (string n in new[] { "map_c1_m11", "skin_dream" }) { try { File.Delete(HotUpdateManager.LocalPath(n)); File.Delete(HotUpdateManager.LocalPath(n) + ".hash"); } catch (IOException) { } }
            HotUpdateManager.UrlOverride = uri;

            var banner = HotUpdateBanner.Create(null, null);
            var m = HotUpdateManager.Instance;
            float end = Time.realtimeSinceStartup + 10f;
            while (m.Status != HotUpdateManager.State.UpdateAvailable && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(HotUpdateManager.State.UpdateAvailable, m.Status, m.Message);
            Assert.AreEqual(420_000, m.TotalBytes);
            yield return null; yield return null;
            Assert.IsTrue(banner.Visible, "the update panel is shown");
            StringAssert.Contains("cập nhật", banner.Text.ToLower());
            Assert.IsTrue(banner.UpdateButton.gameObject.activeSelf, "Cập nhật button");

            banner.UpdateButton.onClick.Invoke();
            float maxProgress = 0f; end = Time.realtimeSinceStartup + 20f;
            while (m.Status == HotUpdateManager.State.Downloading && Time.realtimeSinceStartup < end) { maxProgress = Mathf.Max(maxProgress, m.Progress); yield return null; }
            Assert.AreEqual(HotUpdateManager.State.Done, m.Status, m.Message);
            Assert.AreEqual(7, m.InstalledVersion);
            Assert.IsTrue(File.Exists(HotUpdateManager.LocalPath("map_c1_m11")) && File.Exists(HotUpdateManager.LocalPath("skin_dream")), "bundles stored on the device");
            yield return null;
            Assert.AreEqual(1f, m.Progress);

            // second launch: nothing left to download
            m.Check();
            end = Time.realtimeSinceStartup + 10f;
            while (m.Status == HotUpdateManager.State.Checking && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(HotUpdateManager.State.UpToDate, m.Status);
            Object.Destroy(banner.gameObject); Object.Destroy(m.gameObject);
            Directory.Delete(host, true);
        }
    }
}
