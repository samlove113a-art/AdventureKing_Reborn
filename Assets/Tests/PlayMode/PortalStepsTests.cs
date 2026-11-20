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
    /// The hub portal's stone steps are walkable: the hero climbs them from either side (no sinking into the
    /// pedestal), stands on the top step inside the trigger, and the vortex sits in the ring opening.
    /// Screenshots: QA/Portal/*.png
    /// </summary>
    public sealed class PortalStepsTests
    {
        private TestServer server;
        private PlayerInput2D input;
        private PlayerMotor2D motor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5231);
            yield return server.Start();
            Directory.CreateDirectory("QA/Portal");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (input) foreach (PlayerInput2D.TouchAction a in System.Enum.GetValues(typeof(PlayerInput2D.TouchAction))) input.SetTouchHeld(a, false);
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        private Vector2 Pos => motor.transform.position;

        private IEnumerator Walk(int dir, float seconds)
        {
            input.SetTouchHeld(dir > 0 ? PlayerInput2D.TouchAction.Right : PlayerInput2D.TouchAction.Left, true);
            yield return new WaitForSeconds(seconds);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.4f);
        }

        private static IEnumerator Shot(string name)
        {
            yield return null;
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"QA/Portal/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Hero_Climbs_Portal_Steps_And_Stands_In_The_Gate()
        {
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("portal" + Random.Range(1000, 9999), "portal-pass-2026", HeroClass.Swordsman, "Bước Lên Bệ");
            var world = AdventureWorldSession.Current;
            motor = world.Player;
            input = motor.GetComponent<PlayerInput2D>();
            var rb = motor.GetComponent<Rigidbody2D>();
            var col = motor.GetComponent<Collider2D>();
            var gate = Object.FindObjectsByType<PortalGate2D>(FindObjectsInactive.Exclude).First(g => g.name == "Portal_hub_east_meadow"); // the meadow gate asks first (halls do not)
            float gx = gate.transform.position.x;
            var steps = gate.transform.Find("PortalSteps");
            Assert.IsNotNull(steps, "walkable step surface built from the gate mesh");
            float top = steps.GetComponent<PolygonCollider2D>().bounds.max.y;
            Assert.Greater(top, 0.5f, "the steps really rise above the ground");

            // ---- from the left: walk in, feet end up on the steps, never below their surface
            rb.position = new Vector2(gx - 4.2f, 1.2f); rb.linearVelocity = Vector2.zero;
            motor.transform.position = rb.position;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(col.bounds.min.y, 0.2f, "starts on the ground");
            int steps0 = motor.StepsTaken;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, true);
            float until = Time.time + 4f;
            while (Pos.x < gx - 0.1f && Time.time < until) yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Right, false);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Pos.x, gx - 0.6f, "walked up to the gate (not blocked by a riser)");
            Assert.Greater(motor.StepsTaken, steps0, "stepped up the risers");
            Assert.AreEqual(top, col.bounds.min.y, 0.12f, "feet on the top step, not sunk into the pedestal");
            Assert.IsTrue(gate.PlayerInside, "inside the portal trigger on the top step");
            yield return Shot("01_on_top_step");

            // ---- vortex sits in the ring opening, above the hero
            Vector3 v = gate.Vortex.position;
            Assert.AreEqual(gx, v.x, 0.3f, "vortex centred on the gate");
            Assert.Greater(v.y, top + 1.2f, "vortex above the step, inside the ring");
            Assert.Less(v.y, top + 2.6f, "vortex not floating above the ring");

            // ---- off the far side and back up from the right
            yield return Walk(1, 1.2f);
            Assert.Less(col.bounds.min.y, 0.2f, "down on the ground on the right");
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, true);
            until = Time.time + 4f;
            while (Pos.x > gx + 0.1f && Time.time < until) yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Pos.x, gx + 0.6f, "climbed back from the right");
            Assert.AreEqual(top, col.bounds.min.y, 0.12f, "standing on the top step again");

            // ---- jump on the platform still works
            float y0 = Pos.y, peak = y0;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true);
            until = Time.time + 0.9f;
            while (Time.time < until) { peak = Mathf.Max(peak, Pos.y); yield return null; }
            input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false);
            yield return new WaitForSeconds(1f);
            Assert.Greater(peak, y0 + 0.8f, "jumps from the step");
            Assert.AreEqual(top, col.bounds.min.y, 0.12f, "lands back on the step");

            // ---- the portal still works from up here (confirmation for leaving town)
            gate.Activate();
            yield return null;
            Assert.IsTrue(world.ConfirmOpen, "portal asks to travel");
            yield return Shot("02_portal_confirm_on_step");
            world.CancelPortal();
        }
    }
}
