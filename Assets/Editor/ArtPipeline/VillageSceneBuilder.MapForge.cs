using System;
using AdventureKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// AK Map Forge (merged from feat/ak-map-forge-m02): the one-click window for Map 2. After the merge it drives the SAME
    /// Map 2 builder as the batch pipeline (<c>BuildCh1GreenMeadow</c>, tested scene + Meshy props + panorama), so there is a single
    /// source of truth for Map 2. The branch's own parallel Map 2 factory (different decks / portal arrival / safe zone, no
    /// Meshy slime, no music) was dropped on purpose: it would have overwritten the tested scene with another layout.
    /// Only the decoration seed / flower detail are configurable; collision, portals and spawns stay in TownLayout + the server.
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        /// <summary>Decoration seed and flower density used by <c>BuildCh1GreenMeadow</c> (wildflowers only; never collision).</summary>
        private static int forgeSeed = 20261111, forgeDetail = 2;

        public static void ForgeBuildMap2(int seed, int detail)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before generating a map.");
            forgeSeed = seed; forgeDetail = Mathf.Clamp(detail, 1, 3);
            try
            {
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                BuildCh1GreenMeadow();
                RegisterBuildScenes();
                AssetDatabase.SaveAssets();
            }
            finally { forgeSeed = 20261111; forgeDetail = 2; }
            Debug.Log("[AK Map Forge] Map 2 generated; save, compile and play-test with the server before release.");
        }

        /// <summary>
        /// Checks that the EXISTING Map 1 scene already has the east portal to Map 2 and never edits it (the Map 1 builder places the
        /// gate and the sign). Returns true only if something had to be changed, which is now never the case.
        /// </summary>
        public static bool ForgeLinkMap1()
        {
            if (!System.IO.File.Exists(Ch1VillageScenePath))
                throw new System.IO.FileNotFoundException("Map 1 scene is missing", Ch1VillageScenePath);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Ch1VillageScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            if (!GameObject.Find("Portal_ch1_village_east_m02"))
                throw new InvalidOperationException("Map 1 has no east gate to Map 2: rebuild it with BuildChapter1Map3FromCommandLine.");
            return false;
        }
    }
}
