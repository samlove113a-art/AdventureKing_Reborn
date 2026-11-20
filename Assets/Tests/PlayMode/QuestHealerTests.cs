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
    /// Phase B (part 2) on the real server: the first main quests from the Elder, practice in town, Dược Sư Mộc Linh's
    /// shop (quantity, confirmation, Gold), a potion drunk on purpose, a teleport ticket used from the bag and the
    /// travel quest completing on arrival. Screenshots: QA/Quests/*.png
    /// </summary>
    public sealed class QuestHealerTests
    {
        private TestServer server;
        private PlayerInput2D input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5251);
            yield return server.Start();
            Directory.CreateDirectory("QA/Quests");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            QuestBook.Clear();
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private IEnumerator Tap(PlayerInput2D.TouchAction a)
        {
            input.SetTouchHeld(a, true);
            yield return null;
            input.SetTouchHeld(a, false);
            yield return null;
        }

        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Quests/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        private static IEnumerator WaitMain(string id, bool complete, float timeout = 8f)
        {
            float until = Time.time + timeout;
            while (Time.time < until)
            {
                QuestState q = QuestBook.CurrentMain;
                if (q != null && q.id == id && q.complete == complete) yield break;
                QuestBook.Refresh();
                yield return new WaitForSeconds(0.3f);
            }
            Assert.Fail($"main quest {id} complete={complete} not reached; now {QuestBook.CurrentMain?.id} complete={QuestBook.CurrentMain?.complete} err={QuestBook.LastError}");
        }

        private static IEnumerator Claim(QuestPanel panel, string id)
        {
            yield return WaitMain(id, true);
            panel.Show(QuestPanel.Tab.Main);
            yield return null;
            Assert.IsTrue(panel.ClaimVisible, "claim button for " + id);
            panel.ClaimShown();
            float until = Time.time + 5f;
            while ((QuestBook.CurrentMain?.id ?? "") == id && Time.time < until) yield return null;
            Assert.AreNotEqual(id, QuestBook.CurrentMain?.id, id + " claimed; " + panel.LastMessage);
        }

        [UnityTest]
        public IEnumerator First_Quests_Healer_Shop_Potion_Ticket_Travel()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("quest" + Random.Range(1000, 9999), "quest-pass-2026", HeroClass.Swordsman, "Lữ Khách Mới");
            var world = AdventureWorldSession.Current;
            input = world.Player.GetComponent<PlayerInput2D>();
            var panel = Object.FindAnyObjectByType<QuestPanel>();
            Assert.IsNotNull(panel, "quest tracker on the HUD");
            if (panel.Collapsed) panel.SetCollapsed(false);
            long gold0 = world.Character.gold;

            // ---- 1. Elder greets the new player; closing the dialog completes "Gặp Trưởng Làng"
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (!greeter.DialogOpen && Time.time < until) yield return null;
            StringAssert.Contains("Gặp Trưởng Làng", panel.BodyText, "first main quest shown");
            greeter.Close();
            yield return WaitMain("main_01", true);
            var elderMark = Object.FindObjectsByType<NpcQuestMarker>(FindObjectsInactive.Exclude).First(m => m.name == "NpcElder");
            yield return null;
            Assert.AreEqual("?", elderMark.Shown, "'?' over the Elder: quest to hand in");
            yield return Shot("01_elder_turn_in");
            yield return Claim(panel, "main_01");
            until = Time.time + 15f;
            while (!greeter.Done && Time.time < until) yield return null;

            // ---- 2. practice basic attack x3 in town
            for (int i = 0; i < 3; i++) { yield return Tap(PlayerInput2D.TouchAction.Attack); yield return new WaitForSeconds(0.9f); }
            yield return Claim(panel, "main_02");
            // ---- 3. practice the three skills (animation only in the safe zone)
            var bar = Object.FindAnyObjectByType<HeroActionBar>();
            foreach (var a in new[] { PlayerInput2D.TouchAction.Skill1, PlayerInput2D.TouchAction.Skill2, PlayerInput2D.TouchAction.Skill3 })
            { yield return Tap(a); yield return new WaitForSeconds(0.8f); }
            StringAssert.Contains("Luyện tập", bar.Message);
            yield return Claim(panel, "main_03");

            // ---- 4. "!" over Mộc Linh; talk to her: greeting + shop opens
            var healer = Object.FindAnyObjectByType<HealerNpc>();
            Assert.IsNotNull(healer, "Dược Sư Mộc Linh in the hub");
            var healerMark = healer.GetComponent<NpcQuestMarker>();
            yield return null;
            Assert.AreEqual("!", healerMark.Shown, "'!' over the healer (next quest)");
            Assert.IsNotNull(healer.Outfit, "healer dressed (lotus crown + wings)");
            Assert.Greater(healer.Outfit.PieceCount, 0);
            var rb = world.Player.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(healer.transform.position.x - 1.6f, 1.2f); rb.linearVelocity = Vector2.zero;
            world.Player.transform.position = rb.position;
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(healer.PlayerNear, "talk prompt near the healer");
            yield return Shot("02_healer_counter");
            yield return Tap(PlayerInput2D.TouchAction.Interact);
            var shop = Object.FindAnyObjectByType<HealerShopUI>();
            until = Time.time + 5f;
            while ((!shop.IsOpen || shop.Shop == null) && Time.time < until) yield return null;
            Assert.IsTrue(shop.IsOpen, "shop open");
            Assert.AreEqual(1, healer.Greetings);
            Assert.AreEqual(3, shop.Shop.tabs.Length, "Dược Phẩm + Vé Dịch Chuyển + Thức Ăn Pet");
            yield return WaitMain("main_04", true);

            // ---- buy 5 small HP potions through the UI: quantity, total, confirmation, server Gold
            shop.Select("potion_hp_s");
            shop.SetQty(5);
            Assert.AreEqual(150, shop.Total);
            shop.AskBuy();
            Assert.IsTrue(shop.ConfirmOpen, "confirmation before buying");
            yield return Shot("03_shop_confirm");
            shop.ConfirmYes();
            until = Time.time + 5f;
            while (shop.Busy || shop.LastMessage == "") { if (Time.time > until) break; yield return null; }
            StringAssert.Contains("Đã mua 5", shop.LastMessage);
            Assert.AreEqual(gold0 + 10 + 10 - 150, world.Character.gold, "server Gold after quest rewards (+10 +10) and the purchase (-150)");
            shop.SetQty(120);
            Assert.AreEqual(99, shop.Qty, "at most 99 per purchase");
            yield return Shot("04_shop_bought");
            shop.Close();
            yield return Claim(panel, "main_04");

            // ---- 5. buy one potion and drink it (bars are full in town: the bag asks to confirm drinking anyway)
            healer.Talk();
            until = Time.time + 3f;
            while (!shop.IsOpen && Time.time < until) yield return null;
            shop.Select("potion_mp_s"); shop.SetQty(1); shop.AskBuy(); shop.ConfirmYes();
            until = Time.time + 5f;
            while (shop.Busy && Time.time < until) yield return null;
            shop.Close();
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            bag.Open();
            until = Time.time + 5f;
            while ((bag.State == null || bag.Busy) && Time.time < until) yield return null;
            int mpBefore = bag.PotionCount(false);
            bag.Select(bag.State.slots.First(s => s.itemId == "potion_mp_s").index);
            bag.UseSelected();
            until = Time.time + 4f;
            while (bag.Busy || bag.LastMessage == "") { if (Time.time > until) break; yield return null; }
            StringAssert.Contains("Bấm Dùng lần nữa", bag.LastMessage, "full bar: asks before wasting a potion");
            Assert.AreEqual(mpBefore, bag.PotionCount(false));
            bag.UseSelected(); // deliberate second press
            until = Time.time + 4f;
            while (bag.PotionCount(false) == mpBefore && Time.time < until) yield return null;
            Assert.AreEqual(mpBefore - 1, bag.PotionCount(false), "exactly one potion used");
            bag.Close();
            yield return Claim(panel, "main_05");

            // ---- 6. buy a ticket, use it from the bag: the server removes it and moves the hero; quest completes there
            healer.Talk();
            until = Time.time + 3f;
            while (!shop.IsOpen && Time.time < until) yield return null;
            shop.SetTab(1);
            Assert.AreEqual("ticket_meadow", shop.Selected.itemId);
            shop.SetQty(1); shop.AskBuy(); shop.ConfirmYes();
            until = Time.time + 5f;
            while (shop.Busy && Time.time < until) yield return null;
            shop.Close();
            bag.Open();
            until = Time.time + 5f;
            while ((bag.Busy || !bag.State.slots.Any(s => s.itemId == "ticket_meadow")) && Time.time < until) { bag.Refresh(); yield return new WaitForSeconds(0.3f); }
            bag.Select(bag.State.slots.First(s => s.itemId == "ticket_meadow").index);
            bag.UseSelected();
            StringAssert.Contains("xác nhận", bag.LastMessage);
            yield return Shot("05_ticket_confirm");
            bag.UseSelected();
            yield return TestServer.WaitForScene(TownLayout.Meadow.Scene);
            world = AdventureWorldSession.Current;
            Assert.AreEqual(TownLayout.Meadow.MapId, world.Character.lastMapId);
            yield return WaitMain("main_06", true);
            yield return new WaitForSeconds(1f);
            yield return Shot("06_arrived_quest_done");
            bag = Object.FindAnyObjectByType<InventoryPanel>();
            until = Time.time + 5f;
            while (bag.State == null && Time.time < until) yield return null;
            Assert.IsFalse(bag.State.slots.Any(s => s.itemId == "ticket_meadow"), "ticket used up");
        }
    }
}
