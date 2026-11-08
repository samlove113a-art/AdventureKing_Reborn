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
        [SerializeField, Range(0, 31)] private int previewLayer = 30;

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
            ("", "Tất cả"), ("head", "Mũ"), ("top", "Áo"), ("bottom", "Quần/Váy"), ("gloves", "Găng"),
            ("shoes", "Giày"), ("accessory", "Phụ kiện"), ("back", "Choàng/Cánh"),
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
            if (PreviewModel) Destroy(PreviewModel);
            PreviewModel = null;
            PreviewOutfit = null;
            if (previewCam) previewCam.enabled = false;
            setPlayerInput?.Invoke(true);
            Closed?.Invoke();
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
                if (CurrentTab != Tab.Premium && SlotFilter.Length > 0 && i.slot != SlotFilter) return false;
                if (!AllClasses && !i.usableByCharacter) return false;
                return true;
            });
        }

        private void Refresh()
        {
            foreach (var kv in tabs) Tint(kv.Value, kv.Key == CurrentTab ? (kv.Key == Tab.Premium ? Plum : Teal) : Muted);
            foreach (var kv in chips)
            {
                kv.Value.gameObject.SetActive(CurrentTab != Tab.Premium);
                Tint(kv.Value, kv.Key == SlotFilter ? Gold * 0.85f : new Color(0.2f, 0.24f, 0.34f));
            }
            classToggleText.text = AllClasses ? "Nghề: Tất cả" : "Nghề: " + HeroClassCatalog.Get(heroClass).displayName;
            wallet.text = Wardrobe == null ? "" : $"<color=#FFD873>Vàng {Money(Wardrobe.gold)}</color>    <color=#9FE6FF>Kim Cương {Money(Wardrobe.diamonds)}</color>";

            foreach (Transform c in grid) Destroy(c.gameObject);
            cards.Clear();
            var list = Visible().ToList();
            CosmeticLibrary lib = CosmeticLibrary.Load();
            foreach (WardrobeItem it in list) cards[it.id] = Card(it, lib.Get(it.id));
            emptyText.gameObject.SetActive(Wardrobe != null && list.Count == 0);
            emptyText.text = CurrentTab == Tab.Wardrobe ? "Tủ đồ còn trống — hãy ghé gian Thời Trang nhé!"
                : CurrentTab == Tab.Premium ? "Chưa có skin đặc biệt nào đạt chuẩn để mở bán." : "Không có món nào trong mục này.";
            if (Selected != null && !list.Any(i => i.id == Selected)) Selected = null;
            if (Selected == null && list.Count > 0 && CurrentTab != Tab.Wardrobe) Select(list[0].id, false);
            ShowDetail();
            RefreshButtons();
        }

        private Button Card(WardrobeItem it, CosmeticArt art)
        {
            var go = new GameObject("Card_" + it.id, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(grid, false);
            var img = go.AddComponent<Image>();
            img.sprite = panelSprite; img.type = Image.Type.Sliced;
            img.color = it.IsSet ? new Color(0.24f, 0.18f, 0.36f) : new Color(0.15f, 0.2f, 0.3f);
            var b = go.AddComponent<Button>();
            b.onClick.AddListener(() => Select(it.id, true));
            Rim(go.transform, 0.75f);
            var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            icon.transform.SetParent(go.transform, false);
            Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(128f, 128f));
            icon.sprite = art?.icon; icon.preserveAspect = true; icon.raycastTarget = false;
            Label(go.transform, "Name", it.name, 20, Cream, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(178f, 48f), true).resizeTextForBestFit = true;
            string price = it.owned ? (it.equipped ? "Đang mặc" : "Đã sở hữu") : (it.ForDiamonds ? $"{Money(it.price)} KC" : $"{Money(it.price)} Vàng");
            Color pc = it.owned ? new Color(0.6f, 1f, 0.7f) : it.ForDiamonds ? new Color(0.62f, 0.9f, 1f) : Gold;
            Label(go.transform, "Price", price, 19, pc, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(178f, 30f), true);
            if (!it.usableByCharacter)
                Label(go.transform, "Class", "Nghề khác", 16, new Color(1f, 0.6f, 0.55f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(170f, 24f), true);
            return b;
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
            foreach (var kv in cards)
                if (kv.Value) kv.Value.GetComponent<Image>().color = kv.Key == id ? new Color(0.32f, 0.42f, 0.55f) :
                    (Wardrobe?.Find(kv.Key)?.IsSet == true ? new Color(0.24f, 0.18f, 0.36f) : new Color(0.15f, 0.2f, 0.3f));
            ShowDetail();
            RefreshButtons();
        }

        private bool IsTried(WardrobeItem it) => it.IsSet ? TryOn.set == it.id : TryOn.Get(it.slot) == it.id;
        private bool IsWorn(WardrobeItem it) => Wardrobe != null && (it.IsSet ? Wardrobe.outfit.set == it.id : Wardrobe.outfit.Get(it.slot) == it.id);

        private void ShowDetail()
        {
            WardrobeItem it = Selected != null ? Wardrobe?.Find(Selected) : null;
            detailIcon.enabled = it != null;
            if (it == null)
            {
                detailName.text = Wardrobe == null ? "" : "Chọn một món để xem và mặc thử";
                detailDesc.text = detailPrice.text = "";
                return;
            }
            detailIcon.sprite = CosmeticLibrary.Load()?.Get(it.id)?.icon;
            detailName.text = it.name;
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

            root = Panel(canvasGo.transform, "Shop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.07f, 0.72f), null);
            Stretch(root.GetComponent<RectTransform>());
            var win = Panel(root.transform, "Window", new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f), Vector2.zero, Vector2.zero, Ink, frameSprite ? frameSprite : panelSprite);
            var wr = win.GetComponent<RectTransform>();
            wr.offsetMin = wr.offsetMax = Vector2.zero;
            if (frameSprite) { var wi = win.GetComponent<Image>(); wi.type = Image.Type.Sliced; wi.pixelsPerUnitMultiplier = 2.6f; }

            // title banner + wallet + close
            var banner = Panel(win.transform, "Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(560f, 64f), new Color(0.22f, 0.36f, 0.42f), panelSprite);
            Rim(banner.transform);
            title = Label(banner.transform, "Title", "", 30, Gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 60f), true);
            wallet = Label(win.transform, "Wallet", "", 26, Cream, new Vector2(1f, 1f), new Vector2(-420f, -64f), new Vector2(560f, 40f), true); // ends left of the close button
            wallet.alignment = TextAnchor.MiddleRight;
            wallet.supportRichText = true;
            var close = Btn(win.transform, "Close", "✕", new Vector2(1f, 1f), new Vector2(-62f, -60f), new Vector2(64f, 64f), Rose);
            close.onClick.AddListener(Close);

            // ---- left: fitting room
            var left = Panel(win.transform, "FittingRoom", new Vector2(0f, 0f), new Vector2(0.34f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f), null);
            var lr = left.GetComponent<RectTransform>();
            lr.offsetMin = new Vector2(70f, 60f); lr.offsetMax = new Vector2(-10f, -100f);
            var stageBg = Panel(left.transform, "StageBg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.16f, 0.22f, 0.32f), panelSprite);
            var sr = stageBg.GetComponent<RectTransform>();
            sr.offsetMin = new Vector2(0f, 96f); sr.offsetMax = Vector2.zero;
            Rim(stageBg.transform, 0.8f);
            previewRT = new RenderTexture(640, 820, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "FittingRoomRT" };
            previewImage = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            previewImage.transform.SetParent(stageBg.transform, false);
            Stretch(previewImage.rectTransform, 8f);
            previewImage.texture = previewRT;
            previewImage.gameObject.AddComponent<DragRotate>().shop = this;
            var fitter = previewImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 640f / 820f;
            Label(stageBg.transform, "Hint", "Kéo để xoay · Phòng thử đồ", 18, new Color(0.85f, 0.9f, 1f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(420f, 28f), false);
            string[] poses = { "Idle", "Run", "Jump", "Attack" };
            string[] poseNames = { "Đứng", "Chạy", "Nhảy", "Đánh" };
            for (int i = 0; i < poses.Length; i++)
            {
                string p = poses[i];
                var b = Btn(left.transform, "Pose_" + p, poseNames[i], new Vector2((i + 0.5f) / poses.Length, 0f), new Vector2(0f, 40f), new Vector2(118f, 66f), new Color(0.25f, 0.33f, 0.46f));
                b.onClick.AddListener(() => PlayPose(p));
            }

            // stage: far below the world, own layer, own key light
            stage = new GameObject("FittingRoomStage").transform;
            stage.SetParent(transform, false);
            stage.position = new Vector3(0f, -400f, 0f);
            var camGo = new GameObject("FittingRoomCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = stage.position + new Vector3(0f, 0.6f, -3.7f);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            previewCam = camGo.AddComponent<Camera>();
            previewCam.cullingMask = 1 << previewLayer;
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0.16f, 0.22f, 0.32f, 1f);
            previewCam.fieldOfView = 22f;
            previewCam.targetTexture = previewRT;
            previewCam.enabled = false;
            var key = new GameObject("FittingRoomLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.cullingMask = 1 << previewLayer;
            key.transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(floor.GetComponent<Collider>());
            floor.name = "FittingRoomFloor";
            floor.layer = previewLayer;
            floor.transform.SetParent(transform, false);
            floor.transform.position = stage.position + new Vector3(0f, -0.03f, 0f);
            floor.transform.localScale = new Vector3(0.95f, 0.03f, 0.95f);
            Shader toon = Shader.Find("AdventureKing/Toon");
            if (toon)
            {
                var fm = new Material(toon);
                fm.SetColor("_BaseColor", new Color(0.36f, 0.32f, 0.5f));
                fm.SetFloat("_OutlineWidth", 0f);
                floor.GetComponent<Renderer>().sharedMaterial = fm;
            }

            // ---- right: tabs, filters, grid, details
            var right = Panel(win.transform, "Catalogue", new Vector2(0.34f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), null);
            var rr = right.GetComponent<RectTransform>();
            rr.offsetMin = new Vector2(10f, 60f); rr.offsetMax = new Vector2(-70f, -100f);
            (Tab t, string label)[] tabDefs = { (Tab.Gold, "Thời Trang · Vàng"), (Tab.Premium, "Skin Đặc Biệt · KC"), (Tab.Wardrobe, "Tủ Đồ Của Tôi") };
            for (int i = 0; i < tabDefs.Length; i++)
            {
                Tab t = tabDefs[i].t;
                var b = Btn(right.transform, "Tab_" + t, tabDefs[i].label, new Vector2(0f, 1f), new Vector2(150f + i * 290f, -30f), new Vector2(280f, 60f), Muted);
                b.onClick.AddListener(() => SetTab(t));
                tabs[t] = b;
            }
            var classBtn = Btn(right.transform, "ClassFilter", "", new Vector2(1f, 1f), new Vector2(-130f, -30f), new Vector2(250f, 56f), new Color(0.3f, 0.3f, 0.4f));
            classToggleText = classBtn.GetComponentInChildren<Text>();
            classBtn.onClick.AddListener(ToggleAllClasses);
            for (int i = 0; i < SlotChips.Length; i++)
            {
                string slot = SlotChips[i].slot;
                var b = Btn(right.transform, "Chip_" + (slot.Length == 0 ? "all" : slot), SlotChips[i].label, new Vector2(0f, 1f),
                    new Vector2(70f + i * 133f, -100f), new Vector2(126f, 46f), new Color(0.2f, 0.24f, 0.34f), 19);
                b.onClick.AddListener(() => SetSlotFilter(slot));
                chips[slot] = b;
            }

            // grid (scrollable)
            var view = Panel(right.transform, "Grid", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.12f, 0.55f), panelSprite);
            var vr = view.GetComponent<RectTransform>();
            vr.offsetMin = new Vector2(0f, 250f); vr.offsetMax = new Vector2(0f, -132f);
            view.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(view.transform, false);
            grid = (RectTransform)content.transform;
            grid.anchorMin = new Vector2(0f, 1f); grid.anchorMax = new Vector2(1f, 1f); grid.pivot = new Vector2(0.5f, 1f);
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            var gl = content.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(190f, 230f);
            gl.spacing = new Vector2(14f, 14f);
            gl.padding = new RectOffset(14, 14, 14, 14);
            gl.childAlignment = TextAnchor.UpperCenter;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            emptyText = Label(view.transform, "Empty", "", 24, new Color(0.8f, 0.85f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 80f), false);

            // details
            var det = Panel(right.transform, "Detail", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero, new Color(0.1f, 0.13f, 0.22f), panelSprite);
            var dr = det.GetComponent<RectTransform>();
            dr.pivot = new Vector2(0.5f, 0f);
            dr.sizeDelta = new Vector2(0f, 236f);
            dr.anchoredPosition = Vector2.zero;
            Rim(det.transform, 0.8f);
            detailIcon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            detailIcon.transform.SetParent(det.transform, false);
            Place(detailIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 150f));
            detailIcon.preserveAspect = true;
            detailName = Label(det.transform, "Name", "", 28, Gold, new Vector2(0f, 1f), new Vector2(430f, -40f), new Vector2(480f, 44f), true);
            detailName.alignment = TextAnchor.MiddleLeft;
            detailDesc = Label(det.transform, "Desc", "", 20, Cream, new Vector2(0f, 1f), new Vector2(430f, -100f), new Vector2(480f, 80f), false); // stays above the button row
            detailDesc.alignment = TextAnchor.UpperLeft;
            detailDesc.supportRichText = true;
            detailDesc.resizeTextForBestFit = true; detailDesc.resizeTextMinSize = 15; detailDesc.resizeTextMaxSize = 20;
            detailDesc.verticalOverflow = VerticalWrapMode.Truncate;
            detailPrice = Label(det.transform, "Price", "", 28, Gold, new Vector2(1f, 1f), new Vector2(-200f, -40f), new Vector2(360f, 44f), true);
            detailPrice.supportRichText = true;
            tryBtn = Btn(det.transform, "TryOn", "Mặc thử", new Vector2(1f, 0f), new Vector2(-560f, 56f), new Vector2(170f, 70f), new Color(0.3f, 0.42f, 0.58f));
            tryBtnText = tryBtn.GetComponentInChildren<Text>();
            tryBtn.onClick.AddListener(ToggleTry);
            buyBtn = Btn(det.transform, "Buy", "Mua", new Vector2(1f, 0f), new Vector2(-375f, 56f), new Vector2(170f, 70f), new Color(0.78f, 0.55f, 0.18f));
            buyBtn.onClick.AddListener(AskBuy);
            wearBtn = Btn(det.transform, "Wear", "Mặc", new Vector2(1f, 0f), new Vector2(-375f, 56f), new Vector2(170f, 70f), Teal);
            wearBtnText = wearBtn.GetComponentInChildren<Text>();
            wearBtn.onClick.AddListener(ToggleWear);
            saveBtn = Btn(det.transform, "SaveLook", "Lưu bộ phối", new Vector2(1f, 0f), new Vector2(-150f, 56f), new Vector2(230f, 70f), new Color(0.32f, 0.52f, 0.36f));
            saveBtn.onClick.AddListener(SaveTryOn);
            status = Label(win.transform, "Status", "", 22, new Color(1f, 0.93f, 0.7f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1500f, 34f), false);

            // purchase confirmation
            confirm = Panel(root.transform, "Confirm", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.5f), null);
            Stretch(confirm.GetComponent<RectTransform>());
            var card = Panel(confirm.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 340f), Ink, frameSprite ? frameSprite : panelSprite);
            if (frameSprite) { var ci = card.GetComponent<Image>(); ci.type = Image.Type.Sliced; ci.pixelsPerUnitMultiplier = 2.8f; }
            Label(card.transform, "Title", "Xác nhận mua", 30, Gold, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(600f, 48f), true);
            confirmText = Label(card.transform, "Text", "", 28, Cream, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(620f, 100f), false);
            confirmText.supportRichText = true;
            confirmYes = Btn(card.transform, "Yes", "Mua", new Vector2(0.5f, 0f), new Vector2(-140f, 56f), new Vector2(230f, 66f), new Color(0.78f, 0.55f, 0.18f));
            confirmYes.onClick.AddListener(ConfirmYes);
            var no = Btn(card.transform, "No", "Huỷ", new Vector2(0.5f, 0f), new Vector2(140f, 56f), new Vector2(230f, 66f), Muted);
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
            Rim(go.transform);
            if (!string.IsNullOrEmpty(label) || name == "ClassFilter")
            {
                var t = Label(go.transform, "Text", label, fontSize > 0 ? fontSize : size.y > 60 ? 25 : 21, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 4f), true);
                t.resizeTextForBestFit = true;
            }
            return b;
        }

        private static void Tint(Button b, Color c)
        {
            if (b) b.GetComponent<Image>().color = c;
        }
    }
}
