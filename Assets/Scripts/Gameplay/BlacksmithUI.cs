using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// The Blacksmith's forge window (NPC Thợ Rèn): pick one of the three worn pieces, see +n -> +n+1, the stat gain, the stone and Gold it
    /// costs, the server's success rate and what a failure does, optionally spend a Protection Charm, then strike. The server owns the
    /// roll, the stone / charm / Gold consumption and the receipt (a retried request never charges twice). 2D pixel UI + 2D forge effects.
    /// </summary>
    public sealed class BlacksmithUI : MonoBehaviour
    {
        [SerializeField] private Sprite panelSprite, cellSprite, buttonSprite, coinSprite;
        [SerializeField] private Font font;

        public static readonly (string slot, string label)[] Slots = { ("weapon", "Vũ khí"), ("armor", "Giáp"), ("accessory", "Phụ kiện"), ("ring", "Nhẫn"), ("necklace", "Dây chuyền") };

        public event Action Closed;
        public bool IsOpen => root && root.activeSelf;
        public InventoryState Inv { get; private set; }
        public string SelectedSlot { get; private set; } = "weapon";
        public bool UseProtection { get; private set; }
        public bool UseLucky { get; private set; }
        public int LuckyCount => Inv == null ? 0 : Inv.slots.Where(s => s.itemId == "lucky_stone").Sum(s => s.qty);
        public void ToggleLucky() { UseLucky = !UseLucky; pendingId = null; Redraw(); }
        private Button luckyButton; private Text luckyLabel, popText, powerPop;
        private Image anticipationBar;
        public const float AnticipationSeconds = 1.2f;
        /// <summary>Banner shown for a Kim Cương / Đỏ piece reaching +10 or more (the local celebration; QA reads it).</summary>
        public string LastBanner { get; private set; } = "";
        private static Color TierGlow(string tier) => tier == "Đỏ" ? new Color(1f, 0.25f, 0.2f) : tier == "Kim Cương" ? new Color(0.4f, 0.9f, 1f) : tier == "Vàng" ? new Color(1f, 0.85f, 0.3f) : new Color(0.88f, 0.9f, 0.96f);
        public bool Busy { get; private set; }
        public string LastMessage { get; private set; } = "";
        public bool LastSuccess { get; private set; }
        public int Strikes { get; private set; }
        public Button StrikeButton => strikeButton;

        private GameObject root;
        private Text goldLabel, greet, itemName, levelLine, statLine, stoneLine, goldLine, chanceLabel, failLine, msg, protectLabel;
        private Image bigIcon, flash, chanceFill, protectBox;
        private RectTransform fxLayer;
        private readonly (Image bg, Image icon, Text name, Text lvl)[] rows = new (Image, Image, Text, Text)[5];
        private Button strikeButton, protectButton;
        private string pendingId, pendingSlot;
        private bool pendingProtect;

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
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button Btn(Transform p, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color tint, Action click, int fontSize = 24)
        {
            Image img = Make<Image>(p, name, anchor, pos, size);
            img.sprite = buttonSprite; img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple; img.color = tint;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => click());
            Txt(img.transform, "Label", label, fontSize, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 8f), TextAnchor.MiddleCenter, true);
            return b;
        }

        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f), Cream = new Color(1f, 0.96f, 0.88f), Good = new Color(0.55f, 0.95f, 0.6f), Bad = new Color(1f, 0.5f, 0.45f);

        private void Build()
        {
            if (root) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image dim = Make<Image>(transform, "BlacksmithRoot", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var drt = (RectTransform)root.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            Image card = Make<Image>(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 780f));
            card.sprite = panelSprite; card.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple;
            card.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.97f);
            Transform c = card.transform;
            // the card never exceeds the screen on small / narrow phones
            c.gameObject.AddComponent<ResponsiveLogoFit>().Configure(0.98f, 0.96f);

            Txt(c, "Title", "Lò Rèn Thiết Cốt", 38, Gold, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(700f, 54f), TextAnchor.MiddleCenter, true);
            greet = Txt(c, "Greeting", "", 22, Cream, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1000f, 34f), TextAnchor.MiddleCenter);
            Btn(c, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(-130f, -64f), new Vector2(150f, 62f), new Color(0.55f, 0.3f, 0.3f), Close);
            Image coin = Make<Image>(c, "Coin", new Vector2(0f, 1f), new Vector2(92f, -64f), new Vector2(40f, 40f));
            coin.sprite = coinSprite ? coinSprite : ItemIconBook.Load("coin_small"); coin.preserveAspect = true;
            goldLabel = Txt(c, "Gold", "", 26, Gold, new Vector2(0f, 1f), new Vector2(210f, -64f), new Vector2(240f, 40f), TextAnchor.MiddleLeft, true);

            for (int i = 0; i < Slots.Length; i++)
            {
                int idx = i;
                Image bg = Make<Image>(c, "Slot_" + Slots[i].slot, new Vector2(0f, 1f), new Vector2(350f, -222f - i * 102f), new Vector2(560f, 92f));
                bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; bg.color = new Color(0.55f, 0.6f, 0.78f, 0.9f);
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectSlot(Slots[idx].slot));
                Image ic = Make<Image>(bg.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(80f, 80f));
                ic.preserveAspect = true; ic.raycastTarget = false;
                Text nm = Txt(bg.transform, "Name", "", 24, Cream, new Vector2(0f, 0.5f), new Vector2(330f, 20f), new Vector2(380f, 36f), TextAnchor.MiddleLeft, true);
                Text lv = Txt(bg.transform, "Level", "", 20, Gold, new Vector2(0f, 0.5f), new Vector2(330f, -20f), new Vector2(380f, 30f), TextAnchor.MiddleLeft);
                rows[i] = (bg, ic, nm, lv);
            }

            // detail column
            Image plate = Make<Image>(c, "IconPlate", new Vector2(1f, 1f), new Vector2(-420f, -270f), new Vector2(190f, 190f));
            plate.sprite = cellSprite; plate.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; plate.color = new Color(0.2f, 0.22f, 0.32f, 0.95f); plate.raycastTarget = false;
            bigIcon = Make<Image>(plate.transform, "BigIcon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            bigIcon.preserveAspect = true; bigIcon.raycastTarget = false;
            fxLayer = Make<RectTransform>(plate.transform, "ForgeFx", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));
            flash = Make<Image>(plate.transform, "Flash", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));
            flash.color = new Color(1f, 1f, 1f, 0f); flash.raycastTarget = false;
            itemName = Txt(c, "ItemName", "", 30, Gold, new Vector2(1f, 1f), new Vector2(-205f, -226f), new Vector2(330f, 80f), TextAnchor.MiddleLeft, true);
            levelLine = Txt(c, "LevelLine", "", 36, Color.white, new Vector2(1f, 1f), new Vector2(-205f, -300f), new Vector2(330f, 50f), TextAnchor.MiddleLeft, true);
            statLine = Txt(c, "StatLine", "", 24, Good, new Vector2(1f, 1f), new Vector2(-310f, -382f), new Vector2(520f, 36f), TextAnchor.MiddleLeft);
            stoneLine = Txt(c, "StoneLine", "", 24, Cream, new Vector2(1f, 1f), new Vector2(-310f, -424f), new Vector2(520f, 36f), TextAnchor.MiddleLeft);
            goldLine = Txt(c, "GoldLine", "", 24, Cream, new Vector2(1f, 1f), new Vector2(-310f, -466f), new Vector2(520f, 36f), TextAnchor.MiddleLeft);
            chanceLabel = Txt(c, "ChanceLabel", "", 26, Gold, new Vector2(1f, 1f), new Vector2(-310f, -516f), new Vector2(520f, 36f), TextAnchor.MiddleLeft, true);
            Image track = Make<Image>(c, "ChanceTrack", new Vector2(1f, 1f), new Vector2(-310f, -554f), new Vector2(500f, 18f));
            track.color = new Color(0.07f, 0.06f, 0.12f, 1f); track.raycastTarget = false;
            chanceFill = Make<Image>(track.transform, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(500f, 18f));
            chanceFill.rectTransform.pivot = new Vector2(0f, 0.5f); chanceFill.rectTransform.anchoredPosition = Vector2.zero; chanceFill.color = Good; chanceFill.raycastTarget = false;
            failLine = Txt(c, "FailLine", "", 20, new Color(1f, 0.78f, 0.6f), new Vector2(1f, 1f), new Vector2(-310f, -594f), new Vector2(520f, 30f), TextAnchor.MiddleLeft);
            protectButton = Btn(c, "Protect", "", new Vector2(1f, 1f), new Vector2(-310f, -646f), new Vector2(500f, 58f), new Color(0.3f, 0.36f, 0.55f), ToggleProtection, 22);
            protectLabel = protectButton.GetComponentInChildren<Text>();
            protectButton.GetComponent<RectTransform>().sizeDelta = new Vector2(245f, 58f); protectButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-432f, -646f); protectLabel.fontSize = 17;
            luckyButton = Btn(c, "Lucky", "", new Vector2(1f, 1f), new Vector2(-188f, -646f), new Vector2(245f, 58f), new Color(0.3f, 0.36f, 0.55f), ToggleLucky, 17);
            luckyLabel = luckyButton.GetComponentInChildren<Text>();
            popText = Txt(plate.transform, "PopText", "", 72, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 90f), TextAnchor.MiddleCenter, true); popText.raycastTarget = false;
            powerPop = Txt(c, "PowerPop", "", 30, new Color(0.55f, 1f, 0.6f), new Vector2(1f, 1f), new Vector2(-420f, -168f), new Vector2(330f, 44f), TextAnchor.MiddleCenter, true); powerPop.raycastTarget = false;
            anticipationBar = Make<Image>(plate.transform, "AnticipationBar", new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(0f, 10f)); anticipationBar.color = new Color(1f, 0.8f, 0.3f, 0.95f); anticipationBar.raycastTarget = false;
            strikeButton = Btn(c, "Strike", "ĐẬP ĐỒ!", new Vector2(1f, 1f), new Vector2(-310f, -716f), new Vector2(360f, 70f), new Color(0.82f, 0.6f, 0.18f), Strike, 32);
            msg = Txt(c, "Message", "", 22, new Color(1f, 0.78f, 0.6f), new Vector2(0f, 0f), new Vector2(350f, 56f), new Vector2(540f, 70f), TextAnchor.MiddleLeft);
            root.SetActive(false);
            talkButton = Btn(transform, "TalkToBlacksmith", "Nói chuyện", new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(260f, 74f), new Color(0.26f, 0.56f, 0.62f), () => TalkRequested?.Invoke());
            talkButton.gameObject.SetActive(false);
        }

        private Button talkButton;
        public Button TalkButton { get { Build(); return talkButton; } }
        /// <summary>Raised by the on-screen "Nói chuyện" button (touch); the NPC opens the forge.</summary>
        public event Action TalkRequested;

        // ----------------------------------------------------------------- open / close
        public void Open(string greeting)
        {
            Build();
            root.SetActive(true);
            transform.SetAsLastSibling();
            greet.text = greeting;
            SetInput(false);
            SetMessage("");
            Reload();
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

        private void Reload()
        {
            var world = AdventureWorldSession.Current;
            if (!world || world.Character == null || !AdventureServerConnection.Instance) return;
            Busy = true;
            AdventureServerConnection.Instance.GetInventory(world.Character.characterId, (ok, inv, err) =>
            {
                Busy = false;
                if (ok && inv != null) Inv = inv; else SetMessage(err);
                Redraw();
            });
        }

        // ----------------------------------------------------------------- state
        public BagSlotState Current => Inv?.equipped?.FirstOrDefault(e => e.equipSlot == SelectedSlot)?.item;
        public int StoneCount => Inv == null ? 0 : Inv.slots.Where(s => s.itemId == "enhance_stone").Sum(s => s.qty);
        public int CharmCount => Inv == null ? 0 : Inv.slots.Where(s => s.itemId == "protection_charm").Sum(s => s.qty);
        public bool CanStrike
        {
            get
            {
                var world = AdventureWorldSession.Current;
                BagSlotState it = Current;
                return !Busy && it != null && it.enhanceLevel < 15 && StoneCount > 0 && world && world.Character != null && world.Character.gold >= it.enhanceGold
                    && (!UseProtection || CharmCount > 0);
            }
        }

        public void SelectSlot(string slot) { SelectedSlot = slot; pendingId = null; Redraw(); }
        public void ToggleProtection() { UseProtection = !UseProtection; pendingId = null; Redraw(); }
        private void SetMessage(string t) { LastMessage = t ?? ""; if (msg) msg.text = LastMessage; }

        private void Redraw()
        {
            if (!root) return;
            var world = AdventureWorldSession.Current;
            long gold = world && world.Character != null ? world.Character.gold : 0;
            goldLabel.text = gold.ToString("N0", new System.Globalization.CultureInfo("vi-VN"));
            for (int i = 0; i < Slots.Length; i++)
            {
                BagSlotState it = Inv?.equipped?.FirstOrDefault(e => e.equipSlot == Slots[i].slot)?.item;
                var (bg, ic, nm, lv) = rows[i];
                ic.sprite = it != null ? ItemIconBook.For(it.itemId, it.icon) : null; ic.enabled = ic.sprite;
                nm.text = it != null ? it.name : Slots[i].label + ": trống";
                lv.text = it != null ? (it.enhanceLevel > 0 ? $"+{it.enhanceLevel}  ·  {Slots[i].label}" : Slots[i].label + "  ·  chưa cường hóa") : "Hãy mặc trang bị để cường hóa";
                bg.color = Slots[i].slot == SelectedSlot ? new Color(1f, 0.85f, 0.45f) : new Color(0.55f, 0.6f, 0.78f, 0.9f);
            }
            BagSlotState cur = Current;
            bigIcon.sprite = cur != null ? ItemIconBook.For(cur.itemId, cur.icon) : null; bigIcon.enabled = bigIcon.sprite;
            if (cur == null)
            {
                itemName.text = "Chưa có trang bị"; levelLine.text = ""; statLine.text = ""; stoneLine.text = ""; goldLine.text = "";
                chanceLabel.text = ""; failLine.text = ""; chanceFill.rectTransform.sizeDelta = new Vector2(0f, 18f);
                protectLabel.text = "Bùa Bảo Vệ"; strikeButton.interactable = false;
                return;
            }
            bool max = cur.enhanceLevel >= 15;
            itemName.text = cur.name;
            levelLine.text = max ? $"+{cur.enhanceLevel}  (tối đa)" : $"+{cur.enhanceLevel}  →  +{cur.enhanceLevel + 1}";
            var parts = new System.Collections.Generic.List<string>();
            if (cur.attack > 0) parts.Add($"Công {cur.attack} → {(max ? cur.attack : cur.nextAttack)}");
            if (cur.defense > 0) parts.Add($"Thủ {cur.defense} → {(max ? cur.defense : cur.nextDefense)}");
            statLine.text = parts.Count > 0 ? string.Join("   ", parts) : "Trang bị phụ: không cộng chỉ số";
            if (!max && cur.powerGain > 0) statLine.text += $"\n<color=#8CF28F>Lực Chiến {Inv.combatPower:N0} → {Inv.combatPower + cur.powerGain:N0}  (+{cur.powerGain})</color>";
            int stones = StoneCount;
            stoneLine.text = $"Đá Cường Hóa: <color=#{(stones > 0 ? "8CF28F" : "FF807A")}>{stones}</color> / 1";
            goldLine.text = $"Vàng: <color=#{(gold >= cur.enhanceGold ? "8CF28F" : "FF807A")}>{gold:N0}</color> / {cur.enhanceGold:N0}";
            int shown = Mathf.Min(100, cur.enhanceChance + Inv.blessing * 2 + (UseLucky && LuckyCount > 0 ? 10 : 0));
            string extra = (Inv.blessing > 0 ? $"  <color=#FFD35C>(Chúc Phúc +{Inv.blessing * 2}%)</color>" : "") + (UseLucky && LuckyCount > 0 ? "  <color=#7FD6FF>(May Mắn +10%)</color>" : "");
            chanceLabel.text = max ? "Đã đạt cấp tối đa" : $"[{cur.tier}]  Tỉ lệ thành công: {shown}%{extra}";
            chanceFill.rectTransform.sizeDelta = new Vector2(500f * Mathf.Clamp01(shown / 100f), 18f);
            chanceFill.color = shown >= 70 ? Good : shown >= 40 ? Gold : Bad;
            failLine.text = max ? "" : cur.failDropPercent == 0 ? "Thất bại: KHÔNG tụt cấp (chỉ mất đá + Vàng)." :
                UseProtection ? "Thất bại: giữ nguyên cấp nhờ Bùa (mất đá + Bùa + Vàng)." : $"Thất bại: {cur.failDropPercent}% tụt 1 cấp (không bao giờ vỡ đồ).";
            protectLabel.text = $"Bùa Bảo Vệ: {(UseProtection ? "BẬT" : "tắt")}  (có {CharmCount})";
            if (luckyButton) { luckyButton.interactable = LuckyCount > 0 && !Busy; luckyLabel.text = $"Đá May Mắn: {(UseLucky && LuckyCount > 0 ? "BẬT" : "tắt")} (có {LuckyCount})"; luckyButton.GetComponent<Image>().color = UseLucky && LuckyCount > 0 ? new Color(0.2f, 0.55f, 0.7f) : new Color(0.3f, 0.36f, 0.55f); }
            protectButton.GetComponent<Image>().color = UseProtection ? new Color(0.48f, 0.3f, 0.7f) : new Color(0.3f, 0.36f, 0.55f);
            strikeButton.interactable = CanStrike;
        }

        // ----------------------------------------------------------------- strike
        public void Strike()
        {
            var world = AdventureWorldSession.Current;
            if (!CanStrike || !AdventureServerConnection.Instance) return;
            if (pendingId == null || pendingSlot != SelectedSlot || pendingProtect != UseProtection)
            {
                pendingId = Guid.NewGuid().ToString("N");
                pendingSlot = SelectedSlot;
                pendingProtect = UseProtection;
            }
            Busy = true;
            Redraw();
            StopAllCoroutines();
            StartCoroutine(Anticipate());                    // ~1.2 s of hammering, growing sparks and a filling bar before the result bursts out
            float started = Time.unscaledTime;
            bool useLucky = UseLucky && LuckyCount > 0;
            AdventureServerConnection.Instance.EnhanceItem(world.Character.characterId, SelectedSlot, UseProtection, pendingId, (ok, r, err) =>
            {
                float wait = Mathf.Max(0f, AnticipationSeconds - (Time.unscaledTime - started));
                StartCoroutine(RevealAfter(wait, ok, r, err));
            }, useLucky);
        }

        private bool anticipating;
        private IEnumerator Anticipate()
        {
            anticipating = true;
            var sparks = SpawnSparks(10, TierGlow(Current != null ? Current.tier : ""), up: true);
            float nextHit = 0f;
            for (float t = 0f; t < AnticipationSeconds && anticipating; t += Time.unscaledDeltaTime)
            {
                float k = t / AnticipationSeconds;
                if (t >= nextHit) { ForgeSfx.Play(ForgeSfx.Strike, 0.45f + 0.4f * k); nextHit = t + Mathf.Lerp(0.26f, 0.1f, k); }        // faster and faster hammer
                bigIcon.rectTransform.anchoredPosition = new Vector2(Mathf.Round(Mathf.Sin(t * 90f) * 5f * k), Mathf.Round(Mathf.Abs(Mathf.Sin(t * 40f)) * -3f * k));
                bigIcon.rectTransform.localScale = Vector3.one * (1f - 0.05f * Mathf.Abs(Mathf.Sin(t * 40f)));
                Color g = TierGlow(Current != null ? Current.tier : ""); g.a = 0.12f + 0.5f * k * k; flash.color = g;                  // the glow brightens before the result
                if (anticipationBar) anticipationBar.rectTransform.sizeDelta = new Vector2(170f * k, 10f);
                MoveSparks(sparks, (t * 1.6f) % 1f, true);
                yield return null;
            }
            Clear(sparks);
            if (anticipationBar) anticipationBar.rectTransform.sizeDelta = new Vector2(0f, 10f);
            bigIcon.rectTransform.anchoredPosition = Vector2.zero; bigIcon.rectTransform.localScale = Vector3.one;
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        private IEnumerator RevealAfter(float wait, bool ok, EnhanceResult r, string err)
        {
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            anticipating = false;
            yield return null;
            Reveal(ok, r, err);
        }

        private void Reveal(bool ok, EnhanceResult r, string err)
        {
            var world = AdventureWorldSession.Current;
            {
                Busy = false;
                if (!ok || r == null) { SetMessage(err ?? "Mất kết nối. Nhấn lại để kiểm tra cùng giao dịch."); Redraw(); return; }
                pendingId = null;
                Strikes++;
                LastSuccess = r.success;
                Inv = r.inventory;
                world.Character.gold = r.gold;
                var bag = FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include);
                if (bag) bag.Refresh();
                SetMessage(r.success ? $"Thành công! +{r.afterLevel}  (−{r.goldSpent:N0} Vàng)" :
                    r.afterLevel < r.beforeLevel ? $"Thất bại, tụt còn +{r.afterLevel}.  (−{r.goldSpent:N0} Vàng)" : $"Thất bại, giữ nguyên +{r.afterLevel}.  (−{r.goldSpent:N0} Vàng)");
                msg.color = r.success ? Good : Bad;
                int gainedPower = r.combatPowerAfter - r.combatPowerBefore;
                popText.text = r.success ? "+" + r.afterLevel : r.lostLevel ? "-1" : "";
                powerPop.text = r.success && gainedPower > 0 ? $"+{gainedPower:N0} Lực Chiến!" : "";
                if (r.success && (r.tier == "Kim Cương" || r.tier == "Đỏ") && r.afterLevel >= 10)
                {
                    LastBanner = $"Chúc mừng {world.Character.characterName}! {Current?.name ?? "Trang bị"} [{r.tier}] đạt +{r.afterLevel}!";
                    QuickGoalToast.Show(LastBanner);
                }
                StopAllCoroutines();
                anticipating = false;
                StartCoroutine(r.success ? SuccessFx(TierGlow(r.tier ?? "")) : FailureFx());
                Redraw();
            }
        }

        private IEnumerator SuccessFx(Color glow)
        {
            ForgeSfx.Play(ForgeSfx.Success, 1f);
            var sparks = SpawnSparks(26, glow, up: true);
            for (float t = 0f; t < 1.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 1.3f;
                flash.color = new Color(glow.r, glow.g, glow.b, Mathf.Clamp01(1f - k * 1.6f) * 0.95f);      // dazzling flash in the colour of the tier
                bigIcon.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.35f * (1f - k * 2.5f)));
                if (popText) { popText.color = new Color(glow.r, glow.g, glow.b, Mathf.Clamp01(1.6f - k * 1.5f)); popText.rectTransform.localScale = Vector3.one * (1f + 0.9f * Mathf.Sin(Mathf.Clamp01(k * 3f) * Mathf.PI * 0.5f)); popText.rectTransform.anchoredPosition = new Vector2(0f, 40f * k); }
                if (powerPop) { powerPop.color = new Color(0.55f, 1f, 0.6f, Mathf.Clamp01(2f - k * 1.6f)); powerPop.rectTransform.anchoredPosition = new Vector2(-420f, -168f + 26f * k); }
                MoveSparks(sparks, k, true);
                yield return null;
            }
            Clear(sparks);
            flash.color = new Color(1f, 1f, 1f, 0f);
            bigIcon.rectTransform.localScale = Vector3.one;
            if (popText) { popText.text = ""; popText.rectTransform.localScale = Vector3.one; popText.rectTransform.anchoredPosition = Vector2.zero; }
            if (powerPop) powerPop.text = "";
        }

        private IEnumerator FailureFx()
        {
            ForgeSfx.Play(ForgeSfx.Failure, 0.85f);
            var sparks = SpawnSparks(10, new Color(0.55f, 0.55f, 0.6f), up: false);
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.7f;
                flash.color = new Color(0.9f, 0.15f, 0.12f, Mathf.Clamp01(1f - k * 2f) * 0.55f);
                float shake = Mathf.Sin(t * 70f) * 9f * (1f - k);
                bigIcon.rectTransform.anchoredPosition = new Vector2(Mathf.Round(shake), 0f);
                MoveSparks(sparks, k, false);
                yield return null;
            }
            Clear(sparks);
            flash.color = new Color(1f, 1f, 1f, 0f);
            bigIcon.rectTransform.anchoredPosition = Vector2.zero;
            if (popText) popText.text = "";
        }

        private System.Collections.Generic.List<(Image img, Vector2 dir)> SpawnSparks(int n, Color col, bool up)
        {
            var list = new System.Collections.Generic.List<(Image, Vector2)>();
            var rng = new System.Random(Strikes * 31 + 7);
            for (int i = 0; i < n; i++)
            {
                Image s = Make<Image>(fxLayer, "Spark" + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f)); // hard 8 px squares = pixel particles
                s.color = col; s.raycastTarget = false;
                float a = (float)(rng.NextDouble() * Math.PI * 2), sp = 80f + (float)rng.NextDouble() * 150f;
                list.Add((s, new Vector2(Mathf.Cos(a) * sp, (up ? Mathf.Abs(Mathf.Sin(a)) : Mathf.Sin(a) * 0.5f - 0.2f) * sp)));
            }
            return list;
        }

        private static void MoveSparks(System.Collections.Generic.List<(Image img, Vector2 dir)> sparks, float k, bool rise)
        {
            foreach (var (img, dir) in sparks)
            {
                if (!img) continue;
                Vector2 p = dir * k + new Vector2(0f, rise ? 40f * k : -90f * k * k);
                img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x / 4f) * 4f, Mathf.Round(p.y / 4f) * 4f);   // snapped to a 4 px grid
                var c = img.color; c.a = 1f - k; img.color = c;
            }
        }

        private static void Clear(System.Collections.Generic.List<(Image img, Vector2 dir)> sparks)
        {
            foreach (var (img, _) in sparks) if (img) Destroy(img.gameObject);
        }
    }
}
