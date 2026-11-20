using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Fixes "eroded" UI letters (half-transparent glyphs with holes) game-wide. When a dynamic font's atlas is rebuilt
    /// (new sizes/characters requested — e.g. a resolution change, a window opening with a new font size), every Text
    /// mesh that was not regenerated keeps UVs into the OLD atlas layout and shows broken glyphs until something else
    /// redraws it. This guard records the rebuilt fonts and, after the canvas rebuild of that frame, marks every live
    /// Text using them dirty so they regenerate against the new atlas. Runs only when a rebuild happened.
    /// </summary>
    public sealed class FontAtlasGuard : MonoBehaviour
    {
        private static FontAtlasGuard instance;
        private static readonly HashSet<Font> rebuilt = new HashSet<Font>();
        public static int Repairs { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance) return;
            var go = new GameObject("FontAtlasGuard") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FontAtlasGuard>();
        }

        private void OnEnable() => Font.textureRebuilt += OnRebuilt;
        private void OnDisable() => Font.textureRebuilt -= OnRebuilt;
        private static void OnRebuilt(Font f) { if (f) rebuilt.Add(f); } // may fire inside a canvas rebuild: act later

        private void LateUpdate()
        {
            if (rebuilt.Count == 0) return;
            var fonts = new List<Font>(rebuilt);
            rebuilt.Clear();
            foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t && t.font && fonts.Contains(t.font)) t.SetVerticesDirty();
            Repairs++;
        }
    }
}
