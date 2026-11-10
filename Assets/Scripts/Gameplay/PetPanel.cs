using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Thú Cưng" window (P or the pet button): Pet thường (Gold) / Pet VIP (Kim Cương) tabs, one card per pet with its
    /// portrait, description, price, and — once owned — the satiety bar (minutes left of 60, "Đói" at 0) and the actions
    /// Triệu hồi / Thu hồi / Cho ăn. Buying asks for confirmation first. Everything is a server request.
    /// </summary>
    public sealed class PetPanel : MonoBehaviour
    {
        [SerializeField] private Sprite panelSprite, cellSprite, buttonSprite, coinSprite, diamondSprite;
        [SerializeField] private Font font;
        [SerializeField] private InventoryPanel bag;

        public bool IsOpen => root && root.activeSelf;
        public bool Vip { get; private set; }
        public string LastMessage { get; private set; } = "";
        public bool ConfirmOpen => confirm && confirm.activeSelf;
        public bool Busy { get; private set; }

        private GameObject root, confirm;
        private Text goldLabel, msgLabel, confirmText;
        private readonly Button[] tabs = new Button[2];
        private sealed class Card { public GameObject Go; public Image Portrait; public Text Name, Desc, Price, Sat; public Image SatFill; public Button Buy, Summon, Feed; public Text SummonLabel; }
        private readonly Card[] cards = new Card[2];
        private string pendingBuy;

        private PetDirector Director => PetDirector.Current;

        private T Make<T>(Transform p, string name, Vector2 anchor, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go.AddComponent<T>();
        }
        private Text Txt(Transform p, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align, bool bold = false)
        {
            Text t = Make<Text>(p, name, anchor, pos, box);
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.supportRichText = true; t.raycastTarget = false;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }
        private Button Btn(Transform p, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color tint, Action click)
        {
            Image img = Make<Image>(p, name, anchor, pos, size);
            img.sprite = buttonSprite; img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple; img.color = tint;
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(() => click());
            Txt(img.transform, "Label", label, 22, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, true);
            return b;
        }

        private void Start()
        {
            Build();
            if (Director) Director.Changed += Redraw;
        }

        private void Update()
        {
            var w = AdventureWorldSession.Current;
            var input = w && w.Player ? w.Player.GetComponent<PlayerInput2D>() : null;
            if (input && input.ConsumePet()) Toggle();
        }

        private void Build()
        {
            if (root) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image dim = Make<Image>(transform, "PetRoot", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var drt = (RectTransform)root.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(0f, 0f, 0f, 0.5f);
            Image card = Make<Image>(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 800f));
            card.sprite = panelSprite; card.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple; card.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.97f);
            Transform c = card.transform;
            Color gold = new Color(1f, 0.85f, 0.45f), cream = new Color(1f, 0.96f, 0.88f);
            Txt(c, "Title", "Thú Cưng", 40, gold, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(600f, 56f), TextAnchor.MiddleCenter, true);
            Btn(c, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(-130f, -64f), new Vector2(150f, 62f), new Color(0.55f, 0.3f, 0.3f), Close);
            goldLabel = Txt(c, "Wallet", "", 24, gold, new Vector2(0f, 1f), new Vector2(230f, -64f), new Vector2(360f, 40f), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < 2; i++)
            {
                bool vip = i == 1;
                tabs[i] = Btn(c, "Tab" + i, vip ? "Pet VIP (Kim Cương)" : "Pet thường (Vàng)", new Vector2(0.5f, 1f), new Vector2(-170f + i * 340f, -168f), new Vector2(320f, 62f), new Color(0.3f, 0.36f, 0.55f), () => { Vip = vip; Redraw(); });
            }
            for (int i = 0; i < 2; i++)
            {
                var k = new Card();
                Image bg = Make<Image>(c, "PetCard" + i, new Vector2(0.5f, 0.5f), new Vector2(-290f + i * 580f, -60f), new Vector2(540f, 470f));
                bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; bg.color = new Color(0.55f, 0.6f, 0.78f, 0.95f);
                k.Go = bg.gameObject;
                Transform t = bg.transform;
                k.Portrait = Make<Image>(t, "Portrait", new Vector2(0f, 1f), new Vector2(130f, -130f), new Vector2(210f, 210f)); k.Portrait.preserveAspect = true; k.Portrait.raycastTarget = false;
                k.Name = Txt(t, "Name", "", 28, gold, new Vector2(0f, 1f), new Vector2(380f, -50f), new Vector2(300f, 40f), TextAnchor.MiddleLeft, true);
                k.Desc = Txt(t, "Desc", "", 19, cream, new Vector2(0f, 1f), new Vector2(380f, -130f), new Vector2(300f, 110f), TextAnchor.UpperLeft);
                k.Price = Txt(t, "Price", "", 24, gold, new Vector2(0f, 1f), new Vector2(380f, -210f), new Vector2(300f, 36f), TextAnchor.MiddleLeft, true);
                Image track = Make<Image>(t, "SatTrack", new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(480f, 18f)); track.color = new Color(0f, 0f, 0f, 0.45f); track.raycastTarget = false;
                k.SatFill = Make<Image>(track.transform, "SatFill", new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(480f, 18f));
                k.SatFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                k.SatFill.type = Image.Type.Filled; k.SatFill.fillMethod = Image.FillMethod.Horizontal; k.SatFill.color = new Color(0.55f, 0.95f, 0.5f); k.SatFill.raycastTarget = false;
                k.Sat = Txt(t, "Satiety", "", 20, cream, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(480f, 30f), TextAnchor.MiddleCenter);
                int idx = i;
                k.Buy = Btn(t, "Buy", "Mua", new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(300f, 66f), new Color(0.82f, 0.6f, 0.18f), () => AskBuy(idx));
                k.Summon = Btn(t, "Summon", "Triệu hồi", new Vector2(0.5f, 0f), new Vector2(-125f, 80f), new Vector2(230f, 66f), new Color(0.26f, 0.56f, 0.62f), () => ToggleSummon(idx));
                k.SummonLabel = k.Summon.GetComponentInChildren<Text>();
                k.Feed = Btn(t, "Feed", "Cho ăn", new Vector2(0.5f, 0f), new Vector2(125f, 80f), new Vector2(230f, 66f), new Color(0.25f, 0.6f, 0.4f), () => Feed(idx));
                cards[i] = k;
            }
            msgLabel = Txt(c, "Message", "", 22, new Color(1f, 0.78f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1100f, 34f), TextAnchor.MiddleCenter);
            Image cd = Make<Image>(root.transform, "ConfirmDim", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            confirm = cd.gameObject;
            var crt = (RectTransform)confirm.transform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
            cd.color = new Color(0f, 0f, 0f, 0.55f);
            Image cc = Make<Image>(confirm.transform, "ConfirmCard", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 360f));
            cc.sprite = panelSprite; cc.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple; cc.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.98f);
            confirmText = Txt(cc.transform, "ConfirmText", "", 26, cream, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(620f, 130f), TextAnchor.MiddleCenter);
            Btn(cc.transform, "Yes", "Xác nhận", new Vector2(0.5f, 0.5f), new Vector2(-140f, -100f), new Vector2(240f, 66f), new Color(0.25f, 0.6f, 0.4f), ConfirmBuy);
            Btn(cc.transform, "No", "Huỷ", new Vector2(0.5f, 0.5f), new Vector2(140f, -100f), new Vector2(240f, 66f), new Color(0.45f, 0.45f, 0.52f), () => confirm.SetActive(false));
            confirm.SetActive(false);
            root.SetActive(false);
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            Build();
            root.SetActive(true); transform.SetAsLastSibling();
            SetInput(false);
            if (Director) Director.Refresh(Redraw);
            Redraw();
        }
        public void Close() { if (root) root.SetActive(false); SetInput(true); }
        private static void SetInput(bool on) { var w = AdventureWorldSession.Current; var pi = w && w.Player ? w.Player.GetComponent<PlayerInput2D>() : null; if (pi) pi.enabled = on; }
        private void Message(string m) { LastMessage = m ?? ""; if (msgLabel) msgLabel.text = LastMessage; }
        public void SetVip(bool v) { Vip = v; Redraw(); }

        private PetDefState[] Shown() => Director?.State?.catalog?.Where(p => (p.tier == "vip") == Vip).Take(2).ToArray() ?? new PetDefState[0];
        private OwnedPetState Owned(string id) => Director?.State?.owned?.FirstOrDefault(o => o.petId == id);

        private void Redraw()
        {
            if (!root) return;
            PetsState s = Director ? Director.State : null;
            if (goldLabel) goldLabel.text = s == null ? "" : $"{s.gold:N0} Vàng · {s.diamonds:N0} Kim Cương";
            for (int i = 0; i < 2; i++) tabs[i].GetComponent<Image>().color = (i == 1) == Vip ? new Color(0.82f, 0.6f, 0.18f) : new Color(0.3f, 0.36f, 0.55f);
            PetDefState[] list = Shown();
            for (int i = 0; i < cards.Length; i++)
            {
                Card k = cards[i];
                bool on = i < list.Length;
                k.Go.SetActive(on);
                if (!on) continue;
                PetDefState d = list[i];
                OwnedPetState o = Owned(d.id);
                k.Portrait.sprite = Director ? Director.Portrait(d.id) : null;
                k.Name.text = d.name;
                k.Desc.text = d.desc;
                k.Price.text = o != null ? "<color=#a8f0a0>Đã sở hữu</color>" : $"{d.price:N0} {(d.currency == "gold" ? "Vàng" : "Kim Cương")}";
                k.Buy.gameObject.SetActive(o == null);
                k.Summon.gameObject.SetActive(o != null);
                k.Feed.gameObject.SetActive(o != null);
                if (k.SummonLabel) k.SummonLabel.text = o != null && o.summoned ? "Thu hồi" : "Triệu hồi";
                long max = s != null && s.maxSatietyMs > 0 ? s.maxSatietyMs : 3_600_000;
                k.SatFill.transform.parent.gameObject.SetActive(o != null);
                k.Sat.text = o == null ? "" : o.hungry ? "<color=#9cc6ff>Đói — cho ăn để tiếp tục nhặt Vàng</color>" : $"No: {Mathf.CeilToInt(o.satietyMs / 60000f)}/{max / 60000} phút" + (o.summoned ? " · đang đi theo" : "");
                if (o != null) { k.SatFill.fillAmount = Mathf.Clamp01(o.satietyMs / (float)max); k.SatFill.color = o.hungry ? new Color(0.6f, 0.7f, 0.9f) : new Color(0.55f, 0.95f, 0.5f); }
            }
        }

        public void AskBuy(int i)
        {
            PetDefState[] list = Shown();
            if (i >= list.Length || Busy) return;
            pendingBuy = list[i].id;
            confirmText.text = $"Mua <b>{list[i].name}</b>\nvới giá <b>{list[i].price:N0} {(list[i].currency == "gold" ? "Vàng" : "Kim Cương")}</b>?";
            confirm.SetActive(true); confirm.transform.SetAsLastSibling();
        }

        public void ConfirmBuy()
        {
            confirm.SetActive(false);
            if (pendingBuy == null || !Director) return;
            Busy = true;
            Director.Buy(pendingBuy, (ok, err) => { Busy = false; Message(ok ? "Đã mua thú cưng! Bấm Triệu hồi để nó đi theo bạn." : err); Redraw(); });
        }

        public void ToggleSummon(int i)
        {
            PetDefState[] list = Shown();
            if (i >= list.Length || !Director || Busy) return;
            OwnedPetState o = Owned(list[i].id);
            if (o == null) return;
            Busy = true;
            Action<bool, string> done = (ok, err) => { Busy = false; Message(ok ? (o.summoned ? "Đã thu hồi." : $"{list[i].name} đang đi theo bạn.") : err); Redraw(); };
            if (o.summoned) Director.Dismiss(done); else Director.Summon(o.petId, done);
        }

        public void Feed(int i)
        {
            PetDefState[] list = Shown();
            if (i >= list.Length || !Director || Busy) return;
            Busy = true;
            Director.Feed(list[i].id, bag, (ok, err) => { Busy = false; Message(ok ? "Măm măm! Thú cưng đã no hơn." : err); Redraw(); });
        }
    }
}
