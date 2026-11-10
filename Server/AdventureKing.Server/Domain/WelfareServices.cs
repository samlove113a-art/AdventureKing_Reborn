using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class RewardItem { public string itemId { get; set; } = ""; public int qty { get; set; } = 1; }
public sealed class EventTask { public string @event { get; set; } = ""; public int need { get; set; } = 1; public string text { get; set; } = ""; public long rewardGold { get; set; } public RewardItem[] rewardItems { get; set; } = Array.Empty<RewardItem>(); }
public sealed class EventDef { public string id { get; set; } = ""; public string title { get; set; } = ""; public string banner { get; set; } = ""; public string desc { get; set; } = ""; public string startVn { get; set; } = ""; public string endVn { get; set; } = ""; public EventTask[] tasks { get; set; } = Array.Empty<EventTask>(); }
public sealed class OnlineGift { public int minutes { get; set; } public long rewardGold { get; set; } public RewardItem[] rewardItems { get; set; } = Array.Empty<RewardItem>(); }
public sealed class LevelGift { public int level { get; set; } public long rewardGold { get; set; } public RewardItem[] rewardItems { get; set; } = Array.Empty<RewardItem>(); }
public sealed class EventConfig
{
    public string status { get; set; } = "DRAFT";
    public bool enabled { get; set; }
    public EventDef[] events { get; set; } = Array.Empty<EventDef>();
    public OnlineGift[] online { get; set; } = Array.Empty<OnlineGift>();
    public LevelGift[] levelGifts { get; set; } = Array.Empty<LevelGift>();

    public static EventConfig Load(string path, ItemConfig items)
    {
        var cfg = JsonSerializer.Deserialize<EventConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty event config");
        if (cfg.enabled && cfg.status.Contains("DRAFT") && !cfg.status.Contains("DEV"))
            throw new InvalidDataException("events: a DRAFT reward config may not be enabled (owner approval first)");
        IEnumerable<RewardItem> all = cfg.events.SelectMany(e => e.tasks).SelectMany(t => t.rewardItems)
            .Concat(cfg.online.SelectMany(o => o.rewardItems)).Concat(cfg.levelGifts.SelectMany(l => l.rewardItems));
        foreach (RewardItem r in all) Rewards.Check(items, r);
        return cfg;
    }
}

/// <summary>Reward payout shared by events, welfare, mail and giftcodes: Gold + common items only (never Diamonds,
/// never gear), the bag must fit everything first, and the claim key makes it pay once.</summary>
public static class Rewards
{
    public static readonly string[] AllowedKinds = { "consumable", "petfood", "ticket" };

    public static void Check(ItemConfig items, RewardItem r)
    {
        ItemDef d = items.Get(r.itemId) ?? throw new InvalidDataException("reward: unknown item " + r.itemId);
        if (!AllowedKinds.Contains(d.kind) || r.qty < 1 || r.qty > 99) throw new InvalidDataException("reward: not allowed " + r.itemId);
    }

    /// <summary>Reward items with their display name and icon key (for icons in the windows).</summary>
    public static RewardViewDto[] View(ItemConfig items, IEnumerable<RewardItem> list) =>
        list.Select(i => { ItemDef? d = items.Get(i.itemId); return new RewardViewDto { itemId = i.itemId, name = d?.name ?? i.itemId, icon = d?.icon ?? "", qty = i.qty }; }).ToArray();

    public static string Text(ItemConfig items, long gold, IEnumerable<RewardItem> list)
    {
        var parts = new List<string>();
        if (gold > 0) parts.Add(gold + " Vàng");
        parts.AddRange(list.Select(i => $"{items.Get(i.itemId)?.name} x{i.qty}"));
        return string.Join(", ", parts);
    }

    /// <summary>Inside the caller's transaction: claim row first (PK = once), then bag check, Gold, items.</summary>
    public static void Pay(SqliteConnection c, SqliteTransaction tx, InventoryService inv, string accountId, string characterId, string claimKey, long gold, RewardItem[] items, long now)
    {
        int inserted = Database.Exec(c, tx, "INSERT OR IGNORE INTO reward_claims(character_id, claim_key, claimed_ms) VALUES($c,$k,$t)", ("$c", characterId), ("$k", claimKey), ("$t", now));
        if (inserted == 0) throw new GameError("already_claimed", "Đã nhận phần thưởng này.", 409);
        foreach (RewardItem i in items)
            if (!inv.Fits(c, tx, characterId, i.itemId, i.qty)) throw new GameError("bag_full", "Túi đồ không đủ chỗ cho phần thưởng.", 409);
        if (gold > 0) Wallet.Gold(c, tx, accountId, characterId, gold, "reward:" + claimKey, "reward:" + characterId + ":" + claimKey);
        foreach (RewardItem i in items) inv.Add(c, tx, characterId, i.itemId, i.qty);
        AccountService.Audit(c, tx, accountId, "reward", $"{characterId} {claimKey} +{gold}gold {items.Length} items");
    }

    public static bool Claimed(SqliteConnection c, SqliteTransaction? tx, string characterId, string key) =>
        Database.Scalar<string>(c, tx, "SELECT claim_key FROM reward_claims WHERE character_id=$c AND claim_key=$k", ("$c", characterId), ("$k", key)) != null;

    public static void Own(SqliteConnection c, SqliteTransaction? tx, string accountId, string characterId)
    {
        if (Database.Scalar<string>(c, tx, "SELECT id FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId)) == null)
            throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
    }
}

// ---------------------------------------------------------------- events + welfare
public sealed class RewardViewDto { public string itemId { get; set; } = ""; public string name { get; set; } = ""; public string icon { get; set; } = ""; public int qty { get; set; } }
public sealed class RewardStepDto { public string key { get; set; } = ""; public string text { get; set; } = ""; public int have { get; set; } public int need { get; set; } public string reward { get; set; } = ""; public bool claimable { get; set; } public bool claimed { get; set; } public long gold { get; set; } public RewardViewDto[] items { get; set; } = Array.Empty<RewardViewDto>(); }
public sealed class WelfareTabDto { public string id { get; set; } = ""; public string title { get; set; } = ""; public string banner { get; set; } = ""; public string desc { get; set; } = ""; public string period { get; set; } = ""; public bool locked { get; set; } public string lockedReason { get; set; } = ""; public RewardStepDto[] steps { get; set; } = Array.Empty<RewardStepDto>(); }
public sealed class WelfareDto { public bool rewardsEnabled { get; set; } public string status { get; set; } = ""; public WelfareTabDto[] tabs { get; set; } = Array.Empty<WelfareTabDto>(); public bool anyClaimable { get; set; } public long gold { get; set; } }
public sealed class WelfareClaimDto { public string? key { get; set; } }

/// <summary>
/// "Sự Kiện / Phúc Lợi" window data, all server-side: Sự Kiện Mới (Săn Nấm Lễ Hội: tasks counted from server events
/// inside the event dates), Đăng Nhập (the 7-day check-in has its own panel), Nạp Đầu (locked: payments are off),
/// Phúc Lợi Online (minutes online today, from the client's 30 s heartbeat — gaps over 40 s do not count), Quà Tân Thủ
/// (the starter kit, granted once at the first bag load), Quà Lên Cấp (level milestones). Rewards pay once each and
/// only when the owner-approved config is enabled — production ships the DRAFT disabled.
/// </summary>
public sealed class EventService
{
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<long> now;
    private readonly Dictionary<string, long> lastPing = new();
    private readonly object gate = new();
    public EventConfig Config { get; }
    public const long MaxPingGapMs = 40_000;

    public EventService(Database db, EventConfig config, InventoryService inv, Func<long>? clock = null)
    {
        this.db = db; Config = config; this.inv = inv; now = clock ?? Database.NowMs;
    }

    public string VnDay() => DateTimeOffset.FromUnixTimeMilliseconds(now()).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
    private bool Running(EventDef e) { string d = VnDay(); return string.CompareOrdinal(d, e.startVn) >= 0 && string.CompareOrdinal(d, e.endVn) <= 0; }

    /// <summary>Server event feed (same events as quests).</summary>
    public void Record(string accountId, string characterId, string ev, int amount)
    {
        foreach (EventDef e in Config.events.Where(Running))
        {
            if (!e.tasks.Any(t => t.@event == ev)) continue;
            db.InTransaction((c, tx) =>
            {
                string json = Database.Scalar<string>(c, tx, "SELECT counts_json FROM event_progress WHERE character_id=$c AND event_id=$e", ("$c", characterId), ("$e", e.id)) ?? "{}";
                var counts = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
                int need = e.tasks.Where(t => t.@event == ev).Max(t => t.need);
                counts[ev] = Math.Min(need, counts.GetValueOrDefault(ev) + amount);
                Database.Exec(c, tx, "INSERT INTO event_progress(character_id, event_id, counts_json) VALUES($c,$e,$j) ON CONFLICT(character_id, event_id) DO UPDATE SET counts_json=excluded.counts_json",
                    ("$c", characterId), ("$e", e.id), ("$j", JsonSerializer.Serialize(counts)));
                return 0;
            });
        }
    }

    /// <summary>Online heartbeat (~every 30 s while in the world): adds the time since the previous ping, capped.</summary>
    public void Ping(string accountId, string characterId)
    {
        long t = now(), dt;
        lock (gate)
        {
            dt = lastPing.TryGetValue(characterId, out long last) ? Math.Clamp(t - last, 0, MaxPingGapMs) : 0;
            lastPing[characterId] = t;
        }
        db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            if (dt > 0)
                Database.Exec(c, tx, "INSERT INTO online_time(character_id, vn_day, ms) VALUES($c,$d,$m) ON CONFLICT(character_id, vn_day) DO UPDATE SET ms=ms+excluded.ms",
                    ("$c", characterId), ("$d", VnDay()), ("$m", dt));
            return 0;
        });
    }

    public WelfareDto Get(string accountId, string characterId) => db.InTransaction((c, tx) =>
    {
        Rewards.Own(c, tx, accountId, characterId);
        int level = Database.Scalar<int>(c, tx, "SELECT level FROM characters WHERE id=$c", ("$c", characterId));
        long onlineMs = Database.Scalar<long>(c, tx, "SELECT ms FROM online_time WHERE character_id=$c AND vn_day=$d", ("$c", characterId), ("$d", VnDay()));
        bool kit = Database.Scalar<string>(c, tx, "SELECT grant_key FROM item_grants WHERE character_id=$c AND grant_key='starter_kit_v1'", ("$c", characterId)) != null;
        var tabs = new List<WelfareTabDto>();
        foreach (EventDef e in Config.events)
        {
            var counts = JsonSerializer.Deserialize<Dictionary<string, int>>(
                Database.Scalar<string>(c, tx, "SELECT counts_json FROM event_progress WHERE character_id=$c AND event_id=$e", ("$c", characterId), ("$e", e.id)) ?? "{}") ?? new();
            bool running = Running(e);
            tabs.Add(new WelfareTabDto
            {
                id = "event:" + e.id, title = e.title, banner = e.banner, desc = e.desc, period = $"{e.startVn} → {e.endVn}",
                locked = !running, lockedReason = running ? "" : "Sự kiện chưa diễn ra hoặc đã kết thúc.",
                steps = e.tasks.Select((t, i) => Step(c, tx, characterId, $"event:{e.id}:{i}", t.text, counts.GetValueOrDefault(t.@event), t.need, Rewards.Text(inv.Config, t.rewardGold, t.rewardItems), running)).ToArray(),
            });
        }
        tabs.Add(new WelfareTabDto { id = "login", title = "Đăng Nhập", desc = "Điểm danh 7 ngày: mở bảng Điểm Danh trên HUD.", steps = Array.Empty<RewardStepDto>() });
        tabs.Add(new WelfareTabDto { id = "firsttopup", title = "Nạp Đầu", desc = "Quà nạp lần đầu.", locked = true, lockedReason = "Thanh toán thật chưa được kích hoạt (chờ chủ dự án phê duyệt). Không có giao dịch nào." });
        tabs.Add(new WelfareTabDto
        {
            id = "online", title = "Phúc Lợi Online", desc = "Online càng lâu trong ngày, quà càng nhiều. Làm mới lúc 00:00 giờ Việt Nam.", period = VnDay(),
            steps = Config.online.Select(o => Step(c, tx, characterId, $"online:{VnDay()}:{o.minutes}", $"Online {o.minutes} phút", (int)(onlineMs / 60_000), o.minutes, Rewards.Text(inv.Config, o.rewardGold, o.rewardItems), true)).ToArray(),
        });
        tabs.Add(new WelfareTabDto
        {
            id = "newbie", title = "Quà Tân Thủ", desc = "Gói tân thủ (thuốc, vũ khí đúng nghề, áo vải, bùa) — nhận tự động khi mở Túi đồ lần đầu.",
            steps = new[] { new RewardStepDto { key = "newbie", text = "Gói tân thủ", have = kit ? 1 : 0, need = 1, reward = "Thuốc HP/MP nhỏ x10, trang bị tập sự", claimed = kit } },
        });
        tabs.Add(new WelfareTabDto
        {
            id = "levelup", title = "Quà Lên Cấp", desc = "Đạt cấp để nhận quà.",
            steps = Config.levelGifts.Select(g => Step(c, tx, characterId, $"level:{g.level}", $"Đạt cấp {g.level}", Math.Min(level, g.level), g.level, Rewards.Text(inv.Config, g.rewardGold, g.rewardItems), true)).ToArray(),
        });
        return new WelfareDto { rewardsEnabled = Config.enabled, status = Config.status, tabs = tabs.ToArray(), anyClaimable = tabs.Any(t => t.steps.Any(s => s.claimable)),
            gold = Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c", ("$c", characterId)) };
    });

    private RewardStepDto Step(SqliteConnection c, SqliteTransaction tx, string characterId, string key, string text, int have, int need, string reward, bool open)
    {
        bool claimed = Rewards.Claimed(c, tx, characterId, key);
        (long gold, RewardItem[] items) = Lookup(key);
        return new RewardStepDto { key = key, text = text, have = Math.Min(have, need), need = need, reward = reward, claimed = claimed, claimable = Config.enabled && open && !claimed && have >= need,
            gold = gold, items = Rewards.View(inv.Config, items) };
    }

    public WelfareDto Claim(string accountId, string characterId, WelfareClaimDto? req)
    {
        string key = req?.key ?? "";
        if (!Config.enabled) throw new GameError("rewards_pending", "Phần thưởng đang chờ chủ dự án phê duyệt, chưa thể nhận.", 409);
        WelfareDto state = Get(accountId, characterId);
        RewardStepDto step = state.tabs.SelectMany(t => t.steps).FirstOrDefault(s => s.key == key) ?? throw new GameError("reward_unknown", "Phần thưởng không tồn tại.", 404);
        if (step.claimed) throw new GameError("already_claimed", "Đã nhận phần thưởng này.", 409);
        if (!step.claimable) throw new GameError("not_ready", "Chưa đủ điều kiện nhận.", 409);
        (long gold, RewardItem[] items) = Lookup(key);
        db.InTransaction((c, tx) => { Rewards.Pay(c, tx, inv, accountId, characterId, key, gold, items, now()); return 0; });
        return Get(accountId, characterId);
    }

    private (long, RewardItem[]) Lookup(string key)
    {
        string[] p = key.Split(':');
        if (p[0] == "event") { EventTask t = Config.events.First(e => e.id == p[1]).tasks[int.Parse(p[2])]; return (t.rewardGold, t.rewardItems); }
        if (p[0] == "online") { OnlineGift o = Config.online.First(x => x.minutes == int.Parse(p[2])); return (o.rewardGold, o.rewardItems); }
        if (p[0] == "level") { LevelGift g = Config.levelGifts.First(x => x.level == int.Parse(p[1])); return (g.rewardGold, g.rewardItems); }
        throw new GameError("reward_unknown", "Phần thưởng không tồn tại.", 404);
    }
}

// ---------------------------------------------------------------- mail
public sealed class MailDto { public string id { get; set; } = ""; public string kind { get; set; } = ""; public string title { get; set; } = ""; public string body { get; set; } = ""; public long attachGold { get; set; } public string attachText { get; set; } = ""; public bool hasAttachment { get; set; } public bool read { get; set; } public bool claimed { get; set; } public long createdMs { get; set; } public RewardViewDto[] items { get; set; } = Array.Empty<RewardViewDto>(); }
public sealed class MailboxDto { public MailDto[] mails { get; set; } = Array.Empty<MailDto>(); public int unread { get; set; } public int unclaimed { get; set; } public long gold { get; set; } }
public sealed class MailIdDto { public string? mailId { get; set; } }

/// <summary>
/// Mailbox: system / reward / notice (guild mail once guilds exist). Mails are created only by server code (welcome
/// notice on first open; reward mails from approved server features) — never by a client. Attachments (Gold + common
/// items) are claimed once (claimed_ms set inside the same transaction as the payout); reading marks it read.
/// </summary>
public sealed class MailService
{
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<long> now;
    public MailService(Database db, InventoryService inv, Func<long>? clock = null) { this.db = db; this.inv = inv; now = clock ?? Database.NowMs; }

    /// <summary>Server-side send (not exposed over HTTP). Attachments: Gold + common items only.</summary>
    public string Send(string characterId, string kind, string title, string body, long gold = 0, RewardItem[]? items = null)
    {
        items ??= Array.Empty<RewardItem>();
        foreach (RewardItem i in items) Rewards.Check(inv.Config, i);
        string id = Crypto.NewId("mail");
        db.InTransaction((c, tx) =>
        {
            Database.Exec(c, tx, "INSERT INTO mails(id, character_id, kind, title, body, attach_gold, attach_items, created_ms) VALUES($i,$c,$k,$t,$b,$g,$a,$m)",
                ("$i", id), ("$c", characterId), ("$k", kind), ("$t", title), ("$b", body), ("$g", gold), ("$a", JsonSerializer.Serialize(items)), ("$m", now()));
            return 0;
        });
        return id;
    }

    public MailboxDto List(string accountId, string characterId)
    {
        bool welcome = false;
        MailboxDto box = db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            welcome = Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM mails WHERE character_id=$c", ("$c", characterId)) == 0
                      && Database.Exec(c, tx, "INSERT OR IGNORE INTO item_grants(character_id, grant_key, granted_ms) VALUES($c,'welcome_mail',$t)", ("$c", characterId), ("$t", now())) == 1;
            return new MailboxDto();
        });
        if (welcome) Send(characterId, "notice", "Chào mừng tới Làng Chuông Gió!",
            "Chào lữ khách! Trưởng Làng và mọi người rất vui được gặp bạn. Hãy làm theo Nhiệm vụ chính bên trái màn hình, ghé Dược Sư Mộc Linh mua thuốc trước khi ra Đồng Cỏ Gió. Chúc bạn phiêu lưu vui vẻ!");
        return db.InTransaction((c, tx) =>
        {
            var list = new List<MailDto>();
            using (var cmd = Database.Command(c, tx, "SELECT id, kind, title, body, attach_gold, attach_items, read_ms, claimed_ms, created_ms FROM mails WHERE character_id=$c ORDER BY created_ms DESC LIMIT 100", ("$c", characterId)))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var items = JsonSerializer.Deserialize<RewardItem[]>(r.GetString(5)) ?? Array.Empty<RewardItem>();
                    long gold = r.GetInt64(4);
                    list.Add(new MailDto
                    {
                        id = r.GetString(0), kind = r.GetString(1), title = r.GetString(2), body = r.GetString(3), attachGold = gold,
                        attachText = Rewards.Text(inv.Config, gold, items), hasAttachment = gold > 0 || items.Length > 0, items = Rewards.View(inv.Config, items),
                        read = !r.IsDBNull(6), claimed = !r.IsDBNull(7), createdMs = r.GetInt64(8),
                    });
                }
            return new MailboxDto { mails = list.ToArray(), unread = list.Count(m => !m.read), unclaimed = list.Count(m => m.hasAttachment && !m.claimed),
                gold = Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c", ("$c", characterId)) };
        });
    }

    public MailboxDto Read(string accountId, string characterId, MailIdDto? req)
    {
        db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            if (Database.Exec(c, tx, "UPDATE mails SET read_ms=COALESCE(read_ms,$t) WHERE id=$i AND character_id=$c", ("$t", now()), ("$i", req?.mailId ?? ""), ("$c", characterId)) == 0)
                throw new GameError("mail_unknown", "Thư không tồn tại.", 404);
            return 0;
        });
        return List(accountId, characterId);
    }

    public MailboxDto Claim(string accountId, string characterId, MailIdDto? req)
    {
        db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            using var cmd = Database.Command(c, tx, "SELECT attach_gold, attach_items, claimed_ms FROM mails WHERE id=$i AND character_id=$c", ("$i", req?.mailId ?? ""), ("$c", characterId));
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new GameError("mail_unknown", "Thư không tồn tại.", 404);
            long gold = r.GetInt64(0);
            var items = JsonSerializer.Deserialize<RewardItem[]>(r.GetString(1)) ?? Array.Empty<RewardItem>();
            if (!r.IsDBNull(2)) throw new GameError("already_claimed", "Đã nhận vật phẩm trong thư.", 409);
            if (gold <= 0 && items.Length == 0) throw new GameError("no_attachment", "Thư không có vật phẩm đính kèm.", 409);
            r.Close();
            if (Database.Exec(c, tx, "UPDATE mails SET claimed_ms=$t, read_ms=COALESCE(read_ms,$t) WHERE id=$i AND claimed_ms IS NULL", ("$t", now()), ("$i", req!.mailId)) == 0)
                throw new GameError("already_claimed", "Đã nhận vật phẩm trong thư.", 409);
            Rewards.Pay(c, tx, inv, accountId, characterId, "mail:" + req.mailId, gold, items, now());
            return 0;
        });
        return List(accountId, characterId);
    }
}

// ---------------------------------------------------------------- giftcode
public sealed class GiftcodeDto { public string? code { get; set; } }
public sealed class GiftcodeResultDto { public string code { get; set; } = ""; public long gold { get; set; } public string reward { get; set; } = ""; public long balance { get; set; } }

/// <summary>
/// Giftcodes: created only by server code / operators (no HTTP endpoint creates codes; production has none until the
/// owner approves a campaign). Redeem checks: the code exists, not expired, uses left, this ACCOUNT has not used it,
/// the character's level; then pays (Gold + common items) in one transaction — the use row's primary key
/// (code, account) makes a double redeem impossible. Codes are case-insensitive, trimmed, A-Z0-9 only.
/// </summary>
public sealed class GiftcodeService
{
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<long> now;
    private readonly AttemptLimiter limiter = new(20, TimeSpan.FromMinutes(10));
    public GiftcodeService(Database db, InventoryService inv, Func<long>? clock = null) { this.db = db; this.inv = inv; now = clock ?? Database.NowMs; }

    public static string Normalize(string? code) => new string((code ?? "").Trim().ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    /// <summary>Operator tool (server code only).</summary>
    public void Create(string code, long gold, RewardItem[] items, int maxUses, int minLevel, long expiresMs, string note = "")
    {
        foreach (RewardItem i in items) Rewards.Check(inv.Config, i);
        string c0 = Normalize(code);
        if (c0.Length < 4 || c0.Length > 24) throw new GameError("bad_code", "Mã không hợp lệ.");
        db.InTransaction((c, tx) =>
        {
            Database.Exec(c, tx, "INSERT INTO giftcodes(code, reward_gold, reward_items, max_uses, min_level, expires_ms, note) VALUES($c,$g,$i,$m,$l,$e,$n)",
                ("$c", c0), ("$g", gold), ("$i", JsonSerializer.Serialize(items)), ("$m", maxUses), ("$l", minLevel), ("$e", expiresMs), ("$n", note));
            return 0;
        });
    }

    public GiftcodeResultDto Redeem(string accountId, string characterId, GiftcodeDto? req)
    {
        if (!limiter.TryAcquire(accountId)) throw new GameError("too_many_attempts", "Nhập sai quá nhiều lần, hãy thử lại sau.", 429);
        string code = Normalize(req?.code);
        if (code.Length == 0) throw new GameError("bad_code", "Hãy nhập giftcode.");
        return db.InTransaction((c, tx) =>
        {
            Rewards.Own(c, tx, accountId, characterId);
            using var cmd = Database.Command(c, tx, "SELECT reward_gold, reward_items, max_uses, used, min_level, expires_ms FROM giftcodes WHERE code=$c", ("$c", code));
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new GameError("code_unknown", "Giftcode không tồn tại.", 404);
            long gold = r.GetInt64(0); var items = JsonSerializer.Deserialize<RewardItem[]>(r.GetString(1)) ?? Array.Empty<RewardItem>();
            int max = r.GetInt32(2), used = r.GetInt32(3), minLevel = r.GetInt32(4); long expires = r.GetInt64(5);
            r.Close();
            if (now() >= expires) throw new GameError("code_expired", "Giftcode đã hết hạn.", 409);
            if (used >= max) throw new GameError("code_used_up", "Giftcode đã hết lượt sử dụng.", 409);
            int level = Database.Scalar<int>(c, tx, "SELECT level FROM characters WHERE id=$c", ("$c", characterId));
            if (level < minLevel) throw new GameError("level_too_low", $"Cần đạt cấp {minLevel} để dùng giftcode này.", 409);
            if (Database.Exec(c, tx, "INSERT OR IGNORE INTO giftcode_uses(code, account_id, character_id, used_ms) VALUES($c,$a,$ch,$t)", ("$c", code), ("$a", accountId), ("$ch", characterId), ("$t", now())) == 0)
                throw new GameError("code_already_used", "Tài khoản này đã dùng giftcode này rồi.", 409);
            if (Database.Exec(c, tx, "UPDATE giftcodes SET used=used+1 WHERE code=$c AND used < max_uses", ("$c", code)) == 0)
                throw new GameError("code_used_up", "Giftcode đã hết lượt sử dụng.", 409);
            Rewards.Pay(c, tx, inv, accountId, characterId, "giftcode:" + code, gold, items, now());
            long balance = Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c", ("$c", characterId));
            return new GiftcodeResultDto { code = code, gold = gold, reward = Rewards.Text(inv.Config, gold, items), balance = balance };
        });
    }
}
