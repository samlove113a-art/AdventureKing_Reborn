using System.Collections;
using AdventureKing.Gameplay;
using AdventureKing.QA;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    public sealed class CombatAndInteractionTests : GameplayFixture
    {
        [UnityTest]
        public IEnumerator Melee_Damages_Once_Per_Swing_Never_Player_And_Respects_Cooldown()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            var e = SpawnEnemy(new Vector2(1.3f, 0.45f), p.Root.transform, extraHitbox: true);
            e.Chase.enabled = false; // isolate the player's swing
            yield return WaitGrounded(p.Motor);
            int start = e.Health.CurrentHealth;
            int swings = 0;
            p.Melee.Attacked += () => swings++;

            yield return Tap(keyboard.jKey);
            Assert.AreEqual(1, swings);
            Assert.AreEqual(start - 12, e.Health.CurrentHealth, "Exactly one hit per swing (two colliders on enemy)");
            Assert.AreEqual(p.Health.MaxHealth, p.Health.CurrentHealth, "Swing must not hurt the player");

            yield return Tap(keyboard.jKey); // inside 0.45 s cooldown
            Assert.AreEqual(1, swings, "Cooldown ignored");
            Assert.AreEqual(start - 12, e.Health.CurrentHealth);

            yield return new WaitForSeconds(0.5f);
            yield return Tap(keyboard.jKey);
            Assert.AreEqual(2, swings);
            Assert.AreEqual(start - 24, e.Health.CurrentHealth);

            // Facing left: enemy behind the player must not be hit.
            Down(keyboard.aKey);
            yield return Fixed(2);
            Up(keyboard.aKey);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(-1, p.Motor.Facing);
            yield return Tap(keyboard.jKey);
            Assert.AreEqual(start - 24, e.Health.CurrentHealth, "Hit an enemy behind the player");
        }

        [UnityTest]
        public IEnumerator Touch_Attack_Swings()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            var e = SpawnEnemy(new Vector2(1.3f, 0.45f), p.Root.transform);
            e.Chase.enabled = false;
            yield return WaitGrounded(p.Motor);
            p.Input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true);
            yield return null;
            p.Input.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false);
            yield return null;
            Assert.AreEqual(e.Health.MaxHealth - 12, e.Health.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator Enemy_Ignores_Player_Outside_Detection_Range()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            var e = SpawnEnemy(new Vector2(12f, 0.45f), p.Root.transform);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(12f, e.Body.position.x, 0.05f, "Enemy moved although player is beyond 7 m");
        }

        [UnityTest]
        public IEnumerator Enemy_Pursues_Hits_On_Cooldown_And_Stops_On_Death()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            var e = SpawnEnemy(new Vector2(5f, 0.45f), p.Root.transform);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(e.Body.position.x, 4.5f, "Enemy should chase the player within 7 m");
            Assert.Less(e.Body.linearVelocity.x, -1f);

            float end = Time.time + 4f;
            while (p.Health.CurrentHealth == p.Health.MaxHealth && Time.time < end) yield return new WaitForFixedUpdate();
            Assert.AreEqual(p.Health.MaxHealth - 7, p.Health.CurrentHealth, "First contact hit");
            float firstHit = Time.time;

            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(p.Health.MaxHealth - 7, p.Health.CurrentHealth, "Hit cooldown (1.3 s) ignored");
            while (p.Health.CurrentHealth == p.Health.MaxHealth - 7 && Time.time < firstHit + 2f)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(p.Health.MaxHealth - 14, p.Health.CurrentHealth, "Second hit after cooldown");
            Assert.That(Time.time - firstHit, Is.InRange(1.25f, 1.45f));

            Assert.IsTrue(e.Health.TakeDamage(999, p.Health));
            Assert.IsTrue(e.Health.IsDead);
            int hpAtDeath = p.Health.CurrentHealth;
            Teleport(p, new Vector2(-3f, 0.85f)); // move away: a dead enemy must not chase
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(hpAtDeath, p.Health.CurrentHealth, "Dead enemy kept attacking");
            Assert.Less(Mathf.Abs(e.Body.linearVelocity.x), 0.01f, "Dead enemy kept moving");
        }

        [Test]
        public void Health_Faction_And_Self_Damage_Rules()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            var ally = SpawnPlayer(new Vector2(3f, 0.85f));
            var e = SpawnEnemy(new Vector2(6f, 0.45f), p.Root.transform);
            Assert.IsFalse(p.Health.TakeDamage(10, p.Health), "Self damage");
            Assert.IsFalse(p.Health.TakeDamage(10, ally.Health), "Friendly fire");
            Assert.IsFalse(p.Health.TakeDamage(0, e.Health), "Zero damage");
            Assert.IsTrue(p.Health.TakeDamage(10, e.Health));
            Assert.AreEqual(90, p.Health.CurrentHealth);
            p.Health.Heal(50);
            Assert.AreEqual(100, p.Health.CurrentHealth, "Heal must clamp to max");
            int deaths = 0;
            e.Health.Died += _ => deaths++;
            e.Health.TakeDamage(1000, p.Health);
            e.Health.TakeDamage(1000, p.Health);
            Assert.AreEqual(1, deaths, "Died must fire once");
            e.Health.Heal(10);
            Assert.AreEqual(0, e.Health.CurrentHealth, "Dead units cannot be healed");
        }

        [UnityTest]
        public IEnumerator Npc_Interact_Fires_Once_Per_Press_And_Ignores_Stale_Presses()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            NpcInteraction2D npc = Track(QaWorldFactory.CreateNpc(new Vector2(0f, 0.75f), null));
            int count = 0;
            QaWorldFactory.GetNpcEvent(npc).AddListener(() => count++);
            yield return WaitGrounded(p.Motor);

            Down(keyboard.eKey);
            for (int i = 0; i < 15; i++) yield return null; // holding E
            Up(keyboard.eKey);
            yield return null;
            Assert.AreEqual(1, count, "Holding E must fire exactly once");

            yield return Tap(keyboard.eKey);
            Assert.AreEqual(2, count);

            p.Input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, true);
            for (int i = 0; i < 5; i++) yield return null;
            p.Input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, false);
            yield return null;
            Assert.AreEqual(3, count, "Touch interact");

            // Regression: press E far away, walk in later -> must NOT fire.
            Teleport(p, new Vector2(15f, 0.85f));
            yield return Fixed(3);
            yield return Tap(keyboard.eKey);
            for (int i = 0; i < 10; i++) yield return null;
            Teleport(p, new Vector2(0f, 0.85f));
            yield return Fixed(3);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(3, count, "A stale E press fired the NPC on entering its trigger");

            // Out of range presses never fire.
            Teleport(p, new Vector2(15f, 0.85f));
            yield return Fixed(3);
            yield return Tap(keyboard.eKey);
            Assert.AreEqual(3, count);
        }

        [UnityTest]
        public IEnumerator Camera_Follows_Smoothly_And_Converges()
        {
            var p = SpawnPlayer(new Vector2(0f, 0.85f));
            yield return WaitGrounded(p.Motor);
            var camGo = Track(new GameObject("Test_Camera"));
            camGo.transform.position = new Vector3(0f, 2.1f, -10f);
            var follow = camGo.AddComponent<CameraFollow2D>();
            QaWorldFactory.SetField(follow, "target", p.Root.transform);

            Down(keyboard.dKey);
            float lastX = camGo.transform.position.x;
            int backwardsFrames = 0, frames = 0;
            float end = Time.time + 1.2f;
            while (Time.time < end)
            {
                yield return null;
                float x = camGo.transform.position.x;
                if (x < lastX - 1e-4f) backwardsFrames++;
                lastX = x;
                frames++;
            }
            Up(keyboard.dKey);
            Assert.AreEqual(0, backwardsFrames, $"Camera jittered backwards in {backwardsFrames}/{frames} frames");
            yield return new WaitForSeconds(1.2f);
            Vector3 cam = camGo.transform.position;
            Assert.AreEqual(p.Root.transform.position.x, cam.x, 0.05f);
            Assert.AreEqual(p.Root.transform.position.y + 1.25f, cam.y, 0.05f);
            Assert.AreEqual(-10f, cam.z, 1e-4f, "Camera z must be preserved");
        }
    }
}
