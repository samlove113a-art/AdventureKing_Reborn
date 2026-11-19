using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Hidden doorway: stand in it for a moment and you step out of its twin. Uses enter / exit + a timer (a resting body gets no trigger-stay events).</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class SecretPortal2D : MonoBehaviour
    {
        [SerializeField] private SecretPortal2D twin;
        [SerializeField, Min(0.1f)] private float stayToUse = 0.6f;
        [SerializeField] private SpriteRenderer glow;
        private Rigidbody2D inside;
        private float insideTime;
        private static float lockUntil;

        public int Uses { get; private set; }
        public void SetTwin(SecretPortal2D other) { twin = other; }

        private void Awake() { GetComponent<Collider2D>().isTrigger = true; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (!rb || !other.GetComponentInParent<PlayerInput2D>()) return;
            inside = rb; insideTime = 0f;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.attachedRigidbody == inside) { inside = null; insideTime = 0f; if (glow) glow.color = Color.white; }
        }

        private void Update()
        {
            if (!inside || !twin || Time.time < lockUntil) return;
            insideTime += Time.deltaTime;
            if (glow) glow.color = new Color(1f, 1f, 1f, 0.65f + 0.35f * Mathf.Sin(Time.time * 14f));
            if (insideTime < stayToUse) return;
            Rigidbody2D rb = inside;
            inside = null; insideTime = 0f;
            lockUntil = Time.time + 1.4f;
            Uses++;
            rb.position = (Vector2)twin.transform.position + Vector2.up * 0.1f;
            rb.linearVelocity = Vector2.zero;
            if (glow) glow.color = Color.white;
        }
    }
}
