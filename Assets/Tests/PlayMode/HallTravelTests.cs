using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Phase D: walk through the town's portals into Sảnh Bang Hội and Sảnh Kết Hôn and back (real server portal
    /// graph), move inside each hall, no missing scripts, HUD audited; Game View captures from several spots.
    /// Screenshots: QA/Halls/*.png
    /// </summary>
    public sealed class HallTravelTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Halls";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5281);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private Vector2 Pos => motor.transform.position;

        private IEnumerator WalkTo(float x, float timeout = 20f)
        {
            float end = Time.time + timeout;
            while (Mathf.Abs(Pos.x - x) > 0.25f)
            {
                bool right = Pos.x < x;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, right);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, !right);
                yield return null;
                Assert.Less(Time.time, end, $"timeout walking to x={x}, at {Pos}");
            }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            float settle = Time.time + 2f;
            while (Mathf.Abs(motor.Velocity.x) > 0.05f && Time.time < settle) yield return null;
        }

        private IEnumerator Tap(PlayerInput2D.TouchAction a) { input.SetTouchHeld(a, true); yield return null; input.SetTouchHeld(a, false); yield return null; }

        private static IEnumerator Shot(string name, int w = 1920, int h = 1080)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        private void Bind()
        {
            var world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
        }

        /// <summary>Stand next to the portal at x and press Up (halls are safe zones: no confirmation).</summary>
        private IEnumerator TakePortal(float x, string expectScene, string shotFirst = null)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(x, 8f);
            if (shotFirst != null) { yield return new WaitForSeconds(0.8f); yield return Shot(shotFirst); }
            yield return Tap(PlayerInput2D.TouchAction.Up);
            var world = AdventureWorldSession.Current;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(expectScene);
            yield return new WaitForSeconds(1.5f);
            Bind();
            Assert.AreEqual(expectScene, SceneManager.GetActiveScene().name);
        }

        private static void NoMissingScripts()
        {
            var missing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.GetComponents<Component>().Any(c => c == null)).Select(t => UiAudit.PathOf(t)).ToList();
            Assert.IsEmpty(missing, "missing scripts: " + string.Join(", ", missing));
        }

        [UnityTest]
        public IEnumerator Town_Portals_Lead_Into_Both_Halls_And_Back()
        {
            var problems = new List<string>();
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("halls" + Random.Range(1000, 9999), "halls-pass-2026", HeroClass.Swordsman, "Khách Sảnh");
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            // ---- town → Sảnh Bang Hội
            var gates = Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude);
            Assert.IsTrue(gates.Any(g => g.name == "Portal_hub_guild_hall") && gates.Any(g => g.name == "Portal_hub_wedding_hall"), "both hall portals in town");
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_guild_hall").X, TownLayout.GuildHall.Scene, "00_town_guild_portal");
            Assert.AreEqual(TownLayout.GuildHall.MapId, AdventureWorldSession.Current.MapId);
            Assert.AreEqual(TownLayout.GuildHall.MapId, AdventureWorldSession.Current.Character.lastMapId, "server says: in the guild hall");
            NoMissingScripts();
            Assert.Greater(GameObject.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Count(r => r.name.StartsWith("Prop_gh_")), 10, "Meshy hall models placed");
            yield return Shot("01_guild_arrival");
            yield return UiAudit.Screen(Dir, "02_guild_hud", problems);
            foreach (var (x, name) in new[] { (-4f, "03_guild_ranking"), (3.5f, "04_guild_council"), (17f, "05_guild_throne") })
            {
                yield return WalkTo(x);
                Assert.AreEqual(0.8f, Pos.y, 0.3f, "standing on the hall floor (body centre ~0.8 m above it)");
                yield return Shot(name);
            }
            yield return TakePortal(TownLayout.GuildHall.Portals[0].X, TownLayout.Hub.Scene);
            Assert.AreEqual(TownLayout.Hub.MapId, AdventureWorldSession.Current.Character.lastMapId, "back in town");

            // ---- town → Sảnh Kết Hôn
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_wedding_hall").X, TownLayout.WeddingHall.Scene, "00b_town_wedding_portal");
            Assert.AreEqual(TownLayout.WeddingHall.MapId, AdventureWorldSession.Current.Character.lastMapId, "server says: in the wedding hall");
            NoMissingScripts();
            yield return Shot("06_wedding_arrival");
            yield return WalkTo(2f);
            yield return Shot("07_wedding_centre");
            yield return TakePortal(TownLayout.WeddingHall.Portals[0].X, TownLayout.Hub.Scene);
            Assert.AreEqual(TownLayout.Hub.MapId, AdventureWorldSession.Current.Character.lastMapId, "back in town");

            UiAudit.WriteReport($"{Dir}/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
