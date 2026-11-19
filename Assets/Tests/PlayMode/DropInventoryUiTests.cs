using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Global UI rules (one scaler for every screen canvas, 9-slice frames, HUD clusters pinned to the screen edges) and the
    /// item-drop / bag loop on the real server: junk + gear drops, pickup, 15 s life, bag expansion with Gold, quick sell of junk.
    /// </summary>
    public sealed class DropInventoryUiTests
    {
        private static readonly System.Text.RegularExpressions.Regex FrameName = new System.Text.RegularExpressions.Regex("^(ui_panel|ui_pill|ui_gilded|ui_enamel|btn_|hall_|shop_|item_cell|hud_plate|minimap_frame|popup_|settings_|parchment|ribbon|field|chip|auth_card|frame_panel|frame_bar)");
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5293);
            yield return server.Start();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private Vector2 Pos => motor.transform.position;

        private IEnumerator WalkTo(float x, float timeout = 30f)
        {
            float end = Time.time + timeout;
            while (Mathf.Abs(Pos.x - x) > 0.25f)
            {
                bool right = Pos.x < x;
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, right);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, !right);
                yield return null;
                Assert.Less(Time.time, end, $"timeout walking to x={x}, at {Pos}");
            }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            float settle = Time.time + 2f;
            while (Mathf.Abs(motor.Velocity.x) > 0.05f && Time.time < settle) yield return null;
        }

        private void Bind() { motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>(); }

        private IEnumerator TakePortal(float x, string scene)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(x, 8f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false); yield return null;
            var world = AdventureWorldSession.Current;
            float until = Time.time + 2f;
            while (!world.ConfirmOpen && !world.Traveling && Time.time < until) yield return null;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(scene);
            yield return new WaitForSeconds(1.2f);
            Bind();
        }

        // ------------------------------------------------------------------ UI standard
        [UnityTest]
        public IEnumerator Every_Canvas_Uses_The_Standard_Scaler_Frames_Are_9_Sliced_And_HUD_Clusters_Hug_The_Screen_Edges()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ui" + Random.Range(1000, 9999), "ui-pass-2026", HeroClass.Mage, "Lữ Khách UI");
            Bind();
            var bag = Object.FindAnyObjectByType<InventoryPanel>(); bag.Open();
            yield return new WaitForSeconds(1f);

            var bad = new List<string>();
            foreach (CanvasScaler s in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var c = s.GetComponent<Canvas>();
                if (c && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && !UiStandard.IsStandard(s)) bad.Add(UiAudit.PathOf(s.transform));
            }
            Assert.IsEmpty(bad, "CanvasScaler must be Scale With Screen Size, 1920x1080, match 0.5: " + string.Join(", ", bad));

            // every frame / cell / plate sprite carries a border, so it must be drawn Sliced (never stretched); circles, glows and fills are meant to be Simple / Filled
            var stretched = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(i => i.sprite && i.sprite.border != Vector4.zero && i.type == Image.Type.Simple && FrameName.IsMatch(i.sprite.name)).Select(i => UiAudit.PathOf(i.transform) + " [" + i.sprite.name + "]").ToList();
            Assert.IsEmpty(stretched, "bordered sprites not set to Sliced:\n" + string.Join("\n", stretched));
            // and frame-like names always have a border
            var noBorder = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(i => i.sprite && i.type == Image.Type.Sliced && i.sprite.border == Vector4.zero).Select(i => UiAudit.PathOf(i.transform) + " [" + i.sprite.name + "]").ToList();
            Assert.IsEmpty(noBorder, "Sliced image whose sprite has no 9-slice border:\n" + string.Join("\n", noBorder));

            // clusters pinned to the edges they belong to (anchors at the corner / edge, not floating in the middle)
            RectTransform Find(string n) => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.name == n);
            void Edge(string name, System.Func<RectTransform, bool> ok, string why)
            {
                RectTransform r = Find(name);
                if (r == null) return; // a cluster that does not exist in this scene is not a layout bug
                Assert.IsTrue(ok(r), $"{name}: {why} (anchors {r.anchorMin}..{r.anchorMax})");
            }
            Edge("TouchJoystick", r => r.anchorMin == Vector2.zero && r.anchorMax == Vector2.zero, "joystick pinned to the bottom-left corner");
            Edge("TouchJump", r => r.anchorMin.x == 1f && r.anchorMin.y == 0f, "jump pinned to the bottom-right corner");
            Edge("TouchInteract", r => r.anchorMin.x == 1f && r.anchorMin.y == 0f, "interact pinned to the bottom-right corner");
            Edge("QuestTracker", r => r.anchorMin == new Vector2(0f, 1f), "quest tracker pinned to the top-left");
            foreach (var btn in Object.FindObjectsByType<HeroActionBar>(FindObjectsInactive.Include, FindObjectsSortMode.None).SelectMany(b => b.GetComponentsInChildren<Button>(true)))
            {
                var r = (RectTransform)btn.transform;
                Assert.IsTrue(r.anchorMin == r.anchorMax, "skill/action button has a single anchor point: " + UiAudit.PathOf(r));
            }
        }

        // ------------------------------------------------------------------ drops + bag
        [UnityTest]
        public IEnumerator Meadow_Kill_Drops_Junk_Pickup_15s_Life_Bag_Expansion_And_Quick_Sell()
        {
            Directory.CreateDirectory("QA/Drops");
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("dr" + Random.Range(1000, 9999), "drop-pass-2026", HeroClass.Swordsman, "Nhặt Đồ Rác");
            Bind();
            var world = AdventureWorldSession.Current;
            yield return TakePortal(TownLayout.Hub.Portals[0].X, TownLayout.Meadow.Scene);
            FarmField farm = FarmField.Current;
            float until = Time.time + 6f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;

            // kill slimes until a junk pile lies on the ground (60 % per kill)
            GoldDropView junk = null;
            string[] spawns = { "meadow_s1", "meadow_s2" };
            for (int k = 0; k < 14 && !junk; k++)
            {
                FarmMonsterView slime = farm.Monsters[spawns[k % 2]];
                float until2 = Time.time + 60f;
                while (!slime.Alive && Time.time < until2) { yield return null; } // respawn takes 60 s: use the other slime first
                if (!slime.Alive) continue;
                yield return WalkTo(slime.Home.x - 1.6f);
                until2 = Time.time + 20f;
                while (slime.Alive && Time.time < until2)
                {
                    input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
                    yield return new WaitForSeconds(0.95f);
                    var hp = motor.GetComponent<Health2D>();
                    if (hp.CurrentHealth < hp.MaxHealth * 0.4f) { var bagPanel = Object.FindAnyObjectByType<InventoryPanel>(); bagPanel.UsePotion(true); yield return new WaitForSeconds(1.2f); }
                }
                until2 = Time.time + 4f; // the item pile arrives with the next poll
                while (Time.time < until2 && !junk) { junk = farm.Drops.Values.FirstOrDefault(d => d && d.IsItem); yield return null; }
            }
            Assert.IsNotNull(junk, "a junk pile dropped within 14 kills; last error: " + farm.LastError);
            Assert.IsTrue(farm.Drops.Values.Any(d => d && !d.IsItem), "the Gold pile is still there");
            string junkName = junk.ItemName; string junkId = junk.DropId;
            Debug.Log("[DROPS] item pile: " + junkName);
            // 2D loot: each drop shows ITS OWN pixel sprite (the item's icon / a coin), never a shared box, and nothing 3D
            Assert.IsNotNull(junk.ShownSprite, "junk drop shows a sprite");
            Assert.AreSame(ItemIconBook.WorldSprite(junk.ItemId, "item_junk"), junk.ShownSprite, "the junk drop shows the worldSprite / icon of " + junk.ItemId + " (shared item_junk until the owner assigns one)");
            Assert.AreEqual(0, junk.GetComponentsInChildren<MeshRenderer>(true).Length, "no 3D mesh in a drop");
            GoldDropView coinPile = farm.Drops.Values.First(d => d && !d.IsItem);
            Assert.IsNotNull(coinPile.ShownSprite, "gold drop shows coins");
            
            Assert.AreNotSame(coinPile.ShownSprite, junk.ShownSprite, "item and coin drops look different");

            // ---- pick the junk up: gone from the ground at once, in the bag
            yield return new WaitForSeconds(1.2f);
            yield return WalkTo(junk.Ground.x + 0.6f);
            var bag = Object.FindAnyObjectByType<InventoryPanel>();
            bag.Refresh(); yield return new WaitForSeconds(0.6f);
            int before = bag.State.slots.Where(s => s.kind == "junk").Sum(s => s.qty);
            Assert.IsTrue(farm.TryPickup() || farm.TryPickup(), "pickup started");
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(farm.Drops.ContainsKey(junkId), "picked pile left the ground list");
            bag.Refresh(); yield return new WaitForSeconds(0.6f);
            Assert.Greater(bag.State.slots.Where(s => s.kind == "junk").Sum(s => s.qty), before, "junk is in the bag");
            Assert.AreEqual("junk", bag.State.slots.First(s => s.kind == "junk").kind);

            // ---- a pile nobody picks up disappears by itself after 15 s (the Gold pile is the one left)
            var gold = farm.Drops.Values.FirstOrDefault(d => d && !d.IsItem);
            if (gold)
            {
                string goldId = gold.DropId;
                yield return WalkTo(gold.Ground.x + 14f);
                yield return new WaitForSeconds(16.5f);
                Assert.IsFalse(farm.Drops.ContainsKey(goldId) && farm.Drops[goldId], "an unpicked drop is destroyed after 15 s");
            }

            // ---- bag expansion with Gold
            bag.Open(); yield return new WaitForSeconds(0.8f);
            int cap = bag.State.capacity; long goldBefore = world.Character.gold;
            Assert.AreEqual(40, cap);
            Assert.Greater(bag.State.nextExpandGold, 0);
            bag.Expand();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(41, bag.State.capacity, "one more cell");
            Assert.Less(world.Character.gold, goldBefore, "paid with Gold");
            bag.Close();

            // ---- back to the town: quick sell
            yield return TakePortal(TownLayout.Meadow.Portals[0].X, TownLayout.Hub.Scene);
            long g0 = world.Character.gold;
            int junkCount = bag.State.slots.Where(s => s.kind == "junk").Sum(s => s.qty);
            Assert.Greater(junkCount, 0);
            SellJunkResult res = null; bool done = false;
            AdventureServerConnection.Instance.SellJunk(world.Character.characterId, (ok, r, err) => { done = true; res = ok ? r : null; if (!ok) Debug.Log("[DROPS] sell err " + err); });
            until = Time.time + 8f; while (!done && Time.time < until) yield return null;
            Assert.IsNotNull(res, "quick sell answered");
            Assert.AreEqual(junkCount, res.itemsSold);
            Assert.AreEqual(g0 + res.totalGold, res.gold);
            Assert.AreEqual(0, res.inventory.slots.Count(s => s.kind == "junk"));
        }
    }
}
