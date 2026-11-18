using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.UI
{
    /// <summary>Item rarity, lowest to highest: Trắng &lt; Xanh &lt; Tím &lt; Cam &lt; Đỏ.</summary>
    public enum ItemRarity { White, Blue, Purple, Orange, Red }

    public enum EquipSlot { None, Hat, Armor, Weapon, Boots, Ring, Necklace, Pet }

    [Serializable]
    public class UiItem
    {
        public string Id, Name;
        public Sprite Icon;
        public ItemRarity Rarity;
        public EquipSlot Slot = EquipSlot.None;   // None = not equippable (consumable / junk)
        public int Count = 1;
    }

    /// <summary>
    /// Grid inventory + paperdoll in one script. Add it to an empty child of a Canvas (Screen Space - Overlay, Canvas Scaler = Scale With
    /// Screen Size 1920x1080, Match 0.5); the whole UI is built at runtime. Wide screens: paperdoll left, grid right. Narrow / portrait
    /// (phones): paperdoll on top, grid below. Look: flat cel-shaded fills, hard 3 px line-art outlines, no gradients, no blur, no glow.
    /// Drag &amp; drop (or tap a slot then tap its target) moves items between grid and paperdoll; wrong slot types are refused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryPaperdollUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int gridColumnsWide = 6;       // PC / landscape
        [SerializeField] private int gridColumnsNarrow = 5;     // phone / portrait
        [SerializeField] private int bagSize = 40;
        [SerializeField] private float narrowAspect = 1.2f;     // width / height below this = stacked (mobile) layout
        [SerializeField] private Font font;                     // optional; the built-in font is used when empty

        public event Action<UiItem, EquipSlot> Equipped;        // item moved onto the paperdoll
        public event Action<UiItem, EquipSlot> Unequipped;      // item moved back to the bag
        public event Action<UiItem> Selected;

        // flat cel-shaded palette
        private static readonly Color Ink = new Color(0.10f, 0.07f, 0.16f);
        private static readonly Color Panel = new Color(0.20f, 0.17f, 0.30f), PanelShade = new Color(0.14f, 0.12f, 0.22f), SlotFill = new Color(0.27f, 0.24f, 0.38f);

        /// <summary>Border colour per rarity (never blends: one flat colour per tier).</summary>
        public static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.White => new Color(0.96f, 0.96f, 0.96f),
            ItemRarity.Blue => new Color(0.25f, 0.62f, 1f),
            ItemRarity.Purple => new Color(0.66f, 0.36f, 0.95f),
            ItemRarity.Orange => new Color(1f, 0.58f, 0.12f),
            _ => new Color(0.92f, 0.16f, 0.2f),
        };

        private readonly List<UiItem> bag = new List<UiItem>();
        private readonly Dictionary<EquipSlot, UiItem> worn = new Dictionary<EquipSlot, UiItem>();
        private readonly List<SlotView> gridSlots = new List<SlotView>();
        private readonly Dictionary<EquipSlot, SlotView> doll = new Dictionary<EquipSlot, SlotView>();
        private RectTransform window, dollRoot, gridRoot, ghost;
        private GridLayoutGroup grid;
        private Text detail;
        private Image ghostIcon, ghostBorder;
        private SlotView selected, dragFrom;
        private bool narrow, built;
        private Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        // ------------------------------------------------------------------ public API
        public IReadOnlyList<UiItem> Bag => bag;
        public UiItem WornIn(EquipSlot s) => worn.TryGetValue(s, out var i) ? i : null;

        /// <summary>Replaces the bag content (call again after every server refresh).</summary>
        public void SetBag(IEnumerable<UiItem> items)
        {
            bag.Clear();
            foreach (UiItem i in items) if (i != null && bag.Count < bagSize) bag.Add(i);
            Refresh();
        }

        public void SetWorn(EquipSlot slot, UiItem item)
        {
            if (item == null) worn.Remove(slot); else worn[slot] = item;
            Refresh();
        }

        // ------------------------------------------------------------------ build
        private void Awake() { Build(); }

        private void Build()
        {
            if (built) return;
            built = true;
            window = Box("Window", transform, Panel, Ink, 4f);
            window.anchorMin = new Vector2(0.5f, 0.5f); window.anchorMax = new Vector2(0.5f, 0.5f); window.pivot = new Vector2(0.5f, 0.5f);

            dollRoot = Box("Paperdoll", window, PanelShade, Ink, 3f);
            gridRoot = Box("Bag", window, PanelShade, Ink, 3f);
            Title(dollRoot, "Trang bị"); Title(gridRoot, "Túi đồ");

            // paperdoll: hat / armor / weapon / boots down the left & right of a centre silhouette box, jewellery + pet underneath
            var figure = Box("Figure", dollRoot, SlotFill, Ink, 3f);
            figure.name = "Figure";
            foreach (EquipSlot s in new[] { EquipSlot.Hat, EquipSlot.Armor, EquipSlot.Weapon, EquipSlot.Boots, EquipSlot.Ring, EquipSlot.Necklace, EquipSlot.Pet })
            {
                var v = NewSlot(dollRoot, "Slot_" + s, s);
                doll[s] = v;
            }
            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(gridRoot, false);
            var gr = (RectTransform)gridGo.transform;
            grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(8f, 8f); grid.childAlignment = TextAnchor.UpperLeft;
            for (int i = 0; i < bagSize; i++) gridSlots.Add(NewSlot(gridGo.transform, "Cell_" + i, EquipSlot.None, i));

            detail = NewText(window, "Detail", 26, TextAnchor.MiddleLeft, Color.white);

            ghost = Box("DragGhost", transform, Color.clear, Color.clear, 0f);
            ghost.sizeDelta = new Vector2(96f, 96f);
            ghostBorder = ghost.GetComponent<Image>(); ghostBorder.raycastTarget = false;
            ghostIcon = NewImage(ghost, "Icon", Color.white); ghostIcon.raycastTarget = false;
            Stretch((RectTransform)ghostIcon.transform, 8f);
            ghost.gameObject.SetActive(false);
            ghost.SetAsLastSibling();

            gridRoot.name = "Bag";
            ApplyLayout(true);
            Refresh();
        }

        private void OnRectTransformDimensionsChange() { if (built) ApplyLayout(false); }
        private void OnEnable() { if (built) ApplyLayout(true); }

        // ------------------------------------------------------------------ responsive layout
        private void ApplyLayout(bool force)
        {
            var parent = transform as RectTransform;
            Rect r = parent ? parent.rect : new Rect(0, 0, 1920, 1080);
            if (r.width < 10f) return;
            bool nowNarrow = r.width / Mathf.Max(1f, r.height) < narrowAspect;
            if (!force && nowNarrow == narrow && window.sizeDelta.x > 0f) { /* still re-fit sizes below */ }
            narrow = nowNarrow;

            float margin = 24f, gap = 16f, title = 56f, detailH = 84f;
            float w = Mathf.Min(r.width - 2f * margin, narrow ? 760f : 1500f);
            float h = Mathf.Min(r.height - 2f * margin, narrow ? 1500f : 860f);
            window.sizeDelta = new Vector2(w, h);
            window.anchoredPosition = Vector2.zero;
            float inner = w - 2f * margin, innerH = h - 2f * margin - detailH - gap;

            Rect dollR, bagR;
            if (narrow)    // phone: paperdoll on top (40 %), bag below (60 %)
            {
                float dh = innerH * 0.40f;
                dollR = new Rect(0f, 0f, inner, dh - gap * 0.5f);
                bagR = new Rect(0f, dh + gap * 0.5f, inner, innerH - dh - gap * 0.5f);
            }
            else           // PC: paperdoll left (38 %), bag right
            {
                float dw = inner * 0.38f;
                dollR = new Rect(0f, 0f, dw - gap * 0.5f, innerH);
                bagR = new Rect(dw + gap * 0.5f, 0f, inner - dw - gap * 0.5f, innerH);
            }
            Place(dollRoot, dollR, w, h, margin); Place(gridRoot, bagR, w, h, margin);

            // detail line along the bottom
            var dr = detail.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0f); dr.pivot = new Vector2(0.5f, 0f);
            dr.sizeDelta = new Vector2(inner, detailH); dr.anchoredPosition = new Vector2(0f, margin);

            LayoutDoll(dollR.size, title);
            LayoutGrid(bagR.size, title);
        }

        private static void Place(RectTransform rt, Rect area, float w, float h, float margin)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = area.size;
            rt.anchoredPosition = new Vector2(-w * 0.5f + margin + area.x, -margin - area.y);
        }

        private void LayoutDoll(Vector2 size, float title)
        {
            float pad = 16f;
            float avail = Mathf.Min(size.x - 2f * pad, (size.y - title - pad) / 3.1f * (narrow ? 1.6f : 1f));
            float cell = Mathf.Clamp(Mathf.Min((size.x - 4f * pad) / 3f, (size.y - title - 2f * pad) / 4.2f), 72f, 140f);
            Vector2 c = new Vector2(size.x * 0.5f, -title - (size.y - title) * 0.5f);   // centre of the area under the title
            RectTransform fig = (RectTransform)dollRoot.Find("Figure");
            fig.anchorMin = fig.anchorMax = new Vector2(0f, 1f); fig.pivot = new Vector2(0.5f, 0.5f);
            fig.sizeDelta = new Vector2(cell * 1.25f, cell * 2.6f); fig.anchoredPosition = c + new Vector2(0f, cell * 0.15f);
            float gx = cell * 1.55f;
            SetPos(EquipSlot.Hat, c + new Vector2(-gx, cell * 1.05f), cell);
            SetPos(EquipSlot.Armor, c + new Vector2(-gx, 0f), cell);
            SetPos(EquipSlot.Boots, c + new Vector2(-gx, -cell * 1.05f), cell);
            SetPos(EquipSlot.Weapon, c + new Vector2(gx, cell * 1.05f), cell);
            SetPos(EquipSlot.Ring, c + new Vector2(gx, 0f), cell);
            SetPos(EquipSlot.Necklace, c + new Vector2(gx, -cell * 1.05f), cell);
            SetPos(EquipSlot.Pet, c + new Vector2(0f, -cell * 1.75f), cell);
            _ = avail;
        }

        private void SetPos(EquipSlot s, Vector2 pos, float cell)
        {
            var rt = (RectTransform)doll[s].transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cell, cell); rt.anchoredPosition = pos;
        }

        private void LayoutGrid(Vector2 size, float title)
        {
            int cols = narrow ? gridColumnsNarrow : gridColumnsWide;
            float pad = 16f, sp = grid.spacing.x;
            float cell = Mathf.Floor((size.x - 2f * pad - sp * (cols - 1)) / cols);
            int rows = Mathf.CeilToInt(bagSize / (float)cols);
            float maxCell = (size.y - title - 2f * pad - sp * (rows - 1)) / rows;
            cell = Mathf.Max(48f, Mathf.Min(cell, maxCell));
            grid.cellSize = new Vector2(cell, cell);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = cols;
            var rt = (RectTransform)grid.transform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(cols * cell + (cols - 1) * sp, rows * cell + (rows - 1) * sp);
            rt.anchoredPosition = new Vector2(0f, -title - pad * 0.5f);
        }

        // ------------------------------------------------------------------ state → view
        private void Refresh()
        {
            if (!built) return;
            for (int i = 0; i < gridSlots.Count; i++) gridSlots[i].Show(i < bag.Count ? bag[i] : null);
            foreach (var kv in doll) kv.Value.Show(WornIn(kv.Key));
            if (selected) selected.SetSelected(true);
        }

        private string Describe(UiItem i) => i == null ? "Chọn một món đồ" :
            $"{i.Name}  ·  {RarityName(i.Rarity)}" + (i.Slot != EquipSlot.None ? $"  ·  {SlotName(i.Slot)}" : "") + (i.Count > 1 ? $"  ·  x{i.Count}" : "");

        public static string RarityName(ItemRarity r) => r switch { ItemRarity.White => "Thường", ItemRarity.Blue => "Hiếm", ItemRarity.Purple => "Sử thi", ItemRarity.Orange => "Huyền thoại", _ => "Thần thoại" };
        public static string SlotName(EquipSlot s) => s switch { EquipSlot.Hat => "Nón", EquipSlot.Armor => "Áo", EquipSlot.Weapon => "Vũ khí", EquipSlot.Boots => "Giày", EquipSlot.Ring => "Nhẫn", EquipSlot.Necklace => "Dây chuyền", EquipSlot.Pet => "Pet", _ => "" };

        // ------------------------------------------------------------------ interaction (drag & drop, or tap + tap)
        private void Select(SlotView v)
        {
            if (selected && selected != v) selected.SetSelected(false);
            // tap a selected item, then tap a destination slot = same as dragging it there
            if (selected && selected.Item != null && selected != v) { if (TryMove(selected, v)) { selected = null; detail.text = Describe(null); return; } }
            selected = v; v.SetSelected(true);
            detail.text = Describe(v.Item);
            Selected?.Invoke(v.Item);
        }

        private bool TryMove(SlotView from, SlotView to)
        {
            UiItem item = from.Item;
            if (item == null || from == to) return false;
            bool toDoll = to.Slot != EquipSlot.None, fromDoll = from.Slot != EquipSlot.None;
            if (toDoll && item.Slot != to.Slot) return false;                                   // wrong slot type
            if (toDoll && !fromDoll) { Wear(to.Slot, item, from.Index); return true; }          // bag → paperdoll (swap with what is worn)
            if (!toDoll && fromDoll) { Unwear(from.Slot, to.Index); return true; }              // paperdoll → bag
            if (!toDoll && !fromDoll) { SwapBag(from.Index, to.Index); return true; }           // rearrange the bag
            return false;
        }

        private void Wear(EquipSlot slot, UiItem item, int bagIndex)
        {
            UiItem old = WornIn(slot);
            worn[slot] = item;
            if (old != null) bag[bagIndex] = old; else bag.RemoveAt(bagIndex);
            Refresh(); Equipped?.Invoke(item, slot);
        }

        private void Unwear(EquipSlot slot, int bagIndex)
        {
            UiItem item = WornIn(slot);
            if (item == null) return;
            if (bagIndex < bag.Count) { UiItem other = bag[bagIndex]; if (other != null && other.Slot == slot) { bag[bagIndex] = item; worn[slot] = other; Refresh(); Equipped?.Invoke(other, slot); return; } }
            if (bag.Count >= bagSize) return;                                                    // bag full
            worn.Remove(slot); bag.Add(item);
            Refresh(); Unequipped?.Invoke(item, slot);
        }

        private void SwapBag(int a, int b)
        {
            if (a >= bag.Count) return;
            if (b >= bag.Count) { UiItem it = bag[a]; bag.RemoveAt(a); bag.Add(it); }
            else (bag[a], bag[b]) = (bag[b], bag[a]);
            Refresh();
        }

        // ------------------------------------------------------------------ one slot
        private SlotView NewSlot(Transform parent, string name, EquipSlot slot, int index = -1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SlotView>();
            v.Init(this, slot, index);
            return v;
        }

        private sealed class SlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
        {
            public UiItem Item { get; private set; }
            public EquipSlot Slot { get; private set; }
            public int Index { get; private set; }
            private InventoryPaperdollUI owner;
            private Image border, fill, icon, shade, pick;
            private Text count, label;

            public void Init(InventoryPaperdollUI o, EquipSlot slot, int index)
            {
                owner = o; Slot = slot; Index = index;
                border = gameObject.AddComponent<Image>(); border.color = Ink;                      // 1) hard ink outline
                fill = o.NewImage((RectTransform)transform, "Fill", SlotFill); Stretch((RectTransform)fill.transform, 4f);   // 2) flat fill
                shade = o.NewImage((RectTransform)fill.transform, "Shade", new Color(0f, 0f, 0f, 0.18f));                  // 3) one flat cel shadow band
                shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(1f, 0.28f); shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero; shade.raycastTarget = false;
                icon = o.NewImage((RectTransform)fill.transform, "Icon", Color.white); Stretch((RectTransform)icon.transform, 8f); icon.preserveAspect = true; icon.raycastTarget = false;
                pick = o.NewImage((RectTransform)transform, "Selected", Color.clear); Stretch((RectTransform)pick.transform, -3f); pick.raycastTarget = false;
                count = o.NewText((RectTransform)fill.transform, "Count", 22, TextAnchor.LowerRight, Color.white);
                count.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, 0f); count.raycastTarget = false;
                label = o.NewText((RectTransform)fill.transform, "Label", 20, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.55f)); label.raycastTarget = false;
                label.text = Slot != EquipSlot.None ? SlotName(Slot) : "";
                Show(null);
            }

            public void Show(UiItem item)
            {
                Item = item;
                bool has = item != null;
                border.color = has ? RarityColor(item.Rarity) : Ink;      // rarity = border colour, solid
                icon.enabled = has && item.Icon; icon.sprite = has ? item.Icon : null;
                if (has && !item.Icon) { icon.enabled = true; icon.color = RarityColor(item.Rarity); }   // no art yet: flat swatch
                else icon.color = Color.white;
                count.text = has && item.Count > 1 ? item.Count.ToString() : "";
                label.enabled = !has && Slot != EquipSlot.None;
                pick.color = Color.clear;
            }

            public void SetSelected(bool on) { pick.color = on ? new Color(1f, 0.93f, 0.4f) : Color.clear; pick.type = Image.Type.Simple; if (on) { pick.GetComponent<Image>().color = Color.clear; border.color = Item != null ? RarityColor(Item.Rarity) : Ink; shade.color = new Color(1f, 0.93f, 0.4f, 0.35f); } else shade.color = new Color(0f, 0f, 0f, 0.18f); }

            public void OnPointerClick(PointerEventData e) { if (!e.dragging) owner.Select(this); }
            public void OnBeginDrag(PointerEventData e)
            {
                if (Item == null) { e.pointerDrag = null; return; }
                owner.dragFrom = this; owner.ghost.gameObject.SetActive(true);
                owner.ghostBorder.color = RarityColor(Item.Rarity);
                owner.ghostIcon.sprite = Item.Icon; owner.ghostIcon.color = Item.Icon ? Color.white : RarityColor(Item.Rarity);
                OnDrag(e);
            }
            public void OnDrag(PointerEventData e) { if (owner.dragFrom == this) owner.ghost.position = e.position; }
            public void OnEndDrag(PointerEventData e) { owner.ghost.gameObject.SetActive(false); owner.dragFrom = null; }
            public void OnDrop(PointerEventData e) { if (owner.dragFrom && owner.TryMove(owner.dragFrom, this)) owner.detail.text = owner.Describe(null); }
        }

        // ------------------------------------------------------------------ tiny UI helpers (flat colours, no gradients)
        private RectTransform Box(string name, Transform parent, Color fill, Color outline, float line)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = fill;
            if (line > 0f) { var o = go.AddComponent<Outline>(); o.effectColor = outline; o.effectDistance = new Vector2(line, -line); o.useGraphicAlpha = false; }
            return (RectTransform)go.transform;
        }

        private Image NewImage(RectTransform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = c;
            return img;
        }

        private Text NewText(Transform parent, string name, int size, TextAnchor anchor, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font; t.fontSize = size; t.alignment = anchor; t.color = c; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(t.rectTransform, 4f);
            var ol = go.AddComponent<Outline>(); ol.effectColor = Ink; ol.effectDistance = new Vector2(1.5f, -1.5f);   // hard 1.5 px text outline
            return t;
        }

        private void Title(RectTransform parent, string text)
        {
            Text t = NewText(parent, "Title", 32, TextAnchor.UpperCenter, new Color(1f, 0.93f, 0.6f));
            t.text = text;
            t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = Vector2.one; t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.sizeDelta = new Vector2(0f, 52f); t.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
