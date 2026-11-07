using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Creates and assigns the project's URP asset (mobile-conscious): one shadowed main light,
    /// 1 cascade, soft shadows, MSAA 4x, HDR off. Assigned to Graphics and every Quality level.
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string Folder = "Assets/Settings/Rendering";
        private const string AssetPath = Folder + "/AK_URP.asset";
        private const string RendererPath = Folder + "/AK_URP_Renderer.asset";

        [MenuItem("AdventureKing/Art/Setup URP")]
        public static UniversalRenderPipelineAsset Setup()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (!asset)
            {
                Directory.CreateDirectory(Folder);
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var so = new SerializedObject(asset);
            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("[URP] missing " + n); }
            void I(string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Debug.LogWarning("[URP] missing " + n); }
            void B(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("[URP] missing " + n); }
            B("m_MainLightShadowsSupported", true);
            I("m_MainLightShadowmapResolution", 2048);
            B("m_SoftShadowsSupported", true);
            F("m_ShadowDistance", 40f);
            I("m_ShadowCascadeCount", 1);
            I("m_MSAA", 4);
            B("m_SupportsHDR", false);
            I("m_AdditionalLightsRenderingMode", 0); // disabled: main light only (mobile budget)
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[URP] Render pipeline assigned: " + AssetPath);
            return asset;
        }
    }
}
