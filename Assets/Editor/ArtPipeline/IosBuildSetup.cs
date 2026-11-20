using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// One-click iOS player setup: landscape only, iOS 14.0+, App Icon 1024 (Assets/Art/Branding/AppIcon_1024.png), launch image on the dark backdrop, bundle id, Point/uncompressed branding textures.
    ///   Unity -batchmode -nographics -quit -executeMethod AdventureKing.ArtPipeline.IosBuildSetup.ApplyFromCommandLine
    /// Build the .ipa from Unity Cloud Build (or Build Settings -> iOS -> Xcode) afterwards.
    /// </summary>
    public static class IosBuildSetup
    {
        public const string BundleId = "com.quocsam.adventurekingreborn";

        public static void ApplyFromCommandLine() { Apply(); EditorApplication.Exit(0); }

        [MenuItem("AdventureKing/Build/Apply iOS player settings")]
        public static void Apply()
        {
            foreach (string p in new[] { "Assets/Art/Branding/AppIcon_1024.png", "Assets/Art/Branding/LaunchScreen.png" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default; ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false;
                ti.alphaSource = TextureImporterAlphaSource.None; ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, BundleId);
            PlayerSettings.productName = "Adventure King Reborn";
            PlayerSettings.iOS.targetOSVersionString = "14.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.iOS.hideHomeButton = true;                     // the swipe indicator hides itself; SafeAreaFitter keeps the HUD clear of notch / Dynamic Island
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.SplashScreen.show = false;                       // the game shows its own logo instead of the Unity splash

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Branding/AppIcon_1024.png");
            if (icon)
            {
                var icons = PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.iOS.iOSPlatformIconKind.Application);
                foreach (var pi in icons) pi.SetTexture(icon);                              // Unity scales the one opaque 1024 master to every size
                PlayerSettings.SetPlatformIcons(UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.iOS.iOSPlatformIconKind.Application, icons);
                var marketing = PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.iOS.iOSPlatformIconKind.Marketing);
                foreach (var pi in marketing) pi.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.iOS.iOSPlatformIconKind.Marketing, marketing);
            }
            var launch = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Branding/LaunchScreen.png");
            if (launch)
            {
                PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhoneLandscapeImage);
                PlayerSettings.iOS.SetiPadLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPadImage);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[IOS] player settings applied: landscape, iOS 14.0, icon + launch screen set, bundle " + BundleId);
        }
    }
}
