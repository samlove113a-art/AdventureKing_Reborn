using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>VietQR top-up: order + QR link, webhook secret, exact amount, unique code, never paid twice. Uses test money only.</summary>
public sealed class VietQrTopUpTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly DiamondStore store;
    private readonly string acc;
    private long clock = 1_000_000;
    private static VietQrOptions Opt(bool on) => new() { Enabled = on, BankId = "MB", AccountNo = "0123456789", AccountName = "NGUYEN VAN A", WebhookSecret = "test-secret-0123456789" };

    public VietQrTopUpTests()
    {
        store = new DiamondStore(t.Db);
        acc = new AccountService(t.Db).Register("vqr001", "vqr-pass-0001").AccountId;
    }

    public void Dispose() => t.Dispose();

    private long Diamonds() { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", acc)); }

    [Fact]
    public void Payload_Is_A_Valid_EMVCo_VietQr_String_With_A_Correct_CRC()
    {
        Assert.Equal((ushort)0x29B1, VietQrPayload.Crc16("123456789"));                         // the standard CRC-16/CCITT-FALSE check value
        string p = VietQrPayload.Build("970422", "0123456789", 20000, "AKR123456");
        Assert.StartsWith("000201010212", p);
        Assert.Contains("0010A000000727", p);
        Assert.Contains("0006970422" + "01100123456789", p);
        Assert.Contains("0208QRIBFTTA", p);
        Assert.Contains("5303704", p);
        Assert.Contains("540520000", p);
        Assert.Contains("5802VN", p);
        Assert.Contains("62130809AKR123456", p);
        Assert.Equal(VietQrPayload.Crc16(p[..^4]).ToString("X4"), p[^4..]);
        var q = new VietQrTopUp(t.Db, store, Opt(true), () => clock);
        Assert.Equal(VietQrPayload.Build("970422", "0123456789", 20000, q.Create(acc, "pouch_2").code), q.Create(acc, "pouch_2").payload);
    }

    [Fact]
    public void Disabled_Until_The_Owner_Configures_It()
    {
        var off = new VietQrTopUp(t.Db, store, Opt(false), () => clock);
        Assert.Equal("payments_disabled", Assert.Throws<GameError>(() => off.Create(acc, "pouch_1")).Code);
        var noBank = new VietQrTopUp(t.Db, store, new VietQrOptions { Enabled = true }, () => clock);
        Assert.False(noBank.Enabled);
        Assert.Equal("payments_disabled", Assert.Throws<GameError>(() => noBank.Confirm("x", new BankWebhookDto())).Code);
    }

    [Fact]
    public void Order_Has_A_Unique_Code_And_The_Standard_QR_Link_And_Is_Reused_While_Open()
    {
        var q = new VietQrTopUp(t.Db, store, Opt(true), () => clock);
        TopUpOrderDto o = q.Create(acc, "pouch_2");
        Assert.Matches(@"^AKR\d{6}$", o.code);
        Assert.Equal(20_000, o.amountVnd);
        Assert.Equal($"https://img.vietqr.io/image/MB-0123456789-compact2.png?amount=20000&addInfo={o.code}&accountName=NGUYEN%20VAN%20A", o.qrUrl);
        Assert.Equal(o.orderId, q.Create(acc, "pouch_2").orderId);
        Assert.Equal("pending", q.Status(acc, o.orderId).status);
        clock += 31 * 60_000;
        Assert.Equal("expired", q.Status(acc, o.orderId).status);
        Assert.NotEqual(o.orderId, q.Create(acc, "pouch_2").orderId);
    }

    [Fact]
    public void Webhook_Needs_The_Secret_The_Exact_Amount_And_Credits_Once()
    {
        var q = new VietQrTopUp(t.Db, store, Opt(true), () => clock);
        TopUpOrderDto o = q.Create(acc, "pouch_1");                       // 10 000 VND = 10 KC, first purchase x2 = 20 KC
        string good = "test-secret-0123456789";
        Assert.Equal("webhook_unauthorized", Assert.Throws<GameError>(() => q.Confirm("wrong-secret-0123456789", new BankWebhookDto { content = o.code, amount = 10_000, transactionId = "T1" })).Code);
        Assert.False(q.Confirm(good, new BankWebhookDto { content = "chuyen tien an sang", amount = 10_000, transactionId = "T0" }));   // foreign transfer
        Assert.False(q.Confirm(good, new BankWebhookDto { content = "CK " + o.code, amount = 9_999, transactionId = "T2" }));          // wrong amount
        Assert.Equal(0, Diamonds());
        Assert.True(q.Confirm("Bearer " + good, new BankWebhookDto { content = "nap kim cuong " + o.code.ToLower(), amount = 10_000, transactionId = "T3" }));
        long after = Diamonds();
        Assert.Equal(20, after);
        Assert.Equal("completed", q.Status(acc, o.orderId).status);
        Assert.Equal(20, q.Status(acc, o.orderId).diamondsGranted);
        Assert.True(q.Confirm(good, new BankWebhookDto { content = o.code, amount = 10_000, transactionId = "T3" }));                 // same notification again
        Assert.True(q.Confirm(good, new BankWebhookDto { content = o.code, amount = 10_000, transactionId = "T4" }));                 // same order, other txn id: still paid once
        Assert.Equal(after, Diamonds());
    }

    [Fact]
    public void SePay_Webhook_Format_Is_Understood_Apikey_Header_Transfer_Type_And_Amount_Field()
    {
        var q = new VietQrTopUp(t.Db, store, Opt(true), () => clock);
        TopUpOrderDto o = q.Create(acc, "pouch_1");
        string header = "Apikey test-secret-0123456789";
        // an outgoing transfer is never a top-up
        Assert.False(q.Confirm(header, new BankWebhookDto { id = 1, transferType = "out", transferAmount = 10_000, content = o.code }));
        // incoming, the code only in "description" (SePay also fills "code" when a prefix is configured)
        Assert.True(q.Confirm(header, new BankWebhookDto { id = 92704, gateway = "TPBank", transferType = "in", transferAmount = 10_000, content = "NGUYEN VAN A chuyen tien", description = "BankAPINotify " + o.code, referenceCode = "FT123" }));
        Assert.Equal("completed", q.Status(acc, o.orderId).status);
        Assert.Equal(20, Diamonds());
        // SePay retries the same id: still one credit
        Assert.True(q.Confirm(header, new BankWebhookDto { id = 92704, transferType = "in", transferAmount = 10_000, description = o.code }));
        Assert.Equal(20, Diamonds());
        Assert.Equal("webhook_unauthorized", Assert.Throws<GameError>(() => q.Confirm("Apikey wrong", new BankWebhookDto { id = 2, transferType = "in", transferAmount = 10_000, content = o.code })).Code);
    }
}