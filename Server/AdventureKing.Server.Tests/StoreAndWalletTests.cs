using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

public class StoreAndWalletTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private DateTimeOffset now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero); // 10:00 in Vietnam
    private readonly DiamondStore store;
    private readonly string acc;

    public StoreAndWalletTests()
    {
        accounts = new AccountService(t.Db);
        store = new DiamondStore(t.Db, null, () => now);
        acc = accounts.Register("buyer001", "buyer-pass-001").AccountId;
    }

    public void Dispose() => t.Dispose();

    private long Diamonds()
    {
        using var c = t.Db.Open();
        return Database.Scalar<long>(c, null, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", acc));
    }

    [Fact]
    public void Catalog_Matches_The_Approved_Price_Table_Exactly()
    {
        var expected = new (string id, long price, int diamonds)[]
        {
            ("starter_pack", 9_000, 10), ("month_card", 49_000, 15), ("pouch_1", 10_000, 10), ("pouch_2", 20_000, 22),
            ("pouch_3", 50_000, 58), ("chest_1", 99_000, 125), ("chest_2", 199_000, 260), ("vault", 499_000, 700),
        };
        StorePackageDto[] cat = store.CatalogFor(acc);
        Assert.Equal(expected.Length, cat.Length);
        foreach (var (id, price, diamonds) in expected)
        {
            StorePackageDto p = Assert.Single(cat, x => x.id == id);
            Assert.Equal(price, p.priceVnd);
            Assert.Equal(diamonds, p.diamonds);
            Assert.False(p.purchasable); // payments disabled
        }
        Assert.Equal(20, cat.Single(p => p.id == "pouch_1").firstPurchaseDiamonds);
        Assert.Contains("Lần đầu x2", cat.Single(p => p.id == "pouch_1").tags);
        Assert.Contains("+40%", cat.Single(p => p.id == "vault").tags);
        Assert.Contains("+25%", cat.Single(p => p.id == "chest_1").tags);
        Assert.Contains("+30%", cat.Single(p => p.id == "chest_2").tags);
        Assert.Contains("+10%", cat.Single(p => p.id == "pouch_2").tags);
        Assert.Contains("+16%", cat.Single(p => p.id == "pouch_3").tags);
        Assert.Contains("tối đa 105 KC", cat.Single(p => p.id == "month_card").rewardSummary);
        Assert.DoesNotContain(cat.SelectMany(p => p.tags), tag => tag.Contains("bán chạy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Client_Purchase_Is_Refused_And_Grants_Nothing()
    {
        var e = Assert.Throws<GameError>(() => store.RequestPurchase(acc, "vault"));
        Assert.Equal("payments_disabled", e.Code);
        Assert.Equal(0, Diamonds());
        Assert.Empty(store.History(acc));
    }

    [Fact]
    public void First_Purchase_Bonus_Once_And_Receipt_Replay_Never_Double_Grants()
    {
        StoreOrderDto first = store.GrantVerifiedPurchase(acc, "pouch_1", "test", "txn-1", 10_000);
        Assert.Equal(20, first.diamondsGranted);
        Assert.True(first.firstPurchaseBonus);
        StoreOrderDto replay = store.GrantVerifiedPurchase(acc, "pouch_1", "test", "txn-1", 10_000);
        Assert.Equal(first.orderId, replay.orderId);
        Assert.Equal(20, Diamonds());

        StoreOrderDto second = store.GrantVerifiedPurchase(acc, "pouch_1", "test", "txn-2", 10_000);
        Assert.Equal(10, second.diamondsGranted);
        Assert.Equal(30, Diamonds());

        string other = accounts.Register("thief001", "thief-pass-001").AccountId;
        Assert.Equal("receipt_reused", Assert.Throws<GameError>(() => store.GrantVerifiedPurchase(other, "pouch_1", "test", "txn-1", 10_000)).Code);
        Assert.Equal("price_mismatch", Assert.Throws<GameError>(() => store.GrantVerifiedPurchase(acc, "vault", "test", "txn-3", 1_000)).Code);
        Assert.Equal(30, Diamonds());
        Assert.Equal(2, store.History(acc).Length);
    }

    [Fact]
    public void Starter_Pack_Cannot_Be_Sold_Until_Its_Rare_Item_Is_Configured()
    {
        var e = Assert.Throws<GameError>(() => store.GrantVerifiedPurchase(acc, "starter_pack", "test", "s-1", 9_000));
        Assert.Equal("package_unavailable", e.Code);
        Assert.Contains("chưa được cấu hình", e.Message);
        Assert.Equal(0, Diamonds());
    }

    [Fact]
    public void Month_Card_Pays_15_Now_Then_3_Per_Vietnam_Day_Max_105()
    {
        store.GrantVerifiedPurchase(acc, "month_card", "test", "mc-1", 49_000);
        Assert.Equal(15, Diamonds());
        Assert.Equal("package_unavailable", Assert.Throws<GameError>(() => store.GrantVerifiedPurchase(acc, "month_card", "test", "mc-2", 49_000)).Code);

        for (int day = 0; day < 30; day++)
        {
            MonthCardStatusDto st = store.MonthCardStatus(acc);
            Assert.True(st.active);
            Assert.Equal(30 - day, st.daysRemaining);
            store.ClaimMonthCard(acc);
            Assert.Equal("month_card_claimed", Assert.Throws<GameError>(() => store.ClaimMonthCard(acc)).Code);
            now = now.AddDays(1);
        }
        Assert.Equal(105, Diamonds());
        Assert.False(store.MonthCardStatus(acc).active);
        Assert.Equal("month_card_expired", Assert.Throws<GameError>(() => store.ClaimMonthCard(acc)).Code);
        Assert.Equal(105, Diamonds());
        // Expired card can be bought again.
        store.GrantVerifiedPurchase(acc, "month_card", "test", "mc-3", 49_000);
        Assert.Equal(120, Diamonds());
    }

    [Fact]
    public void Month_Card_Day_Boundary_Is_Midnight_Vietnam_Time()
    {
        now = new DateTimeOffset(2026, 11, 8, 16, 59, 0, TimeSpan.Zero); // 23:59 VN
        store.GrantVerifiedPurchase(acc, "month_card", "test", "mc-b", 49_000);
        store.ClaimMonthCard(acc);
        now = now.AddMinutes(2); // 00:01 VN next day, still 8 Nov in UTC
        store.ClaimMonthCard(acc);
        Assert.Equal(21, Diamonds());
    }

    [Fact]
    public void Vip_Uses_Verified_Paid_Total_Only_And_Needs_Owner_Thresholds()
    {
        Assert.False(store.Vip(acc).configured);
        store.GrantVerifiedPurchase(acc, "chest_1", "test", "v-1", 99_000);
        using (var c = t.Db.Open())
        using (var tx = c.BeginTransaction())
        {
            Wallet.Diamonds(c, tx, acc, 500, "boss_drop", "boss:1"); // free diamonds must not count toward VIP
            tx.Commit();
        }
        long[] thresholds = { 10_000, 50_000, 100_000, 200_000, 500_000, 1_000_000, 2_000_000, 5_000_000, 10_000_000, 20_000_000 };
        var configured = new DiamondStore(t.Db, thresholds, () => now);
        var vip = configured.Vip(acc);
        Assert.Equal(99_000, vip.paidVnd);
        Assert.Equal(2, vip.level);
        Assert.Throws<ArgumentException>(() => new DiamondStore(t.Db, new long[] { 5, 4, 3 }));
    }

    [Fact]
    public void Wallet_Is_Idempotent_And_Never_Goes_Negative()
    {
        using var c = t.Db.Open();
        using var tx = c.BeginTransaction();
        Assert.True(Wallet.Diamonds(c, tx, acc, 50, "quest", "q:1").Applied);
        LedgerResult again = Wallet.Diamonds(c, tx, acc, 50, "quest", "q:1");
        Assert.False(again.Applied);
        Assert.Equal(50, again.Balance);
        Assert.Equal("idempotency_conflict", Assert.Throws<GameError>(() => Wallet.Diamonds(c, tx, acc, 999, "quest", "q:1")).Code);
        Assert.Equal("insufficient_diamonds", Assert.Throws<GameError>(() => Wallet.Diamonds(c, tx, acc, -51, "shop", "s:1")).Code);
        Assert.Equal(0, Wallet.Diamonds(c, tx, acc, -50, "shop", "s:2").Balance);
        tx.Commit();
    }
}
