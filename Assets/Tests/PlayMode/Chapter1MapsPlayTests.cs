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
    /// Chương 1 · Map 3-10 through the real server and the real scenes: every portal of the chain (hub → … → Map 10), each map's own
    /// scene / backdrop / multi-tier decks with real colliders / ladders and ropes, 2D monsters of the map, Mini Boss / Elite / Boss
    /// presentation (tier, big bar above the sprite, enrage tint, slam warning) and no missing scripts. Game View captures:
    /// QA/Chapter1Maps/*.png (needs a window). Boss FIGHT rules (slam, enrage, rewards, chapter progress) are server tests.
    /// </summary>
    public sealed class Chapter1MapsPlayTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private const string Dir = "QA/Chapter1Maps";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            HeroVisualStyle.Override = "2d";
            server = new TestServer(5297);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
            HeroVisualStyle.Override = "3d";
        }

        private Vector2 Pos => motor.transform.position;
        private void Bind() { motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>(); }
        private IEnumerator Tap(PlayerInput2D.TouchAction a) { input.SetTouchHeld(a, true); yield return null; input.SetTouchHeld(a, false); yield return null; }

        private IEnumerator WalkTo(float x, float timeout = 25f)
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

        private static IEnumerator Shot(string name, int w = 1920, int h = 1080)
        {
            yield return null;   // (WaitForEndOfFrame never returns in batch mode)
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

        private IEnumerator TakePortal(float x, string expectScene)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkTo(x, 8f);
            yield return Tap(PlayerInput2D.TouchAction.Up);
            var world = AdventureWorldSession.Current;
            float until = Time.time + 2f;
            while (!world.ConfirmOpen && !world.Traveling && Time.time < until) yield return null;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(expectScene);
            yield return new WaitForSeconds(1.2f);
            Bind();
        }

        private static string BackdropTexture()
        {
            var b = GameObject.Find("Backdrop_Painting");
            var r = b ? b.GetComponent<MeshRenderer>() : null;
            var m = r ? r.sharedMaterial : null;
            return m && m.mainTexture ? m.mainTexture.name : "";
        }

        private static float TopAt(float x, float fromY)
        {
            var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, 30f);
            return hit.collider ? hit.point.y : float.NaN;
        }

        private static (string mapId, string scene, string name, TownLayout.Deck[] decks, TownLayout.Climb[] climbs, TownLayout.PortalSpot[] portals)[] Chain()
        {
            var list = new System.Collections.Generic.List<(string, string, string, TownLayout.Deck[], TownLayout.Climb[], TownLayout.PortalSpot[])>
            {
                (TownLayout.Ch1GlowForest.MapId, TownLayout.Ch1GlowForest.Scene, TownLayout.Ch1GlowForest.DisplayName, TownLayout.Ch1GlowForest.Decks, TownLayout.Ch1GlowForest.Climbs, TownLayout.Ch1GlowForest.Portals),
            };
            foreach (var m in Ch1MapData.All) list.Add((m.MapId, m.Scene, m.DisplayName, m.Decks, m.Climbs, m.Portals));
            return list.ToArray();
        }

        [UnityTest]
        public IEnumerator Every_Portal_From_The_Hub_To_Map_10_Works_And_Every_Map_Has_Its_Own_Terrain_Monsters_And_Bosses()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ch1x" + Random.Range(1000, 9999), "ch1-pass-2026", HeroClass.Swordsman, "Lữ Khách Một");
            Bind();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene);
            yield return TakePortal(TownLayout.Meadow.Portals.First(p => p.Id == "meadow_east_ch1").X, TownLayout.Ch1Village.Scene);
            yield return TakePortal(TownLayout.Ch1Village.Portals.First(p => p.Id == "ch1_village_east_m02").X, TownLayout.Ch1GreenMeadow.Scene);
            yield return TakePortal(TownLayout.Ch1GreenMeadow.Portals.First(p => p.Id == "ch1_m02_east_m03").X, TownLayout.Ch1GlowForest.Scene);

            var chain = Chain();
            var backdrops = new System.Collections.Generic.HashSet<string>();
            var world = AdventureWorldSession.Current;
            for (int i = 0; i < chain.Length; i++)
            {
                var m = chain[i];
                world = AdventureWorldSession.Current;
                Assert.AreEqual(m.mapId, world.MapId, "server map");
                Assert.AreEqual(m.mapId, world.Character.lastMapId);
                int index = i + 3;
                string tex = BackdropTexture();
                StringAssert.Contains($"c1_m{index:00}", tex, $"Map {index} has its own far landscape");
                Assert.IsTrue(backdrops.Add(tex), "backdrops are all different");
                var missing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.GetComponents<Component>().Any(c => c == null)).Select(t => UiAudit.PathOf(t)).ToList();
                Assert.IsEmpty(missing, $"Map {index} missing scripts: " + string.Join(", ", missing));
                Assert.IsFalse(float.IsNaN(TopAt(Pos.x, 3f)), $"Map {index}: ground under the hero");

                // multi-tier terrain with real colliders, and a ladder / rope for every upper tier
                Assert.GreaterOrEqual(m.decks.Length, 4, $"Map {index} has several tiers");
                foreach (var d in m.decks)
                {
                    float mid = (d.XMin + d.XMax) / 2f, top = TopAt(mid, d.Top + 3f);
                    Assert.AreEqual(d.Top, top, 0.45f, $"Map {index} deck {d.Name}: collider top");
                }
                Assert.GreaterOrEqual(m.climbs.Length, 4, $"Map {index} has ladders / ropes");
                Assert.IsTrue(m.climbs.Any(c => c.Rope) && m.climbs.Any(c => !c.Rope), $"Map {index}: both a ladder and a rope");
                foreach (var c in m.climbs)
                    Assert.IsTrue(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Any(t => t.name.Contains(c.Name)), $"Map {index}: climb {c.Name} exists");

                // monsters of this map are 2D sprites; Mini Boss / Elite / Boss have their tier and a bar above their sprite
                yield return new WaitForSeconds(2.5f);
                var views = Object.FindObjectsByType<FarmMonsterView>(FindObjectsInactive.Exclude).Where(v => v.SpawnId != null).ToList();
                Assert.GreaterOrEqual(views.Count, 6, $"Map {index} monsters");
                Assert.IsTrue(views.All(v => v.IsSprite), $"Map {index}: every monster is a 2D sprite");
                var special = views.Where(v => v.Tier != "normal").ToList();
                string expect = index switch { 3 => "mini", 6 => "mini", 8 => "elite", 9 => "mini", 10 => "boss", _ => null };
                if (expect == null) Assert.IsEmpty(special, $"Map {index}: no boss on this map");
                else
                {
                    Assert.AreEqual(1, special.Count, $"Map {index}: one {expect}");
                    var b = special[0];
                    Assert.AreEqual(expect, b.Tier);
                    float h = b.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite && r.name != "SlamWarning").Max(r => r.bounds.size.y);
                    Assert.Greater(h, expect == "boss" ? 2.6f : expect == "mini" ? 1.8f : 1.5f, $"Map {index}: {expect} is clearly bigger than the normal monsters");
                    float normalH = views.First(v => v.Tier == "normal").GetComponentsInChildren<SpriteRenderer>().Max(r => r.bounds.size.y);
                    Assert.Greater(h, normalH * 1.5f, "boss sprite taller than a normal monster");
                    Assert.Greater(b.BarHeight, h * 0.85f, "name + HP bar sit above the sprite, not on it");
                    // phase 2 + slam warning come from the server state: show them
                    var st = new FarmMonsterState { id = b.SpawnId, type = "x", name = "b", hp = 100, maxHp = 1000, alive = true, tier = expect, phase = 2, slamInMs = 900, slamReach = 3.4f };
                    b.Apply(st);
                    yield return null; yield return null;
                    Assert.IsTrue(b.Enraged, "phase 2 shown");
                    Assert.IsTrue(b.SlamWarningVisible, "slam warning on the ground before the hit");
                    Assert.IsFalse(b.GetComponentsInChildren<SpriteRenderer>().First(r => r.name != "SlamWarning" && r.sprite).color.Equals(Color.white), "enraged tint");
                }
                if (index == 3 || index == 6 || index == 10) { yield return WalkTo(Mathf.Min(Pos.x + 10f, 60f), 8f); }
                yield return Shot($"m{index:00}_{m.mapId}");

                if (i + 1 < chain.Length)
                    yield return TakePortal(m.portals.First(p => p.Id.Contains("_east_")).X, chain[i + 1].scene);
            }
            Assert.AreEqual(8, backdrops.Count);
            // Map 10 is the end of the chapter: only the way back
            Assert.IsFalse(chain[^1].portals.Any(p => p.Id.Contains("_east_")));
            yield return TakePortal(chain[^1].portals.First(p => p.Id.Contains("_west_")).X, chain[^2].scene);
            Assert.AreEqual(chain[^2].mapId, AdventureWorldSession.Current.Character.lastMapId, "and back down the chain");
        }
    }
}
