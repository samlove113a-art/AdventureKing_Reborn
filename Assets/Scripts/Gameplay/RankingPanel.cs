using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Bảng Xếp Hạng": Cấp Độ, Lực Chiến, Bang Hội, Boss Hunter — rows straight from the server (real characters only,
    /// top 10 shown, plus your own rank). Boards with no data yet (no guilds, no world boss) say why instead of
    /// showing invented names.
    /// </summary>
    public sealed class RankingPanel : CodePanel
    {
        public static readonly string[] Boards = { "level", "power", "guild", "boss" };
        private static readonly string[] BoardNames = { "Cấp Độ", "Lực Chiến", "Bang Hội", "Boss Hunter" };
        private static readonly string[] ClassNames = { "Kiếm Sĩ", "Pháp Sư", "Sát Thủ", "Cung Thủ" };
        private const int RowCount = 10;

        public RankBoardState State { get; private set; }
        public string Board { get; private set; } = "level";

        private readonly Button[] tabs = new Button[4];
        private sealed class Row { public Image Bg; public Text Rank, Name, Cls, Value; }
        private readonly Row[] rows = new Row[RowCount];
        private Row mine, headerRow;
        private Text header, empty;

        protected override void Build()
        {
            BuildShell("Bảng Xếp Hạng", new Vector2(1300f, 900f));
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                tabs[i] = Btn(card, "Tab" + i, BoardNames[i], new Vector2(0.5f, 1f), new Vector2(-435f + i * 290f, -160f), new Vector2(270f, 64f), Slate, () => Show(Boards[k]));
            }
            headerRow = MakeRow("Header", -222f);
            headerRow.Bg.color = new Color(0f, 0f, 0f, 0f);
            foreach (Text t in new[] { headerRow.Rank, headerRow.Name, headerRow.Cls, headerRow.Value }) { t.fontSize = 19; t.resizeTextMaxSize = 19; t.color = new Color(0.8f, 0.84f, 0.95f); }
            header = Txt(card, "Total", "", 18, new Color(0.8f, 0.84f, 0.95f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1100f, 28f), TextAnchor.MiddleCenter);
            for (int i = 0; i < RowCount; i++) rows[i] = MakeRow("Row" + i, -268f - i * 50f);
            mine = MakeRow("Mine", -772f);
            mine.Bg.color = new Color(0.85f, 0.72f, 0.45f, 0.95f);
            empty = Txt(card, "Empty", "", 26, Cream, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(1000f, 120f), TextAnchor.MiddleCenter);
        }

        private Row MakeRow(string name, float y)
        {
            var r = new Row { Bg = Box(card, name, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1100f, 46f), null, new Color(0.2f, 0.24f, 0.36f, 0.85f)) };
            Transform t = r.Bg.transform;
            r.Rank = Txt(t, "Rank", "", 22, Gold, new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(100f, 40f), TextAnchor.MiddleCenter, true);
            r.Name = Txt(t, "Name", "", 22, Cream, new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(420f, 40f), TextAnchor.MiddleLeft, true);
            r.Cls = Txt(t, "Class", "", 20, new Color(0.8f, 0.84f, 0.95f), new Vector2(0f, 0.5f), new Vector2(680f, 0f), new Vector2(220f, 40f), TextAnchor.MiddleLeft);
            r.Value = Txt(t, "Value", "", 22, Gold, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 40f), TextAnchor.MiddleRight, true);
            return r;
        }

        public override void Open()
        {
            base.Open();
            Show(Board);
        }

        public void Show(string board)
        {
            Board = board;
            State = null;
            Redraw();
            if (CharacterId == null || !Server) { Message("Chưa kết nối máy chủ."); return; }
            Busy = true;
            Server.GetRanking(CharacterId, board, (ok, s, err) =>
            {
                Busy = false;
                if (board != Board) return;
                if (!ok || s == null) { Message(err ?? "Không tải được bảng xếp hạng."); return; }
                State = s;
                Message("");
                Redraw();
            });
        }

        private void Fill(Row r, RankRowState x, string label)
        {
            r.Rank.text = "#" + x.rank;
            r.Name.text = x.name;
            r.Cls.text = x.heroClass >= 0 && x.heroClass < ClassNames.Length ? ClassNames[x.heroClass] + " · Cấp " + x.level : "";
            r.Value.text = Board == "level" ? "Cấp " + x.value : $"{x.value:N0}";
        }

        private void Redraw()
        {
            if (!root) return;
            for (int i = 0; i < 4; i++) tabs[i].GetComponent<Image>().color = Boards[i] == Board ? Amber : Slate;
            RankBoardState s = State;
            int n = s?.rows?.Length ?? 0;
            headerRow.Bg.gameObject.SetActive(n > 0);
            if (n > 0) { headerRow.Rank.text = "Hạng"; headerRow.Name.text = "Nhân vật"; headerRow.Cls.text = "Nghề · Cấp"; headerRow.Value.text = s.valueLabel; }
            header.text = s == null ? "Đang tải từ máy chủ..." : n == 0 ? "" : $"{s.total} nhân vật trên máy chủ · hiển thị top {RowCount}";
            for (int i = 0; i < RowCount; i++)
            {
                bool on = i < n;
                rows[i].Bg.gameObject.SetActive(on);
                if (!on) continue;
                Fill(rows[i], s.rows[i], s.valueLabel);
                rows[i].Bg.color = s.rows[i].me ? new Color(0.62f, 0.5f, 0.26f, 0.95f) : new Color(0.2f, 0.24f, 0.36f, 0.85f);
            }
            bool showMine = s != null && s.mine != null && !string.IsNullOrEmpty(s.mine.characterId) && s.mine.rank > RowCount;
            mine.Bg.gameObject.SetActive(showMine);
            if (showMine) Fill(mine, s.mine, s.valueLabel);
            empty.text = s != null && n == 0 ? (string.IsNullOrEmpty(s.emptyReason) ? "Chưa có dữ liệu." : s.emptyReason) : "";
        }
    }
}
