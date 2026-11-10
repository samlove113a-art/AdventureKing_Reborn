using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Xã Hội" window — everything is a server request, the window only shows the server's answer:
    /// <b>Chat</b> (Thế Giới / Bang Hội / Riêng Tư), <b>Bạn Bè</b> (add, accept, remove, block, whisper, propose),
    /// <b>Bang Hội</b> (create with emblem, browse + apply, invites, members + roles, applications, guild quests,
    /// contributions — closed while the owner has not approved the rates —, notice, leave / disband) and
    /// <b>Hôn Nhân</b> (proposals, engagement, the Wedding Hall ceremony start / confirm, divorce).
    /// Also owns the HUD chat ticker and the red dot for pending requests.
    /// </summary>
    public sealed class SocialPanel : CodePanel
    {
        [SerializeField] private GameObject dot;
        public static SocialPanel Instance { get; private set; }

        public string Tab { get; private set; } = "chat";
        public FriendsState Friends { get; private set; }
        public GuildViewState GuildView { get; private set; }
        public MarriageState Marriage { get; private set; }
        public readonly List<ChatMessageState> ChatLog = new List<ChatMessageState>();
        public string SendChannel { get; private set; } = "world";
        public bool DotShown => dot && dot.activeSelf;
        public string TickerText => ticker ? ticker.text : "";
        public string ChatText => chatLog ? chatLog.text : "";

        private readonly Dictionary<string, GameObject> pages = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Button> tabButtons = new Dictionary<string, Button>();
        private Text chatLog, ticker, guildHeader, guildNotice, marriageStatus, feeText, pageText;
        private InputField chatInput, chatTo, friendName, guildName, inviteName, noticeInput;
        private readonly Button[] channelButtons = new Button[3];
        private readonly List<Button> emblemButtons = new List<Button>();
        private string emblem = "bell";
        private string guildSub = "members";
        private readonly Dictionary<string, Button> guildSubButtons = new Dictionary<string, Button>();
        private GameObject guildNone, guildIn, officerTools;
        private Button leaveBtn, disbandBtn, startCeremonyBtn, confirmCeremonyBtn, divorceBtn;
        private readonly List<Button> contributeButtons = new List<Button>();
        private float tickerUntil;
        private int friendPage, guildPage;
        private Button guildPrev, guildNext;
        private string promptedCeremony = "";

        private sealed class Row { public GameObject Go; public Text Main, Sub; public Button[] Btn = new Button[4]; public Action[] Act = new Action[4]; }
        private readonly Dictionary<string, List<Row>> rows = new Dictionary<string, List<Row>>();

        private static string ClassName(int c) => c switch { 0 => "Kiếm Sĩ", 1 => "Pháp Sư", 2 => "Sát Thủ", 3 => "Cung Thủ", _ => "" };
        private static string RoleName(string r) => r == "leader" ? "Bang Chủ" : r == "vice" ? "Phó Bang" : "Thành viên";
        private static readonly Dictionary<string, string> EmblemNames = new Dictionary<string, string>
            { ["bell"] = "Chuông", ["sword"] = "Kiếm", ["lotus"] = "Sen", ["dragon"] = "Rồng", ["phoenix"] = "Phượng", ["crown"] = "Vương" };
        private static string EmblemName(string e) => e != null && EmblemNames.TryGetValue(e, out string n) ? n : e;
        private string Me => CharacterId;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (dot) dot.SetActive(false);
            BuildTicker();
            if (WorldSync.Current)
            {
                WorldSync.Current.ChatArrived += OnChat;
                WorldSync.Current.Synced += OnSynced;
            }
        }

        // ---------------------------------------------------------------- HUD ticker (last 2 lines)
        private void BuildTicker()
        {
            Image bg = Box(transform, "ChatTicker", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(900f, 72f), null, new Color(0f, 0f, 0f, 0.35f));
            ticker = Txt(bg.transform, "Text", "", 21, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 68f), TextAnchor.MiddleLeft);
            ticker.resizeTextForBestFit = false; ticker.verticalOverflow = VerticalWrapMode.Truncate;
            bg.gameObject.SetActive(false);
        }

        private static string Line(ChatMessageState m)
        {
            string ch = m.channel == "guild" ? "<color=#9fe39f>[Bang]</color>" : m.channel == "private" ? "<color=#ffb3e0>[Riêng]</color>" : "<color=#ffd27a>[Thế giới]</color>";
            string who = m.channel == "private" && m.senderId == AdventureWorldSession.Current?.Character?.characterId ? "→ " + m.toName : m.sender;
            return $"{ch} <b>{who}</b>: {m.text}";
        }

        private void OnChat(ChatMessageState[] msgs)
        {
            foreach (var m in msgs) if (!ChatLog.Any(x => x.id == m.id)) ChatLog.Add(m);
            if (ChatLog.Count > 200) ChatLog.RemoveRange(0, ChatLog.Count - 200);
            if (ticker)
            {
                ticker.text = string.Join("\n", ChatLog.Skip(Math.Max(0, ChatLog.Count - 2)).Select(Line));
                ticker.transform.parent.gameObject.SetActive(!IsOpen && ChatLog.Count > 0);
                tickerUntil = Time.time + 9f;
            }
            if (IsOpen && Tab == "chat") RedrawChat();
        }

        private void Update()
        {
            if (ticker && ticker.transform.parent.gameObject.activeSelf && Time.time > tickerUntil) ticker.transform.parent.gameObject.SetActive(false);
        }

        private void OnSynced(SyncState s)
        {
            if (dot) dot.SetActive(s.badges != null && s.badges.Total > 0);
            CeremonyState c = s.ceremony;
            // the partner started the ceremony: ask this player to confirm
            if (c != null && c.Valid && c.state == "invited" && c.invitedBy != Me && (c.aId == Me || c.bId == Me) && promptedCeremony != c.id)
            {
                promptedCeremony = c.id;
                OpenTab("marriage");
                Message((c.aId == Me ? c.bName : c.aName) + " mời bạn làm lễ cưới. Bấm “Xác nhận làm lễ”.");
            }
        }

        // ---------------------------------------------------------------- build
        protected override void Build()
        {
            BuildShell("Xã Hội", new Vector2(1500f, 900f));
            string[] ids = { "chat", "friends", "guild", "marriage" };
            string[] names = { "Chat", "Bạn Bè", "Bang Hội", "Hôn Nhân" };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                tabButtons[id] = Btn(card, "Tab_" + id, names[i], new Vector2(0.5f, 1f), new Vector2(-480f + i * 320f, -160f), new Vector2(300f, 62f), Slate, () => OpenTab(id), 24);
                var page = new GameObject("Page_" + id, typeof(RectTransform));
                page.transform.SetParent(card, false);
                var rt = (RectTransform)page.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                pages[id] = page;
            }
            BuildChat(pages["chat"].transform);
            BuildFriends(pages["friends"].transform);
            BuildGuild(pages["guild"].transform);
            BuildMarriage(pages["marriage"].transform);
        }

        private List<Row> Rows(Transform p, string key, float top, int count, float width = 1320f)
        {
            var list = new List<Row>();
            for (int i = 0; i < count; i++)
            {
                var r = new Row();
                Image bg = Box(p, key + "Row" + i, new Vector2(0.5f, 1f), new Vector2(0f, top - i * 66f), new Vector2(width, 64f), cellSprite, new Color(0.55f, 0.6f, 0.78f, 0.95f));
                r.Go = bg.gameObject;
                r.Main = Txt(bg.transform, "Main", "", 23, Gold, new Vector2(0f, 0.5f), new Vector2(260f, 10f), new Vector2(500f, 32f), TextAnchor.MiddleLeft, true);
                r.Sub = Txt(bg.transform, "Sub", "", 17, Cream, new Vector2(0f, 0.5f), new Vector2(260f, -15f), new Vector2(500f, 26f), TextAnchor.MiddleLeft);
                for (int b = 0; b < 4; b++)
                {
                    int bi = b; Row rr = r;
                    r.Btn[b] = Btn(bg.transform, "B" + b, "", new Vector2(1f, 0.5f), new Vector2(-95f - b * 172f, 0f), new Vector2(162f, 62f), Teal, () => rr.Act[bi]?.Invoke(), 20);
                }
                list.Add(r);
            }
            rows[key] = list;
            return list;
        }

        private static void SetRow(Row r, string main, string sub, params (string label, Color c, Action a)[] buttons)
        {
            r.Go.SetActive(true);
            r.Main.text = main; r.Sub.text = sub;
            for (int b = 0; b < 4; b++)
            {
                bool on = b < buttons.Length;
                r.Btn[b].gameObject.SetActive(on);
                r.Act[b] = on ? buttons[b].a : null;
                if (on) SetBtn(r.Btn[b], buttons[b].label, buttons[b].c, true);
            }
        }

        private void HideRows(string key, int from) { for (int i = from; i < rows[key].Count; i++) rows[key][i].Go.SetActive(false); }

        private void BuildChat(Transform p)
        {
            string[] ch = { "world", "guild", "private" };
            string[] nm = { "Thế Giới", "Bang Hội", "Riêng Tư" };
            for (int i = 0; i < 3; i++)
            {
                string c = ch[i];
                channelButtons[i] = Btn(p, "Ch_" + c, nm[i], new Vector2(0f, 1f), new Vector2(180f + i * 240f, -238f), new Vector2(220f, 64f), Slate, () => { SendChannel = c; RedrawChat(); }, 22);
            }
            Image logBg = Box(p, "Log", new Vector2(0.5f, 1f), new Vector2(0f, -500f), new Vector2(1320f, 440f), null, new Color(0f, 0f, 0f, 0.35f));
            chatLog = Txt(logBg.transform, "Text", "", 22, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1290f, 420f), TextAnchor.LowerLeft);
            chatLog.resizeTextForBestFit = false; chatLog.verticalOverflow = VerticalWrapMode.Truncate;
            chatTo = Field(p, "To", new Vector2(0f, 1f), new Vector2(240f, -770f), new Vector2(300f, 64f), "Gửi cho (tên)", 16, 22);
            chatInput = Field(p, "Input", new Vector2(0.5f, 1f), new Vector2(40f, -770f), new Vector2(740f, 64f), "Nhập tin nhắn…", 120, 22);
            Btn(p, "Send", "Gửi", new Vector2(1f, 1f), new Vector2(-170f, -770f), new Vector2(200f, 64f), Green, SendChat, 24);
            chatInput.onEndEdit.AddListener(_ =>
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SendChat();
            });
        }

        private void BuildFriends(Transform p)
        {
            friendName = Field(p, "Name", new Vector2(0f, 1f), new Vector2(330f, -238f), new Vector2(480f, 64f), "Tên người chơi", 16, 22);
            Btn(p, "Add", "Kết bạn", new Vector2(0f, 1f), new Vector2(700f, -238f), new Vector2(220f, 64f), Green, () => Do(n => n.FriendRequest(Me, friendName.text.Trim(), FriendsDone("Đã gửi lời mời kết bạn."))), 22);
            Btn(p, "Block", "Chặn", new Vector2(0f, 1f), new Vector2(930f, -238f), new Vector2(200f, 64f), Rose, () => Do(n => n.Block(Me, friendName.text.Trim(), FriendsDone("Đã chặn."))), 22);
            Rows(p, "friends", -320f, 7);
            pageText = Txt(p, "Page", "", 20, Cream, new Vector2(0.5f, 1f), new Vector2(0f, -800f), new Vector2(300f, 40f), TextAnchor.MiddleCenter);
            Btn(p, "Prev", "◀", new Vector2(0.5f, 1f), new Vector2(-220f, -800f), new Vector2(90f, 64f), Slate, () => { friendPage = Math.Max(0, friendPage - 1); RedrawFriends(); });
            Btn(p, "Next", "▶", new Vector2(0.5f, 1f), new Vector2(220f, -800f), new Vector2(90f, 64f), Slate, () => { friendPage++; RedrawFriends(); });
        }

        private void BuildGuild(Transform p)
        {
            // ---- not in a guild: create + invites + browse
            guildNone = new GameObject("None", typeof(RectTransform));
            guildNone.transform.SetParent(p, false);
            var nrt = (RectTransform)guildNone.transform; nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one; nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            Transform n = guildNone.transform;
            guildName = Field(n, "GuildName", new Vector2(0f, 1f), new Vector2(300f, -238f), new Vector2(420f, 64f), "Tên bang hội", 16, 22);
            for (int i = 0; i < 6; i++)
            {
                int k = i;
                emblemButtons.Add(Btn(n, "Emblem" + i, "", new Vector2(0f, 1f), new Vector2(580f + i * 112f, -238f), new Vector2(104f, 64f), Slate, () => { emblem = GuildView?.emblems.ElementAtOrDefault(k) ?? emblem; RedrawGuild(); }, 20));
            }
            Btn(n, "Create", "Lập bang", new Vector2(1f, 1f), new Vector2(-140f, -238f), new Vector2(220f, 64f), Green, () => Do(x => x.GuildCreate(Me, guildName.text.Trim(), emblem, GuildDone("Đã lập bang hội!"))), 22);
            feeText = Txt(n, "Fee", "", 19, Cream, new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1300f, 30f), TextAnchor.MiddleCenter);
            Rows(n, "guildList", -345f, 7);
            // ---- in a guild
            guildIn = new GameObject("In", typeof(RectTransform));
            guildIn.transform.SetParent(p, false);
            var irt = (RectTransform)guildIn.transform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
            Transform g = guildIn.transform;
            guildHeader = Txt(g, "Header", "", 24, Gold, new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(1320f, 40f), TextAnchor.MiddleLeft, true);
            guildNotice = Txt(g, "Notice", "", 20, Cream, new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(1320f, 32f), TextAnchor.MiddleLeft);
            string[] subs = { "members", "applications", "quests", "fund" };
            string[] subNames = { "Thành viên", "Đơn xin vào", "Nhiệm vụ bang", "Quỹ & Mời" };
            for (int i = 0; i < subs.Length; i++)
            {
                string s = subs[i];
                guildSubButtons[s] = Btn(g, "Sub_" + s, subNames[i], new Vector2(0f, 1f), new Vector2(190f + i * 250f, -320f), new Vector2(236f, 64f), Slate, () => { guildSub = s; guildPage = 0; RedrawGuild(); }, 20);
            }
            Rows(g, "guildRows", -388f, 6);
            guildPrev = Btn(g, "GuildPrev", "◀", new Vector2(1f, 1f), new Vector2(-260f, -320f), new Vector2(90f, 64f), Slate, () => { guildPage = Math.Max(0, guildPage - 1); RedrawGuild(); });
            guildNext = Btn(g, "GuildNext", "▶", new Vector2(1f, 1f), new Vector2(-150f, -320f), new Vector2(90f, 64f), Slate, () => { guildPage++; RedrawGuild(); });
            officerTools = new GameObject("Officer", typeof(RectTransform));
            officerTools.transform.SetParent(g, false);
            var ort = (RectTransform)officerTools.transform; ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = ort.offsetMax = Vector2.zero;
            inviteName = Field(officerTools.transform, "Invite", new Vector2(0f, 1f), new Vector2(300f, -790f), new Vector2(400f, 64f), "Mời người chơi (tên)", 16, 20);
            Btn(officerTools.transform, "InviteBtn", "Mời vào bang", new Vector2(0f, 1f), new Vector2(620f, -790f), new Vector2(220f, 64f), Teal, () => Do(x => x.GuildInvite(Me, inviteName.text.Trim(), GuildDone("Đã gửi lời mời."))), 20);
            noticeInput = Field(officerTools.transform, "NoticeIn", new Vector2(0f, 1f), new Vector2(980f, -790f), new Vector2(460f, 64f), "Thông báo bang", 120, 20);
            Btn(officerTools.transform, "NoticeBtn", "Lưu", new Vector2(1f, 1f), new Vector2(-330f, -790f), new Vector2(120f, 64f), Teal, () => Do(x => x.GuildNotice(Me, noticeInput.text, GuildDone("Đã lưu thông báo."))), 20);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                contributeButtons.Add(Btn(g, "Contribute" + i, "", new Vector2(0.5f, 1f), new Vector2(-300f + i * 300f, -720f), new Vector2(280f, 64f), Amber,
                    () => { long amt = GuildView?.contributionGold.ElementAtOrDefault(k) ?? 0; if (amt > 0) Do(x => x.GuildContribute(Me, amt, GuildDone($"Đã đóng góp {amt:N0} Vàng."))); }, 20));
            }
            leaveBtn = Btn(g, "Leave", "Rời bang", new Vector2(1f, 1f), new Vector2(-140f, -790f), new Vector2(200f, 64f), Rose, () => Do(x => x.GuildLeave(Me, GuildDone("Đã rời bang."))), 20);
            disbandBtn = Btn(g, "Disband", "Giải tán", new Vector2(1f, 1f), new Vector2(-140f, -232f), new Vector2(200f, 64f), Rose, () => Do(x => x.GuildDisband(Me, GuildDone("Đã giải tán bang."))), 20);
        }

        private void BuildMarriage(Transform p)
        {
            marriageStatus = Txt(p, "Status", "", 26, Gold, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1320f, 80f), TextAnchor.MiddleCenter, true);
            Rows(p, "proposals", -340f, 4);
            startCeremonyBtn = Btn(p, "Start", "Tổ chức lễ cưới", new Vector2(0.5f, 1f), new Vector2(-330f, -650f), new Vector2(300f, 66f), Amber, () => Do(x => x.StartCeremony(Me, MarriageDone("Đã mời người ấy làm lễ. Chờ xác nhận…"))), 22);
            confirmCeremonyBtn = Btn(p, "Confirm", "Xác nhận làm lễ", new Vector2(0.5f, 1f), new Vector2(0f, -650f), new Vector2(300f, 66f), Green, () => Do(x => x.ConfirmCeremony(Me, MarriageDone("Lễ cưới bắt đầu!"))), 22);
            divorceBtn = Btn(p, "Divorce", "Ly hôn", new Vector2(0.5f, 1f), new Vector2(330f, -650f), new Vector2(240f, 66f), Rose, () => Do(x => x.Divorce(Me, MarriageDone("Đã kết thúc quan hệ."))), 22);
        }

        // ---------------------------------------------------------------- open / tabs
        public void OpenTab(string tab)
        {
            Open();
            Tab = tab;
            foreach (var kv in pages) kv.Value.SetActive(kv.Key == tab);
            foreach (var kv in tabButtons) kv.Value.GetComponent<Image>().color = kv.Key == tab ? Amber : Slate;
            if (ticker) ticker.transform.parent.gameObject.SetActive(false);
            Refresh();
        }

        public override void Open() { base.Open(); if (!pages.Values.Any(p => p.activeSelf)) { foreach (var kv in pages) kv.Value.SetActive(kv.Key == Tab); } }

        /// <summary>Opened next to another player ("E · Tương tác"): friends tab with their name filled in.</summary>
        public void OpenFor(string playerName)
        {
            OpenTab("friends");
            friendName.text = playerName;
            if (chatTo) chatTo.text = playerName;
            Message("Người chơi: " + playerName + " — kết bạn, nhắn riêng hoặc mời vào bang.");
        }

        public void Refresh()
        {
            var n = Server;
            if (!n || Me == null) return;
            switch (Tab)
            {
                case "chat": RedrawChat(); break;
                case "friends": n.GetFriends(Me, FriendsDone(null)); break;
                case "guild": n.GetGuild(Me, GuildDone(null)); break;
                case "marriage": n.GetMarriage(Me, MarriageDone(null)); break;
            }
        }

        private void Do(Action<AdventureServerConnection> call)
        {
            if (Busy || !Server || Me == null) return;
            Busy = true;
            call(Server);
        }

        private Action<bool, FriendsState, string> FriendsDone(string ok) => (success, s, err) =>
        {
            Busy = false;
            if (success && s != null) { Friends = s; if (ok != null) Message(ok); } else Message(err);
            RedrawFriends();
        };
        private Action<bool, GuildViewState, string> GuildDone(string ok) => (success, s, err) =>
        {
            Busy = false;
            if (success && s != null) { GuildView = s; if (ok != null) Message(ok); } else Message(err);
            RedrawGuild();
        };
        private Action<bool, MarriageState, string> MarriageDone(string ok) => (success, s, err) =>
        {
            Busy = false;
            if (success && s != null) { Marriage = s; if (ok != null) Message(ok); } else Message(err);
            RedrawMarriage();
        };

        // ---------------------------------------------------------------- chat
        public void SendChat()
        {
            string text = chatInput ? chatInput.text.Trim() : "";
            if (text.Length == 0 || Busy || !Server || Me == null) return;
            Busy = true;
            Server.SendChat(Me, SendChannel, chatTo.text.Trim(), text, (ok, err) =>
            {
                Busy = false;
                if (ok) chatInput.text = ""; else Message(err);
            });
        }

        /// <summary>For tests / scripted sends.</summary>
        public void SetChat(string channel, string to, string text) { SendChannel = channel; if (chatTo) chatTo.text = to ?? ""; if (chatInput) chatInput.text = text; }

        private void RedrawChat()
        {
            if (!chatLog) return;
            for (int i = 0; i < 3; i++) channelButtons[i].GetComponent<Image>().color = new[] { "world", "guild", "private" }[i] == SendChannel ? Amber : Slate;
            chatTo.gameObject.SetActive(SendChannel == "private");
            chatLog.text = ChatLog.Count == 0 ? "<color=#bbbbbb>Chưa có tin nhắn.</color>" : string.Join("\n", ChatLog.Skip(Math.Max(0, ChatLog.Count - 14)).Select(Line));
        }

        // ---------------------------------------------------------------- friends
        private void RedrawFriends()
        {
            if (!rows.ContainsKey("friends")) return;
            FriendsState f = Friends ?? new FriendsState();
            var items = new List<Action<Row>>();
            foreach (var x in f.incoming) { var q = x; items.Add(r => SetRow(r, q.name + " muốn kết bạn", ClassName(q.heroClass) + " · Cấp " + q.level,
                ("Đồng ý", Green, () => Do(n => n.FriendAccept(Me, q.id, true, FriendsDone("Đã thêm bạn.")))), ("Từ chối", Grey, () => Do(n => n.FriendAccept(Me, q.id, false, FriendsDone(null)))))); }
            foreach (var x in f.friends) { var q = x; items.Add(r => SetRow(r, (q.online ? "<color=#9ff0a8>●</color> " : "<color=#888888>●</color> ") + q.name,
                ClassName(q.heroClass) + " · Cấp " + q.level + (q.online ? " · " + TownLayout.For(q.mapId).DisplayName : " · ngoại tuyến"),
                ("Nhắn", Teal, () => { OpenTab("chat"); SendChannel = "private"; chatTo.text = q.name; RedrawChat(); }),
                ("Cầu hôn", new Color(0.85f, 0.4f, 0.55f), () => Do(n => n.Propose(Me, q.name, MarriageDone("Đã gửi lời cầu hôn tới " + q.name + "!")))),
                ("Xóa", Grey, () => Do(n => n.FriendRemove(Me, q.id, FriendsDone("Đã xóa bạn.")))),
                ("Chặn", Rose, () => Do(n => n.Block(Me, q.name, FriendsDone("Đã chặn.")))))); }
            foreach (var x in f.outgoing) { var q = x; items.Add(r => SetRow(r, "Đã mời: " + q.name, "Đang chờ trả lời", ("Hủy", Grey, () => Do(n => n.FriendRemove(Me, q.id, FriendsDone(null)))))); }
            foreach (var x in f.blocked) { var q = x; items.Add(r => SetRow(r, "<color=#ff9a9a>Đã chặn:</color> " + q.name, "", ("Bỏ chặn", Teal, () => Do(n => n.Unblock(Me, q.id, FriendsDone("Đã bỏ chặn.")))))); }
            List<Row> list = rows["friends"];
            int pages = Math.Max(1, (items.Count + list.Count - 1) / list.Count);
            friendPage = Mathf.Clamp(friendPage, 0, pages - 1);
            for (int i = 0; i < list.Count; i++)
            {
                int k = friendPage * list.Count + i;
                if (k < items.Count) items[k](list[i]); else list[i].Go.SetActive(false);
            }
            pageText.text = items.Count == 0 ? "Chưa có bạn bè. Nhập tên để kết bạn." : $"Trang {friendPage + 1}/{pages}";
        }

        // ---------------------------------------------------------------- guild
        private void RedrawGuild()
        {
            if (!guildNone) return;
            GuildViewState v = GuildView ?? new GuildViewState();
            bool inGuild = v.guild != null && v.guild.Valid;
            guildNone.SetActive(!inGuild);
            guildIn.SetActive(inGuild);
            string econ = v.economyEnabled ? "" : " (đóng góp & thưởng nhiệm vụ bang: chờ duyệt)";
            if (!inGuild)
            {
                for (int i = 0; i < emblemButtons.Count; i++)
                {
                    bool on = i < v.emblems.Length;
                    emblemButtons[i].gameObject.SetActive(on);
                    if (on) SetBtn(emblemButtons[i], EmblemName(v.emblems[i]), v.emblems[i] == emblem ? Amber : Slate, true);
                }
                feeText.text = v.pricesApproved ? $"Phí lập bang: {v.createFeeGold:N0} Vàng" : $"Phí lập bang đề xuất {v.createFeeGold:N0} Vàng — đang chờ duyệt, hiện không thu phí.{econ}";
                var items = new List<Action<Row>>();
                foreach (var x in v.invites) { var q = x; items.Add(r => SetRow(r, $"Lời mời: [{EmblemName(q.emblem)}] {q.name}", $"Cấp {q.level} · {q.members}/{q.maxMembers} · Bang Chủ {q.leader}",
                    ("Vào bang", Green, () => Do(n => n.GuildRespondInvite(Me, q.id, true, GuildDone("Đã vào bang " + q.name + "!")))), ("Từ chối", Grey, () => Do(n => n.GuildRespondInvite(Me, q.id, false, GuildDone(null)))))); }
                foreach (var x in v.browse) { var q = x; bool applied = v.applied.Any(a => a.id == q.id); items.Add(r => SetRow(r, $"[{EmblemName(q.emblem)}] {q.name}", $"Cấp {q.level} · {q.members}/{q.maxMembers} thành viên · Bang Chủ {q.leader}",
                    applied ? ("Đã gửi đơn", Grey, (Action)null) : ("Xin vào", Teal, () => Do(n => n.GuildApply(Me, q.id, GuildDone("Đã gửi đơn xin vào " + q.name + ".")))))); }
                List<Row> list = rows["guildList"];
                for (int i = 0; i < list.Count; i++) if (i < items.Count) items[i](list[i]); else list[i].Go.SetActive(false);
                if (items.Count == 0) SetRow(list[0], "Chưa có bang hội nào", "Hãy là người đầu tiên lập bang!");
                return;
            }
            GuildState g = v.guild;
            bool officer = g.myRole is "leader" or "vice";
            guildHeader.text = $"[{EmblemName(g.emblem)}] {g.name} · Cấp {g.level} · EXP {g.exp:N0}{(g.expNext > 0 ? "/" + g.expNext.ToString("N0") : "")} · Quỹ {g.fundGold:N0} Vàng · {g.members.Length}/{g.maxMembers} · {RoleName(g.myRole)} · Cống hiến {g.myDevotion:N0}";
            guildNotice.text = "Thông báo: " + (string.IsNullOrEmpty(g.notice) ? "(chưa có)" : g.notice) + econ;
            officerTools.SetActive(officer);
            disbandBtn.gameObject.SetActive(g.myRole == "leader");
            foreach (var kv in guildSubButtons) kv.Value.GetComponent<Image>().color = kv.Key == guildSub ? Amber : Slate;
            guildSubButtons["applications"].GetComponentInChildren<Text>().text = "Đơn xin vào" + (g.applications.Length > 0 ? $" ({g.applications.Length})" : "");
            var rowsNow = new List<Action<Row>>();
            if (guildSub == "members")
                foreach (var x in g.members)
                {
                    var q = x;
                    var btns = new List<(string, Color, Action)>();
                    if (g.myRole == "leader" && q.id != Me)
                    {
                        btns.Add(q.role == "vice" ? ("Bãi chức", Grey, (Action)(() => Do(n => n.GuildRole(Me, q.id, "member", GuildDone(null))))) : ("Phong Phó", Teal, () => Do(n => n.GuildRole(Me, q.id, "vice", GuildDone(null)))));
                        btns.Add(("Nhường", Amber, () => Do(n => n.GuildRole(Me, q.id, "leader", GuildDone("Đã nhường chức Bang Chủ.")))));
                    }
                    if (officer && q.id != Me && q.role != "leader" && (g.myRole == "leader" || q.role == "member"))
                        btns.Add(("Mời ra", Rose, () => Do(n => n.GuildKick(Me, q.id, GuildDone(null)))));
                    rowsNow.Add(r => SetRow(r, (q.online ? "<color=#9ff0a8>●</color> " : "<color=#888888>●</color> ") + q.name + " · " + RoleName(q.role),
                        ClassName(q.heroClass) + " · Cấp " + q.level + " · Cống hiến " + q.devotion.ToString("N0"), btns.ToArray()));
                }
            else if (guildSub == "applications")
                foreach (var x in g.applications)
                {
                    var q = x;
                    rowsNow.Add(r => SetRow(r, q.name + " xin vào bang", ClassName(q.heroClass) + " · Cấp " + q.level,
                        ("Duyệt", Green, () => Do(n => n.GuildReview(Me, q.id, true, GuildDone(q.name + " đã vào bang.")))), ("Từ chối", Grey, () => Do(n => n.GuildReview(Me, q.id, false, GuildDone(null))))));
                }
            else if (guildSub == "quests")
                foreach (var x in g.quests)
                {
                    var q = x;
                    rowsNow.Add(r => SetRow(r, (q.done ? "<color=#9ff0a8>✔</color> " : "") + q.text, $"Tiến độ hôm nay: {q.have}/{q.need} · Thưởng: {q.rewardExp} EXP bang, {q.rewardDevotion} cống hiến mỗi thành viên" + (v.economyEnabled ? "" : " (chờ duyệt)")));
                }
            List<Row> gl = rows["guildRows"];
            int gPages = Math.Max(1, (rowsNow.Count + gl.Count - 1) / gl.Count); guildPage = Math.Min(guildPage, gPages - 1);
            for (int i = 0; i < gl.Count; i++) { int k = guildPage * gl.Count + i; if (k < rowsNow.Count) rowsNow[k](gl[i]); else gl[i].Go.SetActive(false); }
            guildPrev.gameObject.SetActive(gPages > 1); guildNext.gameObject.SetActive(gPages > 1);
            if (rowsNow.Count == 0 && guildSub != "fund") SetRow(gl[0], guildSub == "applications" ? "Không có đơn nào" : "Trống", "");
            bool fund = guildSub == "fund";
            for (int i = 0; i < contributeButtons.Count; i++)
            {
                bool on = fund && i < v.contributionGold.Length;
                contributeButtons[i].gameObject.SetActive(on);
                if (on) SetBtn(contributeButtons[i], v.economyEnabled ? $"Góp {v.contributionGold[i]:N0} Vàng" : $"{v.contributionGold[i]:N0} (chờ duyệt)", v.economyEnabled ? Amber : Grey, v.economyEnabled);
            }
            if (fund) SetRow(gl[0], $"Quỹ bang: {g.fundGold:N0} Vàng", v.economyEnabled ? "Góp Vàng vào quỹ để tăng EXP bang và cống hiến của bạn." : "Mức quy đổi cống hiến / EXP đang chờ chủ dự án duyệt — chưa mở đóng góp.");
        }

        // ---------------------------------------------------------------- marriage
        private void RedrawMarriage()
        {
            if (!marriageStatus) return;
            MarriageState m = Marriage ?? new MarriageState();
            CeremonyState c = WorldSync.Current && WorldSync.Current.Last != null ? WorldSync.Current.Last.ceremony : m.ceremony;
            bool inHall = AdventureWorldSession.Current && AdventureWorldSession.Current.MapId == TownLayout.WeddingHall.MapId;
            string ring = m.pricesApproved ? $"Nhẫn cưới: {m.ringPriceGold:N0} Vàng (trả khi cầu hôn, hoàn lại nếu bị từ chối)." : $"Nhẫn cưới đề xuất {m.ringPriceGold:N0} Vàng — chờ duyệt, hiện không thu phí.";
            marriageStatus.text = m.status switch
            {
                "married" => $"♥ Đã thành hôn với <color=#ffb3c8>{m.partnerName}</color> ({(m.partnerOnline ? "trực tuyến" : "ngoại tuyến")})\nNgày cưới: {DateTimeOffset.FromUnixTimeMilliseconds(m.wedMs).ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy}",
                "engaged" => $"Đã đính hôn với <color=#ffb3c8>{m.partnerName}</color> ({(m.partnerOnline ? "trực tuyến" : "ngoại tuyến")})\n" +
                             (inHall ? "Hai bạn đang ở Sảnh Kết Hôn: bấm “Tổ chức lễ cưới” (cả hai cần có mặt)." : "Hãy cùng đến Sảnh Kết Hôn (cổng phía tây làng) để làm lễ."),
                _ => "Chưa có bạn đời.\nCầu hôn một người bạn từ thẻ “Bạn Bè”. " + ring,
            };
            var items = new List<Action<Row>>();
            foreach (var x in m.incoming) { var q = x; items.Add(r => SetRow(r, "♥ " + q.name + " cầu hôn bạn!", ClassName(q.heroClass) + " · Cấp " + q.level,
                ("Đồng ý", Green, () => Do(n => n.RespondProposal(Me, q.id, true, MarriageDone("Hai bạn đã đính hôn!")))), ("Từ chối", Grey, () => Do(n => n.RespondProposal(Me, q.id, false, MarriageDone(null)))))); }
            foreach (var x in m.outgoing) { var q = x; items.Add(r => SetRow(r, "Đã cầu hôn: " + q.name, "Đang chờ trả lời", ("Hủy", Grey, () => Do(n => n.CancelProposal(Me, q.id, MarriageDone(null)))))); }
            List<Row> list = rows["proposals"];
            for (int i = 0; i < list.Count; i++) if (i < items.Count) items[i](list[i]); else list[i].Go.SetActive(false);
            bool mine = c != null && c.Valid && (c.aId == Me || c.bId == Me);
            startCeremonyBtn.gameObject.SetActive(m.status == "engaged" && !(mine && c.state is "invited" or "running"));
            SetBtn(startCeremonyBtn, "Tổ chức lễ cưới", inHall ? Amber : Grey, inHall);
            confirmCeremonyBtn.gameObject.SetActive(mine && c.state == "invited" && c.invitedBy != Me);
            divorceBtn.gameObject.SetActive(m.status is "engaged" or "married");
            SetBtn(divorceBtn, m.status == "married" ? "Ly hôn" : "Hủy đính hôn", Rose, true);
        }

        public void StartCeremony() => Do(x => x.StartCeremony(Me, MarriageDone("Đã mời người ấy làm lễ. Chờ xác nhận…")));
        public void ConfirmCeremony() => Do(x => x.ConfirmCeremony(Me, MarriageDone("Lễ cưới bắt đầu!")));
    }
}
