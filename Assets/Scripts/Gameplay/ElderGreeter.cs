using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// New-player welcome (tutorial step 0): the Village Elder walks up to the hero, turns, waves and greets the
    /// character by name in a dialog. Closing the dialog records the step on the server (it never repeats, on any
    /// device), then the Elder bows and walks back to his spot. Returning players are not interrupted.
    /// </summary>
    public sealed class ElderGreeter : MonoBehaviour
    {
        [SerializeField] private NpcActor elder;
        [SerializeField] private GameObject dialog;
        [SerializeField] private Text speaker;
        [SerializeField] private Text line;
        [SerializeField] private Button continueButton;
        [SerializeField] private float startDelay = 1.2f;

        public bool Greeting { get; private set; }
        public bool DialogOpen => dialog && dialog.activeSelf;
        public bool Done { get; private set; }

        private bool closed;
        private float home;

        private void Awake()
        {
            if (dialog) dialog.SetActive(false);
            if (continueButton) continueButton.onClick.AddListener(Close);
        }

        private IEnumerator Start()
        {
            if (!elder) yield break;
            home = elder.transform.position.x;
            // Wait for the world session to restore the character (and its server tutorial state).
            float until = Time.time + 5f;
            while ((!AdventureWorldSession.Current || AdventureWorldSession.Current.Character == null) && Time.time < until) yield return null;
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (!world || world.Character == null || world.Character.tutorialCompleted || world.Character.tutorialStep > 0) { Done = true; yield break; }

            Greeting = true;
            yield return new WaitForSeconds(startDelay);
            PlayerMotor2D player = world.Player;
            var input = player.GetComponent<PlayerInput2D>();
            float px = player.transform.position.x;
            float side = px >= home ? -1f : 1f;                    // stop on the side he comes from
            yield return elder.WalkTo(px + side * 1.7f);
            elder.Face(player.transform.position.x);
            yield return elder.Gesture("Wave", 2.2f, "Talk");

            if (input) input.enabled = false;                       // the hero listens; movement resumes after
            if (speaker) speaker.text = "Trưởng Làng";
            if (line) line.text = $"Chào mừng <b>{world.Character.characterName}</b> đến Thị trấn Chuông Gió!\n" +
                                  "Ta là Trưởng Làng. Hãy đi dạo quanh thị trấn, leo lên Sân Chuông bằng dây leo, " +
                                  "và khi sẵn sàng, cổng rune ở phía đông sẽ đưa con ra Đồng Cỏ Gió.";
            if (dialog) dialog.SetActive(true);
            closed = false;
            while (!closed) yield return null;
            if (dialog) dialog.SetActive(false);
            if (input) input.enabled = true;

            // Record the step on the server (server enforces order; never repeats on re-login).
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (server && world.Character != null)
                server.CompleteTutorialStep(world.Character.characterId, 0, (ok, updated, message) =>
                {
                    if (ok && updated != null)
                    {
                        world.Character.tutorialStep = updated.tutorialStep;
                        world.Character.tutorialCompleted = updated.tutorialCompleted;
                    }
                });

            yield return elder.Gesture("Bow", 1.8f);
            yield return elder.WalkTo(home);
            elder.FaceFront();
            Greeting = false;
            Done = true;
        }

        /// <summary>"Cảm ơn ông!" button (also E / Enter).</summary>
        public void Close() => closed = true;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (DialogOpen && UnityEngine.InputSystem.Keyboard.current is { } k &&
                (k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                Close();
#endif
        }
    }
}
