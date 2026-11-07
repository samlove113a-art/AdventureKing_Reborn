using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Entry scene: authentication must fail closed without a backend; the hero hall shows each real
    /// animated 3D class, and Try Attack / Try Skill reach their contact frames with class VFX.
    /// Captures go to QA/LobbyCaptures (internal review only).
    /// </summary>
    public sealed class EntryLobbyTests
    {
        private const string ScenePath = "Assets/Scenes/AK_Entry.unity";
        private Scene scene;

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.IsNotNull(op, "Entry scene missing - AdventureKing/Scenes/Rebuild Entry Scene");
            while (!op.isDone) yield return null;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
#else
            Assert.Ignore("editor only");
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone) yield return null;
            }
            var director = Object.FindAnyObjectByType<AdventureAudioDirector>();
            if (director) Object.Destroy(director.gameObject); // DontDestroyOnLoad singleton
        }

        private static T Find<T>(string name = null) where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include).First(c => name == null || c.name == name);

        [UnityTest]
        public IEnumerator Login_Fails_Closed_Without_Backend()
        {
            var flow = Find<AdventureEntryFlow>();
            Assert.AreEqual(EntryScreen.Splash, flow.CurrentScreen);
            flow.SkipSplash();
            yield return null;
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen);
            Find<InputField>("AccountInput").text = "tester01";
            Find<InputField>("PasswordInput").text = "password123";
            flow.LoginPassword();
            yield return null;
            StringAssert.Contains("Chưa kết nối máy chủ", Find<Text>("AuthStatus").text);
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen, "Must not reach selection without real auth");
            flow.ContinueWithGoogle();
            flow.ContinueWithFacebook();
            yield return null;
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen, "Social login must not be faked");
        }

        [UnityTest]
        public IEnumerator Hero_Hall_Shows_All_Classes_With_Attack_And_Skill_Contact()
        {
            // INTERNAL QA ONLY: reveal the hall directly (bypassing login is test-only, never in product code).
            var flow = Find<AdventureEntryFlow>();
            flow.SkipSplash(); // leave Splash so the timed Start() coroutine cannot switch back to Auth mid-test
            yield return null;
            Assert.AreEqual(EntryScreen.Authentication, flow.CurrentScreen);
            Find<RectTransform>("SplashRoot").gameObject.SetActive(false);
            Find<RectTransform>("AuthRoot").gameObject.SetActive(false);
            Find<RectTransform>("SelectionRoot").gameObject.SetActive(true);
            var preview = Find<HeroSelectionPreview3D>();
            var capture = new LobbyCapture();
            Directory.CreateDirectory("QA/LobbyCaptures");

            foreach (HeroClass c in System.Enum.GetValues(typeof(HeroClass)))
            {
                flow.SelectClass(c);
                yield return new WaitForSeconds(1.0f);
                Assert.AreEqual(c, preview.ActiveClass);
                Assert.IsTrue(preview.ActiveModel, c + " model not shown");
                Assert.IsTrue(preview.ActiveModel.GetComponentsInChildren<SkinnedMeshRenderer>().Length >= 2, c + " body + weapon");
                Assert.AreEqual(HeroClassCatalog.Get(c).displayName, Find<Text>("HeroName").text);
                yield return capture.Save($"QA/LobbyCaptures/{c}_idle.png");

                var events = preview.ActiveModel.GetComponentInChildren<CharacterAnimationEvents>();
                int attackHits = 0, skillHits = 0, skillCasts = 0;
                events.AttackImpact += () => attackHits++;
                events.SkillImpact += () => skillHits++;
                events.SkillCast += () => skillCasts++;

                preview.PlayAttackPreview();
                float end = Time.time + 3f;
                while (attackHits == 0 && Time.time < end) yield return null;
                Assert.Greater(attackHits, 0, c + " attack never reached its contact frame");
                yield return new WaitForSeconds(0.08f);
                int previewLayer = preview.ActiveModel.layer;
                bool visibleFx =
                    Object.FindObjectsByType<VfxAutoDestroy>(FindObjectsInactive.Exclude).Any(v => v.gameObject.layer == previewLayer) ||
                    Object.FindObjectsByType<VfxProjectile>(FindObjectsInactive.Exclude).Any(v => v.gameObject.layer == previewLayer);
                Assert.IsTrue(visibleFx, c + " attack VFX missing or not on the preview camera's layer");
                yield return capture.Save($"QA/LobbyCaptures/{c}_attack.png");
                yield return new WaitForSeconds(1.6f);

                preview.PlaySkillPreview();
                end = Time.time + 6f;
                while (skillHits == 0 && Time.time < end) yield return null;
                Assert.Greater(skillCasts, 0, c + " skill cast event missing");
                Assert.Greater(skillHits, 0, c + " skill never reached its impact frame");
                yield return new WaitForSeconds(0.1f);
                yield return capture.Save($"QA/LobbyCaptures/{c}_skill.png");
                yield return new WaitForSeconds(3.5f);
            }
            capture.Dispose();
        }

        /// <summary>Renders the whole Overlay UI (incl. the 3D preview RawImage) to a PNG via a temp camera.</summary>
        private sealed class LobbyCapture
        {
            private readonly Canvas canvas;
            private readonly Camera cam;
            private readonly RenderTexture rt;

            public LobbyCapture()
            {
                canvas = Object.FindAnyObjectByType<Canvas>();
                rt = new RenderTexture(1600, 900, 24);
                var go = new GameObject("QA_UICapture");
                cam = go.AddComponent<Camera>();
                cam.cullingMask = 1 << 5;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.orthographic = true;
                cam.targetTexture = rt;
                cam.transform.position = new Vector3(5000, 5000, -10);
                cam.enabled = false;
            }

            public IEnumerator Save(string path)
            {
                // Batchmode has no game-view frame end, so WaitForEndOfFrame never resumes; render manually.
                yield return null;
                Assert.IsTrue(Find<RectTransform>("SelectionRoot").gameObject.activeInHierarchy, "hall not visible for " + path);
                Assert.IsFalse(Find<RectTransform>("AuthRoot").gameObject.activeInHierarchy, "auth overlay visible for " + path);
                var prevMode = canvas.renderMode;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                canvas.renderMode = prevMode;
            }

            public void Dispose()
            {
                if (cam) Object.Destroy(cam.gameObject);
                rt.Release();
            }
        }
    }
}
