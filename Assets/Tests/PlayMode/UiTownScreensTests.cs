using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// In-town screens through the real game + server, audited by <see cref="UiAudit"/> at PC / phone / tablet
    /// sizes: plain HUD, Elder welcome dialog, merchant menu, fashion shop (Gold + wardrobe), purchase confirmation,
    /// portal confirmation. PNGs + report: QA/UI/town/.
    /// </summary>
    public sealed class UiTownScreensTests
    {
        private TestServer server;
        private const string Dir = "QA/UI/town";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5213, starterGold: 5000, devWelfare: true); // fixture: the audit buys an item (explicit fixture)
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        [UnityTest]
        public IEnumerator Town_Screens_Pass_Ui_Audit_At_All_Resolutions()
        {
            var problems = new List<string>();
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ui.town@test.vn", "ui-town-pass-2026", HeroClass.Archer, "Cung Thủ Sớm");
            AdventureWorldSession world = AdventureWorldSession.Current;
            // no "Missing Script" anywhere in the town scene
            var missing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.GetComponents<Component>().Any(c => c == null)).Select(t => UiAudit.PathOf(t)).ToList();
            Assert.IsEmpty(missing, "missing scripts: " + string.Join(", ", missing));
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            Assert.IsTrue(greeter && greeter.DialogOpen, "Elder dialog");
            yield return UiAudit.Screen(Dir, "01_elder_dialog", problems);
            greeter.Close();
            until = Time.time + 15f;
            while (!greeter.Done && Time.time < until) yield return null;
            yield return new WaitForSeconds(3.5f); // map banner faded
            var hud = Object.FindAnyObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "player HUD");
            // the fold state is a saved local preference (shared with manual play sessions): start unfolded
            Object.FindAnyObjectByType<HudShortcutBar>().SetCollapsed(false, true);
            Assert.AreEqual("5.000", hud.GoldShown, "HUD Gold = server starter Gold");
            Assert.AreEqual("0", hud.DiamondsShown, "HUD Diamonds = server value");
            Assert.Greater(hud.HpFraction, 0.95f, "HP bar from the player Health2D");
            yield return UiAudit.Screen(Dir, "02_hud", problems);
            var menu = Object.FindAnyObjectByType<HudSystemMenu>();
            menu.Toggle();
            Assert.IsTrue(menu.IsOpen, "settings menu");
            yield return UiAudit.Screen(Dir, "02b_settings", problems);

            // ---- background music: village day theme really plays; music on/off and volume are separate from SFX
            var audio = AdventureAudioDirector.Instance;
            Assert.IsNotNull(audio, "audio director");
            bool wasMuted = audio.Muted, wasMusicOn = audio.MusicOn;
            audio.SetAllMuted(false);
            audio.SetMusicOn(true);
            until = Time.realtimeSinceStartup + 3f;
            while (!audio.MusicAudible && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(audio.MusicAudible, "village BGM playing and audible");
            StringAssert.Contains("BGM_Village_Day", audio.CurrentMusic.name);
            float sfxBefore = audio.EffectsVolume;
            menu.ToggleMusic();
            Assert.IsFalse(audio.MusicOn);
            Assert.IsFalse(audio.MusicAudible, "music off silences only the BGM");
            Assert.AreEqual(sfxBefore, audio.EffectsVolume, "SFX volume untouched");
            Assert.AreEqual("Nhạc nền: Tắt", menu.MusicLabelText);
            menu.ToggleMusic();
            Assert.IsTrue(audio.MusicOn);
            float mv = audio.MusicVolume;
            menu.CycleMusicVolume();
            Assert.AreNotEqual(mv, audio.MusicVolume, "music volume step");
            while (Mathf.Abs(audio.MusicVolume - 0.75f) > 0.01f) menu.CycleMusicVolume(); // back to default
            menu.CycleEffectsVolume();
            Assert.AreNotEqual(sfxBefore, audio.EffectsVolume, "SFX volume step");
            audio.SetEffectsVolume(sfxBefore);
            // night falls (display mode) -> the calm night theme fades in
            var prevMode = DayNightCycle.ModeOverride;
            DayNightCycle.ModeOverride = DayNightMode.Night;
            until = Time.realtimeSinceStartup + 4f;
            while (!(audio.CurrentMusic && audio.CurrentMusic.name.Contains("BGM_Village_Night") && audio.MusicAudible) && Time.realtimeSinceStartup < until) yield return null;
            StringAssert.Contains("BGM_Village_Night", audio.CurrentMusic.name, "night theme after dark");
            DayNightCycle.ModeOverride = prevMode;
            until = Time.realtimeSinceStartup + 4f;
            while (!audio.CurrentMusic.name.Contains("BGM_Village_Day") && Time.realtimeSinceStartup < until) yield return null;
            StringAssert.Contains("BGM_Village_Day", audio.CurrentMusic.name, "day theme back");
            audio.SetMusicOn(wasMusicOn);
            audio.SetAllMuted(wasMuted);
            menu.Close();
            var mail = Object.FindObjectsByType<HudFeatureButton>(FindObjectsInactive.Exclude).First(b => b.Title == "Hộp Thư");
            mail.Open();
            var popup = Object.FindAnyObjectByType<HudPopup>();
            Assert.IsTrue(popup.IsOpen, "shortcut popup");
            StringAssert.Contains("chưa được mở", popup.Message, "unbuilt features say so honestly");
            yield return UiAudit.Screen(Dir, "02c_shortcut_popup", problems);
            popup.Hide();

            // ---- 7-day check-in (dev reward config on this test server; production keeps the owner's draft off)
            var checkin = Object.FindAnyObjectByType<CheckinPanel>();
            Assert.IsNotNull(checkin, "check-in panel built");
            until = Time.time + 5f;
            while (!checkin.RedDotShown && Time.time < until) yield return null;
            Assert.IsTrue(checkin.RedDotShown, "red dot: the server says today's gift is claimable");
            checkin.Open();
            until = Time.time + 5f;
            while ((checkin.Last == null || checkin.Busy) && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(checkin.Last.canClaimToday);
            Assert.AreEqual("claimable", checkin.Last.days[0].state);
            yield return UiAudit.Screen(Dir, "02d_checkin", problems);
            long goldBefore = world.Character.gold;
            checkin.Claim();
            checkin.Claim(); // impatient double click: ignored while the first is in flight
            until = Time.time + 5f;
            while ((checkin.Busy || checkin.Last.claimedDays == 0) && Time.time < until) yield return null;
            Assert.AreEqual(1, checkin.Last.claimedDays, "day 1 claimed once");
            Assert.AreEqual(goldBefore + 200, world.Character.gold, "server-paid day-1 Gold (dev config) on the HUD");
            Assert.AreEqual("claimed", checkin.Last.days[0].state);
            Assert.IsFalse(checkin.Last.canClaimToday, "one claim per VN day");
            Assert.IsFalse(checkin.RedDotShown, "dot gone once claimed");
            yield return UiAudit.Screen(Dir, "02e_checkin_claimed", problems);
            checkin.Close();

            // ---- bag window (server inventory: starter kit) audited at every resolution
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            Assert.IsNotNull(bag, "bag window");
            bag.Open();
            until = Time.time + 5f;
            while ((bag.State == null || bag.Busy) && Time.time < until) yield return null;
            Assert.IsNotNull(bag.State, "bag from the server");
            bag.Select(bag.State.slots[0].index);
            yield return null;
            yield return UiAudit.Screen(Dir, "02g_bag", problems);
            bag.Close();

            // ---- real name above the hero's head (no fake VIP frame)
            var plate = Object.FindAnyObjectByType<PlayerNameplate>();
            Assert.IsNotNull(plate, "player nameplate");
            Assert.AreEqual("Cung Thủ Sớm", plate.Shown, "server character name above the head");

            // ---- fold arrow: the whole icon block folds away and back (PC too), minimap untouched
            var bar = Object.FindAnyObjectByType<HudShortcutBar>();
            bar.ToggleCollapsed();
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(bar.Collapsed);
            Assert.Less(bar.GetComponent<CanvasGroup>().alpha, 0.05f, "icons folded away");
            yield return UiAudit.Screen(Dir, "02f_icons_folded", problems);
            bar.ToggleCollapsed();
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(bar.Collapsed);
            Assert.Greater(bar.GetComponent<CanvasGroup>().alpha, 0.95f, "icons back");

            var merchant = Object.FindAnyObjectByType<FashionMerchant>();
            world.Player.transform.position = merchant.transform.position + new Vector3(1.6f, 0.9f, 0f);
            world.Player.GetComponent<Rigidbody2D>().position = world.Player.transform.position;
            yield return new WaitForSeconds(0.8f);
            merchant.Talk();
            yield return new WaitForSeconds(1.8f);
            Assert.IsTrue(merchant.MenuOpen, "merchant menu");
            yield return UiAudit.Screen(Dir, "03_merchant_menu", problems);
            merchant.OpenShop(FashionShopUI.Tab.Gold);
            var shop = Object.FindAnyObjectByType<FashionShopUI>();
            until = Time.realtimeSinceStartup + 10f;
            while ((shop.Busy || shop.Wardrobe == null) && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return UiAudit.Screen(Dir, "04_shop_gold", problems);
            shop.Select("head_lotus_crown", true);
            shop.AskBuy();
            yield return UiAudit.Screen(Dir, "05_shop_confirm", problems);
            shop.ConfirmYes();
            until = Time.realtimeSinceStartup + 10f;
            yield return null;
            while (shop.Busy && Time.realtimeSinceStartup < until) yield return null;
            shop.SetTab(FashionShopUI.Tab.Wardrobe);
            yield return UiAudit.Screen(Dir, "06_shop_wardrobe", problems);
            shop.Close();

            var portal = Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude).First();
            world.AskPortalConfirmation(portal);
            yield return null;
            Assert.IsTrue(world.ConfirmOpen, "portal confirm");
            yield return UiAudit.Screen(Dir, "07_portal_confirm", problems);
            world.CancelPortal();

            UiAudit.WriteReport($"{Dir}/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
