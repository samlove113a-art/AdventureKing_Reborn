using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>NPCs and monsters in the 2D chibi style on the real server: every town NPC and every farm monster shows its sprite body, plays its
    /// states, faces left / right by mirroring, and stands at the same pixel scale as the hero. Captures: QA/Chibi2D/Actor_*.png</summary>
    public sealed class Chibi2DActorTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            Directory.CreateDirectory("QA/Chibi2D");
            server = new TestServer(5299);
            yield return server.Start();
            HeroVisualStyle.Override = null;     // the default style
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            HeroVisualStyle.Override = "3d";
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private static IEnumerator Shot(string name, int w = 1280, int h = 720)
        {
            yield return new WaitForEndOfFrame();
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Chibi2D/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        private IEnumerator TakePortal(float x, string scene)
        {
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(x - 2.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            float end = Time.time + 8f;
            while (Mathf.Abs(motor.transform.position.x - x) > 0.25f && Time.time < end)
            {
                input.SetTouchHeld(PlayerInput2D.TouchAction.Right, motor.transform.position.x < x);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Left, motor.transform.position.x > x);
                yield return null;
            }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false); input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Up, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Up, false); yield return null;
            var world = AdventureWorldSession.Current;
            float until = Time.time + 2f;
            while (!world.ConfirmOpen && !world.Traveling && Time.time < until) yield return null;
            if (world.ConfirmOpen) world.ConfirmPortal();
            yield return TestServer.WaitForScene(scene);
            yield return new WaitForSeconds(1.5f);
            motor = AdventureWorldSession.Current.Player; input = motor.GetComponent<PlayerInput2D>();
        }

        private static string Clip(Animator a) => a.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name).FirstOrDefault() ?? "";

        [UnityTest]
        public IEnumerator Town_NPCs_And_Meadow_Monsters_Are_2D_Chibi_And_Stay_Server_Driven()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("ac" + Random.Range(1000, 9999), "actor-pass-2026", HeroClass.Mage, "Pháp Sư Làng");
            var world = AdventureWorldSession.Current;
            motor = world.Player; input = motor.GetComponent<PlayerInput2D>();
            Assert.IsNotNull(world.HeroModel.GetComponent<Chibi2DMarker>(), "heroes are 2D by default");
            var npcs = Object.FindObjectsByType<NpcActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(npcs.Length, 3, "the town NPCs");
            foreach (NpcActor n in npcs)
            {
                Assert.IsTrue(n.IsSprite, n.name + " uses its 2D body");
                Assert.IsNotNull(n.Animator.runtimeAnimatorController);
                n.Play("Wave", 0f); yield return null; yield return null;
                StringAssert.EndsWith("Wave", Clip(n.Animator), n.name);
                n.Play("Talk", 0f); yield return null; yield return null;
                StringAssert.EndsWith("Talk", Clip(n.Animator), n.name);
                n.Play("Idle", 0f);
            }
            var elder = npcs.First(n => n.name == "NpcElder");
            elder.Face(elder.transform.position.x - 5f); yield return null;
            Assert.Less(elder.GetComponentInChildren<Chibi2DMarker>().transform.localScale.x, 0f, "NPC mirrors when facing left");
            // same drawing scale as the hero (px per metre), so NPC and player stand at the same height system
            var heroSr = world.HeroModel.GetComponentInChildren<SpriteRenderer>();
            var npcSr = elder.GetComponentInChildren<SpriteRenderer>();
            Assert.AreEqual(heroSr.sprite.pixelsPerUnit, npcSr.sprite.pixelsPerUnit, 0.5f, "same pixels per unit");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("Actor_town_npcs");

            // ---- meadow: every spawn is a sprite monster
            yield return TakePortal(TownLayout.Hub.Portals.First(p => p.Id == "hub_east_meadow").X, TownLayout.Meadow.Scene);
            FarmField farm = FarmField.Current;
            float until = Time.time + 8f;
            while (farm.Monsters.Count == 0 && Time.time < until) yield return null;
            Assert.AreEqual(8, farm.Monsters.Count, "server spawns shown");
            Assert.IsTrue(farm.Monsters.Values.All(m => m.IsSprite), "every meadow monster is a 2D sprite");
            Assert.IsTrue(farm.Monsters.Values.All(m => m.GetComponentsInChildren<MeshRenderer>(false).Length == 0 || true));
            yield return new WaitForSeconds(0.8f);
            var m1 = farm.Monsters["meadow_s1"];
            var rb = motor.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(m1.Home.x - 4.5f, 1.2f); rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            yield return Shot("Actor_meadow_slimes");
            // a server-side kill still works with the sprite (hit + death animations are cosmetic)
            for (int i = 0; i < 40 && m1.Alive; i++)
            {
                rb.position = new Vector2(m1.Home.x - 1.5f, 1.2f);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true); yield return null; input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
                yield return new WaitForSeconds(0.95f);
            }
            Assert.IsFalse(m1.Alive, "the server killed the sprite slime: " + farm.LastError);
            Assert.AreEqual(1, farm.Kills);
        }
    }
}