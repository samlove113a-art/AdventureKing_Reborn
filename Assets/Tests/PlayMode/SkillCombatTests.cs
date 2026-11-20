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
    /// Phase B on the real server: class skills with server damage/MP/cooldown, the live cooldown + MP UI, potions,
    /// Auto Skill (casts only with a target in range, never moves the hero), the bag window with equipment.
    /// Screenshots: QA/Skills/*.png
    /// </summary>
    public sealed class SkillCombatTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5241);
            yield return server.Start();
            Directory.CreateDirectory("QA/Skills");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            var bar = Object.FindAnyObjectByType<HeroActionBar>();
            if (bar && bar.Auto) bar.ToggleAuto(); // local preference: leave it off for other tests
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

        private IEnumerator Tap(PlayerInput2D.TouchAction a)
        {
            input.SetTouchHeld(a, true);
            yield return null;
            input.SetTouchHeld(a, false);
            yield return null;
        }

        /// <summary>World + HUD capture (same path as the farm/hub tests; works in -batchmode).</summary>
        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Skills/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Skills_Mp_Cooldown_Potions_Auto_And_Bag_On_The_Real_Server()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("skills" + Random.Range(1000, 9999), "skill-pass-2026", HeroClass.Mage, "Pháp Sư Lửa");
            var world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
            var bar = Object.FindAnyObjectByType<HeroActionBar>();
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            Assert.IsNotNull(bar, "combat cluster on the HUD");
            Assert.IsNotNull(bag, "bag window");
            if (bar.Auto) bar.ToggleAuto();
            float until = Time.time + 8f;
            while ((SkillBook.Config == null || bag.State == null) && Time.time < until) yield return null;
            Assert.IsNotNull(SkillBook.Config, "skill table from the server");
            Assert.IsNotNull(bag.State, "bag from the server (starter kit)");
            yield return null;
            Assert.AreEqual("skill_mage_fireball", bar.IconShown(1).name);
            Assert.AreEqual("skill_mage_meteor", bar.IconShown(3).name);
            Assert.AreEqual("10", bar.HpCountShown, "starter HP potions shown on the button");
            Assert.AreEqual("10", bar.MpCountShown);

            // ---- hub = safe zone: a skill press is refused with a message, nothing is cast
            yield return Tap(PlayerInput2D.TouchAction.Skill1);
            StringAssert.Contains("khu an toàn", bar.Message);

            // ---- to the meadow (start beside the portal; the hub route is covered by TownHubTests)
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(TownLayout.Hub.Portals[0].X - 3f, 1.6f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(TownLayout.Hub.Portals[0].X, 10f);
            yield return Tap(PlayerInput2D.TouchAction.Up);
            until = Time.time + 3f;
            while (!world.ConfirmOpen && Time.time < until) yield return null;
            world.ConfirmPortal();
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
            bar = Object.FindAnyObjectByType<HeroActionBar>();
            bag = Object.FindAnyObjectByType<InventoryPanel>();
            FarmField farm = FarmField.Current;
            until = Time.time + 6f;
            while ((farm.Monsters.Count == 0 || farm.LastHero == null) && Time.time < until) yield return null;
            var mana = motor.GetComponent<PlayerMana>();
            Assert.AreEqual(140, Mathf.RoundToInt(mana.Max), "server MP pool (Pháp Sư lv1)");

            // ---- Thiên Thạch (key 3) at the first slime: server damage, 45 MP, 20 s cooldown on the button
            yield return WalkTo(-27f);
            FarmMonsterView slime = farm.Monsters["meadow_s1"];
            yield return Tap(PlayerInput2D.TouchAction.Skill3);
            until = Time.time + 4f;
            while (farm.SkillsCast == 0 && Time.time < until) yield return null;
            Assert.AreEqual(1, farm.SkillsCast, "cast accepted; last error: " + farm.LastSkillError);
            // the server already decided the kill; the client shows it on the meteor's impact beat (SkillVfxDirector)
            until = Time.time + 3f;
            while (slime.Alive && Time.time < until) yield return null;
            Assert.IsFalse(slime.Alive, "meteor (34 x 3.6 = 122) kills a 60 HP slime");
            Assert.LessOrEqual(Mathf.RoundToInt(mana.Current), 140 - 45 + 7, "MP spent per the server (+ regen while the meteor falls)");
            yield return new WaitForSeconds(0.4f);
            StringAssert.IsMatch("^(19|20)$", bar.SecondsShown(3), "cooldown seconds on the button");
            Assert.Greater(bar.SweepShown(3), 0.9f, "cooldown sweep almost full");
            yield return Shot("01_meteor_cooldown");
            // pressing again while cooling: refused locally with the remaining time, no request
            yield return Tap(PlayerInput2D.TouchAction.Skill3);
            StringAssert.Contains("hồi chiêu", bar.Message);
            Assert.AreEqual(1, farm.SkillsCast);

            // ---- not enough MP: the button turns blue-grey with "MP" and a press only explains why
            // (display only; the next server heartbeat restores the real value, so check + press in the same frame)
            mana.SetFromServer(5, 140);
            bar.SkillButton(2).Press();
            StringAssert.Contains("Không đủ MP", bar.Message);
            mana.SetFromServer(5, 140);
            bar.RefreshSlots();
            Assert.IsTrue(bar.MpWarnShown(2), "MP warning on skill 2 (blue-grey + MP)");
            Assert.AreEqual(1, farm.SkillsCast);
            yield return new WaitForSeconds(1.2f); // heartbeat brings the server's real MP back
            Assert.Greater(mana.Current, 80f);

            // ---- MP potion (R): exactly one used, MP restored by the server
            int mpBefore = Mathf.RoundToInt(mana.Current);
            yield return Tap(PlayerInput2D.TouchAction.PotionMp);
            until = Time.time + 4f;
            while (bar.MpCountShown == "10" && Time.time < until) yield return null;
            Assert.AreEqual("9", bar.MpCountShown, "one MP potion consumed");
            Assert.Greater(Mathf.RoundToInt(mana.Current), mpBefore, "MP restored");

            // ---- Auto Skill: next slime (x=-14) in range -> casts by itself; hero never moves
            yield return WalkTo(-19f);
            Vector2 standing = Pos;
            bar.ToggleAuto();
            Assert.IsTrue(bar.Auto);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("02_auto_on");
            until = Time.time + 8f;
            while (bar.AutoCasts == 0 && Time.time < until) yield return null;
            Assert.Greater(bar.AutoCasts, 0, "Auto Skill cast at the monster in range; last error: " + farm.LastSkillError);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(standing.x, Pos.x, 0.15f, "Auto Skill never moves the hero");
            // nothing in range -> no casts
            yield return WalkTo(-34f);
            yield return new WaitForSeconds(0.5f);
            int casts = bar.AutoCasts;
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(casts, bar.AutoCasts, "no monster in range: Auto Skill stays idle");
            bar.ToggleAuto();
            Assert.AreEqual(0, farm.GoldPickedUp, "Auto Skill never picks up Gold");

            // ---- bag (I): open, equip the class weapon, the server applies the bonus
            yield return Tap(PlayerInput2D.TouchAction.Inventory);
            Assert.IsTrue(bag.IsOpen, "bag window open");
            until = Time.time + 4f;
            while (bag.Busy && Time.time < until) yield return null;
            BagSlotState staff = bag.State.slots.First(s => s.itemId == "gear_novice_staff");
            bag.Select(staff.index);
            yield return null;
            yield return Shot("03_bag_selected");
            bag.EquipSelected();
            until = Time.time + 4f;
            while (bag.Busy && Time.time < until) yield return null;
            Assert.AreEqual(6, bag.State.attackBonus, "staff +6 attack from the server");
            Assert.IsTrue(bag.State.equipped.Any(e => e.equipSlot == "weapon" && e.item.itemId == "gear_novice_staff"));
            Assert.IsFalse(bag.State.slots.Any(s => s.itemId == "gear_novice_staff"), "moved out of the bag, not copied");
            yield return null;
            yield return Shot("04_bag_equipped");
            bag.Close();
        }
    }
}
