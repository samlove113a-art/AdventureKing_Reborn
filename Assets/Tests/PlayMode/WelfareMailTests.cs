using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Sự Kiện / Phúc Lợi, Hộp Thư, Giftcode and Bảng Xếp Hạng through the real HUD icons and the real server (dev
    /// reward config; production keeps the DRAFT off), audited by <see cref="UiAudit"/> at PC / phone / tablet sizes.
    /// PNGs + report: QA/UI/welfare/.
    /// </summary>
    public sealed class WelfareMailTests
    {
        private TestServer server;
        private const string Dir = "QA/UI/welfare";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5271, devWelfare: true);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private static Button Icon(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(b => b.name == "Shortcut_" + name);

        private static IEnumerator Until(System.Func<bool> ok, float seconds = 6f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ok() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Event_Welfare_Mail_Giftcode_And_Ranking_Windows_Use_Real_Server_Data()
        {
            var problems = new List<string>();
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("welfare" + Random.Range(1000, 9999), "welfare-pass-2026", HeroClass.Swordsman, "Phúc Lợi Kiếm");
            var world = AdventureWorldSession.Current;
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            yield return Until(() => greeter && greeter.DialogOpen, 20f);
            if (greeter && greeter.DialogOpen) greeter.Close();
            yield return Until(() => !greeter || greeter.Done, 15f);
            yield return new WaitForSeconds(3.5f);
            Object.FindAnyObjectByType<HudShortcutBar>().SetCollapsed(false, true);

            // ---- red dot only because the server says a gift is claimable (dev level-1 fixture), heartbeat running
            var welfare = Object.FindAnyObjectByType<WelfarePanel>();
            Assert.IsNotNull(welfare, "welfare window built");
            yield return Until(() => welfare.DotShown && welfare.Pings > 0, 8f);
            Assert.IsTrue(welfare.DotShown, "red dot from the server's anyClaimable");
            Assert.Greater(welfare.Pings, 0, "online heartbeat sent");

            // ---- Sự kiện icon -> Săn Nấm Lễ Hội with 3 tasks at 0 progress
            Icon("event").onClick.Invoke();
            Assert.IsTrue(welfare.IsOpen);
            yield return Until(() => welfare.State != null);
            Assert.IsTrue(welfare.State.rewardsEnabled, "dev config");
            WelfareTabState ev = welfare.State.tabs.First(t => t.id.StartsWith("event:"));
            Assert.AreEqual("Săn Nấm Lễ Hội", ev.title);
            Assert.AreEqual(3, ev.steps.Length);
            Assert.IsTrue(ev.steps.All(s => s.have == 0 && !s.claimable));
            Assert.AreEqual(6, welfare.State.tabs.Length, "Sự Kiện Mới, Đăng Nhập, Nạp Đầu, Online, Tân Thủ, Lên Cấp");
            yield return UiAudit.Screen(Dir, "01_event", problems);

            // ---- Nạp Đầu stays locked (payments off)
            welfare.SelectIndex(System.Array.FindIndex(welfare.State.tabs, t => t.id == "firsttopup"));
            Assert.IsTrue(welfare.State.tabs.First(t => t.id == "firsttopup").locked);
            yield return UiAudit.Screen(Dir, "02_first_topup_locked", problems);
            // ---- Phúc Lợi Online: minutes from the server
            welfare.SelectIndex(System.Array.FindIndex(welfare.State.tabs, t => t.id == "online"));
            yield return UiAudit.Screen(Dir, "03_online", problems);

            // ---- Quà Lên Cấp: the level-1 gift is claimable -> claim once through the window, Gold on the HUD
            int lv = System.Array.FindIndex(welfare.State.tabs, t => t.id == "levelup");
            welfare.SelectIndex(lv);
            yield return UiAudit.Screen(Dir, "04_levelup", problems);
            long gold0 = world.Character.gold;
            Assert.IsTrue(welfare.State.tabs[lv].steps[0].claimable, "level 1 gift claimable");
            welfare.ClaimRow(0);
            welfare.ClaimRow(0); // double tap while in flight: ignored
            yield return Until(() => !welfare.Busy);
            Assert.AreEqual(gold0 + 10, world.Character.gold, "server-paid Gold on the HUD, paid once; " + welfare.LastMessage);
            Assert.IsTrue(welfare.State.tabs.First(t => t.id == "levelup").steps[0].claimed);
            Assert.IsFalse(welfare.DotShown, "dot gone: nothing else claimable");
            yield return UiAudit.Screen(Dir, "05_levelup_claimed", problems);
            welfare.Close();

            // ---- Hộp Thư: welcome notice from the server, opening marks it read, dot clears
            var mail = Object.FindAnyObjectByType<MailPanel>();
            Icon("mail").onClick.Invoke();
            Assert.IsTrue(mail.IsOpen);
            yield return Until(() => mail.State != null && mail.State.mails.Length > 0 && mail.SelectedId != null);
            Assert.AreEqual(1, mail.State.mails.Length);
            Assert.AreEqual("notice", mail.State.mails[0].kind);
            yield return Until(() => mail.State.unread == 0);
            Assert.AreEqual(0, mail.State.unread, "read on the server");
            Assert.IsFalse(mail.DotShown);
            yield return UiAudit.Screen(Dir, "06_mail", problems);
            mail.Close();

            // ---- Giftcode: the server refuses an unknown code (production has none)
            var gift = Object.FindAnyObjectByType<GiftcodePanel>();
            Icon("giftcode").onClick.Invoke();
            Assert.IsTrue(gift.IsOpen);
            gift.Input.text = "khongco2026";
            gift.Redeem();
            yield return Until(() => !gift.Busy);
            StringAssert.Contains("không tồn tại", gift.LastMessage);
            Assert.IsNull(gift.Last);
            yield return UiAudit.Screen(Dir, "07_giftcode", problems);
            gift.Close();

            // ---- BXH: me on the level board; guild / boss boards say why they are empty
            var rank = Object.FindAnyObjectByType<RankingPanel>();
            Icon("ranking").onClick.Invoke();
            yield return Until(() => rank.State != null);
            Assert.IsTrue(rank.State.rows.Any(r => r.me && r.name == "Phúc Lợi Kiếm"), "my character on the level board");
            yield return UiAudit.Screen(Dir, "08_rank_level", problems);
            rank.Show("power");
            yield return Until(() => rank.State != null);
            Assert.IsTrue(rank.State.rows.Any(r => r.me && r.value > 0), "Lực Chiến from the server");
            yield return UiAudit.Screen(Dir, "09_rank_power", problems);
            rank.Show("boss");
            yield return Until(() => rank.State != null);
            Assert.IsEmpty(rank.State.rows);
            StringAssert.Contains("Boss", rank.State.emptyReason);
            yield return UiAudit.Screen(Dir, "10_rank_boss_empty", problems);
            rank.Close();

            // ---- art QA only: how an equipped frame would look (client preview, not a grant; reset after)
            var plate = Object.FindAnyObjectByType<PlayerNameplate>();
            Assert.Greater(plate.FrameCount, 30, "owner frames attached");
            yield return Until(() => plate.TitlesLoaded);
            Assert.IsFalse(plate.FrameShown, "nothing owned -> no frame");
            foreach (string f in new[] { "VIP_15", "BossHunter_10", "Special_Starter" })
            {
                plate.ShowTitle(f);
                yield return null;
                Assert.IsTrue(plate.FrameShown);
                yield return Capture($"{Dir}/qa_frame_preview_{f}.png");
            }
            plate.ShowTitle(null);
            Assert.IsFalse(plate.FrameShown);

            UiAudit.WriteReport($"{Dir}/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        private static IEnumerator Capture(string path)
        {
            yield return null;
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
