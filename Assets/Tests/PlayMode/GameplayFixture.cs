using System.Collections;
using System.Collections.Generic;
using AdventureKing.Gameplay;
using AdventureKing.QA;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Base fixture: isolated Input System (virtual keyboard) + a flat Ground floor whose top is y = 0.
    /// Every actor is built with QaWorldFactory, i.e. exactly the wiring used in the QA sandbox scene.
    /// </summary>
    public abstract class GameplayFixture : InputTestFixture
    {
        protected Keyboard keyboard;
        private readonly List<GameObject> spawned = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Track(QaWorldFactory.CreateGround("Test_Floor", new Vector2(0f, -0.5f), new Vector2(80f, 1f), null));
        }

        public override void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go) Object.Destroy(go);
            spawned.Clear();
            held.Clear();
            base.TearDown();
        }

        protected T Track<T>(T obj) where T : Object
        {
            if (obj is GameObject go) spawned.Add(go);
            else if (obj is Component c) spawned.Add(c.gameObject);
            return obj;
        }

        protected QaWorldFactory.PlayerRig SpawnPlayer(Vector2 position)
        {
            QaWorldFactory.PlayerRig rig = QaWorldFactory.CreatePlayer(position, null);
            Track(rig.Root);
            return rig;
        }

        protected QaWorldFactory.EnemyRig SpawnEnemy(Vector2 position, Transform player, bool extraHitbox = false)
        {
            QaWorldFactory.EnemyRig rig = QaWorldFactory.CreateEnemy(position, player, null, extraHitbox);
            Track(rig.Root);
            return rig;
        }

        protected GameObject SpawnPlatform(Vector2 center, Vector2 size) =>
            Track(QaWorldFactory.CreateGround("Test_Platform", center, size, null));

        private readonly HashSet<Key> held = new HashSet<Key>();

        /// <summary>Key events are queued as full keyboard state events and processed by the next
        /// player-loop input update, the same path real hardware takes, so wasPressedThisFrame is
        /// observed by MonoBehaviour.Update. (InputTestFixture.Press(queueEventOnly) builds delta events
        /// from the device's current state pointer, which is null between player-loop updates in
        /// batchmode, so it cannot be used from a [UnityTest] coroutine here.)</summary>
        protected void Down(KeyControl key)
        {
            held.Add(key.keyCode);
            QueueKeyboard();
        }

        protected void Up(KeyControl key)
        {
            held.Remove(key.keyCode);
            QueueKeyboard();
        }

        private void QueueKeyboard()
        {
            var keys = new Key[held.Count];
            held.CopyTo(keys);
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
        }

        protected IEnumerator Tap(KeyControl key, int heldFrames = 2)
        {
            Down(key);
            for (int i = 0; i < heldFrames; i++) yield return null;
            Up(key);
            yield return null;
        }

        protected static IEnumerator Fixed(int steps)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        protected static IEnumerator WaitGrounded(PlayerMotor2D motor, float timeout = 3f)
        {
            float end = Time.time + timeout;
            while (!motor.IsGrounded && Time.time < end) yield return new WaitForFixedUpdate();
            Assert.IsTrue(motor.IsGrounded, "Player never became grounded");
            // Let landing velocity settle.
            yield return Fixed(5);
        }

        protected static void Teleport(QaWorldFactory.PlayerRig rig, Vector2 position)
        {
            rig.Body.position = position;
            rig.Root.transform.position = position;
            rig.Body.linearVelocity = Vector2.zero;
        }
    }
}
