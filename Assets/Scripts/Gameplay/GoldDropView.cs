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
        /// <summary>Item pile (junk / equipment) instead of Gold; tinted by kind.</summary>
        public bool IsItem { get; private set; }
        public string ItemName { get; private set; }
        /// <summary>0 white .. 4 red (server rarity). Purple and above: pillar of light, bigger bounce, chime.</summary>
        public int RarityTier { get; private set; }
        public bool Collecting => collectT >= 0f;
        public LootBeam Beam { get; private set; }
        private bool chimed;

        private float age, expiresAt, collectT = -1f;
        private Vector3 from;
        private Transform target;
        private Transform[] sparkles = new Transform[0];
        private Vector3[] sparkleBase = new Vector3[0];

        /// <summary>True once the coins have hit the ground (bounce phase reached).</summary>
        public bool Landed { get; private set; }

        private void Awake()
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Sparkle")) list.Add(t);
            sparkles = list.ToArray();
            sparkleBase = System.Array.ConvertAll(sparkles, s => s.localScale);
        }

        public void Bind(FarmDropState d, Vector2 popFrom)
        {
            DropId = d.id;
            Amount = d.amount;
            Ground = new Vector2(d.x, d.y);
            from = new Vector3(popFrom.x, popFrom.y + 0.4f, 0.2f);
            transform.position = from;
            expiresAt = Time.time + d.expiresInMs / 1000f;
            IsItem = d.IsItem;
            ItemName = d.itemName;
            if (IsItem)
            {
                RarityTier = LootRarity.Tier(d.itemRarity);
                Color tint = RarityTier > 0 ? Color.Lerp(Color.white, LootRarity.ColorOf(RarityTier), 0.7f)               // blue / purple / orange / red pile
                    : d.itemKind == "equipment" ? new Color(0.72f, 0.62f, 1f) : new Color(0.62f, 0.78f, 0.58f);             // white equipment violet, junk moss green
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", tint);
                Beam = LootBeam.Attach(this, RarityTier);
            }
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
                collectT += dt * 8f; // picked: gone from the ground at once (flies into the hero in ~0.12 s)
                Vector3 end = target ? target.position + Vector3.up * 1f : transform.position;
                transform.position = Vector3.Lerp(transform.position, end, Mathf.Clamp01(collectT));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(collectT));
                if (collectT >= 1f) Destroy(gameObject);
                return;
            }
            // a pile nobody picked up disappears with the server timer (15 s): free the object, not just hide it
            if (!Pending && Time.time >= expiresAt + 0.25f) { Destroy(gameObject); return; }
            age += dt;
            // fly out in an arc, land and bounce twice (decaying), then rest spinning and bobbing
            Vector3 rest = new Vector3(Ground.x + 0.6f, Ground.y + 0.3f, 0.2f);
            bool high = RarityTier >= LootRarity.High;
            // normal: out in an arc, two bounces (0.32 m, 0.12 m). High rarity: a higher arc and three bounces (0.75 m, 0.34 m, 0.12 m) so it reads from across the screen
            float arc = high ? 2.3f : 1.4f, bounceEnd = high ? 1.46f : 1.05f;
            if (age < 0.5f)
            {
                float k = age / 0.5f;
                transform.position = Vector3.Lerp(from, rest, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
            }
            else if (age < bounceEnd)
            {
                float b = age - 0.5f, h;
                if (high) h = b < 0.46f ? Mathf.Sin(b / 0.46f * Mathf.PI) * 0.75f : b < 0.76f ? Mathf.Sin((b - 0.46f) / 0.30f * Mathf.PI) * 0.34f : Mathf.Sin((b - 0.76f) / 0.20f * Mathf.PI) * 0.12f;
                else h = b < 0.32f ? Mathf.Sin(b / 0.32f * Mathf.PI) * 0.32f : Mathf.Sin((b - 0.32f) / 0.23f * Mathf.PI) * 0.12f;
                transform.position = rest + Vector3.up * Mathf.Max(0f, h);
                if (!Landed) { Landed = true; if (!chimed) { chimed = true; LootRarity.PlayDrop(RarityTier); } }   // the chime rings on the first touchdown
            }
            else transform.position = rest + Vector3.up * (Mathf.Sin(age * 3f) * 0.08f);
            transform.rotation = Quaternion.Euler(0f, age * 140f, 0f);
            // sparkles: twinkle around the pile, always facing the camera
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (!sparkles[i]) continue;
                float tw = Mathf.Max(0f, Mathf.Sin(age * 5f + i * 2.1f));
                sparkles[i].localScale = sparkleBase[i] * (0.3f + tw);
                sparkles[i].rotation = Quaternion.Euler(0f, 0f, age * 90f + i * 40f);
            }
            if (Time.time > expiresAt - 4f) // blink before it expires
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = Mathf.Repeat(Time.time * 6f, 1f) > 0.3f;
        }
    }
}
