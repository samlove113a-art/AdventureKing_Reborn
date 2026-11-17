using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Which body the heroes use: the original rigged 3D chibi (default, always kept) or the 2D chibi sprites baked from it
    /// (Resources/Chibi2D/{Class}_Visual2D). Classes without a 2D body yet fall back to 3D, so groups can be converted one at a
    /// time. Choice: PlayerPrefs "ak_visual_style" ("2d" / "3d") or env AK_VISUAL_STYLE; gameplay, server and saves never depend on it.
    /// </summary>
    public static class HeroVisualStyle
    {
        public const string Key = "ak_visual_style";
        /// <summary>2D chibi is the default look; "3d" (the original rigged models, never deleted) stays selectable for QA.</summary>
        public const string DefaultStyle = "2d";
        /// <summary>QA / tests: force a style for this run (null = use the saved choice).</summary>
        public static string Override;

        public static bool Chibi2D
        {
            get
            {
                string s = Override;
                if (string.IsNullOrEmpty(s)) s = System.Environment.GetEnvironmentVariable("AK_VISUAL_STYLE");
                if (string.IsNullOrEmpty(s)) { try { s = PlayerPrefs.GetString(Key, DefaultStyle); } catch { s = DefaultStyle; } }
                return string.Equals(s, "2d", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Save(bool chibi2D) { try { PlayerPrefs.SetString(Key, chibi2D ? "2d" : "3d"); } catch { } }

        /// <summary>The 2D body of a class, or null when it has none yet.</summary>
        public static GameObject Body2D(HeroClass heroClass) => Resources.Load<GameObject>("Chibi2D/" + heroClass + "_Visual2D");

        /// <summary>The prefab to spawn: the 2D body when the style is 2D and one exists, otherwise the given 3D prefab.</summary>
        public static GameObject Resolve(HeroClass heroClass, GameObject prefab3D)
        {
            if (!Chibi2D) return prefab3D;
            GameObject two = Body2D(heroClass);
            return two ? two : prefab3D;
        }
    }
}
