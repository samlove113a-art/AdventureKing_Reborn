using System.Security.Cryptography;
using System.Text;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;

namespace AdventureKing.Server.Domain;

public sealed class VietQrOptions
{
    /// <summary>Master switch. OFF by default: nothing can be bought until the owner fills the bank account and turns it on (appsettings VietQr:Enabled).</summary>
    public bool Enabled { get; set; }
    public string BankId { get; set; } = "";        // e.g. "MB", "VCB" (short VietQR bank code)
    /// <summary>6-digit NAPAS bank BIN used inside the EMVCo payload. Empty = looked up from BankId (common banks).</summary>
    public string BankBin { get; set; } = "";
    public string Bin => BankBin.Length > 0 ? BankBin : VietQrPayload.BinOf(BankId);
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "";
    /// <summary>Shared secret the bank-notification service sends in the Authorization header of the webhook.</summary>
    public string WebhookSecret { get; set; } = "";
    public int OrderMinutes { get; set; } = 30;
    public bool Ready => Enabled && BankId.Length > 0 && Bin.Length == 6 && AccountNo.Length > 0 && AccountName.Length > 0 && WebhookSecret.Length >= 16;
}

public sealed class TopUpOrderDto
{
    public string orderId { get; set; } = "";
    public string code { get; set; } = "";
    public string packageId { get; set; } = "";
    public long amountVnd { get; set; }
    public string status { get; set; } = "";      // pending | completed | expired
    public string qrUrl { get; set; } = "";
    public string bankId { get; set; } = "";
    public string accountNo { get; set; } = "";
    public string accountName { get; set; } = "";
    /// <summary>EMVCo VietQR string (NAPAS 247, dynamic amount + transfer text + CRC16). The app turns it into the QR picture itself.</summary>
    public string payload { get; set; } = "";
    public long expiresMs { get; set; }
    public int diamondsGranted { get; set; }
}

/// <summary>VietQR / NAPAS 247 payload builder (EMVCo merchant-presented QR).</summary>
public static class VietQrPayload
{
    private static readonly Dictionary<string, string> Bins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MB"] = "970422", ["MBBANK"] = "970422", ["VCB"] = "970436", ["VIETCOMBANK"] = "970436", ["TCB"] = "970407", ["TECHCOMBANK"] = "970407", ["ACB"] = "970416",
        ["BIDV"] = "970418", ["VPB"] = "970432", ["VPBANK"] = "970432", ["TPB"] = "970423", ["TPBANK"] = "970423", ["CTG"] = "970415", ["VIETINBANK"] = "970415",
        ["STB"] = "970403", ["SACOMBANK"] = "970403", ["VBA"] = "970405", ["AGRIBANK"] = "970405", ["OCB"] = "970448", ["SHB"] = "970443", ["HDB"] = "970437", ["HDBANK"] = "970437",
        ["MSB"] = "970426", ["VIB"] = "970441", ["SEAB"] = "970440", ["EIB"] = "970431",
    };
    public static string BinOf(string bankId) => Bins.TryGetValue(bankId ?? "", out string b) ? b : "";

    private static string Tlv(string id, string value) => id + value.Length.ToString("00") + value;

    public static string Build(string bin, string accountNo, long amount, string addInfo)
    {
        string beneficiary = Tlv("00", bin) + Tlv("01", accountNo);
        string merchant = Tlv("00", "A000000727") + Tlv("01", beneficiary) + Tlv("02", "QRIBFTTA");
        string body = Tlv("00", "01") + Tlv("01", "12") + Tlv("38", merchant) + Tlv("53", "704") + Tlv("54", amount.ToString())
                      + Tlv("58", "VN") + Tlv("62", Tlv("08", addInfo)) + "6304";
        return body + Crc16(body).ToString("X4");
    }

    /// <summary>CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) over the whole string including the "6304" tag.</summary>
    public static ushort Crc16(string s)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in System.Text.Encoding.ASCII.GetBytes(s))
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}

public sealed class BankWebhookDto { public string? content { get; set; } public long amount { get; set; } public string? transactionId { get; set; } }

/// <summary>
/// 100 % VietQR top-up. The client can only ASK for an order; Diamonds are credited exclusively by <see cref="Confirm"/>, called by the bank-notification
/// webhook (secret-protected), which matches the unique transfer code "AKR######" and the exact amount, then grants through DiamondStore.GrantVerifiedPurchase
/// (idempotent per provider transaction), so a notification can never pay twice. Disabled until the owner configures and enables it.
/// </summary>
public sealed class VietQrTopUp
{
    private readonly Database db;
    private readonly DiamondStore store;
    private readonly VietQrOptions opt;
    private readonly Func<long> now;

    public VietQrTopUp(Database db, DiamondStore store, VietQrOptions opt, Func<long>? clock = null)
    { this.db = db; this.store = store; this.opt = opt; now = clock ?? Database.NowMs; }

    public bool Enabled => opt.Ready;

    public string QrUrl(string code, long amount) =>
        $"https://img.vietqr.io/image/{Uri.EscapeDataString(opt.BankId)}-{Uri.EscapeDataString(opt.AccountNo)}-compact2.png?amount={amount}&addInfo={Uri.EscapeDataString(code)}&accountName={Uri.EscapeDataString(opt.AccountName)}";

    private TopUpOrderDto Dto(string id, string code, string pkg, long amount, string status, long expires, int granted) => new()
    {
        orderId = id, code = code, packageId = pkg, amountVnd = amount, status = status, expiresMs = expires, diamondsGranted = granted,
        qrUrl = QrUrl(code, amount), payload = VietQrPayload.Build(opt.Bin, opt.AccountNo, amount, code), bankId = opt.BankId, accountNo = opt.AccountNo, accountName = opt.AccountName,
    };

    public TopUpOrderDto Create(string accountId, string packageId)
    {
        if (!opt.Ready) throw new GameError("payments_disabled", "Nạp tiền chưa được kích hoạt.", 403);
        StorePackage p = DiamondStore.Get(packageId);
        string reason = store.CatalogFor(accountId).First(x => x.id == p.Id).unavailableReason;
        if (reason.Length > 0 && !reason.StartsWith("Thanh toán thật")) throw new GameError("package_unavailable", reason, 409);
        return db.InTransaction((c, tx) =>
        {
            long t = now();
            // one open order per account + package: re-use it instead of piling up codes
            using (var cmd = Database.Command(c, tx, "SELECT id, code, expires_ms FROM topup_orders WHERE account_id=$a AND package_id=$p AND status='pending' AND expires_ms>$t",
                       ("$a", accountId), ("$p", p.Id), ("$t", t)))
            using (var r = cmd.ExecuteReader())
                if (r.Read()) return Dto(r.GetString(0), r.GetString(1), p.Id, p.PriceVnd, "pending", r.GetInt64(2), 0);
            string code;
            do code = "AKR" + RandomNumberGenerator.GetInt32(100000, 1000000);
            while (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM topup_orders WHERE code=$c", ("$c", code)) > 0);
            string id = Crypto.NewId("top");
            long exp = t + opt.OrderMinutes * 60_000L;
            Database.Exec(c, tx, "INSERT INTO topup_orders(id, account_id, package_id, code, amount_vnd, status, created_ms, expires_ms) VALUES($i,$a,$p,$c,$m,'pending',$t,$e)",
                ("$i", id), ("$a", accountId), ("$p", p.Id), ("$c", code), ("$m", p.PriceVnd), ("$t", t), ("$e", exp));
            return Dto(id, code, p.Id, p.PriceVnd, "pending", exp, 0);
        });
    }

    public TopUpOrderDto Status(string accountId, string orderId) => db.InTransaction((c, tx) =>
    {
        using var cmd = Database.Command(c, tx, "SELECT code, package_id, amount_vnd, status, expires_ms, diamonds FROM topup_orders WHERE id=$i AND account_id=$a", ("$i", orderId), ("$a", accountId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new GameError("order_unknown", "Không tìm thấy đơn nạp.", 404);
        string status = r.GetString(3);
        if (status == "pending" && r.GetInt64(4) <= now()) status = "expired";
        return Dto(orderId, r.GetString(0), r.GetString(1), r.GetInt64(2), status, r.GetInt64(4), r.GetInt32(5));
    });

    /// <summary>Bank notification: matches the code inside the transfer text and the exact amount. Returns false when nothing matched (never throws on foreign transfers).</summary>
    public bool Confirm(string? authorization, BankWebhookDto? body)
    {
        if (!opt.Ready) throw new GameError("payments_disabled", "Nạp tiền chưa được kích hoạt.", 403);
        byte[] given = Encoding.UTF8.GetBytes((authorization ?? "").Replace("Bearer ", "").Trim()), want = Encoding.UTF8.GetBytes(opt.WebhookSecret);
        if (given.Length != want.Length || !CryptographicOperations.FixedTimeEquals(given, want)) throw new GameError("webhook_unauthorized", "Sai bí mật webhook.", 401);
        if (body == null || string.IsNullOrWhiteSpace(body.content) || string.IsNullOrWhiteSpace(body.transactionId) || body.amount <= 0) return false;
        var m = System.Text.RegularExpressions.Regex.Match(body.content.ToUpperInvariant().Replace(" ", ""), @"AKR\d{6}");
        if (!m.Success) return false;
        string? id = null, account = null, pkg = null; long amount = 0; string status = "";
        using (var c = db.Open())
        using (var cmd = Database.Command(c, null, "SELECT id, account_id, package_id, amount_vnd, status FROM topup_orders WHERE code=$c", ("$c", m.Value)))
        using (var r = cmd.ExecuteReader())
            if (r.Read()) { id = r.GetString(0); account = r.GetString(1); pkg = r.GetString(2); amount = r.GetInt64(3); status = r.GetString(4); }
        if (id == null) return false;
        if (status == "completed") return true;                 // duplicate notification: already paid, nothing granted again
        if (body.amount != amount) return false;                // wrong amount: not credited
        // grant (idempotent per bank transaction id) and mark the order in separate transactions; a retry after a crash replays the same grant
        StoreOrderDto granted = store.GrantVerifiedPurchase(account!, pkg!, "vietqr", body.transactionId!, amount);
        using (var c = db.Open())
            Database.Exec(c, null, "UPDATE topup_orders SET status='completed', diamonds=$d, txn=$t, completed_ms=$m WHERE id=$i AND status<>'completed'",
                ("$d", granted.diamondsGranted), ("$t", body.transactionId!), ("$m", now()), ("$i", id));
        return true;    }
}
