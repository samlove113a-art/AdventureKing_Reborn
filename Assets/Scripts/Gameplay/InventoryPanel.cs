using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Túi Đồ" window: the server bag (40 cells, stacks up to 99 with the count on the cell), the three equipment slots
    /// (Vũ khí / Áo / Phụ kiện) with the total attack/defence bonus, and a detail area for the selected item with the
    /// real actions: Dùng (potions), Trang bị / Tháo (gear). Every action is a server request; the window only shows the
    /// server's answer. Opened with I or the bag button. Built at runtime from the HUD's frame sprites.
    /// </summary>
    public sealed class InventoryPanel : MonoBehaviour
    {
        [Serializable] public struct IconEntry { public string name; public Sprite sprite; }
        [SerializeField] private IconEntry[] icons = new IconEntry[0];
        [SerializeField] private Sprite panelSprite, cellSprite, buttonSprite;
        [SerializeField] private Font font;

        public InventoryState State { get; private set; }
        public bool IsOpen => root && root.activeSelf;
        public int SelectedIndex { get; private set; } = -1;
        public string LastMessage { get; private set; } = "";
        public bool Busy { get; private set; }

        private GameObject root;
        private readonly List<(Image bg, Image icon, Text qty)> cells = new List<(Image, Image, Text)>();
        private readonly Dictionary<string, (Image icon, Text label)> equipCells = new Dictionary<string, (Image, Text)>();
        private Text statsLabel, nameLabel, descLabel, msgLabel, powerLabel;
        private Image characterPreview;
        private int targetPower, displayedPower;
        private float powerPulseUntil;
        private bool useProtection;
        private string pendingEnhanceId, pendingEnhanceSlot;
        private bool pendingEnhanceProtect;
        private Button enhanceButton, protectionButton;
        private Text enhanceLabel, protectionLabel;
        private RectTransform responsiveCard;
        private readonly List<(Outline edge, Color color)> rareItemEdges = new List<(Outline, Color)>();
        private Button useButton, equipButton, unequipButton;
        private string selectedEquip;
        private AdventureWorldSession world;
        private float nextRefreshTry;

        private static readonly (string slot, string label)[] EquipSlots = { ("weapon", "Vũ khí"), ("armor", "Áo"), ("accessory", "Phụ kiện") };
        private const int Columns = 6, MaxCells = 99;
        private RectTransform content;
        private ScrollRect scroll;
        private Button expandButton;
        private Text expandLabel;

        /// <summary>Buys one more bag cell with Gold (the server sets the price; up to 99 cells).</summary>
        public void Expand()
        {
            if (Busy || State == null || !world || world.Character == null) return;
            Busy = true;
            AdventureServerConnection.Instance.ExpandBag(world.Character.characterId, (ok, r, err) =>
            {
                Busy = false;
                if (!ok || r == null) { Message(err); return; }
                world.Character.gold = r.gold;
                Apply(r.inventory);
                Message($"Đã mở thêm 1 ô (−{r.priceGold:N0} Vàng). Túi: {r.inventory.capacity} ô.");
            });
        }

        private Sprite Icon(string name)
        {
            Sprite s = icons.FirstOrDefault(i => i.name == name).sprite;
            return s ? s : (string.IsNullOrEmpty(name) ? null : Resources.Load<Sprite>("ItemIcons/" + name)); // icons added after the scene was built
        }
        /// <summary>Item icon by its server icon key (shared with the reward windows).</summary>
        public Sprite IconSprite(string name) => Icon(name);

        private void Start() => Build();

        private void Update()
        {
            if (root && root.activeSelf)
            {
                if (responsiveCard)
                {
                    var viewport = ((RectTransform)root.transform).rect;
                    float scale = Mathf.Min(1f, Mathf.Min((viewport.width - 24f) / 1280f, (viewport.height - 24f) / 800f));
                    responsiveCard.localScale = Vector3.one * Mathf.Clamp(scale, 0.28f, 1f);
                }
                foreach (var rare in rareItemEdges)
                {
                    if (!rare.edge) continue;
                    Color c = rare.color;
                    c.a = 0.7f + 0.3f * Mathf.PingPong(Time.unscaledTime * 1.8f, 1f);
                    rare.edge.effectColor = c;
                }
                if (powerLabel)
                {
                    displayedPower = Mathf.RoundToInt(Mathf.MoveTowards(displayedPower, targetPower,
                        Time.unscaledDeltaTime * Mathf.Max(220f, Mathf.Abs(targetPower - displayedPower) * 7f)));
                    powerLabel.text = $"TỔNG LỰC CHIẾN  {displayedPower:N0}";
                    powerLabel.color = Time.unscaledTime < powerPulseUntil
                        ? Color.Lerp(new Color(1f, 0.75f, 0.2f), Color.white, Mathf.PingPong(Time.unscaledTime * 6f, 1f))
                        : new Color(1f, 0.83f, 0.4f);
                }
                if (characterPreview && world && world.HeroModel)
                {
                    SpriteRenderer hero = world.HeroModel.GetComponentInChildren<SpriteRenderer>();
                    characterPreview.sprite = hero ? hero.sprite : null;
                    characterPreview.enabled = characterPreview.sprite;
                }
            }
            if (!world) world = AdventureWorldSession.Current;
            if (State == null && !Busy && world && world.Character != null && AdventureServerConnection.Instance && Time.time >= nextRefreshTry)
            {
                nextRefreshTry = Time.time + 3f;
                Refresh();
            }
        }

        // ------------------------------------------------------------------ server
        public void Refresh(Action done = null)
        {
            if (!world) world = AdventureWorldSession.Current;
            if (!world || world.Character == null || !AdventureServerConnection.Instance) return;
            Busy = true;
            AdventureServerConnection.Instance.GetInventory(world.Character.characterId, (ok, inv, err) =>
            {
                Busy = false;
                if (ok && inv != null) Apply(inv); else LastMessage = err;
                done?.Invoke();
            });
        }

        private void Apply(InventoryState inv)
        {
            bool changed = State != null && inv != null && inv.combatPower != State.combatPower;
            bool wasReady = State != null && State.slots.Any(x => x.itemId == "enhance_stone") &&
                State.equipped != null && State.equipped.Length > 0;
            bool isReady = inv != null && inv.slots.Any(x => x.itemId == "enhance_stone") &&
                inv.equipped != null && inv.equipped.Length > 0;
            State = inv;
            if (isReady && !wasReady) QuickGoalToast.Show("Đủ nguyên liệu cường hóa! Mở túi đồ để nâng cấp.");
            targetPower = inv != null ? inv.combatPower : 0;
            if (changed) powerPulseUntil = Time.unscaledTime + 0.85f;
            else if (displayedPower == 0) displayedPower = targetPower;
            Redraw();
        }

        public int PotionCount(bool hp) => State == null ? 0 : State.slots.Where(s => s.kind == "consumable" && s.icon == (hp ? "item_potion_hp" : "item_potion_mp")).Sum(s => s.qty);

        /// <summary>Drinks the first HP (or MP) potion stack in the bag: the server consumes exactly one and restores.</summary>
        public void UsePotion(bool hp, Action<string> say = null)
        {
            if (State == null) { say?.Invoke("Đang tải túi đồ..."); Refresh(); return; }
            BagSlotState s = State.slots.Where(x => x.kind == "consumable" && x.icon == (hp ? "item_potion_hp" : "item_potion_mp")).OrderBy(x => x.index).FirstOrDefault();
            if (s == null) { say?.Invoke(hp ? "Hết thuốc HP." : "Hết thuốc MP."); return; }
            Use(s.index, say);
        }

        private int pendingForce = -1;

        public void Use(int slot, Action<string> say = null, bool force = false)
        {
            if (Busy || !world || world.Character == null) return;
            Busy = true;
            AdventureServerConnection.Instance.UseItem(world.Character.characterId, slot, (ok, r, err) =>
            {
                Busy = false;
                if (!ok || r == null)
                {
                    // a full bar: the bag window offers to drink anyway (wasteful, so only on a second, deliberate press)
                    if (say == null && err != null && err.Contains("đầy")) { pendingForce = slot; err += " Bấm Dùng lần nữa nếu vẫn muốn uống."; }
                    Message(err); say?.Invoke(err); return;
                }
                pendingForce = -1;
                if (r.inventory != null) Apply(r.inventory);
                ApplyHero(r.hero);
                string done = r.restoredHp > 0 ? $"+{r.restoredHp} HP" : $"+{r.restoredMp} MP";
                Message(done);
                say?.Invoke(done);
            }, force);
        }

        private void ApplyHero(HeroCombatState h)
        {
            if (h == null) return;
            if (FarmField.Current) { FarmField.Current.ApplyHero(h); return; }
            if (!world || !world.Player) return;
            var health = world.Player.GetComponent<Health2D>();
            if (health && h.maxHp > 0) health.SetFromServer(h.hp, h.maxHp);
            var mana = world.Player.GetComponent<PlayerMana>();
            if (mana && h.maxMp > 0) mana.SetFromServer(h.mp, h.maxMp);
        }

        /// <summary>"Dùng": a potion is drunk; a ticket asks to confirm, then the server removes it and moves the hero.</summary>
        public void UseSelected()
        {
            if (State == null || SelectedIndex < 0) return;
            BagSlotState s = State.slots.FirstOrDefault(x => x.index == SelectedIndex);
            if (s == null) return;
            if (s.kind != "ticket") { Use(SelectedIndex, null, pendingForce == SelectedIndex); return; }
            if (PendingTicket == SelectedIndex) { ConfirmTicket(); return; }
            PendingTicket = SelectedIndex;
            Message($"Dùng 1 {s.name}? Bấm Dùng lần nữa để xác nhận.");
            if (useLabel) useLabel.text = "Xác nhận";
        }

        public int PendingTicket { get; private set; } = -1;
        private Text useLabel;

        /// <summary>Second press on a ticket: travel (the window closes; on failure the ticket stays in the bag).</summary>
        public void ConfirmTicket()
        {
            if (PendingTicket < 0 || !world) return;
            int slot = PendingTicket;
            PendingTicket = -1;
            Close();
            world.UseTicket(slot, err => { Open(); Message(err); });
        }

        public void EquipSelected()
        {
            if (Busy || State == null || SelectedIndex < 0) return;
            Busy = true;
            AdventureServerConnection.Instance.EquipItem(world.Character.characterId, SelectedIndex, (ok, inv, err) =>
            {
                Busy = false;
                if (!ok || inv == null) { Message(err); return; }
                Apply(inv);
                SelectedIndex = -1;
                Message("Đã trang bị.");
            });
        }

        public void Unequip(string equipSlot)
        {
            if (Busy || State == null) return;
            Busy = true;
            AdventureServerConnection.Instance.UnequipItem(world.Character.characterId, equipSlot, (ok, inv, err) =>
            {
                Busy = false;
                if (!ok || inv == null) { Message(err); return; }
                Apply(inv);
                selectedEquip = null;
                Message("Đã tháo trang bị.");
            });
        }

        private static Color TierColor(string rarity)
        {
            switch ((rarity ?? "white").ToLowerInvariant())
            {
                case "blue": return new Color(0.3f, 0.65f, 1f);
                case "purple": return new Color(0.74f, 0.45f, 1f);
                case "orange": return new Color(1f, 0.62f, 0.22f);
                case "red": return new Color(1f, 0.29f, 0.36f);
                default: return new Color(0.92f, 0.93f, 0.97f);
            }
        }

        private void ToggleProtection()
        {
            useProtection = !useProtection;
            pendingEnhanceId = null;
            Redraw();
        }

        public void EnhanceSelected()
        {
            if (Busy || State == null || string.IsNullOrEmpty(selectedEquip) || !world || world.Character == null
                || !AdventureServerConnection.Instance) return;
            if (pendingEnhanceId == null || pendingEnhanceSlot != selectedEquip || pendingEnhanceProtect != useProtection)
            {
                pendingEnhanceId = Guid.NewGuid().ToString("N");
                pendingEnhanceSlot = selectedEquip;
                pendingEnhanceProtect = useProtection;
            }
            Busy = true;
            AdventureServerConnection.Instance.EnhanceItem(world.Character.characterId, selectedEquip, useProtection, pendingEnhanceId, (ok, r, err) =>
            {
                Busy = false;
                if (!ok || r == null) { Message(err ?? "Mất kết nối. Nhấn lại để kiểm tra cùng giao dịch."); return; }
                pendingEnhanceId = null; // confirmed receipt; next click is a new attempt
                Apply(r.inventory);
                Message(r.success ? $"Cường hóa thành công +{r.afterLevel}!" :
                    r.afterLevel < r.beforeLevel ? $"Thất bại, tụt còn +{r.afterLevel}." : "Thất bại, giữ nguyên cấp.");
            });
        }

        private void Message(string text) { LastMessage = text ?? ""; if (msgLabel) msgLabel.text = LastMessage; }

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open() { if (!root) Build(); root.SetActive(true); transform.SetAsLastSibling(); Refresh(); }
        public void Close() { if (root) root.SetActive(false); }

        public void Select(int slot) { pendingEnhanceId = null; SelectedIndex = slot; selectedEquip = null; PendingTicket = -1; pendingForce = -1; if (useLabel) useLabel.text = "Dùng"; Redraw(); }
        public void SelectEquip(string equipSlot) { pendingEnhanceId = null; selectedEquip = equipSlot; SelectedIndex = -1; Redraw(); }

        // ------------------------------------------------------------------ UI
        private T Make<T>(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<T>();
        }

        private Text Txt(Transform parent, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align)
        {
            Text t = Make<Text>(parent, name, anchor, pos, box);
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color tint, Action onClick)
        {
            Image img = Make<Image>(parent, name, anchor, pos, size);
            img.sprite = buttonSprite;
            img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = tint;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            Text t = Txt(img.transform, "Label", label, 24, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            return b;
        }

        private void Build()
        {
            if (root) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image dim = Make<Image>(transform, "InventoryRoot", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var drt = (RectTransform)root.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(0f, 0f, 0f, 0.5f);

            Image card = Make<Image>(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f, 800f));
            card.sprite = panelSprite;
            card.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple;
            card.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.97f);
            Transform c = card.transform;
            responsiveCard = card.rectTransform;
            Color gold = new Color(1f, 0.85f, 0.45f), cream = new Color(1f, 0.96f, 0.88f);
            Text title = Txt(c, "Title", "NHÂN VẬT  •  TÚI ĐỒ", 37, gold, new Vector2(0.5f, 1f), new Vector2(0f, -63f), new Vector2(720f, 56f), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Btn(c, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(-120f, -56f), new Vector2(150f, 62f), new Color(0.55f, 0.3f, 0.3f), Close);

            // Left paperdoll panel. Existing 3 server-owned slots remain functional;
            // future slots are explicitly locked instead of inventing equipment on the client.
            Txt(c, "EquipTitle", "Nhân vật", 28, gold, new Vector2(0f, 1f), new Vector2(275f, -127f), new Vector2(380f, 44f), TextAnchor.MiddleCenter);
            powerLabel = Txt(c, "CombatPower", "TỔNG LỰC CHIẾN", 28, gold,
                new Vector2(0f, 1f), new Vector2(275f, -176f), new Vector2(490f, 48f), TextAnchor.MiddleCenter);
            powerLabel.fontStyle = FontStyle.Bold;

            Image stage = Make<Image>(c, "PaperdollPodium", new Vector2(0f, 1f), new Vector2(278f, -412f), new Vector2(256f, 318f));
            stage.color = new Color(0.19f, 0.22f, 0.38f, 0.89f);
            Image glow = Make<Image>(stage.transform, "PodiumGlow", new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(190f, 27f));
            glow.color = new Color(0.48f, 0.65f, 1f, 0.55f); glow.raycastTarget = false;
            characterPreview = Make<Image>(stage.transform, "Hero2DPreview", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 18f), new Vector2(235f, 260f));
            characterPreview.preserveAspect = true; characterPreview.raycastTarget = false; characterPreview.enabled = false;

            var realPositions = new [] { new Vector2(88f, -277f), new Vector2(464f, -277f), new Vector2(88f, -448f) };
            for (int i = 0; i < EquipSlots.Length; i++)
            {
                var (slot, label) = EquipSlots[i];
                Image cell = Make<Image>(c, "Equip_" + slot, new Vector2(0f, 1f), realPositions[i], new Vector2(96f, 96f));
                cell.sprite = cellSprite; cell.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple;
                cell.color = new Color(0.4f, 0.45f, 0.62f);
                cell.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectEquip(slot));
                var border = cell.gameObject.AddComponent<Outline>(); border.effectColor = gold;
                border.effectDistance = new Vector2(2f, -2f);
                Image ic = Make<Image>(cell.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(77f, 77f));
                ic.preserveAspect = true; ic.raycastTarget = false; ic.enabled = false;
                Text labelText = Txt(cell.transform, "Label", label, 18, cream,
                    new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(154f, 29f), TextAnchor.MiddleCenter);
                equipCells[slot] = (ic, labelText);
            }

            string[] unavailable = { "Nón", "Giày", "Dây chuyền", "Pet", "Hóa thân" };
            var lockedPositions = new [] {
                new Vector2(464f, -448f), new Vector2(88f, -574f), new Vector2(464f, -574f),
                new Vector2(190f, -614f), new Vector2(365f, -614f)
            };
            for (int i = 0; i < unavailable.Length; i++)
            {
                Image locked = Make<Image>(c, "Locked_" + unavailable[i], new Vector2(0f, 1f), lockedPositions[i], new Vector2(88f, 73f));
                locked.sprite = cellSprite; locked.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple;
                locked.color = new Color(0.3f, 0.32f, 0.38f, 0.62f);
                Txt(locked.transform, "LockedLabel", unavailable[i] + "\n(chưa mở)", 14,
                    new Color(0.73f, 0.73f, 0.77f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 67f), TextAnchor.MiddleCenter);   // stays inside its 88 px cell (was 116 px wide: it spilled over the item name)
            }
            statsLabel = Txt(c, "Stats", "", 19, cream, new Vector2(0f, 0f),
                new Vector2(270f, 60f), new Vector2(360f, 49f), TextAnchor.MiddleCenter);

            // bag grid (right): a scroll view, so 40 base cells and up to 99 bought cells share one layout
            const float cell0 = 75f, gap = 8f;
            Image viewport = Make<Image>(c, "BagViewport", new Vector2(0f, 1f), Vector2.zero, new Vector2(Columns * (cell0 + gap), 470f));
            var vrt = viewport.rectTransform; vrt.pivot = new Vector2(0f, 1f); vrt.anchoredPosition = new Vector2(650f, -150f);
            viewport.color = new Color(0f, 0f, 0f, 0.01f); // (needs a graphic so the mask and the drag area exist)
            viewport.gameObject.AddComponent<RectMask2D>();
            var contentGo = new GameObject("BagContent", typeof(RectTransform)) { layer = 5 }; // (AddComponent<RectTransform> on a UI object returns null)
            contentGo.transform.SetParent(viewport.transform, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(Columns * (cell0 + gap), 5 * (cell0 + gap));
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = vrt; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
            for (int i = 0; i < MaxCells; i++)
            {
                int index = i;
                float x = (i % Columns) * (cell0 + gap), y = -(i / Columns) * (cell0 + gap);
                Image bg = Make<Image>(content, "Cell" + i, new Vector2(0f, 1f), new Vector2(x + cell0 / 2f, y - cell0 / 2f), new Vector2(cell0, cell0));
                var edge = bg.gameObject.AddComponent<Outline>(); edge.effectDistance = new Vector2(2f, -2f); edge.effectColor = Color.white;
                bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple;
                bg.color = new Color(0.55f, 0.6f, 0.78f, 0.9f);
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(index));
                Image ic = Make<Image>(bg.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(63f, 63f));
                ic.preserveAspect = true; ic.raycastTarget = false; ic.enabled = false;
                Text q = Txt(bg.transform, "Qty", "", 22, Color.white, new Vector2(1f, 0f), new Vector2(-30f, 16f), new Vector2(60f, 28f), TextAnchor.MiddleRight);
                q.fontStyle = FontStyle.Bold;
                var o = q.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f);
                cells.Add((bg, ic, q));
            }

            // detail strip (bottom right)
            nameLabel = Txt(c, "ItemName", "Chọn một vật phẩm", 28, gold, new Vector2(0f, 0f), new Vector2(705f, 146f), new Vector2(520f, 40f), TextAnchor.MiddleLeft);   // clear of the locked slot column on the left
            nameLabel.fontStyle = FontStyle.Bold;
            descLabel = Txt(c, "ItemDesc", "", 22, cream, new Vector2(705f, 94f), new Vector2(470f, 56f), TextAnchor.UpperLeft);
            msgLabel = Txt(c, "Message", "", 22, new Color(1f, 0.75f, 0.6f), new Vector2(0f, 0f), new Vector2(645f, 39f), new Vector2(520f, 32f), TextAnchor.MiddleLeft);
            expandButton = Btn(c, "Expand", "Mở rộng túi", new Vector2(0f, 0f), new Vector2(270f, 118f), new Vector2(310f, 62f), new Color(0.82f, 0.6f, 0.18f), Expand);   // 62: still >= 40 px on the 16:9 phone scale
            expandLabel = expandButton.GetComponentInChildren<Text>();
            expandLabel.fontSize = 20;
            useButton = Btn(c, "Use", "Dùng", new Vector2(1f, 0f), new Vector2(-470f, 110f), new Vector2(180f, 64f), new Color(0.25f, 0.6f, 0.4f), UseSelected);
            equipButton = Btn(c, "Equip", "Trang bị", new Vector2(1f, 0f), new Vector2(-270f, 110f), new Vector2(180f, 64f), new Color(0.3f, 0.45f, 0.75f), EquipSelected);
            useLabel = useButton.GetComponentInChildren<Text>();
            unequipButton = Btn(c, "Unequip", "Tháo", new Vector2(1f, 0f), new Vector2(-470f, 110f), new Vector2(180f, 64f), new Color(0.6f, 0.4f, 0.25f), () => { if (selectedEquip != null) Unequip(selectedEquip); });
            enhanceButton = Btn(c, "Enhance", "Cường hóa", new Vector2(1f, 0f), new Vector2(-270f, 39f),
                new Vector2(180f, 58f), new Color(0.75f, 0.44f, 0.12f), EnhanceSelected);
            enhanceLabel = enhanceButton.GetComponentInChildren<Text>(); enhanceLabel.fontSize = 18;
            protectionButton = Btn(c, "Protect", "Bùa: TẮT", new Vector2(1f, 0f), new Vector2(-470f, 39f),
                new Vector2(180f, 58f), new Color(0.42f, 0.33f, 0.71f), ToggleProtection);
            protectionLabel = protectionButton.GetComponentInChildren<Text>(); protectionLabel.fontSize = 18;
            root.SetActive(false);
            Redraw();
        }

        private void Redraw()
        {
            if (!root) return;
            var bySlot = State?.slots?.ToDictionary(s => s.index) ?? new Dictionary<int, BagSlotState>();
            rareItemEdges.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                var (bg, icon, qty) = cells[i];
                bool used = State != null && i < State.capacity;
                bg.gameObject.SetActive(State == null ? i < 40 : used);
                if (bySlot.TryGetValue(i, out BagSlotState s))
                {
                    icon.sprite = Icon(s.icon); icon.enabled = icon.sprite;
                    icon.color = s.usableByClass ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    qty.text = s.qty > 1 ? s.qty.ToString() : "";
                }
                else { icon.enabled = false; qty.text = ""; }
                Color rarity = bySlot.TryGetValue(i, out BagSlotState held)
                    ? TierColor(held.rarity) : new Color(0.59f, 0.61f, 0.7f);
                bg.color = i == SelectedIndex ? new Color(1f, 0.85f, 0.45f)
                    : Color.Lerp(new Color(0.22f, 0.25f, 0.34f), rarity, 0.44f);
                Outline border = bg.GetComponent<Outline>();
                if (border)
                {
                    border.effectColor = rarity;
                    if (held != null && (held.rarity == "orange" || held.rarity == "red"))
                        rareItemEdges.Add((border, rarity));
                }
            }
            foreach (var (slot, _) in EquipSlots)
            {
                EquippedState e = State?.equipped?.FirstOrDefault(x => x.equipSlot == slot);
                var (ic, label) = equipCells[slot];
                ic.enabled = e != null && Icon(e.item.icon);
                if (e != null) ic.sprite = Icon(e.item.icon);
                label.text = slot == "weapon" ? "Vũ khí" : slot == "armor" ? "Áo" : "Phụ kiện";
                if (e != null && e.item.enhanceLevel > 0) label.text += " +" + e.item.enhanceLevel;
                Image cell = ic.transform.parent.GetComponent<Image>();
                if (cell) cell.color = e == null ? new Color(0.4f, 0.45f, 0.62f) :
                    Color.Lerp(new Color(0.21f, 0.22f, 0.32f), TierColor(e.item.rarity), 0.55f);
            }
            if (statsLabel) statsLabel.text = State == null ? "" : $"Tấn công +{State.attackBonus}\nPhòng thủ +{State.defenseBonus}";
            if (content) content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Ceil((State == null ? 40 : State.capacity) / (float)Columns) * 83f);
            if (expandButton)
            {
                bool canBuy = State != null && State.nextExpandGold > 0;
                expandButton.gameObject.SetActive(State != null);
                expandButton.interactable = canBuy;
                expandLabel.text = State == null ? "" : canBuy ? $"Mở rộng túi · {State.nextExpandGold:N0} Vàng ({State.capacity}/{State.maxCapacity})" : $"Túi tối đa {State.capacity} ô";
            }

            BagSlotState sel = SelectedIndex >= 0 && bySlot.TryGetValue(SelectedIndex, out var x0) ? x0 : null;
            EquippedState selEq = selectedEquip != null ? State?.equipped?.FirstOrDefault(e => e.equipSlot == selectedEquip) : null;
            if (sel != null)
            {
                nameLabel.text = sel.name + (sel.enhanceLevel > 0 ? $" +{sel.enhanceLevel}" : "") + (sel.qty > 1 ? $"  ×{sel.qty}" : "");
                nameLabel.color = TierColor(sel.rarity);
                descLabel.text = sel.desc + (sel.usableByClass ? "" : "  (Nghề của bạn không dùng được.)");
            }
            else if (selEq != null) { nameLabel.text = selEq.item.name + (selEq.item.enhanceLevel > 0 ? $" +{selEq.item.enhanceLevel}" : "") + " (đang mặc)"; nameLabel.color = TierColor(selEq.item.rarity); descLabel.text = selEq.item.desc; }
            else { nameLabel.color = new Color(1f, 0.85f, 0.45f); nameLabel.text = State == null ? "Đang tải túi đồ..." : "Chọn một vật phẩm"; descLabel.text = State == null ? "" : $"{State.slots.Length}/{State.capacity} ô đã dùng · tối đa {State.stackMax}/ô"; }
            useButton.gameObject.SetActive(sel != null && (sel.kind == "consumable" || sel.kind == "ticket"));
            equipButton.gameObject.SetActive(sel != null && sel.kind == "equipment" && sel.usableByClass);
            unequipButton.gameObject.SetActive(selEq != null);
            int stones = State == null ? 0 : State.slots.Where(x => x.itemId == "enhance_stone").Sum(x => x.qty);
            int charms = State == null ? 0 : State.slots.Where(x => x.itemId == "protection_charm").Sum(x => x.qty);
            if (enhanceButton)
            {
                enhanceButton.gameObject.SetActive(selEq != null);
                enhanceButton.interactable = selEq != null && stones > 0 && selEq.item.enhanceLevel < 15
                    && !Busy && (!useProtection || charms > 0);
                enhanceLabel.text = selEq == null ? "" : $"Lên +{selEq.item.enhanceLevel + 1} · {selEq.item.enhanceChance}% ({stones} đá)";
            }
            if (protectionButton)
            {
                protectionButton.gameObject.SetActive(selEq != null);
                protectionButton.interactable = charms > 0 && !Busy;
                protectionLabel.text = $"Bùa {(useProtection ? "BẬT" : "TẮT")} ({charms})";
            }
        }
    }
}
