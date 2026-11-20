using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AdventureKing.Gameplay
{
    [Serializable] public sealed class HotBundle { public string name, url, hash; public long size; public string kind; }
    [Serializable] public sealed class HotCatalog { public int version; public string notes; public HotBundle[] bundles = new HotBundle[0]; }

    /// <summary>
    /// In-app content updates WITHOUT reinstalling the .ipa. A small catalog.json on any static host (Firebase Storage, a CDN, your VPS) lists remote AssetBundles (new maps, events, skins, VFX).
    /// On the login screen the manager reads the catalog; if its version is newer than the installed one it reports the size, the player presses Cập nhật, a progress bar fills while the
    /// bundles are downloaded to the device (hash-checked, resumable per bundle) and kept for the next launches. Bundles are built by Tools: AdventureKing/Build/Hot Update Bundles.
    /// Remote URL: Resources/HotUpdateUrl.txt (one line); empty = no hot update (the game works fully offline from the installed build).
    /// </summary>
    public sealed class HotUpdateManager : MonoBehaviour
    {
        public enum State { Idle, Checking, UpToDate, UpdateAvailable, Downloading, Done, Failed }

        public static HotUpdateManager Instance { get; private set; }
        public State Status { get; private set; } = State.Idle;
        public HotCatalog Remote { get; private set; }
        public float Progress { get; private set; }
        public long TotalBytes { get; private set; }
        public string Message { get; private set; } = "";
        public int InstalledVersion => PlayerPrefs.GetInt("hot_version", 0);

        private static string Dir => Path.Combine(Application.persistentDataPath, "hotupdate");
        /// <summary>Tests / tools can point the manager at another host.</summary>
        public static string UrlOverride;
        private string CatalogUrl
        {
            get { if (!string.IsNullOrEmpty(UrlOverride)) return UrlOverride.TrimEnd('/') + "/catalog.json"; var t = Resources.Load<TextAsset>("HotUpdateUrl"); string u = t ? t.text.Trim() : ""; return u.Length == 0 ? "" : u.TrimEnd('/') + "/catalog.json"; }
        }

        private void Awake() { Instance = this; }

        public static string LocalPath(string bundleName) => Path.Combine(Dir, bundleName);

        /// <summary>Loads an installed bundle (null when it was never downloaded).</summary>
        public static AssetBundle Load(string bundleName)
        {
            string p = LocalPath(bundleName);
            return File.Exists(p) ? AssetBundle.LoadFromFile(p) : null;
        }

        public void Check() { if (Status != State.Checking && Status != State.Downloading) StartCoroutine(CheckRoutine()); }

        private IEnumerator CheckRoutine()
        {
            Status = State.Checking; Message = "Đang kiểm tra bản cập nhật...";
            string url = CatalogUrl;
            if (url.Length == 0) { Status = State.UpToDate; Message = ""; yield break; }
            using var req = UnityWebRequest.Get(url + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());   // never cached
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { Status = State.UpToDate; Message = ""; yield break; }   // offline: just play
            HotCatalog c = null;
            try { c = JsonUtility.FromJson<HotCatalog>(req.downloadHandler.text); } catch (Exception) { }
            if (c == null || c.bundles == null) { Status = State.UpToDate; yield break; }
            Remote = c;
            TotalBytes = 0;
            foreach (HotBundle b in c.bundles) if (!HasBundle(b)) TotalBytes += b.size;
            if (c.version > InstalledVersion || TotalBytes > 0) { Status = State.UpdateAvailable; Message = $"Có bản cập nhật mới ({TotalBytes / 1048576f:0.0} MB). {c.notes}"; }
            else { Status = State.UpToDate; Message = ""; }
        }

        private bool HasBundle(HotBundle b)
        {
            string p = LocalPath(b.name);
            return File.Exists(p) && File.Exists(p + ".hash") && File.ReadAllText(p + ".hash") == b.hash;
        }

        /// <summary>Called by the Cập nhật button.</summary>
        public void StartUpdate() { if (Status == State.UpdateAvailable || Status == State.Failed) StartCoroutine(DownloadRoutine()); }

        private IEnumerator DownloadRoutine()
        {
            Status = State.Downloading; Progress = 0f;
            Directory.CreateDirectory(Dir);
            long done = 0, total = Mathf.Max(1, (int)Math.Min(int.MaxValue, TotalBytes));
            foreach (HotBundle b in Remote.bundles)
            {
                if (HasBundle(b)) continue;
                string target = LocalPath(b.name), tmp = target + ".part";
                using var req = new UnityWebRequest(b.url, "GET") { downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, timeout = 120 };
                var op = req.SendWebRequest();
                while (!op.isDone) { Progress = Mathf.Clamp01((done + (long)req.downloadedBytes) / (float)total); Message = $"Đang tải {b.name}..."; yield return null; }
                if (req.result != UnityWebRequest.Result.Success) { Status = State.Failed; Message = "Tải thất bại, hãy thử lại."; yield break; }
                if (File.Exists(target)) File.Delete(target);
                File.Move(tmp, target);
                File.WriteAllText(target + ".hash", b.hash);
                done += b.size;
            }
            PlayerPrefs.SetInt("hot_version", Remote.version); PlayerPrefs.Save();
            Progress = 1f; Status = State.Done; Message = "Đã cập nhật xong!";
        }
    }
}
