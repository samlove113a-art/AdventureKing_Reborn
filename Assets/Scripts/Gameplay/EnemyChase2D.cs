using UnityEngine;

namespace AdventureKing.Gameplay
{
    // Local-only QA AI, not trusted online server combat.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health2D))]
    public sealed class EnemyChase2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Health2D health;
        [SerializeField] private Transform player;
        [SerializeField, Min(0.1f)] private float detection = 7f;
        [SerializeField, Min(0.1f)] private float reach = 1f;
        [SerializeField, Min(0.1f)] private float speed = 2.4f;
        [SerializeField, Min(1)] private int damage = 7;
        [SerializeField, Min(0.1f)] private float hitCooldown = 1.3f;
        [SerializeField] private SpriteRenderer visual;
        private float readyAt;

        private void Awake()
        {
            if (!body) body = GetComponent<Rigidbody2D>();
            if (!health) health = GetComponent<Health2D>();
            body.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            if (health.IsDead || !player) { Stop(); return; }
            float distance = Vector2.Distance(transform.position, player.position);
            Health2D targetHealth = player.GetComponentInParent<Health2D>();
            if (distance > detection || (targetHealth && targetHealth.IsDead)) { Stop(); return; }
            float direction = Mathf.Sign(player.position.x - transform.position.x);
            if (distance > reach)
            {
                body.linearVelocity = new Vector2(direction * speed, body.linearVelocity.y);
                if (visual) visual.flipX = direction < 0f;
            }
            else
            {
                Stop();
                if (targetHealth && Time.time >= readyAt)
                {
                    readyAt = Time.time + hitCooldown;
                    targetHealth.TakeDamage(damage, health);
                }
            }
        }

        private void Stop() => body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }
}
