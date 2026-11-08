using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Original Adventure King rune-crystal portal. Stand inside its trigger and press Up (W / ↑ / stick up)
    /// or Interact (E) to travel. Travel itself is validated by the server (portal must be on the character's
    /// current map and within reach); the world session performs the scene change.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class PortalGate2D : MonoBehaviour
    {
        [SerializeField] private string portalId = "hub_east_meadow";
        [SerializeField] private string destinationName = "Đồng Cỏ Gió";
        [SerializeField] private GameObject prompt;
        [SerializeField] private Text promptLabel;
        [SerializeField] private AudioSource hum;
        [SerializeField] private Light glow;
        [SerializeField, Min(0f)] private float glowBase = 2.2f, glowNear = 4f;
        [Tooltip("Ask before travelling (e.g. leaving town for a field).")]
        [SerializeField] private bool requireConfirmation;
        [Tooltip("Vortex root animated when the portal fires.")]
        [SerializeField] private Transform vortex;

        private PlayerInput2D playerInside;
        private bool upWasHeld;
        private float glowTarget;

        public string PortalId => portalId;
        public bool RequireConfirmation => requireConfirmation;
        public string DestinationName => destinationName;
        public bool PlayerInside => playerInside;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            if (prompt) prompt.SetActive(false);
            if (promptLabel) promptLabel.text = "▲ / W  ·  Đến " + destinationName;
            glowTarget = glowBase;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var input = other.GetComponentInParent<PlayerInput2D>();
            if (!input) return;
            playerInside = input;
            upWasHeld = input.Vertical > 0.5f; // don't fire just because Up was already held while walking in
            if (prompt) prompt.SetActive(true);
            glowTarget = glowNear;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var input = other.GetComponentInParent<PlayerInput2D>();
            if (!input || input != playerInside) return;
            playerInside = null;
            if (prompt) prompt.SetActive(false);
            glowTarget = glowBase;
        }

        private void Update()
        {
            if (glow) glow.intensity = Mathf.MoveTowards(glow.intensity, glowTarget, Time.deltaTime * 6f);
            if (!playerInside || !playerInside.enabled) return;
            bool up = playerInside.Vertical > 0.5f;
            bool pressed = (up && !upWasHeld) || playerInside.ConsumeInteract();
            upWasHeld = up;
            if (pressed) Activate();
        }

        /// <summary>Same path as the player's key press (used by touch UI and tests).</summary>
        public void Activate()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (!world) return;
            if (requireConfirmation) world.AskPortalConfirmation(this);
            else world.UsePortal(this);
        }

        /// <summary>Activation animation: the vortex surges and spins up while the player is pulled in.</summary>
        public void PlayActivation()
        {
            if (vortex) StartCoroutine(Surge());
        }

        private System.Collections.IEnumerator Surge()
        {
            Vector3 baseScale = vortex.localScale;
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.7f;
                vortex.localScale = baseScale * (1f + 0.6f * Mathf.Sin(k * Mathf.PI * 0.5f));
                vortex.Rotate(0f, 0f, -720f * Time.unscaledDeltaTime, Space.Self);
                yield return null;
            }
        }
    }
}
