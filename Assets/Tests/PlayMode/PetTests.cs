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
    /// Pets on the real server: buy (confirmation, Gold), summon (the model follows the hero), a full pet refuses food,
    /// and on the farm the fed pet runs to the owner's coins and the server pays that drop once (no manual pickup).
    /// Screenshots: QA/Pets/*.png
    /// </summary>
    public sealed class PetTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5261);
            yield return server.Start();
            Directory.CreateDirectory("QA/Pets");
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

        private IEnumerator Tap(PlayerInput2D.TouchAction a) { input.SetTouchHeld(a, true); yield return null; input.SetTouchHeld(a, false); yield return null; }

        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Pets/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Buy_Summon_Follow_And_Pet_Collects_The_Owners_Gold()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("pets" + Random.Range(1000, 9999), "pet-pass-2026", HeroClass.Mage, "Chủ Gà Bông");
            var world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            // ---- Pet window (P): buy the chick (2 500 Vàng) with a confirmation
            var panel = Object.FindAnyObjectByType<PetPanel>();
            var dir = PetDirector.Current;
            Assert.IsNotNull(panel); Assert.IsNotNull(dir);
            yield return Tap(PlayerInput2D.TouchAction.Pet);
            Assert.IsTrue(panel.IsOpen, "pet window open");
            until = Time.time + 5f;
            while (dir.State == null && Time.time < until) yield return null;
            Assert.AreEqual(4, dir.State.catalog.Length, "2 Gold pets + 2 VIP pets");
            yield return Shot("01_pet_shop");
            long gold0 = world.Character.gold;
            panel.SetVip(false);
            int chick = dir.State.catalog.Where(p => p.tier == "normal").ToList().FindIndex(p => p.id == "pet_chick");
            panel.AskBuy(chick);
            Assert.IsTrue(panel.ConfirmOpen, "confirm before buying");
            panel.ConfirmBuy();
            until = Time.time + 5f;
            while ((panel.Busy || !dir.State.owned.Any()) && Time.time < until) yield return null;
            Assert.AreEqual(gold0 - 2500, world.Character.gold, "server Gold after the pet");
            // VIP needs Diamonds (payments off): refused honestly
            panel.SetVip(true);
            panel.AskBuy(0); panel.ConfirmBuy();
            until = Time.time + 5f;
            while (panel.Busy && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.2f);
            StringAssert.Contains("Kim Cương", panel.LastMessage);
            panel.SetVip(false);
            panel.ToggleSummon(chick);
            until = Time.time + 5f;
            while (!dir.Pet && Time.time < until) yield return null;
            Assert.IsNotNull(dir.Pet, "pet model spawned");
            // a full pet refuses food (nothing wasted)
            panel.Feed(chick);
            until = Time.time + 4f;
            while (panel.Busy && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(panel.LastMessage.Contains("no") || panel.LastMessage.Contains("thức ăn"), panel.LastMessage);
            yield return Shot("02_pet_owned");
            panel.Close();

            // ---- it follows the hero in town
            yield return WalkTo(Pos.x + 6f, 10f);
            yield return new WaitForSeconds(1.2f);
            Assert.Less(Mathf.Abs(dir.Pet.position.x - Pos.x), 2.2f, "pet follows the hero");
            yield return Shot("03_pet_follows");

            // ---- meadow: kill a slime from range, stand 4 m from the coins: the pet fetches them
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(TownLayout.Hub.Portals[0].X - 3f, 1.6f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(TownLayout.Hub.Portals[0].X, 10f);
            yield return Tap(PlayerInput2D.TouchAction.Up);
            until = Time.time + 3f;
            while (!world.ConfirmOpen && Time.time < until) yield return null;
            world.ConfirmPortal();
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            world = AdventureWorldSession.Current;
            motor = world.Player; input = motor.GetComponent<PlayerInput2D>();
            dir = PetDirector.Current;
            FarmField farm = FarmField.Current;
            until = Time.time + 8f;
            while ((farm.Monsters.Count == 0 || farm.LastHero == null || !dir.Pet) && Time.time < until) yield return null;
            Assert.IsNotNull(dir.Pet, "pet came along to the meadow");
            yield return WalkTo(-27f);
            yield return Tap(PlayerInput2D.TouchAction.Skill3);           // meteor kills slime s1 at -22
            until = Time.time + 6f;
            while (farm.Drops.Count == 0 && Time.time < until) yield return null;
            Assert.AreEqual(1, farm.Drops.Count, "Gold dropped; " + farm.LastSkillError);
            long amount = farm.Drops.Values.First().Amount;
            Assert.IsNull(farm.DropInReach(), "the coins are out of the hero's own reach");
            until = Time.time + 8f;
            while (farm.PetGoldPickedUp == 0 && Time.time < until) { if (dir.Mood == "fetch") yield return null; else yield return null; }
            Assert.AreEqual(amount, farm.PetGoldPickedUp, "the pet collected the drop; last error " + farm.LastError);
            Assert.AreEqual(1, dir.Collected);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("04_pet_collected");
            bool done = false; long serverGold = -1;
            AdventureServerConnection.Instance.GetWardrobe(world.Character.characterId, (ok, w, err) => { done = true; serverGold = ok ? w.gold : -1; });
            while (!done) yield return null;
            Assert.AreEqual(world.Character.gold, serverGold, "server wallet = HUD");

            // ---- pet auto-potion: turn HP on at 90 % in the Pet window (server settings), get hit once by a slime:
            // the server has the pet give one HP potion from the bag (count drops by one), the pet shows +HP
            var petPanel = Object.FindAnyObjectByType<PetPanel>();
            petPanel.Open();
            until = Time.time + 5f;
            while ((petPanel.Auto == null || petPanel.Busy) && Time.time < until) yield return null;
            Assert.IsFalse(petPanel.Auto.hpEnabled, "off by default");
            petPanel.ToggleAuto(true);
            until = Time.time + 5f;
            while (petPanel.Busy && Time.time < until) yield return null;
            for (int i = 0; i < 10 && petPanel.Auto.hpPercent < 90; i++)
            {
                petPanel.StepAuto(true, 5);
                until = Time.time + 5f;
                while (petPanel.Busy && Time.time < until) yield return null;
            }
            Assert.IsTrue(petPanel.Auto.hpEnabled);
            Assert.AreEqual(90, petPanel.Auto.hpPercent, "clamped at the server's 90 % maximum");
            var problems = new System.Collections.Generic.List<string>();
            yield return UiAudit.Screen("QA/Pets", "05_pet_auto_potion_settings", problems);
            UiAudit.WriteReport("QA/Pets/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            petPanel.Close();
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            int hpPotions = bag.PotionCount(true);
            Assert.Greater(hpPotions, 0, "starter HP potions in the bag");
            yield return WalkTo(-15.2f);                                  // next to slime s2 (-14)
            yield return Tap(PlayerInput2D.TouchAction.Attack);           // aggro: it strikes back
            until = Time.time + 15f;
            while (farm.PetDrinks == 0 && Time.time < until) yield return null;
            yield return null; yield return null;
            Assert.AreEqual(1, farm.PetDrinks, "one server-decided pet potion; last error " + farm.LastError);
            Assert.Greater(farm.LastPetDrink.hp, 0);
            Assert.AreEqual(1, dir.PotionsGiven);
            Assert.AreEqual("potion", dir.Mood);
            yield return Shot("06_pet_gives_potion");
            until = Time.time + 5f;
            while (bag.PotionCount(true) != hpPotions - 1 && Time.time < until) yield return null;
            Assert.AreEqual(hpPotions - 1, bag.PotionCount(true), "exactly one potion left the server bag");
        }
    }
}
