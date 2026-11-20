using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>A platform that glides between two points; whoever stands on it is carried along. Used with a one-way collider.</summary>
    public sealed class MovingPlatform2D : MonoBehaviour
    {
        [SerializeField] private Vector2 from, to;
        [SerializeField, Min(0.1f)] private float seconds = 6f;
        [SerializeField, Min(0f)] private float pause = 0.8f;
        private Rigidbody2D rb;
        private Vector2 lastPos;

        public Vector2 Velocity { get; private set; }
        public void Configure(Vector2 a, Vector2 b, float travelSeconds) { from = a; to = b; seconds = travelSeconds; transform.position = a; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (!rb) { rb = gameObject.AddComponent<Rigidbody2D>(); }
            rb.bodyType = RigidbodyType2D.Kinematic;
            lastPos = transform.position;
        }

        private void FixedUpdate()
        {
            float cycle = seconds + pause;
            float t = Mathf.Repeat(Time.time, cycle * 2f);
            float k = t < seconds ? t / seconds : t < cycle ? 1f : t < cycle + seconds ? 1f - (t - cycle) / seconds : 0f;
            k = k * k * (3f - 2f * k);                                   // ease in / out
            Vector2 p = Vector2.Lerp(from, to, k);
            Velocity = (p - lastPos) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            rb.MovePosition(p);
            lastPos = p;
        }

        private void OnCollisionStay2D(Collision2D c)
        {
            Rigidbody2D other = c.rigidbody;
            if (!other || other.bodyType != RigidbodyType2D.Dynamic) return;
            foreach (ContactPoint2D cp in c.contacts)
                if (cp.normal.y < -0.5f)                                 // the other body rests on top
                {
                    other.position += Velocity * Time.fixedDeltaTime;
                    break;
                }
        }
    }
}
