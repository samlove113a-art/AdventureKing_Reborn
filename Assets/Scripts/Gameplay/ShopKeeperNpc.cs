using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// A shop NPC that sells ONE group of goods (Thú cưng và thức ăn, Trang sức ...): walk up, press E / Up, the shop window of its group opens.
    /// The body is the 2D sprite actor the NpcActor child swaps in (tinted so each keeper reads differently); the goods come from the server shop with the same id.
    /// </summary>
    public sealed class ShopKeeperNpc : MonoBehaviour
    {
        [SerializeField] private NpcActor actor;
        [SerializeField] private HealerShopUI shop;
        [SerializeField] private GameObject prompt;
        [SerializeField] private string greeting = "Chào {0}! Cần gì cứ chọn nhé.";
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Min(0.5f)] private float range = 2.4f;

        public bool PlayerNear { get; private set; }
        public int Greetings { get; private set; }
        public HealerShopUI Shop => shop;

        public void Bind(NpcActor a, HealerShopUI s, GameObject p, string hello, Color color) { actor = a; shop = s; prompt = p; greeting = hello; tint = color; }

        private void Start()
        {
            if (prompt) prompt.SetActive(false);
            if (!actor) actor = GetComponent<NpcActor>();
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.color = tint;   // same sprite family, its own colours
        }

        private void Update()
        {
            var world = AdventureWorldSession.Current;
            if (!world || !world.Player) return;
            Vector2 d = world.Player.transform.position - transform.position;
            bool near = Mathf.Abs(d.x) < range && Mathf.Abs(d.y) < 2.2f && !(shop && shop.IsOpen) && !(world.Traveling || world.Leaving);
            if (near != PlayerNear)
            {
                PlayerNear = near;
                if (prompt) prompt.SetActive(near);
                if (actor && near) actor.Face(world.Player.transform.position.x);
            }
            var input = world.Player.GetComponent<PlayerInput2D>();
            if (near && input && input.enabled && input.ConsumeInteract()) Talk();
        }

        public void Talk()
        {
            var world = AdventureWorldSession.Current;
            if (!world || world.Character == null || (shop && shop.IsOpen)) return;
            Greetings++;
            if (prompt) prompt.SetActive(false);
            if (actor) actor.Play("Wave", 0.1f);
            if (shop) shop.Open(string.Format(greeting, "<b>" + world.Character.characterName + "</b>"));
        }
    }
}
