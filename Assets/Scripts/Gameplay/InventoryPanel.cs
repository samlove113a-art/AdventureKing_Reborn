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
        private Text statsLabel, nameLabel, descLabel, msgLabel;
        private Button useButton, equipButton, unequipButton;
        private string selectedEquip;
        private AdventureWorldSession world;
        private float nextRefreshTry;

        private static readonly (string slot, string label)[] EquipSlots = { ("weapon", "Vũ khí"), ("armor", "Áo"), ("accessory", "Phụ kiện") };
        private const int Columns = 8;

        private Sprite Icon(string name) => icons.FirstOrDefault(i => i.name == name).sprite;

        private void Start() => Build();

        private void Update()
        {
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
            State = inv;
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

        private void Message(string text) { LastMessage = text ?? ""; if (msgLabel) msgLabel.text = LastMessage; }

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open() { if (!root) Build(); root.SetActive(true); transform.SetAsLastSibling(); Refresh(); }
        public void Close() { if (root) root.SetActive(false); }

        public void Select(int slot) { SelectedIndex = slot; selectedEquip = null; PendingTicket = -1; pendingForce = -1; if (useLabel) useLabel.text = "Dùng"; Redraw(); }
        public void SelectEquip(string equipSlot) { selectedEquip = equipSlot; SelectedIndex = -1; Redraw(); }

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
            Color gold = new Color(1f, 0.85f, 0.45f), cream = new Color(1f, 0.96f, 0.88f);
            Text title = Txt(c, "Title", "Túi Đồ", 40, gold, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(600f, 56f), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Btn(c, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(-120f, -56f), new Vector2(150f, 62f), new Color(0.55f, 0.3f, 0.3f), Close);

            // equipment column (left)
            Txt(c, "EquipTitle", "Trang bị", 28, gold, new Vector2(0f, 1f), new Vector2(170f, -120f), new Vector2(260f, 40f), TextAnchor.MiddleCenter);
            for (int i = 0; i < EquipSlots.Length; i++)
            {
                var (slot, label) = EquipSlots[i];
                Image cell = Make<Image>(c, "Equip_" + slot, new Vector2(0f, 1f), new Vector2(170f, -215f - i * 150f), new Vector2(120f, 120f));
                cell.sprite = cellSprite; cell.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple;
                cell.color = new Color(0.75f, 0.8f, 1f);
                cell.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectEquip(slot));
                Image ic = Make<Image>(cell.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
                ic.preserveAspect = true; ic.raycastTarget = false; ic.enabled = false;
                Text lab = Txt(cell.transform, "Label", label, 20, cream, new Vector2(0.5f, 0f), new Vector2(0f, -18f), new Vector2(200f, 28f), TextAnchor.MiddleCenter);
                equipCells[slot] = (ic, lab);
            }
            statsLabel = Txt(c, "Stats", "", 22, cream, new Vector2(0f, 0f), new Vector2(170f, 70f), new Vector2(300f, 60f), TextAnchor.MiddleCenter);

            // bag grid (right)
            const float cell0 = 104f, gap = 8f;
            for (int i = 0; i < 40; i++)
            {
                int index = i;
                float x = 360f + (i % Columns) * (cell0 + gap), y = -150f - (i / Columns) * (cell0 + gap);
                Image bg = Make<Image>(c, "Cell" + i, new Vector2(0f, 1f), new Vector2(x + cell0 / 2f, y - cell0 / 2f + 40f), new Vector2(cell0, cell0));
                bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple;
                bg.color = new Color(0.55f, 0.6f, 0.78f, 0.9f);
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(index));
                Image ic = Make<Image>(bg.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
                ic.preserveAspect = true; ic.raycastTarget = false; ic.enabled = false;
                Text q = Txt(bg.transform, "Qty", "", 22, Color.white, new Vector2(1f, 0f), new Vector2(-30f, 16f), new Vector2(60f, 28f), TextAnchor.MiddleRight);
                q.fontStyle = FontStyle.Bold;
                var o = q.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f);
                cells.Add((bg, ic, q));
            }

            // detail strip (bottom right)
            nameLabel = Txt(c, "ItemName", "Chọn một vật phẩm", 28, gold, new Vector2(0f, 0f), new Vector2(620f, 150f), new Vector2(520f, 40f), TextAnchor.MiddleLeft);
            nameLabel.fontStyle = FontStyle.Bold;
            descLabel = Txt(c, "ItemDesc", "", 22, cream, new Vector2(0f, 0f), new Vector2(620f, 100f), new Vector2(520f, 56f), TextAnchor.UpperLeft);
            msgLabel = Txt(c, "Message", "", 22, new Color(1f, 0.75f, 0.6f), new Vector2(0f, 0f), new Vector2(620f, 52f), new Vector2(520f, 32f), TextAnchor.MiddleLeft);
            useButton = Btn(c, "Use", "Dùng", new Vector2(1f, 0f), new Vector2(-470f, 110f), new Vector2(180f, 64f), new Color(0.25f, 0.6f, 0.4f), UseSelected);
            equipButton = Btn(c, "Equip", "Trang bị", new Vector2(1f, 0f), new Vector2(-270f, 110f), new Vector2(180f, 64f), new Color(0.3f, 0.45f, 0.75f), EquipSelected);
            useLabel = useButton.GetComponentInChildren<Text>();
            unequipButton = Btn(c, "Unequip", "Tháo", new Vector2(1f, 0f), new Vector2(-470f, 110f), new Vector2(180f, 64f), new Color(0.6f, 0.4f, 0.25f), () => { if (selectedEquip != null) Unequip(selectedEquip); });
            root.SetActive(false);
            Redraw();
        }

        private void Redraw()
        {
            if (!root) return;
            var bySlot = State?.slots?.ToDictionary(s => s.index) ?? new Dictionary<int, BagSlotState>();
            for (int i = 0; i < cells.Count; i++)
            {
                var (bg, icon, qty) = cells[i];
                bool used = State != null && i < State.capacity;
                bg.gameObject.SetActive(State == null || used);
                if (bySlot.TryGetValue(i, out BagSlotState s))
                {
                    icon.sprite = Icon(s.icon); icon.enabled = icon.sprite;
                    icon.color = s.usableByClass ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    qty.text = s.qty > 1 ? s.qty.ToString() : "";
                }
                else { icon.enabled = false; qty.text = ""; }
                bg.color = i == SelectedIndex ? new Color(1f, 0.85f, 0.45f) : new Color(0.55f, 0.6f, 0.78f, 0.9f);
            }
            foreach (var (slot, _) in EquipSlots)
            {
                EquippedState e = State?.equipped?.FirstOrDefault(x => x.equipSlot == slot);
                var (ic, _) = equipCells[slot];
                ic.enabled = e != null && Icon(e.item.icon);
                if (e != null) ic.sprite = Icon(e.item.icon);
            }
            if (statsLabel) statsLabel.text = State == null ? "" : $"Tấn công +{State.attackBonus}\nPhòng thủ +{State.defenseBonus}";

            BagSlotState sel = SelectedIndex >= 0 && bySlot.TryGetValue(SelectedIndex, out var x0) ? x0 : null;
            EquippedState selEq = selectedEquip != null ? State?.equipped?.FirstOrDefault(e => e.equipSlot == selectedEquip) : null;
            if (sel != null)
            {
                nameLabel.text = sel.name + (sel.qty > 1 ? $"  ×{sel.qty}" : "");
                descLabel.text = sel.desc + (sel.usableByClass ? "" : "  (Nghề của bạn không dùng được.)");
            }
            else if (selEq != null) { nameLabel.text = selEq.item.name + " (đang mặc)"; descLabel.text = selEq.item.desc; }
            else { nameLabel.text = State == null ? "Đang tải túi đồ..." : "Chọn một vật phẩm"; descLabel.text = State == null ? "" : $"{State.slots.Length}/{State.capacity} ô đã dùng · tối đa {State.stackMax}/ô"; }
            useButton.gameObject.SetActive(sel != null && (sel.kind == "consumable" || sel.kind == "ticket"));
            equipButton.gameObject.SetActive(sel != null && sel.kind == "equipment" && sel.usableByClass);
            unequipButton.gameObject.SetActive(selEq != null);
        }
    }
}
