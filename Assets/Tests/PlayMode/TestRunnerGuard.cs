using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdventureKing.Tests
{
    /// <summary>
    /// The Unity Test Framework drives PlayMode tests from a MonoBehaviour (PlaymodeTestsController) that lives
    /// in its InitTestScene and is NOT DontDestroyOnLoad. The game legitimately loads scenes in Single mode
    /// (logout, portals), which would destroy that runner and freeze the test run forever (CPU busy, no output).
    /// Tests that let the game change scenes call <see cref="Protect"/> first.
    /// </summary>
    public static class TestRunnerGuard
    {
        public static void Protect()
        {
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb.GetType().Name != "PlaymodeTestsController") continue;
                Transform root = mb.transform.root;
                if (root.gameObject.scene.name != "DontDestroyOnLoad") Object.DontDestroyOnLoad(root.gameObject);
            }
        }

        /// <summary>
        /// Leaves a clean world for the next test: unloads game scenes (never the test runner's scene) and destroys
        /// the game's persistent singletons.
        /// </summary>
        public static IEnumerator CloseGame()
        {
            Scene keep = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.name.StartsWith("InitTestScene")) keep = s;
            }
            if (!keep.IsValid()) keep = SceneManager.CreateScene("EmptyAfterTest_" + Time.frameCount);
            SceneManager.SetActiveScene(keep);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s == keep || !s.isLoaded) continue;
                yield return SceneManager.UnloadSceneAsync(s);
            }
            if (AdventureKing.Gameplay.AdventureServerConnection.Instance)
                Object.Destroy(AdventureKing.Gameplay.AdventureServerConnection.Instance.gameObject);
            var director = Object.FindAnyObjectByType<AdventureKing.Gameplay.AdventureAudioDirector>();
            if (director) Object.Destroy(director.gameObject);
            AdventureKing.Gameplay.AdventurePlayerSession.Clear();
            yield return null;
        }
    }
}
