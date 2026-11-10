using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Player settings for Windows, Android and iOS (landscape, IL2CPP ARM64, development-only HTTP) and
    /// one-click internal builds. The Android/iOS development builds embed Resources/ServerEndpoint.txt pointing at
    /// this PC's LAN address so a phone on the same Wi-Fi reaches the local test server
    /// (`dotnet run --project Server/AdventureKing.Server --environment Lan`). No store builds, no signing secrets.
    /// </summary>
    public static class PlatformBuilds
    {
        private const string EndpointFile = "Assets/Resources/ServerEndpoint.txt";
        private static readonly string[] Scenes = { "Assets/Scenes/AK_Entry.unity", "Assets/Scenes/AK_Village.unity", "Assets/Scenes/AK_Meadow.unity",
            "Assets/Scenes/AK_GuildHall.unity", "Assets/Scenes/AK_WeddingHall.unity" };

        [MenuItem("AdventureKing/Build/Apply Player Settings (PC, Android, iOS)")]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = "SAM";
            PlayerSettings.productName = "Adventure King Reborn";
            const string id = "com.sam.adventurekingreborn";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, id);

            // Landscape only on phones/tablets.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; // Unity 6.6 minimum (24 is deprecated)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            // Plain HTTP only in development builds (LAN test server); release builds require HTTPS.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;

            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[BUILD] player settings applied");
        }

        public static string LanAddress()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = a.Address.ToString();
                    if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172.")) return ip;
                }
            }
            return null;
        }

        [MenuItem("AdventureKing/Build/Windows (internal)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AdventureKingReborn.exe", lan: false);

        [MenuItem("AdventureKing/Build/Android APK (development, LAN server)")]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/Android/AdventureKingReborn-dev.apk", lan: true);

        [MenuItem("AdventureKing/Build/iOS Xcode project (development, LAN server)")]
        public static void BuildIos() => Build(BuildTarget.iOS, "Builds/iOS", lan: true);

        public static void BuildWindowsFromCommandLine() => RunCli(BuildWindows);
        public static void BuildAndroidFromCommandLine() => RunCli(BuildAndroid);
        public static void BuildIosFromCommandLine() => RunCli(BuildIos);

        private static void RunCli(System.Action a)
        {
            try { a(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Build(BuildTarget target, string path, bool lan)
        {
            ApplySettings();
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException(target + " Build Support is not installed for this Unity editor.");
            bool wroteEndpoint = false;
            try
            {
                if (lan)
                {
                    string ip = LanAddress() ?? throw new BuildFailedException("No LAN IPv4 address found for the phone to reach.");
                    Directory.CreateDirectory(Path.GetDirectoryName(EndpointFile));
                    File.WriteAllText(EndpointFile, $"http://{ip}:5080/api/v1\n");
                    AssetDatabase.ImportAsset(EndpointFile);
                    wroteEndpoint = true;
                }
                var opts = new BuildPlayerOptions
                {
                    scenes = Scenes.Where(File.Exists).ToArray(), locationPathName = path, target = target,
                    options = lan ? BuildOptions.Development : BuildOptions.None,
                };
                var report = BuildPipeline.BuildPlayer(opts);
                Debug.Log($"[BUILD] {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors -> {path}");
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new BuildFailedException(target + " build " + report.summary.result);
            }
            finally
            {
                // The repo default stays the PC loopback server.
                if (wroteEndpoint) { AssetDatabase.DeleteAsset(EndpointFile); }
            }
        }
    }
}
