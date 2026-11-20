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
        public string ItemId { get; private set; }
        /// <summary>The 2D sprite this drop shows (coin / pile / bag for Gold, the item's own icon for an item).</summary>
        public Sprite ShownSprite => icon ? icon.sprite : null;
        public string Rarity { get; private set; }

        private float age, expiresAt, collectT = -1f;
        private Vector3 from;
        private GameObject lightColumn;
        private SpriteRenderer lightRenderer;
        private static Sprite whiteSquare;
        private Transform target;
        private float iconBase = 1f;
        [SerializeField] private SpriteRenderer icon;      // the drop's own 2D sprite (coin / the item's worldSprite or icon)
        [SerializeField] private Sprite goldFallback;       // the project's existing coin sprite, used until coin sprites are set in ItemVisuals
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
            ItemId = d.itemId;
            Rarity = string.IsNullOrEmpty(d.itemRarity) ? "white" : d.itemRarity;
            if (IsItem && (Rarity == "purple" || Rarity == "orange" || Rarity == "red")) CreateRareColumn();
            ShowIcon(d);
        }

        /// <summary>Gold shows coins (small / pile / bag by amount); an item shows exactly its own icon (never a shared box).</summary>
        private void ShowIcon(FarmDropState d)
        {
            if (!icon) icon = GetComponentInChildren<SpriteRenderer>(true);
            if (!icon) return;
            // the owner's per-item worldSprite / icon (Resources/ItemVisuals.asset); until it is filled: the shared icon of the kind (item_junk) / the HUD coin
            Sprite s;
            if (IsItem) s = ItemIconBook.WorldSprite(d.itemId, "item_junk");
            else { s = ItemIconBook.Coins(d.amount); if (!s) s = goldFallback; }
            icon.sprite = s;
            icon.enabled = s;
            if (s) iconBase = 0.9f / Mathf.Max(0.01f, Mathf.Max(s.bounds.size.x, s.bounds.size.y));   // whatever picture is assigned reads as ~0.9 m on the ground
            icon.sortingOrder = 8;
        }

        private void CreateRareColumn()
        {
            if (!whiteSquare)
            {
                // A hard-edged 2D column, not a blurred 3D beam. Shared 1x1 sprite.
                Texture2D texture = Texture2D.whiteTexture;
                whiteSquare = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 1f);
            }
            lightColumn = new GameObject("Rare_Item_Column");
            lightColumn.transform.SetParent(transform.parent, false); // item spins; the beam stays upright
            lightRenderer = lightColumn.AddComponent<SpriteRenderer>();
            lightRenderer.sprite = whiteSquare;
            lightRenderer.sortingOrder = 7;
            lightRenderer.color = Rarity == "red" ? new Color(1f, 0.19f, 0.31f, 0.42f) :
                Rarity == "orange" ? new Color(1f, 0.58f, 0.16f, 0.42f) :
                new Color(0.66f, 0.31f, 1f, 0.4f);
            lightColumn.transform.localScale = new Vector3(0.25f, 2.8f, 1f);
            if (Camera.main && FarmField.CoinChime())
                AudioSource.PlayClipAtPoint(FarmField.CoinChime(), Camera.main.transform.position, 0.23f);
        }

        private void OnDestroy() { if (lightColumn) Destroy(lightColumn); }

        public bool Expired => Time.time >= expiresAt;

        /// <summary>Server confirmed: fly into the hero, then vanish.</summary>
        public void Collect(Transform hero)
        {
            target = hero;
            collectT = 0f;
            if (lightColumn) lightColumn.SetActive(false);
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
            if (lightColumn)
            {
                lightColumn.transform.position = new Vector3(Ground.x + 0.6f, Ground.y + 1.68f, 0.1f);
                var col = lightRenderer.color;
                col.a = (Time.time > expiresAt - 4f ? 0.18f : 0.4f) *
                    (0.85f + 0.15f * Mathf.Sin(Time.time * 8f));
                lightRenderer.color = col;
            }
            // fly out in an arc, land and bounce twice (decaying), then rest spinning and bobbing
            Vector3 rest = new Vector3(Ground.x + 0.6f, Ground.y + 0.3f, 0.2f);
            if (age < 0.5f)
            {
                float k = age / 0.5f;
                transform.position = Vector3.Lerp(from, rest, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.4f;
            }
            else if (age < 1.05f)
            {
                float b = age - 0.5f;                                   // two bounces: 0.32 m then 0.12 m
                float h = b < 0.32f ? Mathf.Sin(b / 0.32f * Mathf.PI) * 0.32f : Mathf.Sin((b - 0.32f) / 0.23f * Mathf.PI) * 0.12f;
                transform.position = rest + Vector3.up * Mathf.Max(0f, h);
                Landed = true;
            }
            else transform.position = rest + Vector3.up * (Mathf.Sin(age * 3f) * 0.08f);
            // 2D: stays upright; a quick tilt while it flies out, then a little squash when it lands
            float tilt = age < 0.5f ? Mathf.Sin(age * 28f) * 14f : 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, tilt);
            if (icon) icon.transform.localScale = Vector3.one * iconBase * (age > 0.5f && age < 0.62f ? 1.18f : 1f);
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
