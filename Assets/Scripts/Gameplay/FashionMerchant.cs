using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// The town's fashion merchant at the market stall. When the hero walks up, a "Nói chuyện" prompt appears
    /// (E / ▲ on keyboard, a tap button on touch). Talking turns the merchant to the hero, waves, greets the
    /// character BY NAME and offers: Mua sắm (opens the Gold shop), Xem tủ đồ (opens the wardrobe tab),
    /// Trò chuyện (small talk) and Đóng. A confirmed purchase gets a bow of thanks.
    /// </summary>
    public sealed class FashionMerchant : MonoBehaviour
    {
        [SerializeField] private NpcActor actor;
        [SerializeField] private FashionShopUI shop;
        [SerializeField] private string displayName = "Chủ Tiệm Lụa";
        [SerializeField, Min(0.5f)] private float talkRange = 2.4f;
        [SerializeField] private GameObject worldPrompt;

        public bool PlayerNear { get; private set; }
        public bool MenuOpen => menu && menu.activeSelf;
        public string LastLine => line ? line.text : "";
        public string DisplayName => displayName;
        public Button TalkButton => talkButton;
        public static FashionMerchant Active { get; private set; }
        private void OnEnable() => Active = this;
        private void OnDestroy() { if (Active == this) Active = null; }

        private GameObject menu;
        private Text speaker, line;
        private Button talkButton;
        private PlayerInput2D input;
        private int chat;
        private bool busy;

        private static readonly string[] SmallTalk =
        {
            "Vải ở tiệm ta dệt từ tơ sen ven suối Chuông Gió, mặc nhẹ như mây.",
            "Áo choàng gió bay theo bước chạy của con — thử chạy một vòng trong phòng thử đồ xem!",
            "Mặc thử thì cứ thoải mái, ta không lấy đồng nào cả. Ưng món nào thì hẵng mua.",
            "Đồ mua rồi sẽ nằm trong tủ đồ của con mãi mãi, đăng nhập lại vẫn còn nguyên.",
        };

        private void Start()
        {
            if (!actor) actor = GetComponent<NpcActor>();
            if (!shop) shop = FindAnyObjectByType<FashionShopUI>();
            if (!shop) { enabled = false; return; }
            BuildUi();
            shop.Purchased += _ => { if (actor) StartCoroutine(actor.Gesture("Bow", 1.6f)); };
            shop.Closed += () => { if (actor) actor.Play("Idle"); };
            if (worldPrompt) worldPrompt.SetActive(false);
        }

        private void BuildUi()
        {
            Transform canvas = shop.CanvasRoot;
            Color cream = new Color(1f, 0.97f, 0.9f), gold = new Color(1f, 0.84f, 0.5f);
            talkButton = shop.Btn(canvas, "TalkToMerchant", "Nói chuyện", new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(260f, 74f), new Color(0.26f, 0.56f, 0.62f));
            talkButton.onClick.AddListener(Talk);
            talkButton.gameObject.SetActive(false);

            menu = shop.Panel(canvas, "MerchantMenu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1240f, 330f),
                new Color(0.08f, 0.1f, 0.18f, 0.96f), null);
            var img = menu.GetComponent<Image>();
            // same parchment scroll + ribbon as every NPC conversation
            bool scroll = shop.Skin(img, "parchment");
            Sprite frame = shop.FrameSprite;
            if (!scroll && frame) { img.sprite = frame; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 3.2f; }
            var banner = shop.Panel(menu.transform, "SpeakerBanner", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, 6f), new Vector2(380f, 72f),
                new Color(0.42f, 0.24f, 0.4f, 1f), null);
            if (!shop.Skin(banner.GetComponent<Image>(), "ribbon", 0.8f)) shop.Rim(banner.transform);
            speaker = shop.Label(banner.transform, "MerchantSpeaker", displayName, 28, scroll ? cream : gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256f, 50f), true);
            speaker.gameObject.AddComponent<FitOneLine>().Configure(28, 16);
            line = shop.Label(menu.transform, "MerchantLine", "", 26, scroll ? new Color(0.24f, 0.14f, 0.05f) : cream, new Vector2(0.5f, 0.5f), new Vector2(-10f, 40f), new Vector2(940f, 130f), false);
            if (scroll) line.GetComponent<Shadow>().effectColor = new Color(1f, 0.95f, 0.8f, 0.4f);
            line.alignment = TextAnchor.UpperLeft;
            line.supportRichText = true;
            line.resizeTextForBestFit = true;
            line.resizeTextMinSize = 20;
            line.verticalOverflow = VerticalWrapMode.Truncate;
            (string label, Color c, UnityEngine.Events.UnityAction act)[] opts =
            {
                ("Mua sắm", new Color(0.78f, 0.55f, 0.18f), () => OpenShop(FashionShopUI.Tab.Gold)),
                ("Xem tủ đồ", new Color(0.26f, 0.56f, 0.62f), () => OpenShop(FashionShopUI.Tab.Wardrobe)),
                ("Trò chuyện", new Color(0.45f, 0.3f, 0.62f), ChatLine),
                ("Đóng", new Color(0.36f, 0.36f, 0.42f), CloseMenu),
            };
            for (int i = 0; i < opts.Length; i++)
            {
                var b = shop.Btn(menu.transform, "Option_" + i, opts[i].label, new Vector2(0.5f, 0f), new Vector2(-375f + i * 250f, 74f), new Vector2(230f, 64f), opts[i].c);
                b.onClick.AddListener(opts[i].act);
            }
            menu.SetActive(false);
        }

        private void Update()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            PlayerMotor2D player = world ? world.Player : null;
            if (!player) return;
            if (!input) input = player.GetComponent<PlayerInput2D>();
            Vector2 d = player.transform.position - transform.position;
            bool near = Mathf.Abs(d.x) < talkRange && Mathf.Abs(d.y) < 2.2f && !shop.IsOpen && !MenuOpen && !(world.Traveling || world.Leaving);
            if (near != PlayerNear)
            {
                PlayerNear = near;
                if (worldPrompt) worldPrompt.SetActive(near);
                if (talkButton) talkButton.gameObject.SetActive(near);
                if (near && actor && !busy) actor.Face(player.transform.position.x);
                else if (!near && actor && !MenuOpen && !shop.IsOpen) actor.FaceFront();
            }
            if (near && input && input.enabled && input.ConsumeInteract()) Talk();
#if ENABLE_INPUT_SYSTEM
            if (MenuOpen && UnityEngine.InputSystem.Keyboard.current is { } k && k.escapeKey.wasPressedThisFrame) CloseMenu();
#endif
        }

        /// <summary>Starts the conversation (keyboard E, gamepad, or the touch button).</summary>
        public void Talk()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (busy || MenuOpen || shop.IsOpen || !world || world.Character == null) return;
            QuestBook.Event("talk:tailor");
            StartCoroutine(Greet(world));
        }

        private IEnumerator Greet(AdventureWorldSession world)
        {
            busy = true;
            SetInput(false);
            if (talkButton) talkButton.gameObject.SetActive(false);
            if (worldPrompt) worldPrompt.SetActive(false);
            if (actor)
            {
                actor.Face(world.Player.transform.position.x);
                actor.Play("Wave", 0.12f);
            }
            line.text = $"Chào <b>{world.Character.characterName}</b>! Ghé Tiệm Thời Trang Chuông Gió xem đồ mới nhé.\n" +
                        "Con cứ mặc thử thoải mái, ưng món nào ta gói ngay — trả bằng Vàng.";
            menu.SetActive(true);
            yield return new WaitForSeconds(1.6f);
            if (actor && MenuOpen) actor.Play("Talk");
            busy = false;
        }

        public void ChatLine()
        {
            line.text = SmallTalk[chat % SmallTalk.Length];
            chat++;
            if (actor) StartCoroutine(actor.Gesture("Talk", 1.2f, "Talk"));
        }

        public void OpenShop(FashionShopUI.Tab tab)
        {
            menu.SetActive(false);
            shop.Open(tab, SetInput);
        }

        public void CloseMenu()
        {
            if (!MenuOpen) return;
            menu.SetActive(false);
            SetInput(true);
            if (actor) StartCoroutine(actor.Gesture("Bow", 1.4f));
        }

        private void SetInput(bool on)
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            PlayerInput2D pi = world && world.Player ? world.Player.GetComponent<PlayerInput2D>() : null;
            if (pi) pi.enabled = on;
            if (on && actor && actor.Animator) actor.Play("Idle");
        }
    }
}
