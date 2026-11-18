using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "LỰC CHIẾN" in big numerals at the top centre of the world screen. The value is the server's (InventoryState.combatPower, the same
    /// formula as the ranking), so it only moves when the server says it did: equipping / unequipping gear, levelling up. Every change counts
    /// up (or down) over about a second with a punch of the numerals, a green (gain) or red (loss) flash and a floating "+123".
    /// Created by InventoryPanel for every world scene; owns its own overlay canvas (1920x1080 standard scaler).
    /// </summary>
    public sealed class CombatPowerHud : MonoBehaviour
    {
        private const float CountSeconds = 0.9f, PunchSeconds = 0.45f, FlashSeconds = 0.9f;
        private static readonly Color Ink = new Color(0.08f, 0.05f, 0.14f), Gain = new Color(0.4f, 1f, 0.5f), Loss = new Color(1f, 0.38f, 0.34f), Gold = new Color(1f, 0.86f, 0.4f);

        private InventoryPanel panel;
        private AdventureWorldSession world;
        private Font font;
        private Canvas canvas;
        private RectTransform card;
        private Text number, delta;
        private int shown = -1, target = -1, from;
        private float countT = 1f, punchT = 1f, flashT = 1f, deltaT = 1f, nextLevelCheck;
        private int lastLevel;
        private Color flash = Color.white;

        /// <summary>The number on screen right now (mid-count while it rolls).</summary>
        public int Shown => shown;
        /// <summary>The server value it is counting toward.</summary>
        public int Target => target;
        public bool Counting => countT < 1f;
        public string Text => number ? number.text : "";

        public static CombatPowerHud Attach(InventoryPanel owner, Font uiFont)
        {
            var existing = owner.GetComponent<CombatPowerHud>();
            if (existing) return existing;
            var hud = owner.gameObject.AddComponent<CombatPowerHud>();
            hud.panel = owner; hud.font = uiFont;
            owner.StateChanged += hud.OnInventory;
            if (owner.State != null) hud.OnInventory(owner.State);
            return hud;
        }

        private void OnDestroy()
        {
            if (panel) panel.StateChanged -= OnInventory;
            if (canvas) Destroy(canvas.gameObject);      // the overlay canvas is a scene root (see EnsureUi), so it goes with this component
        }

        public RectTransform Card => card;
        public Canvas Canvas => canvas;

        private void OnInventory(InventoryState inv) { if (inv != null) SetPower(inv.combatPower); }

        /// <summary>Shows <paramref name="power"/>: the first value appears at once, later ones count from what is on screen.</summary>
        public void SetPower(int power)
        {
            EnsureUi();
            if (target < 0) { shown = target = power; Render(); return; }
            if (power == target) return;
            int diff = power - target;
            from = shown; target = power; countT = 0f; punchT = 0f; flashT = 0f; deltaT = 0f;
            flash = diff > 0 ? Gain : Loss;
            delta.text = (diff > 0 ? "+" : "−") + Mathf.Abs(diff).ToString("N0");
            delta.color = flash;
        }

        private void Update()
        {
            if (!world) world = AdventureWorldSession.Current;
            // a level-up changes the power without touching the bag: ask the server again when the level moves
            if (world && world.Character != null && Time.unscaledTime >= nextLevelCheck)
            {
                nextLevelCheck = Time.unscaledTime + 2f;
                if (lastLevel != 0 && world.Character.level != lastLevel && panel && !panel.Busy) panel.Refresh();
                lastLevel = world.Character.level;
            }
            if (!card) return;
            float dt = Time.unscaledDeltaTime;
            if (countT < 1f)
            {
                countT = Mathf.Min(1f, countT + dt / CountSeconds);
                float e = 1f - Mathf.Pow(1f - countT, 3f);                     // fast start, slow landing
                shown = Mathf.RoundToInt(Mathf.Lerp(from, target, e));
                Render();
            }
            punchT = Mathf.Min(1f, punchT + dt / PunchSeconds);
            flashT = Mathf.Min(1f, flashT + dt / FlashSeconds);
            deltaT = Mathf.Min(1f, deltaT + dt / 1.2f);
            float punch = punchT < 1f ? 1f + 0.24f * Mathf.Sin(punchT * Mathf.PI) * (1f - punchT * 0.4f) : 1f;
            number.rectTransform.localScale = Vector3.one * punch;
            number.color = Color.Lerp(flash, Color.white, Mathf.SmoothStep(0f, 1f, flashT));
            var dc = delta.color; dc.a = deltaT < 1f ? 1f - Mathf.SmoothStep(0.45f, 1f, deltaT) : 0f; delta.color = dc;
            delta.rectTransform.anchoredPosition = new Vector2(170f, -16f + 34f * deltaT);
        }

        private void Render() { if (number) number.text = shown.ToString("N0"); }

        // ------------------------------------------------------------------ UI (flat cel look: ink outline, one flat fill, no gradient)
        private void EnsureUi()
        {
            if (card) return;
            // a scene ROOT object: a canvas nested under another canvas (the bag panel) would not be a screen-space overlay of its own
            var go = new GameObject("CombatPowerCanvas", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 12;
            var scaler = go.AddComponent<CanvasScaler>(); UiStandard.Apply(scaler);
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(go.transform, false);
            card = (RectTransform)cardGo.transform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f); card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(330f, 92f); card.anchoredPosition = new Vector2(0f, -14f);
            var bg = cardGo.GetComponent<Image>(); bg.color = new Color(0.16f, 0.12f, 0.26f, 0.92f); bg.raycastTarget = false;
            var ol = cardGo.AddComponent<Outline>(); ol.effectColor = Ink; ol.effectDistance = new Vector2(3f, -3f);
            var label = NewText(card, "Label", 24, TextAnchor.UpperCenter, Gold);
            label.text = "LỰC CHIẾN";
            label.rectTransform.anchorMin = new Vector2(0f, 1f); label.rectTransform.anchorMax = Vector2.one; label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f); label.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            number = NewText(card, "Number", 58, TextAnchor.MiddleCenter, Color.white);
            number.fontStyle = FontStyle.Bold;
            number.rectTransform.anchorMin = Vector2.zero; number.rectTransform.anchorMax = Vector2.one;
            number.rectTransform.offsetMin = new Vector2(8f, 4f); number.rectTransform.offsetMax = new Vector2(-8f, -30f);
            delta = NewText(card, "Delta", 34, TextAnchor.MiddleLeft, Gain);
            delta.fontStyle = FontStyle.Bold;
            delta.rectTransform.anchorMin = delta.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            delta.rectTransform.sizeDelta = new Vector2(200f, 44f);
            var c = delta.color; c.a = 0f; delta.color = c;
            Render();
        }

        private Text NewText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.alignment = anchor; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var ol = go.AddComponent<Outline>(); ol.effectColor = Ink; ol.effectDistance = new Vector2(2f, -2f);   // hard text outline
            return t;
        }
    }
}
