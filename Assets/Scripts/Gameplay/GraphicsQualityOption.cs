using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Player-selectable graphics preset (Thấp / Vừa / Cao) for PC and mobile: render scale, MSAA, shadows,
    /// frame-rate cap. Stored locally in PlayerPrefs because it is a DEVICE setting, not character data.
    /// Defaults: Cao on desktop, Vừa on phones/tablets.
    /// </summary>
    public sealed class GraphicsQualityOption : MonoBehaviour
    {
        public enum Level { Low = 0, Medium = 1, High = 2 }
        private const string Key = "AK_Option_GraphicsLevel";
        [SerializeField] private Text label;
        public static Level Current { get; private set; } = Level.High;

        private void Start() => Apply(Load());

        public static Level Load()
        {
            Level fallback = Application.isMobilePlatform ? Level.Medium : Level.High;
            return (Level)PlayerPrefs.GetInt(Key, (int)fallback);
        }

        /// <summary>Button handler: Thấp → Vừa → Cao → Thấp.</summary>
        public void Cycle()
        {
            Apply((Level)(((int)Current + 1) % 3));
            PlayerPrefs.SetInt(Key, (int)Current);
            PlayerPrefs.Save();
        }

        public void Apply(Level level)
        {
            Current = level;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp)
            {
                urp.renderScale = level == Level.Low ? 0.75f : level == Level.Medium ? 0.9f : 1f;
                urp.msaaSampleCount = level == Level.Low ? 1 : level == Level.Medium ? 2 : 4;
                urp.shadowDistance = level == Level.Low ? 0f : level == Level.Medium ? 25f : 40f;
            }
            QualitySettings.shadows = level == Level.Low ? UnityEngine.ShadowQuality.Disable : UnityEngine.ShadowQuality.All;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = level == Level.Low ? 30 : 60;
            if (label) label.text = "Đồ họa: " + (level == Level.Low ? "Thấp" : level == Level.Medium ? "Vừa" : "Cao");
        }
    }
}
