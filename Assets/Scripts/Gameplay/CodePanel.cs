using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Base for the windows built in code at runtime (event, mail, giftcode, ranking): a dimmed full-screen root with a
    /// framed card, text/button helpers in the game's font and frames, and player input paused while open.
    /// </summary>
    public abstract class CodePanel : MonoBehaviour
    {
        [SerializeField] protected Sprite panelSprite, cellSprite, buttonSprite, coinSprite;

        /// <summary>A row of reward icons (Gold coin first, then items) with quantities.</summary>
        protected sealed class RewardStrip { public Image[] Cells, Icons; public Text[] Qty; }

        protected RewardStrip MakeStrip(Transform p, string name, Vector2 anchor, Vector2 firstPos, int n, float size, float gap)
        {
            var s = new RewardStrip { Cells = new Image[n], Icons = new Image[n], Qty = new Text[n] };
            for (int i = 0; i < n; i++)
            {
                Image cell = Box(p, name + i, anchor, firstPos + new Vector2(i * (size + gap), 0f), new Vector2(size, size), cellSprite, new Color(0.85f, 0.78f, 0.62f, 1f));
                s.Cells[i] = cell;
                s.Icons[i] = Box(cell.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f), null, Color.white);
                s.Icons[i].preserveAspect = true;
                s.Qty[i] = Txt(cell.transform, "Qty", "", Mathf.RoundToInt(size * 0.36f), Color.white, new Vector2(1f, 0f), new Vector2(-size * 0.5f, size * 0.24f), new Vector2(size * 0.92f, size * 0.5f), TextAnchor.LowerRight, true);
                s.Qty[i].resizeTextForBestFit = false;
                s.Qty[i].verticalOverflow = VerticalWrapMode.Overflow; s.Qty[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                var o = s.Qty[i].gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return s;
        }

        private InventoryPanel iconSource;
        protected Sprite ItemIcon(string icon)
        {
            if (!iconSource) iconSource = FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            return iconSource ? iconSource.IconSprite(icon) : null;
        }

        /// <summary>Shows the reward as icons; returns how many cells are used.</summary>
        protected int FillStrip(RewardStrip s, long gold, RewardViewState[] items, bool dim = false)
        {
            int n = 0;
            void Put(Sprite sp, string qty)
            {
                if (n >= s.Cells.Length) return;
                s.Cells[n].gameObject.SetActive(true);
                s.Icons[n].sprite = sp; s.Icons[n].enabled = sp;
                s.Icons[n].color = dim ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                s.Qty[n].text = qty;
                n++;
            }
            if (gold > 0) Put(coinSprite, gold >= 1000 ? (gold / 1000f).ToString("0.#") + "K" : gold.ToString());
            if (items != null) foreach (RewardViewState it in items) Put(ItemIcon(it.icon), "x" + it.qty);
            for (int i = n; i < s.Cells.Length; i++) s.Cells[i].gameObject.SetActive(false);
            return n;
        }
        [SerializeField] protected Font font;

        protected static readonly Color Gold = new Color(1f, 0.85f, 0.45f), Cream = new Color(1f, 0.96f, 0.88f),
            Teal = new Color(0.26f, 0.56f, 0.62f), Green = new Color(0.25f, 0.6f, 0.4f), Grey = new Color(0.42f, 0.43f, 0.5f),
            Slate = new Color(0.3f, 0.36f, 0.55f), Amber = new Color(0.82f, 0.6f, 0.18f), Rose = new Color(0.55f, 0.3f, 0.3f);

        protected GameObject root;
        protected Transform card;
        protected Text msgLabel;
        public bool IsOpen => root && root.activeSelf;
        public bool Busy { get; protected set; }
        public string LastMessage { get; private set; } = "";

        private static Sprite circle;
        /// <summary>Soft-edged round sprite (notification dots).</summary>
        protected static Sprite Circle
        {
            get
            {
                if (circle) return circle;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude;
                        float a = Mathf.Clamp01(n / 2f - 1f - d);                    // anti-aliased edge
                        float rim = Mathf.Clamp01(d - (n / 2f - 7f)) * 0.35f;          // darker rim
                        px[y * n + x] = new Color(1f - rim, 1f - rim, 1f - rim, a);
                    }
                tex.SetPixels(px); tex.Apply();
                return circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>Red notification dot (round) in the top-right corner of a parent.</summary>
        protected GameObject Dot(Transform parent, float size = 24f)
        {
            Image d = Box(parent, "Dot", new Vector2(1f, 1f), new Vector2(-size * 0.55f, -size * 0.5f), new Vector2(size, size), null, new Color(0.92f, 0.18f, 0.2f));
            d.sprite = Circle; d.type = Image.Type.Simple;
            return d.gameObject;
        }

        private static Sprite white;
        protected static Sprite White => white ? white : white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        protected T Make<T>(Transform p, string name, Vector2 anchor, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go.AddComponent<T>();
        }

        protected Text Txt(Transform p, string name, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align, bool bold = false)
        {
            Text t = Make<Text>(p, name, anchor, pos, box);
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.supportRichText = true; t.raycastTarget = false;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(14, size - 8); t.resizeTextMaxSize = size;
            return t;
        }

        protected Image Box(Transform p, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite s, Color c)
        {
            Image img = Make<Image>(p, name, anchor, pos, size);
            img.sprite = s; img.type = s ? Image.Type.Sliced : Image.Type.Simple; img.color = c; img.raycastTarget = false;
            return img;
        }

        protected Button Btn(Transform p, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color tint, Action click, int fontSize = 22)
        {
            Image img = Make<Image>(p, name, anchor, pos, size);
            img.sprite = buttonSprite; img.type = buttonSprite ? Image.Type.Sliced : Image.Type.Simple; img.color = tint;
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(() => click());
            Txt(img.transform, "Label", label, fontSize, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 6f), TextAnchor.MiddleCenter, true);
            return b;
        }

        /// <summary>Text input on a light field (mobile keyboard opens on tap).</summary>
        protected InputField Field(Transform p, string name, Vector2 anchor, Vector2 pos, Vector2 size, string placeholder, int limit, int fontSize = 24)
        {
            Image bg = Make<Image>(p, name, anchor, pos, size);
            bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; bg.color = new Color(0.95f, 0.92f, 0.84f, 1f);
            Text ph = Txt(bg.transform, "Placeholder", placeholder, fontSize, new Color(0.45f, 0.4f, 0.35f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 8f), TextAnchor.MiddleLeft);
            ph.fontStyle = FontStyle.Italic;
            Text tx = Txt(bg.transform, "Text", "", fontSize, new Color(0.2f, 0.13f, 0.07f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 8f), TextAnchor.MiddleLeft);
            tx.resizeTextForBestFit = false; tx.supportRichText = false;
            var f = bg.gameObject.AddComponent<InputField>();
            f.targetGraphic = bg; f.textComponent = tx; f.placeholder = ph; f.characterLimit = limit;
            return f;
        }

        protected static void SetBtn(Button b, string label, Color tint, bool interactable)
        {
            b.interactable = interactable;
            b.GetComponent<Image>().color = tint;
            Text t = b.GetComponentInChildren<Text>();
            if (t) t.text = label;
        }

        /// <summary>Dimmed root + framed card + title + close button + bottom message line.</summary>
        protected void BuildShell(string title, Vector2 cardSize)
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image dim = Make<Image>(transform, "Root", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root = dim.gameObject;
            var drt = (RectTransform)root.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(0f, 0f, 0f, 0.5f);
            Image c = Make<Image>(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);
            c.sprite = panelSprite; c.type = panelSprite ? Image.Type.Sliced : Image.Type.Simple; c.color = panelSprite ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.97f);
            card = c.transform;
            Txt(card, "Title", title, 40, Gold, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(700f, 56f), TextAnchor.MiddleCenter, true);
            Btn(card, "Close", "Đóng", new Vector2(1f, 1f), new Vector2(-130f, -64f), new Vector2(150f, 62f), Rose, Close);
            msgLabel = Txt(card, "Message", "", 22, new Color(1f, 0.78f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(cardSize.x - 160f, 34f), TextAnchor.MiddleCenter);
            root.SetActive(false);
        }

        protected abstract void Build();

        public virtual void Open()
        {
            if (!root) Build();
            root.SetActive(true);
            transform.SetAsLastSibling();
            SetInput(false);
            Message("");
        }

        public virtual void Close() { if (root) root.SetActive(false); SetInput(true); }

        protected void Message(string m) { LastMessage = m ?? ""; if (msgLabel) msgLabel.text = LastMessage; }

        protected static void SetInput(bool on)
        {
            var w = AdventureWorldSession.Current;
            var pi = w && w.Player ? w.Player.GetComponent<PlayerInput2D>() : null;
            if (pi) pi.enabled = on;
        }

        protected static string CharacterId
        {
            get { var w = AdventureWorldSession.Current; return w && w.Character != null ? w.Character.characterId : null; }
        }

        protected static void ShowGold(long gold)
        {
            var w = AdventureWorldSession.Current;
            if (w && w.Character != null) w.Character.gold = gold; // HUD shows the server-confirmed balance
        }

        protected static AdventureServerConnection Server => AdventureServerConnection.Instance;
    }
}
