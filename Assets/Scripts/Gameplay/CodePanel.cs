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
        [SerializeField] protected Sprite panelSprite, cellSprite, buttonSprite;
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
