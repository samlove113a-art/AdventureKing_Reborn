using System.Collections;
using AdventureKing.Gameplay;
using AdventureKing.QA;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>Loads the generated internal QA sandbox and checks it runs and is wired correctly.</summary>
    public sealed class QaSceneSmokeTests
    {
        private const string ScenePath = "Assets/QA_Internal/Scenes/QA_GameplaySandbox.unity";
        private Scene scene;

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            AsyncOperation op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.IsNotNull(op, "QA scene missing - run AdventureKing/QA (internal)/Rebuild Gameplay Sandbox Scene");
            while (!op.isDone) yield return null;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
#else
            Assert.Ignore("Editor-only smoke test");
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Application.targetFrameRate = -1; // QaHud locks 60 FPS; don't slow down other tests

            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone) yield return null;
            }
        }

        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();

        [UnityTest]
        public IEnumerator Sandbox_Runs_And_Is_Wired()
        {
            yield return new WaitForSeconds(1.0f);

            var motor = Find<PlayerMotor2D>();
            Assert.IsNotNull(motor, "Player missing");
            Assert.IsTrue(motor.IsGrounded, "Player should stand on Ground layer after spawn");
            Assert.AreEqual(QaWorldFactory.PlayerLayer, motor.gameObject.layer);
            LayerMask mask = QaWorldFactory.GetField<LayerMask>(motor, "groundMask");
            Assert.AreEqual(1 << QaWorldFactory.GroundLayer, mask.value, "GroundMask must be Ground only");
            Assert.AreEqual(QaWorldFactory.GroundLayer, LayerMask.NameToLayer("Ground"));
            Assert.IsNotNull(QaWorldFactory.GetField<Transform>(motor, "groundProbe"));

            var body = motor.GetComponent<Rigidbody2D>();
            Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
            Assert.IsTrue(body.freezeRotation);
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation, "Interpolation needed for smooth camera");
            Assert.AreEqual(CombatFaction.Player, motor.GetComponent<Health2D>().Faction);
            Assert.IsNotNull(motor.GetComponent<CapsuleCollider2D>());
            Assert.IsNotNull(motor.GetComponent<PlayerMelee2D>());

            var follow = Find<CameraFollow2D>();
            Assert.AreSame(motor.transform, QaWorldFactory.GetField<Transform>(follow, "target"));

            var enemy = Find<EnemyChase2D>();
            Assert.AreSame(motor.transform, enemy.Target);
            Assert.AreEqual(CombatFaction.Enemy, enemy.GetComponent<Health2D>().Faction);

            Assert.IsTrue(Find<LadderZone2D>().GetComponent<Collider2D>().isTrigger, "Ladder must be a trigger");

            var npc = Find<NpcInteraction2D>();
            Assert.IsTrue(npc.GetComponent<Collider2D>().isTrigger);
            Assert.AreEqual(1, QaWorldFactory.GetNpcEvent(npc).GetPersistentEventCount(), "NPC must call a real UI handler");

            Assert.IsNotNull(Find<EventSystem>());
            Assert.IsNotNull(Find<InputSystemUIInputModule>(), "UI must use the Input System UI module");
            var buttons = Object.FindObjectsByType<TouchButton2D>(FindObjectsInactive.Include);
            Assert.AreEqual(7, buttons.Length, "Left, Right, Up, Down, Jump, Attack, Interact");
            var seen = new System.Collections.Generic.HashSet<PlayerInput2D.TouchAction>();
            foreach (TouchButton2D b in buttons)
            {
                Assert.AreSame(motor.GetComponent<PlayerInput2D>(), QaWorldFactory.GetField<PlayerInput2D>(b, "target"), b.name);
                seen.Add(QaWorldFactory.GetField<PlayerInput2D.TouchAction>(b, "action"));
            }
            Assert.AreEqual(7, seen.Count, "Each touch action exactly once");
        }

        [UnityTest]
        public IEnumerator Sandbox_Touch_Move_And_Npc_Dialog_Through_Real_UI_Handler()
        {
            yield return new WaitForSeconds(0.5f);
            var input = Find<PlayerInput2D>();
            var body = input.GetComponent<Rigidbody2D>();
            float x0 = body.position.x;

            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, true);
            yield return new WaitForSeconds(0.5f);
            input.SetTouchHeld(PlayerInput2D.TouchAction.Left, false);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(body.position.x, x0 - 1.5f, "Touch left should move the player");
            Assert.Less(Mathf.Abs(body.linearVelocity.x), 0.05f, "Touch left stuck");

            var npc = Find<NpcInteraction2D>();
            body.position = new Vector2(npc.transform.position.x, 0.85f);
            yield return new WaitForSeconds(0.3f);
            var dialog = Find<QaNpcDialog>();
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, true);
            yield return null;
            yield return null;
            input.SetTouchHeld(PlayerInput2D.TouchAction.Interact, false);
            yield return null;
            Assert.AreEqual(1, dialog.InteractCount);
            Assert.IsTrue(QaWorldFactory.GetField<GameObject>(dialog, "panel").activeSelf, "Dialog panel should be visible");
        }
    }
}
