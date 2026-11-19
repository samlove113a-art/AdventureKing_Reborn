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
    /// The whole fashion loop in Play Mode with the real server process (nothing mocked):
    /// walk to the merchant -> prompt -> greeting by name -> shop -> free try-on (Gold untouched, real hero
    /// unchanged) -> buy (server price) -> worn in town -> rapid repeat clicks / owned / not enough Gold or
    /// Diamonds refused without charge -> wardrobe wear/unwear -> "Lưu & Thoát" -> server restart -> login ->
    /// the same outfit is restored. Screens: QA/Shop/.
    /// </summary>
    public sealed class ShopFashionTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Shop";
        private GameplayRecorder recorder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            HeroVisualStyle.Override = "2d";   // a saved QA "3d" choice must not turn the 2D fashion test into a 3D one
            server = new TestServer(5203, starterGold: 200000); // fixture: enough Gold to buy several 2D fashion pieces (explicit fixture)
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StopRecording();
            HeroVisualStyle.Override = null;
            Time.captureFramerate = 0; // never leak a fixed game clock into the next test (coyote-time tests are frame sensitive)
            Time.timeScale = 1f;
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private void Bind()
        {
            motor = AdventureWorldSession.Current.Player;
            input = motor.GetComponent<PlayerInput2D>();
        }

        private IEnumerator WalkTo(float x, float timeout = 40f)
        {
            float end = Time.time + timeout;
            while (Mathf.Abs(motor.transform.position.x - x) > 0.3f)
            {
                bool right = motor.transform.position.x < x;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, right);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, !right);
                yield return null;
                Assert.Less(Time.time, end, $"timeout walking to {x}, at {motor.transform.position}");
            }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            yield return null; // (no WaitForEndOfFrame: it never resumes in -batchmode)
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

        private static IEnumerator WaitIdle(FashionShopUI shop, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            while (shop.Busy && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(shop.Busy, "shop request timed out; status: " + shop.Status);
            yield return null;
        }

        private void StopRecording()
        {
            if (!recorder) return;
            File.WriteAllText($"{Dir}/video_frames.txt", recorder.Directory + "\n" + recorder.Frames + "\n");
            recorder.End();
            recorder = null;
        }

        private static string Worn(OutfitLoadout o) => o == null ? "" : o.ToString();


        private static string SpeedText(AdventureWorldSession w) => w.Player.SpeedMultiplier.ToString("0.00");

        /// <summary>
        /// The whole 2D fashion loop with the real server: shop shows only THIS class's sets, try-on is free and preview-only, buy a set (server price),
        /// wear a hat + a mount (speed bonus + lifted hero), refuse what the balance cannot pay, Diamonds tab, wardrobe wear/unwear, relog + server restart.
        /// </summary>
        [UnityTest]
        public IEnumerator Merchant_TryOn_Buy_Wear_Mount_Survive_Relog_And_Restart()
        {
            const string account = "fashion.tester@test.vn", password = "fashion-pass-2026", heroName = "Kiếm Lụa";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter(account, password, HeroClass.Mage, heroName);
            Bind();
            AdventureWorldSession world = AdventureWorldSession.Current;
            Assert.AreEqual(200000, world.Character.gold, "starter Gold granted by the server");
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            Assert.IsTrue(input.enabled, "movement unlocked after the Elder");
            var doll = world.HeroModel.GetComponent<Paperdoll2D>();
            Assert.IsNotNull(doll, "2D hero has a paper doll; model=" + world.HeroModel.name + " style2d=" + HeroVisualStyle.Chibi2D);

            // ---- 1. walk to the market stall, talk, open the Gold shop
            var merchant = Object.FindAnyObjectByType<FashionMerchant>();
            Assert.IsNotNull(merchant, "fashion merchant in town");
            yield return WalkTo(merchant.transform.position.x + 1.6f);
            yield return null;
            Assert.IsTrue(merchant.PlayerNear, "prompt when standing next to the merchant");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, true);
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, false);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(merchant.MenuOpen, "merchant menu opened");
            StringAssert.Contains(heroName, merchant.LastLine, "greets the character by name");
            merchant.OpenShop(FashionShopUI.Tab.Gold);
            var shop = Object.FindAnyObjectByType<FashionShopUI>();
            yield return WaitIdle(shop);
            Assert.IsTrue(shop.IsOpen);
            Assert.IsNotNull(shop.Wardrobe, "wardrobe loaded: " + shop.Status);
            Assert.IsNotNull(shop.PreviewModel, "2D fitting-room hero");
            Assert.IsNotNull(shop.PreviewDoll, "fitting-room paper doll");
            Assert.AreEqual(0, shop.PreviewModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length + shop.PreviewModel.GetComponentsInChildren<MeshRenderer>(true).Length, "no 3D in the fitting room");

            // ---- 2. class filter: only this class's sets (Pháp Sư = 1) are shown, in Gold; the other classes' sets are hidden
            var sets = shop.Visible().Where(i => i.IsSet).ToList();
            Assert.GreaterOrEqual(sets.Count, 5, "several Gold sets for the Swordsman");
            Assert.IsTrue(sets.All(i => i.classes.Length == 1 && i.classes[0] == 1 && !i.ForDiamonds), "only Mage Gold sets");
            Assert.IsEmpty(shop.Visible().Where(i => i.id.StartsWith("set2d_assassin") || i.id.StartsWith("set2d_swordsman") || i.id.StartsWith("set2d_archer")), "other classes' sets hidden");
            shop.SetSlotFilter("mount");
            Assert.IsTrue(shop.Visible().Count() >= 1 && shop.Visible().All(i => i.slot == "mount"), "slot filter: mounts");
            shop.SetSlotFilter("wings");
            Assert.IsTrue(shop.Visible().Count() >= 1 && shop.Visible().All(i => i.slot == "wings"), "slot filter: wings");
            shop.SetSlotFilter("");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("01_shop_gold");

            // ---- 3. try-on is free and preview-only
            WardrobeItem setItem = sets.OrderBy(i => i.price).First();
            string realBefore = Worn(world.Character.outfit);
            shop.Select(setItem.id, true);
            Assert.AreEqual(setItem.id, shop.TryOn.set);
            foreach (string piece in FashionCatalog2D.Get(setItem.id).pieces) Assert.AreEqual(FashionCatalog2D.PieceId(setItem.id, piece), shop.PreviewDoll.Equipped(Slot(piece)), "preview wears the set piece in its own slot: " + piece);
            Assert.AreEqual(200000, shop.Wardrobe.gold, "try-on charges nothing");
            Assert.AreEqual(realBefore, Worn(world.Character.outfit), "real hero unchanged by try-on");
            Assert.IsNull(doll.Equipped(PaperSlot.Top), "real 2D hero not dressed by a try-on");
            shop.PlayPose("Run");
            yield return new WaitForSeconds(0.8f);
            shop.TurnPreview();
            Assert.AreEqual(-1, shop.PreviewFacing);
            yield return Shot("02_tryon_run");
            shop.PlayPose("Idle");
            shop.Close();
            yield return null;
            Assert.IsNull(doll.Equipped(PaperSlot.Top), "closing without buying leaves the hero as it was");
            Assert.AreEqual(0, shop.PurchaseRequestsSent);

            // ---- 4. buy the set: one request, server price, worn for real
            shop.Open(FashionShopUI.Tab.Gold, on => input.enabled = on);
            yield return WaitIdle(shop);
            shop.Select(setItem.id, true);
            shop.AskBuy();
            shop.ConfirmYes();
            shop.Buy(setItem.id);                                  // impatient second click while the first is in flight
            yield return WaitIdle(shop);
            Assert.AreEqual(1, shop.PurchaseRequestsSent, "double click -> one request");
            Assert.AreEqual(200000 - setItem.price, shop.Wardrobe.gold, "server price charged once");
            Assert.AreEqual(setItem.id, world.Character.outfit.set, "bought set worn for real");
            foreach (string piece in FashionCatalog2D.Get(setItem.id).pieces) Assert.AreEqual(FashionCatalog2D.PieceId(setItem.id, piece), doll.Equipped(Slot(piece)), "2D hero wears the set piece: " + piece);
            Assert.IsTrue(doll.Body.enabled, "the set never hides the whole body: pieces are separate layers");
            yield return Shot("03_bought_set");

            // ---- 5. a hat and a mount: layers stack in the right order, the mount lifts the hero and adds speed
            WardrobeItem hat = shop.Wardrobe.items.First(i => FashionCatalog2D.Has(i.id) && i.slot == "wings" && !i.ForDiamonds && i.usableByCharacter);
            WardrobeItem mount = shop.Wardrobe.items.First(i => FashionCatalog2D.Has(i.id) && i.slot == "mount" && !i.ForDiamonds);
            foreach (WardrobeItem it in new[] { hat, mount })
            {
                shop.Select(it.id, true);
                shop.Buy(it.id);
                yield return WaitIdle(shop);
                Assert.IsTrue(shop.Wardrobe.Owns(it.id), it.id + ": " + shop.Status);
            }
            Assert.AreEqual(hat.id, doll.Equipped(PaperSlot.Wings));
            Assert.AreEqual(FashionCatalog2D.PieceId(setItem.id, "top"), doll.Equipped(PaperSlot.Top), "adding a hat / mount left the top as it was");
            Assert.AreEqual(FashionCatalog2D.PieceId(setItem.id, "bottom"), doll.Equipped(PaperSlot.Bottom), "... and the bottom");
            Assert.AreEqual(mount.id, doll.Equipped(PaperSlot.Mount));
            Assert.Greater(doll.Lift, 0.3f, "mount lifts the hero onto the saddle");
            // taking ONE piece of the set off leaves every other slot as it was
            shop.SetTab(FashionShopUI.Tab.Wardrobe);
            Assert.Greater(world.Player.SpeedMultiplier, 1.1f, "mount + set speed bonus applied: " + SpeedText(world));
            Assert.AreEqual(1f + FashionCatalog2D.MoveSpeedPct(world.Character.outfit) / 100f, world.Player.SpeedMultiplier, 0.001f);
            var sr = new Dictionary<string, int>();
            foreach (SpriteRenderer r in doll.GetComponentsInChildren<SpriteRenderer>()) sr[r.name] = r.sortingOrder;
            Assert.Less(sr["Paper_Mount"], doll.Body.sortingOrder, "mount behind the hero");
            Assert.Less(sr["Paper_Wings"], doll.Body.sortingOrder, "wings behind the hero");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("04_set_hat_mount");

            // ---- 6. not enough Gold is refused by the client and by the server, nothing charged
            long gold = shop.Wardrobe.gold;
            WardrobeItem dear = shop.Wardrobe.items.Where(i => FashionCatalog2D.Has(i.id) && !i.owned && !i.ForDiamonds && i.usableByCharacter && i.price > gold).OrderBy(i => i.price).FirstOrDefault();
            if (dear == null)
            {
                // spend down until something is out of reach (the balance is only a fixture)
                foreach (WardrobeItem w in shop.Wardrobe.items.Where(i => FashionCatalog2D.Has(i.id) && !i.owned && !i.ForDiamonds && i.usableByCharacter && i.slot != "set").OrderByDescending(i => i.price).Take(6))
                {
                    if (shop.Wardrobe.gold < w.price) continue;
                    shop.Select(w.id, false); shop.Buy(w.id);
                    yield return WaitIdle(shop);
                }
                gold = shop.Wardrobe.gold;
                dear = shop.Wardrobe.items.Where(i => FashionCatalog2D.Has(i.id) && !i.owned && !i.ForDiamonds && i.usableByCharacter && i.price > gold).OrderBy(i => i.price).FirstOrDefault();
            }
            if (dear != null)
            {
                shop.Select(dear.id, true);
                shop.AskBuy();
                StringAssert.Contains("Không đủ Vàng", shop.Status);
                shop.Buy(dear.id);
                yield return WaitIdle(shop);
                StringAssert.Contains("Không đủ Gold", shop.Status);
                Assert.AreEqual(gold, shop.Wardrobe.gold, "refused purchase charges nothing");
                Assert.IsFalse(shop.Wardrobe.Owns(dear.id));
            }

            // ---- 7. Diamonds tab: only Diamond items, refused at 0 Diamonds
            shop.SetTab(FashionShopUI.Tab.Premium);
            var premium = shop.Visible().ToList();
            Assert.IsTrue(premium.Count >= 1 && premium.All(i => i.ForDiamonds), "Diamonds tab lists Diamond items only");
            WardrobeItem rare = premium.First(i => i.usableByCharacter);
            shop.Select(rare.id, true);
            shop.AskBuy();
            StringAssert.Contains("Không đủ Kim Cương", shop.Status);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("05_diamonds_tab");

            // ---- 8. wardrobe: unwear and wear the hat again (saved on the server each time)
            shop.SetTab(FashionShopUI.Tab.Wardrobe);
            Assert.GreaterOrEqual(shop.Visible().Count(), 3, "wardrobe shows the owned items");
            shop.Select(hat.id, false);
            if (world.Character.outfit.Get("wings") != hat.id)      // a later purchase may have put other wings on: wear these first
            {
                shop.ToggleWear();
                yield return WaitIdle(shop);
            }
            Assert.AreEqual(hat.id, world.Character.outfit.Get("wings"), "wings worn");
            shop.ToggleWear();
            yield return WaitIdle(shop);
            Assert.IsNull(world.Character.outfit.Get("wings"), "wings taken off");
            Assert.IsNull(doll.Equipped(PaperSlot.Wings));
            shop.ToggleWear();
            yield return WaitIdle(shop);
            Assert.AreEqual(hat.id, world.Character.outfit.Get("wings"), "wings on again");
            yield return Shot("06_wardrobe");
            yield return Shot("06b_wardrobe_phone", 2340, 1080);
            shop.Close();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(input.enabled, "control returns after the shop");

            // ---- 9. relog + server restart: the same outfit (set, hat, mount) comes back from the database, speed included
            string wornBefore = Worn(world.Character.outfit);
            string mountBefore = world.Character.outfit.Get("mount");
            long goldBefore = world.Character.gold;
            world.ExitToLogin();
            yield return TestServer.WaitForSceneName("AK_Entry");
            yield return TestRunnerGuard.CloseGame();
            server.Stop(deleteData: false);
            yield return server.Start();
            yield return server.OpenGame();
            var flow = Object.FindAnyObjectByType<AdventureEntryFlow>();
            flow.SkipSplash();
            yield return null;
            EntryLobbyTests.Find<InputField>("AccountInput").text = account;
            EntryLobbyTests.Find<InputField>("PasswordInput").text = password;
            flow.LoginPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, flow.StatusMessage);
            flow.EnterSelectedCharacter();
            yield return TestServer.WaitForScene(TownLayout.Hub.Scene);
            world = AdventureWorldSession.Current;
            Assert.AreEqual(wornBefore, Worn(world.Character.outfit), "same outfit after relog + server restart");
            Assert.AreEqual(goldBefore, world.Character.gold, "Gold after restart");
            var doll2 = world.HeroModel.GetComponent<Paperdoll2D>();
            Assert.AreEqual(FashionCatalog2D.PieceId(setItem.id, "top"), doll2.Equipped(PaperSlot.Top), "set pieces restored");
            Assert.AreEqual(mountBefore, doll2.Equipped(PaperSlot.Mount), "mount restored");
            Assert.Greater(world.Player.SpeedMultiplier, 1.1f, "speed bonus restored");
            yield return Shot("07_after_restart_relog");
        }

        private static PaperSlot Slot(string slot) => slot switch { "top" => PaperSlot.Top, "bottom" => PaperSlot.Bottom, "wings" => PaperSlot.Wings, _ => PaperSlot.Mount };

        // ---------------------------------------------------------------- data completeness (no art is drawn by code: sprites are the owner's to assign)
        private static readonly string[] Heroes = { "Swordsman", "Mage", "Assassin", "Archer" };

        /// <summary>Every 2D catalogue item and set piece has an (empty) slot in the Fashion Database for the owner's sprites; anchors exist for the overlay clips.</summary>
        [UnityTest]
        public IEnumerator Every_2D_Item_Has_A_Database_Entry_For_The_Owners_Sprites()
        {
            FashionCatalog2D.Reset();
            var items = FashionCatalog2D.All.ToList();
            Assert.GreaterOrEqual(items.Count, 60, "2D catalogue items (40 sets + tops, bottoms, wings, mounts)");
            Assert.IsNotNull(FashionCatalog2D.Database, "Resources/Fashion/FashionDatabase.asset exists");
            foreach (string slot in new[] { "set", "top", "bottom", "wings", "mount" }) Assert.IsTrue(items.Any(i => i.slot == slot), "slot " + slot);
            foreach (string hero in Heroes)
                Assert.AreEqual(10, items.Count(i => i.slot == "set" && i.id.StartsWith("set2d_" + hero.ToLowerInvariant() + "_")), hero + ": 10 sets");
            foreach (FashionItem2D it in items)
            {
                Assert.IsNotNull(FashionCatalog2D.Visual(it.id), it.id + ": database entry");
                if (it.slot == "set")
                {
                    Assert.Contains("top", it.pieces); Assert.Contains("bottom", it.pieces);
                    foreach (string p in it.pieces) Assert.IsNotNull(FashionCatalog2D.Visual(FashionCatalog2D.PieceId(it.id, p)), it.id + "." + p);
                }
            }
            foreach (string h in Heroes)
                foreach (string clip in new[] { "Idle", "Run", "Jump", "Fall", "Hit", "Attack", "Skill" })
                    Assert.IsTrue(FashionCatalog2D.TryAnchor(h, clip, 0, out BodyAnchor a) && a.Head.y > 60f, $"anchor {h}_{clip}");
            yield return null;
        }

        /// <summary>
        /// Slots are independent layers: every class wears a set (pieces) + wings + mount, each with its own SpriteRenderer and sorting order;
        /// changing one slot leaves all the others untouched, and a mount lifts the hero. 2D only.
        /// </summary>
        [UnityTest]
        public IEnumerator Paper_Doll_Slots_Are_Independent_Layers_On_All_Classes()
        {
            FashionCatalog2D.Reset();
            foreach (string h in Heroes)
            {
                GameObject go = Object.Instantiate(Resources.Load<GameObject>($"Chibi2D/{h}_Visual2D"));
                Assert.AreEqual(0, go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length + go.GetComponentsInChildren<MeshRenderer>(true).Length, h + ": 2D only");
                var doll = go.GetComponent<Paperdoll2D>();
                string setId = FashionCatalog2D.All.First(i => i.slot == "set" && i.id.StartsWith("set2d_" + h.ToLowerInvariant() + "_")).id;
                OutfitLoadout look = FashionCatalog2D.WithSet(new OutfitLoadout(), setId).With("wings", "wings2d_f_seraph").With("mount", "mount2d_white_horse");
                doll.ApplyLoadout(look);
                yield return null;
                if (h == "Mage" || h == "Archer")
                {
                    // contact picture of the female look (Meshy wings) for the owner: QA/Shop/look_<class>.png
                    var camGo = new GameObject("Test_Camera");
                    var cam = camGo.AddComponent<Camera>();
                    cam.orthographic = true; cam.orthographicSize = 1.1f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.5f, 0.62f);
                    camGo.transform.position = new Vector3(0f, 0.95f, -10f);
                    go.transform.position = Vector3.zero;
                    doll.ApplyLoadout(new OutfitLoadout().With("wings", "wings2d_f_seraph"));
                    yield return null;
                    var rtp = new RenderTexture(512, 512, 24);
                    cam.targetTexture = rtp; cam.Render();
                    RenderTexture.active = rtp;
                    var tp = new Texture2D(512, 512, TextureFormat.RGB24, false); tp.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tp.Apply();
                    RenderTexture.active = null; cam.targetTexture = null; rtp.Release();
                    Directory.CreateDirectory(Dir);
                    File.WriteAllBytes($"{Dir}/look_{h}.png", tp.EncodeToPNG());
                    Object.Destroy(tp); Object.Destroy(camGo);
                    doll.ApplyLoadout(look);
                    yield return null;
                }
                var all = new[] { PaperSlot.Top, PaperSlot.Bottom, PaperSlot.Wings, PaperSlot.Mount };
                foreach (PaperSlot s in all) Assert.IsNotNull(doll.Equipped(s), $"{h} {s} worn");
                var renderers = all.Select(s => doll.RendererOf(s)).ToList();
                Assert.AreEqual(all.Length, renderers.Distinct().Count(), "one SpriteRenderer per slot");
                Assert.AreEqual(all.Length, renderers.Select(r => r.sortingOrder).Distinct().Count(), "one sorting order per slot");
                int bodyOrder = doll.Body.sortingOrder;
                Assert.Less(doll.RendererOf(PaperSlot.Mount).sortingOrder, doll.RendererOf(PaperSlot.Wings).sortingOrder);
                Assert.Less(doll.RendererOf(PaperSlot.Wings).sortingOrder, bodyOrder, "wings behind the body");
                Assert.Less(bodyOrder, doll.RendererOf(PaperSlot.Bottom).sortingOrder);
                Assert.Less(doll.RendererOf(PaperSlot.Bottom).sortingOrder, doll.RendererOf(PaperSlot.Top).sortingOrder);
               Assert.IsTrue(doll.Body.enabled, "no slot hides the body");
                Assert.Greater(doll.Lift, 0.3f, h + " lifted by the mount");
                // replace ONLY the top: every other slot is untouched
                string before = string.Join("|", all.Where(s => s != PaperSlot.Top).Select(s => doll.Equipped(s)));
                doll.ApplyLoadout(look.With("top", "top2d_gold"));
                Assert.AreEqual("top2d_gold", doll.Equipped(PaperSlot.Top));
                Assert.AreEqual(before, string.Join("|", all.Where(s => s != PaperSlot.Top).Select(s => doll.Equipped(s))), "other slots kept");
                Assert.AreSame(renderers[0], doll.RendererOf(PaperSlot.Top), "the same renderer is reused");
                // take the wings off: only the wings go
                doll.ApplyLoadout(look.With("top", "top2d_gold").With("wings", null));
                Assert.IsNull(doll.Equipped(PaperSlot.Wings));
                Assert.IsNotNull(doll.Equipped(PaperSlot.Top)); Assert.IsNotNull(doll.Equipped(PaperSlot.Mount));
                Object.Destroy(go);
                yield return null;
            }
        }
    }
}