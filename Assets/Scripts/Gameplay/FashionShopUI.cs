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
    /// Fashion shop + wardrobe + 3D fitting room ("Tiệm Thời Trang Chuông Gió").
    ///  - Everything shown (names, prices, ownership, balance, worn outfit) comes from the server's wardrobe
    ///    snapshot; the client never sends a price or a balance.
    ///  - Try-on only dresses the preview model; the real hero changes only after the server confirms a purchase
    ///    or an equip. Closing the shop discards unsaved try-ons.
    ///  - Purchases carry a request id: buttons lock while a request is in flight and a lost connection is
    ///    retried with the same id, so a double click or a retry can never charge twice.
    /// The UI is built at runtime from the house UI kit (fonts + sprites assigned by the scene builder).
    /// </summary>
    public sealed class FashionShopUI : MonoBehaviour
    {
        [Header("House UI kit")]
        [SerializeField] private Font font;
        [SerializeField] private Font boldFont;
        [SerializeField] private Sprite panelSprite, enamelSprite, rimSprite, frameSprite, pillSprite;
        /// <summary>Purpose-made shop skins (FrameArt: shop_window, shop_tab, chip, item_cell, purse_*, btn_*...), looked up by name.</summary>
        [SerializeField] private Sprite[] skins = new Sprite[0];

        private Sprite S(string n)
        {
            foreach (Sprite s in skins) if (s && s.name == n) return s;
            return null;
        }

        /// <summary>Puts a skin on an image (white tint so the painted colours show). False when that skin is not assigned.</summary>
        internal bool Skin(Image img, string n, float scale = 1f)
        {
            Sprite s = S(n);
            if (!s || !img) return false;
            img.sprite = s; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1f / scale;
            return true;
        }
        [SerializeField, Range(0, 31)] private int previewLayer = 30;
        [Header("Shop art")]
        [SerializeField] private Sprite coinIcon, diamondIcon;
        [Tooltip("Đứng, Chạy, Nhảy, Đánh")]
        [SerializeField] private Sprite[] poseIcons = new Sprite[4];
        [Tooltip("Baked dawn backdrop layers (sky, hills, mid, near) shown behind the fitting-room hero.")]
        [SerializeField] private Texture2D[] backdropLayers = new Texture2D[0];

        public enum Tab { Gold, Premium, Wardrobe }

        // ---------------------------------------------------------------- state
        public bool IsOpen => root && root.activeSelf;
        public bool Busy { get; private set; }
        public WardrobeSnapshot Wardrobe { get; private set; }
        public OutfitLoadout TryOn { get; private set; } = new OutfitLoadout();
        public Tab CurrentTab { get; private set; }
        public string SlotFilter { get; private set; } = "";
        public bool AllClasses { get; private set; }
        public string Selected { get; private set; }
        public string Status => status ? status.text : "";
        public HeroOutfit PreviewOutfit { get; private set; }
        public GameObject PreviewModel { get; private set; }
        public int PurchaseRequestsSent { get; private set; }
        public event Action<WardrobeSnapshot> Purchased;
        public event Action Closed;

        private HeroClass heroClass;
        private string characterId;
        private Action<bool> setPlayerInput;

        // ---------------------------------------------------------------- built UI
        private GameObject root, confirm;
        private Text walletGold, walletDiamonds, rarityLabel;
        private Text title, wallet, status, detailName, detailDesc, detailPrice, confirmText, emptyText, classToggleText;
        private RawImage previewImage;
        private RectTransform grid;
        private Button tryBtn, buyBtn, wearBtn, saveBtn, confirmYes;
        private Text tryBtnText, wearBtnText;
        private Image detailIcon;
        private readonly Dictionary<Tab, Button> tabs = new Dictionary<Tab, Button>();
        private readonly Dictionary<string, Button> chips = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> cards = new Dictionary<string, Button>();
        private Camera previewCam;
        private RenderTexture previewRT;
        private Transform stage;
        private Animator previewAnim;
        private float yaw = 160f;
        private Action confirmAction;

        private static readonly Color Ink = new Color(0.07f, 0.09f, 0.16f, 0.96f), Cream = new Color(1f, 0.97f, 0.9f),
            Gold = new Color(1f, 0.84f, 0.45f), Teal = new Color(0.26f, 0.56f, 0.62f), Plum = new Color(0.45f, 0.3f, 0.62f),
            Muted = new Color(0.34f, 0.36f, 0.44f), Rose = new Color(0.72f, 0.3f, 0.36f);

        public static readonly (string slot, string label)[] SlotChips =
        {
            ("", "Tất cả"), ("hair", "Tóc"), ("head", "Mũ"), ("top", "Áo"), ("bottom", "Quần/Váy"), ("gloves", "Găng"),
            ("shoes", "Giày"), ("accessory", "Phụ kiện"), ("back", "Choàng/Cánh"), ("weapon", "Vũ khí"), ("set", "Bộ nguyên set"),
        };

        public static string SlotName(string slot) => slot switch
        {
            "head" => "Mũ / Trang sức đầu", "top" => "Áo", "bottom" => "Quần / Váy", "gloves" => "Găng tay", "shoes" => "Giày",
            "accessory" => "Phụ kiện", "back" => "Áo choàng / Cánh", "hair" => "Tóc", "weapon" => "Ngoại hình vũ khí", "set" => "Skin nguyên bộ", _ => slot,
        };

        public static string Money(long v) => v.ToString("#,0").Replace(',', '.');

        // ================================================================= open / close
        /// <summary>Opens the shop for the world session's character (loads the wardrobe from the server first).</summary>
        public void Open(Tab tab, Action<bool> playerInput = null)
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (!world || world.Character == null) return;
            if (!root) Build();
            setPlayerInput = playerInput;
            setPlayerInput?.Invoke(false);
            heroClass = world.Character.heroClass;
            characterId = world.Character.characterId;
            CurrentTab = tab;
            SlotFilter = "";
            AllClasses = false;
            Selected = null;
            root.SetActive(true);
            SetHud(false);
            title.text = "Tiệm Thời Trang Chuông Gió";
            SpawnPreview(world);
            TryOn = (world.Character.outfit ?? new OutfitLoadout()).Clone();
            ApplyPreview();
            Wardrobe = null;
            Refresh();
            SetStatus("Đang tải tủ đồ từ máy chủ...");
            if (!server) { SetStatus("Chưa kết nối máy chủ."); return; }
            Busy = true;
            RefreshButtons();
            server.GetWardrobe(characterId, (ok, w, err) =>
            {
                Busy = false;
                if (!IsOpen) return;
                if (!ok || w == null) { SetStatus(err ?? "Không tải được tủ đồ."); RefreshButtons(); return; }
                Wardrobe = w;
                TryOn = w.outfit.Clone();
                ApplyPreview();
                SetStatus("");
                Refresh();
            });
        }

        public void Close()
        {
            if (!IsOpen) return;
            // Unsaved try-ons are dropped: the real hero keeps the server-confirmed outfit.
            root.SetActive(false);
            confirm.SetActive(false);
            SetHud(true);
            if (PreviewModel) Destroy(PreviewModel);
            PreviewModel = null;
            PreviewOutfit = null;
            if (previewCam) previewCam.enabled = false;
            setPlayerInput?.Invoke(true);
            Closed?.Invoke();
        }

        /// <summary>The town HUD (player frame, shortcuts, touch controls) is hidden behind the shop, not shown through it.</summary>
        private void SetHud(bool on)
        {
            foreach (PlayerHud h in FindObjectsByType<PlayerHud>(FindObjectsInactive.Include))
            {
                var c = h.GetComponent<Canvas>();
                if (c) c.enabled = on;
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current is { } k && k.escapeKey.wasPressedThisFrame)
            {
                if (confirm.activeSelf) confirm.SetActive(false); else Close();
            }
#endif
            if (stage) stage.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ================================================================= catalogue view
        public IEnumerable<WardrobeItem> Visible()
        {
            if (Wardrobe == null) return Enumerable.Empty<WardrobeItem>();
            CosmeticLibrary lib = CosmeticLibrary.Load();
            return Wardrobe.items.Where(i =>
            {
                if (lib == null || lib.Get(i.id) == null) return false; // never sell what the client cannot show
                switch (CurrentTab)
                {
                    case Tab.Gold: if (i.IsSet || i.ForDiamonds) return false; break;
                    case Tab.Premium: if (!i.IsSet) return false; break;
                    case Tab.Wardrobe: if (!i.owned) return false; break;
                }
                if (SlotFilter == "set" ? !i.IsSet : SlotFilter.Length > 0 && i.slot != SlotFilter) return false;
                if (!AllClasses && !i.usableByCharacter) return false;
                return true;
            });
        }

        private void Refresh()
        {
            foreach (var kv in tabs)
                if (!SkinToggle(kv.Value, "shop_tab", kv.Key == CurrentTab))
                    Tint(kv.Value, kv.Key == CurrentTab ? (kv.Key == Tab.Premium ? Plum : Teal) : new Color(0.17f, 0.2f, 0.32f));
            foreach (var kv in chips)
                if (!SkinToggle(kv.Value, "chip", kv.Key == SlotFilter))
                    Tint(kv.Value, kv.Key == SlotFilter ? new Color(0.86f, 0.66f, 0.28f) : new Color(0.16f, 0.2f, 0.32f));
            classToggleText.text = AllClasses ? "Mọi nghề" : HeroClassCatalog.Get(heroClass).displayName;
            if (walletGold) walletGold.text = Wardrobe == null ? "…" : Money(Wardrobe.gold);
            if (walletDiamonds) walletDiamonds.text = Wardrobe == null ? "…" : Money(Wardrobe.diamonds);

            foreach (Transform c in grid) Destroy(c.gameObject);
            cards.Clear();
            var list = Visible().ToList();
            CosmeticLibrary lib = CosmeticLibrary.Load();
            foreach (WardrobeItem it in list) cards[it.id] = Card(it, lib.Get(it.id));
            emptyText.gameObject.SetActive(Wardrobe != null && list.Count == 0);
            emptyText.text = CurrentTab == Tab.Wardrobe && SlotFilter.Length == 0 ? "Tủ đồ còn trống — hãy ghé gian Thời Trang nhé!"
                : SlotFilter is "hair" or "weapon" ? "Kiểu tóc và ngoại hình vũ khí đang được thiết kế — chưa có món nào đạt chuẩn để mở bán."
                : CurrentTab == Tab.Premium ? "Chưa có skin đặc biệt nào trong mục này." : "Chưa có món nào trong mục này.";
            if (Selected != null && !list.Any(i => i.id == Selected)) Selected = null;
            if (Selected == null && list.Count > 0 && CurrentTab != Tab.Wardrobe) Select(list[0].id, false);
            else Highlight();
            ShowDetail();
            RefreshButtons();
        }

        public static string RarityName(string r) => r switch { "rare" => "Hiếm", "epic" => "Sử thi", "legendary" => "Huyền thoại", _ => "Thường" };
        public static Color RarityColor(string r) => r switch
        {
            "rare" => new Color(0.36f, 0.66f, 1f), "epic" => new Color(0.74f, 0.46f, 1f), "legendary" => new Color(1f, 0.74f, 0.3f), _ => new Color(0.66f, 0.72f, 0.8f),
        };

        /// <summary>This item as worn by THIS class's hero (per-class render), falling back to the generic icon.</summary>
        private Sprite IconFor(string id)
        {
            CosmeticArt art = CosmeticLibrary.Load()?.Get(id);
            return art?.For(heroClass)?.icon ? art.For(heroClass).icon : art?.icon;
        }

        private Button Card(WardrobeItem it, CosmeticArt art)
        {
            Color rc = RarityColor(it.rarity);
            var go = new GameObject("Card_" + it.id, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(grid, false);
            var img = go.AddComponent<Image>();
            img.sprite = panelSprite; img.type = Image.Type.Sliced;
            CardLook(img, it, false);
            var b = go.AddComponent<Button>();
            b.onClick.AddListener(() => Select(it.id, true));
            var cb = b.colors; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); b.colors = cb;
            // icon well with a soft rarity glow
            var well = Panel(go.transform, "Well", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(164f, 152f),
                new Color(rc.r * 0.35f, rc.g * 0.35f, rc.b * 0.45f, 0.9f), panelSprite);
            well.GetComponent<Image>().raycastTarget = false;
            var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            icon.transform.SetParent(well.transform, false);
            UiKit.Stretch(icon.rectTransform, 6f, 6f, 6f, 6f);
            icon.sprite = IconFor(it.id); icon.preserveAspect = true; icon.raycastTarget = false;
            var strip = Panel(go.transform, "Rarity", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(150f, 6f), rc, pillSprite);
            strip.GetComponent<Image>().raycastTarget = false;
            Text name = Label(go.transform, "Name", it.name, 20, Cream, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(180f, 50f), true);
            name.resizeTextForBestFit = true;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            string price = it.owned ? (it.equipped ? "Đang mặc" : "Đã sở hữu") : Money(it.price);
            Color pc = it.owned ? new Color(0.6f, 1f, 0.7f) : it.ForDiamonds ? new Color(0.62f, 0.9f, 1f) : Gold;
            Text pt = Label(go.transform, "Price", price, 20, pc, new Vector2(0.5f, 0f), new Vector2(it.owned ? 0f : 14f, 26f), new Vector2(it.owned ? 180f : 140f, 30f), true);
            pt.gameObject.AddComponent<FitOneLine>().Configure(20, 14);
            if (!it.owned)
            {
                var ci = new GameObject("Currency", typeof(RectTransform)).AddComponent<Image>();
                ci.transform.SetParent(go.transform, false);
                Place(ci.rectTransform, new Vector2(0.5f, 0f), new Vector2(-pt.preferredWidth * 0.5f - 6f, 26f), new Vector2(26f, 26f));
                ci.sprite = it.ForDiamonds ? diamondIcon : coinIcon; ci.raycastTarget = false; ci.preserveAspect = true;
            }
            if (!it.usableByCharacter)
            {
                Text cls = Label(go.transform, "Class", "Nghề khác", 16, new Color(1f, 0.62f, 0.58f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(150f, 24f), true);
                cls.gameObject.AddComponent<FitOneLine>().Configure(16, 12);
            }
            return b;
        }

        private Color CardColor(WardrobeItem it, bool selected) =>
            selected ? new Color(0.3f, 0.36f, 0.56f) : it != null && it.IsSet ? new Color(0.2f, 0.15f, 0.32f) : new Color(0.13f, 0.17f, 0.28f);

        /// <summary>Card tile: lavender cell, orchid for full sets, gold rim + glow when selected.</summary>
        private void CardLook(Image img, WardrobeItem it, bool selected)
        {
            if (!Skin(img, selected ? "item_cell_on" : it != null && it.IsSet ? "item_cell_set" : "item_cell"))
                img.color = CardColor(it, selected);
        }

        private void Highlight()
        {
            foreach (var kv in cards)
                if (kv.Value) CardLook(kv.Value.GetComponent<Image>(), Wardrobe?.Find(kv.Key), kv.Key == Selected);
        }

        public void SetTab(Tab t) { CurrentTab = t; Selected = null; Refresh(); }
        public void SetSlotFilter(string s) { SlotFilter = s ?? ""; Selected = null; Refresh(); }
        public void ToggleAllClasses() { AllClasses = !AllClasses; Refresh(); }

        /// <summary>Selects an item; selecting also tries it on (free, preview only).</summary>
        public void Select(string id, bool tryIt)
        {
            Selected = id;
            WardrobeItem it = Wardrobe?.Find(id);
            if (tryIt && it != null && it.usableByCharacter && !IsTried(it)) ToggleTry();
            Highlight();
            ShowDetail();
            RefreshButtons();
        }

        private bool IsTried(WardrobeItem it) => it.IsSet ? TryOn.set == it.id : TryOn.Get(it.slot) == it.id;
        private bool IsWorn(WardrobeItem it) => Wardrobe != null && (it.IsSet ? Wardrobe.outfit.set == it.id : Wardrobe.outfit.Get(it.slot) == it.id);

        private void ShowDetail()
        {
            WardrobeItem it = Selected != null ? Wardrobe?.Find(Selected) : null;
            detailIcon.enabled = it != null;
            rarityLabel.transform.parent.gameObject.SetActive(it != null);
            if (it == null)
            {
                detailName.text = Wardrobe == null ? "" : "Chọn một món để xem và mặc thử";
                detailDesc.text = detailPrice.text = "";
                return;
            }
            detailIcon.sprite = IconFor(it.id);
            detailName.text = it.name;
            rarityLabel.text = RarityName(it.rarity);
            rarityLabel.transform.parent.GetComponent<Image>().color = RarityColor(it.rarity) * 0.75f;
            string classes = it.classes.Length >= 4 ? "Mọi nghề" : string.Join(", ", it.classes.Select(c => HeroClassCatalog.Get((HeroClass)c).displayName));
            detailDesc.text = $"<color=#C9D6E8>{SlotName(it.slot)} · {classes}</color>\n{it.description}";
            detailPrice.text = it.owned ? (IsWorn(it) ? "<color=#9CFFB4>Đang mặc</color>" : "<color=#9CFFB4>Đã sở hữu</color>")
                : it.ForDiamonds ? $"<color=#9FE6FF>{Money(it.price)} Kim Cương</color>" : $"<color=#FFD873>{Money(it.price)} Vàng</color>";
        }

        private void RefreshButtons()
        {
            WardrobeItem it = Selected != null ? Wardrobe?.Find(Selected) : null;
            bool ready = Wardrobe != null && !Busy;
            tryBtn.interactable = ready && it != null && it.usableByCharacter;
            tryBtnText.text = it != null && IsTried(it) ? "Bỏ thử" : "Mặc thử";
            buyBtn.gameObject.SetActive(it == null || !it.owned);
            buyBtn.interactable = ready && it != null && !it.owned && it.usableByCharacter;
            wearBtn.gameObject.SetActive(it != null && it.owned);
            wearBtn.interactable = ready && it != null && it.owned && it.usableByCharacter;
            wearBtnText.text = it != null && IsWorn(it) ? "Tháo" : "Mặc";
            bool allOwned = Wardrobe != null && TryOnItems().All(Wardrobe.Owns);
            saveBtn.interactable = ready && allOwned && !TryOn.SameAs(Wardrobe.outfit);
            foreach (var kv in tabs) kv.Value.interactable = !Busy;
        }

        private IEnumerable<string> TryOnItems()
        {
            if (!string.IsNullOrEmpty(TryOn.set)) yield return TryOn.set;
            if (TryOn.slots != null) foreach (OutfitSlotItem s in TryOn.slots) yield return s.itemId;
        }

        // ================================================================= actions
        public void ToggleTry()
        {
            WardrobeItem it = Selected != null ? Wardrobe?.Find(Selected) : null;
            if (it == null || !it.usableByCharacter) return;
            bool on = IsTried(it);
            TryOn = it.IsSet ? TryOn.WithSet(on ? "" : it.id) : TryOn.With(it.slot, on ? Wardrobe.outfit.Get(it.slot) == it.id ? null : Wardrobe.outfit.Get(it.slot) : it.id);
            ApplyPreview();
            ShowDetail();
            RefreshButtons();
        }

        /// <summary>"Mua": asks for confirmation (always for Diamonds), then buys and wears it.</summary>
        public void AskBuy()
        {
            WardrobeItem it = Selected != null ? Wardrobe?.Find(Selected) : null;
            if (it == null || it.owned || Busy) return;
            long balance = it.ForDiamonds ? Wardrobe.diamonds : Wardrobe.gold;
            if (balance < it.price)
            {
                SetStatus(it.ForDiamonds ? $"Không đủ Kim Cương: cần {Money(it.price)}, bạn có {Money(balance)}." : $"Không đủ Vàng: cần {Money(it.price)}, bạn có {Money(balance)}.");
                return;
            }
            confirmText.text = $"Mua <b>{it.name}</b> với giá\n{(it.ForDiamonds ? $"<color=#9FE6FF>{Money(it.price)} Kim Cương</color>" : $"<color=#FFD873>{Money(it.price)} Vàng</color>")}?";
            confirmAction = () => Buy(it.id);
            confirm.SetActive(true);
        }

        public void ConfirmYes()
        {
            confirm.SetActive(false);
            Action a = confirmAction;
            confirmAction = null;
            a?.Invoke();
        }

        /// <summary>Server purchase with a fresh request id; a lost connection is retried with the SAME id.</summary>
        public void Buy(string itemId)
        {
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (Busy || !server || Wardrobe == null) return;
            Busy = true;
            RefreshButtons();
            string requestId = Guid.NewGuid().ToString("N");
            SetStatus("Đang thanh toán...");
            StartCoroutine(BuyRoutine(server, itemId, requestId));
        }

        private IEnumerator BuyRoutine(AdventureServerConnection server, string itemId, string requestId)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool done = false, ok = false, retry = false;
                WardrobeSnapshot result = null;
                string error = null;
                PurchaseRequestsSent++;
                server.PurchaseCosmetic(characterId, itemId, requestId, true, (o, w, err, connection) =>
                {
                    done = true; ok = o; result = w; error = err; retry = connection;
                });
                while (!done) yield return null;
                if (ok && result != null)
                {
                    Busy = false;
                    Wardrobe = result;
                    // keep other un-bought try-ons on the preview, but the bought item is now really worn
                    WardrobeItem it = result.Find(itemId);
                    TryOn = it != null && it.IsSet ? TryOn.WithSet(itemId) : TryOn.With(it?.slot ?? "", itemId);
                    ApplyPreview();
                    AdventureWorldSession.Current?.ApplyServerOutfit(result.outfit, result.gold, result.diamonds, result.serverRevision);
                    SetStatus(result.replayed ? "Giao dịch đã được ghi nhận trước đó." : $"Đã mua {it?.name}! Món đồ đã vào tủ và được mặc lên.");
                    Refresh();
                    Purchased?.Invoke(result);
                    yield break;
                }
                if (!retry || attempt == 2)
                {
                    Busy = false;
                    SetStatus(error ?? "Mua thất bại.");
                    RefreshButtons();
                    yield break;
                }
                SetStatus("Mất kết nối — đang thử lại giao dịch (không bị trừ tiền hai lần)...");
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        /// <summary>"Mặc"/"Tháo" on an owned item, saved on the server immediately.</summary>
        public void ToggleWear()
        {
            WardrobeItem it = Selected != null ? Wardrobe?.Find(Selected) : null;
            if (it == null || !it.owned || Busy) return;
            bool worn = IsWorn(it);
            OutfitLoadout next = it.IsSet ? Wardrobe.outfit.WithSet(worn ? "" : it.id) : Wardrobe.outfit.With(it.slot, worn ? null : it.id);
            SaveOutfit(next, worn ? $"Đã tháo {it.name}." : $"Đã mặc {it.name}.");
        }

        /// <summary>"Lưu bộ phối": saves the whole preview look (only possible when every piece is owned).</summary>
        public void SaveTryOn() => SaveOutfit(TryOn, "Đã lưu bộ trang phục.");

        private void SaveOutfit(OutfitLoadout next, string message)
        {
            AdventureServerConnection server = AdventureServerConnection.Instance;
            if (Busy || !server) return;
            Busy = true;
            RefreshButtons();
            SetStatus("Đang lưu...");
            server.EquipOutfit(characterId, next, (ok, w, err) =>
            {
                Busy = false;
                if (!ok || w == null) { SetStatus(err ?? "Không lưu được trang phục."); RefreshButtons(); return; }
                Wardrobe = w;
                TryOn = w.outfit.Clone();
                ApplyPreview();
                AdventureWorldSession.Current?.ApplyServerOutfit(w.outfit, w.gold, w.diamonds, w.serverRevision);
                SetStatus(message);
                Refresh();
            });
        }

        private void SetStatus(string s) { if (status) status.text = s; }

        // ================================================================= 3D fitting room
        private void SpawnPreview(AdventureWorldSession world)
        {
            if (PreviewModel) Destroy(PreviewModel);
            GameObject prefab = world.HeroPrefabs != null && (int)heroClass < world.HeroPrefabs.Length ? world.HeroPrefabs[(int)heroClass] : null;
            if (!prefab) return;
            PreviewModel = Instantiate(prefab, stage, false);
            PreviewModel.name = "FittingRoomHero";
            PreviewModel.transform.localPosition = Vector3.zero;
            PreviewModel.transform.localRotation = Quaternion.identity;
            foreach (Transform t in PreviewModel.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = previewLayer;
            HeroCombatPresentation pres = PreviewModel.GetComponent<HeroCombatPresentation>();
            if (pres) pres.ProjectileRangeScale = 0.3f;
            previewAnim = PreviewModel.GetComponentInChildren<Animator>();
            if (previewAnim)
            {
                previewAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                previewAnim.SetBool("Grounded", true);
            }
            PreviewOutfit = AdventureWorldSession.DressHero(PreviewModel, heroClass);
            yaw = 160f;
            previewCam.enabled = true;
            PlayPose("Idle");
        }

        private void ApplyPreview()
        {
            if (!PreviewOutfit) return;
            PreviewOutfit.Apply(TryOn);
            foreach (GameObject p in PreviewOutfit.Pieces)
                if (p) foreach (Transform t in p.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = previewLayer;
        }

        private Coroutine poseRoutine;

        /// <summary>Fitting-room motion: Idle, Run, Jump, Attack (Skill too) on the real animator.</summary>
        public void PlayPose(string pose)
        {
            if (!previewAnim) return;
            if (poseRoutine != null) StopCoroutine(poseRoutine);
            poseRoutine = StartCoroutine(Pose(pose));
        }

        private IEnumerator Pose(string pose)
        {
            Animator a = previewAnim;
            a.SetFloat("Speed", 0f);
            a.SetFloat("VerticalSpeed", 0f);
            a.SetBool("Grounded", true);
            if (PreviewOutfit) { PreviewOutfit.SpeedOverride = 0f; PreviewOutfit.Airborne = false; }
            switch (pose)
            {
                case "Run":
                    a.SetFloat("Speed", 4.5f);
                    if (HasParam(a, "RunPlayback")) a.SetFloat("RunPlayback", 1.2f);
                    if (PreviewOutfit) PreviewOutfit.SpeedOverride = 4.5f;
                    break;
                case "Jump":
                    for (int k = 0; k < 2; k++)
                    {
                        a.SetBool("Grounded", false);
                        a.SetFloat("VerticalSpeed", 6f);
                        if (PreviewOutfit) PreviewOutfit.Airborne = true;
                        yield return new WaitForSeconds(0.38f);
                        a.SetFloat("VerticalSpeed", -6f);
                        yield return new WaitForSeconds(0.38f);
                        a.SetBool("Grounded", true);
                        a.SetFloat("VerticalSpeed", 0f);
                        if (PreviewOutfit) PreviewOutfit.Airborne = false;
                        yield return new WaitForSeconds(0.45f);
                    }
                    break;
                case "Attack":
                    a.SetTrigger("Attack");
                    break;
                case "Skill":
                    a.SetTrigger("Skill");
                    break;
            }
            poseRoutine = null;
        }

        private static bool HasParam(Animator a, string n)
        {
            foreach (AnimatorControllerParameter p in a.parameters) if (p.name == n) return true;
            return false;
        }

        /// <summary>Drag on the preview (mouse or finger) turns the model.</summary>
        private sealed class DragRotate : MonoBehaviour, IDragHandler
        {
            public FashionShopUI shop;
            public void OnDrag(PointerEventData e) => shop.yaw -= e.delta.x * 0.45f;
        }

        public void RotatePreview(float degrees) => yaw += degrees;

        // ================================================================= build
        /// <summary>Shared overlay canvas (the shop window and the merchant's talk menu live on it).</summary>
        internal Sprite FrameSprite => frameSprite;
        internal Transform CanvasRoot { get { if (!root) Build(); return canvasRoot; } }
        private Transform canvasRoot;

        private void Build()
        {
            var canvasGo = new GameObject("FashionShopCanvas", typeof(RectTransform));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasGo.transform;
            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            Color navy = new Color(0.08f, 0.1f, 0.2f, 0.97f), panelNavy = new Color(0.1f, 0.13f, 0.25f, 0.95f);

            root = Panel(canvasGo.transform, "Shop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.08f, 0.8f), null);
            UiKit.Stretch(root.GetComponent<RectTransform>());
            // window: deep navy-violet with a slim gilded rim (no thick black frame)
            var win = Panel(root.transform, "Window", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, navy, panelSprite);
            UiKit.Stretch(win.GetComponent<RectTransform>(), 100f, 40f, 100f, 36f);
            bool velvet = Skin(win.GetComponent<Image>(), "shop_window"); // violet velvet + gold fleur corners
            if (!velvet) Rim(win.transform, 0.95f);

            // ---- header: title, balances, close
            var header = Panel(win.transform, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.17f, 0.15f, 0.34f, 0.95f), panelSprite);
            var hr = header.GetComponent<RectTransform>();
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(34f, -100f); hr.offsetMax = new Vector2(-34f, -30f);
            if (velvet) header.GetComponent<Image>().color = new Color(0.3f, 0.2f, 0.5f, 0.35f); // soft band on the velvet, no second frame
            else Rim(header.transform, 0.7f);
            title = Label(header.transform, "Title", "", 34, Gold, new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(600f, 60f), true);
            title.alignment = TextAnchor.MiddleLeft;
            title.gameObject.AddComponent<FitOneLine>().Configure(34, 22);
            var to = title.gameObject.AddComponent<Outline>(); to.effectColor = new Color(0.25f, 0.12f, 0.03f, 0.9f);
            Text Pill(string name, Sprite icon, float x)
            {
                var p = Panel(header.transform, name, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(230f, 56f), new Color(0.05f, 0.06f, 0.14f, 0.9f), pillSprite);
                p.GetComponent<Image>().raycastTarget = false;
                Skin(p.GetComponent<Image>(), name == "Gold" ? "purse_gold" : "purse_gem", 0.85f); // bronze coin purse vs crystal purse
                var ic = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
                ic.transform.SetParent(p.transform, false);
                Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(44f, 44f));
                ic.sprite = icon; ic.preserveAspect = true; ic.raycastTarget = false;
                Text t = Label(p.transform, "Amount", "", 24, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
                UiKit.Stretch(t.rectTransform, 60f, 2f, 18f, 2f);
                t.alignment = TextAnchor.MiddleLeft;
                t.gameObject.AddComponent<FitOneLine>().Configure(24, 15);
                return t;
            }
            walletDiamonds = Pill("Diamonds", diamondIcon, -230f);
            walletGold = Pill("Gold", coinIcon, -480f);
            var close = Btn(header.transform, "Close", "✕", new Vector2(1f, 0.5f), new Vector2(-62f, 0f), new Vector2(64f, 64f), Rose);
            close.onClick.AddListener(Close);
            wallet = walletGold;

            // ---- left: fitting room stage (fantasy backdrop behind the hero)
            var stageBg = Panel(win.transform, "FittingRoom", new Vector2(0f, 0f), new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero, panelNavy, panelSprite);
            UiKit.Inset(stageBg.GetComponent<RectTransform>(), 38f, 38f, 8f, 112f); // keep the 0..0.36 column, clear of the window ornament
            stageBg.AddComponent<RectMask2D>();
            foreach (Texture2D layer in backdropLayers)
            {
                if (!layer) continue;
                var l = new GameObject("Backdrop_" + layer.name, typeof(RectTransform)).AddComponent<RawImage>();
                l.gameObject.layer = 5;
                l.transform.SetParent(stageBg.transform, false);
                l.texture = layer;
                l.raycastTarget = false;
                l.color = new Color(0.86f, 0.86f, 0.92f, 1f);
                var fit = l.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = layer.width / (float)layer.height;
            }
            previewRT = new RenderTexture(720, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "FittingRoomRT" };
            previewImage = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            previewImage.transform.SetParent(stageBg.transform, false);
            UiKit.Stretch(previewImage.rectTransform, 0f, 110f, 0f, 0f);
            previewImage.texture = previewRT;
            previewImage.gameObject.AddComponent<DragRotate>().shop = this;
            var fitter = previewImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 720f / 900f;
            if (S("stage_bezel"))
            {
                // slim lavender bezel drawn over the stage (no heavy gold frame)
                var bezel = Panel(stageBg.transform, "StageBezel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white, null);
                UiKit.Stretch(bezel.GetComponent<RectTransform>());
                var bi = bezel.GetComponent<Image>();
                bi.raycastTarget = false;
                Skin(bi, "stage_bezel");
            }
            else Rim(stageBg.transform, 0.9f);
            Text hint = Label(stageBg.transform, "Hint", "Kéo để xoay", 18, new Color(1f, 1f, 1f, 0.85f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(300f, 28f), true);
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            // pose bar: round icon buttons with captions
            var poseBar = Panel(stageBg.transform, "PoseBar", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero, new Color(0.05f, 0.06f, 0.14f, 0.82f), null);
            var pbr = poseBar.GetComponent<RectTransform>();
            pbr.pivot = new Vector2(0.5f, 0f);
            pbr.offsetMin = new Vector2(0f, 0f); pbr.offsetMax = new Vector2(0f, 110f);
            var pbl = poseBar.AddComponent<HorizontalLayoutGroup>();
            pbl.childAlignment = TextAnchor.MiddleCenter; pbl.spacing = 18f;
            pbl.childControlWidth = pbl.childControlHeight = false;
            string[] poses = { "Idle", "Run", "Jump", "Attack" };
            string[] poseNames = { "Đứng", "Chạy", "Nhảy", "Đánh" };
            for (int i = 0; i < poses.Length; i++)
            {
                string p = poses[i];
                var cell = new GameObject("Pose_" + p, typeof(RectTransform));
                cell.layer = 5;
                cell.transform.SetParent(poseBar.transform, false);
                ((RectTransform)cell.transform).sizeDelta = new Vector2(96f, 100f);
                var hit = cell.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
                var ic = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
                ic.transform.SetParent(cell.transform, false);
                Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(68f, 68f));
                ic.sprite = i < poseIcons.Length ? poseIcons[i] : null; ic.preserveAspect = true;
                var b = cell.AddComponent<Button>();
                b.targetGraphic = ic;
                b.onClick.AddListener(() => PlayPose(p));
                Text cap = Label(cell.transform, "Caption", poseNames[i], 18, Cream, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(96f, 26f), true);
                cap.gameObject.AddComponent<FitOneLine>().Configure(18, 12);
            }

            // stage: far below the world, own layer, own key light, transparent clear (backdrop shows through)
            stage = new GameObject("FittingRoomStage").transform;
            stage.SetParent(transform, false);
            stage.position = new Vector3(0f, -400f, 0f);
            var camGo = new GameObject("FittingRoomCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = stage.position + new Vector3(0f, 0.6f, -3.05f);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            previewCam = camGo.AddComponent<Camera>();
            previewCam.cullingMask = 1 << previewLayer;
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCam.fieldOfView = 24f;
            previewCam.targetTexture = previewRT;
            previewCam.enabled = false;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false; // keep the alpha channel
            var key = new GameObject("FittingRoomLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.color = new Color(1f, 0.93f, 0.85f);
            key.cullingMask = 1 << previewLayer;
            key.transform.rotation = Quaternion.Euler(32f, 25f, 0f);
            // stone dais with a gold inlay ring (toon)
            Shader toon = Shader.Find("AdventureKing/Toon");
            void Dais(string name, float y, float scale, float h, Color c)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(d.GetComponent<Collider>());
                d.name = name;
                d.layer = previewLayer;
                d.transform.SetParent(transform, false);
                d.transform.position = stage.position + new Vector3(0f, y, 0f);
                d.transform.localScale = new Vector3(scale, h, scale);
                if (toon)
                {
                    var m = new Material(toon);
                    m.SetColor("_BaseColor", c);
                    m.SetFloat("_OutlineWidth", 0.004f);
                    d.GetComponent<Renderer>().sharedMaterial = m;
                }
            }
            Dais("DaisBase", -0.09f, 1.25f, 0.05f, new Color(0.36f, 0.38f, 0.48f));
            Dais("DaisRing", -0.04f, 1.05f, 0.012f, new Color(0.95f, 0.76f, 0.38f));
            Dais("DaisTop", -0.035f, 0.98f, 0.012f, new Color(0.5f, 0.52f, 0.62f));

            // ---- right: catalogue
            var right = new GameObject("Catalogue", typeof(RectTransform));
            right.layer = 5;
            right.transform.SetParent(win.transform, false);
            var rr = (RectTransform)right.transform;
            rr.anchorMin = new Vector2(0.36f, 0f); rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(16f, 38f); rr.offsetMax = new Vector2(-38f, -112f);
            UiKit.VStack(rr, 10f);
            RectTransform tabRow = UiKit.HRow(rr, "Tabs", 64f, 12f, TextAnchor.MiddleLeft);
            (Tab t, string label)[] tabDefs = { (Tab.Gold, "Thời Trang · Vàng"), (Tab.Premium, "Skin Đặc Biệt · Kim Cương"), (Tab.Wardrobe, "Tủ Đồ Của Tôi") };
            foreach (var (t, label) in tabDefs)
            {
                Tab tt = t;
                var b = Btn(tabRow, "Tab_" + t, label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 64f), Muted);
                UiKit.Size(b, 64f, 300f, 1f, 180f);
                b.onClick.AddListener(() => SetTab(tt));
                tabs[t] = b;
            }
            // filter chips: horizontally scrollable row (+ class filter at the end)
            var chipView = new GameObject("Filters", typeof(RectTransform));
            chipView.layer = 5;
            chipView.transform.SetParent(rr, false);
            UiKit.Size(chipView.transform, 62f); // >= 40 px touch target at 720p
            chipView.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            chipView.AddComponent<RectMask2D>();
            var chipContent = UiKit.HRow(chipView.transform, "Chips", 62f, 8f, TextAnchor.MiddleLeft);
            chipContent.anchorMin = new Vector2(0f, 0f); chipContent.anchorMax = new Vector2(0f, 1f);
            chipContent.pivot = new Vector2(0f, 0.5f);
            chipContent.sizeDelta = Vector2.zero; // height = the 54-unit view (a new RectTransform's default 100 delta made the pills 154 tall and clipped them)
            chipContent.anchoredPosition = Vector2.zero;
            chipContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var chipScroll = chipView.AddComponent<ScrollRect>();
            chipScroll.content = chipContent; chipScroll.vertical = false; chipScroll.movementType = ScrollRect.MovementType.Clamped;
            foreach (var (slot, label) in SlotChips)
            {
                string s = slot;
                var b = Btn(chipContent, "Chip_" + (slot.Length == 0 ? "all" : slot), label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132f, 62f), new Color(0.16f, 0.2f, 0.32f), 20);
                UiKit.Size(b, 62f, Mathf.Max(112f, label.Length * 13f + 44f));
                b.onClick.AddListener(() => SetSlotFilter(s));
                chips[slot] = b;
            }
            var classBtn = Btn(chipContent, "ClassFilter", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 62f), new Color(0.3f, 0.27f, 0.46f), 20);
            UiKit.Size(classBtn, 62f, 170f);
            classToggleText = classBtn.GetComponentInChildren<Text>();
            classBtn.onClick.AddListener(ToggleAllClasses);

            // grid (scrollable, takes the remaining height)
            var view = Panel(rr, "Grid", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.04f, 0.05f, 0.12f, 0.6f), panelSprite);
            UiKit.Size(view.transform, 300f, -1f, -1f, -1f, 1f);
            view.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = 5;
            content.transform.SetParent(view.transform, false);
            grid = (RectTransform)content.transform;
            grid.anchorMin = new Vector2(0f, 1f); grid.anchorMax = new Vector2(1f, 1f); grid.pivot = new Vector2(0.5f, 1f);
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            var gl = content.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(200f, 262f);
            gl.spacing = new Vector2(14f, 14f);
            gl.padding = new RectOffset(14, 14, 14, 14);
            gl.childAlignment = TextAnchor.UpperCenter;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.AddComponent<ScrollRect>();
            scroll.content = grid; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            emptyText = Label(view.transform, "Empty", "", 24, new Color(0.82f, 0.86f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 120f), false);

            // details: icon | name, rarity, description | price + actions
            var det = Panel(rr, "Detail", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.14f, 0.28f, 0.95f), panelSprite);
            UiKit.Size(det.transform, 214f);
            if (!Skin(det.GetComponent<Image>(), "item_cell", 1.2f)) Rim(det.transform, 0.7f);
            var iconWell = Panel(det.transform, "IconWell", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(172f, 172f), new Color(0.06f, 0.07f, 0.16f, 0.9f), panelSprite);
            iconWell.GetComponent<Image>().raycastTarget = false;
            detailIcon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            detailIcon.transform.SetParent(iconWell.transform, false);
            UiKit.Stretch(detailIcon.rectTransform, 8f, 8f, 8f, 8f);
            detailIcon.preserveAspect = true; detailIcon.raycastTarget = false;
            var info = new GameObject("Info", typeof(RectTransform));
            info.layer = 5;
            info.transform.SetParent(det.transform, false);
            var ir = (RectTransform)info.transform;
            ir.anchorMin = new Vector2(0f, 0f); ir.anchorMax = new Vector2(1f, 1f);
            ir.offsetMin = new Vector2(204f, 18f); ir.offsetMax = new Vector2(-470f, -18f);
            UiKit.VStack(ir, 4f, TextAnchor.UpperLeft);
            RectTransform nameRow = UiKit.HRow(ir, "NameRow", 44f, 10f, TextAnchor.MiddleLeft);
            detailName = Label(nameRow, "Name", "", 28, Gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 44f), true);
            detailName.alignment = TextAnchor.MiddleLeft;
            detailName.gameObject.AddComponent<FitOneLine>().Configure(28, 18);
            UiKit.Size(detailName, 44f, 300f, 1f, 160f);
            var badge = Panel(nameRow, "RarityBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 34f), Color.gray, pillSprite);
            UiKit.Size(badge.transform, 34f, 130f, 0f, 110f);
            rarityLabel = Label(badge.transform, "Rarity", "", 18, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            UiKit.Stretch(rarityLabel.rectTransform, 10f, 2f, 10f, 2f);
            rarityLabel.gameObject.AddComponent<FitOneLine>().Configure(18, 12);
            detailDesc = Label(ir, "Desc", "", 20, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 120f), false);
            detailDesc.alignment = TextAnchor.UpperLeft;
            detailDesc.supportRichText = true;
            detailDesc.resizeTextForBestFit = true; detailDesc.resizeTextMinSize = 15; detailDesc.resizeTextMaxSize = 20;
            detailDesc.verticalOverflow = VerticalWrapMode.Truncate;
            UiKit.Size(detailDesc, 120f);
            var act = new GameObject("Actions", typeof(RectTransform));
            act.layer = 5;
            act.transform.SetParent(det.transform, false);
            var ar = (RectTransform)act.transform;
            ar.anchorMin = new Vector2(1f, 0f); ar.anchorMax = new Vector2(1f, 1f); ar.pivot = new Vector2(1f, 0.5f);
            ar.sizeDelta = new Vector2(450f, 0f); ar.anchoredPosition = new Vector2(-18f, 0f);
            UiKit.VStack(ar, 10f, TextAnchor.MiddleRight);
            detailPrice = Label(ar, "Price", "", 30, Gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 44f), true);
            detailPrice.alignment = TextAnchor.MiddleRight;
            detailPrice.supportRichText = true;
            UiKit.Size(detailPrice, 44f);
            RectTransform btnRow = UiKit.HRow(ar, "Buttons", 66f, 10f, TextAnchor.MiddleRight);
            tryBtn = Btn(btnRow, "TryOn", "Mặc thử", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 66f), new Color(0.3f, 0.42f, 0.62f));
            UiKit.Size(tryBtn, 66f, 140f);
            tryBtnText = tryBtn.GetComponentInChildren<Text>();
            tryBtn.onClick.AddListener(ToggleTry);
            buyBtn = Btn(btnRow, "Buy", "Mua", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 66f), new Color(0.84f, 0.58f, 0.18f));
            UiKit.Size(buyBtn, 66f, 140f);
            buyBtn.onClick.AddListener(AskBuy);
            wearBtn = Btn(btnRow, "Wear", "Trang bị", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 66f), Teal);
            UiKit.Size(wearBtn, 66f, 140f);
            wearBtnText = wearBtn.GetComponentInChildren<Text>();
            wearBtn.onClick.AddListener(ToggleWear);
            saveBtn = Btn(btnRow, "SaveLook", "Lưu bộ phối", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 66f), new Color(0.3f, 0.52f, 0.36f));
            UiKit.Size(saveBtn, 66f, 140f);
            saveBtn.onClick.AddListener(SaveTryOn);
            status = Label(rr, "Status", "", 21, new Color(1f, 0.93f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 30f), false);
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 14; status.resizeTextMaxSize = 21;
            status.verticalOverflow = VerticalWrapMode.Truncate;
            UiKit.Size(status, 30f);

            // purchase confirmation (framed card, measured content area)
            confirm = Panel(root.transform, "Confirm", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f), null);
            UiKit.Stretch(confirm.GetComponent<RectTransform>());
            var card = Panel(confirm.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 400f), Ink, frameSprite ? frameSprite : panelSprite);
            var ci2 = card.GetComponent<Image>();
            bool crest = Skin(ci2, "popup_card");
            if (!crest && frameSprite) { ci2.type = Image.Type.Sliced; ci2.color = Color.white; ci2.pixelsPerUnitMultiplier = 2.6f; }
            Canvas.ForceUpdateCanvases();
            RectTransform cc = UiKit.Content(ci2, crest ? 0f : 14f);
            if (crest) UiKit.Inset(cc, 36f, 24f, 36f, 7f); // title sits in the card's crest band (top 58)
            UiKit.VStack(cc, crest ? 14f : 10f, crest ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter);
            Text ct = Label(cc, "Title", "Xác nhận mua", 32, Gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 44f), true);
            UiKit.Size(ct, 44f);
            confirmText = Label(cc, "Text", "", 28, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 96f), false);
            confirmText.supportRichText = true;
            confirmText.resizeTextForBestFit = true; confirmText.resizeTextMinSize = 18; confirmText.resizeTextMaxSize = 28;
            UiKit.Size(confirmText, 96f);
            RectTransform crow = UiKit.HRow(cc, "Buttons", 66f, 20f, TextAnchor.MiddleCenter);
            confirmYes = Btn(crow, "Yes", "Mua", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 66f), new Color(0.84f, 0.58f, 0.18f));
            UiKit.Size(confirmYes, 66f, 240f);
            confirmYes.onClick.AddListener(ConfirmYes);
            var no = Btn(crow, "No", "Huỷ", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 66f), Muted);
            UiKit.Size(no, 66f, 240f);
            no.onClick.AddListener(() => confirm.SetActive(false));
            confirm.SetActive(false);
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (previewRT) { if (previewCam) previewCam.targetTexture = null; previewRT.Release(); Destroy(previewRT); }
        }

        // ================================================================= tiny UI kit (house style)
        private static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        internal GameObject Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c, Sprite s)
        {
            var go = new GameObject("Shop" + name, typeof(RectTransform)); // unique names: tests and tools look UI up by name
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = c;
            if (s) { img.sprite = s; img.type = Image.Type.Sliced; }
            return go;
        }

        internal void Rim(Transform parent, float alpha = 1f)
        {
            if (!rimSprite) return;
            var go = new GameObject("GildedRim", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var img = go.AddComponent<Image>();
            img.sprite = rimSprite; img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
        }

        internal Text Label(Transform parent, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, bool bold)
        {
            var go = new GameObject("Shop" + name, typeof(RectTransform)); // unique names: tests and tools look UI up by name
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, pos, box);
            var t = go.AddComponent<Text>();
            t.font = bold && boldFont ? boldFont : font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.resizeTextMinSize = Mathf.Min(14, size);
            t.resizeTextMaxSize = size;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        internal Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, int fontSize = 0)
        {
            var go = new GameObject("Shop" + name, typeof(RectTransform)); // unique names: tests and tools look UI up by name
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, pos, size);
            var img = go.AddComponent<Image>();
            img.sprite = enamelSprite ? enamelSprite : pillSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var b = go.AddComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.6f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            string skin = UiKit.SkinFor(bg);
            bool skinned = Skin(img, skin);
            if (!skinned) Rim(go.transform);
            if (!string.IsNullOrEmpty(label) || name == "ClassFilter")
            {
                int fs = fontSize > 0 ? fontSize : size.y > 60 ? 25 : 21;
                Color fg = skinned ? UiKit.InkFor(skin) : Color.white;
                var t = Label(go.transform, "Text", label, fs, fg, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
                if (skinned && fg.grayscale < 0.4f) t.GetComponent<Shadow>().effectColor = new Color(1f, 0.96f, 0.8f, 0.4f);
                UiKit.Stretch(t.rectTransform, Mathf.Clamp(size.y * 0.22f, 8f, 16f), 2f, Mathf.Clamp(size.y * 0.22f, 8f, 16f), 2f); // follows the button size
                t.gameObject.AddComponent<FitOneLine>().Configure(fs, 13);
            }
            return b;
        }

        private static void Tint(Button b, Color c)
        {
            if (b) b.GetComponent<Image>().color = c;
        }

        /// <summary>On/off skin pair (e.g. shop_tab / shop_tab_on): lit gold when active, with dark ink on it.</summary>
        private bool SkinToggle(Button b, string baseName, bool on)
        {
            if (!b || !Skin(b.GetComponent<Image>(), on ? baseName + "_on" : baseName)) return false;
            Text t = b.GetComponentInChildren<Text>();
            if (t)
            {
                t.color = on ? new Color(0.26f, 0.14f, 0.03f) : new Color(0.9f, 0.88f, 1f);
                var sh = t.GetComponent<Shadow>();
                if (sh) sh.effectColor = on ? new Color(1f, 0.96f, 0.8f, 0.4f) : new Color(0f, 0f, 0f, 0.55f);
            }
            return true;
        }
    }
}
