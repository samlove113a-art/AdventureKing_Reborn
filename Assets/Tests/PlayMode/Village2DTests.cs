using System.Collections;
using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>The hub (Thị trấn Chuông Gió) as a pure 2D map: orthographic camera, no 3D object, sprite scenery, working decks, jump pad, moving platform, secret doorway. Screenshots: QA/Village2D/*.png</summary>
    public sealed class Village2DTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;
        private Rigidbody2D rb;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5271);
            yield return server.Start();
            Directory.CreateDirectory("QA/Village2D");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            HeroVisualStyle.Override = null;
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Village2D/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        private IEnumerator Teleport(float x, float y, float settle = 1.2f)
        {
            rb.position = new Vector2(x, y);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(settle);
        }

        [UnityTest]
        public IEnumerator Hub_Is_Pure_2D_And_Its_Platforms_Gimmicks_And_Pictures_Work()
        {
            HeroVisualStyle.Override = "2d";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("v2d" + Random.Range(1000, 9999), "v2d-pass-2026", HeroClass.Mage, "Làng 2D");
            var world = AdventureWorldSession.Current;
            motor = world.Player; input = motor.GetComponent<PlayerInput2D>(); rb = motor.GetComponent<Rigidbody2D>();
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            // ---- pure 2D
            Camera cam = Camera.main;
            Assert.IsTrue(cam.orthographic, "orthographic camera");
            Assert.AreEqual(0, Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Length, "no 3D light");
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length, "no 3D collider");
            Assert.AreEqual(0, Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length, "no 3D rigidbody");
            var meshes = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(m => !m.GetComponent<TextMesh>()).ToList();
            Assert.AreEqual(0, meshes.Count, "no visible 3D mesh in the hub: " + string.Join(", ", meshes.Take(5).Select(m => m.name)));
            Assert.AreEqual(0, Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude).Length, "no visible skinned mesh");
            Assert.Greater(Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude).Length, 60, "the world is sprites");
            Assert.IsNotNull(Object.FindAnyObjectByType<ParallaxBackground4>(), "parallax background");

            yield return Shot("01_spawn");
            yield return Teleport(3f, 1f); yield return Shot("02_bell_tower_statue");
            yield return Teleport(-44f, 1f); yield return Shot("03_forge_market");
            yield return Teleport(40f, 1f); yield return Shot("04_teahouse");
            yield return Teleport(66f, 1f); yield return Shot("05_portal_east");
            yield return Teleport(-56f, 1f); yield return Shot("06_portal_west");

            // ---- a deck holds the hero at its collider top (rope bridge at 5 m)
            yield return Teleport(22f, 6.5f);
            Assert.AreEqual(5f + 0.8f, rb.position.y, 0.35f, "stands on the bridge deck");
            yield return Shot("07_bridge");
            yield return Teleport(5f, 11.5f);
            Assert.AreEqual(10f + 0.8f, rb.position.y, 0.35f, "stands on the wind lookout");
            yield return Shot("08_lookout");

            // ---- jump pad
            var pad = Object.FindAnyObjectByType<JumpPad2D>();
            Assert.IsNotNull(pad);
            yield return Teleport(pad.transform.position.x, 1.2f, 0.2f);
            float peak = rb.position.y;
            float end = Time.time + 2.5f;
            while (Time.time < end) { peak = Mathf.Max(peak, rb.position.y); yield return null; }
            Assert.Greater(pad.Launches, 0, "the pad fired");
            Assert.Greater(peak, 5f, "the pad throws the hero up to the bridge height");

            // ---- moving platform
            var plat = Object.FindAnyObjectByType<MovingPlatform2D>();
            Assert.IsNotNull(plat);
            float y0 = plat.transform.position.y;
            yield return new WaitForSeconds(2.5f);
            Assert.Greater(Mathf.Abs(plat.transform.position.y - y0), 0.5f, "the platform moves");
            yield return Teleport(plat.transform.position.x, plat.transform.position.y + 1.2f, 0.2f);
            yield return Shot("09_moving_platform");

            // ---- secret doorway
            var doors = Object.FindObjectsByType<SecretPortal2D>(FindObjectsInactive.Exclude);
            Assert.AreEqual(2, doors.Length);
            SecretPortal2D a = doors.OrderBy(d => d.transform.position.y).First(), b = doors.OrderByDescending(d => d.transform.position.y).First();
            yield return Teleport(a.transform.position.x, a.transform.position.y + 0.5f, 1.5f);
            var heroCol = rb.GetComponent<Collider2D>();
            Assert.Greater(a.Uses, 0, $"the doorway fired (door at {a.transform.position}, hero at {rb.position}, touching {a.GetComponent<Collider2D>().IsTouching(heroCol)}, twin {(b ? b.name : "none")}, trigger {a.GetComponent<Collider2D>().isTrigger}, layer {a.gameObject.layer})");
            Assert.AreEqual(b.transform.position.x, rb.position.x, 1.2f, "arrived at the twin doorway");
            yield return Shot("10_secret_doorway");

        }
    }
}
