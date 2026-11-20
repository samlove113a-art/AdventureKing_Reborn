using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Hộp Thư": the mailbox from the server (system / reward / notice; guild mail once guilds exist). The list on the
    /// left (newest first, unread bold with a dot), the open letter on the right with its attachments and a Nhận button
    /// that the server pays once. Opening a letter marks it read on the server. The HUD dot shows only for real unread
    /// or unclaimed mail.
    /// </summary>
    public sealed class MailPanel : CodePanel
    {
        [SerializeField] private GameObject dot;

        public MailboxState State { get; private set; }
        public string SelectedId { get; private set; }
        public bool DotShown => dot && dot.activeSelf;

        private const int ListCount = 6;
        private sealed class Item { public Button Btn; public Text Title, Sub; public GameObject Unread; }
        private readonly Item[] items = new Item[ListCount];
        private Text letterTitle, letterMeta, letterBody, attach, empty;
        private Button claim;
        private RewardStrip attachIcons;
        private float nextPoll;

        private void Start()
        {
            if (dot) dot.SetActive(false);
            nextPoll = Time.unscaledTime + 2f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPoll || Busy || CharacterId == null || !Server) return;
            nextPoll = Time.unscaledTime + 120f;
            Refresh();
        }

        protected override void Build()
        {
            BuildShell("Hộp Thư", new Vector2(1500f, 860f));
            for (int i = 0; i < ListCount; i++)
            {
                int k = i;
                Image bg = Make<Image>(card, "Mail" + i, new Vector2(0f, 1f), new Vector2(330f, -210f - i * 100f), new Vector2(480f, 90f));
                bg.sprite = cellSprite; bg.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; bg.color = new Color(0.55f, 0.6f, 0.78f, 0.95f);
                var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.onClick.AddListener(() => Select(k));
                var it = new Item { Btn = b };
                it.Title = Txt(bg.transform, "Title", "", 22, Gold, new Vector2(0f, 0.5f), new Vector2(240f, 16f), new Vector2(420f, 32f), TextAnchor.MiddleLeft, true);
                it.Sub = Txt(bg.transform, "Sub", "", 17, Cream, new Vector2(0f, 0.5f), new Vector2(240f, -20f), new Vector2(420f, 28f), TextAnchor.MiddleLeft);
                it.Unread = Dot(bg.transform, 22f);
                items[i] = it;
            }
            empty = Txt(card, "Empty", "", 22, Cream, new Vector2(0f, 1f), new Vector2(330f, -300f), new Vector2(460f, 80f), TextAnchor.MiddleCenter);
            // letter on the same dark cell frame as the rest of the UI: light text (dark ink on it was unreadable)
            Box(card, "Letter", new Vector2(0f, 1f), new Vector2(1010f, -460f), new Vector2(820f, 600f), cellSprite, new Color(0.55f, 0.6f, 0.78f, 0.95f));
            letterTitle = Txt(card, "LetterTitle", "", 30, Gold, new Vector2(0f, 1f), new Vector2(1010f, -205f), new Vector2(760f, 44f), TextAnchor.MiddleLeft, true);
            letterMeta = Txt(card, "LetterMeta", "", 18, new Color(0.78f, 0.82f, 0.92f), new Vector2(0f, 1f), new Vector2(1010f, -245f), new Vector2(760f, 28f), TextAnchor.MiddleLeft);
            letterBody = Txt(card, "LetterBody", "", 23, Cream, new Vector2(0f, 1f), new Vector2(1010f, -420f), new Vector2(760f, 300f), TextAnchor.UpperLeft);
            letterBody.resizeTextForBestFit = false; letterBody.verticalOverflow = VerticalWrapMode.Truncate; letterBody.lineSpacing = 1.15f;
            attach = Txt(card, "Attach", "", 22, new Color(1f, 0.8f, 0.45f), new Vector2(0f, 1f), new Vector2(1010f, -600f), new Vector2(760f, 34f), TextAnchor.MiddleLeft, true);
            attachIcons = MakeStrip(card, "AttachIcon", new Vector2(0f, 1f), new Vector2(650f, -668f), 5, 64f, 8f); // 618..970
            claim = Btn(card, "Claim", "Nhận", new Vector2(0f, 1f), new Vector2(1250f, -668f), new Vector2(240f, 68f), Green, Claim);
        }

        public override void Open()
        {
            base.Open();
            Redraw();
            Refresh();
        }

        public void Refresh()
        {
            string id = CharacterId;
            if (id == null || !Server) return;
            Server.GetMail(id, (ok, s, err) =>
            {
                if (!ok || s == null) { if (IsOpen) Message(err ?? "Không tải được hộp thư."); if (dot) dot.SetActive(false); return; }
                Apply(s);
                if (IsOpen && SelectedId == null && s.mails.Length > 0) Select(0);
            });
        }

        private void Apply(MailboxState s)
        {
            State = s;
            if (dot) dot.SetActive(s.unread > 0 || s.unclaimed > 0);
            Redraw();
        }

        private MailState Selected => State?.mails?.FirstOrDefault(m => m.id == SelectedId);

        public void Select(int i)
        {
            if (State == null || i >= State.mails.Length) return;
            MailState m = State.mails[i];
            SelectedId = m.id;
            Redraw();
            if (!m.read && CharacterId != null && Server)
                Server.ReadMail(CharacterId, m.id, (ok, s, err) => { if (ok && s != null) Apply(s); });
        }

        private static string Kind(string k) => k switch { "system" => "Hệ thống", "reward" => "Phần thưởng", "notice" => "Thông báo", "guild" => "Bang hội", _ => k };

        private void Redraw()
        {
            if (!root) return;
            MailState[] list = State?.mails ?? new MailState[0];
            for (int i = 0; i < ListCount; i++)
            {
                bool on = i < list.Length;
                items[i].Btn.gameObject.SetActive(on);
                if (!on) continue;
                MailState m = list[i];
                items[i].Title.text = m.title;
                items[i].Title.fontStyle = m.read ? FontStyle.Normal : FontStyle.Bold;
                items[i].Sub.text = Kind(m.kind) + (m.hasAttachment ? (m.claimed ? " · đã nhận quà" : " · <color=#ffd27a>có quà</color>") : "");
                items[i].Unread.SetActive(!m.read || (m.hasAttachment && !m.claimed));
                items[i].Btn.GetComponent<Image>().color = m.id == SelectedId ? new Color(0.85f, 0.72f, 0.45f, 0.98f) : new Color(0.55f, 0.6f, 0.78f, 0.95f);
            }
            empty.text = State == null ? "Đang tải từ máy chủ..." : list.Length == 0 ? "Hộp thư trống." : "";
            MailState sel = Selected;
            letterTitle.text = sel?.title ?? "";
            letterMeta.text = sel == null ? "" : Kind(sel.kind) + " · " + DateTimeOffset.FromUnixTimeMilliseconds(sel.createdMs).ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
            letterBody.text = sel?.body ?? (list.Length > 0 ? "Chọn một thư để đọc." : "");
            attach.text = sel != null && sel.hasAttachment ? "Đính kèm: " + sel.attachText : "";
            FillStrip(attachIcons, sel != null && sel.hasAttachment ? sel.attachGold : 0, sel != null && sel.hasAttachment ? sel.items : null, sel != null && sel.claimed);
            claim.gameObject.SetActive(sel != null && sel.hasAttachment);
            if (sel != null && sel.hasAttachment) SetBtn(claim, sel.claimed ? "Đã nhận" : "Nhận", sel.claimed ? Grey : Green, !sel.claimed && !Busy);
        }

        public void Claim()
        {
            MailState sel = Selected;
            if (Busy || sel == null || !sel.hasAttachment || sel.claimed || CharacterId == null || !Server) return;
            Busy = true;
            Redraw();
            string text = sel.attachText;
            Server.ClaimMail(CharacterId, sel.id, (ok, s, err) =>
            {
                Busy = false;
                if (ok && s != null) { ShowGold(s.gold); Message("Đã nhận: " + text); Apply(s); }
                else { Message(err ?? "Không nhận được vật phẩm."); Redraw(); }
            });
        }
    }
}
