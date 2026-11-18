using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Cường Hóa Trang Bị +1…+15. Opened from the bag window for the selected piece of gear (bag cell or worn). It shows the success rate, the
    /// stones and Gold of the attempt, what a failure costs at this level, the stat preview and a Bùa Bảo Vệ slot (on / off). Everything it shows
    /// comes from the server's table (InventoryState.enhanceRules); the attempt itself is one server call that takes the materials and rolls, so
    /// the client only presents the result: a hammer strike, a bright flash and rays with a sparkling run on success, a shake and a dull thud on
    /// failure, a cyan flash when the charm saves the piece, and a crack when it shatters.
    /// </summary>
    public sealed class EnhancePanel : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.08f, 0.05f, 0.14f), Cream = new Color(1f, 0.96f, 0.88f), Gold = new Color(1f, 0.86f, 0.4f),
            Good = new Color(0.45f, 1f, 0.55f), Bad = new Color(1f, 0.42f, 0.38f), Dim = new Color(0.62f, 0.6f, 0.72f);

        private InventoryPanel owner;
        private Font font;
        private Canvas canvas;
        private GameObject root;
        private RectTransform card, burst, frame;
        private Image frameBorder, icon, flash;
        private Text title, itemName, statA, statB, rate, cost, risk, charmLabel, banner, message, enhanceLabel;
        private Image[] pips = new Image[EnhanceMax];
        private Image[] rays = new Image[14];
        private Button enhanceButton, charmButton;
        private const int EnhanceMax = 15;

        private string where = "bag", equipSlot = "";
        private int slot;
        private bool charmOn, busy;
        private float flashT = 1f, shakeT = 1f, bannerT = 1f, burstT = 1f, shakePower;
        private Color flashColor = Color.white, burstColor = Gold;

        public bool IsOpen => root && root.activeSelf;
        public bool Busy => busy;
        public bool CharmOn => charmOn;
        public string Outcome { get; private set; } = "";
        public EnhanceResult LastResult { get; private set; }
        public string BannerText => banner ? banner.text : "";
        public string RateText => rate ? rate.text : "";
        public string CostText => cost ? cost.text : "";
        public string RiskText => risk ? risk.text : "";
        public string MessageText => message ? message.text : "";
        public bool EnhanceEnabled => enhanceButton && enhanceButton.interactable;
        public float FlashAlpha => flash ? flash.color.a : 0f;
        public bool BurstVisible => rays != null && rays.Any(r => r && r.color.a > 0.05f);

        public static EnhancePanel Attach(InventoryPanel inventory, Font uiFont)
        {
            var existing = inventory.GetComponent<EnhancePanel>();
            if (existing) return existing;
            var p = inventory.gameObject.AddComponent<EnhancePanel>();
            p.owner = inventory; p.font = uiFont;
            return p;
        }

        private void OnDestroy() { if (canvas) Destroy(canvas.gameObject); }

        // ------------------------------------------------------------------ open / close
        /// <summary>Opens for the gear in a bag cell ("bag", slot) or on the hero ("equipped", equipSlot).</summary>
        public void Open(string whereFrom, int bagSlot, string wornSlot)
        {
            Build();
            where = whereFrom; slot = bagSlot; equipSlot = wornSlot ?? "";
            Outcome = ""; LastResult = null; banner.text = ""; message.text = "";
            root.SetActive(true);
            Refresh();
        }

        public void Close() { if (root) root.SetActive(false); }

        private BagSlotState Target()
        {
            InventoryState s = owner ? owner.State : null;
            if (s == null) return null;
            if (where == "equipped") return s.equipped?.FirstOrDefault(e => e.equipSlot == equipSlot)?.item;
            return s.slots?.FirstOrDefault(x => x.index == slot && x.kind == "equipment");
        }

        private int Count(string itemId) => owner && owner.State != null && !string.IsNullOrEmpty(itemId) ? owner.State.slots.Where(x => x.itemId == itemId).Sum(x => x.qty) : 0;

        private long HeroGold { get { var w = AdventureWorldSession.Current; return w && w.Character != null ? w.Character.gold : 0; } }

        // ------------------------------------------------------------------ view
        public void Refresh()
        {
            if (!root) return;
            BagSlotState t = Target();
            EnhanceRulesState rules = owner && owner.State != null ? owner.State.enhanceRules : null;
            bool ready = t != null && rules != null && rules.success != null && rules.success.Length >= EnhanceMax;
            for (int i = 0; i < pips.Length; i++) pips[i].color = ready && i < t.enhance ? Gold : new Color(0.27f, 0.24f, 0.38f);
            if (!ready)
            {
                itemName.text = Outcome == "broken" ? "Trang bị đã vỡ" : "Chọn một trang bị trong túi đồ";
                icon.enabled = false; frameBorder.color = Ink; statA.text = statB.text = rate.text = cost.text = risk.text = "";
                charmLabel.text = ""; enhanceButton.interactable = false; charmButton.gameObject.SetActive(false);
                return;
            }
            charmButton.gameObject.SetActive(true);
            Sprite sprite = owner.IconSprite(t.icon);
            icon.enabled = sprite; icon.sprite = sprite;
            frameBorder.color = LootRarity.ColorOf(LootRarity.Tier(t.rarity));
            itemName.text = t.name + (t.enhance > 0 ? "  +" + t.enhance : "");
            itemName.color = LootRarity.ColorOf(LootRarity.Tier(t.rarity));
            if (t.enhance >= rules.maxLevel)
            {
                statA.text = $"Tấn công {t.attack}"; statB.text = $"Phòng thủ {t.defense}";
                rate.text = "ĐÃ ĐẠT +" + rules.maxLevel; cost.text = ""; risk.text = "Không thể cường hóa thêm.";
                enhanceButton.interactable = false; charmLabel.text = ""; charmButton.gameObject.SetActive(false);
                return;
            }
            int lv = t.enhance;
            statA.text = $"Tấn công  {t.attack}  →  <color=#7DFF8C>{t.nextAttack}</color>";
            statB.text = $"Phòng thủ  {t.defense}  →  <color=#7DFF8C>{t.nextDefense}</color>";
            int stonesNeed = rules.stones[lv], stonesHave = Count(rules.stoneItemId); long gold = rules.gold[lv];
            rate.text = $"Tỷ lệ thành công  {rules.success[lv]}%";
            rate.color = rules.success[lv] >= 70 ? Good : rules.success[lv] >= 40 ? Gold : Bad;
            cost.text = $"Đá: <color={(stonesHave >= stonesNeed ? "#7DFF8C" : "#FF6B61")}>{stonesNeed}</color> (có {stonesHave})     Vàng: <color={(Affordable(gold) ? "#7DFF8C" : "#FF6B61")}>{gold:N0}</color>";
            bool unsafeLevel = lv >= rules.safeLevel, canBreak = lv >= rules.breakFromLevel;
            risk.text = !unsafeLevel ? "Thất bại: chỉ mất nguyên liệu, trang bị an toàn."
                : canBreak ? $"Thất bại: giảm 1 cấp, {rules.breakPercent}% VỠ ĐỒ.\nBùa Bảo Vệ giữ nguyên cấp và không vỡ."
                : "Thất bại: giảm 1 cấp.\nBùa Bảo Vệ giữ nguyên cấp.";
            risk.color = !unsafeLevel ? Dim : canBreak ? Bad : Gold;
            int charms = Count(rules.charmItemId);
            if (charms == 0) charmOn = false;
            charmLabel.text = $"Bùa Bảo Vệ: {(charmOn ? "ĐANG BẬT" : "TẮT")}  (có {charms})";
            charmButton.GetComponent<Image>().color = charmOn ? new Color(0.2f, 0.62f, 0.7f) : new Color(0.3f, 0.27f, 0.42f);
            charmButton.interactable = charms > 0 && !busy;
            enhanceButton.interactable = !busy && stonesHave >= stonesNeed && Affordable(gold);
            enhanceLabel.text = busy ? "Đang rèn..." : $"CƯỜNG HÓA  +{lv + 1}";
        }

        private bool Affordable(long price) => HeroGold >= price;

        public void ToggleCharm()
        {
            if (busy) return;
            EnhanceRulesState rules = owner.State?.enhanceRules;
            if (rules != null && Count(rules.charmItemId) == 0) { message.text = "Bạn không có Bùa Bảo Vệ (mua ở Dược Sư Mộc Linh, tab Cường Hóa)."; charmOn = false; Refresh(); return; }
            charmOn = !charmOn;
            Refresh();
        }

        // ------------------------------------------------------------------ the attempt
        /// <summary>Sends one attempt to the server and presents what it answers (at least 0.7 s of hammering, so the result lands on a beat).</summary>
        public void Attempt()
        {
            if (busy || !owner || !AdventureServerConnection.Instance) return;
            var world = AdventureWorldSession.Current;
            if (!world || world.Character == null || Target() == null) return;
            busy = true; message.text = ""; banner.text = "";
            EnhanceFx.Play(EnhanceFx.Kind.Hammer, 0.7f);
            Shake(0.4f);
            Refresh();
            bool done = false, ok = false; EnhanceResult result = null; string err = null;
            AdventureServerConnection.Instance.EnhanceItem(world.Character.characterId, where, slot, equipSlot, charmOn, (success, r, e) => { done = true; ok = success; result = r; err = e; });
            StartCoroutine(Resolve(() => done, () => (ok, result, err)));
        }

        private IEnumerator Resolve(Func<bool> done, Func<(bool ok, EnhanceResult r, string err)> get)
        {
            float until = Time.unscaledTime + 0.7f;
            while (Time.unscaledTime < until || !done()) yield return null;
            var (ok, r, err) = get();
            busy = false;
            if (!ok || r == null) { message.text = string.IsNullOrEmpty(err) ? "Không thể cường hóa lúc này." : err; Refresh(); yield break; }
            var world = AdventureWorldSession.Current;
            if (world && world.Character != null) world.Character.gold = r.gold;
            owner.Adopt(r.inventory);
            PlayOutcome(r);
            Refresh();
        }

        /// <summary>Shows a server result: banner, flash, rays, shake and sound. (Public so the presentation can be exercised without a roll.)</summary>
        public void PlayOutcome(EnhanceResult r)
        {
            Build();
            LastResult = r; Outcome = r.outcome;
            Color col; string text; EnhanceFx.Kind sound;
            switch (r.outcome)
            {
                case "success": text = $"THÀNH CÔNG!  +{r.levelAfter}"; col = Gold; sound = EnhanceFx.Kind.Success; flashColor = new Color(1f, 0.95f, 0.7f); burstColor = Gold; Flash(); Burst(); Shake(0.15f); break;
                case "protected": text = "BÙA BẢO VỆ ĐÃ CỨU TRANG BỊ"; col = new Color(0.5f, 0.95f, 1f); sound = EnhanceFx.Kind.Protected; flashColor = new Color(0.45f, 0.9f, 1f); burstColor = new Color(0.5f, 0.95f, 1f); Flash(0.55f); Shake(0.2f); break;
                case "drop": text = $"THẤT BẠI  —  GIẢM XUỐNG +{r.levelAfter}"; col = Bad; sound = EnhanceFx.Kind.Fail; flashColor = new Color(1f, 0.3f, 0.25f); Flash(0.4f); Shake(0.8f); break;
                case "broken": text = "TRANG BỊ ĐÃ VỠ!"; col = new Color(1f, 0.2f, 0.2f); sound = EnhanceFx.Kind.Broken; flashColor = new Color(0.9f, 0.1f, 0.1f); Flash(0.8f); Shake(1.4f); break;
                default: text = "THẤT BẠI"; col = Dim; sound = EnhanceFx.Kind.Fail; flashColor = new Color(0.8f, 0.8f, 0.9f); Flash(0.3f); Shake(0.6f); break;
            }
            banner.text = text; banner.color = col; bannerT = 0f;
            message.text = r.outcome == "success" ? $"Đã tiêu {r.stonesUsed} đá và {r.goldSpent:N0} Vàng." : r.charmUsed ? $"Đã tiêu {r.stonesUsed} đá, {r.goldSpent:N0} Vàng và 1 Bùa Bảo Vệ." : $"Đã tiêu {r.stonesUsed} đá và {r.goldSpent:N0} Vàng.";
            EnhanceFx.Play(sound);
        }

        private void Flash(float strength = 1f) { flashT = 0f; flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, strength); flashStrength = strength; }
        private float flashStrength = 1f;
        private void Burst() { burstT = 0f; }
        private void Shake(float power) { shakeT = 0f; shakePower = power; }

        private void Update()
        {
            if (!card) return;
            float dt = Time.unscaledDeltaTime;
            if (flashT < 1f)
            {
                flashT = Mathf.Min(1f, flashT + dt / 0.55f);
                var c = flashColor; c.a = flashStrength * (1f - Mathf.SmoothStep(0f, 1f, flashT)); flash.color = c;
            }
            if (shakeT < 1f)
            {
                shakeT = Mathf.Min(1f, shakeT + dt / 0.5f);
                float k = (1f - shakeT) * shakePower * 16f;
                card.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 90f) * k, Mathf.Cos(Time.unscaledTime * 77f) * k * 0.6f);
            }
            else card.anchoredPosition = Vector2.zero;
            if (bannerT < 1f)
            {
                bannerT = Mathf.Min(1f, bannerT + dt / 0.5f);
                banner.rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(bannerT * Mathf.PI) * (1f - bannerT));
            }
            for (int i = 0; i < rays.Length; i++)
            {
                if (burstT >= 1f) { rays[i].color = Color.clear; continue; }
                float k = burstT;
                rays[i].rectTransform.localScale = new Vector3(1f - 0.6f * k, 0.3f + 1.5f * k, 1f);
                var c = burstColor; c.a = (1f - k) * (i % 2 == 0 ? 0.95f : 0.6f); rays[i].color = c;
            }
            if (burstT < 1f) burstT = Mathf.Min(1f, burstT + dt / 0.8f);
        }

        // ------------------------------------------------------------------ UI (flat cel look: ink outlines, flat fills, no gradients)
        private void Build()
        {
            if (root) return;
            var cgo = new GameObject("EnhanceCanvas", typeof(RectTransform));
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 400;   // above the HUD clusters and the floating name plates (modal window)
            UiStandard.Apply(cgo.AddComponent<CanvasScaler>());
            cgo.AddComponent<GraphicRaycaster>();
            root = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(cgo.transform, false);
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            var cardGo = Box(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 700f), new Color(0.16f, 0.12f, 0.26f), 4f);
            card = (RectTransform)cardGo.transform;
            title = Label(card, "Title", "CƯỜNG HÓA TRANG BỊ", 44, Gold, new Vector2(0f, 300f), new Vector2(700f, 60f), TextAnchor.MiddleCenter, true);
            for (int i = 0; i < EnhanceMax; i++)
            {
                var p = Box(card, "Pip" + i, new Vector2(0.5f, 0.5f), new Vector2((i - 7) * 44f, 232f), new Vector2(36f, 36f), new Color(0.27f, 0.24f, 0.38f), 2f);
                pips[i] = p.GetComponent<Image>();
            }
            // item frame (rarity border), name, stat preview
            frame = (RectTransform)Box(card, "Frame", new Vector2(0.5f, 0.5f), new Vector2(-290f, 70f), new Vector2(180f, 180f), Ink, 0f).transform;
            frameBorder = frame.GetComponent<Image>();
            var inner = Box(frame, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(164f, 164f), new Color(0.27f, 0.24f, 0.38f), 0f);
            icon = NewImage(inner.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132f, 132f), Color.white); icon.preserveAspect = true;
            itemName = Label(card, "Name", "", 32, Cream, new Vector2(-290f, -50f), new Vector2(400f, 48f), TextAnchor.MiddleCenter, true);
            statA = Label(card, "StatA", "", 28, Cream, new Vector2(-290f, -100f), new Vector2(420f, 40f), TextAnchor.MiddleCenter, true);
            statB = Label(card, "StatB", "", 28, Cream, new Vector2(-290f, -140f), new Vector2(420f, 40f), TextAnchor.MiddleCenter, true);
            statA.supportRichText = statB.supportRichText = true;
            // the right column: rate, cost, risk, charm slot
            rate = Label(card, "Rate", "", 40, Gold, new Vector2(185f, 130f), new Vector2(520f, 56f), TextAnchor.MiddleCenter, true);
            cost = Label(card, "Cost", "", 28, Cream, new Vector2(185f, 76f), new Vector2(520f, 42f), TextAnchor.MiddleCenter, true); cost.supportRichText = true;
            risk = Label(card, "Risk", "", 24, Dim, new Vector2(185f, 10f), new Vector2(520f, 70f), TextAnchor.MiddleCenter, true);
            charmButton = Btn(card, "Charm", "", new Vector2(185f, -70f), new Vector2(480f, 64f), new Color(0.3f, 0.27f, 0.42f), ToggleCharm);
            charmLabel = charmButton.GetComponentInChildren<Text>(); charmLabel.fontSize = 26;
            banner = Label(card, "Banner", "", 54, Gold, new Vector2(0f, -190f), new Vector2(900f, 74f), TextAnchor.MiddleCenter, true);
            message = Label(card, "Message", "", 24, Dim, new Vector2(0f, -240f), new Vector2(880f, 36f), TextAnchor.MiddleCenter, true);
            enhanceButton = Btn(card, "Enhance", "CƯỜNG HÓA", new Vector2(0f, -302f), new Vector2(420f, 80f), new Color(0.82f, 0.55f, 0.12f), Attempt);
            enhanceLabel = enhanceButton.GetComponentInChildren<Text>(); enhanceLabel.fontSize = 36; enhanceLabel.fontStyle = FontStyle.Bold;
            Btn(card, "Close", "Đóng", new Vector2(390f, 300f), new Vector2(140f, 56f), new Color(0.55f, 0.3f, 0.3f), Close);
            // rays burst from the item frame
            var burstGo = new GameObject("Burst", typeof(RectTransform)); burstGo.transform.SetParent(card, false);
            burst = (RectTransform)burstGo.transform; burst.anchoredPosition = new Vector2(-290f, 70f);
            for (int i = 0; i < rays.Length; i++)
            {
                var ray = NewImage(burst, "Ray" + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 200f), Color.clear);
                ray.rectTransform.pivot = new Vector2(0.5f, 0f);
                ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * (360f / rays.Length));
                ray.raycastTarget = false;
                rays[i] = ray;
            }
            flash = NewImage(card, "Flash", new Vector2(0.5f, 0.5f), Vector2.zero, card.sizeDelta, Color.clear);
            flash.raycastTarget = false;
            root.SetActive(false);
        }

        private Text Label(Transform parent, string name, string text, int size, Color color, Vector2 pos, Vector2 box, TextAnchor anchor, bool outline)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = box;
            var t = go.GetComponent<Text>();
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline) { var o = go.AddComponent<Outline>(); o.effectColor = Ink; o.effectDistance = new Vector2(2f, -2f); }
            return t;
        }

        private static Image NewImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>(); img.color = color;
            return img;
        }

        private static GameObject Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, float line)
        {
            Image img = NewImage(parent, name, anchor, pos, size, fill);
            if (line > 0f) { var o = img.gameObject.AddComponent<Outline>(); o.effectColor = Ink; o.effectDistance = new Vector2(line, -line); }
            return img.gameObject;
        }

        private Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color tint, Action onClick)
        {
            GameObject go = Box(parent, name, new Vector2(0.5f, 0.5f), pos, size, tint, 3f);
            var b = go.AddComponent<Button>(); b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(() => onClick());
            Label(go.transform, "Label", label, 30, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter, true);
            return b;
        }

        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    }
}
