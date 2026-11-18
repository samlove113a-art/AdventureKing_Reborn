using System.Collections;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Loot drops by rarity (pillar of light, bigger bounce, chime only for purple / orange / red) and the Lực Chiến HUD that counts to the
    /// server's number. Pure client behaviour on the real components, no server needed (the numbers come from DTOs, built here).
    /// </summary>
    public sealed class LootPowerHudTests
    {
        private static GoldDropView Drop(string rarity, string id = "d1")
        {
            var go = new GameObject("Drop_" + rarity);
            var view = go.AddComponent<GoldDropView>();
            view.Bind(new FarmDropState { id = id, x = 5f, y = 0f, expiresInMs = 15000, itemId = "gear_x", itemName = "Đồ", itemKind = "equipment", itemRarity = rarity, qty = 1 }, new Vector2(3f, 0.5f));
            return view;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var b in Object.FindObjectsByType<LootBeam>(FindObjectsInactive.Include)) Object.Destroy(b.gameObject);
            foreach (var d in Object.FindObjectsByType<GoldDropView>(FindObjectsInactive.Include)) Object.Destroy(d.gameObject);
            foreach (var h in Object.FindObjectsByType<CombatPowerHud>(FindObjectsInactive.Include)) Object.Destroy(h.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Where(c => c.name == "CombatPowerCanvas")) Object.Destroy(c.gameObject);
        }

        [Test]
        public void Rarity_Tiers_And_Colours_Follow_White_Blue_Purple_Orange_Red()
        {
            Assert.AreEqual(new[] { 0, 1, 2, 3, 4 }, new[] { "white", "blue", "purple", "orange", "red" }.Select(LootRarity.Tier).ToArray());
            Assert.AreEqual(0, LootRarity.Tier(""), "no rarity = white");
            Assert.AreEqual(0, LootRarity.Tier(null));
            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(LootRarity.ColorOf).Distinct().Count(), "one flat colour per tier");
            Color r = LootRarity.ColorOf(4), o = LootRarity.ColorOf(3);
            Assert.Greater(r.r, 0.8f); Assert.Less(r.g, 0.3f);                 // red
            Assert.Greater(o.r, 0.9f); Assert.Greater(o.g, 0.5f); Assert.Less(o.b, 0.3f);   // orange
            Assert.Greater(LootRarity.ColorOf(1).b, LootRarity.ColorOf(1).r);   // blue
        }

        [Test]
        public void Chime_Is_Synthesised_Longer_And_Richer_For_Higher_Tiers()
        {
            float[] p = LootRarity.Synth(2), o = LootRarity.Synth(3), r = LootRarity.Synth(4);
            Assert.Less(p.Length, o.Length); Assert.Less(o.Length, r.Length);
            foreach (float[] data in new[] { p, o, r })
            {
                Assert.AreEqual(0.8f, data.Max(Mathf.Abs), 0.01f, "normalised, audible and not clipping");
                Assert.Greater(data.Count(x => Mathf.Abs(x) > 0.05f), data.Length / 6, "rings for a while");
            }
            // each tier sounds different (not the same run at another length)
            int n = Mathf.Min(p.Length, o.Length);
            Assert.Greater(Enumerable.Range(0, n).Count(i => Mathf.Abs(p[i] - o[i]) > 0.05f), n / 10);
            var clip = LootRarity.Chime(3);
            Assert.AreEqual(o.Length, clip.samples); Assert.AreSame(clip, LootRarity.Chime(3), "built once");
        }

        [UnityTest]
        public IEnumerator High_Rarity_Drops_Get_A_Pillar_Of_Light_And_A_Bigger_Bounce_Lower_Ones_Do_Not()
        {
            GoldDropView white = Drop("white", "w"), blue = Drop("blue", "b"), purple = Drop("purple", "p"), orange = Drop("orange", "o"), red = Drop("red", "r");
            Assert.IsNull(white.Beam); Assert.IsNull(blue.Beam);
            Assert.IsNotNull(purple.Beam); Assert.IsNotNull(orange.Beam); Assert.IsNotNull(red.Beam);
            Assert.AreEqual(new[] { 2, 3, 4 }, new[] { purple.Beam.Tier, orange.Beam.Tier, red.Beam.Tier });
            Assert.AreEqual(3, Object.FindObjectsByType<LootBeam>(FindObjectsInactive.Exclude).Length, "only the three high drops have a beam");

            float whiteMax = 0f, purpleMax = 0f, end = Time.time + 1.9f;
            while (Time.time < end)
            {
                whiteMax = Mathf.Max(whiteMax, white.transform.position.y); purpleMax = Mathf.Max(purpleMax, purple.transform.position.y);
                yield return null;
            }
            Assert.Greater(purpleMax, whiteMax + 0.5f, $"high rarity arcs higher ({purpleMax:0.00} vs {whiteMax:0.00})");
            Assert.IsTrue(purple.Landed && orange.Landed && red.Landed);
            // the pillar stands on the ground where the item rests, is visible, taller than the screen's play area, and in the tier colour
            foreach (var d in new[] { purple, orange, red })
            {
                var beam = d.Beam;
                Assert.IsTrue(beam.Visible, d.name + " pillar visible");
                Assert.AreEqual(d.Ground.x + 0.6f, beam.transform.position.x, 0.05f);
                Assert.AreEqual(d.Ground.y, beam.transform.position.y, 0.15f, "pillar starts at the ground");
                var body = beam.transform.Find("Pillar").GetComponent<SpriteRenderer>();
                Assert.Greater(body.bounds.size.y, 6f, "a tall pillar");
                Color want = LootRarity.ColorOf(beam.Tier);
                Color px = body.sprite.texture.GetPixel(3, 20);          // flat centre band of the baked pillar
                Assert.AreEqual(want.r, px.r, 0.02f); Assert.AreEqual(want.g, px.g, 0.02f); Assert.AreEqual(want.b, px.b, 0.02f);
                var mpb = new MaterialPropertyBlock(); body.GetPropertyBlock(mpb);
                Assert.Greater(mpb.GetColor("_Color").a, 0.3f, "visible: alpha comes from the pulse");
            }
            Assert.IsNotNull(red.Beam.transform.Find("Ring"), "red also has the ground ring");
            Assert.IsNotNull(orange.Beam.transform.Find("Core"), "orange and red have the bright core");
            Assert.IsNull(purple.Beam.transform.Find("Core"));

            // picked up: the pillar fades out with the item
            purple.Collect(null);
            float gone = Time.time + 1.5f;
            while (purple.Beam && Time.time < gone) yield return null;
            Assert.IsTrue(!purple.Beam, "pillar removed after the pickup");
            Assert.IsTrue(orange.Beam, "other pillars stay");
        }

        [UnityTest]
        public IEnumerator Combat_Power_Counts_Up_To_The_Server_Number_With_A_Flash_And_The_Delta()
        {
            var hud = new GameObject("hud").AddComponent<CombatPowerHud>();
            hud.SetPower(1000);
            Assert.AreEqual(1000, hud.Shown, "the first value appears at once");
            Assert.IsFalse(hud.Counting);
            hud.SetPower(1650);                                              // equipped something: +650
            Assert.IsTrue(hud.Counting); Assert.AreEqual(1650, hud.Target);
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(hud.Shown, 1000); Assert.Less(hud.Shown, 1650, "rolling, not jumping");
            var texts = hud.Canvas.GetComponentsInChildren<Text>(true);
            Text delta = texts.First(t => t.name == "Delta"), number = texts.First(t => t.name == "Number");
            StringAssert.StartsWith("+", delta.text); StringAssert.Contains("650", delta.text.Replace(".", "").Replace(",", ""));
            Assert.Greater(number.color.g, number.color.r - 0.01f, "green flash on a gain");
            yield return new WaitForSeconds(1.0f);
            Assert.IsFalse(hud.Counting); Assert.AreEqual(1650, hud.Shown);
            Assert.AreEqual("1650", new string(hud.Text.Where(char.IsDigit).ToArray()), "the numerals show the final value");
            // a loss counts down with a red flash
            hud.SetPower(1400);
            yield return new WaitForSeconds(0.2f);
            Assert.Less(hud.Shown, 1650); Assert.Greater(hud.Shown, 1400);
            StringAssert.StartsWith("−", texts.First(t => t.name == "Delta").text);
            Assert.Greater(number.color.r, number.color.g, "red flash on a loss");
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(1400, hud.Shown);
            // same value again: nothing moves
            hud.SetPower(1400); Assert.IsFalse(hud.Counting);
            // sits at the top centre of a 1920x1080 reference canvas, clear of the HP panel (left) and the minimap (right)
            var card = hud.Card;
            Assert.AreEqual(new Vector2(0.5f, 1f), card.anchorMin);
            Assert.Less(card.sizeDelta.x, 400f);
            var scaler = hud.Canvas.GetComponent<CanvasScaler>();
            Assert.IsNull(hud.Canvas.transform.parent, "its own overlay canvas at the scene root");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, hud.Canvas.renderMode);
            Assert.IsTrue(UiStandard.IsStandard(scaler));
        }
    }
}
