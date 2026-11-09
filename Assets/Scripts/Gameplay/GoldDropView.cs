using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// A small pile of Gold coins on the ground (server drop). Pops out of the monster, then spins and bobs.
    /// When the server confirms the pickup it flies up into the hero and disappears. Expires with the server timer.
    /// </summary>
    public sealed class GoldDropView : MonoBehaviour
    {
        public string DropId { get; private set; }
        public long Amount { get; private set; }
        public bool Pending { get; set; }
        public Vector2 Ground { get; private set; }

        private float age, expiresAt, collectT = -1f;
        private Vector3 from;
        private Transform target;

        public void Bind(FarmDropState d, Vector2 popFrom)
        {
            DropId = d.id;
            Amount = d.amount;
            Ground = new Vector2(d.x, d.y);
            from = new Vector3(popFrom.x, popFrom.y + 0.4f, 0.2f);
            transform.position = from;
            expiresAt = Time.time + d.expiresInMs / 1000f;
        }

        public bool Expired => Time.time >= expiresAt;

        /// <summary>Server confirmed: fly into the hero, then vanish.</summary>
        public void Collect(Transform hero)
        {
            target = hero;
            collectT = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (collectT >= 0f)
            {
                collectT += dt * 3f;
                Vector3 end = target ? target.position + Vector3.up * 1f : transform.position;
                transform.position = Vector3.Lerp(transform.position, end, Mathf.Clamp01(collectT));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(collectT));
                if (collectT >= 1f) Destroy(gameObject);
                return;
            }
            age += dt;
            // pop out in an arc, then rest on the ground spinning and bobbing
            Vector3 rest = new Vector3(Ground.x + 0.6f, Ground.y + 0.25f, 0.2f);
            if (age < 0.5f)
            {
                float k = age / 0.5f;
                transform.position = Vector3.Lerp(from, rest, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.1f;
            }
            else transform.position = rest + Vector3.up * (Mathf.Sin(age * 3f) * 0.08f);
            transform.rotation = Quaternion.Euler(0f, age * 140f, 0f);
            if (Time.time > expiresAt - 5f) // blink before it expires
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = Mathf.Repeat(Time.time * 6f, 1f) > 0.3f;
        }
    }
}
