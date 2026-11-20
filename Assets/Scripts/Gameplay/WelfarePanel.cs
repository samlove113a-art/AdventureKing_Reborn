using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Sự Kiện · Phúc Lợi" window: tabs on the left (Sự Kiện Mới, Đăng Nhập, Nạp Đầu, Phúc Lợi Online, Quà Tân Thủ,
    /// Quà Lên Cấp) straight from the server, the event banner / dates / description on the right, then each reward
    /// step with its real progress bar and a Nhận / Đã nhận / Chưa đạt / Chờ duyệt button. Claims are server requests
    /// that pay once. Also sends the 30 s online heartbeat while in the world (the server caps gaps), and drives the
    /// red dots on the Sự kiện / Phúc lợi icons only when the server says something is claimable.
    /// </summary>
    public sealed class WelfarePanel : CodePanel
    {
        [SerializeField] private CheckinPanel checkin;
        [SerializeField] private GameObject[] dots = new GameObject[0];
        [SerializeField] private Sprite bannerSprite;

        public const float PingSeconds = 30f;
        public WelfareState State { get; private set; }
        public string Tab { get; private set; } = "";
        public int Pings { get; private set; }
        public bool DotShown => dots.Any(d => d && d.activeSelf);

        private const int TabCount = 6, RowCount = 4;
        private readonly Button[] tabs = new Button[TabCount];
        private readonly Text[] tabLabels = new Text[TabCount];
        private readonly GameObject[] tabDots = new GameObject[TabCount];
        private Text head, period, desc, notice;
        private Image banner;
        private Button openCheckin;
        private sealed class Row { public GameObject Go; public Text Text, Reward, Count; public Image Fill; public Button Claim; public RewardStrip Icons; }
        private readonly Row[] rows = new Row[RowCount];
        private float nextPing, nextDots;

        private void Start()
        {
            foreach (GameObject d in dots) if (d) d.SetActive(false);
            nextPing = Time.unscaledTime + 2f;
            nextDots = Time.unscaledTime + 1.5f;
        }

        private void Update()
        {
            string id = CharacterId;
            if (id == null || !Server) return;
            if (Time.unscaledTime >= nextPing)
            {
                nextPing = Time.unscaledTime + PingSeconds;
                Server.OnlinePing(id, (ok, err) => { if (ok) Pings++; });
            }
            if (Time.unscaledTime >= nextDots && !Busy)
            {
                nextDots = Time.unscaledTime + 90f;
                Refresh();
            }
        }

        protected override void Build()
        {
            BuildShell("Sự Kiện · Phúc Lợi", new Vector2(1500f, 860f));
            for (int i = 0; i < TabCount; i++)
            {
                int k = i;
                tabs[i] = Btn(card, "Tab" + i, "", new Vector2(0f, 1f), new Vector2(190f, -200f - i * 92f), new Vector2(270f, 80f), Slate, () => SelectIndex(k), 22);
                tabLabels[i] = tabs[i].GetComponentInChildren<Text>();
                tabDots[i] = Dot(tabs[i].transform);
            }
            // right side: x 340..1440 of the card (centre 890 from the left)
            banner = Box(card, "Banner", new Vector2(0f, 1f), new Vector2(890f, -205f), new Vector2(1080f, 120f), bannerSprite, bannerSprite ? Color.white : new Color(0.33f, 0.2f, 0.42f, 0.9f));
            head = Txt(banner.transform, "Head", "", 36, Gold, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(1000f, 50f), TextAnchor.MiddleCenter, true);
            head.gameObject.AddComponent<Outline>().effectColor = new Color(0.2f, 0.08f, 0.02f, 0.9f);
            period = Txt(banner.transform, "Period", "", 22, Cream, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1000f, 32f), TextAnchor.MiddleCenter);
            desc = Txt(card, "Desc", "", 22, Cream, new Vector2(0f, 1f), new Vector2(890f, -305f), new Vector2(1080f, 60f), TextAnchor.MiddleCenter);
            notice = Txt(card, "Notice", "", 20, new Color(1f, 0.75f, 0.45f), new Vector2(0f, 1f), new Vector2(890f, -350f), new Vector2(1080f, 30f), TextAnchor.MiddleCenter, true);
            for (int i = 0; i < RowCount; i++)
            {
                var r = new Row();
                Image bg = Box(card, "Step" + i, new Vector2(0f, 1f), new Vector2(890f, -420f - i * 104f), new Vector2(1080f, 94f), cellSprite, new Color(0.55f, 0.6f, 0.78f, 0.95f));
                r.Go = bg.gameObject;
                Transform t = bg.transform;
                r.Text = Txt(t, "Text", "", 25, Gold, new Vector2(0f, 0.5f), new Vector2(240f, 18f), new Vector2(440f, 36f), TextAnchor.MiddleLeft, true);
                r.Reward = Txt(t, "Reward", "", 18, Cream, new Vector2(0f, 0.5f), new Vector2(240f, -20f), new Vector2(440f, 30f), TextAnchor.MiddleLeft);
                r.Icons = MakeStrip(t, "Reward", new Vector2(0f, 0.5f), new Vector2(500f, 0f), 4, 56f, 3f); // 472..705
                Image track = Box(t, "Track", new Vector2(0f, 0.5f), new Vector2(785f, -10f), new Vector2(150f, 18f), null, new Color(0f, 0f, 0f, 0.45f));
                r.Fill = Box(track.transform, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 18f), White, new Color(0.55f, 0.95f, 0.5f));
                r.Fill.type = Image.Type.Filled; r.Fill.fillMethod = Image.FillMethod.Horizontal;
                r.Count = Txt(t, "Count", "", 20, Cream, new Vector2(0f, 0.5f), new Vector2(785f, 18f), new Vector2(150f, 30f), TextAnchor.MiddleCenter, true);
                int k = i;
                r.Claim = Btn(t, "Claim", "Nhận", new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(190f, 66f), Green, () => ClaimRow(k));
                rows[i] = r;
            }
            openCheckin = Btn(card, "OpenCheckin", "Mở Điểm Danh 7 Ngày", new Vector2(0f, 1f), new Vector2(890f, -470f), new Vector2(420f, 72f), Amber, () => { Close(); if (checkin) checkin.Open(); });
            openCheckin.gameObject.SetActive(false);
        }

        /// <summary>Open on a tab: "event", "login", "firsttopup", "online", "newbie", "levelup".</summary>
        public void OpenTab(string tab)
        {
            Open();
            Tab = tab;
            Redraw();
            Refresh();
        }

        public void Refresh()
        {
            string id = CharacterId;
            if (id == null || !Server) return;
            Server.GetWelfare(id, (ok, s, err) =>
            {
                if (!ok || s == null) { if (IsOpen) Message(err ?? "Không tải được sự kiện."); foreach (GameObject d in dots) if (d) d.SetActive(false); return; }
                State = s;
                foreach (GameObject d in dots) if (d) d.SetActive(s.rewardsEnabled && s.anyClaimable);
                Redraw();
            });
        }

        private WelfareTabState[] Tabs => State?.tabs ?? new WelfareTabState[0];
        private WelfareTabState Current => Tabs.FirstOrDefault(t => t.id == Tab || (Tab == "event" && t.id.StartsWith("event:"))) ?? Tabs.FirstOrDefault();

        private readonly System.Collections.Generic.Dictionary<string, Sprite> banners = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Generated banner per tab: two-colour gradient, soft light from above, decorative dots
        /// (mushroom caps for the festival). Locked tabs are grey.</summary>
        private Sprite BannerFor(WelfareTabState tab)
        {
            string key = tab.locked ? "locked" : tab.id.StartsWith("event:") ? "event" : tab.id;
            if (banners.TryGetValue(key, out Sprite s) && s) return s;
            (Color a, Color b, Color dot) = key switch
            {
                "event" => (new Color(0.95f, 0.45f, 0.25f), new Color(0.55f, 0.16f, 0.32f), new Color(1f, 0.92f, 0.8f)),
                "online" => (new Color(0.2f, 0.62f, 0.66f), new Color(0.12f, 0.24f, 0.45f), new Color(0.75f, 1f, 0.95f)),
                "levelup" => (new Color(0.9f, 0.68f, 0.22f), new Color(0.5f, 0.26f, 0.12f), new Color(1f, 0.95f, 0.7f)),
                "newbie" => (new Color(0.35f, 0.7f, 0.38f), new Color(0.13f, 0.34f, 0.25f), new Color(0.85f, 1f, 0.8f)),
                "login" => (new Color(0.36f, 0.5f, 0.9f), new Color(0.2f, 0.18f, 0.48f), new Color(0.85f, 0.9f, 1f)),
                _ => (new Color(0.42f, 0.42f, 0.48f), new Color(0.22f, 0.22f, 0.28f), new Color(0.7f, 0.7f, 0.75f)),
            };
            const int w = 540, h = 60;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            var rnd = new System.Random(key.GetHashCode());
            var dots = new (float x, float y, float r, bool cap)[18];
            for (int i = 0; i < dots.Length; i++) dots[i] = ((float)rnd.NextDouble() * w, (float)rnd.NextDouble() * h, 2f + (float)rnd.NextDouble() * 5f, key == "event" && i % 3 == 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    Color c = Color.Lerp(b, a, Mathf.SmoothStep(0f, 1f, 0.35f + 0.65f * Mathf.Sin(u * Mathf.PI) * 0.8f + v * 0.2f));
                    c = Color.Lerp(c, Color.white, Mathf.Pow(v, 6f) * 0.25f);              // light from above
                    c *= 1f - Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 4f) * 0.35f;              // darker ends
                    foreach (var d in dots)
                    {
                        float dist = new Vector2(x - d.x, (y - d.y) * (d.cap ? 1.6f : 1f)).magnitude;
                        if (d.cap && y < d.y) continue;                                    // half circle: a mushroom cap
                        float k = Mathf.Clamp01((d.r * (d.cap ? 2.2f : 1f)) - dist);
                        if (k > 0f) c = Color.Lerp(c, dot, k * 0.35f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            return banners[key] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 0, 24, 0));
        }

        public void SelectIndex(int i)
        {
            if (i < Tabs.Length) { Tab = Tabs[i].id; Message(""); Redraw(); }
        }

        private void Redraw()
        {
            if (!root) return;
            WelfareTabState[] all = Tabs;
            WelfareTabState cur = Current;
            for (int i = 0; i < TabCount; i++)
            {
                bool on = i < all.Length;
                tabs[i].gameObject.SetActive(on);
                if (!on) continue;
                tabLabels[i].text = all[i].id.StartsWith("event:") ? "Sự Kiện Mới" : all[i].title;
                tabs[i].GetComponent<Image>().color = all[i] == cur ? Amber : Slate;
                tabDots[i].SetActive(State.rewardsEnabled && all[i].steps.Any(s => s.claimable));
            }
            if (cur == null) { head.text = "Đang tải từ máy chủ..."; period.text = desc.text = notice.text = ""; foreach (Row r in rows) r.Go.SetActive(false); return; }
            head.text = cur.title;
            if (!bannerSprite) { banner.sprite = BannerFor(cur); banner.type = Image.Type.Simple; banner.color = Color.white; }
            period.text = string.IsNullOrEmpty(cur.period) ? "" : (cur.id == "online" ? "Hôm nay: " : "Thời gian: ") + cur.period;
            desc.text = cur.desc;
            notice.text = cur.locked ? cur.lockedReason
                : !State.rewardsEnabled && cur.steps.Length > 0 && cur.id != "newbie" ? "Phần thưởng là bản nháp đang chờ chủ dự án phê duyệt — chưa thể nhận." : "";
            openCheckin.gameObject.SetActive(cur.id == "login");
            for (int i = 0; i < RowCount; i++)
            {
                Row r = rows[i];
                bool on = i < cur.steps.Length;
                r.Go.SetActive(on);
                if (!on) continue;
                RewardStepState s = cur.steps[i];
                r.Text.text = s.text;
                r.Reward.text = "Thưởng: " + s.reward;
                FillStrip(r.Icons, s.gold, s.items, s.claimed);
                r.Count.text = $"{s.have}/{s.need}";
                r.Fill.fillAmount = s.need > 0 ? Mathf.Clamp01(s.have / (float)s.need) : 0f;
                bool done = s.have >= s.need;
                if (s.claimed) SetBtn(r.Claim, cur.id == "newbie" ? "Đã nhận" : "Đã nhận", Grey, false);
                else if (s.claimable) SetBtn(r.Claim, "Nhận", Green, !Busy);
                else if (cur.locked) SetBtn(r.Claim, "Đã khóa", Grey, false);
                else if (done && !State.rewardsEnabled) SetBtn(r.Claim, "Chờ duyệt", Grey, false);
                else SetBtn(r.Claim, "Chưa đạt", Grey, false);
            }
        }

        public void ClaimRow(int i)
        {
            WelfareTabState cur = Current;
            if (Busy || cur == null || i >= cur.steps.Length || !cur.steps[i].claimable) return;
            string id = CharacterId;
            if (id == null || !Server) return;
            Busy = true;
            Redraw();
            string reward = cur.steps[i].reward;
            Server.ClaimWelfare(id, cur.steps[i].key, (ok, s, err) =>
            {
                Busy = false;
                if (ok && s != null)
                {
                    State = s;
                    ShowGold(s.gold);
                    foreach (GameObject d in dots) if (d) d.SetActive(s.rewardsEnabled && s.anyClaimable);
                    Message("Đã nhận: " + reward);
                }
                else Message(err ?? "Không nhận được phần thưởng.");
                Redraw();
            });
        }
    }
}
