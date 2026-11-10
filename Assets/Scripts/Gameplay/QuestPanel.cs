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
    /// Quest tracker on the left of the HUD: Chính / Phụ / Hằng ngày tabs, real server progress per objective, rewards,
    /// "Nhận thưởng" for a finished quest (server pays once), "Chỉ đường" (where to go, which NPC) and a fold arrow.
    /// Level + EXP bar from the server. Refreshes every few seconds and after every quest event.
    /// </summary>
    public sealed class QuestPanel : MonoBehaviour
    {
        [SerializeField] private Sprite panelSprite, buttonSprite, arrowSprite;
        [SerializeField] private Font font;

        public enum Tab { Main, Side, Daily }
        public Tab Current { get; private set; } = Tab.Main;
        public bool Collapsed { get; private set; }
        public string BodyText => body ? body.text : "";
        public string HintText => hint ? hint.text : "";
        public bool ClaimVisible => claimButton && claimButton.gameObject.activeSelf;
        public string LastMessage { get; private set; } = "";

        private RectTransform card, bodyRoot;
        private Text title, level, body, hint;
        private Image expFill;
        private Button claimButton, guideButton;
        private readonly Button[] tabs = new Button[3];
        private float nextRefresh;
        private const string FoldKey = "ak_quest_folded";

        private void Start()
        {
            Build();
            QuestBook.Changed += Redraw;
            try { if (PlayerPrefs.GetInt(FoldKey, 0) == 1) SetCollapsed(true); } catch { }
        }

        private void OnDestroy() => QuestBook.Changed -= Redraw;

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh && AdventureWorldSession.Current && AdventureWorldSession.Current.Character != null)
            {
                nextRefresh = Time.unscaledTime + 4f;
                QuestBook.Refresh();
            }
        }

        private T Make<T>(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<T>();
        }

        private Text Txt(Transform parent, string name, string text, int size, Color c, Vector2 pos, Vector2 box, TextAnchor align, bool bold = false)
        {
            Text t = Make<Text>(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, box);
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.supportRichText = true;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.raycastTarget = false;
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.7f); o.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        private Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color tint, Action click)
        {
            Image img = Make<Image>(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            img.sprite = buttonSprite; img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple; img.color = tint;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => click());
            Text t = Txt(img.transform, "Label", label, 22, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter, true);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return b;
        }

        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(64f, -262f); // clear of a phone notch / rounded corner
            rt.sizeDelta = new Vector2(460f, 420f);
            card = (RectTransform)transform;
            Image bg = gameObject.AddComponent<Image>();
            bg.sprite = null; bg.type = Image.Type.Simple; // plain plate (that sprite has no 9-slice border and stretched into blobs)
            var edge = gameObject.AddComponent<Outline>(); edge.effectColor = new Color(1f, 0.82f, 0.4f, 0.75f); edge.effectDistance = new Vector2(2f, -2f);
            bg.color = new Color(0.06f, 0.08f, 0.16f, 0.86f); // (the panel sprite is a white shape meant to be tinted)
            bg.raycastTarget = true;
            Color gold = new Color(1f, 0.85f, 0.45f), cream = new Color(1f, 0.96f, 0.88f);

            title = Txt(card, "Title", "Nhiệm vụ", 26, gold, new Vector2(24f, -16f), new Vector2(200f, 40f), TextAnchor.MiddleLeft, true);
            level = Txt(card, "Level", "", 18, cream, new Vector2(200f, -16f), new Vector2(170f, 40f), TextAnchor.MiddleRight);
            Image fold = Make<Image>(card, "Fold", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(421f, -37f), new Vector2(70f, 70f)); // centre pivot: it rotates in place
            fold.sprite = arrowSprite; fold.preserveAspect = true;
            var fb = fold.gameObject.AddComponent<Button>(); fb.targetGraphic = fold; fb.onClick.AddListener(() => SetCollapsed(!Collapsed));
            fold.transform.localEulerAngles = new Vector3(0f, 0f, 90f); // chevron points up = fold away

            var bodyGo = new GameObject("Body", typeof(RectTransform)) { layer = 5 }; // (AddComponent<RectTransform> on a UI object returns null)
            bodyGo.transform.SetParent(card, false);
            bodyRoot = (RectTransform)bodyGo.transform;
            bodyRoot.anchorMin = bodyRoot.anchorMax = bodyRoot.pivot = new Vector2(0f, 1f);
            bodyRoot.anchoredPosition = Vector2.zero;
            bodyRoot.sizeDelta = new Vector2(460f, 420f);
            Image track = Make<Image>(bodyRoot, "ExpTrack", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -62f), new Vector2(412f, 10f));
            track.color = new Color(0f, 0f, 0f, 0.45f); track.raycastTarget = false;
            expFill = Make<Image>(track.transform, "ExpFill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(412f, 10f));
            expFill.color = new Color(0.55f, 0.95f, 0.5f); expFill.raycastTarget = false;
            expFill.type = Image.Type.Filled; expFill.fillMethod = Image.FillMethod.Horizontal; expFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            string[] names = { "Chính", "Phụ", "Hằng ngày" };
            for (int i = 0; i < 3; i++)
            {
                Tab tab = (Tab)i;
                tabs[i] = Btn(bodyRoot, "Tab" + i, names[i], new Vector2(24f + i * 140f, -82f), new Vector2(132f, 62f), new Color(0.3f, 0.36f, 0.55f), () => Show(tab));
            }
            body = Txt(bodyRoot, "QuestBody", "Đang tải nhiệm vụ...", 19, cream, new Vector2(24f, -154f), new Vector2(412f, 160f), TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true; body.resizeTextMinSize = 14; body.resizeTextMaxSize = 19;
            claimButton = Btn(bodyRoot, "Claim", "Nhận thưởng", new Vector2(24f, -322f), new Vector2(200f, 62f), new Color(0.82f, 0.6f, 0.18f), ClaimShown);
            guideButton = Btn(bodyRoot, "Guide", "Chỉ đường", new Vector2(236f, -322f), new Vector2(200f, 62f), new Color(0.26f, 0.56f, 0.62f), Guide);
            hint = Txt(card, "Hint", "", 17, new Color(0.75f, 0.92f, 1f), new Vector2(24f, -394f), new Vector2(412f, 28f), TextAnchor.MiddleLeft);
            Redraw();
        }

        public void SetCollapsed(bool c)
        {
            Collapsed = c;
            if (bodyRoot) bodyRoot.gameObject.SetActive(!c);
            if (hint) hint.gameObject.SetActive(!c);
            if (card) card.sizeDelta = new Vector2(460f, c ? 72f : 420f);
            var fold = card ? card.Find("Fold") : null;
            if (fold) fold.localEulerAngles = new Vector3(0f, 0f, c ? -90f : 90f);
            try { PlayerPrefs.SetInt(FoldKey, c ? 1 : 0); } catch { }
        }

        public void Show(Tab t) { Current = t; Redraw(); }

        private QuestState[] Shown()
        {
            QuestLog log = QuestBook.Log;
            if (log == null) return new QuestState[0];
            return Current == Tab.Main ? log.main : Current == Tab.Side ? log.side : log.daily;
        }

        private QuestState Claimable => Shown().FirstOrDefault(q => q.complete && !q.claimed);

        private void Redraw()
        {
            if (!body) return;
            QuestLog log = QuestBook.Log;
            for (int i = 0; i < 3; i++)
                if (tabs[i]) tabs[i].GetComponent<Image>().color = (int)Current == i ? new Color(0.82f, 0.6f, 0.18f) : new Color(0.3f, 0.36f, 0.55f);
            if (log == null) { body.text = "Đang tải nhiệm vụ..."; claimButton.gameObject.SetActive(false); return; }
            long span = Math.Max(1, log.expNextLevel - log.expLevelStart);
            level.text = $"Cấp {log.level} · {log.exp - log.expLevelStart}/{span} EXP";
            expFill.fillAmount = Mathf.Clamp01((log.exp - log.expLevelStart) / (float)span);
            QuestState[] list = Shown();
            var sb = new System.Text.StringBuilder();
            if (list.Length == 0) sb.Append(Current == Tab.Main ? "Đã hoàn thành toàn bộ chuỗi nhiệm vụ chính hiện có." : "Không còn nhiệm vụ.");
            foreach (QuestState q in list.Take(Current == Tab.Main ? 1 : 3))
            {
                string head = q.type == "main" ? $"[{q.step}/{log.mainTotal}] " : "";
                string state = q.claimed ? " <color=#9aa>(đã nhận)</color>" : q.complete ? " <color=#ffd54a>(xong)</color>" : "";
                sb.Append($"<b><color=#ffd98a>{head}{q.title}</color></b>{state}\n");
                foreach (QuestObjectiveState o in q.objectives)
                    sb.Append($"  • {o.text}  <b>{o.have}/{o.need}</b>\n");
                var rewards = new List<string>();
                if (q.rewardExp > 0) rewards.Add(q.rewardExp + " EXP");
                if (q.rewardGold > 0) rewards.Add(q.rewardGold + " Vàng");
                if (!string.IsNullOrEmpty(q.rewardItems)) rewards.Add(q.rewardItems);
                if (Current == Tab.Main) sb.Append($"<color=#bfe3ff>{q.desc}</color>\n");
                sb.Append($"<color=#a8f0a0>Thưởng: {string.Join(", ", rewards)}</color>\n");
            }
            if (Current == Tab.Daily && !string.IsNullOrEmpty(log.dailyDay)) sb.Append($"<color=#9aa>Làm mới lúc 00:00 (giờ Việt Nam) · {log.dailyDay}</color>");
            body.text = sb.ToString().TrimEnd();
            claimButton.gameObject.SetActive(Claimable != null);
            guideButton.gameObject.SetActive(list.Any(q => !q.claimed));
        }

        public void ClaimShown()
        {
            QuestState q = Claimable;
            if (q == null) return;
            claimButton.interactable = false;
            QuestBook.Claim(q.id, (ok, r, err) =>
            {
                claimButton.interactable = true;
                LastMessage = ok ? $"+{r.exp} EXP" + (r.gold > 0 ? $", +{r.gold} Vàng" : "") + (r.leveledUp ? $" · Lên cấp {r.level}!" : "") : err;
                if (hint) hint.text = LastMessage;
                if (ok) { var bag = FindAnyObjectByType<InventoryPanel>(); if (bag) bag.Refresh(); }
            });
        }

        /// <summary>Where to go for the shown quest (map + NPC), relative to where the hero is now.</summary>
        public void Guide()
        {
            QuestState q = Shown().FirstOrDefault(x => !x.claimed);
            if (q == null || !hint) return;
            var world = AdventureWorldSession.Current;
            string here = world && world.Character != null ? world.Character.lastMapId : "";
            string where = q.mapId == here ? "ngay trong bản đồ này" :
                q.mapId == TownLayout.Meadow.MapId ? "Đồng Cỏ Gió — qua cổng rune phía đông làng (hoặc dùng vé)" :
                q.mapId == TownLayout.Ch1Village.MapId ? "Làng Nấm — qua cổng pha lê ở cuối phía đông Đồng Cỏ Gió" : "Thị trấn Chuông Gió — qua cổng phía tây Đồng Cỏ";
            hint.text = string.IsNullOrEmpty(q.npc) ? $"Đi tới: {where}" : $"Gặp {q.npc}: {where}";
            LastMessage = hint.text;
        }
    }
}
