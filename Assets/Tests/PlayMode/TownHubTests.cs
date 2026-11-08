using System.Collections;
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
    /// Plays the central town like a player, through the real input path (same as the touch buttons), with the
    /// real server: run, jump, rope and ladder climbs up and down, rope bridge, dropping off ledges, walking the
    /// whole map both ways (stuck detection), falling out of the map (recovery), and the portal round trip
    /// Thị trấn Chuông Gió -> Đồng Cỏ Gió -> back, validated by the server. Screens go to QA/HubTest.
    /// </summary>
    public sealed class TownHubTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private GameplayRecorder recorder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5197);
            yield return server.Start();
            Directory.CreateDirectory("QA/HubTest");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) ReleaseAll();
            if (recorder)
            {
                File.WriteAllText("QA/HubTest/video_frames.txt", recorder.Directory + "\n" + recorder.Frames + "\n");
                recorder.End();
            }
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private void Bind()
        {
            motor = AdventureWorldSession.Current.Player;
            input = motor.GetComponent<PlayerInput2D>();
        }

        private Vector2 Pos => motor.transform.position;
        private float Feet => motor.transform.position.y - 0.8f;

        private void ReleaseAll()
        {
            foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction)))
                input.SetTouchHeld(a, false);
        }

        /// <summary>Walks to x; fails if the player stops making progress (stuck on a collider).</summary>
        private IEnumerator WalkTo(float x, float timeout = 30f)
        {
            float end = Time.time + timeout, lastX = Pos.x, checkAt = Time.time + 1f;
            while (Mathf.Abs(Pos.x - x) > 0.25f)
            {
                bool right = Pos.x < x;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, right);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, !right);
                yield return null;
                if (Time.time >= checkAt)
                {
                    Assert.Greater(Mathf.Abs(Pos.x - lastX), 0.3f, $"stuck at {Pos} walking to x={x}");
                    lastX = Pos.x;
                    checkAt = Time.time + 1f;
                }
                Assert.Less(Time.time, end, $"timeout walking to x={x}, at {Pos}");
            }
            ReleaseAll();
            yield return Settle();
        }

        private IEnumerator Settle(float seconds = 3f)
        {
            float end = Time.time + seconds;
            yield return new WaitForFixedUpdate();
            while ((!motor.IsGrounded || motor.Velocity.sqrMagnitude > 0.01f) && !motor.IsClimbing && Time.time < end) yield return null;
        }

        private IEnumerator Climb(PlayerInput2D.TouchAction dir, System.Func<bool> done, string what, float timeout = 12f)
        {
            float end = Time.time + timeout;
            input.SetTouchHeld(dir, true);
            while (!done())
            {
                Assert.Less(Time.time, end, $"timeout {what}, at {Pos}, climbing={motor.IsClimbing}");
                yield return null;
            }
            input.SetTouchHeld(dir, false);
            yield return Settle();
        }

        /// <summary>All characters rendered (nothing truncated) and the label box stays inside its parent rect.</summary>
        private static void AssertTextFits(UnityEngine.UI.Text t, string what)
        {
            Assert.IsNotNull(t, what);
            Canvas.ForceUpdateCanvases();
            // Lay the text out exactly as the label does (best fit + truncate) and again with overflow allowed.
            // If the box were too small, truncation would drop characters, so both counts must match.
            // Words wider than the box wrap per character in uGUI, so horizontal spill cannot happen with Wrap.
            Vector2 box = t.rectTransform.rect.size;
            Assert.AreEqual(HorizontalWrapMode.Wrap, t.horizontalOverflow, what + " must wrap");
            int Visible(string s, VerticalWrapMode v)
            {
                var st = t.GetGenerationSettings(box);
                st.verticalOverflow = v;
                var g = new TextGenerator();
                g.PopulateWithErrors(s, st, t.gameObject);
                return g.characterCountVisible;
            }
            int fitted = Visible(t.text, VerticalWrapMode.Truncate), full = Visible(t.text, VerticalWrapMode.Overflow);
            Assert.AreEqual(full, fitted, $"{what}: text truncated in its {box.x:0}x{box.y:0} box ('{t.text}')");
            // Negative control: the same check does catch text that cannot fit.
            string tooLong = string.Concat(Enumerable.Repeat(t.text + "\n", 12));
            if (t.resizeTextForBestFit) Assert.Less(Visible(tooLong, VerticalWrapMode.Truncate), Visible(tooLong, VerticalWrapMode.Overflow), what + ": check is blind");
            TestContext.WriteLine($"{what}: all {full} characters drawn in {box.x:0}x{box.y:0}");
            var parent = t.rectTransform.parent as RectTransform;
            Rect r = t.rectTransform.rect, pr = parent.rect;
            Vector2 lp = t.rectTransform.localPosition;
            Assert.LessOrEqual(lp.x + r.xMax, pr.xMax + 0.5f, what + " right edge");
            Assert.GreaterOrEqual(lp.x + r.xMin, pr.xMin - 0.5f, what + " left edge");
            Assert.LessOrEqual(lp.y + r.yMax, pr.yMax + 0.5f, what + " top edge");
            Assert.GreaterOrEqual(lp.y + r.yMin, pr.yMin - 0.5f, what + " bottom edge");
        }

        private void AssertStanding(float floorTop, string where)
        {
            Assert.IsTrue(motor.IsGrounded, $"not grounded at {where} ({Pos})");
            Assert.IsFalse(motor.IsClimbing, $"still climbing at {where}");
            Assert.AreEqual(floorTop, Feet, 0.15f, $"wrong floor at {where} ({Pos})");
        }

        /// <summary>Phone-shaped frame (19.5:9) with the touch controls shown, captured from the running game.</summary>
        private static IEnumerator PhoneShot(string name)
        {
            // Emulate a phone: touch controls on, keyboard-only hints off (as TouchControlsVisibility does on device).
            var all = Object.FindObjectsByType<TouchControlsVisibility>(FindObjectsInactive.Include);
            var saved = all.Select(t => (t.gameObject, t.gameObject.activeSelf)).ToArray();
            foreach (var t in all) t.gameObject.SetActive(!t.hideOnTouch);
            yield return Shot(name, 2340, 1080);
            foreach (var (go, active) in saved) go.SetActive(active);
        }

        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            yield return null;
            Camera cam = Camera.main;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(cam, rt); // world + HUD/dialog/loading UI
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/HubTest/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Town_Run_Jump_Climb_Bridge_Portal_RoundTrip()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("hub.tester@test.vn", "hub-pass-2026", HeroClass.Assassin, "Lữ Khách Gió");
            Bind();
            recorder = GameplayRecorder.Begin("town_traversal");
            AdventureWorldSession world = AdventureWorldSession.Current;
            Assert.AreEqual(TownLayout.Hub.MapId, world.MapId);
            Assert.IsTrue(world.HeroModel, "3D hero spawned");

            // ---- new player: the Village Elder walks over, waves and greets the character by name
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            Assert.IsNotNull(greeter, "Elder NPC with greeter in town");
            var elderActor = greeter.GetComponent<NpcActor>();
            float elderStart = elderActor.transform.position.x;
            float until0 = Time.time + 20f;
            while (!greeter.DialogOpen && Time.time < until0) yield return null;
            Assert.IsTrue(greeter.DialogOpen, "Elder greeting dialog");
            Assert.Less(Mathf.Abs(elderActor.transform.position.x - Pos.x), 2.2f, "Elder walked up to the hero");
            Assert.Greater(Mathf.Abs(elderActor.transform.position.x - elderStart), 1f, "Elder actually moved");
            var dialogLine = EntryLobbyTests.Find<UnityEngine.UI.Text>("Line");
            StringAssert.Contains("Lữ Khách Gió", dialogLine.text);
            yield return Shot("00_elder_greeting");
            AssertTextFits(dialogLine, "Elder dialog");
            yield return PhoneShot("00b_elder_greeting_phone");
            AssertTextFits(dialogLine, "Elder dialog (phone)");
            // Every sign label is fully drawn inside its measured parchment panel (no clipped / spilled words).
            var signs = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.name == "SignText").ToArray();
            Assert.GreaterOrEqual(signs.Length, 3, "town signs");
            foreach (Canvas sign in signs) AssertTextFits(sign.GetComponentInChildren<UnityEngine.UI.Text>(), "sign at x=" + sign.transform.position.x.ToString("0"));
            greeter.Close();
            until0 = Time.time + 15f;
            while (!greeter.Done && Time.time < until0) yield return null;
            Assert.IsTrue(greeter.Done, "Elder finished and returned");
            Assert.AreEqual(1, world.Character.tutorialStep, "tutorial step saved on the server");
            Assert.IsTrue(input.enabled, "movement unlocked after the dialog");

            // ---- layers & colliders
            Assert.AreEqual(9, motor.gameObject.layer, "player layer");
            Assert.IsFalse(Physics2D.GetIgnoreLayerCollision(9, 8), "player must collide with ground");
            Assert.IsFalse(Physics2D.GetIgnoreLayerCollision(9, 11), "player must collide with one-way decks");
            var collision = GameObject.Find("Collision");
            Assert.IsNotNull(collision);
            foreach (BoxCollider2D c in collision.GetComponentsInChildren<BoxCollider2D>())
            {
                bool oneWay = TownLayout.Hub.Decks.Any(d => d.Name == c.name);
                Assert.AreEqual(oneWay ? 11 : 8, c.gameObject.layer, c.name + " layer");
                Assert.AreEqual(oneWay, c.usedByEffector && c.GetComponent<PlatformEffector2D>(), c.name + " one-way effector");
                Assert.IsFalse(c.isTrigger, c.name + " must be solid");
            }
            Assert.AreEqual(TownLayout.Hub.Climbs.Length, Object.FindObjectsByType<LadderZone2D>(FindObjectsInactive.Exclude).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude).Length);

            // ---- spawn & jump
            yield return Settle();
            AssertStanding(0f, "spawn");
            Assert.AreEqual(TownLayout.Hub.Spawn.x, Pos.x, 0.3f);
            yield return Shot("01_spawn");
            yield return PhoneShot("01b_spawn_phone_19.5x9");
            float peak = Pos.y;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true);
            for (float t = 0; t < 0.5f; t += Time.deltaTime) { peak = Mathf.Max(peak, Pos.y); yield return null; }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false);
            Assert.Greater(peak - 0.8f, 1.2f, "jump height");
            yield return Settle();
            AssertStanding(0f, "after jump");

            // ---- rope up to the bell terrace, then rope up to the wind lookout
            yield return WalkTo(-4f);
            yield return Climb(PlayerInput2D.TouchAction.Up, () => !motor.IsClimbing && motor.IsGrounded && Feet > 4.5f, "rope to bell terrace");
            AssertStanding(5f, "bell terrace");
            yield return Shot("02_bell_terrace");
            yield return WalkTo(8f);
            yield return Climb(PlayerInput2D.TouchAction.Up, () => !motor.IsClimbing && motor.IsGrounded && Feet > 9.5f, "rope to lookout");
            AssertStanding(10f, "wind lookout");
            yield return WalkTo(4f);
            yield return Shot("03_wind_lookout");

            // ---- drop off the lookout edge back to the terrace, cross the rope bridge to the teahouse veranda
            yield return WalkTo(11.5f);
            AssertStanding(5f, "dropped to terrace");
            yield return WalkTo(23f);
            AssertStanding(5f, "rope bridge");
            yield return Shot("04_rope_bridge");
            yield return WalkTo(40f);
            AssertStanding(5f, "teahouse veranda");

            // ---- ladder up to the windmill lookout and back DOWN through the one-way deck
            yield return Climb(PlayerInput2D.TouchAction.Up, () => !motor.IsClimbing && motor.IsGrounded && Feet > 9f, "ladder to windmill lookout");
            AssertStanding(9.5f, "windmill lookout");
            yield return Climb(PlayerInput2D.TouchAction.Down, () => motor.IsGrounded && !motor.IsClimbing && Feet < 6f, "ladder down to veranda");
            AssertStanding(5f, "back on veranda (ladder foot)");

            // ---- ladder from the veranda down to the ground
            yield return WalkTo(46f);
            yield return Climb(PlayerInput2D.TouchAction.Down, () => motor.IsGrounded && !motor.IsClimbing && Feet < 0.5f, "ladder down to ground");
            AssertStanding(0f, "ground under veranda");

            // ---- portal to the meadow (validated by the server)
            yield return WalkTo(TownLayout.Hub.Portals[0].X);
            var gate = Object.FindAnyObjectByType<PortalGate2D>();
            Assert.IsTrue(gate.PlayerInside, "inside portal trigger");
            yield return Shot("05_portal");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true);
            yield return null;
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false);
            // Leaving town asks first; the dialog names the destination and freezes movement.
            float until = Time.time + 2f;
            while (!world.ConfirmOpen && Time.time < until) yield return null;
            Assert.IsTrue(world.ConfirmOpen, "portal confirmation dialog");
            StringAssert.Contains("Đồng Cỏ Gió", EntryLobbyTests.Find<UnityEngine.UI.Text>("Question").text);
            Assert.IsFalse(input.enabled, "movement locked while the dialog is open");
            yield return Shot("05b_portal_confirm");
            world.ConfirmPortal();
            gate.Activate(); // double activation (double click / lag) must not start a second trip
            Assert.IsTrue(world.Traveling);
            until = Time.time + 8f;
            bool sawLoading = false;
            while (SceneManager.GetActiveScene().name == TownLayout.Hub.Scene && Time.time < until)
            {
                if (world && world.LoadingVisible && !sawLoading) { sawLoading = true; yield return Shot("05c_loading"); }
                yield return null;
            }
            Assert.IsTrue(sawLoading, "loading screen shown during travel");
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            Bind();
            Assert.AreEqual(TownLayout.Meadow.MapId, AdventureWorldSession.Current.MapId);
            Assert.AreEqual(TownLayout.Meadow.Spawn.x, Pos.x, 0.5f, "arrived at the meadow portal");
            Assert.IsTrue(AdventureWorldSession.Current.HeroModel, "hero respawned in the meadow");
            yield return Settle();
            AssertStanding(0f, "meadow arrival");
            yield return Shot("06_meadow_arrival");

            // ---- and back through the meadow portal
            yield return WalkTo(TownLayout.Meadow.Portals[0].X);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true);
            yield return null;
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false);
            yield return TestServer.WaitForScene(TownLayout.Hub.Scene);
            Bind();
            Assert.AreEqual(TownLayout.Hub.MapId, AdventureWorldSession.Current.MapId);
            Assert.AreEqual(62f, Pos.x, 0.6f, "arrived next to the town portal");

            // ---- walk the whole town floor west and back east: never stuck
            yield return Settle();
            yield return WalkTo(TownLayout.Hub.MinX + 2f, 45f);
            AssertStanding(0f, "west end");
            yield return Shot("07_west_end");
            yield return WalkTo(20f, 45f);
            AssertStanding(0f, "plaza again");

            // ---- fall out of the map -> recovered on the last safe ground
            int before = AdventureWorldSession.Current.FallRecoveries;
            var body = motor.GetComponent<Rigidbody2D>();
            body.position = new Vector2(20f, -20f);
            motor.transform.position = new Vector3(20f, -20f, 0f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(before + 1, AdventureWorldSession.Current.FallRecoveries, "fall recovery triggered");
            yield return Settle();
            Assert.Greater(Feet, -0.2f, "back on the ground after falling out");
        }
    }
}
