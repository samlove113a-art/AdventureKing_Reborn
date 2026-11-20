using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Bouncy mushroom: launches the hero upward when they land on it (or walk into it). Squashes and springs back.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class JumpPad2D : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float launchSpeed = 17f;
        [SerializeField] private Transform visual;
        private float cooldownUntil, squash;

        public float LaunchSpeed => launchSpeed;
        public int Launches { get; private set; }

        private void Awake() { GetComponent<Collider2D>().isTrigger = true; if (!visual) visual = transform; }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < cooldownUntil) return;
            Rigidbody2D rb = other.attachedRigidbody;
            if (!rb || !other.GetComponentInParent<PlayerInput2D>()) return;
            if (rb.linearVelocity.y > 1.5f) return;                      // already going up
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, launchSpeed);
            cooldownUntil = Time.time + 0.35f;
            squash = 1f;
            Launches++;
        }

        private void Update()
        {
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 3.5f);
            float k = Mathf.Sin(squash * Mathf.PI) * 0.22f;              // squash on press, stretch on release
            if (visual) visual.localScale = new Vector3(1f + k * 0.8f, 1f - k, 1f);
        }
    }
}
