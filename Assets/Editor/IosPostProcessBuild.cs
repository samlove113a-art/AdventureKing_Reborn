#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Xcode Info.plist for the online test server: App Transport Security lets the app talk plain HTTP
    /// (NSAllowsArbitraryLoads) and the server IP gets its own insecure-HTTP exception. Owner decision 2026-11-21 (test server on http://100.91.134.127:5080).
    /// </summary>
    public static class IosPostProcessBuild
    {
        public const string ServerHost = "100.91.134.127";

        [PostProcessBuild(100)]
        public static void AllowHttpServer(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var doc = new PlistDocument();
            doc.ReadFromFile(plistPath);
            PlistElementDict ats = doc.root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
            PlistElementDict domains = ats.CreateDict("NSExceptionDomains");
            PlistElementDict host = domains.CreateDict(ServerHost);
            host.SetBoolean("NSExceptionAllowsInsecureHTTPLoads", true);
            host.SetBoolean("NSIncludesSubdomains", false);
            doc.WriteToFile(plistPath);
        }
    }
}
#endif
