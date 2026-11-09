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
            server = new TestServer(5221); // real production config (starter Gold = current proposal, 5000)
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
            long start = world.Character.gold;
            Assert.AreEqual(5000, start, "production starter Gold (proposal)");
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
            until = Time.realtimeSinceStartup + 4f;
            while (!(AdventureAudioDirector.Instance.CurrentMusic && AdventureAudioDirector.Instance.CurrentMusic.name.Contains("BGM_Meadow")) && Time.realtimeSinceStartup < until) yield return null;
            StringAssert.Contains("BGM_Meadow", AdventureAudioDirector.Instance.CurrentMusic.name, "farm map plays its own theme");
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;
            Assert.AreEqual(8, farm.Monsters.Count, "server spawns shown");
            Assert.IsTrue(farm.Monsters.Values.All(m => m.Alive));
            FarmMonsterView first = farm.Monsters["meadow_s1"];
            Assert.IsTrue(first.NameLabel && first.NameLabel.isActiveAndEnabled, "monster name shown above its HP bar");
            Assert.AreEqual("Slime Bông", first.NameLabel.text);

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
            Assert.Greater(slime.Strikes, 0, "the slime fought back on the server's timer while being hit");
            var health = motor.GetComponent<Health2D>();
            Assert.Less(health.CurrentHealth, health.MaxHealth, "hero HP lowered by server-decided hits");
            Assert.AreEqual(1, farm.Kills);
            Assert.AreEqual(1, farm.Drops.Count, "one Gold drop on the ground");
            GoldDropView drop = farm.Drops.Values.First();
            long amount = drop.Amount;
            Assert.That(amount, Is.InRange(5, 11), "server-rolled Gold");
            yield return new WaitForSeconds(0.35f);
            yield return Shot("03a_gold_flying");
            yield return new WaitForSeconds(0.45f);
            Assert.IsTrue(drop.Landed, "coins landed and bounced");
            yield return Shot("03_gold_on_ground");

            // ---- walk onto the coins: NO automatic pickup (Pets will do that later); press ↓/S (manual pickup)
            yield return WalkTo(drop.Ground.x + 0.6f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, farm.GoldPickedUp, "standing on the coins does not pick them up by itself");
            Assert.IsNotNull(farm.DropInReach(), "the coins are in reach");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Pickup, true);
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Pickup, false);
            until = Time.time + 5f;
            while (farm.GoldPickedUp == 0 && Time.time < until) yield return null;
            Assert.AreEqual(amount, farm.GoldPickedUp, "picked up; last error: " + farm.LastError);
            Assert.AreEqual(1, farm.CoinChimes, "coin chime played once");
            Assert.AreEqual(start + amount, world.Character.gold);
            yield return new WaitForSeconds(0.3f);
            var hud = Object.FindAnyObjectByType<PlayerHud>();
            Assert.AreEqual((start + amount).ToString("N0", new System.Globalization.CultureInfo("vi-VN")), hud.GoldShown, "HUD shows the server balance");
            yield return Shot("04_picked_up");

            // the server really has it (fresh read, not the client's copy)
            bool done = false; long serverGold = -1;
            AdventureServerConnection.Instance.GetWardrobe(world.Character.characterId, (ok, w, err) => { done = true; serverGold = ok ? w.gold : -1; });
            while (!done) yield return null;
            Assert.AreEqual(start + amount, serverGold, "server wallet credited exactly once");

            // ---- the slime comes back on the server's 60 s timer, without any client action
            until = Time.time + 70f;
            while (!slime.Alive && Time.time < until) yield return null;
            Assert.IsTrue(slime.Alive, "respawned by the server timer");
            Assert.AreEqual(slime.MaxHp, slime.Hp);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("05_respawned");
        }

        /// <summary>A fragile Mage gets knocked out by a slime it provoked: death screen, no actions, server revive.</summary>
        [UnityTest]
        public IEnumerator Monster_Knocks_Out_Hero_Death_Screen_And_Server_Revive()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("farmm" + Random.Range(1000, 9999), "farm-pass-2026", HeroClass.Mage, "Pháp Sư Liều");
            var world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
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
            until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;

            // one fireball at the slime (Mage reach 6 m), then stand right next to it and let it fight back
            yield return WalkTo(-23f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true);
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
            FarmMonsterView slime = farm.Monsters["meadow_s1"];
            until = Time.time + 3f;
            while (slime.Hp == slime.MaxHp && Time.time < until) yield return null;
            Assert.Less(slime.Hp, slime.MaxHp, "hit landed; last error: " + farm.LastError);
            yield return new WaitForSeconds(2.2f);
            yield return Shot("06_slime_fights_back");
            until = Time.time + 30f; // 75 HP, 8 per hit every 1.6 s
            while (!farm.HeroDead && Time.time < until) yield return null;
            Assert.IsTrue(farm.HeroDead, "knocked out by server-decided hits; last error: " + farm.LastError);
            Assert.AreEqual(0, motor.GetComponent<Health2D>().CurrentHealth);
            Assert.IsTrue(farm.DeathPanel && farm.DeathPanel.activeSelf, "death screen");
            Assert.IsFalse(input.enabled, "no moving or attacking while down");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("07_knocked_out");

            farm.Revive();
            until = Time.time + 5f;
            while (farm.HeroDead && Time.time < until) yield return null;
            Assert.IsFalse(farm.HeroDead, "revived by the server");
            Assert.AreEqual(TownLayout.Meadow.Spawn.x, motor.transform.position.x, 1f, "back at the village gate");
            Assert.AreEqual(75, motor.GetComponent<Health2D>().CurrentHealth, "full HP from the server");
            Assert.IsTrue(input.enabled);
            var visual = motor.GetComponent<CharacterVisual3D>();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, visual.Revived, "the body stood up on revive");
            Assert.IsFalse(visual.InDeathPose, "not lying down after revive (no Attack press needed)");
            yield return new WaitForSeconds(1.2f); // a heartbeat later the server agrees (no teleport refusal)
            Assert.IsFalse(farm.HeroDead);
            Assert.IsFalse(visual.InDeathPose);
            yield return Shot("08_revived");

            // walk, jump and swing normally again
            float x0 = Pos.x;
            yield return WalkTo(x0 + 2.5f, 6f);
            Assert.Greater(Pos.x, x0 + 2f, "walks after revive");
            float y0 = Pos.y;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true);
            float peak = y0; until = Time.time + 1.2f;
            while (Time.time < until) { peak = Mathf.Max(peak, Pos.y); yield return null; if (Time.time > until - 1f) input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false); }
            Assert.Greater(peak, y0 + 0.5f, "jumps after revive");
            yield return new WaitForSeconds(0.8f);
            var anim = visual.Animator;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true);
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
            bool swung = false; until = Time.time + 1f;
            while (Time.time < until && !swung) { swung = anim.GetCurrentAnimatorStateInfo(0).IsName("Attack") || anim.GetNextAnimatorStateInfo(0).IsName("Attack"); yield return null; }
            Assert.IsTrue(swung, "attacks after revive");
            yield return Shot("09_after_revive_moving");
        }
    }
}
