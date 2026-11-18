using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>In the real world scene with the real server: the Lực Chiến HUD exists and shows the server's number; a red drop lights its pillar next to the hero.
    /// Game View captures: QA/LootPower/*.png.</summary>
    public sealed class LootPowerWorldTests
    {
        private TestServer server;
        private const string Dir = "QA/LootPower";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            HeroVisualStyle.Override = "2d";
            server = new TestServer(5311);
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

        [UnityTest]
        public IEnumerator Hud_Shows_The_Servers_Combat_Power_And_A_Red_Drop_Lights_Its_Pillar()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("lp" + Random.Range(1000, 9999), "lp-pass-2026", HeroClass.Swordsman, "Thử Lực Chiến");
            var world = AdventureWorldSession.Current;
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            var panel = Object.FindAnyObjectByType<InventoryPanel>();
            Assert.IsNotNull(panel);
            until = Time.time + 10f;
            while (panel.State == null && Time.time < until) yield return null;
            Assert.IsNotNull(panel.State, "bag loaded from the server");
            Assert.Greater(panel.State.combatPower, 0, "the server sends Lực Chiến");
            var hud = panel.GetComponent<CombatPowerHud>();
            Assert.IsNotNull(hud, "the HUD is created with the bag panel in every world scene");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(panel.State.combatPower, hud.Target);
            Assert.AreEqual(panel.State.combatPower, hud.Shown, "first value appears at once");
            var corners = new Vector3[4]; hud.Card.GetWorldCorners(corners);
            Assert.Greater(corners[1].y, Screen.height * 0.9f, "the card is at the top of the screen"); Assert.AreEqual(Screen.width * 0.5f, (corners[0].x + corners[2].x) / 2f, 2f, "centred");
            // a red drop beside the hero
            Vector3 hero = world.Player.transform.position;
            var go = new GameObject("RedDrop");
            var drop = go.AddComponent<GoldDropView>();
            drop.Bind(new FarmDropState { id = "red1", x = hero.x + 2.2f, y = 0f, expiresInMs = 15000, itemId = "gear_king_crown_charm", itemName = "Vương Miện Nấm Quỷ", itemKind = "equipment", itemRarity = "red", qty = 1 }, new Vector2(hero.x + 3.5f, 1f));
            yield return new WaitForSeconds(1.9f);
            Assert.IsTrue(drop.Beam && drop.Beam.Visible, "the pillar is up");
            Assert.AreEqual(4, drop.Beam.Tier);
            Shot("red_drop_and_power_hud");
            Object.Destroy(go);
        }
    }
}
