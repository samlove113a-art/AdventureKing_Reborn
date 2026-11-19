using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>The Blacksmith's forge with the real server: walk up, talk, forge window, +0 -> +1 with the Gold and stone fee charged once, protection charm, no money = no strike.</summary>
    public sealed class BlacksmithTests
    {
        private TestServer server;
        private string fixture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            // fixture: the real items.json plus 10 Enhancement Stones and 2 Protection Charms in the starter kit (so the forge can be exercised)
            string src = File.ReadAllText(Path.Combine(TestServer.ServerConfigDir, "items.json"));
            Assert.IsTrue(src.Contains("\"starterKit\": ["), "items.json starter kit");
            fixture = Path.Combine(Path.GetTempPath(), "ak_items_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(fixture, src.Replace("\"starterKit\": [", "\"starterKit\": [ { \"itemId\": \"enhance_stone\", \"qty\": 10 }, { \"itemId\": \"protection_charm\", \"qty\": 2 },"));
            server = new TestServer(5209, starterGold: 100000, extraArgs: $"--AdventureKing:ItemConfigPath=\"{fixture}\"");
            yield return server.Start();
            Directory.CreateDirectory("QA/Blacksmith");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return Resources.UnloadUnusedAssets();
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
            try { File.Delete(fixture); } catch (IOException) { }
        }

        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Blacksmith/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        private static IEnumerator WaitIdle(BlacksmithUI forge, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            while (forge.Busy && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(forge.Busy, "forge request timed out; message: " + forge.LastMessage);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Smith_Forge_Charges_Gold_And_Stone_Once_And_Protection_Charm_Works()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("smith.tester@test.vn", "smith-pass-2026", HeroClass.Swordsman, "Khách Thợ Rèn");
            var world = AdventureWorldSession.Current;
            var motor = world.Player;
            var input = motor.GetComponent<PlayerInput2D>();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;
            Assert.IsTrue(input.enabled);

            // wear the novice blade (the forge enhances WORN gear)
            bool done = false; InventoryState inv = null;
            AdventureServerConnection.Instance.GetInventory(world.Character.characterId, (ok, i, e) => { inv = i; done = true; });
            while (!done) yield return null;
            int blade = inv.slots.First(s => s.itemId == "gear_novice_blade").index;
            done = false;
            AdventureServerConnection.Instance.EquipItem(world.Character.characterId, blade, (ok, i, e) => { inv = i; done = true; });
            while (!done) yield return null;
            Assert.AreEqual("gear_novice_blade", inv.equipped.First(x => x.equipSlot == "weapon").item.itemId);

            // ---- walk to the Thợ Rèn: prompt, talk, forge window
            var smith = Object.FindAnyObjectByType<BlacksmithNpc>();
            Assert.IsNotNull(smith, "Blacksmith NPC wired in town");
            var forge = Object.FindAnyObjectByType<BlacksmithUI>(FindObjectsInactive.Include);
            Assert.IsNotNull(forge);
            float target = smith.transform.position.x + 1.6f;
            until = Time.time + 40f;
            while (Mathf.Abs(motor.transform.position.x - target) > 0.3f)
            {
                bool right = motor.transform.position.x < target;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, right);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, !right);
                yield return null;
                Assert.Less(Time.time, until, "walking to the smith");
            }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(smith.PlayerNear, "talk prompt next to the smith");
            Assert.IsTrue(forge.TalkButton.gameObject.activeInHierarchy, "touch 'Nói chuyện' button");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, true);
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, false);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(forge.IsOpen, "forge window opened");
            Assert.IsFalse(input.enabled, "hero stands still at the forge");
            yield return WaitIdle(forge);
            Assert.IsNotNull(forge.Inv);

            // ---- what the window shows comes from the server
            BagSlotState blade0 = forge.Current;
            Assert.AreEqual("gear_novice_blade", blade0.itemId);
            Assert.AreEqual(0, blade0.enhanceLevel);
            Assert.AreEqual(InventoryChance(0), blade0.enhanceChance);
            Assert.AreEqual(50, blade0.enhanceGold, "Gold fee of +0 -> +1 (server config)");
            Assert.AreEqual(10, forge.StoneCount);
            Assert.AreEqual(2, forge.CharmCount);
            Assert.IsTrue(forge.CanStrike);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("01_forge_open");

            // ---- strike: +0 -> +1 is a sure success; the fee is charged ONCE even if the button is hammered
            long gold0 = world.Character.gold;
            forge.Strike();
            forge.Strike();
            forge.Strike();
            yield return WaitIdle(forge);
            Assert.AreEqual(1, forge.Strikes, "hammering the button sends one request");
            Assert.IsTrue(forge.LastSuccess, "+0 -> +1 always succeeds");
            Assert.AreEqual(1, forge.Current.enhanceLevel);
            Assert.AreEqual(gold0 - 50, world.Character.gold, "Gold fee charged once");
            Assert.AreEqual(9, forge.StoneCount, "one stone used");
            StringAssert.Contains("Thành công", forge.LastMessage);
            yield return new WaitForSeconds(0.25f);
            yield return Shot("02_forge_success");
            yield return new WaitForSeconds(1f);

            // ---- protection charm: toggled on, one charm is used by the next strike
            forge.ToggleProtection();
            Assert.IsTrue(forge.UseProtection);
            Assert.IsTrue(forge.CanStrike);
            forge.Strike();
            yield return WaitIdle(forge);
            Assert.AreEqual(1, forge.CharmCount, "one Protection Charm consumed");
            Assert.AreEqual(2, forge.Strikes);
            Assert.GreaterOrEqual(forge.Current.enhanceLevel, 1, "never drops below the level it had (protected)");
            yield return new WaitForSeconds(0.3f);
            yield return Shot("03_forge_protected");
            forge.ToggleProtection();

            // ---- no Gold = no strike (the button is disabled; the server would refuse too)
            long keep = world.Character.gold;
            world.Character.gold = 5;
            forge.SelectSlot("weapon");
            Assert.IsFalse(forge.CanStrike, "not enough Gold: strike disabled");
            Assert.IsFalse(forge.StrikeButton.interactable);
            world.Character.gold = keep;
            forge.SelectSlot("armor");
            Assert.IsNull(forge.Current, "nothing worn in the armor slot yet");
            Assert.IsFalse(forge.CanStrike);
            forge.SelectSlot("weapon");

            // ---- close: control returns, no 3D anywhere in the window
            Assert.AreEqual(0, forge.GetComponentsInChildren<MeshRenderer>(true).Length);
            forge.Close();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(input.enabled, "control returns after the forge");
        }

        private static int InventoryChance(int level) => new[] { 100, 95, 90, 85, 80, 70, 65, 55, 45, 40, 35, 30, 25, 20, 15 }[level];
    }
}
