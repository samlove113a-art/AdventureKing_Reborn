using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace AdventureKing.Tests
{
    /// <summary>Name-plate frames (VIP 1..15, Boss Hunter 1..10) live in the bag; the player wears or removes them. Frames are granted straight into the test database (the real server has no grant route). Screenshots: QA/Frames/*.png</summary>
    public sealed class FrameBagTests
    {
        private TestServer server;

        [UnitySetUp]
        public IEnumerator SetUp() { TestRunnerGuard.Protect(); server = new TestServer(5281); yield return server.Start(); Directory.CreateDirectory("QA/Frames"); }

        [UnityTearDown]
        public IEnumerator TearDown() { HeroVisualStyle.Override = null; yield return TestRunnerGuard.CloseGame(); server.Stop(); }

        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes($"QA/Frames/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; Object.Destroy(tex); rt.Release();
        }

        private void Grant(string characterId, params string[] titles)
        {
            string db = Path.Combine(server.DataDir, "test.db");
            string py = @"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe";
            if (!File.Exists(py)) Assert.Ignore("python (Blender) not found for the database fixture");
            string rows = string.Join(";", titles.Select(t => $"c.execute(\"INSERT OR IGNORE INTO titles_owned(character_id,title_id,source,granted_ms) VALUES(?,?,?,?)\",('{characterId}','{t}','test',1))"));
            var psi = new ProcessStartInfo(py, $"-c \"import sqlite3;c=sqlite3.connect(r'{db}',timeout=20);{rows.Replace("\"", "\\\"")};c.commit()\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            p.WaitForExit(20000);
            Assert.AreEqual(0, p.ExitCode, p.StandardError.ReadToEnd());
        }

        [UnityTest]
        public IEnumerator Frames_Sit_In_The_Bag_And_Can_Be_Worn_Or_Taken_Off()
        {
            HeroVisualStyle.Override = "2d";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("frm" + Random.Range(1000, 9999), "frm-pass-2026", HeroClass.Mage, "Khung Tên");
            var world = AdventureWorldSession.Current;
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            var plate = Object.FindAnyObjectByType<PlayerNameplate>();
            bag.Open();
            until = Time.time + 8f;
            while (bag.State == null && Time.time < until) yield return null;
            Assert.IsNotNull(bag.State);
            Assert.IsFalse(bag.State.slots.Any(s => s.kind == "frame"), "no frame owned yet: nothing in the bag");

            string[] vip = Enumerable.Range(1, 15).Select(i => $"VIP_{i:00}").ToArray();
            string[] boss = Enumerable.Range(1, 10).Select(i => $"BossHunter_{i:00}").ToArray();
            Grant(world.Character.characterId, vip.Concat(boss).ToArray());
            bag.Refresh();
            yield return new WaitForSeconds(1f);
            var frames = bag.State.slots.Where(s => s.kind == "frame").ToList();
            Assert.AreEqual(25, frames.Count, "VIP 1..15 + Boss Hunter 1..10");
            Assert.AreEqual("VIP_01", frames[0].itemId);
            Assert.AreEqual("BossHunter_10", frames[24].itemId);
            Assert.IsTrue(frames.All(f => f.index >= bag.State.capacity), "frames take no real cell");
            Assert.IsNull(plate.EquippedTitle);
            yield return Shot("01_bag_with_frames");

            // wear VIP 15
            BagSlotState v15 = frames.First(f => f.itemId == "VIP_15");
            bag.Select(v15.index);
            yield return Shot("02_vip15_selected");
            bag.EquipSelected();
            until = Time.time + 6f;
            while (plate.EquippedTitle != "VIP_15" && Time.time < until) yield return null;
            Assert.AreEqual("VIP_15", plate.EquippedTitle, "worn");
            Assert.IsTrue(plate.FrameShown, "the frame is shown above the name");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(bag.State.slots.Single(s => s.itemId == "VIP_15").worn, "bag marks it worn");
            bag.Close();
            yield return new WaitForSeconds(0.4f);
            yield return Shot("03_vip15_worn_in_world");

            // take it off: it stays in the bag
            bag.Open();
            yield return new WaitForSeconds(0.8f);
            bag.Select(bag.State.slots.First(s => s.itemId == "VIP_15").index);
            bag.EquipSelected();
            until = Time.time + 6f;
            while (plate.EquippedTitle != null && Time.time < until) yield return null;
            Assert.IsNull(plate.EquippedTitle, "taken off");
            Assert.IsFalse(plate.FrameShown);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(bag.State.slots.Any(s => s.itemId == "VIP_15" && !s.worn), "still in the bag");
            // a Boss Hunter frame can be worn too
            bag.Select(bag.State.slots.First(s => s.itemId == "BossHunter_10").index);
            bag.EquipSelected();
            until = Time.time + 6f;
            while (plate.EquippedTitle != "BossHunter_10" && Time.time < until) yield return null;
            Assert.AreEqual("BossHunter_10", plate.EquippedTitle);
            bag.Close();
            yield return new WaitForSeconds(0.4f);
            yield return Shot("04_boss_hunter10_worn_in_world");
        }
    }
}
