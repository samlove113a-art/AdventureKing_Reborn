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
    /// Đợt 5 end-to-end on the real server: travel to the meadow, swing at a slime with the real attack input until
    /// the SERVER says it died, see the Gold drop on the ground, walk over it, the server credits the wallet once and
    /// the HUD shows the confirmed balance; the slime comes back by itself after the server's 60 s timer.
    /// Screenshots: QA/Farm/*.png
    /// </summary>
    public sealed class FarmTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5221); // real production config: starter Gold 800
            yield return server.Start();
            Directory.CreateDirectory("QA/Farm");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private Vector2 Pos => motor.transform.position;

        private IEnumerator WalkTo(float x, float timeout = 30f)
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

        /// <summary>World + HUD capture (same path as the hub test; works in -batchmode).</summary>
        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Farm/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Kill_Slime_Pick_Up_Gold_Server_Credits_Once_And_Respawns()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("farm" + Random.Range(1000, 9999), "farm-pass-2026", HeroClass.Swordsman, "Nông Dân Gió");
            var world = AdventureWorldSession.Current;
            Assert.AreEqual(800, world.Character.gold, "approved starter Gold");
            Assert.IsNull(Object.FindAnyObjectByType<FarmField>(), "the hub is a safe zone: no monsters");

            // ---- portal to the meadow
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
            // (the hub route itself — jumps, ladders, bridge — is covered by TownHubTests; start beside the portal)
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(TownLayout.Hub.Portals[0].X - 3f, 1.2f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            yield return WalkTo(TownLayout.Hub.Portals[0].X, 10f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true);
            yield return null; yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false);
            float until = Time.time + 3f;
            while (!world.ConfirmOpen && Time.time < until) yield return null;
            world.ConfirmPortal();
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();

            FarmField farm = FarmField.Current;
            Assert.IsNotNull(farm, "meadow has the farm");
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;
            Assert.AreEqual(8, farm.Monsters.Count, "server spawns shown");
            Assert.IsTrue(farm.Monsters.Values.All(m => m.Alive));

            // ---- walk up to the first slime (x = -22) and swing until the server says it died
            yield return WalkTo(-23.6f);
            yield return Shot("01_slime_in_front");
            FarmMonsterView slime = farm.Monsters["meadow_s1"];
            int swings = 0;
            until = Time.time + 15f;
            while (slime.Alive && Time.time < until)
            {
                input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true);
                yield return null;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
                swings++;
                if (swings == 2) { yield return new WaitForSeconds(0.25f); yield return Shot("02_hit"); }
                yield return new WaitForSeconds(0.95f); // Kiếm Sĩ pace
            }
            Assert.IsFalse(slime.Alive, "slime killed by the server; last error: " + farm.LastError);
            Assert.AreEqual(1, farm.Kills);
            Assert.AreEqual(1, farm.Drops.Count, "one Gold drop on the ground");
            GoldDropView drop = farm.Drops.Values.First();
            long amount = drop.Amount;
            Assert.That(amount, Is.InRange(5, 11), "server-rolled Gold");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("03_gold_on_ground");

            // ---- walk onto the coins: the server credits the wallet, the HUD shows it
            yield return WalkTo(drop.Ground.x + 0.6f);
            until = Time.time + 5f;
            while (farm.GoldPickedUp == 0 && Time.time < until) yield return null;
            Assert.AreEqual(amount, farm.GoldPickedUp, "picked up; last error: " + farm.LastError);
            Assert.AreEqual(800 + amount, world.Character.gold);
            yield return new WaitForSeconds(0.3f);
            var hud = Object.FindAnyObjectByType<PlayerHud>();
            Assert.AreEqual((800 + amount).ToString("N0", new System.Globalization.CultureInfo("vi-VN")), hud.GoldShown, "HUD shows the server balance");
            yield return Shot("04_picked_up");

            // the server really has it (fresh read, not the client's copy)
            bool done = false; long serverGold = -1;
            AdventureServerConnection.Instance.GetWardrobe(world.Character.characterId, (ok, w, err) => { done = true; serverGold = ok ? w.gold : -1; });
            while (!done) yield return null;
            Assert.AreEqual(800 + amount, serverGold, "server wallet credited exactly once");

            // ---- the slime comes back on the server's 60 s timer, without any client action
            until = Time.time + 70f;
            while (!slime.Alive && Time.time < until) yield return null;
            Assert.IsTrue(slime.Alive, "respawned by the server timer");
            Assert.AreEqual(slime.MaxHp, slime.Hp);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("05_respawned");
        }
    }
}
