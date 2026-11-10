using AdventureKing.Server.Data;

namespace AdventureKing.Server.Domain;

// ---------------------------------------------------------------- leaderboards
public sealed class RankRowDto { public int rank { get; set; } public string characterId { get; set; } = ""; public string name { get; set; } = ""; public int heroClass { get; set; } public int level { get; set; } public long value { get; set; } public bool me { get; set; } }
public sealed class RankBoardDto { public string board { get; set; } = ""; public string title { get; set; } = ""; public string valueLabel { get; set; } = ""; public string emptyReason { get; set; } = ""; public RankRowDto[] rows { get; set; } = Array.Empty<RankRowDto>(); public RankRowDto? mine { get; set; } public int total { get; set; } }

/// <summary>
/// Bảng Xếp Hạng from real server data only: Cấp (level, then EXP, then who got there first), Lực Chiến (computed from
/// the server's own combat stats + equipped gear), Bang Hội (no guild system yet → empty, says so) and Boss Hunter
/// (no world boss yet → empty, says so). No bots, no placeholder names.
/// </summary>
public sealed class RankingService
{
    public const int Top = 50;
    private readonly Database db;
    private readonly FarmService farm;
    public RankingService(Database db, FarmService farm) { this.db = db; this.farm = farm; }

    public static readonly string[] Boards = { "level", "power", "guild", "boss" };

    public RankBoardDto Get(string accountId, string characterId, string board)
    {
        var rows = new List<RankRowDto>();
        db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            if (board is "level" or "power")
            {
                using var cmd = Database.Command(c, tx, "SELECT id, name, hero_class, level, experience FROM characters ORDER BY level DESC, experience DESC, created_ms ASC");
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    rows.Add(new RankRowDto { characterId = r.GetString(0), name = r.GetString(1), heroClass = r.GetInt32(2), level = r.GetInt32(3), value = board == "level" ? r.GetInt32(3) : r.GetInt64(4) });
            }
            return 0;
        });
        if (board == "power")
        {
            foreach (RankRowDto x in rows) x.value = farm.CombatPower(x.characterId, x.heroClass, x.level);
            rows = rows.OrderByDescending(x => x.value).ThenByDescending(x => x.level).ToList(); // stable: ties keep level order
        }
        for (int i = 0; i < rows.Count; i++) { rows[i].rank = i + 1; rows[i].me = rows[i].characterId == characterId; }
        RankRowDto? mine = rows.FirstOrDefault(x => x.me);
        return board switch
        {
            "level" => new RankBoardDto { board = board, title = "Cấp Độ", valueLabel = "Cấp", rows = rows.Take(Top).ToArray(), mine = mine, total = rows.Count },
            "power" => new RankBoardDto { board = board, title = "Lực Chiến", valueLabel = "Lực chiến", rows = rows.Take(Top).ToArray(), mine = mine, total = rows.Count },
            "guild" => new RankBoardDto { board = board, title = "Bang Hội", valueLabel = "Điểm", emptyReason = "Chưa có bang hội nào: hệ thống bang hội chưa được mở." },
            "boss" => new RankBoardDto { board = board, title = "Boss Hunter", valueLabel = "Điểm săn Boss", emptyReason = "Chưa có điểm săn Boss: Boss thế giới chưa được mở." },
            _ => throw new GameError("board_unknown", "Bảng xếp hạng không tồn tại.", 404),
        };
    }
}

// ---------------------------------------------------------------- nameplate frames (titles)
public sealed class TitleDto { public string id { get; set; } = ""; public string category { get; set; } = ""; public int level { get; set; } public string name { get; set; } = ""; public string source { get; set; } = ""; }
public sealed class TitlesDto { public TitleDto[] owned { get; set; } = Array.Empty<TitleDto>(); public string? equipped { get; set; } public string note { get; set; } = ""; }
public sealed class TitleIdDto { public string? titleId { get; set; } }

/// <summary>
/// Khung tên (VIP 1–15, Boss Hunter 1–10, special). Ownership rows are written only by server code once valid data
/// exists (verified payments for VIP, valid boss kills for Boss Hunter, an approved event for special frames) — there
/// is no endpoint that grants one. Players equip one owned frame at a time; frames never add Lực Chiến.
/// Today nobody owns any (payments off, no world boss), so every nameplate shows only the name.
/// </summary>
public sealed class TitleService
{
    private readonly Database db;
    private readonly Func<long> now;
    public TitleService(Database db, Func<long>? clock = null) { this.db = db; now = clock ?? Database.NowMs; }

    public static readonly string[] Catalog =
        Enumerable.Range(1, 15).Select(i => $"VIP_{i:00}")
        .Concat(Enumerable.Range(1, 10).Select(i => $"BossHunter_{i:00}"))
        .Concat(new[] { "Special_Starter", "Special_Event", "Special_Festival", "Special_Achievement", "Special_Couple", "Special_GuildMaster", "Special_TopServer", "Special_Special" })
        .ToArray();

    private static TitleDto Describe(string id, string source)
    {
        string[] p = id.Split('_');
        string cat = p[0] == "VIP" ? "VIP" : p[0] == "BossHunter" ? "BossHunter" : "Special";
        int lvl = cat == "Special" ? 0 : int.Parse(p[1]);
        string name = cat == "VIP" ? $"VIP {lvl}" : cat == "BossHunter" ? $"Boss Hunter {lvl}" : p[1] switch
        {
            "Starter" => "Tân Thủ", "Event" => "Sự Kiện", "Festival" => "Lễ Hội", "Achievement" => "Thành Tựu",
            "Couple" => "Cặp Đôi", "GuildMaster" => "Bang Chủ", "TopServer" => "Top Server", _ => "Đặc Biệt",
        };
        return new TitleDto { id = id, category = cat, level = lvl, name = name, source = source };
    }

    /// <summary>Server-side grant (no HTTP route). Idempotent.</summary>
    public void Grant(string characterId, string titleId, string source)
    {
        if (!Catalog.Contains(titleId)) throw new GameError("title_unknown", "Khung tên không tồn tại.", 404);
        db.InTransaction((c, tx) => Database.Exec(c, tx, "INSERT OR IGNORE INTO titles_owned(character_id, title_id, source, granted_ms) VALUES($c,$t,$s,$m)",
            ("$c", characterId), ("$t", titleId), ("$s", source), ("$m", now())));
    }

    public TitlesDto Get(string accountId, string characterId) => db.InTransaction((c, tx) =>
    {
        Rewards.Own(c, tx, accountId, characterId);
        var owned = new List<TitleDto>();
        using (var cmd = Database.Command(c, tx, "SELECT title_id, source FROM titles_owned WHERE character_id=$c ORDER BY granted_ms", ("$c", characterId)))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) owned.Add(Describe(r.GetString(0), r.GetString(1)));
        string? eq = Database.Scalar<string>(c, tx, "SELECT title_id FROM title_equipped WHERE character_id=$c", ("$c", characterId));
        return new TitlesDto
        {
            owned = owned.ToArray(), equipped = eq,
            note = owned.Count == 0 ? "Chưa sở hữu khung tên nào (khung VIP cần nạp hợp lệ, khung Boss Hunter cần săn Boss: cả hai chưa mở)." : "",
        };
    });

    public TitlesDto Equip(string accountId, string characterId, TitleIdDto? req)
    {
        string? id = req?.titleId;
        db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            if (string.IsNullOrEmpty(id)) { Database.Exec(c, tx, "DELETE FROM title_equipped WHERE character_id=$c", ("$c", characterId)); return 0; }
            if (Database.Scalar<string>(c, tx, "SELECT title_id FROM titles_owned WHERE character_id=$c AND title_id=$t", ("$c", characterId), ("$t", id)) == null)
                throw new GameError("title_not_owned", "Bạn chưa sở hữu khung tên này.", 403);
            Database.Exec(c, tx, "INSERT INTO title_equipped(character_id, title_id) VALUES($c,$t) ON CONFLICT(character_id) DO UPDATE SET title_id=excluded.title_id", ("$c", characterId), ("$t", id));
            return 0;
        });
        return Get(accountId, characterId);
    }
}
