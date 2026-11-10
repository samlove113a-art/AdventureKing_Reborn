using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Mộc Linh's shop window: Dược Phẩm / Vé Dịch Chuyển tabs from the server, item rows with icon + price, a quantity
    /// picker (−/+ and 1 / 10 / 99, 1–99 per purchase), the total, and a confirmation before anything is bought. The
    /// server checks bag space before taking Gold and never charges a retried purchase twice.
    /// </summary>
    public sealed class HealerShopUI : MonoBehaviour
    {
        [Serializable] public struct IconEntry { public string name; public Sprite sprite; }
        [SerializeField] private IconEntry[] icons = new IconEntry[0];
        [SerializeField] private Sprite panelSprite, cellSprite, buttonSprite, coinSprite;
        [SerializeField] private Font font;
        [SerializeField] private string shopId = "healer";

        public event Action Closed;
        public bool IsOpen => root && root.activeSelf;
        public NpcShop Shop { get; private set; }
        public int TabIndex { get; private set; }
        public NpcShopItem Selected { get; private set; }
        public int Qty { get; private set; } = 1;
        public bool ConfirmOpen => confirm && confirm.activeSelf;
        public bool Busy { get; private set; }
        public string LastMessage { get; private set; } = "";
        public long Total => Selected == null ? 0 : Selected.priceGold * Qty;

        private GameObject root, confirm;
        private Text greet, goldLabel, nameLabel, descLabel, qtyLabel, totalLabel, msgLabel, confirmText;
        private Image detailIcon;
        private readonly Button[] tabButtons = new Button[3];
        private readonly (Image bg, Image icon, Text name, Text price)[] rows = new (Image, Image, Text, Text)[6];
        private Button buyButton;

        private Sprite Icon(string name) => icons.FirstOrDefault(i => i.name == name).sprite;

        private T Make<T>(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<T>();
        }

        private Text Txt(Transform p, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align, bool bold = false)
        {
            Text t = Make<Text>(p, name, anchor, pos, box);
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.supportRichText = true;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.raycastTarget = false;
            return t;
        }

        private Button Btn(Transform p, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color tint, Action click)
        {
            Image img = Make<Image>(p, name, anchor, pos, size);
            img.sprite = buttonSprite; img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple; img.color = tint;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => click());
            Txt(img.transform, "Label", label, 24, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, true);
            return b;
        }

        private void Build()
        {
            if (root) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image dim = Make<Image>(transform, "HealerShopRoot", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var drt = (RectTransform)root.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(0f, 0f, 0f, 0.5f);
            Image card = Make<Image>(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 780f));
            card.sprite = panelSprite; card.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple;
            card.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.97f);
            Transform c = card.transform;
            Color gold = new Color(1f, 0.85f, 0.45f), cream = new Color(1f, 0.96f, 0.88f);
            Txt(c, "Title", "Dược Sư Mộc Linh", 38, gold, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(700f, 54f), TextAnchor.MiddleCenter, true);
            greet = Txt(c, "Greeting", "", 22, cream, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1000f, 34f), TextAnchor.MiddleCenter);
            Btn(c, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(-130f, -64f), new Vector2(150f, 62f), new Color(0.55f, 0.3f, 0.3f), Close);
            Image coin = Make<Image>(c, "Coin", new Vector2(0f, 1f), new Vector2(92f, -64f), new Vector2(40f, 40f));
            coin.sprite = coinSprite; coin.preserveAspect = true;
            goldLabel = Txt(c, "Gold", "", 26, gold, new Vector2(0f, 1f), new Vector2(210f, -64f), new Vector2(200f, 40f), TextAnchor.MiddleLeft, true);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                string[] names = { "Dược Phẩm", "Vé Dịch Chuyển", "Thức Ăn Pet" };
                tabButtons[i] = Btn(c, "Tab" + i, names[i], new Vector2(0f, 1f), new Vector2(160f + i * 230f, -200f), new Vector2(220f, 62f), new Color(0.3f, 0.36f, 0.55f), () => SetTab(idx));
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int idx = i;
                Image bg = Make<Image>(c, "Row" + i, new Vector2(0f, 1f), new Vector2(330f, -280f - i * 78f), new Vector2(540f, 70f));
                bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; bg.color = new Color(0.55f, 0.6f, 0.78f, 0.9f);
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectRow(idx));
                Image ic = Make<Image>(bg.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(58f, 58f));
                ic.preserveAspect = true; ic.raycastTarget = false;
                Text nm = Txt(bg.transform, "Name", "", 22, cream, new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(300f, 34f), TextAnchor.MiddleLeft, true);
                Text pr = Txt(bg.transform, "Price", "", 22, gold, new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(140f, 34f), TextAnchor.MiddleRight, true);
                rows[i] = (bg, ic, nm, pr);
            }
            // detail + quantity
            detailIcon = Make<Image>(c, "DetailIcon", new Vector2(1f, 1f), new Vector2(-470f, -280f), new Vector2(110f, 110f));
            detailIcon.preserveAspect = true;
            nameLabel = Txt(c, "ItemName", "", 28, gold, new Vector2(1f, 1f), new Vector2(-250f, -250f), new Vector2(380f, 40f), TextAnchor.MiddleLeft, true);
            descLabel = Txt(c, "ItemDesc", "", 21, cream, new Vector2(1f, 1f), new Vector2(-250f, -312f), new Vector2(380f, 80f), TextAnchor.UpperLeft);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            Btn(c, "Minus", "−", new Vector2(1f, 1f), new Vector2(-470f, -420f), new Vector2(70f, 64f), new Color(0.3f, 0.36f, 0.55f), () => SetQty(Qty - 1));
            qtyLabel = Txt(c, "Qty", "1", 30, Color.white, new Vector2(1f, 1f), new Vector2(-370f, -420f), new Vector2(110f, 64f), TextAnchor.MiddleCenter, true);
            Btn(c, "Plus", "+", new Vector2(1f, 1f), new Vector2(-270f, -420f), new Vector2(70f, 64f), new Color(0.3f, 0.36f, 0.55f), () => SetQty(Qty + 1));
            Btn(c, "Q1", "1", new Vector2(1f, 1f), new Vector2(-470f, -500f), new Vector2(110f, 60f), new Color(0.36f, 0.42f, 0.6f), () => SetQty(1));
            Btn(c, "Q10", "10", new Vector2(1f, 1f), new Vector2(-345f, -500f), new Vector2(110f, 60f), new Color(0.36f, 0.42f, 0.6f), () => SetQty(10));
            Btn(c, "Q99", "99", new Vector2(1f, 1f), new Vector2(-220f, -500f), new Vector2(110f, 60f), new Color(0.36f, 0.42f, 0.6f), () => SetQty(99));
            totalLabel = Txt(c, "Total", "", 26, gold, new Vector2(1f, 1f), new Vector2(-345f, -575f), new Vector2(380f, 40f), TextAnchor.MiddleCenter, true);
            buyButton = Btn(c, "Buy", "Mua", new Vector2(1f, 1f), new Vector2(-345f, -650f), new Vector2(300f, 66f), new Color(0.82f, 0.6f, 0.18f), AskBuy);
            msgLabel = Txt(c, "Message", "", 22, new Color(1f, 0.78f, 0.6f), new Vector2(0f, 0f), new Vector2(330f, 60f), new Vector2(560f, 34f), TextAnchor.MiddleLeft);

            // confirmation overlay
            Image cd = Make<Image>(root.transform, "ConfirmDim", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            confirm = cd.gameObject;
            var crt = (RectTransform)confirm.transform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
            cd.color = new Color(0f, 0f, 0f, 0.55f);
            Image cc = Make<Image>(confirm.transform, "ConfirmCard", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 360f));
            cc.sprite = panelSprite; cc.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple; cc.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.98f);
            confirmText = Txt(cc.transform, "ConfirmText", "", 26, cream, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(620f, 130f), TextAnchor.MiddleCenter);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Btn(cc.transform, "Yes", "Xác nhận", new Vector2(0.5f, 0.5f), new Vector2(-140f, -100f), new Vector2(240f, 66f), new Color(0.25f, 0.6f, 0.4f), ConfirmYes);
            Btn(cc.transform, "No", "Huỷ", new Vector2(0.5f, 0.5f), new Vector2(140f, -100f), new Vector2(240f, 66f), new Color(0.45f, 0.45f, 0.52f), () => confirm.SetActive(false));
            confirm.SetActive(false);
            root.SetActive(false);
        }

        public void Open(string greeting)
        {
            Build();
            root.SetActive(true);
            transform.SetAsLastSibling();
            greet.text = greeting;
            SetInput(false);
            Message("");
            if (Shop == null)
            {
                Busy = true;
                AdventureServerConnection.Instance.GetShop(shopId, (ok, s, err) =>
                {
                    Busy = false;
                    if (!ok || s == null) { Message(err); return; }
                    Shop = s;
                    SetTab(0);
                });
            }
            else SetTab(TabIndex);
        }

        public void Close()
        {
            if (!root) return;
            root.SetActive(false);
            SetInput(true);
            Closed?.Invoke();
        }

        private static void SetInput(bool on)
        {
            var world = AdventureWorldSession.Current;
            var pi = world && world.Player ? world.Player.GetComponent<PlayerInput2D>() : null;
            if (pi) pi.enabled = on;
        }

        public void SetTab(int i)
        {
            if (Shop == null) return;
            TabIndex = Mathf.Clamp(i, 0, Shop.tabs.Length - 1);
            Selected = Shop.tabs[TabIndex].items.FirstOrDefault();
            Qty = 1;
            Redraw();
        }

        public void SelectRow(int i)
        {
            if (Shop == null) return;
            var items = Shop.tabs[TabIndex].items;
            if (i < items.Length) { Selected = items[i]; Redraw(); }
        }

        public void Select(string itemId)
        {
            if (Shop == null) return;
            for (int t = 0; t < Shop.tabs.Length; t++)
                if (Shop.tabs[t].items.FirstOrDefault(x => x.itemId == itemId) is { } it) { TabIndex = t; Selected = it; Redraw(); return; }
        }

        public void SetQty(int q) { Qty = Mathf.Clamp(q, 1, Shop != null ? Shop.maxPerPurchase : 99); Redraw(); }

        private void Message(string text) { LastMessage = text ?? ""; if (msgLabel) msgLabel.text = LastMessage; }

        private void Redraw()
        {
            var world = AdventureWorldSession.Current;
            if (goldLabel) goldLabel.text = world && world.Character != null ? world.Character.gold.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) : "";
            if (Shop == null) return;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                tabButtons[i].gameObject.SetActive(i < Shop.tabs.Length);
                if (i < Shop.tabs.Length)
                {
                    tabButtons[i].GetComponent<Image>().color = i == TabIndex ? new Color(0.82f, 0.6f, 0.18f) : new Color(0.3f, 0.36f, 0.55f);
                    tabButtons[i].GetComponentInChildren<Text>().text = Shop.tabs[i].name; // names come from the server
                }
            }
            var items = Shop.tabs[TabIndex].items;
            for (int i = 0; i < rows.Length; i++)
            {
                var (bg, ic, nm, pr) = rows[i];
                bool on = i < items.Length;
                bg.gameObject.SetActive(on);
                if (!on) continue;
                ic.sprite = Icon(items[i].icon); ic.enabled = ic.sprite;
                nm.text = items[i].name;
                pr.text = items[i].priceGold + " Vàng";
                bg.color = items[i] == Selected ? new Color(1f, 0.85f, 0.45f) : new Color(0.55f, 0.6f, 0.78f, 0.9f);
            }
            if (Selected != null)
            {
                detailIcon.sprite = Icon(Selected.icon); detailIcon.enabled = detailIcon.sprite;
                nameLabel.text = Selected.name;
                descLabel.text = Selected.desc + (Selected.kind == "ticket" && !string.IsNullOrEmpty(Selected.destName) ? $"\nĐích: {Selected.destName} (cấp {Selected.minLevel}+)" : "");
            }
            qtyLabel.text = Qty.ToString();
            totalLabel.text = $"Tổng: {Total:N0} Vàng";
            buyButton.interactable = Selected != null && !Busy;
        }

        public void AskBuy()
        {
            if (Selected == null || Busy) return;
            confirmText.text = $"Mua <b>{Qty} × {Selected.name}</b>\nvới giá <b>{Total:N0} Vàng</b>?";
            confirm.SetActive(true);
            confirm.transform.SetAsLastSibling();
        }

        public void ConfirmYes()
        {
            confirm.SetActive(false);
            var world = AdventureWorldSession.Current;
            if (Selected == null || Busy || !world || world.Character == null) return;
            Busy = true;
            NpcShopItem item = Selected;
            int qty = Qty;
            AdventureServerConnection.Instance.BuyFromShop(world.Character.characterId, shopId, item.itemId, qty, Guid.NewGuid().ToString("N"), (ok, r, err) =>
            {
                Busy = false;
                if (!ok || r == null) { Message(err); Redraw(); return; }
                world.Character.gold = r.gold; // HUD shows the server balance
                var bag = FindAnyObjectByType<InventoryPanel>();
                if (bag) bag.Refresh();
                Message($"Đã mua {r.qty} × {item.name} (−{r.totalGold:N0} Vàng).");
                Redraw();
            });
        }
    }
}
