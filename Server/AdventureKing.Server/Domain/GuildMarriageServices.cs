using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

// ================================================================== guilds
public sealed class GuildMemberDto { public string id { get; set; } = ""; public string name { get; set; } = ""; public int heroClass { get; set; } public int level { get; set; } public string role { get; set; } = ""; public long devotion { get; set; } public bool online { get; set; } }
public sealed class GuildQuestDto { public string id { get; set; } = ""; public string text { get; set; } = ""; public int have { get; set; } public int need { get; set; } public bool done { get; set; } public long rewardExp { get; set; } public long rewardDevotion { get; set; } }
public sealed class GuildSummaryDto { public string id { get; set; } = ""; public string name { get; set; } = ""; public string emblem { get; set; } = ""; public int level { get; set; } public int members { get; set; } public int maxMembers { get; set; } public string leader { get; set; } = ""; }
public sealed class GuildDto
{
    public string id { get; set; } = ""; public string name { get; set; } = ""; public string emblem { get; set; } = ""; public string notice { get; set; } = "";
    public int level { get; set; } public long exp { get; set; } public long expNext { get; set; } public long fundGold { get; set; } public int maxMembers { get; set; }
    public string myRole { get; set; } = ""; public long myDevotion { get; set; }
    public GuildMemberDto[] members { get; set; } = Array.Empty<GuildMemberDto>(); public GuildMemberDto[] applications { get; set; } = Array.Empty<GuildMemberDto>();
    public GuildQuestDto[] quests { get; set; } = Array.Empty<GuildQuestDto>();
}
public sealed class GuildViewDto
{
    public GuildDto? guild { get; set; } public GuildSummaryDto[] browse { get; set; } = Array.Empty<GuildSummaryDto>(); public GuildSummaryDto[] invites { get; set; } = Array.Empty<GuildSummaryDto>();
    public GuildSummaryDto[] applied { get; set; } = Array.Empty<GuildSummaryDto>();
    public string[] emblems { get; set; } = Array.Empty<string>(); public long createFeeGold { get; set; } public bool pricesApproved { get; set; } public bool economyEnabled { get; set; }
    public long[] contributionGold { get; set; } = Array.Empty<long>(); public string status { get; set; } = "";
}
public sealed class GuildCreateDto { public string? name { get; set; } public string? emblem { get; set; } public string? requestId { get; set; } }
public sealed class GuildTargetDto { public string? id { get; set; } public string? name { get; set; } public bool accept { get; set; } public string? role { get; set; } }
public sealed class GuildContributeDto { public long amount { get; set; } public string? requestId { get; set; } }
public sealed class GuildNoticeDto { public string? notice { get; set; } }

/// <summary>
/// Guilds, all on the server: create (name rules + unique, emblem from a fixed list; the DRAFT fee is charged only
/// once the owner approves prices), Bang Chủ / Phó Bang (max per config) / member, invite / apply → accept / approve,
/// leave, kick, promote / demote, transfer, disband. Fund + devotion + guild exp from Gold contributions and daily
/// guild quests are the guild ECONOMY, closed until the owner approves (economyEnabled). Every Gold movement goes
/// through Wallet with an idempotency key (no double charge, no duplication).
/// </summary>
public sealed class GuildService
{
    private readonly Database db;
    private readonly SocialConfig cfg;
    private readonly NameRules names;
    private readonly PresenceService presence;
    public Func<long> Clock { get; set; } = Database.NowMs;

    public GuildService(Database db, SocialConfig cfg, NameRules names, PresenceService presence) { this.db = db; this.cfg = cfg; this.names = names; this.presence = presence; }

    private GuildConfig G => cfg.guild;
    public string VnDay() => DateTimeOffset.FromUnixTimeMilliseconds(Clock()).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
    private int LevelFor(long exp) { int l = 0; for (int i = 0; i < G.levelExp.Length; i++) if (exp >= G.levelExp[i]) l = i; return l + 1; }
    private int Cap(int level) => G.maxMembers[Math.Clamp(level - 1, 0, G.maxMembers.Length - 1)];

    private static (string guild, string role)? Membership(SqliteConnection c, SqliteTransaction? tx, string me)
    {
        using var cmd = Database.Command(c, tx, "SELECT guild_id, role FROM guild_members WHERE character_id=$m", ("$m", me));
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetString(1)) : null;
    }
    private static (string guild, string role) Need(SqliteConnection c, SqliteTransaction? tx, string me) =>
        Membership(c, tx, me) ?? throw new GameError("not_in_guild", "Bạn chưa vào bang hội.", 409);
    private static (string guild, string role) Officer(SqliteConnection c, SqliteTransaction? tx, string me)
    {
        var m = Need(c, tx, me);
        if (m.role is not ("leader" or "vice")) throw new GameError("guild_rights", "Chỉ Bang Chủ hoặc Phó Bang làm được việc này.", 403);
        return m;
    }
    private int Count(SqliteConnection c, SqliteTransaction? tx, string g) => (int)Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM guild_members WHERE guild_id=$g", ("$g", g));
    private int LevelOf(SqliteConnection c, SqliteTransaction? tx, string g) => Database.Scalar<int>(c, tx, "SELECT level FROM guilds WHERE id=$g", ("$g", g));

    private void Join(SqliteConnection c, SqliteTransaction tx, string g, string who, string role)
    {
        if (Membership(c, tx, who) != null) throw new GameError("already_in_guild", "Người chơi đã ở trong một bang hội.", 409);
        if (Count(c, tx, g) >= Cap(LevelOf(c, tx, g))) throw new GameError("guild_full", "Bang hội đã đủ thành viên.", 409);
        Database.Exec(c, tx, "INSERT INTO guild_members(character_id, guild_id, role, devotion, joined_ms) VALUES($c,$g,$r,0,$t)", ("$c", who), ("$g", g), ("$r", role), ("$t", Clock()));
        Database.Exec(c, tx, "DELETE FROM guild_requests WHERE character_id=$c", ("$c", who)); // every other invite / application
    }

    public GuildViewDto Get(string accountId, string me) => db.InTransaction((c, tx) => { SocialDb.Me(c, tx, accountId, me); return View(c, tx, me); });

    public GuildViewDto Create(string accountId, string me, GuildCreateDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Who self = SocialDb.Me(c, tx, accountId, me);
        if (Membership(c, tx, me) != null) throw new GameError("already_in_guild", "Bạn đã ở trong một bang hội.", 409);
        if (self.Level < G.minLevelToCreate) throw new GameError("level_too_low", $"Cần đạt cấp {G.minLevelToCreate} để lập bang.", 409);
        NameCheck n = names.Check(req?.name);
        if (!n.Ok) throw new GameError("guild_name_" + n.Code, n.Message.Replace("nhân vật", "bang hội"));
        if (!G.emblems.Contains(req!.emblem ?? "")) throw new GameError("guild_emblem", "Huy hiệu không hợp lệ.");
        if (Database.Scalar<string>(c, tx, "SELECT id FROM guilds WHERE name_key=$k", ("$k", n.UniqueKey)) != null) throw new GameError("guild_name_taken", "Tên bang đã có người dùng.", 409);
        if (cfg.pricesApproved && G.createFeeGold > 0)
        {
            string rid = (req.requestId ?? "").Trim();
            if (rid.Length is < 8 or > 64) throw new GameError("request_id", "Thiếu mã yêu cầu.");
            Wallet.Gold(c, tx, accountId, me, -G.createFeeGold, "guild:create", "guild:create:" + me + ":" + rid);
        }
        string id = Crypto.NewId("gld");
        Database.Exec(c, tx, "INSERT INTO guilds(id, name, name_key, emblem, created_ms) VALUES($i,$n,$k,$e,$t)", ("$i", id), ("$n", n.Normalized), ("$k", n.UniqueKey), ("$e", req.emblem), ("$t", Clock()));
        Join(c, tx, id, me, "leader");
        AccountService.Audit(c, tx, accountId, "guild_create", $"{me} {id} {n.Normalized} fee={(cfg.pricesApproved ? G.createFeeGold : 0)}");
        return View(c, tx, me);
    });

    public GuildViewDto Apply(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        if (Membership(c, tx, me) != null) throw new GameError("already_in_guild", "Bạn đã ở trong một bang hội.", 409);
        string g = Database.Scalar<string>(c, tx, "SELECT id FROM guilds WHERE id=$g", ("$g", req?.id ?? "")) ?? throw new GameError("guild_unknown", "Bang hội không tồn tại.", 404);
        if (Count(c, tx, g) >= Cap(LevelOf(c, tx, g))) throw new GameError("guild_full", "Bang hội đã đủ thành viên.", 409);
        // invited already? then joining is immediate
        if (Database.Exec(c, tx, "DELETE FROM guild_requests WHERE guild_id=$g AND character_id=$m AND kind='invite'", ("$g", g), ("$m", me)) == 1) { Join(c, tx, g, me, "member"); return View(c, tx, me); }
        Database.Exec(c, tx, "INSERT OR IGNORE INTO guild_requests(guild_id, character_id, kind, by_id, created_ms) VALUES($g,$m,'apply',$m,$t)", ("$g", g), ("$m", me), ("$t", Clock()));
        return View(c, tx, me);
    });

    public GuildViewDto Invite(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, _) = Officer(c, tx, me);
        SocialDb.Who t = SocialDb.Target(c, tx, req?.name, me);
        if (SocialDb.Blocked(c, tx, me, t.Id)) throw new GameError("blocked", "Không thể mời người chơi này.", 409);
        if (Membership(c, tx, t.Id) != null) throw new GameError("already_in_guild", "Người chơi đã ở trong một bang hội.", 409);
        // they applied already? accept straight away
        if (Database.Exec(c, tx, "DELETE FROM guild_requests WHERE guild_id=$g AND character_id=$t AND kind='apply'", ("$g", g), ("$t", t.Id)) == 1) { Join(c, tx, g, t.Id, "member"); return View(c, tx, me); }
        Database.Exec(c, tx, "INSERT OR IGNORE INTO guild_requests(guild_id, character_id, kind, by_id, created_ms) VALUES($g,$t,'invite',$m,$n)", ("$g", g), ("$t", t.Id), ("$m", me), ("$n", Clock()));
        return View(c, tx, me);
    });

    public GuildViewDto RespondInvite(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        string g = req?.id ?? "";
        if (Database.Exec(c, tx, "DELETE FROM guild_requests WHERE guild_id=$g AND character_id=$m AND kind='invite'", ("$g", g), ("$m", me)) == 0)
            throw new GameError("invite_unknown", "Lời mời không còn.", 404);
        if (req!.accept) Join(c, tx, g, me, "member");
        return View(c, tx, me);
    });

    public GuildViewDto Review(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, _) = Officer(c, tx, me);
        if (Database.Exec(c, tx, "DELETE FROM guild_requests WHERE guild_id=$g AND character_id=$t AND kind='apply'", ("$g", g), ("$t", req?.id ?? "")) == 0)
            throw new GameError("application_unknown", "Đơn xin vào không còn.", 404);
        if (req!.accept) Join(c, tx, g, req.id!, "member");
        return View(c, tx, me);
    });

    public GuildViewDto Leave(string accountId, string me) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, role) = Need(c, tx, me);
        if (role == "leader")
        {
            if (Count(c, tx, g) > 1) throw new GameError("leader_must_transfer", "Bang Chủ hãy nhường chức trước khi rời bang (hoặc giải tán bang).", 409);
            Disband(c, tx, g);
        }
        else Database.Exec(c, tx, "DELETE FROM guild_members WHERE character_id=$m", ("$m", me));
        return View(c, tx, me);
    });

    private static void Disband(SqliteConnection c, SqliteTransaction tx, string g)
    {
        Database.Exec(c, tx, "DELETE FROM guild_members WHERE guild_id=$g", ("$g", g));
        Database.Exec(c, tx, "DELETE FROM guild_requests WHERE guild_id=$g", ("$g", g));
        Database.Exec(c, tx, "DELETE FROM guild_quest_progress WHERE guild_id=$g", ("$g", g));
        Database.Exec(c, tx, "DELETE FROM guilds WHERE id=$g", ("$g", g));
    }

    public GuildViewDto DisbandGuild(string accountId, string me) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, role) = Need(c, tx, me);
        if (role != "leader") throw new GameError("guild_rights", "Chỉ Bang Chủ được giải tán bang.", 403);
        Disband(c, tx, g);
        AccountService.Audit(c, tx, accountId, "guild_disband", $"{me} {g}");
        return View(c, tx, me);
    });

    public GuildViewDto Kick(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, role) = Officer(c, tx, me);
        var target = Membership(c, tx, req?.id ?? "");
        if (target == null || target.Value.guild != g || req!.id == me) throw new GameError("member_unknown", "Thành viên không có trong bang.", 404);
        if (target.Value.role == "leader" || (role == "vice" && target.Value.role != "member")) throw new GameError("guild_rights", "Bạn không đủ quyền với thành viên này.", 403);
        Database.Exec(c, tx, "DELETE FROM guild_members WHERE character_id=$t", ("$t", req.id));
        return View(c, tx, me);
    });

    public GuildViewDto SetRole(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, role) = Need(c, tx, me);
        if (role != "leader") throw new GameError("guild_rights", "Chỉ Bang Chủ phong / bãi chức.", 403);
        var target = Membership(c, tx, req?.id ?? "");
        if (target == null || target.Value.guild != g || req!.id == me) throw new GameError("member_unknown", "Thành viên không có trong bang.", 404);
        string want = req.role ?? "";
        if (want == "leader")
        {
            Database.Exec(c, tx, "UPDATE guild_members SET role='vice' WHERE character_id=$m", ("$m", me));
            Database.Exec(c, tx, "UPDATE guild_members SET role='leader' WHERE character_id=$t", ("$t", req.id));
            return View(c, tx, me);
        }
        if (want is not ("vice" or "member")) throw new GameError("guild_role", "Chức vụ không hợp lệ.");
        if (want == "vice" && target.Value.role != "vice" &&
            Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM guild_members WHERE guild_id=$g AND role='vice'", ("$g", g)) >= G.maxVice)
            throw new GameError("vice_full", $"Bang chỉ có tối đa {G.maxVice} Phó Bang.", 409);
        Database.Exec(c, tx, "UPDATE guild_members SET role=$r WHERE character_id=$t", ("$r", want), ("$t", req.id));
        return View(c, tx, me);
    });

    public GuildViewDto SetNotice(string accountId, string me, GuildNoticeDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var (g, _) = Officer(c, tx, me);
        string text = names.MaskChat(req?.notice).Trim();
        if (SocialDb.TextLength(text) > 120) throw new GameError("notice_too_long", "Thông báo tối đa 120 ký tự.");
        Database.Exec(c, tx, "UPDATE guilds SET notice=$n WHERE id=$g", ("$n", text), ("$g", g));
        return View(c, tx, me);
    });

    /// <summary>Gold → guild fund (+ devotion, + guild exp). Economy flag + approved tiers only; replay-safe.</summary>
    public GuildViewDto Contribute(string accountId, string me, GuildContributeDto? req) => db.InTransaction((c, tx) =>
    {
        if (!cfg.economyEnabled) throw new GameError("economy_pending", "Đóng góp bang hội đang chờ chủ dự án duyệt mức quy đổi.", 409);
        SocialDb.Me(c, tx, accountId, me);
        var (g, _) = Need(c, tx, me);
        long amount = req?.amount ?? 0;
        if (!G.contributionGold.Contains(amount)) throw new GameError("contribution_amount", "Mức đóng góp không hợp lệ.");
        string rid = (req!.requestId ?? "").Trim();
        if (rid.Length is < 8 or > 64) throw new GameError("request_id", "Thiếu mã yêu cầu.");
        LedgerResult res = Wallet.Gold(c, tx, accountId, me, -amount, "guild:contribute", "guildfund:" + me + ":" + rid);
        if (!res.Applied) return View(c, tx, me); // replay: nothing added twice
        long devotion = (long)Math.Floor(amount * G.devotionPerGold), exp = (long)Math.Floor(amount * G.expPerGold);
        Database.Exec(c, tx, "UPDATE guilds SET fund_gold=fund_gold+$a, exp=exp+$e WHERE id=$g", ("$a", amount), ("$e", exp), ("$g", g));
        Database.Exec(c, tx, "UPDATE guild_members SET devotion=devotion+$d WHERE character_id=$m", ("$d", devotion), ("$m", me));
        Relevel(c, tx, g);
        AccountService.Audit(c, tx, accountId, "guild_contribute", $"{me} {g} {amount}");
        return View(c, tx, me);
    });

    private void Relevel(SqliteConnection c, SqliteTransaction tx, string g)
    {
        long exp = Database.Scalar<long>(c, tx, "SELECT exp FROM guilds WHERE id=$g", ("$g", g));
        Database.Exec(c, tx, "UPDATE guilds SET level=$l WHERE id=$g", ("$l", LevelFor(exp)), ("$g", g));
    }

    /// <summary>Server event feed (same events as quests): guild-wide daily counters; completing one (economy open)
    /// gives guild exp and devotion to every member, once per day per quest.</summary>
    public void Record(string accountId, string me, string ev, int amount)
    {
        if (!G.quests.Any(q => q.@event == ev)) return;
        db.InTransaction((c, tx) =>
        {
            string? g = SocialDb.GuildOf(c, tx, me);
            if (g == null) return 0;
            string day = VnDay();
            foreach (GuildQuestDef q in G.quests.Where(q => q.@event == ev))
            {
                int have = Database.Scalar<int>(c, tx, "SELECT count FROM guild_quest_progress WHERE guild_id=$g AND vn_day=$d AND quest_id=$q", ("$g", g), ("$d", day), ("$q", q.id));
                if (have >= q.need) continue;
                int next = Math.Min(q.need, have + amount);
                Database.Exec(c, tx, "INSERT INTO guild_quest_progress(guild_id, vn_day, quest_id, count) VALUES($g,$d,$q,$n) ON CONFLICT(guild_id, vn_day, quest_id) DO UPDATE SET count=excluded.count",
                    ("$g", g), ("$d", day), ("$q", q.id), ("$n", next));
                if (next >= q.need && cfg.economyEnabled)
                {
                    Database.Exec(c, tx, "UPDATE guilds SET exp=exp+$e WHERE id=$g", ("$e", q.rewardExp), ("$g", g));
                    Database.Exec(c, tx, "UPDATE guild_members SET devotion=devotion+$d WHERE guild_id=$g", ("$d", q.rewardDevotion), ("$g", g));
                    Relevel(c, tx, g);
                }
            }
            return 0;
        });
    }

    public RankBoardDto Ranking(string myCharacterId)
    {
        using var c = db.Open();
        string? mine = SocialDb.GuildOf(c, null, myCharacterId);
        var rows = new List<RankRowDto>();
        using (var cmd = Database.Command(c, null, """
                   SELECT g.id, g.name, g.level, g.exp, (SELECT COUNT(*) FROM guild_members m WHERE m.guild_id=g.id) FROM guilds g
                   ORDER BY g.level DESC, g.exp DESC, g.created_ms ASC
                   """))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                rows.Add(new RankRowDto { rank = rows.Count + 1, characterId = r.GetString(0), name = r.GetString(1), level = r.GetInt32(2), value = r.GetInt64(3), heroClass = -1, me = r.GetString(0) == mine });
        return new RankBoardDto
        {
            board = "guild", title = "Bang Hội", valueLabel = "Điểm bang", rows = rows.Take(RankingService.Top).ToArray(), mine = rows.FirstOrDefault(x => x.me), total = rows.Count,
            emptyReason = rows.Count == 0 ? "Chưa có bang hội nào. Hãy lập bang tại Sảnh Bang Hội!" : "",
        };
    }

    private GuildSummaryDto[] Summaries(SqliteConnection c, SqliteTransaction tx, string where, params (string, object?)[] args)
    {
        var l = new List<GuildSummaryDto>();
        using var cmd = Database.Command(c, tx, $"""
            SELECT g.id, g.name, g.emblem, g.level, (SELECT COUNT(*) FROM guild_members m WHERE m.guild_id=g.id),
                   (SELECT ch.name FROM guild_members m JOIN characters ch ON ch.id=m.character_id WHERE m.guild_id=g.id AND m.role='leader')
            FROM guilds g WHERE {where} ORDER BY g.level DESC, g.exp DESC LIMIT 30
            """, args);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            l.Add(new GuildSummaryDto { id = r.GetString(0), name = r.GetString(1), emblem = r.GetString(2), level = r.GetInt32(3), members = r.GetInt32(4), maxMembers = Cap(r.GetInt32(3)), leader = r.IsDBNull(5) ? "" : r.GetString(5) });
        return l.ToArray();
    }

    private GuildViewDto View(SqliteConnection c, SqliteTransaction tx, string me)
    {
        var view = new GuildViewDto
        {
            emblems = G.emblems, createFeeGold = G.createFeeGold, pricesApproved = cfg.pricesApproved, economyEnabled = cfg.economyEnabled,
            contributionGold = G.contributionGold, status = cfg.status,
            invites = Summaries(c, tx, "g.id IN (SELECT guild_id FROM guild_requests WHERE character_id=$m AND kind='invite')", ("$m", me)),
            applied = Summaries(c, tx, "g.id IN (SELECT guild_id FROM guild_requests WHERE character_id=$m AND kind='apply')", ("$m", me)),
        };
        var m = Membership(c, tx, me);
        if (m == null) { view.browse = Summaries(c, tx, "1=1"); return view; }
        string g = m.Value.guild;
        using (var cmd = Database.Command(c, tx, "SELECT name, emblem, notice, level, exp, fund_gold FROM guilds WHERE id=$g", ("$g", g)))
        using (var r = cmd.ExecuteReader())
        {
            r.Read();
            int lvl = r.GetInt32(3);
            view.guild = new GuildDto
            {
                id = g, name = r.GetString(0), emblem = r.GetString(1), notice = r.GetString(2), level = lvl, exp = r.GetInt64(4), fundGold = r.GetInt64(5),
                expNext = lvl < G.levelExp.Length ? G.levelExp[lvl] : 0, maxMembers = Cap(lvl), myRole = m.Value.role,
            };
        }
        GuildMemberDto[] Members(string sql)
        {
            var l = new List<GuildMemberDto>();
            using var cmd = Database.Command(c, tx, sql, ("$g", g));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                l.Add(new GuildMemberDto { id = r.GetString(0), name = r.GetString(1), heroClass = r.GetInt32(2), level = r.GetInt32(3), role = r.GetString(4), devotion = r.GetInt64(5), online = presence.Online(r.GetString(0)) });
            return l.ToArray();
        }
        view.guild.members = Members("""
            SELECT ch.id, ch.name, ch.hero_class, ch.level, m.role, m.devotion FROM guild_members m JOIN characters ch ON ch.id=m.character_id
            WHERE m.guild_id=$g ORDER BY CASE m.role WHEN 'leader' THEN 0 WHEN 'vice' THEN 1 ELSE 2 END, m.devotion DESC, ch.name
            """);
        view.guild.myDevotion = view.guild.members.First(x => x.id == me).devotion;
        if (m.Value.role is "leader" or "vice")
            view.guild.applications = Members("SELECT ch.id, ch.name, ch.hero_class, ch.level, 'apply', 0 FROM guild_requests q JOIN characters ch ON ch.id=q.character_id WHERE q.guild_id=$g AND q.kind='apply'");
        string day = VnDay();
        view.guild.quests = G.quests.Select(q =>
        {
            int have = Database.Scalar<int>(c, tx, "SELECT count FROM guild_quest_progress WHERE guild_id=$g AND vn_day=$d AND quest_id=$q", ("$g", g), ("$d", day), ("$q", q.id));
            return new GuildQuestDto { id = q.id, text = q.text, have = have, need = q.need, done = have >= q.need, rewardExp = q.rewardExp, rewardDevotion = q.rewardDevotion };
        }).ToArray();
        return view;
    }
}

// ================================================================== marriage
public sealed class ProposalDto { public string id { get; set; } = ""; public string name { get; set; } = ""; public int heroClass { get; set; } public int level { get; set; } }
public sealed class MarriageDto
{
    public string status { get; set; } = "none"; public string partnerId { get; set; } = ""; public string partnerName { get; set; } = ""; public int partnerClass { get; set; }
    public bool partnerOnline { get; set; } public long engagedMs { get; set; } public long wedMs { get; set; }
    public ProposalDto[] incoming { get; set; } = Array.Empty<ProposalDto>(); public ProposalDto[] outgoing { get; set; } = Array.Empty<ProposalDto>();
    public long ringPriceGold { get; set; } public bool pricesApproved { get; set; } public CeremonyDto? ceremony { get; set; }
}
public sealed class CeremonyStepDto { public string id { get; set; } = ""; public long startMs { get; set; } public int ms { get; set; } }
public sealed class CeremonyDto
{
    public string id { get; set; } = ""; public string state { get; set; } = ""; public string invitedBy { get; set; } = ""; public long inviteExpiresMs { get; set; }
    public string aId { get; set; } = ""; public string aName { get; set; } = ""; public int aClass { get; set; }
    public string bId { get; set; } = ""; public string bName { get; set; } = ""; public int bClass { get; set; }
    public long startMs { get; set; } public long endMs { get; set; } public long serverMs { get; set; } public CeremonyStepDto[] steps { get; set; } = Array.Empty<CeremonyStepDto>();
}
public sealed class ProposeDto { public string? name { get; set; } public string? requestId { get; set; } }

/// <summary>
/// Marriage between two real players, on the server: propose (friends, both free, level, not blocked; the ring price
/// is charged only once the owner approves prices, refunded on decline) → accept / decline → engaged. The ceremony in
/// the Wedding Hall needs BOTH partners present on that map: one starts it, the other confirms; then the server runs it
/// on its own clock (steps: intro, vows, rings, cake, toast, kiss, finale) and every client in the hall plays the same
/// timeline. If a partner leaves the hall it is cancelled (still engaged). At the end the server marks them married
/// and gives both the wedding gifts (unlisted cosmetics, once). Divorce ends it (gifts are kept).
/// </summary>
public sealed class MarriageService
{
    private readonly Database db;
    private readonly SocialConfig cfg;
    private readonly PresenceService presence;
    private readonly object gate = new();
    private Ceremony? current; // one ceremony at a time in the Wedding Hall
    public Func<long> Clock { get; set; } = Database.NowMs;

    private sealed class Ceremony
    {
        public string Id = "", MarriageId = "", A = "", B = "", AName = "", BName = "", InvitedBy = "", State = "invited";
        public int AClass, BClass;
        public long InviteExpires, Start, End, DoneMs;
    }

    public MarriageService(Database db, SocialConfig cfg, PresenceService presence) { this.db = db; this.cfg = cfg; this.presence = presence; }
    private MarriageConfig M => cfg.marriage;

    private static (string id, string a, string b, string status, long engaged, long wed)? MarriageOf(SqliteConnection c, SqliteTransaction? tx, string me)
    {
        using var cmd = Database.Command(c, tx, "SELECT id, a_id, b_id, status, engaged_ms, COALESCE(wed_ms,0) FROM marriages WHERE a_id=$m OR b_id=$m", ("$m", me));
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetInt64(5)) : null;
    }

    public MarriageDto Get(string accountId, string me) { Tick(); return db.InTransaction((c, tx) => { SocialDb.Me(c, tx, accountId, me); return View(c, tx, me); }); }

    public MarriageDto Propose(string accountId, string me, ProposeDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Who self = SocialDb.Me(c, tx, accountId, me);
        SocialDb.Who t = SocialDb.Target(c, tx, req?.name, me);
        if (MarriageOf(c, tx, me) != null) throw new GameError("already_bound", "Bạn đã có bạn đời hoặc đang đính hôn.", 409);
        if (MarriageOf(c, tx, t.Id) != null) throw new GameError("partner_bound", "Người ấy đã có bạn đời hoặc đang đính hôn.", 409);
        if (SocialDb.Blocked(c, tx, me, t.Id)) throw new GameError("blocked", "Không thể cầu hôn người chơi này.", 409);
        if (M.requireFriends && !SocialDb.Friends(c, tx, me, t.Id)) throw new GameError("not_friends", "Hai bạn cần là bạn bè trước khi cầu hôn.", 409);
        if (self.Level < M.minLevel || t.Level < M.minLevel) throw new GameError("level_too_low", $"Cả hai cần đạt cấp {M.minLevel}.", 409);
        if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM proposals WHERE (from_id=$m AND to_id=$t) OR (from_id=$t AND to_id=$m)", ("$m", me), ("$t", t.Id)) > 0)
            throw new GameError("already_proposed", "Đã có lời cầu hôn giữa hai bạn đang chờ trả lời.", 409);
        if (cfg.pricesApproved && M.ringPriceGold > 0)
            Wallet.Gold(c, tx, accountId, me, -M.ringPriceGold, "marriage:ring", "ring:" + me + ":" + t.Id + ":" + Clock());
        Database.Exec(c, tx, "INSERT INTO proposals(from_id, to_id, created_ms) VALUES($m,$t,$n)", ("$m", me), ("$t", t.Id), ("$n", Clock()));
        AccountService.Audit(c, tx, accountId, "propose", $"{me} -> {t.Id}");
        return View(c, tx, me);
    });

    public MarriageDto Respond(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        string from = req?.id ?? "";
        if (Database.Exec(c, tx, "DELETE FROM proposals WHERE from_id=$f AND to_id=$m", ("$f", from), ("$m", me)) == 0)
            throw new GameError("proposal_unknown", "Lời cầu hôn không còn.", 404);
        SocialDb.Who proposer = SocialDb.ById(c, tx, from)!;
        if (!req!.accept || MarriageOf(c, tx, me) != null || MarriageOf(c, tx, from) != null)
        {
            Refund(c, tx, proposer, me);
            if (req.accept) throw new GameError("already_bound", "Một trong hai bạn đã có bạn đời.", 409);
            return View(c, tx, me);
        }
        Database.Exec(c, tx, "INSERT INTO marriages(id, a_id, b_id, status, engaged_ms) VALUES($i,$a,$b,'engaged',$t)", ("$i", Crypto.NewId("mar")), ("$a", from), ("$b", me), ("$t", Clock()));
        // every other proposal involving either of them is void (refunded)
        var others = new List<(string f, string t)>();
        using (var cmd = Database.Command(c, tx, "SELECT from_id, to_id FROM proposals WHERE from_id IN ($a,$b) OR to_id IN ($a,$b)", ("$a", from), ("$b", me)))
        using (var r = cmd.ExecuteReader()) while (r.Read()) others.Add((r.GetString(0), r.GetString(1)));
        foreach (var (f, t) in others)
        {
            Database.Exec(c, tx, "DELETE FROM proposals WHERE from_id=$f AND to_id=$t", ("$f", f), ("$t", t));
            Refund(c, tx, SocialDb.ById(c, tx, f)!, t);
        }
        AccountService.Audit(c, tx, proposer.AccountId, "engaged", $"{from} & {me}");
        return View(c, tx, me);
    });

    private void Refund(SqliteConnection c, SqliteTransaction tx, SocialDb.Who proposer, string to)
    {
        if (cfg.pricesApproved && M.ringPriceGold > 0)
            Wallet.Gold(c, tx, proposer.AccountId, proposer.Id, M.ringPriceGold, "marriage:ring_refund", "ringrefund:" + proposer.Id + ":" + to + ":" + Clock());
    }

    public MarriageDto CancelProposal(string accountId, string me, GuildTargetDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Who self = SocialDb.Me(c, tx, accountId, me);
        if (Database.Exec(c, tx, "DELETE FROM proposals WHERE from_id=$m AND to_id=$t", ("$m", me), ("$t", req?.id ?? "")) == 1) Refund(c, tx, self, req!.id!);
        return View(c, tx, me);
    });

    public MarriageDto Divorce(string accountId, string me) { Tick(); return DivorceCore(accountId, me); }
    private MarriageDto DivorceCore(string accountId, string me) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var m = MarriageOf(c, tx, me) ?? throw new GameError("not_bound", "Bạn chưa có bạn đời.", 409);
        lock (gate) if (current != null && current.MarriageId == m.id && current.State is "invited" or "running") current.State = "cancelled";
        Database.Exec(c, tx, "DELETE FROM marriages WHERE id=$i", ("$i", m.id));
        AccountService.Audit(c, tx, accountId, "divorce", $"{m.a} & {m.b} ({m.status})");
        return View(c, tx, me);
    });

    // ------------------------------------------------------------- ceremony (server clock)
    private bool BothPresent(string a, string b) => presence.PresentOn(a, SyncService.WeddingHall) && presence.PresentOn(b, SyncService.WeddingHall);

    public MarriageDto StartCeremony(string accountId, string me) { Tick(); return StartCore(accountId, me); }
    private MarriageDto StartCore(string accountId, string me) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        var m = MarriageOf(c, tx, me) ?? throw new GameError("not_engaged", "Bạn cần đính hôn trước.", 409);
        if (m.status != "engaged") throw new GameError("already_married", "Hai bạn đã thành hôn.", 409);
        string partner = m.a == me ? m.b : m.a;
        if (!BothPresent(me, partner)) throw new GameError("partner_absent", "Cả hai cần có mặt tại Sảnh Kết Hôn.", 409);
        lock (gate)
        {
            if (current != null && current.State is "invited" or "running" && current.MarriageId != m.id) throw new GameError("hall_busy", "Sảnh đang có lễ cưới khác, hãy chờ.", 409);
            if (current != null && current.MarriageId == m.id && current.State == "running") throw new GameError("ceremony_running", "Lễ cưới đang diễn ra.", 409);
            SocialDb.Who a = SocialDb.ById(c, tx, m.a)!, b = SocialDb.ById(c, tx, m.b)!;
            current = new Ceremony
            {
                Id = Crypto.NewId("cer"), MarriageId = m.id, A = a.Id, B = b.Id, AName = a.Name, BName = b.Name, AClass = a.HeroClass, BClass = b.HeroClass,
                InvitedBy = me, State = "invited", InviteExpires = Clock() + M.ceremony.inviteSeconds * 1000L,
            };
        }
        return View(c, tx, me);
    });

    public MarriageDto ConfirmCeremony(string accountId, string me) { Tick(); return ConfirmCore(accountId, me); }
    private MarriageDto ConfirmCore(string accountId, string me) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        lock (gate)
        {
            if (current == null || current.State != "invited" || (current.A != me && current.B != me)) throw new GameError("ceremony_unknown", "Không có lời mời làm lễ.", 404);
            if (current.InvitedBy == me) throw new GameError("ceremony_wait", "Hãy chờ người ấy xác nhận.", 409);
            if (!BothPresent(current.A, current.B)) throw new GameError("partner_absent", "Cả hai cần có mặt tại Sảnh Kết Hôn.", 409);
            current.State = "running";
            current.Start = Clock() + M.ceremony.leadInMs;
            current.End = current.Start + M.ceremony.steps.Sum(s => (long)s.ms);
        }
        return View(c, tx, me);
    });

    public MarriageDto CancelCeremony(string accountId, string me) { Tick(); return CancelCore(accountId, me); }
    private MarriageDto CancelCore(string accountId, string me) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        lock (gate) if (current != null && (current.A == me || current.B == me) && current.State is "invited" or "running") { current.State = "cancelled"; current.DoneMs = Clock(); }
        return View(c, tx, me);
    });

    /// <summary>Advances the ceremony: invite timeout, a partner leaving the hall cancels, the end finalizes the marriage.</summary>
    public void Tick()
    {
        Ceremony? finish = null;
        lock (gate)
        {
            if (current == null) return;
            long t = Clock();
            if (current.State == "invited" && t > current.InviteExpires) { current.State = "expired"; current.DoneMs = t; }
            else if (current.State == "running")
            {
                if (!BothPresent(current.A, current.B)) { current.State = "cancelled"; current.DoneMs = t; }
                else if (t >= current.End) { current.State = "done"; current.DoneMs = t; finish = current; }
            }
            if (current.State is "done" or "cancelled" or "expired" && t - current.DoneMs > 30000) current = null; // shown 30 s, then cleared
        }
        if (finish == null) return;
        db.InTransaction((c, tx) =>
        {
            if (Database.Exec(c, tx, "UPDATE marriages SET status='married', wed_ms=$t WHERE id=$i AND status='engaged'", ("$t", Clock()), ("$i", finish.MarriageId)) == 1)
                foreach (string id in new[] { finish.A, finish.B })
                {
                    SocialDb.Who w = SocialDb.ById(c, tx, id)!;
                    foreach (string gift in M.gifts) CosmeticShop.GrantGift(c, tx, w.AccountId, w.Id, gift, "wedding:" + finish.MarriageId);
                    AccountService.Audit(c, tx, w.AccountId, "married", $"{finish.A} & {finish.B}");
                }
            return 0;
        });
    }

    /// <summary>The ceremony as everybody in the Wedding Hall sees it (null when none).</summary>
    public CeremonyDto? CeremonyView(bool tick = true)
    {
        if (tick) Tick();
        lock (gate)
        {
            if (current == null) return null;
            long s = current.Start;
            var steps = new List<CeremonyStepDto>();
            foreach (var st in M.ceremony.steps) { steps.Add(new CeremonyStepDto { id = st.id, startMs = s, ms = st.ms }); s += st.ms; }
            return new CeremonyDto
            {
                id = current.Id, state = current.State, invitedBy = current.InvitedBy, inviteExpiresMs = current.InviteExpires,
                aId = current.A, aName = current.AName, aClass = current.AClass, bId = current.B, bName = current.BName, bClass = current.BClass,
                startMs = current.Start, endMs = current.End, serverMs = Clock(), steps = current.State == "invited" ? Array.Empty<CeremonyStepDto>() : steps.ToArray(),
            };
        }
    }

    private MarriageDto View(SqliteConnection c, SqliteTransaction tx, string me)
    {
        ProposalDto[] P(string sql)
        {
            var l = new List<ProposalDto>();
            using var cmd = Database.Command(c, tx, sql, ("$m", me));
            using var r = cmd.ExecuteReader();
            while (r.Read()) l.Add(new ProposalDto { id = r.GetString(0), name = r.GetString(1), heroClass = r.GetInt32(2), level = r.GetInt32(3) });
            return l.ToArray();
        }
        var dto = new MarriageDto
        {
            ringPriceGold = M.ringPriceGold, pricesApproved = cfg.pricesApproved,
            incoming = P("SELECT c.id, c.name, c.hero_class, c.level FROM proposals p JOIN characters c ON c.id=p.from_id WHERE p.to_id=$m"),
            outgoing = P("SELECT c.id, c.name, c.hero_class, c.level FROM proposals p JOIN characters c ON c.id=p.to_id WHERE p.from_id=$m"),
        };
        var m = MarriageOf(c, tx, me);
        if (m != null)
        {
            string partner = m.Value.a == me ? m.Value.b : m.Value.a;
            SocialDb.Who p = SocialDb.ById(c, tx, partner)!;
            dto.status = m.Value.status; dto.partnerId = partner; dto.partnerName = p.Name; dto.partnerClass = p.HeroClass;
            dto.partnerOnline = presence.Online(partner); dto.engagedMs = m.Value.engaged; dto.wedMs = m.Value.wed;
            CeremonyDto? cer = CeremonyView(tick: false); // inside a transaction: never finalize from here
            if (cer != null && (cer.aId == me || cer.bId == me)) dto.ceremony = cer;
        }
        return dto;
    }
}
