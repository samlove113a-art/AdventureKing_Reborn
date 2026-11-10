using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>Skill table from the server (/skills), loaded once per session; display only — the server decides.</summary>
    public static class SkillBook
    {
        public static SkillConfigDto Config { get; private set; }
        private static bool loading;
        private static readonly List<Action> waiting = new List<Action>();

        public static void Ensure(Action ready)
        {
            if (Config != null) { ready?.Invoke(); return; }
            if (ready != null) waiting.Add(ready);
            if (loading || !AdventureServerConnection.Instance) return;
            loading = true;
            AdventureServerConnection.Instance.GetSkills((ok, cfg, err) =>
            {
                loading = false;
                if (ok && cfg != null) Config = cfg;
                var w = waiting.ToArray(); waiting.Clear();
                foreach (var a in w) a?.Invoke();
            });
        }

        public static ClassSkillsDto For(HeroClass c) => Config?.classes?.FirstOrDefault(x => x.heroClass == (int)c);
        public static SkillDefDto Skill(HeroClass c, int slot) => For(c)?.skills?.FirstOrDefault(s => s.slot == slot);
        public static int Cost(SkillDefDto s, int level) => s == null || Config == null ? 0 : Mathf.RoundToInt(s.mpCost * (1f + Config.mpCostPerLevel * Mathf.Max(0, level - 1)));
    }


    /// <summary>
    /// Bottom-right combat cluster for PC and touch: big basic-attack button, the class's three skills on an arc
    /// (skill 3 in the ornate frame), Auto Skill toggle (+ priority order), Pickup, HP/MP potions with live counts and the
    /// bag. Keys: J attack, 1/2/3 skills, ↓/S pickup, Q / R potions, I bag. Cooldown sweeps + seconds, the not-enough-MP
    /// tint and the AUTO glow are live UI (nothing baked into the icons). Everything that matters is decided by the
    /// server: this only asks (FarmField.CastSkill / TryPickup, InventoryPanel.UsePotion).
    /// Auto Skill only casts when a valid monster is in range, MP is enough and the skill is off cooldown — it never
    /// moves the hero, never looks for monsters elsewhere and never picks up Gold.
    /// </summary>
    public sealed class HeroActionBar : MonoBehaviour
    {
        [SerializeField] private Sprite[] skillIcons = new Sprite[12]; // [class * 3 + slot - 1]
        [SerializeField] private Sprite attackIcon, autoIcon, pickupIcon, hpIcon, mpIcon, bagIcon, frame, frameUltimate;
        [SerializeField] private Font font;
        [SerializeField] private InventoryPanel inventory;
        [SerializeField] private PetPanel petPanel;
        [SerializeField] private Sprite petIcon;

        private const string AutoKey = "ak_auto_skill", AutoOrderKey = "ak_auto_order";
        private static readonly int[][] Orders = { new[] { 3, 2, 1 }, new[] { 1, 2, 3 }, new[] { 2, 3, 1 }, new[] { 3, 1, 2 } };

        private sealed class SkillSlot
        {
            public int Slot;
            public RectTransform Root;
            public Image Icon, Sweep;
            public Text Seconds, MpWarn, Key;
            public ActionPress Press;
        }

        private readonly SkillSlot[] slots = new SkillSlot[3];
        private Image autoGlow;
        private Text autoLabel, orderLabel, hpCount, mpCount, message;
        private float messageUntil, nextAuto;
        private AdventureWorldSession world;
        private PlayerInput2D input;
        private bool built;

        public bool Auto { get; private set; }
        public int[] AutoOrder => Orders[orderIndex];
        private int orderIndex;
        public int AutoCasts { get; private set; }
        public string Message => message ? message.text : "";
        public ActionPress AttackButton { get; private set; }
        public ActionPress PickupButton { get; private set; }
        public ActionPress HpButton { get; private set; }
        public ActionPress MpButton { get; private set; }
        public ActionPress BagButton { get; private set; }
        public ActionPress PetButton { get; private set; }
        public ActionPress AutoButton { get; private set; }
        public ActionPress SkillButton(int slot) => slots[slot - 1]?.Press;
        public string SecondsShown(int slot) => slots[slot - 1]?.Seconds && slots[slot - 1].Seconds.enabled ? slots[slot - 1].Seconds.text : "";
        public float SweepShown(int slot) => slots[slot - 1]?.Sweep ? slots[slot - 1].Sweep.fillAmount : 0f;
        public bool MpWarnShown(int slot) => slots[slot - 1]?.MpWarn && slots[slot - 1].MpWarn.enabled;
        public Sprite IconShown(int slot) => slots[slot - 1]?.Icon ? slots[slot - 1].Icon.sprite : null;
        public string HpCountShown => hpCount ? hpCount.text : "";
        public string MpCountShown => mpCount ? mpCount.text : "";

        private void Start()
        {
            try { Auto = PlayerPrefs.GetInt(AutoKey, 0) == 1; orderIndex = Mathf.Clamp(PlayerPrefs.GetInt(AutoOrderKey, 0), 0, Orders.Length - 1); } catch { }
            Build();
            SkillBook.Ensure(null);
        }

        // ------------------------------------------------------------------ UI
        private Image Img(Transform parent, string name, Sprite s, Vector2 pos, float size, Color? c = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.color = c ?? Color.white;
            img.preserveAspect = true;
            return img;
        }

        private Text Label(Transform parent, string name, string text, int size, Color c, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<Text>();
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow; // a short box never swallows the line
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.03f, 0.08f, 0.95f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        /// <summary>Round button: icon + frame, anchored to the bottom-right corner at pos (centre offset).</summary>
        private RectTransform Button(string name, Sprite icon, Sprite frm, Vector2 pos, float size, out Image iconImg, out ActionPress press, string key)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // whole square is the touch target
            iconImg = Img(rt, "Icon", icon, Vector2.zero, size * 0.86f);
            iconImg.raycastTarget = false;
            if (frm) Img(rt, "Frame", frm, Vector2.zero, size * (frm == frameUltimate ? 1.18f : 1.02f)).raycastTarget = false;
            press = go.AddComponent<ActionPress>();
            if (!string.IsNullOrEmpty(key) && !Application.isMobilePlatform)
            {
                var badge = Img(rt, "KeyBadge", null, new Vector2(size * 0.36f, -size * 0.36f), 30f, new Color(0.08f, 0.06f, 0.14f, 0.85f));
                badge.raycastTarget = false;
                Label(badge.transform, "Key", key, 18, new Color(1f, 0.95f, 0.75f), Vector2.zero, new Vector2(30f, 30f));
            }
            return rt;
        }

        private void Build()
        {
            if (built) return;
            built = true;
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

            Button("Attack", attackIcon, frame, new Vector2(-165f, 170f), 190f, out _, out ActionPress atk, "J");
            AttackButton = atk;
            atk.Down = () => { if (Input2D) Input2D.SetTouchHeld(PlayerInput2D.TouchAction.Attack, true); };
            atk.Up = () => { if (Input2D) Input2D.SetTouchHeld(PlayerInput2D.TouchAction.Attack, false); };

            Vector2[] pos = { new Vector2(-385f, 135f), new Vector2(-345f, 330f), new Vector2(-165f, 400f) };
            for (int i = 0; i < 3; i++)
            {
                int slot = i + 1;
                float size = slot == 3 ? 132f : 116f;
                RectTransform root = Button("Skill" + slot, null, slot == 3 ? frameUltimate : frame, pos[i], size, out Image icon, out ActionPress press, slot.ToString());
                var s = new SkillSlot { Slot = slot, Root = root, Icon = icon, Press = press };
                // cooldown sweep (radial, dark) + seconds + MP warning: live UI on top of the icon
                s.Sweep = Img(root, "CooldownSweep", Circle(), Vector2.zero, size * 0.84f, new Color(0.02f, 0.02f, 0.06f, 0.68f));
                s.Sweep.type = Image.Type.Filled;
                s.Sweep.fillMethod = Image.FillMethod.Radial360;
                s.Sweep.fillOrigin = (int)Image.Origin360.Top;
                s.Sweep.fillClockwise = false;
                s.Sweep.fillAmount = 0f;
                s.Sweep.raycastTarget = false;
                s.Seconds = Label(root, "Seconds", "", slot == 3 ? 44 : 40, Color.white, Vector2.zero, new Vector2(size, size));
                s.Seconds.enabled = false;
                s.MpWarn = Label(root, "NoMp", "MP", 22, new Color(0.55f, 0.75f, 1f), new Vector2(0f, -size * 0.3f), new Vector2(size, 30f));
                s.MpWarn.enabled = false;
                press.Down = () => TryCast(slot, false);
                slots[i] = s;
            }

            // Auto sits left of skill 2 (clear of the screen edge / notch inset and of skill 3's key badge), labels above it
            Button("Auto", autoIcon, frame, new Vector2(-470f, 470f), 84f, out _, out ActionPress auto, "");
            AutoButton = auto;
            autoGlow = Img(auto.transform, "AutoGlow", Circle(), Vector2.zero, 96f, new Color(0.45f, 0.95f, 1f, 0.45f));
            autoGlow.raycastTarget = false;
            autoGlow.transform.SetAsFirstSibling();
            autoLabel = Label(auto.transform, "AutoLabel", "AUTO", 20, Color.white, new Vector2(0f, 60f), new Vector2(140f, 30f));
            orderLabel = Label(auto.transform, "Order", "", 19, new Color(1f, 0.92f, 0.65f), new Vector2(0f, 88f), new Vector2(140f, 30f));
            auto.Down = ToggleAuto;
            // tapping the order line cycles the priority (3›2›1 → 1›2›3 → …)
            orderLabel.raycastTarget = true; // the text itself is the tap target (one Graphic per GameObject)
            orderLabel.gameObject.AddComponent<ActionPress>().Down = CycleAutoOrder;

            Button("Pickup", pickupIcon, frame, new Vector2(-560f, 125f), 100f, out _, out ActionPress pick, "S");
            PickupButton = pick;
            pick.Down = () => TryPickup(true);
            Button("PotionHp", hpIcon, frame, new Vector2(-560f, 250f), 86f, out _, out ActionPress hp, "Q");
            HpButton = hp;
            hpCount = Label(hp.transform, "Count", "0", 20, Color.white, new Vector2(30f, 30f), new Vector2(60f, 30f));
            hp.Down = () => UsePotion(true);
            Button("PotionMp", mpIcon, frame, new Vector2(-560f, 360f), 86f, out _, out ActionPress mp, "R");
            MpButton = mp;
            mpCount = Label(mp.transform, "Count", "0", 20, Color.white, new Vector2(30f, 30f), new Vector2(60f, 30f));
            mp.Down = () => UsePotion(false);
            Button("Bag", bagIcon, frame, new Vector2(-690f, 125f), 90f, out _, out ActionPress bag, "I");
            BagButton = bag;
            bag.Down = () => { if (inventory) inventory.Toggle(); };
            Button("Pet", petIcon, frame, new Vector2(-690f, 250f), 90f, out _, out ActionPress pet, "P");
            PetButton = pet;
            pet.Down = () => { if (petPanel) petPanel.Toggle(); };

            message = Label(transform, "ActionMessage", "", 26, new Color(1f, 0.9f, 0.7f), Vector2.zero, new Vector2(900f, 34f));
            var mrt = message.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0f); // bottom centre, above the key hints (clear of the HUD icons)
            mrt.anchoredPosition = new Vector2(0f, 160f); // above the world-status line (y 110) and the key hints
            group = gameObject.AddComponent<CanvasGroup>();
            RefreshAutoUi();
        }

        private CanvasGroup group;
        /// <summary>The cluster steps aside while player control is off (NPC dialog, travel, knocked out).</summary>
        public bool Shown => group && group.alpha > 0.5f;

        private static Sprite circle;
        private static Sprite Circle()
        {
            if (circle) return circle;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude;
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(n / 2f - r));
                }
            tex.SetPixels(px); tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return circle;
        }

        // ------------------------------------------------------------------ actions
        private PlayerInput2D Input2D
        {
            get
            {
                if (!input && AdventureWorldSession.Current && AdventureWorldSession.Current.Player)
                    input = AdventureWorldSession.Current.Player.GetComponent<PlayerInput2D>();
                return input;
            }
        }

        private void Say(string text, float seconds = 2.2f)
        {
            if (!message) return;
            message.text = text ?? "";
            messageUntil = Time.time + seconds;
        }

        private HeroClass Class => world && world.Character != null ? world.Character.heroClass : HeroClass.Swordsman;
        private int Level => world && world.Character != null ? Mathf.Max(1, world.Character.level) : 1;

        /// <summary>Skill press (button, key or Auto). Client checks are only for feedback; the server decides.</summary>
        public void TryCast(int slot, bool fromAuto)
        {
            if (!world || world.Character == null) return;
            if (Input2D && !Input2D.enabled) return; // fallen / in a dialog
            FarmField farm = FarmField.Current;
            if (!farm)
            {
                // safe zone: skills are practice only (animation, no damage, no MP) — the tutorial quest counts it
                if (fromAuto) return;
                var visual = world.Player.GetComponent<CharacterVisual3D>();
                if (SkillVfxDirector.IsCasting(world.Player.transform)) return;
                // practice: the full skill effect plays at a point ahead (no monster, no damage)
                var motor = world.Player.GetComponent<PlayerMotor2D>();
                int facing = motor ? motor.Facing : 1;
                SkillDefDto practice = SkillBook.Skill(Class, slot);
                float reach = practice == null ? 3f : practice.shape == "self" ? 0f : Mathf.Min(4f, Mathf.Max(2f, practice.range * 0.6f));
                SkillVfxDirector.Play(new SkillCast
                {
                    Class = Class, Slot = slot, Hero = world.Player.transform, Visual = visual, Facing = facing,
                    Target = world.Player.transform.position + Vector3.right * facing * reach,
                    Radius = practice == null ? 2f : Mathf.Max(1.4f, practice.radius), Range = practice == null ? 4f : Mathf.Min(5f, practice.range),
                });
                Say("Luyện tập kỹ năng (trong làng không gây sát thương — khu an toàn).");
                QuestBook.Event("practice:skill" + slot);
                return;
            }
            SkillDefDto def = SkillBook.Skill(Class, slot);
            if (def == null) { if (!fromAuto) Say("Đang tải kỹ năng..."); SkillBook.Ensure(null); return; }
            float left = farm.CooldownEnds[slot - 1] - Time.time;
            if (left > 0f) { if (!fromAuto) Say($"{def.name} đang hồi chiêu ({Mathf.CeilToInt(left)} giây)."); return; }
            int cost = SkillBook.Cost(def, Level);
            var mana = world.Player.GetComponent<PlayerMana>();
            if (mana && mana.Current < cost) { if (!fromAuto) Say($"Không đủ MP để dùng {def.name} (cần {cost})."); return; }
            if (fromAuto) AutoCasts++;
            farm.CastSkill(slot, def, (ok, msg) => { if (!ok && !fromAuto) Say(msg); });
        }

        public void TryPickup(bool fromButton)
        {
            FarmField farm = FarmField.Current;
            if (!farm) { if (fromButton) Say("Không có Vàng để nhặt ở đây."); return; }
            if (!farm.TryPickup() && fromButton) Say("Không có Vàng ở gần. Hãy lại gần đồng Vàng.");
        }

        public void UsePotion(bool hp)
        {
            if (!inventory) return;
            inventory.UsePotion(hp, msg => Say(msg));
        }

        public void ToggleAuto()
        {
            Auto = !Auto;
            try { PlayerPrefs.SetInt(AutoKey, Auto ? 1 : 0); } catch { }
            RefreshAutoUi();
            Say(Auto ? "Auto Skill: BẬT (chỉ dùng kỹ năng khi có quái trong tầm)" : "Auto Skill: TẮT");
        }

        public void CycleAutoOrder()
        {
            orderIndex = (orderIndex + 1) % Orders.Length;
            try { PlayerPrefs.SetInt(AutoOrderKey, orderIndex); } catch { }
            RefreshAutoUi();
        }

        private void RefreshAutoUi()
        {
            if (autoGlow) autoGlow.enabled = Auto;
            if (autoLabel) { autoLabel.text = Auto ? "AUTO: BẬT" : "AUTO: TẮT"; autoLabel.color = Auto ? new Color(0.6f, 1f, 0.95f) : new Color(0.96f, 0.96f, 1f); }
            if (orderLabel) orderLabel.text = string.Join(" › ", AutoOrder);
        }

        // ------------------------------------------------------------------ frame
        private PlayerMelee2D hookedMelee;
        private void OnSwing() { if (!FarmField.Current) QuestBook.Event("practice:attack"); }

        private void Update()
        {
            if (!world) world = AdventureWorldSession.Current;
            if (!world || world.Character == null || !world.Player) return;
            var melee = world.Player.GetComponent<PlayerMelee2D>();
            if (melee && melee != hookedMelee) { if (hookedMelee) hookedMelee.Attacked -= OnSwing; hookedMelee = melee; melee.Attacked += OnSwing; }
            PlayerInput2D inp = Input2D;
            if (group)
            {
                bool show = !inp || inp.enabled;
                group.alpha = show ? 1f : 0f;
                group.interactable = group.blocksRaycasts = show;
            }
            if (inp)
            {
                for (int s = 1; s <= 3; s++) if (inp.ConsumeSkill(s)) TryCast(s, false);
                if (inp.ConsumePickup()) TryPickup(false);
                if (inp.ConsumePotionHp()) UsePotion(true);
                if (inp.ConsumePotionMp()) UsePotion(false);
                if (inp.ConsumeInventory() && inventory) inventory.Toggle();
            }

            FarmField farm = FarmField.Current;
            RefreshSlots();

            if (Auto && farm && Time.time >= nextAuto)
            {
                nextAuto = Time.time + 0.25f;
                foreach (int slot in AutoOrder)
                {
                    SkillDefDto def = SkillBook.Skill(Class, slot);
                    if (def != null && farm.CanCastNow(slot, def, SkillBook.Cost(def, Level))) { TryCast(slot, true); break; }
                }
            }
            if (autoGlow && Auto) autoGlow.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 5f));

            if (inventory)
            {
                if (hpCount) hpCount.text = inventory.PotionCount(true).ToString();
                if (mpCount) mpCount.text = inventory.PotionCount(false).ToString();
            }
            if (message && message.text.Length > 0 && Time.time > messageUntil) message.text = "";
        }

        /// <summary>Icons, cooldown sweeps / seconds and the not-enough-MP state from the current values.</summary>
        public void RefreshSlots()
        {
            if (!world || world.Character == null || !world.Player) return;
            FarmField farm = FarmField.Current;
            var mana = world.Player.GetComponent<PlayerMana>();
            for (int i = 0; i < 3; i++)
            {
                SkillSlot s = slots[i];
                if (s == null) continue;
                int idx = (int)Class * 3 + i;
                if (skillIcons != null && idx < skillIcons.Length && s.Icon.sprite != skillIcons[idx]) s.Icon.sprite = skillIcons[idx];
                SkillDefDto def = SkillBook.Skill(Class, i + 1);
                float left = farm ? farm.CooldownEnds[i] - Time.time : 0f;
                bool cooling = def != null && left > 0.05f;
                s.Sweep.fillAmount = cooling ? Mathf.Clamp01(left / Mathf.Max(0.1f, def.cooldownSeconds)) : 0f;
                s.Seconds.enabled = cooling;
                if (cooling) s.Seconds.text = Mathf.CeilToInt(left).ToString();
                bool noMp = def != null && mana && mana.Current < SkillBook.Cost(def, Level);
                s.MpWarn.enabled = noMp && !cooling;
                s.Icon.color = noMp ? new Color(0.45f, 0.5f, 0.72f) : Color.white;
            }
        }
    }
}
