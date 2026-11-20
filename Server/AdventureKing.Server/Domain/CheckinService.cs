using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

// ---------------------------------------------------------------- config (shape of Docs/ReferenceConfigs/AdventureKing_Welfare_Config_Example.json)
public sealed class RewardDef
{
    public string type { get; set; } = "";          // gold | diamonds | bundle | cosmetic_choice
    public long amount { get; set; }
    public string? bundleId { get; set; }
    public string[] eligibleItemIds { get; set; } = Array.Empty<string>();
}
public sealed class CheckinDayDef { public int day { get; set; } public string icon { get; set; } = ""; public bool milestone { get; set; } public RewardDef[] rewards { get; set; } = Array.Empty<RewardDef>(); }
public sealed class BundleDef { public string bundleId { get; set; } = ""; public RewardDef[] contents { get; set; } = Array.Empty<RewardDef>(); }
public sealed class DailyCheckinDef
{
    public string id { get; set; } = "";
    public bool enabled { get; set; }
    public CheckinDayDef[] days { get; set; } = Array.Empty<CheckinDayDef>();
    public BundleDef[] bundles { get; set; } = Array.Empty<BundleDef>();
}
public sealed class WelfareConfig
{
    public string catalogId { get; set; } = "";
    public string status { get; set; } = "";
    public DailyCheckinDef dailyCheckin { get; set; } = new();

    public static WelfareConfig Load(string path)
    {
        var c = JsonSerializer.Deserialize<WelfareConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty welfare config");
        c.Validate();
        return c;
    }

    public void Validate()
    {
        var d = dailyCheckin;
        if (d.days.Length != 7 || d.days.Select(x => x.day).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 7)) == false)
            throw new InvalidDataException("check-in needs days 1..7");
        foreach (RewardDef r in d.days.SelectMany(x => x.rewards).Concat(d.bundles.SelectMany(b => b.contents)))
        {
            if (r.type is not ("gold" or "diamonds" or "bundle" or "cosmetic_choice")) throw new InvalidDataException("unknown reward " + r.type);
            if (r.type is "gold" or "diamonds" && r.amount <= 0) throw new InvalidDataException("reward amount must be > 0");
            if (r.type == "bundle" && d.bundles.All(b => b.bundleId != r.bundleId)) throw new InvalidDataException("unknown bundle " + r.bundleId);
            if (r.type == "cosmetic_choice" && r.eligibleItemIds.Length == 0) throw new InvalidDataException("choice without items");
        }
        // an enabled LIVE config must not still be the owner's unapproved draft
        if (d.enabled && status.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase) && !status.Contains("DEV", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("check-in rewards are a DRAFT pending owner approval: keep enabled=false (a DEV config may enable it for tests)");
    }
}

// ---------------------------------------------------------------- DTOs
public sealed class RewardDto
{
    public string type { get; set; } = ""; public long amount { get; set; } public string label { get; set; } = "";
    public string[] choices { get; set; } = Array.Empty<string>(); public string[] choiceNames { get; set; } = Array.Empty<string>();
}
public sealed class CheckinDayDto { public int day { get; set; } public string icon { get; set; } = ""; public bool milestone { get; set; } public string state { get; set; } = "locked"; public RewardDto[] rewards { get; set; } = Array.Empty<RewardDto>(); }
public sealed class CheckinDto
{
    public bool enabled { get; set; }
    public string status { get; set; } = "";
    public string message { get; set; } = "";
    public int cycle { get; set; }
    public int claimedDays { get; set; }
    public bool canClaimToday { get; set; }
    public string todayVn { get; set; } = "";
    public CheckinDayDto[] days { get; set; } = Array.Empty<CheckinDayDto>();
    public long gold { get; set; }
    public long diamonds { get; set; }
    public RewardDto[] granted { get; set; } = Array.Empty<RewardDto>();
}
public sealed class CheckinClaimDto { public string? choiceItemId { get; set; } }

/// <summary>
/// 7-day check-in (account scope), all decided on the server:
///  • at most one claim per Vietnam calendar day (UTC+7, server clock — the device clock is never used);
///  • a missed day pauses the cycle (no reset); after day 7 the next cycle starts on a later VN day;
///  • rewards are paid in ONE transaction through the ledger (idempotency keys per cycle/day/reward) and a
///    receipt row whose primary key makes a double claim impossible, even for concurrent requests;
///  • while the owner has not approved the rewards the config stays enabled=false: the panel shows the plan
///    honestly and claims are refused (no fake rewards).
/// </summary>
public sealed class CheckinService
{
    private readonly Database db;
    private readonly CosmeticCatalog cosmetics;
    private readonly Func<long> now;
    public WelfareConfig Config { get; }

    public CheckinService(Database db, WelfareConfig config, CosmeticCatalog cosmetics, Func<long>? clock = null)
    {
        this.db = db; Config = config; this.cosmetics = cosmetics; now = clock ?? Database.NowMs;
    }

    private string VnDay() => DateTimeOffset.FromUnixTimeMilliseconds(now()).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");

    private string Label(RewardDef r) => r.type switch
    {
        "gold" => $"{r.amount:N0} Vàng".Replace(',', '.'),
        "diamonds" => $"{r.amount} Kim Cương",
        "bundle" => "Rương: " + string.Join(" + ", Config.dailyCheckin.bundles.First(b => b.bundleId == r.bundleId).contents.Select(Label)),
        "cosmetic_choice" => "Chọn 1 món thời trang",
        _ => r.type,
    };

    private RewardDto Dto(RewardDef r) => new()
    {
        type = r.type, amount = r.amount, label = Label(r), choices = r.eligibleItemIds,
        choiceNames = r.eligibleItemIds.Select(id => cosmetics.Find(id)?.name ?? id).ToArray(), // names from the server catalog
    };

    private static (int cycle, int claimed, string last) Progress(SqliteConnection c, SqliteTransaction? tx, string accountId)
    {
        using var q = Database.Command(c, tx, "SELECT cycle, claimed_days, last_vn_day FROM checkin_progress WHERE account_id=$a", ("$a", accountId));
        using var r = q.ExecuteReader();
        return r.Read() ? (r.GetInt32(0), r.GetInt32(1), r.GetString(2)) : (1, 0, "");
    }

    private static void OwnCharacter(SqliteConnection c, SqliteTransaction? tx, string accountId, string characterId)
    {
        if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId)) == 0)
            throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
    }

    private CheckinDto Build(SqliteConnection c, SqliteTransaction? tx, string accountId, string characterId)
    {
        var (cycle, claimed, last) = Progress(c, tx, accountId);
        string today = VnDay();
        bool claimedToday = last == today;
        // a finished cycle rolls over on a later VN day
        if (claimed >= 7 && !claimedToday) { cycle++; claimed = 0; }
        bool can = Config.dailyCheckin.enabled && !claimedToday && claimed < 7;
        var dto = new CheckinDto
        {
            enabled = Config.dailyCheckin.enabled, status = Config.status, cycle = cycle, claimedDays = claimed, canClaimToday = can, todayVn = today,
            message = !Config.dailyCheckin.enabled ? "Điểm danh chưa mở: phần thưởng đang chờ chủ dự án duyệt."
                : claimedToday ? "Hôm nay bạn đã điểm danh. Quay lại vào ngày mai (giờ Việt Nam)." : "Có quà điểm danh hôm nay!",
            days = Config.dailyCheckin.days.OrderBy(d => d.day).Select(d => new CheckinDayDto
            {
                day = d.day, icon = d.icon, milestone = d.milestone, rewards = d.rewards.Select(Dto).ToArray(),
                state = d.day <= claimed ? "claimed" : d.day == claimed + 1 && can ? "claimable" : "locked",
            }).ToArray(),
            gold = Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c", ("$c", characterId)),
            diamonds = Database.Scalar<long>(c, tx, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", accountId)),
        };
        return dto;
    }

    public CheckinDto Status(string accountId, string characterId)
    {
        using var c = db.Open();
        OwnCharacter(c, null, accountId, characterId);
        return Build(c, null, accountId, characterId);
    }

    public CheckinDto Claim(string accountId, string characterId, CheckinClaimDto? req)
    {
        if (!Config.dailyCheckin.enabled) throw new GameError("checkin_disabled", "Điểm danh chưa mở: phần thưởng đang chờ chủ dự án duyệt.", 409);
        try
        {
            return db.InTransaction((c, tx) =>
            {
                OwnCharacter(c, tx, accountId, characterId);
                var (cycle, claimed, last) = Progress(c, tx, accountId);
                string today = VnDay();
                if (last == today) throw new GameError("already_claimed_today", "Hôm nay bạn đã điểm danh rồi.", 409);
                if (claimed >= 7) { cycle++; claimed = 0; }
                int day = claimed + 1;
                CheckinDayDef def = Config.dailyCheckin.days.First(d => d.day == day);
                var granted = new List<RewardDto>();
                int n = 0;
                void Grant(RewardDef r)
                {
                    string key = $"checkin:{accountId}:{cycle}:{day}:{n++}";
                    switch (r.type)
                    {
                        case "gold": Wallet.Gold(c, tx, accountId, characterId, r.amount, "checkin", key); granted.Add(Dto(r)); break;
                        case "diamonds": Wallet.Diamonds(c, tx, accountId, r.amount, "checkin", key); granted.Add(Dto(r)); break;
                        case "bundle": foreach (RewardDef x in Config.dailyCheckin.bundles.First(b => b.bundleId == r.bundleId).contents) Grant(x); break;
                        case "cosmetic_choice":
                        {
                            string? pick = req?.choiceItemId;
                            if (pick == null || Array.IndexOf(r.eligibleItemIds, pick) < 0)
                                throw new GameError("choice_required", "Hãy chọn một món thời trang trong danh sách quà ngày 7.", 409);
                            CatalogItem item = cosmetics.Find(pick) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
                            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM owned_items WHERE account_id=$a AND IFNULL(character_id,'')=$c AND item_id=$i",
                                    ("$a", accountId), ("$c", item.kind == ItemKinds.CosmeticSet ? "" : characterId), ("$i", pick)) > 0)
                                throw new GameError("already_owned", "Bạn đã có món này, hãy chọn món khác.", 409);
                            Database.Exec(c, tx, """
                                INSERT INTO owned_items(id, account_id, character_id, item_id, kind, quantity, source, acquired_ms)
                                VALUES($id,$a,$c,$i,$k,1,$s,$t)
                                """, ("$id", Crypto.NewId("own")), ("$a", accountId), ("$c", item.kind == ItemKinds.CosmeticSet ? null : characterId),
                                ("$i", pick), ("$k", item.kind), ("$s", $"checkin:{cycle}:{day}"), ("$t", now()));
                            granted.Add(new RewardDto { type = "cosmetic", amount = 1, label = item.name });
                            break;
                        }
                    }
                }
                foreach (RewardDef r in def.rewards) Grant(r);
                Database.Exec(c, tx, """
                    INSERT INTO checkin_claims(account_id, cycle, day, character_id, vn_day, catalog_id, reward_json, claimed_ms)
                    VALUES($a,$cy,$d,$c,$v,$cat,$r,$t)
                    """, ("$a", accountId), ("$cy", cycle), ("$d", day), ("$c", characterId), ("$v", today), ("$cat", Config.catalogId),
                    ("$r", JsonSerializer.Serialize(granted)), ("$t", now()));
                Database.Exec(c, tx, """
                    INSERT INTO checkin_progress(account_id, cycle, claimed_days, last_vn_day) VALUES($a,$cy,$d,$v)
                    ON CONFLICT(account_id) DO UPDATE SET cycle=$cy, claimed_days=$d, last_vn_day=$v
                    """, ("$a", accountId), ("$cy", cycle), ("$d", day), ("$v", today));
                AccountService.Audit(c, tx, accountId, "checkin", $"{characterId} cycle {cycle} day {day}");
                CheckinDto dto = Build(c, tx, accountId, characterId);
                dto.granted = granted.ToArray();
                return dto;
            });
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 19) // constraint: a concurrent claim of the same day won
        {
            throw new GameError("already_claimed_today", "Hôm nay bạn đã điểm danh rồi.", 409);
        }
    }
}
