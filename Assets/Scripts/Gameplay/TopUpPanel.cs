using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Nạp Kim Cương" by VietQR. Pick a package -> the server creates an order with a unique transfer code (AKR######) and the QR link; the picture is downloaded and shown with
    /// Copy buttons for the account number and the transfer text. The panel polls the order and closes itself with a message the moment the bank notification credits the Diamonds.
    /// The client never writes a balance: Diamonds are added only by the server (bank webhook).
    /// </summary>
    public sealed class TopUpPanel : MonoBehaviour
    {
        [SerializeField] private Sprite panelSprite, buttonSprite;
        [SerializeField] private Font font;

        private GameObject root, listView, orderView;
        private Text status, orderInfo, title;
        private RawImage qr;
        private Transform listContent;
        private TopUpOrderState order;
        private StoreCatalogState catalog;
        private Coroutine poll;

        public bool IsOpen => root && root.activeSelf;
        public TopUpOrderState Order => order;
        public StoreCatalogState Catalog => catalog;
        public string LastMessage { get; private set; } = "";
        public bool Completed { get; private set; }
        public bool QrShown => qr && qr.texture;

        private T Make<T>(Transform p, string n, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax) where T : Component
        {
            var go = new GameObject(n, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = omin; rt.offsetMax = omax;
            return typeof(T) == typeof(RectTransform) ? rt as T : go.AddComponent<T>();
        }

        private Text Label(Transform p, string n, string s, int size, Color c, Vector2 amin, Vector2 amax, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var t = Make<Text>(p, n, amin, amax, Vector2.zero, Vector2.zero);
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = size; t.color = c; t.alignment = a; t.text = s; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button Btn(Transform p, string n, string s, Vector2 amin, Vector2 amax, System.Action click, Color? tint = null)
        {
            var img = Make<Image>(p, n, amin, amax, Vector2.zero, Vector2.zero);
            img.sprite = buttonSprite; img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple; img.color = tint ?? new Color(0.35f, 0.4f, 0.6f);
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(() => click());
            Label(img.transform, "Text", s, 24, Color.white, Vector2.zero, Vector2.one);
            return b;
        }

        private void Build()
        {
            if (root) return;
            root = Make<Image>(transform, "TopUpRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var card = Make<Image>(root.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480f, -330f), new Vector2(480f, 330f));
            card.sprite = panelSprite; card.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple; card.color = panelSprite ? Color.white : new Color(0.08f, 0.1f, 0.2f, 0.97f);
            title = Label(card.transform, "Title", "NẠP KIM CƯƠNG", 38, new Color(1f, 0.85f, 0.45f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            ((RectTransform)title.transform).offsetMin = new Vector2(0f, -90f); ((RectTransform)title.transform).offsetMax = new Vector2(0f, -20f);
            Btn(card.transform, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(1f, 1f), Close, new Color(0.55f, 0.3f, 0.3f));
            var cr = (RectTransform)card.transform.Find("Close"); cr.anchoredPosition = new Vector2(-90f, -50f); cr.sizeDelta = new Vector2(140f, 56f);
            status = Label(card.transform, "Status", "", 22, new Color(1f, 0.78f, 0.6f), new Vector2(0f, 0f), new Vector2(1f, 0f));
            ((RectTransform)status.transform).offsetMin = new Vector2(30f, 12f); ((RectTransform)status.transform).offsetMax = new Vector2(-30f, 70f);

            listView = Make<RectTransform>(card.transform, "List", Vector2.zero, Vector2.one, new Vector2(40f, 80f), new Vector2(-40f, -100f)).gameObject;
            listContent = listView.transform;
            var vl = listView.AddComponent<VerticalLayoutGroup>(); vl.spacing = 10f; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            orderView = Make<RectTransform>(card.transform, "Order", Vector2.zero, Vector2.one, new Vector2(30f, 80f), new Vector2(-30f, -100f)).gameObject;
            qr = Make<RawImage>(orderView.transform, "Qr", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var qrt = qr.rectTransform; qrt.pivot = new Vector2(0f, 0.5f); qrt.anchoredPosition = new Vector2(10f, 0f); qrt.sizeDelta = new Vector2(400f, 400f);
            orderInfo = Label(orderView.transform, "Info", "", 24, new Color(1f, 0.96f, 0.88f), new Vector2(0f, 0f), new Vector2(1f, 1f), TextAnchor.UpperLeft);
            var irt = (RectTransform)orderInfo.transform; irt.offsetMin = new Vector2(440f, 170f); irt.offsetMax = new Vector2(0f, 0f);
            Btn(orderView.transform, "CopyAccount", "Copy Số Tài Khoản", new Vector2(0f, 0f), new Vector2(0f, 0f), () => Copy(order.accountNo, "Đã copy số tài khoản."));
            Btn(orderView.transform, "CopyContent", "Copy Nội Dung CK", new Vector2(0f, 0f), new Vector2(0f, 0f), () => Copy(order.code, "Đã copy nội dung chuyển khoản."));
            Btn(orderView.transform, "Back", "Chọn gói khác", new Vector2(0f, 0f), new Vector2(0f, 0f), ShowList, new Color(0.4f, 0.4f, 0.5f));
            string[] names = { "CopyAccount", "CopyContent", "Back" };
            for (int i = 0; i < names.Length; i++)
            {
                var r = (RectTransform)orderView.transform.Find(names[i]); r.pivot = new Vector2(0f, 0f); r.anchoredPosition = new Vector2(440f, 10f + i * 52f); r.sizeDelta = new Vector2(420f, 46f);
            }
            root.SetActive(false);
        }

        public void Open()
        {
            Build();
            Completed = false;
            root.SetActive(true); transform.SetAsLastSibling();
            ShowList();
            var net = AdventureServerConnection.Instance;
            if (!net) { Say("Chưa kết nối máy chủ."); return; }
            Say("Đang tải các gói nạp...");
            net.GetStoreCatalog((ok, cat, err) =>
            {
                if (!ok || cat == null) { Say(err ?? "Không tải được gói nạp."); return; }
                catalog = cat;
                Say(cat.paymentsEnabled ? "Chọn gói, quét mã QR bằng app ngân hàng. Kim Cương tự cộng khi tiền vào." : "Nạp tiền chưa được kích hoạt trên máy chủ.");
                Redraw();
            });
        }

        public void Close() { if (poll != null) StopCoroutine(poll); poll = null; if (root) root.SetActive(false); }

        private void Say(string s) { LastMessage = s ?? ""; if (status) status.text = LastMessage; }
        private void Copy(string text, string msg) { GUIUtility.systemCopyBuffer = text ?? ""; Say(msg); }

        private void ShowList() { if (poll != null) StopCoroutine(poll); poll = null; if (listView) listView.SetActive(true); if (orderView) orderView.SetActive(false); }

        private void Redraw()
        {
            foreach (Transform t in listContent) Destroy(t.gameObject);
            foreach (StorePackageState p in catalog.packages.Where(p => p.kind == "Pack" || p.kind == "MonthCard" || p.kind == "Starter"))
            {
                StorePackageState pkg = p;
                Button b = Btn(listContent, "Pkg_" + p.id, $"{p.name}   ·   {p.priceVnd:N0} đ   →   {p.rewardSummary}", Vector2.zero, Vector2.one, () => Choose(pkg),
                    p.purchasable ? new Color(0.3f, 0.5f, 0.35f) : new Color(0.3f, 0.3f, 0.36f));
                ((RectTransform)b.transform).sizeDelta = new Vector2(0f, 62f);
                b.interactable = catalog.paymentsEnabled && p.purchasable;
                b.GetComponentInChildren<Text>().fontSize = 21;
            }
        }

        public void Choose(StorePackageState pkg)
        {
            var net = AdventureServerConnection.Instance;
            if (!net) return;
            Say("Đang tạo đơn nạp...");
            net.CreateTopUp(pkg.id, (ok, o, err) =>
            {
                if (!ok || o == null) { Say(err ?? "Không tạo được đơn nạp."); return; }
                order = o;
                listView.SetActive(false); orderView.SetActive(true);
                orderInfo.text = $"<b>{pkg.name}</b>\nSố tiền: <b>{o.amountVnd:N0} đ</b>\nNgân hàng: {o.bankId}\nSTK: <b>{o.accountNo}</b>\nTên: {o.accountName}\nNội dung CK: <b>{o.code}</b>\n(giữ đúng nội dung và số tiền)";
                Say("Quét mã QR hoặc chuyển khoản đúng nội dung. Đang chờ tiền vào...");
                ShowQr(o.payload);
                if (poll != null) StopCoroutine(poll);
                poll = StartCoroutine(Poll(o.orderId));
            });
        }

        /// <summary>The balance shown in the HUD is whatever the server says (no local arithmetic).</summary>
        private void SyncBalance()
        {
            var world = AdventureWorldSession.Current; var net = AdventureServerConnection.Instance;
            if (!world || world.Character == null || !net) return;
            net.GetPets(world.Character.characterId, (ok, st, e) => { if (ok && st != null && world.Character != null) { world.Character.gold = st.gold; world.Character.diamonds = st.diamonds; } });   // any server snapshot carries the real balances
        }

        /// <summary>The QR picture is generated right here from the VietQR (EMVCo) payload: no download, so it always appears and stays pixel-sharp.</summary>
        private void ShowQr(string payload)
        {
            if (qr && qr.texture) Destroy(qr.texture);
            Texture2D tex = QrCodeEncoder.ToTexture(payload, 512);
            if (!tex) { if (qr) qr.color = new Color(1f, 1f, 1f, 0f); Say("Không tạo được mã QR, hãy chuyển khoản thủ công theo thông tin bên cạnh. Đang chờ tiền vào..."); return; }
            qr.texture = tex; qr.color = Color.white;
        }
        private IEnumerator Poll(string orderId)
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(3f);
                var net = AdventureServerConnection.Instance;
                if (!net) continue;
                bool done = false; TopUpOrderState latest = null;
                net.GetTopUp(orderId, (ok, o, err) => { latest = ok ? o : null; done = true; });
                while (!done) yield return null;
                if (latest == null) continue;
                order = latest;
                if (latest.status == "completed")
                {
                    Completed = true;
                    Say($"Đã nhận {latest.diamondsGranted} Kim Cương!");
                    yield return new WaitForSecondsRealtime(1.6f);
                    SyncBalance();
                    PlayerHud.RefreshVip();                                   // VIP may have risen with this top-up
                    Close();
                    QuickGoalToast.Show($"Nạp thành công! +{latest.diamondsGranted} Kim Cương");           // floating confirmation after the QR window closes
                    yield break;
                }
                if (latest.status == "expired") { Say("Đơn nạp đã hết hạn. Hãy chọn gói để tạo đơn mới."); ShowList(); yield break; }
            }
        }
    }
}
