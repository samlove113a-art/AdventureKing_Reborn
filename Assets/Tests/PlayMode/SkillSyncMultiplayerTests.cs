using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Two real clients on one server: player A (this game, the viewer) sees player B's EXACT skill — class + slot +
    /// target — replayed with the same choreography on B's avatar: a practice Thiên Thạch in the town (safe zone) and a
    /// server-accepted Cầu Lửa on a slime in Đồng Cỏ Gió (A's own slime view reacts). The viewer's camera is never
    /// shaken / frozen by someone else's skill. Captures: QA/SkillSync/*.png. Run with a window.
    /// </summary>
    public sealed class SkillSyncMultiplayerTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/SkillSync";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5321);
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
        }

        private static void Shot(string name)
        {
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        /// <summary>Waits for B's replay on A's screen, captures it and checks the viewer's camera stays calm.</summary>
        private IEnumerator SeeReplay(RemoteAvatar b, int before, string expect, string tag)
        {
            float until = Time.time + 6f;
            while (b.SkillsReplayed == before && Time.time < until) yield return null;
            Assert.Greater(b.SkillsReplayed, before, $"A replays B's skill ({tag}); last remote {SkillVfxDirector.LastRemoteSkill}");
            Assert.AreEqual(expect, SkillVfxDirector.LastRemoteSkill, "the exact class + slot");
            float t0 = Time.time, maxShake = 0f; bool shot1 = false, shot2 = false;
            while (Time.time - t0 < 2.2f)
            {
                maxShake = Mathf.Max(maxShake, CameraFollow2D.ShakeNow);
                Assert.AreEqual(1f, Time.timeScale, "no hit-stop on the viewer's screen");
                if (!shot1 && Time.time - t0 > 0.55f) { yield return new WaitForEndOfFrame(); Shot(tag + "_a"); shot1 = true; }
                if (!shot2 && Time.time - t0 > 1.25f) { yield return new WaitForEndOfFrame(); Shot(tag + "_b"); shot2 = true; }
                yield return null;
            }
            Assert.AreEqual(0f, maxShake, 0.0001f, "someone else's skill does not shake the viewer's camera");
        }

        [UnityTest]
        public IEnumerator Viewer_Sees_The_Exact_Skill_Another_Player_Casts()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("sync" + Random.Range(1000, 9999), "sync-pass-2026", HeroClass.Swordsman, "Người Xem A");
            motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            var qp = Object.FindAnyObjectByType<QuestPanel>(); if (qp) qp.gameObject.SetActive(false);

            // ---- player B (independent client, Pháp Sư) stands 3 m to A's right in the town
            var b = new SecondClient(server.ApiUrl);
            yield return b.Register("syncb" + Random.Range(1000, 9999), "sync-pass-2026", "Pháp Sư B", HeroClass.Mage);
            b.X = Pos.x + 3f; b.Y = Pos.y; b.Facing = -1;
            var runner = new GameObject("SecondClientRunner").AddComponent<CoroutineHost>();
            Object.DontDestroyOnLoad(runner.gameObject); // B keeps beating while A changes scene
            runner.StartCoroutine(b.HeartbeatLoop());
            var sync = WorldSync.Current;
            until = Time.time + 8f;
            while (!sync.Avatars.ContainsKey(b.CharacterId) && Time.time < until) yield return null;
            Assert.IsTrue(sync.Avatars.ContainsKey(b.CharacterId), "A sees B");
            RemoteAvatar av = sync.Avatars[b.CharacterId];
            yield return new WaitForSeconds(1f);

            // ---- 1) practice Thiên Thạch in the town (safe zone)
            int before = av.SkillsReplayed;
            b.Practice(3);
            yield return SeeReplay(av, before, "Mage_3", "1_town_practice_meteor");

            // ---- 2) on the farm: B's server-accepted Cầu Lửa on Slime Bông meadow_s1, seen by A
            b.Heartbeat = false;
            yield return new WaitForSeconds(0.3f);
            yield return b.Travel("hub_east_meadow", 66f);
            var rb = motor.GetComponent<Rigidbody2D>();
            float portal = TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X;
            rb.position = new Vector2(portal - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(portal, 8f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true); yield return null; yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false);
            var world = AdventureWorldSession.Current;
            until = Time.time + 3f;
            while (!world.ConfirmOpen && Time.time < until) yield return null;
            world.ConfirmPortal();
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>();
            qp = Object.FindAnyObjectByType<QuestPanel>(); if (qp) qp.gameObject.SetActive(false);
            FarmField farm = FarmField.Current;
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;
            FarmMonsterView slime = farm.Monsters["meadow_s1"];
            // A watches from between them (B at x -27, the slime at -22: both in A's frame); B faces right
            yield return WalkTo(-25.2f);
            b.Facing = 1; b.Heartbeat = true;
            for (float x = b.X; x < -27f; x = Mathf.Min(-27f, x + 2f)) { b.X = x; yield return b.SyncOnce(); yield return new WaitForSeconds(0.17f); }
            b.X = -27f;
            runner.StartCoroutine(b.HeartbeatLoop());
            sync = WorldSync.Current;
            until = Time.time + 8f;
            while (!sync.Avatars.ContainsKey(b.CharacterId) && Time.time < until) yield return null;
            Assert.IsTrue(sync.Avatars.ContainsKey(b.CharacterId), "A sees B in the meadow");
            av = sync.Avatars[b.CharacterId];
            yield return new WaitForSeconds(1.5f); // B's avatar settles at x -27
            before = av.SkillsReplayed;
            int reactions = slime.Reactions;
            yield return b.Skill(1, "meadow_s1");
            Assert.IsTrue(string.IsNullOrEmpty(b.LastError) || !b.LastError.Contains("/farm/skill"), "B's skill accepted: " + b.LastError);
            yield return SeeReplay(av, before, "Mage_1", "2_meadow_fireball");
            Assert.Greater(slime.Reactions, reactions, "A's own view of the slime reacts to B's fireball");
            b.Heartbeat = false;
        }

        private sealed class CoroutineHost : MonoBehaviour { }
    }
}
