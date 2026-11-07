using System.Collections;
using AdventureKing.Gameplay;
using AdventureKing.QA;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    public sealed class MovementTests : GameplayFixture
    {
        [UnityTest]
        public IEnumerator Keyboard_LeftRight_Moves_And_Never_Sticks()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);

            Down(keyboard.dKey);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1f, p.Input.Horizontal, 1e-4f);
            Assert.Greater(p.Body.linearVelocity.x, 5.5f, "Should reach run speed to the right");
            Assert.AreEqual(1, p.Motor.Facing);

            Up(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, p.Input.Horizontal, 1e-4f, "Right key stuck after release");
            Assert.Less(Mathf.Abs(p.Body.linearVelocity.x), 0.05f);

            Down(keyboard.aKey);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(p.Body.linearVelocity.x, -5.5f, "Should reach run speed to the left");
            Assert.AreEqual(-1, p.Motor.Facing);

            Down(keyboard.dKey); // both held -> cancel out
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, p.Input.Horizontal, 1e-4f);

            Up(keyboard.aKey);
            Up(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, p.Input.Horizontal, 1e-4f, "Keys stuck after releasing both");
            Assert.Less(Mathf.Abs(p.Body.linearVelocity.x), 0.05f);
        }

        [UnityTest]
        public IEnumerator Touch_LeftRight_Releases_On_Up_Exit_And_Disable()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);

            var buttonGo = Track(new GameObject("Test_TouchRight"));
            var right = buttonGo.AddComponent<TouchButton2D>();
            QaWorldFactory.SetField(right, "target", p.Input);
            QaWorldFactory.SetField(right, "action", PlayerInput2D.TouchAction.Right);
            var leftGo = Track(new GameObject("Test_TouchLeft"));
            var left = leftGo.AddComponent<TouchButton2D>();
            QaWorldFactory.SetField(left, "target", p.Input);
            QaWorldFactory.SetField(left, "action", PlayerInput2D.TouchAction.Left);

            right.OnPointerDown(null);
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(p.Body.linearVelocity.x, 5.5f);
            right.OnPointerUp(null);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, p.Input.Horizontal, 1e-4f, "Touch right stuck after pointer up");

            left.OnPointerDown(null);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(p.Body.linearVelocity.x, -5.5f);
            left.OnPointerExit(null); // finger slid off the button
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, p.Input.Horizontal, 1e-4f, "Touch left stuck after pointer exit");

            right.OnPointerDown(null);
            yield return null;
            yield return null;
            buttonGo.SetActive(false); // e.g. UI hidden mid-press
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, p.Input.Horizontal, 1e-4f, "Touch right stuck after button disabled");
            Assert.Less(Mathf.Abs(p.Body.linearVelocity.x), 0.05f);
        }

        private IEnumerator MeasureRise(QaWorldFactory.PlayerRig p, float seconds, System.Action<float> result)
        {
            float startY = p.Body.position.y, maxY = startY, end = Time.time + seconds;
            while (Time.time < end)
            {
                maxY = Mathf.Max(maxY, p.Body.position.y);
                yield return new WaitForFixedUpdate();
            }
            result(maxY - startY);
        }

        [UnityTest]
        public IEnumerator Jump_Held_Is_Full_And_Tap_Is_Cut()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);

            float fullRise = 0f;
            Down(keyboard.spaceKey);
            yield return MeasureRise(p, 0.8f, r => fullRise = r);
            Up(keyboard.spaceKey);
            yield return WaitGrounded(p.Motor);
            // v^2 / 2g = 10^2 / (2 * 9.81 * 3) = 1.70
            Assert.That(fullRise, Is.InRange(1.45f, 1.95f), "Full jump height");

            float tapRise = 0f;
            Down(keyboard.spaceKey);
            yield return null;
            yield return new WaitForFixedUpdate();
            Up(keyboard.spaceKey);
            yield return MeasureRise(p, 0.8f, r => tapRise = r);
            yield return WaitGrounded(p.Motor);
            Assert.Less(tapRise, fullRise * 0.6f, $"Jump cut not applied (tap {tapRise:0.00} vs full {fullRise:0.00})");
            Assert.Greater(tapRise, 0.1f, "Tap should still hop");
            TestContext.WriteLine($"fullRise={fullRise:0.000} tapRise={tapRise:0.000}");
        }

        [UnityTest]
        public IEnumerator Jump_Lands_On_Elevated_Platform()
        {
            SpawnPlatform(new Vector2(3.5f, 0.75f), new Vector2(3f, 0.5f)); // top y = 1.0, x 2..5
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);

            Down(keyboard.dKey);
            Down(keyboard.spaceKey);
            yield return new WaitForSeconds(0.5f);
            Up(keyboard.dKey);
            yield return new WaitForSeconds(0.4f);
            Up(keyboard.spaceKey);
            yield return WaitGrounded(p.Motor);

            Assert.That(p.Body.position.x, Is.InRange(2f, 5f), "Should be standing on the platform span");
            Assert.Greater(p.Body.position.y, 1.6f, "Should be standing on top of the platform (y≈1.8)");
            Assert.IsTrue(p.Motor.IsGrounded);
        }

        [UnityTest]
        public IEnumerator No_Double_Jump_From_Ground_Probe_Overlap()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);
            float startY = p.Body.position.y;

            yield return Tap(keyboard.spaceKey, 1);
            Down(keyboard.spaceKey); // second press right after take-off
            float maxY = startY, end = Time.time + 1f;
            while (Time.time < end)
            {
                maxY = Mathf.Max(maxY, p.Body.position.y);
                yield return new WaitForFixedUpdate();
            }
            Up(keyboard.spaceKey);
            Assert.Less(maxY - startY, 1.95f, "Second press during take-off produced a double jump");
        }

        [UnityTest]
        public IEnumerator Buffered_Jump_Fires_On_Landing_Only_Within_Window()
        {
            // Pressed ~0.06 s before touchdown: must jump on landing without a new press.
            var p = SpawnPlayer(new Vector2(0f, 3f));
            while (p.Body.position.y - 0.8f > 0.7f) yield return new WaitForFixedUpdate();
            Down(keyboard.spaceKey);
            float maxAfter = 0f, end = Time.time + 0.9f;
            bool landed = false;
            while (Time.time < end)
            {
                if (!landed && p.Body.linearVelocity.y > 1f) landed = true;
                if (landed) maxAfter = Mathf.Max(maxAfter, p.Body.position.y);
                yield return new WaitForFixedUpdate();
            }
            Up(keyboard.spaceKey);
            Assert.IsTrue(landed, "Buffered jump never fired");
            Assert.Greater(maxAfter, 0.8f + 1.4f, "Buffered jump should be a full jump");
            yield return WaitGrounded(p.Motor);

            // Pressed ~0.24 s before touchdown (outside the 0.12 s buffer): must NOT jump.
            Teleport(p, new Vector2(0f, 5f));
            while (p.Body.position.y - 0.8f > 3f) yield return new WaitForFixedUpdate();
            yield return Tap(keyboard.spaceKey, 1);
            yield return WaitGrounded(p.Motor);
            float peak = 0f;
            end = Time.time + 0.5f;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, p.Body.position.y);
                yield return new WaitForFixedUpdate();
            }
            Assert.Less(peak, 1.0f, "Stale jump press outside buffer window still fired");
        }

        private IEnumerator RunOffLedge(QaWorldFactory.PlayerRig p)
        {
            Down(keyboard.dKey);
            float end = Time.time + 3f;
            while (p.Motor.IsGrounded && Time.time < end) yield return new WaitForFixedUpdate();
            Assert.IsFalse(p.Motor.IsGrounded, "Never left the ledge");
        }

        [UnityTest]
        public IEnumerator Coyote_Jump_Allowed_Briefly_After_Leaving_Ledge()
        {
            SpawnPlatform(new Vector2(-3f, 2.75f), new Vector2(6f, 0.5f)); // top y = 3, ends at x = 0
            var p = SpawnPlayer(new Vector2(-2f, 3.85f));
            yield return WaitGrounded(p.Motor);
            yield return RunOffLedge(p);
            yield return new WaitForSeconds(0.05f);
            yield return Tap(keyboard.spaceKey, 1);
            yield return Fixed(2);
            Up(keyboard.dKey);
            Assert.Greater(p.Body.linearVelocity.y, 3f, "Coyote jump within 0.1 s was rejected");
        }

        [UnityTest]
        public IEnumerator Coyote_Jump_Rejected_After_Window()
        {
            SpawnPlatform(new Vector2(-3f, 2.75f), new Vector2(6f, 0.5f));
            var p = SpawnPlayer(new Vector2(-2f, 3.85f));
            yield return WaitGrounded(p.Motor);
            yield return RunOffLedge(p);
            yield return new WaitForSeconds(0.25f);
            yield return Tap(keyboard.spaceKey, 1);
            yield return Fixed(2);
            Up(keyboard.dKey);
            Assert.Less(p.Body.linearVelocity.y, 0f, "Late coyote jump (0.25 s) should be rejected");
        }

        [UnityTest]
        public IEnumerator Ladder_Climbs_Up_Down_And_Exit_Restores_Gravity()
        {
            Track(QaWorldFactory.CreateLadder(new Vector2(0f, 2.6f), new Vector2(0.8f, 5.2f), null));
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);

            float y0 = p.Body.position.y;
            Down(keyboard.wKey);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(p.Motor.IsClimbing, "W inside ladder should start climbing");
            Assert.AreEqual(0f, p.Body.gravityScale, 1e-4f);
            Assert.Greater(p.Body.position.y - y0, 1.1f, "Should climb ~1.5 m in 0.5 s");
            Up(keyboard.wKey);
            yield return Fixed(3);

            float hold = p.Body.position.y;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(hold, p.Body.position.y, 0.02f, "Should hang still on ladder with no input");

            Down(keyboard.sKey);
            yield return new WaitForSeconds(0.2f);
            Up(keyboard.sKey);
            yield return Fixed(2);
            Assert.Less(p.Body.position.y, hold - 0.3f, "S should climb down");
            Assert.IsTrue(p.Motor.IsClimbing);

            Down(keyboard.dKey); // step sideways out of the ladder trigger in mid-air
            float end = Time.time + 1f;
            while (p.Motor.IsClimbing && Time.time < end) yield return new WaitForFixedUpdate();
            Up(keyboard.dKey);
            Assert.IsFalse(p.Motor.IsClimbing, "Leaving the ladder zone should stop climbing");
            Assert.AreEqual(QaWorldFactory.PlayerGravity, p.Body.gravityScale, 1e-4f, "Gravity not restored");
            yield return WaitGrounded(p.Motor);
        }

        [UnityTest]
        public IEnumerator Ladder_Jump_Off_And_Walk_Off_At_Floor()
        {
            Track(QaWorldFactory.CreateLadder(new Vector2(0f, 2.6f), new Vector2(3f, 5.2f), null));
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);

            Down(keyboard.wKey);
            yield return new WaitForSeconds(0.3f);
            Up(keyboard.wKey);
            Assert.IsTrue(p.Motor.IsClimbing);
            yield return Tap(keyboard.spaceKey, 1);
            yield return Fixed(2);
            Assert.IsFalse(p.Motor.IsClimbing, "Jump should detach from ladder");
            Assert.AreEqual(QaWorldFactory.PlayerGravity, p.Body.gravityScale, 1e-4f);
            Assert.Greater(p.Body.linearVelocity.y, 3f);
            yield return WaitGrounded(p.Motor);

            // Climb down to the floor inside a wide ladder zone, then walk sideways: normal movement resumes.
            Down(keyboard.wKey);
            yield return new WaitForSeconds(0.2f);
            Up(keyboard.wKey);
            Down(keyboard.sKey);
            yield return new WaitForSeconds(0.4f);
            Up(keyboard.sKey);
            Assert.IsTrue(p.Motor.IsGrounded);
            Down(keyboard.dKey);
            yield return Fixed(5);
            Up(keyboard.dKey);
            Assert.IsFalse(p.Motor.IsClimbing, "Walking along the floor should end climbing");
            Assert.AreEqual(QaWorldFactory.PlayerGravity, p.Body.gravityScale, 1e-4f);
        }
    }
}
