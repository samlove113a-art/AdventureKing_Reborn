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
            Assert.IsEmpty(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name.StartsWith("HallSpot_")).Select(t => t.name), "every hall model is generated (no empty spots)");
            // Đội Trưởng Liên Minh: talk at the reception counter (Meshy NPC with an animated face)
            var captain = Object.FindObjectsByType<NpcTalkSpot>(FindObjectsInactive.Exclude).First(s => s.name == "NpcCaptain");
            Assert.IsNotNull(captain.GetComponentInChildren<HeroFace>(), "captain has the animated face layer");
            yield return WalkTo(captain.transform.position.x + 1.2f);
            until = Time.time + 3f;
            while (!captain.PlayerNear && Time.time < until) yield return null;
            Assert.IsTrue(captain.PlayerNear, "captain talk prompt");
            yield return Tap(PlayerInput2D.TouchAction.Interact);
            Assert.AreEqual(1, captain.Talks);
            StringAssert.Contains("Đội Trưởng Liên Minh", captain.Said);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("02b_guild_captain");
            // the captain opens the guild window (create / browse guilds); close it to walk on
            Assert.IsTrue(SocialPanel.Instance && SocialPanel.Instance.IsOpen && SocialPanel.Instance.Tab == "guild", "captain opens the guild tab");
            SocialPanel.Instance.Close();
            yield return new WaitForSeconds(0.3f);
            foreach (var (x, name) in new[] { (-4f, "03_guild_ranking"), (3.5f, "04_guild_council"), (10.5f, "04b_guild_altar"), (17f, "05_guild_throne") })
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
            Assert.IsEmpty(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name.StartsWith("HallSpot_")).Select(t => t.name), "every wedding model is generated");
            yield return Shot("06_wedding_arrival");
            yield return UiAudit.Screen(Dir, "06b_wedding_hud", problems);
            foreach (var (x, name) in new[] { (-9f, "07_wedding_guests"), (8f, "08_wedding_musician"), (18f, "09_wedding_banquet") })
            {
                yield return WalkTo(x);
                yield return Shot(name);
            }
            // Chủ Hôn → ceremony rehearsal cinematic (camera shots, captions, petals), then the camera comes back
            var petals = Object.FindAnyObjectByType<PetalFall>();
            var ceremony = Object.FindAnyObjectByType<WeddingCeremony>();
            var director = CinematicDirector.Current;
            var follow = Camera.main.GetComponent<CameraFollow2D>();
            var officiant = Object.FindObjectsByType<NpcTalkSpot>(FindObjectsInactive.Exclude).First(s => s.name == "NpcOfficiant");
            yield return WalkTo(officiant.transform.position.x + 1.2f);
            until = Time.time + 3f;
            while (!officiant.PlayerNear && Time.time < until) yield return null;
            yield return Tap(PlayerInput2D.TouchAction.Interact);
            // the officiant first asks the server whether this hero is engaged (real ceremony) — not engaged: rehearsal
            until = Time.time + 3f;
            while (ceremony.Played == 0 && Time.time < until) yield return null;
            Assert.AreEqual(1, ceremony.Played, "talking to the officiant starts the rehearsal");
            Assert.IsTrue(director.Playing);
            Assert.IsFalse(follow.enabled, "cinematic camera has the camera");
            Assert.IsFalse(input.enabled, "input paused during the cinematic");
            var shotNames = new[] { "10_ceremony_wide", "11_ceremony_entrance", "12_ceremony_vows", "13_ceremony_rings", "14_ceremony_cake", "15_ceremony_toast", "16_ceremony_finale" };
            int taken = 0;
            until = Time.time + 40f;
            while (director.Playing && Time.time < until)
            {
                if (director.ShotIndex >= taken)
                {
                    yield return new WaitForSeconds(director.ShotIndex == 0 ? 1.6f : 2.0f); // after the move, during the hold
                    if (director.ShotIndex >= taken && director.Playing) { yield return Shot(shotNames[Mathf.Min(taken, shotNames.Length - 1)]); taken++; }
                }
                yield return null;
            }
            Assert.IsFalse(director.Playing, "ceremony finished");
            Assert.GreaterOrEqual(taken, 6, "captured the ceremony shots");
            Assert.GreaterOrEqual(petals.Bursts, 3, "petal showers at the rings, cake, toast and finale");
            CollectionAssert.AreEqual(new[] { "Bow", "Kneel", "Celebrate", "Toast", "Cheer" }, ceremony.HeroGestures, "the hero's ceremony gestures (Meshy clips) all played");
            Assert.IsTrue(follow.enabled, "camera handed back to the follow camera");
            Assert.IsTrue(input.enabled, "input back");
            yield return TakePortal(TownLayout.WeddingHall.Portals[0].X, TownLayout.Hub.Scene);
            Assert.AreEqual(TownLayout.Hub.MapId, AdventureWorldSession.Current.Character.lastMapId, "back in town");

            UiAudit.WriteReport($"{Dir}/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
