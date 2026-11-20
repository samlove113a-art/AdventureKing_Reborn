using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// One scaling rule for every screen-space canvas: Scale With Screen Size, reference 1920x1080, Match 0.5 (width and height
    /// weigh equally, so the UI scales proportionally and is never squeezed on a phone / ultrawide / 4:3 tablet).
    /// New canvases call <see cref="Apply"/>; scenes built before this rule are corrected when they load.
    /// </summary>
    public static class UiStandard
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public const float Match = 0.5f;

        public static void Apply(CanvasScaler s)
        {
            if (!s) return;
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = ReferenceResolution;
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = Match;
        }

        public static bool IsStandard(CanvasScaler s) =>
            s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && s.referenceResolution == ReferenceResolution &&
            s.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight && Mathf.Approximately(s.matchWidthOrHeight, Match);

        /// <summary>Screen-space canvases only (world-space labels keep their own size). Returns how many were changed.</summary>
        public static int FixAll()
        {
            int n = 0;
            foreach (CanvasScaler s in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Canvas c = s.GetComponent<Canvas>();
                if (!c || !c.isRootCanvas || c.renderMode == RenderMode.WorldSpace || IsStandard(s)) continue;
                Apply(s); n++;
            }
            return n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            FixAll();
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode) => FixAll();
    }
}
