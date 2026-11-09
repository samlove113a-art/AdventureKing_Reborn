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
            server = new TestServer(5203, starterGold: 5000); // fixture: enough Gold to exercise several purchases (production starter Gold is 800)
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StopRecording();
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

        [UnityTest]
        public IEnumerator Merchant_TryOn_Buy_Wear_Survive_Relog_And_Restart()
        {
            const string account = "fashion.tester@test.vn", password = "fashion-pass-2026", heroName = "Kiếm Khách Lụa";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter(account, password, HeroClass.Swordsman, heroName);
            Bind();
            AdventureWorldSession world = AdventureWorldSession.Current;
            Assert.AreEqual(5000, world.Character.gold, "starter Gold granted by the server");
            Assert.AreEqual(0, world.Outfit.PieceCount, "new hero wears nothing extra");
            // new character: let the Elder finish his welcome first (it locks movement while he talks)
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            Assert.IsTrue(input.enabled, "movement unlocked after the Elder");
            recorder = GameplayRecorder.Begin("fashion_shop");

            Debug.Log("[SHOP-TEST] step 1");

            // ---- 1. walk to the market stall: prompt appears only in range
            var merchant = Object.FindAnyObjectByType<FashionMerchant>();
            Assert.IsNotNull(merchant, "fashion merchant in town");
            Assert.IsFalse(merchant.PlayerNear, "no prompt from far away");
            yield return WalkTo(merchant.transform.position.x + 1.6f);
            yield return null;
            Assert.IsTrue(merchant.PlayerNear, "prompt when standing next to the merchant");
            Assert.IsTrue(merchant.TalkButton.gameObject.activeInHierarchy, "touch talk button shown in range");
            yield return Shot("01_merchant_prompt");

            Debug.Log("[SHOP-TEST] step 2");

            // ---- 2. talk (same path as E / the touch button): greeting by name + 4 options
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, true);
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, false);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(merchant.MenuOpen, "merchant menu opened");
            StringAssert.Contains(heroName, merchant.LastLine, "greets the character by name");
            Assert.IsFalse(input.enabled, "hero holds still while talking");
            yield return new WaitForSeconds(1.4f);
            yield return Shot("02_merchant_greeting");
            string first = merchant.LastLine;
            merchant.ChatLine();
            Assert.AreNotEqual(first, merchant.LastLine, "small talk answers");

            Debug.Log("[SHOP-TEST] step 3");

            // ---- 3. open the Gold shop: catalogue + prices from the server
            merchant.OpenShop(FashionShopUI.Tab.Gold);
            var shop = Object.FindAnyObjectByType<FashionShopUI>();
            yield return WaitIdle(shop);
            Assert.IsTrue(shop.IsOpen);
            Assert.IsNotNull(shop.Wardrobe, "wardrobe loaded: " + shop.Status);
            Assert.AreEqual(5000, shop.Wardrobe.gold);
            var gold = shop.Visible().ToList();
            Assert.GreaterOrEqual(gold.Count, 10, "Gold fashion pieces listed for this class");
            Assert.IsTrue(gold.All(i => i.currency == "gold" && !i.IsSet));
            Assert.IsNotNull(shop.PreviewModel, "3D fitting-room hero");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("03_shop_gold");
            shop.SetSlotFilter("shoes");
            Assert.IsTrue(shop.Visible().All(i => i.slot == "shoes") && shop.Visible().Count() == 2, "slot filter");
            shop.SetSlotFilter("");

            Debug.Log("[SHOP-TEST] step 4");

            // ---- 4. try-on is free and preview-only
            string realBefore = Worn(world.Character.outfit);
            shop.Select("back_wind_cape", true);
            shop.Select("head_bell_circlet", true);
            Assert.AreEqual("back_wind_cape", shop.TryOn.Get("back"));
            Assert.AreEqual("head_bell_circlet", shop.TryOn.Get("head"));
            Assert.Greater(shop.PreviewOutfit.PieceCount, 0, "preview dressed");
            Assert.AreEqual(5000, shop.Wardrobe.gold, "try-on charges nothing");
            Assert.AreEqual(realBefore, Worn(world.Character.outfit), "real hero unchanged by try-on");
            Assert.AreEqual(0, world.Outfit.PieceCount);
            shop.PlayPose("Run");
            yield return new WaitForSeconds(1.2f);
            shop.RotatePreview(-60f);
            yield return Shot("04_tryon_run");
            shop.PlayPose("Jump");
            yield return new WaitForSeconds(0.45f);
            yield return Shot("04b_tryon_jump");
            shop.PlayPose("Idle");
            shop.Close();
            yield return null;
            Assert.AreEqual(0, world.Outfit.PieceCount, "closing without buying leaves the real hero as it was");
            Assert.AreEqual(0, shop.PurchaseRequestsSent);

            Debug.Log("[SHOP-TEST] step 5");

            // ---- 5. buy with confirmation; a double click sends one request
            shop.Open(FashionShopUI.Tab.Gold, on => input.enabled = on);
            yield return WaitIdle(shop);
            shop.Select("head_lotus_crown", true);
            shop.AskBuy();
            yield return Shot("05_confirm_purchase");
            shop.ConfirmYes();
            shop.Buy("head_lotus_crown"); // impatient second click while the first is in flight
            yield return WaitIdle(shop);
            Assert.AreEqual(1, shop.PurchaseRequestsSent, "double click -> one request");
            Assert.AreEqual(3800, shop.Wardrobe.gold, "server price charged once");
            Assert.IsTrue(shop.Wardrobe.Owns("head_lotus_crown"));
            Assert.AreEqual("head_lotus_crown", world.Character.outfit.Get("head"), "bought item worn for real");
            Assert.Greater(world.Outfit.PieceCount, 0, "town hero dressed");
            yield return Shot("06_bought_crown");

            Debug.Log("[SHOP-TEST] step 6");

            // ---- 6. several purchases in a row
            foreach (string id in new[] { "gloves_jade_silk", "shoes_cloud_boots", "acc_silver_bell" })
            {
                shop.Select(id, true);
                shop.Buy(id);
                yield return WaitIdle(shop);
                Assert.IsTrue(shop.Wardrobe.Owns(id), id + ": " + shop.Status);
            }
            Assert.AreEqual(5000 - 1200 - 700 - 900 - 600, shop.Wardrobe.gold);
            // an owned item cannot be bought again (server refuses, no charge)
            shop.Buy("gloves_jade_silk");
            yield return WaitIdle(shop);
            StringAssert.Contains("đã sở hữu", shop.Status.ToLowerInvariant());
            Assert.AreEqual(1600, shop.Wardrobe.gold);

            Debug.Log("[SHOP-TEST] step 7");

            // ---- 7. not enough Gold: the client says so, and the server refuses a forced request too
            shop.Select("back_dragonfly_wings", true);
            shop.AskBuy();
            StringAssert.Contains("Không đủ Vàng", shop.Status);
            shop.Buy("back_dragonfly_wings");
            yield return WaitIdle(shop);
            StringAssert.Contains("Không đủ Gold", shop.Status);
            Assert.AreEqual(1600, shop.Wardrobe.gold, "refused purchase charges nothing");
            Assert.IsFalse(shop.Wardrobe.Owns("back_dragonfly_wings"));
            yield return Shot("07_not_enough_gold");

            Debug.Log("[SHOP-TEST] step 8");

            // ---- 8. premium tab: full sets for Diamonds, refused at 0 Diamonds
            shop.SetTab(FashionShopUI.Tab.Premium);
            var premium = shop.Visible().ToList();
            Assert.IsTrue(premium.Count >= 1 && premium.All(i => i.IsSet && i.ForDiamonds), "premium tab lists Diamond sets only");
            shop.Select("set_crystal_zephyr", true);
            Assert.AreEqual("set_crystal_zephyr", shop.TryOn.set, "premium set can be tried on");
            shop.AskBuy();
            StringAssert.Contains("Không đủ Kim Cương", shop.Status);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("08_premium_tryon");
            shop.Select("set_crystal_zephyr", false);
            shop.ToggleTry();

            Debug.Log("[SHOP-TEST] step 9");

            // ---- 9. wardrobe: unwear and wear again (saved on the server each time)
            shop.SetTab(FashionShopUI.Tab.Wardrobe);
            Assert.AreEqual(4, shop.Visible().Count(), "wardrobe shows the 4 owned items");
            shop.Select("gloves_jade_silk", false);
            shop.ToggleWear();
            yield return WaitIdle(shop);
            Assert.IsNull(world.Character.outfit.Get("gloves"), "gloves taken off");
            shop.ToggleWear();
            yield return WaitIdle(shop);
            Assert.AreEqual("gloves_jade_silk", world.Character.outfit.Get("gloves"), "gloves on again");
            yield return Shot("09_wardrobe");
            yield return Shot("09b_wardrobe_phone", 2340, 1080);
            shop.Close();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(input.enabled, "control returns after the shop");
            yield return Shot("10_town_dressed");

            Debug.Log("[SHOP-TEST] step 10");

            // ---- 10. "Lưu & Thoát", restart the server, log in: the outfit comes back from the database
            yield return WalkTo(merchant.transform.position.x + 6f); // walk away dressed (on video)
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true);
            yield return new WaitForSeconds(0.2f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false);
            yield return new WaitForSeconds(1.2f);
            StopRecording();
            string wornBefore = Worn(world.Character.outfit);
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
            Assert.AreEqual(1600, world.Character.gold, "Gold after restart");
            Assert.Greater(world.Outfit.PieceCount, 0, "hero dressed again on spawn");
            yield return Shot("11_after_restart_relog");
        }

        // ---------------------------------------------------------------- every class, every state
        private static readonly string[] Heroes = { "Swordsman", "Mage", "Assassin", "Archer" };

        public static readonly (string name, OutfitLoadout look)[] Looks =
        {
            ("gold_A", new OutfitLoadout().With("head", "head_lotus_crown").With("top", "top_brocade_vest").With("bottom", "bottom_pleat_skirt")
                .With("gloves", "gloves_jade_silk").With("shoes", "shoes_cloud_boots").With("accessory", "acc_silver_bell").With("back", "back_wind_cape")),
            ("gold_B", new OutfitLoadout().With("head", "head_bell_circlet").With("shoes", "shoes_ember_boots").With("back", "back_dragonfly_wings")),
            ("premium", new OutfitLoadout().With("gloves", "gloves_jade_silk").WithSet("set_crystal_zephyr")),
            ("fun_duck", new OutfitLoadout().With("head", "head_duck_buddy").With("gloves", "gloves_jade_silk").With("shoes", "shoes_ember_boots")),
            ("fun_bunny", new OutfitLoadout().With("head", "head_bunny_ears").With("bottom", "bottom_pleat_skirt").With("shoes", "shoes_cloud_boots")),
            ("fun_frog", new OutfitLoadout().With("head", "head_frog_eyes").With("accessory", "acc_silver_bell").With("shoes", "shoes_cloud_boots")),
            ("set_dino", new OutfitLoadout().WithSet("set_little_dino")),
            ("set_shark", new OutfitLoadout().WithSet("set_baby_shark")),
            ("set_dream", new OutfitLoadout().With("shoes", "shoes_cloud_boots").WithSet("set_sweet_dream")),
        };

        /// <summary>Every catalog item (server config) has real art that builds and skins on every class.</summary>
        [UnityTest]
        public IEnumerator Every_Catalog_Item_Builds_On_Every_Class()
        {
            string json = System.IO.File.ReadAllText("Server/AdventureKing.Server/config/cosmetics.json");
            var ids = System.Text.RegularExpressions.Regex.Matches(json, "\"id\":\\s*\"([a-z0-9_]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.GreaterOrEqual(ids.Length, 21, "catalog items");
            var lib = CosmeticLibrary.Load();
            for (int hi = 0; hi < Heroes.Length; hi++)
            {
#if UNITY_EDITOR
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{Heroes[hi]}_Visual.prefab");
#else
                GameObject prefab = null;
#endif
                GameObject go = Object.Instantiate(prefab);
                HeroOutfit outfit = AdventureWorldSession.DressHero(go, (HeroClass)hi);
                foreach (string id in ids)
                {
                    CosmeticArt art = lib.Get(id);
                    Assert.IsNotNull(art, id + ": listed in the server catalog but has no 3D art");
                    outfit.Apply(art.slot == "set" ? new OutfitLoadout().WithSet(id) : new OutfitLoadout().With(art.slot, id));
                    yield return null;
                    Assert.Greater(outfit.PieceCount, 0, $"{Heroes[hi]} {id}: pieces built");
                    foreach (GameObject p in outfit.Pieces)
                    {
                        var smr = p ? p.GetComponent<SkinnedMeshRenderer>() : null;
                        if (!smr) continue;
                        Assert.IsTrue(smr.bones.All(b => b), $"{Heroes[hi]} {id} {p.name}: all bones resolved");
                        Assert.Greater(smr.sharedMesh.vertexCount, 0, $"{Heroes[hi]} {id} {p.name}: has vertices");
                        // (placement is checked visually in the per-state contact sheets: skinned-renderer bounds are bone-derived and coarse)
                    }
                }
                Object.Destroy(go);
                yield return null;
            }
        }

        /// <summary>
        /// Each class wears each test look while the real animator plays Idle, Run, Jump, Climb, Attack and Skill.
        /// Checks: every piece is built and skinned to this hero's bones, the crown stays on the head, the weapon
        /// stays in the hand, the face layer still blinks, nothing throws. Contact sheets -> QA/Shop/states_*.png.
        /// </summary>
        [UnityTest]
        public IEnumerator Outfits_Follow_Every_State_On_All_Classes()
        {
            var lightGo = new GameObject("Test_Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var camGo = new GameObject("Test_Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.8f, 0.86f, 0.93f);
            cam.fieldOfView = 20f;
            camGo.transform.position = new Vector3(0f, 0.85f, 5.2f);
            camGo.transform.LookAt(new Vector3(0f, 0.75f, 0f));
            string[] states = { "Idle", "Run", "Jump", "Climb", "Attack", "Skill" };
            const int cell = 300;
            try
            {
                foreach (var (lookName, look) in Looks)
                {
                    var sheet = new Texture2D(cell * states.Length, cell * Heroes.Length, TextureFormat.RGB24, false);
                    for (int hi = 0; hi < Heroes.Length; hi++)
                    {
#if UNITY_EDITOR
                        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Heroes/{Heroes[hi]}_Visual.prefab");
#else
                        GameObject prefab = null;
#endif
                        GameObject go = Object.Instantiate(prefab);
                        go.transform.localScale = Vector3.one * 1.55f;
                        var anim = go.GetComponentInChildren<Animator>();
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        HeroOutfit outfit = AdventureWorldSession.DressHero(go, (HeroClass)hi);
                        outfit.Apply(look);
                        Assert.Greater(outfit.PieceCount, 2, $"{Heroes[hi]} {lookName} pieces built");
                        foreach (GameObject p in outfit.Pieces)
                        {
                            var smr = p.GetComponent<SkinnedMeshRenderer>();
                            if (!smr) continue;
                            Assert.IsTrue(smr.bones.All(b => b), $"{p.name}: all bones resolved");
                            Assert.Greater(smr.sharedMesh.vertexCount, 0, p.name + " has vertices");
                        }
                        var all = go.GetComponentsInChildren<Transform>();
                        Transform head = all.First(t => t.name == "Head");
                        var weapon = go.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s => s.name.StartsWith("Weapon_"));
                        HeroFingers fingers = go.GetComponent<HeroFingers>();
                        var face = go.GetComponent<HeroFace>();
                        Assert.IsTrue(face && face.enabled, "face layer still active");
                        for (int si = 0; si < states.Length; si++)
                        {
                            string st = states[si];
                            anim.SetBool("Climbing", st == "Climb");
                            anim.SetBool("Grounded", st != "Jump");
                            anim.SetFloat("Speed", st == "Run" ? 4.5f : 0f);
                            anim.SetFloat("VerticalSpeed", st == "Jump" ? 5f : 0f);
                            if (st == "Attack") anim.SetTrigger("Attack");
                            if (st == "Skill") anim.SetTrigger("Skill");
                            if (st == "Climb") anim.SetFloat("ClimbPhase", 0.3f);
                            outfit.SpeedOverride = st == "Run" ? 4.5f : 0f;
                            outfit.Airborne = st == "Jump";
                            go.transform.rotation = Quaternion.Euler(0f, st == "Climb" ? 180f : st == "Idle" ? 160f : 70f, 0f);
                            yield return new WaitForSeconds(st == "Attack" || st == "Skill" ? 0.45f : 0.7f);
                            // the crown/circlet rides on the head in every state
                            GameObject headPiece = outfit.Pieces.FirstOrDefault(p => p && (p.name.Contains("crown") || p.name.Contains("circlet")) && p.GetComponent<SkinnedMeshRenderer>());
                            if (headPiece)
                            {
                                Bounds b = headPiece.GetComponent<SkinnedMeshRenderer>().bounds;
                                Assert.Less(Vector3.Distance(b.center, head.position), 0.75f, $"{Heroes[hi]} {lookName} {st}: head piece on the head");
                            }
                            if (weapon && fingers)
                            {
                                Transform hand = (fingers.right.weaponGrip >= 0f ? fingers.right : fingers.left).joints[0].parent;
                                var baked = new Mesh();
                                weapon.BakeMesh(baked, true);
                                Matrix4x4 m = weapon.transform.localToWorldMatrix;
                                float near = baked.vertices.Min(v => Vector3.Distance(m.MultiplyPoint3x4(v), hand.position));
                                Object.Destroy(baked);
                                Assert.Less(near, 0.12f, $"{Heroes[hi]} {lookName} {st}: weapon still in the hand");
                            }
                            var rt = new RenderTexture(cell, cell, 24) { antiAliasing = 4 };
                            cam.targetTexture = rt;
                            cam.Render();
                            RenderTexture.active = rt;
                            sheet.ReadPixels(new Rect(0, 0, cell, cell), si * cell, (Heroes.Length - 1 - hi) * cell);
                            RenderTexture.active = null;
                            cam.targetTexture = null;
                            rt.Release();
                        }
                        Object.Destroy(go);
                        yield return null;
                    }
                    sheet.Apply();
                    File.WriteAllBytes($"{Dir}/states_{lookName}.png", sheet.EncodeToPNG());
                    Object.Destroy(sheet);
                }
            }
            finally
            {
                Object.Destroy(camGo);
                Object.Destroy(lightGo);
            }
        }
    }
}
