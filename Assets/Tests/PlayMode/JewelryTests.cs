using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>Shop NPC groups in the hub (potions / pets + food / jewelry), rings + necklaces as bag + paperdoll slots, the forge enhancing a ring. Screenshots: QA/Jewelry/*.png</summary>
    public sealed class JewelryTests
    {
        private TestServer server;
        private string fixture;

        [UnitySetUp]
        public IEnumerator SetUp() { TestRunnerGuard.Protect(); string src = File.ReadAllText(Path.Combine(TestServer.ServerConfigDir, "items.json"));
            fixture = Path.Combine(Path.GetTempPath(), "ak_items_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(fixture, src.Replace("\"starterKit\": [", "\"starterKit\": [ { \"itemId\": \"enhance_stone\", \"qty\": 10 }, { \"itemId\": \"protection_charm\", \"qty\": 2 },"));   // fixture: stones for the forge
            server = new TestServer(5291, starterGold: 60000, extraArgs: $"--AdventureKing:ItemConfigPath=\"{fixture}\""); yield return server.Start(); Directory.CreateDirectory("QA/Jewelry"); }

        [UnityTearDown]
        public IEnumerator TearDown() { HeroVisualStyle.Override = null; yield return TestRunnerGuard.CloseGame(); server.Stop(); try { File.Delete(fixture); } catch (IOException) { } }

        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes($"QA/Jewelry/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; Object.Destroy(tex); rt.Release();
        }

        private static IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(cond(), "timeout: " + what);
        }

        [UnityTest]
        public IEnumerator Three_Shop_Groups_Rings_And_Necklaces_And_Forge_Work()
        {
            HeroVisualStyle.Override = "2d";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("jew" + Random.Range(1000, 9999), "jew-pass-2026", HeroClass.Mage, "Trang Sức");
            var world = AdventureWorldSession.Current;
            var rb = world.Player.GetComponent<Rigidbody2D>();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            // ---- the shop NPC groups exist and each has its own window / goods
            var keepers = Object.FindObjectsByType<ShopKeeperNpc>(FindObjectsInactive.Include);
            var jeweler = keepers.First(k => k.name == "NpcJeweler");
            var petKeeper = keepers.First(k => k.name == "NpcPetKeeper");
            Assert.IsNotNull(Object.FindAnyObjectByType<HealerNpc>(), "potion seller");
            Assert.IsNotNull(Object.FindAnyObjectByType<NpcActor>(), "NPC bodies");

            rb.position = new Vector2(petKeeper.transform.position.x + 0.2f, 1f); yield return new WaitForSeconds(0.8f);
            petKeeper.Talk();
            yield return Until(() => petKeeper.Shop.Shop != null, 8f, "pet shop loaded");
            Assert.AreEqual(new[] { "Thức Ăn Pet" }, petKeeper.Shop.Shop.tabs.Select(t => t.name).ToArray(), "pet keeper sells pet food only");
            yield return Shot("01_pet_keeper_shop");
            petKeeper.Shop.Close();

            rb.position = new Vector2(jeweler.transform.position.x + 0.2f, 1f); yield return new WaitForSeconds(0.8f);
            jeweler.Talk();
            yield return Until(() => jeweler.Shop.Shop != null, 8f, "jewelry shop loaded");
            Assert.AreEqual(new[] { "Nhẫn", "Dây Chuyền", "Đồ Rèn" }, jeweler.Shop.Shop.tabs.Select(t => t.name).ToArray());
            Assert.AreEqual(3, jeweler.Shop.Shop.tabs[0].items.Length, "3 shop rings");
            Assert.AreEqual(6, jeweler.Shop.Shop.tabs[1].items.Length, "6 shop necklaces");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02_jeweler_rings");
            jeweler.Shop.SetTab(1);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("03_jeweler_necklaces");

            // ---- buy a silver ring and a jade necklace
            foreach (string id in new[] { "gear_neck_jade" })
            {
                jeweler.Shop.Select(id); jeweler.Shop.AskBuy(); jeweler.Shop.ConfirmYes();
                yield return Until(() => !jeweler.Shop.Busy, 8f, "buying " + id);
            }
            jeweler.Shop.SetTab(0);
            jeweler.Shop.Select("gear_ring_silver"); jeweler.Shop.AskBuy(); jeweler.Shop.ConfirmYes();
            yield return Until(() => !jeweler.Shop.Busy, 8f, "buying the ring");
            jeweler.Shop.Close();

            // ---- wear them from the bag: ring + necklace have their own paperdoll slots
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            bag.Open();
            yield return Until(() => bag.State != null, 8f, "bag");
            yield return new WaitForSeconds(0.6f);
            foreach (string id in new[] { "gear_ring_silver", "gear_neck_jade" })
            {
                bag.Refresh(); yield return new WaitForSeconds(0.8f);
                var slot = bag.State.slots.FirstOrDefault(s => s.itemId == id);
                Assert.IsNotNull(slot, id + " is in the bag");
                bag.Select(slot.index);
                bag.EquipSelected();
                yield return Until(() => !bag.Busy, 8f, "wearing " + id);
                yield return new WaitForSeconds(0.5f);
            }
            Assert.IsTrue(bag.State.equipped.Any(e => e.equipSlot == "ring" && e.item.itemId == "gear_ring_silver"), "ring slot");
            Assert.IsTrue(bag.State.equipped.Any(e => e.equipSlot == "necklace" && e.item.itemId == "gear_neck_jade"), "necklace slot");
            yield return Shot("04_bag_ring_and_necklace_worn");
            bag.Close();

            // ---- the forge lists ring + necklace and enhances the ring (tier info + the 1.2 s anticipation)
            var smith = Object.FindAnyObjectByType<BlacksmithNpc>();
            var forge = Object.FindAnyObjectByType<BlacksmithUI>(FindObjectsInactive.Include);
            rb.position = new Vector2(smith.transform.position.x + 1.6f, 1f); yield return new WaitForSeconds(0.8f);
            var input = world.Player.GetComponent<PlayerInput2D>();
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, false);
            yield return Until(() => forge.IsOpen && forge.Inv != null, 8f, "forge");
            Assert.AreEqual(5, BlacksmithUI.Slots.Length);
            forge.SelectSlot("ring");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("gear_ring_silver", forge.Current.itemId);
            Assert.AreEqual("Bạc", forge.Current.tier, "blue rarity = Bạc tier");
            Assert.Greater(forge.Current.powerGain, 0, "the window knows the combat power of the next +1");
            yield return Shot("05_forge_ring");
            Assert.IsTrue(forge.CanStrike, $"can strike: stones {forge.StoneCount}, gold {world.Character.gold}, fee {forge.Current.enhanceGold}, busy {forge.Busy}");
            float t0 = Time.realtimeSinceStartup;
            forge.Strike();
            Assert.IsTrue(forge.Busy, "the forge is busy during the build-up");
            yield return Shot("06_forge_anticipation");
            yield return Until(() => !forge.Busy, 10f, "strike");
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - t0, BlacksmithUI.AnticipationSeconds - 0.15f, "the result waits for the ~1.2 s build-up");
            Assert.IsTrue(forge.LastSuccess, "+0 -> +1 always succeeds");
            Assert.AreEqual(1, forge.Current.enhanceLevel);
            yield return new WaitForSeconds(0.35f);
            yield return Shot("07_forge_ring_success");
        }
    }
}
