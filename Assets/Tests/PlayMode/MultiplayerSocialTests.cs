using System.Collections;
using System.Collections.Generic;
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
    /// Phase D multiplayer with two real accounts on one server: player A is this Unity game client (real UI), player B
    /// is an independent second client (own session, same endpoints, own 6 Hz heartbeat). Covers seeing each other move,
    /// chat (world + private), "E · Tương tác", friends, a guild (create in the UI, apply, approve, guild chat, the guild's
    /// private Guild Hall instance), a proposal, the two-player wedding in the Wedding Hall with the full staging, and
    /// persistence after fresh logins. Screenshots: QA/Multiplayer/*.png
    /// </summary>
    public sealed class MultiplayerSocialTests
    {
        private TestServer server;
        private SecondClient bob;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Multiplayer";
        private const string PassA = "multi-pass-A-2026", PassB = "multi-pass-B-2026";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5291, starterGold: 100000, devWelfare: true);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (bob != null) bob.Heartbeat = false;
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        /// <summary>Records the 180 s ceremony to QA video frames (set AK_RECORD_WEDDING=1; slower).</summary>
        private static bool Record => System.Environment.GetEnvironmentVariable("AK_RECORD_WEDDING") == "1";
        private Vector2 Pos => motor.transform.position;
        private void Bind() { var w = AdventureWorldSession.Current; motor = w.Player; input = motor.GetComponent<PlayerInput2D>(); }
        private static string MyId => AdventureWorldSession.Current.Character.characterId;

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
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator Tap(PlayerInput2D.TouchAction a) { input.SetTouchHeld(a, true); yield return null; input.SetTouchHeld(a, false); yield return null; }

        private IEnumerator TakePortal(float x, string scene)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(x, 8f);
            yield return Tap(PlayerInput2D.TouchAction.Up);
            var world = AdventureWorldSession.Current;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(scene);
            yield return new WaitForSeconds(1.2f);
            Bind();
        }

        private static IEnumerator Shot(string name, int w = 1920, int h = 1080)
        {
            // render after every LateUpdate (procedural poses: the cake cut, the kiss lean), as the game frame does
            var gate = new GameObject("ShotGate").AddComponent<LateFrameShot>();
            gate.Name = name; gate.W = w; gate.H = h;
            while (!gate.Done) yield return null;
            Object.Destroy(gate.gameObject);
        }

        /// <summary>Captures in the frame's last LateUpdate (after every other script has posed the characters).</summary>
        [DefaultExecutionOrder(32000)]
        private sealed class LateFrameShot : MonoBehaviour
        {
            public string Name; public int W, H; public bool Done;
            private void LateUpdate()
            {
                if (Done) return;
                Write(Name, W, H);
                Done = true;
            }
        }
        private static void Write(string name, int w, int h)
        {
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

        /// <summary>Clicks the visible button with this label inside the social window (the real UI path).</summary>
        private static void Click(string label, string under = null)
        {
            var root = SocialPanel.Instance.transform;
            Button b = root.GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.interactable && x.GetComponentInChildren<Text>()?.text == label &&
                (under == null || x.transform.GetComponentsInParent<Transform>().Any(p => p.name == under)));
            Assert.IsNotNull(b, $"button '{label}' in the social window");
            b.onClick.Invoke();
        }

        private static IEnumerator Idle(float seconds = 6f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            while (SocialPanel.Instance.Busy && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>B walks (within the server's speed check) to x.</summary>
        private IEnumerator BobWalk(float x)
        {
            bob.Anim = "run";
            while (Mathf.Abs(bob.X - x) > 0.01f)
            {
                bob.Facing = x > bob.X ? 1 : -1;
                bob.X = Mathf.MoveTowards(bob.X, x, 0.5f);
                yield return new WaitForSecondsRealtime(0.08f);
            }
            bob.Anim = "idle";
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private static IEnumerator Until(System.Func<bool> ok, float seconds = 8f, string what = "")
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ok() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(ok(), "timed out: " + what);
        }

        [UnityTest]
        public IEnumerator Two_Players_Chat_Befriend_Form_A_Guild_And_Marry_In_The_Wedding_Hall()
        {
            var problems = new List<string>();
            string accA = "multiA" + Random.Range(1000, 9999), accB = "multiB" + Random.Range(1000, 9999);
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter(accA, PassA, HeroClass.Swordsman, "Chú Rể Long");
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            var sync = WorldSync.Current;
            Assert.IsNotNull(sync, "multiplayer heartbeat in the world scene");

            // ---- the second player joins the same server and walks around near A
            bob = new SecondClient(server.ApiUrl);
            yield return bob.Register(accB, PassB, "Cô Dâu Mai", HeroClass.Mage);
            bob.X = Pos.x + 3f; bob.Y = 0.9f;
            var runner = new GameObject("SecondClientRunner").AddComponent<CoroutineHost>();
            runner.StartCoroutine(bob.HeartbeatLoop());
            yield return Until(() => sync.Avatar(bob.CharacterId), 6f, "B appears on A's screen");
            RemoteAvatar b = sync.Avatar(bob.CharacterId);
            StringAssert.Contains("Cô Dâu Mai", b.NameShown);
            yield return Until(() => bob.LastSync != null && bob.LastSync.peers.Any(p => p.id == MyId), 6f, "A appears in B's sync");
            // B walks: A sees it move smoothly
            float x0 = b.transform.position.x;
            bob.Anim = "run";
            for (int i = 0; i < 12; i++) { bob.X += 0.25f; yield return new WaitForSeconds(0.08f); }
            bob.Anim = "idle";
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(b.transform.position.x, x0 + 2f, "remote player moved on A's screen");
            yield return Shot("01_two_players_town");

            // ---- chat: world from B, private from A (through the real social window)
            yield return bob.Say("world", null, "Chào cả làng! Có ai đi săn Slime không?");
            yield return Until(() => SocialPanel.Instance.ChatLog.Any(m => m.sender == "Cô Dâu Mai"), 6f, "world chat reached A");
            StringAssert.Contains("Có ai đi săn", SocialPanel.Instance.TickerText);
            yield return Shot("02_chat_ticker");
            SocialPanel.Instance.OpenTab("chat");
            SocialPanel.Instance.SetChat("private", "Cô Dâu Mai", "Mình đi cùng nhé!");
            Click("Gửi");
            yield return Idle();
            yield return Until(() => bob.LastSync.chat.Any(m => m.channel == "private" && m.sender == "Chú Rể Long") || bob.Syncs > 0 && SocialPanel.Instance.ChatLog.Any(m => m.channel == "private"), 6f, "private chat");
            yield return new WaitForSeconds(0.5f);
            StringAssert.Contains("Mình đi cùng nhé!", SocialPanel.Instance.ChatText);
            yield return UiAudit.Screen(Dir, "03_social_chat", problems);
            SocialPanel.Instance.Close();

            // ---- "E · Tương tác" next to B → social window for B → friend request (UI) → B accepts
            yield return BobWalk(Pos.x + 1.1f);
            yield return Until(() => sync.Nearest == sync.Avatar(bob.CharacterId), 6f, "interaction prompt next to B");
            Assert.IsTrue(sync.Avatar(bob.CharacterId).PromptShown);
            yield return Shot("04_interact_prompt");
            yield return Tap(PlayerInput2D.TouchAction.Interact);
            yield return Until(() => SocialPanel.Instance.IsOpen && SocialPanel.Instance.Tab == "friends", 3f, "social window for B");
            Click("Kết bạn");
            yield return Idle();
            yield return Until(() => bob.LastSync.badges.friendRequests == 1, 6f, "B got the friend request");
            yield return bob.AcceptFriend(MyId);
            SocialPanel.Instance.Refresh();
            yield return Idle();
            Assert.IsTrue(SocialPanel.Instance.Friends.friends.Any(f => f.name == "Cô Dâu Mai" && f.online), "friend + online status from the server");
            yield return UiAudit.Screen(Dir, "05_friends", problems);

            // ---- guild: A creates it in the UI, B applies, A approves, guild chat
            SocialPanel.Instance.OpenTab("guild");
            yield return Idle();
            SocialPanel.Instance.transform.GetComponentsInChildren<InputField>(true).First(f => f.name == "GuildName").text = "Gió Chuông";
            Click("Lập bang");
            yield return Idle();
            GuildState g = SocialPanel.Instance.GuildView.guild;
            Assert.IsTrue(g.Valid, "guild created: " + SocialPanel.Instance.LastMessage);
            Assert.AreEqual("leader", g.myRole);
            yield return bob.ApplyGuild(g.id);
            SocialPanel.Instance.Refresh();
            yield return Idle();
            Click("Đơn xin vào (1)");
            yield return Idle();
            yield return UiAudit.Screen(Dir, "06_guild_application", problems);
            Click("Duyệt");
            yield return Idle();
            Assert.AreEqual(2, SocialPanel.Instance.GuildView.guild.members.Length, "B approved into the guild");
            yield return bob.Say("guild", null, "Cảm ơn Bang Chủ!");
            yield return Until(() => SocialPanel.Instance.ChatLog.Any(m => m.channel == "guild" && m.sender == "Cô Dâu Mai"), 6f, "guild chat");
            Click("Thành viên");
            yield return Idle();
            yield return UiAudit.Screen(Dir, "07_guild_members", problems);
            SocialPanel.Instance.Close();

            // ---- both go to the Guild Hall: the guild's private instance
            yield return bob.Travel("hub_guild_hall", 74f);
            runner.StartCoroutine(BobWalk(-12.5f));
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_guild_hall").X, TownLayout.GuildHall.Scene);
            sync = WorldSync.Current;
            yield return Until(() => sync.Avatar(bob.CharacterId), 6f, "guild mate visible in the Guild Hall");
            StringAssert.Contains("Gió Chuông", sync.Avatar(bob.CharacterId).NameShown);
            yield return Shot("08_guild_hall_two_members");

            // ---- marriage: A proposes from the friends tab (UI), B accepts
            SocialPanel.Instance.OpenTab("friends");
            yield return Idle();
            Click("Cầu hôn");
            yield return Idle();
            yield return Until(() => bob.LastSync.badges.proposals == 1, 6f, "B got the proposal");
            yield return bob.AcceptProposal(MyId);
            SocialPanel.Instance.Close();

            // ---- both to the Wedding Hall
            yield return new WaitForSeconds(1.6f);
            yield return bob.Travel("guild_hall_hub", -19f);
            yield return new WaitForSeconds(1.7f);
            yield return bob.Travel("hub_wedding_hall", -56f);
            yield return TakePortal(TownLayout.GuildHall.Portals[0].X, TownLayout.Hub.Scene);
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_wedding_hall").X, TownLayout.WeddingHall.Scene);
            sync = WorldSync.Current;
            runner.StartCoroutine(BobWalk(0f));
            yield return Until(() => sync.Avatar(bob.CharacterId), 6f, "bride visible in the Wedding Hall");
            yield return WalkTo(-1f);
            SocialPanel.Instance.OpenTab("marriage");
            yield return Idle();
            Assert.AreEqual("engaged", SocialPanel.Instance.Marriage.status);
            yield return UiAudit.Screen(Dir, "09_marriage_engaged", problems);
            Click("Tổ chức lễ cưới");
            yield return Idle();
            yield return Until(() => bob.LastSync.ceremony != null && bob.LastSync.ceremony.state == "invited", 6f, "B invited to the ceremony");
            yield return bob.ConfirmCeremony();
            SocialPanel.Instance.Close();

            // ---- the 180 s cinematic ceremony: both clients on the same server clock; 8 key moments captured
            var ceremony = Object.FindAnyObjectByType<WeddingCeremony>();
            yield return Until(() => ceremony.Cinematic != null && ceremony.Cinematic.Running, 10f, "two-player cinematic started");
            WeddingCinematic cin = ceremony.Cinematic;
            CeremonyState onB = bob.LastSync.ceremony;
            Assert.IsTrue(onB != null && onB.state == "running" && onB.steps.Length == 8, "the second client receives the same running ceremony");
            Assert.AreEqual(WorldSync.Current.Last.ceremony.startMs, onB.startMs, "same server start time on both clients");
            Assert.AreEqual(180000, onB.endMs - onB.startMs, "3-minute ceremony");
            GameplayRecorder rec = Record ? GameplayRecorder.Begin("wedding180") : null;
            float recordStartT = cin.T;
            var moments = new (float t, string name)[]
            {
                (12f, "30_entrance_walk"), (47.8f, "31_vows_hand_in_hand"), (81.6f, "32_ring_sliding_on"), (83.2f, "32b_ring_on_finger"),
                (109.2f, "33_kiss"), (132.9f, "34_knife_in_cake"), (138.6f, "34b_slice_cut"), (151.5f, "35_glasses_clink"),
                (156.2f, "36_drinking"), (174f, "37_celebration"),
            };
            foreach (var (mt, name) in moments)
            {
                yield return Until(() => cin.T >= mt || !cin.Running, 400f, "ceremony reaches " + name);
                Assert.IsTrue(cin.Running, $"ceremony still running at {name} ({cin.AbortReason})");
                yield return Shot(name);
                if (name == "32_ring_sliding_on") Assert.AreEqual("rings", cin.SceneId);
            }
            yield return Until(() => !cin.Running, 60f, "ceremony finished");
            if (rec) rec.End();
            File.WriteAllLines($"{Dir}/wedding_events.txt", new[] { $"recordStartT={recordStartT:0.000}", $"frames={(rec ? rec.Frames : 0)}", $"framesDir={(rec ? rec.Directory : "")}" }.Concat(cin.Events));
            Assert.IsTrue(cin.Finished, "cinematic played to the end: " + cin.AbortReason);
            Assert.IsTrue(cin.Events.Contains("banner"), "married banner shown after the server confirmed");
            Assert.IsTrue(cin.RingOnB && cin.RingOnA, "both rings went onto fingers");
            Assert.Less(cin.RingToFingerB, 0.03f, "ring sits on the finger");
            Assert.Greater(cin.KissHeldSeconds, 2f, $"faces met for the kiss (closest {cin.KissFaceDistanceMin:0.000} m)");
            Assert.AreEqual(2, cin.Cuts, "two cuts into the cake");
            Assert.Greater(cin.KnifeDepthMax, 0.05f, "the blade went into the cake");
            Assert.IsTrue(cin.SliceOnPlate, "the slice was served on the plate");
            Assert.IsTrue(cin.Clinked && cin.ClinkRimDistanceMin < 0.015f && cin.ClinkRimDistanceMin > -0.035f, $"the goblet rims touched (edge gap {cin.ClinkRimDistanceMin:0.000} m)");
            Assert.Less(cin.WineLevelA, 0.6f, "A drank");
            Assert.Less(cin.WineLevelB, 0.6f, "B drank");
            Assert.Less(Mathf.Max(cin.GobletToMouthMinA, cin.GobletToMouthMinB), 0.08f, "goblets reached the lips");
            Assert.Less(cin.GripErrorMax, 0.1f, "hands stayed on the props they hold");
            yield return Until(() => !CinematicDirector.Current.Playing, 6f, "camera handed back");
            Assert.IsTrue(Camera.main.GetComponent<CameraFollow2D>().enabled, "camera handed back");
            Assert.IsTrue(input.enabled, "controls handed back");

            // ---- the server says married; gifts owned; persists after fresh logins
            SocialPanel.Instance.OpenTab("marriage");
            yield return Idle();
            Assert.AreEqual("married", SocialPanel.Instance.Marriage.status);
            yield return UiAudit.Screen(Dir, "20_married", problems);
            SocialPanel.Instance.Close();
            yield return Until(() => sync.Avatar(bob.CharacterId) && sync.Avatar(bob.CharacterId).NameShown.Contains("♥ Chú Rể Long"), 6f, "partner on the bride's name plate");
            yield return Shot("21_married_nameplates");
            bob.Heartbeat = false;
            var fresh = new SecondClient(server.ApiUrl);
            yield return fresh.Login(accB, PassB);
            yield return fresh.Call("GET", "/profile", null, j => { });
            MarriageState mb = null; GuildViewState gb = null;
            yield return new SecondClientAs(fresh, bob.CharacterId).Get<MarriageState>("/marriage", m => mb = m);
            yield return new SecondClientAs(fresh, bob.CharacterId).Get<GuildViewState>("/guild", v => gb = v);
            Assert.AreEqual("married", mb.status, "persisted marriage after a new login");
            Assert.AreEqual("Chú Rể Long", mb.partnerName);
            Assert.AreEqual("Gió Chuông", gb.guild.name, "persisted guild after a new login");
            string wardrobe = null;
            yield return new SecondClientAs(fresh, bob.CharacterId).Raw("/wardrobe", j => wardrobe = j);
            StringAssert.Contains("wed_pearl_crown", wardrobe, "wedding gift owned");

            UiAudit.WriteReport($"{Dir}/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }

    /// <summary>Runs coroutines for the second client independently of the scene.</summary>
    public sealed class CoroutineHost : MonoBehaviour { private void Awake() => DontDestroyOnLoad(gameObject); }

    /// <summary>A logged-in SecondClient used for an existing character id (persistence checks).</summary>
    public sealed class SecondClientAs
    {
        private readonly SecondClient c; private readonly string id;
        public SecondClientAs(SecondClient client, string characterId) { c = client; id = characterId; }
        public IEnumerator Get<T>(string path, System.Action<T> got) => c.Call("GET", $"/characters/{id}{path}", null, j => got(JsonUtility.FromJson<T>(j)));
        public IEnumerator Raw(string path, System.Action<string> got) => c.Call("GET", $"/characters/{id}{path}", null, got);
    }
}
