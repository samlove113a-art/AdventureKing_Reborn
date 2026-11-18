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
    /// Cường Hóa on the real server: buy stones, equip the starter blade, open the panel for it, enhance +0 → +1 (100 %), see the success flash, rays and
    /// banner, the stones and Gold taken by the server, the stats and Lực Chiến rise. The other outcomes (fail / drop / protected / broken) are presented
    /// from server-shaped results (their rolls are covered by the server tests). Game View captures: QA/Enhance/*.png.
    /// </summary>
    public sealed class EnhancePanelTests
    {
        private TestServer server;
        private const string Dir = "QA/Enhance";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            HeroVisualStyle.Override = "2d";
            server = new TestServer(5313);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
            HeroVisualStyle.Override = "3d";
        }

        private static void Shot(string name, int w = 1920, int h = 1080)
        {
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

        [Test]
        public void Sounds_Are_Synthesised_Audible_And_Different()
        {
            var kinds = (EnhanceFx.Kind[])System.Enum.GetValues(typeof(EnhanceFx.Kind));
            foreach (var k in kinds)
            {
                float[] d = EnhanceFx.Synth(k);
                Assert.AreEqual(0.8f, d.Max(Mathf.Abs), 0.01f, k + " normalised");
                Assert.Greater(d.Count(x => Mathf.Abs(x) > 0.04f), d.Length / 12, k + " is not a blip");
                Assert.AreSame(EnhanceFx.Clip(k), EnhanceFx.Clip(k), "built once");
            }
            Assert.Greater(EnhanceFx.Synth(EnhanceFx.Kind.Success).Length, EnhanceFx.Synth(EnhanceFx.Kind.Fail).Length, "the success run rings longer");
            Assert.Greater(EnhanceFx.Synth(EnhanceFx.Kind.Fail).Length, EnhanceFx.Synth(EnhanceFx.Kind.Hammer).Length);
            for (int i = 0; i < kinds.Length; i++)
                for (int j = i + 1; j < kinds.Length; j++)
                {
                    float[] a = EnhanceFx.Synth(kinds[i]), b = EnhanceFx.Synth(kinds[j]);
                    int n = Mathf.Min(a.Length, b.Length);
                    Assert.Greater(Enumerable.Range(0, n).Count(x => Mathf.Abs(a[x] - b[x]) > 0.05f), n / 8, $"{kinds[i]} and {kinds[j]} sound different");
                }
        }

        [UnityTest]
        public IEnumerator Enhance_The_Equipped_Blade_On_The_Real_Server_With_Flash_Banner_And_Rising_Power()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("en" + Random.Range(1000, 9999), "en-pass-2026", HeroClass.Swordsman, "Thợ Rèn Thử");
            var world = AdventureWorldSession.Current;
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            var panel = bag.GetComponent<EnhancePanel>();
            Assert.IsNotNull(panel, "the enhancement window is part of every world scene");
            until = Time.time + 10f;
            while (bag.State == null && Time.time < until) yield return null;
            Assert.IsNotNull(bag.State);
            Assert.IsNotNull(bag.State.enhanceRules); Assert.AreEqual(15, bag.State.enhanceRules.maxLevel); Assert.AreEqual(100, bag.State.enhanceRules.success[0]);

            // stones from the Dược Sư's Cường Hóa tab (server price), then equip the starter blade
            bool bought = false, boughtOk = false; string buyErr = null;
            AdventureServerConnection.Instance.BuyFromShop(world.Character.characterId, "healer", "enhance_stone", 5, "req-" + System.Guid.NewGuid().ToString("N"), (ok, r, e) => { bought = true; boughtOk = ok; buyErr = e; if (ok && r != null) world.Character.gold = r.gold; });
            until = Time.time + 8f; while (!bought && Time.time < until) yield return null;
            Assert.IsTrue(boughtOk, "bought stones: " + buyErr);
            bool refreshed = false; bag.Refresh(() => refreshed = true);
            until = Time.time + 8f; while (!refreshed && Time.time < until) yield return null;
            var blade = bag.State.slots.First(s => s.kind == "equipment" && s.equipSlot == "weapon");
            bag.Select(blade.index); bag.EquipSelected();
            until = Time.time + 8f; while (bag.State.equipped.All(e => e.equipSlot != "weapon") && Time.time < until) yield return null;
            Assert.IsTrue(bag.State.equipped.Any(e => e.equipSlot == "weapon"), "blade equipped");
            int power0 = bag.State.combatPower, atk0 = bag.State.equipped.First(e => e.equipSlot == "weapon").item.attack;
            long gold0 = world.Character.gold; int stones0 = bag.State.slots.Where(s => s.itemId == "enhance_stone").Sum(s => s.qty);
            Assert.AreEqual(5, stones0);

            // open the window for the worn blade
            bag.SelectEquip("weapon"); bag.OpenEnhance();
            Assert.IsTrue(panel.IsOpen);
            StringAssert.Contains("100%", panel.RateText);
            StringAssert.Contains("100", System.Text.RegularExpressions.Regex.Replace(panel.CostText, "<.*?>", ""));     // the Gold of the +0 → +1 attempt
            Assert.IsTrue(panel.EnhanceEnabled, "stones and Gold are enough");
            StringAssert.Contains("an toàn", panel.RiskText);
            yield return null;
            Shot("01_window");

            panel.Attempt();
            Assert.IsTrue(panel.Busy, "hammering");
            until = Time.time + 10f; while ((panel.Busy || panel.LastResult == null) && Time.time < until) yield return null;
            Assert.IsNotNull(panel.LastResult, "the server answered");
            Assert.AreEqual("success", panel.Outcome, "+0 → +1 always succeeds");
            Assert.AreEqual(1, panel.LastResult.levelAfter);
            StringAssert.Contains("THÀNH CÔNG", panel.BannerText);
            yield return new WaitForSeconds(0.12f);
            Assert.Greater(panel.FlashAlpha, 0.3f, "the flash is up"); Assert.IsTrue(panel.BurstVisible, "rays");
            Shot("02_success_flash");
            // what the server charged and what it raised
            Assert.AreEqual(gold0 - 100, world.Character.gold); Assert.AreEqual(panel.LastResult.gold, world.Character.gold);
            Assert.AreEqual(stones0 - 1, bag.State.slots.Where(s => s.itemId == "enhance_stone").Sum(s => s.qty));
            var after = bag.State.equipped.First(e => e.equipSlot == "weapon").item;
            Assert.AreEqual(1, after.enhance); Assert.Greater(after.attack, atk0);
            Assert.Greater(bag.State.combatPower, power0, "Lực Chiến rises with the enhancement");
            var hud = bag.GetComponent<CombatPowerHud>();
            Assert.AreEqual(bag.State.combatPower, hud.Target, "the HUD counts to the new power");
            yield return new WaitForSeconds(0.8f);
            Assert.LessOrEqual(panel.FlashAlpha, 0.05f, "the flash fades");
            StringAssert.Contains("95%", panel.RateText, "the next step is harder (+1 → +2: 95 %)");
            panel.Close();
        }

        [UnityTest]
        public IEnumerator Every_Outcome_Is_Presented_Distinctly()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("eo" + Random.Range(1000, 9999), "eo-pass-2026", HeroClass.Mage, "Thợ Rèn Hai");
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            var panel = bag.GetComponent<EnhancePanel>();
            until = Time.time + 10f; while (bag.State == null && Time.time < until) yield return null;
            bag.Select(bag.State.slots.First(s => s.kind == "equipment").index); bag.OpenEnhance();
            var banners = new System.Collections.Generic.List<string>();
            foreach (var (outcome, expect, level) in new[] { ("fail", "THẤT BẠI", 3), ("drop", "GIẢM XUỐNG +6", 6), ("protected", "BÙA BẢO VỆ", 7), ("broken", "ĐÃ VỠ", 0), ("success", "THÀNH CÔNG", 9) })
            {
                panel.PlayOutcome(new EnhanceResult { outcome = outcome, itemId = "x", levelBefore = 7, levelAfter = level, stonesUsed = 3, goldSpent = 900, charmUsed = outcome == "protected", gold = 1000, inventory = bag.State });
                yield return null;
                Assert.AreEqual(outcome, panel.Outcome);
                StringAssert.Contains(expect, panel.BannerText);
                Assert.Greater(panel.FlashAlpha, 0.2f, outcome + " flashes");
                Assert.AreEqual(outcome == "success", panel.BurstVisible, outcome + ": rays only on success");
                banners.Add(panel.BannerText);
                if (outcome == "broken") Shot("03_broken");
                if (outcome == "protected") Shot("04_protected");
                yield return new WaitForSeconds(0.7f);
            }
            Assert.AreEqual(5, banners.Distinct().Count());
        }
    }
}
