using AdventureKing.Server.Data;
using AdventureKing.Server.Security;

namespace AdventureKing.Server.Domain;

public enum PackageKind { Starter, MonthCard, Pack }

/// <summary>Server-owned catalog row (Part 11). The client only ever displays these values.</summary>
public sealed record StorePackage(
    string Id, string Name, PackageKind Kind, long PriceVnd, int BaseDiamonds, int BonusDiamonds,
    int FirstPurchaseExtra = 0, bool OncePerAccount = false, string? RewardItemId = null,
    int MonthCardDays = 0, int MonthCardDaily = 0)
{
    public int Diamonds => BaseDiamonds + BonusDiamonds;
}

public sealed class StorePackageDto
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string kind { get; set; } = "";
    public long priceVnd { get; set; }
    public int diamonds { get; set; }
    public int baseDiamonds { get; set; }
    public int bonusDiamonds { get; set; }
    public int firstPurchaseDiamonds { get; set; }
    public bool firstPurchaseAvailable { get; set; }
    public string[] tags { get; set; } = Array.Empty<string>();
    public string rewardSummary { get; set; } = "";
    public bool purchasable { get; set; }
    public string unavailableReason { get; set; } = "";
}

public sealed class MonthCardStatusDto
{
    public bool active { get; set; }
    public int daysRemaining { get; set; }
    public bool claimableToday { get; set; }
    public int dailyDiamonds { get; set; }
    public string[] claimedDays { get; set; } = Array.Empty<string>();
}

public sealed class StoreOrderDto
{
    public string orderId { get; set; } = "";
    public string packageId { get; set; } = "";
    public long priceVnd { get; set; }
    public int diamondsGranted { get; set; }
    public bool firstPurchaseBonus { get; set; }
    public string status { get; set; } = "";
    public long createdMs { get; set; }
}

/// <summary>
/// Diamond store. REAL-MONEY PAYMENTS ARE DISABLED: no payment provider is integrated or approved, so the client
/// purchase endpoint always refuses. <see cref="GrantVerifiedPurchase"/> is the single entry point a future,
/// owner-approved payment verifier (Google Play / App Store / PC gateway receipt check) will call after it has
/// verified the receipt server-to-server. It enforces catalog price, once-only packages, first-purchase bonus,
/// month-card state and provider-transaction uniqueness so a receipt can never be granted twice.
/// </summary>
public sealed class DiamondStore
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static readonly IReadOnlyList<StorePackage> Catalog = new[]
    {
        new StorePackage("starter_pack", "Gói Tân Thủ", PackageKind.Starter, 9_000, 10, 0, OncePerAccount: true,
            RewardItemId: null /* rare item must be configured by the owner before this package can be sold */),
        new StorePackage("month_card", "Thẻ Tháng", PackageKind.MonthCard, 49_000, 15, 0, MonthCardDays: 30, MonthCardDaily: 3),
        new StorePackage("pouch_1", "Túi KC 1", PackageKind.Pack, 10_000, 10, 0, FirstPurchaseExtra: 10),
        new StorePackage("pouch_2", "Túi KC 2", PackageKind.Pack, 20_000, 20, 2),
        new StorePackage("pouch_3", "Túi KC 3", PackageKind.Pack, 50_000, 50, 8),
        new StorePackage("chest_1", "Rương KC 1", PackageKind.Pack, 99_000, 100, 25),
        new StorePackage("chest_2", "Rương KC 2", PackageKind.Pack, 199_000, 200, 60),
        new StorePackage("vault", "Kho KC", PackageKind.Pack, 499_000, 500, 200),
    };

    private readonly Database db;
    private readonly long[] vipThresholdsVnd;
    private readonly Func<DateTimeOffset> clock;

    /// <param name="vipThresholdsVnd">Cumulative verified VND for VIP 1..10; empty = not configured yet (owner decides).</param>
    public DiamondStore(Database db, long[]? vipThresholdsVnd = null, Func<DateTimeOffset>? clock = null)
    {
        this.db = db;
        this.vipThresholdsVnd = vipThresholdsVnd ?? Array.Empty<long>();
        if (this.vipThresholdsVnd.Length is not (0 or 10) ||
            this.vipThresholdsVnd.Zip(this.vipThresholdsVnd.Skip(1)).Any(p => p.Second <= p.First) ||
            this.vipThresholdsVnd.Any(v => v <= 0))
            throw new ArgumentException("VIP thresholds must be 10 strictly increasing positive VND values (or none).");
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public static bool PaymentsEnabled => false; // flip only after the owner approves a real, compliant integration

    public static StorePackage Get(string id) =>
        Catalog.FirstOrDefault(p => p.Id == id) ?? throw new GameError("package_unknown", "Gói không tồn tại.", 404);

    public string TodayVn() => clock().ToOffset(VietnamOffset).ToString("yyyy-MM-dd");

    /// <summary>Tags are derived from the numbers, never hand-written, so "+40%" or "Lần đầu x2" are always true.</summary>
    public static string[] Tags(StorePackage p, bool firstAvailable)
    {
        var tags = new List<string>();
        if (p.BonusDiamonds > 0) tags.Add($"+{p.BonusDiamonds * 100 / p.BaseDiamonds}%");
        if (p.FirstPurchaseExtra > 0 && firstAvailable && p.FirstPurchaseExtra == p.Diamonds) tags.Add("Lần đầu x2");
        if (p.OncePerAccount) tags.Add("1 lần / tài khoản");
        if (p.Kind == PackageKind.MonthCard) tags.Add($"{p.MonthCardDays} ngày");
        return tags.ToArray();
    }

    public StorePackageDto[] CatalogFor(string accountId)
    {
        using var c = db.Open();
        return Catalog.Select(p =>
        {
            long bought = Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM store_orders WHERE account_id=$a AND package_id=$p AND status='granted'",
                ("$a", accountId), ("$p", p.Id));
            bool first = p.FirstPurchaseExtra > 0 && bought == 0;
            string reason = Unavailable(c, null, accountId, p, bought);
            return new StorePackageDto
            {
                id = p.Id, name = p.Name, kind = p.Kind.ToString(), priceVnd = p.PriceVnd, diamonds = p.Diamonds,
                baseDiamonds = p.BaseDiamonds, bonusDiamonds = p.BonusDiamonds,
                firstPurchaseDiamonds = p.FirstPurchaseExtra > 0 ? p.Diamonds + p.FirstPurchaseExtra : 0,
                firstPurchaseAvailable = first, tags = Tags(p, first), rewardSummary = Summary(p, first),
                purchasable = PaymentsEnabled && reason.Length == 0,
                unavailableReason = !PaymentsEnabled ? "Thanh toán thật chưa được kích hoạt." : reason,
            };
        }).ToArray();
    }

    private static string Summary(StorePackage p, bool first) => p.Kind switch
    {
        PackageKind.Starter => $"{p.Diamonds} KC + 1 vật phẩm hiếm",
        PackageKind.MonthCard => $"{p.Diamonds} KC ngay + {p.MonthCardDaily} KC/ngày × {p.MonthCardDays} ngày (tối đa {p.Diamonds + p.MonthCardDaily * p.MonthCardDays} KC)",
        _ when first && p.FirstPurchaseExtra > 0 => $"{p.Diamonds} KC — lần đầu nhận {p.Diamonds + p.FirstPurchaseExtra} KC",
        _ when p.BonusDiamonds > 0 => $"{p.BaseDiamonds} KC + {p.BonusDiamonds} KC thưởng = {p.Diamonds} KC",
        _ => $"{p.Diamonds} KC",
    };

    private string Unavailable(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string accountId, StorePackage p, long bought)
    {
        if (p.Kind == PackageKind.Starter && p.RewardItemId == null) return "Vật phẩm hiếm tặng kèm chưa được cấu hình.";
        if (p.OncePerAccount && bought > 0) return "Đã mua (chỉ 1 lần mỗi tài khoản).";
        if (p.Kind == PackageKind.MonthCard && MonthCard(c, tx, accountId).active) return "Thẻ Tháng đang còn hiệu lực.";
        return "";
    }

    /// <summary>Client-facing purchase request. Always refused until real payments are approved and integrated.</summary>
    public void RequestPurchase(string accountId, string packageId)
    {
        Get(packageId);
        throw new GameError("payments_disabled", "Thanh toán thật chưa được kích hoạt. Chưa có giao dịch nào được thực hiện.", 403);
    }

    /// <summary>
    /// Grants a purchase whose payment a server-side verifier has ALREADY confirmed with the provider.
    /// Idempotent per (provider, providerTxnId).
    /// </summary>
    public StoreOrderDto GrantVerifiedPurchase(string accountId, string packageId, string provider, string providerTxnId, long paidVnd)
    {
        StorePackage p = Get(packageId);
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerTxnId))
            throw new GameError("receipt_invalid", "Thiếu mã giao dịch thanh toán.");
        return db.InTransaction((c, tx) =>
        {
            using (var cmd = Database.Command(c, tx,
                       "SELECT id, account_id, package_id, price_vnd, diamonds_granted, first_purchase_bonus, status, created_ms FROM store_orders WHERE provider=$p AND provider_txn_id=$t",
                       ("$p", provider), ("$t", providerTxnId)))
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    if (r.GetString(1) != accountId || r.GetString(2) != packageId)
                        throw new GameError("receipt_reused", "Biên lai đã được dùng cho giao dịch khác.", 409);
                    return OrderDto(r); // replay: same result, nothing granted again
                }

            if (paidVnd != p.PriceVnd) throw new GameError("price_mismatch", "Số tiền thanh toán không khớp giá gói.", 409);
            long bought = Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM store_orders WHERE account_id=$a AND package_id=$p AND status='granted'",
                ("$a", accountId), ("$p", p.Id));
            string reason = Unavailable(c, tx, accountId, p, bought);
            if (reason.Length > 0) throw new GameError("package_unavailable", reason, 409);

            bool firstBonus = p.FirstPurchaseExtra > 0 && bought == 0;
            int grant = p.Diamonds + (firstBonus ? p.FirstPurchaseExtra : 0);
            string orderId = Crypto.NewId("ord");
            long now = clock().ToUnixTimeMilliseconds();
            Database.Exec(c, tx, """
                INSERT INTO store_orders(id, account_id, package_id, price_vnd, diamonds_granted, first_purchase_bonus, provider, provider_txn_id, status, created_ms)
                VALUES($id,$a,$p,$price,$d,$f,$prov,$txn,'granted',$t)
                """, ("$id", orderId), ("$a", accountId), ("$p", p.Id), ("$price", p.PriceVnd), ("$d", grant),
                ("$f", firstBonus ? 1 : 0), ("$prov", provider), ("$txn", providerTxnId), ("$t", now));
            Wallet.Diamonds(c, tx, accountId, grant, "store:" + p.Id, "order:" + orderId);
            Database.Exec(c, tx, "UPDATE accounts SET paid_total_vnd = paid_total_vnd + $v WHERE id=$a", ("$v", p.PriceVnd), ("$a", accountId));
            if (p.Kind == PackageKind.MonthCard)
                Database.Exec(c, tx, "INSERT INTO month_cards(account_id, order_id, start_day, days_total, daily_diamonds) VALUES($a,$o,$d,$n,$k)",
                    ("$a", accountId), ("$o", orderId), ("$d", TodayVn()), ("$n", p.MonthCardDays), ("$k", p.MonthCardDaily));
            // Starter rare item grant goes here once RewardItemId is configured (Unavailable() blocks sale until then).
            AccountService.Audit(c, tx, accountId, "store_grant", $"{p.Id} {provider}:{providerTxnId} +{grant}KC");
            return new StoreOrderDto { orderId = orderId, packageId = p.Id, priceVnd = p.PriceVnd, diamondsGranted = grant, firstPurchaseBonus = firstBonus, status = "granted", createdMs = now };
        });
    }

    private static StoreOrderDto OrderDto(Microsoft.Data.Sqlite.SqliteDataReader r) => new()
    {
        orderId = r.GetString(0), packageId = r.GetString(2), priceVnd = r.GetInt64(3), diamondsGranted = r.GetInt32(4),
        firstPurchaseBonus = r.GetInt64(5) != 0, status = r.GetString(6), createdMs = r.GetInt64(7),
    };

    public MonthCardStatusDto MonthCardStatus(string accountId)
    {
        using var c = db.Open();
        return MonthCard(c, null, accountId);
    }

    private MonthCardStatusDto MonthCard(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string accountId)
    {
        string today = TodayVn();
        using var cmd = Database.Command(c, tx,
            "SELECT order_id, start_day, days_total, daily_diamonds FROM month_cards WHERE account_id=$a ORDER BY start_day DESC LIMIT 1", ("$a", accountId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new MonthCardStatusDto();
        string order = r.GetString(0);
        DateOnly start = DateOnly.Parse(r.GetString(1)), now = DateOnly.Parse(today);
        int total = r.GetInt32(2), daily = r.GetInt32(3);
        int dayIndex = now.DayNumber - start.DayNumber; // 0-based
        var claimed = new List<string>();
        using (var q = Database.Command(c, tx, "SELECT claim_day FROM month_card_claims WHERE account_id=$a AND order_id=$o ORDER BY claim_day", ("$a", accountId), ("$o", order)))
        using (var qr = q.ExecuteReader())
            while (qr.Read()) claimed.Add(qr.GetString(0));
        bool active = dayIndex >= 0 && dayIndex < total;
        return new MonthCardStatusDto
        {
            active = active, daysRemaining = active ? total - dayIndex : 0, dailyDiamonds = daily,
            claimableToday = active && !claimed.Contains(today), claimedDays = claimed.ToArray(),
        };
    }

    /// <summary>Claims today's month-card diamonds (Vietnam calendar day). Once per day, only inside the 30-day window.</summary>
    public LedgerResult ClaimMonthCard(string accountId)
    {
        string today = TodayVn();
        return db.InTransaction((c, tx) =>
        {
            string? order = null; string? start = null; int total = 0, daily = 0;
            using (var cmd = Database.Command(c, tx, "SELECT order_id, start_day, days_total, daily_diamonds FROM month_cards WHERE account_id=$a ORDER BY start_day DESC LIMIT 1", ("$a", accountId)))
            using (var r = cmd.ExecuteReader())
                if (r.Read()) { order = r.GetString(0); start = r.GetString(1); total = r.GetInt32(2); daily = r.GetInt32(3); }
            if (order == null) throw new GameError("month_card_none", "Bạn chưa có Thẻ Tháng.", 409);
            int idx = DateOnly.Parse(today).DayNumber - DateOnly.Parse(start!).DayNumber;
            if (idx < 0 || idx >= total) throw new GameError("month_card_expired", "Thẻ Tháng đã hết hạn.", 409);
            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM month_card_claims WHERE account_id=$a AND order_id=$o AND claim_day=$d",
                    ("$a", accountId), ("$o", order), ("$d", today)) > 0)
                throw new GameError("month_card_claimed", "Hôm nay bạn đã nhận thưởng Thẻ Tháng.", 409);
            Database.Exec(c, tx, "INSERT INTO month_card_claims(account_id, order_id, claim_day, diamonds, claimed_ms) VALUES($a,$o,$d,$k,$t)",
                ("$a", accountId), ("$o", order), ("$d", today), ("$k", daily), ("$t", Database.NowMs()));
            return Wallet.Diamonds(c, tx, accountId, daily, "month_card_daily", $"monthcard:{order}:{today}");
        });
    }

    public StoreOrderDto[] History(string accountId)
    {
        using var c = db.Open();
        using var cmd = Database.Command(c, null,
            "SELECT id, account_id, package_id, price_vnd, diamonds_granted, first_purchase_bonus, status, created_ms FROM store_orders WHERE account_id=$a ORDER BY created_ms DESC LIMIT 100",
            ("$a", accountId));
        using var r = cmd.ExecuteReader();
        var list = new List<StoreOrderDto>();
        while (r.Read()) list.Add(OrderDto(r));
        return list.ToArray();
    }

    /// <summary>VIP from verified paid VND only (free diamonds never count). Level 0 while thresholds are unconfigured.</summary>
    public (int level, long paidVnd, bool configured) Vip(string accountId)
    {
        using var c = db.Open();
        long paid = Database.Scalar<long>(c, null, "SELECT paid_total_vnd FROM accounts WHERE id=$a", ("$a", accountId));
        int level = vipThresholdsVnd.Count(t => paid >= t);
        return (level, paid, vipThresholdsVnd.Length == 10);
    }
}
