using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>7-day check-in: VN calendar days on the server clock, pause-not-reset, pay-once, draft stays off.</summary>
public sealed class CheckinServiceTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly CheckinService checkin;
    private readonly string acc;
    private readonly CharacterDto hero;
    // 2026-11-10 08:00 in Vietnam (01:00 UTC)
    private long clock = new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    private const long Day = 86_400_000;

    private static string Config(string file) => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", file);

    public CheckinServiceTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 800);
        checkin = new CheckinService(t.Db, WelfareConfig.Load(Config("welfare.dev.json")), CosmeticCatalog.Load(CosmeticShopTests.CatalogPath), () => clock);
        acc = accounts.Register("checkin01", "checkin-pass-01").AccountId;
        hero = chars.Create(acc, "Điểm Danh", 3, 0);
    }

    public void Dispose() => t.Dispose();

    [Fact]
    public void Production_config_is_the_unapproved_draft_and_refuses_claims()
    {
        var prod = new CheckinService(t.Db, WelfareConfig.Load(Config("welfare.json")), CosmeticCatalog.Load(CosmeticShopTests.CatalogPath), () => clock);
        CheckinDto s = prod.Status(acc, hero.characterId);
        Assert.False(s.enabled);
        Assert.False(s.canClaimToday);
        Assert.Equal(7, s.days.Length);
        Assert.Contains("chờ chủ dự án duyệt", s.message);
        Assert.Equal("checkin_disabled", Assert.Throws<GameError>(() => prod.Claim(acc, hero.characterId, null)).Code);
        // enabling the owner's DRAFT for live is refused at startup
        var cfg = WelfareConfig.Load(Config("welfare.json"));
        cfg.dailyCheckin.enabled = true;
        Assert.Throws<InvalidDataException>(cfg.Validate);
    }

    [Fact]
    public void One_claim_per_VN_day_and_missed_days_pause()
    {
        CheckinDto d1 = checkin.Claim(acc, hero.characterId, null);
        Assert.Equal(1, d1.claimedDays);
        Assert.Equal(800 + 200, d1.gold);
        Assert.Equal("already_claimed_today", Assert.Throws<GameError>(() => checkin.Claim(acc, hero.characterId, null)).Code);
        // 23:30 VN the same day: still the same day
        clock += 15 * 3_600_000 + 1_800_000;
        Assert.Equal("already_claimed_today", Assert.Throws<GameError>(() => checkin.Claim(acc, hero.characterId, null)).Code);
        // 00:30 VN next day (17:30 UTC): new day
        clock += 3_600_000;
        Assert.Equal(2, checkin.Claim(acc, hero.characterId, null).claimedDays);
        // skip three days: the cycle pauses, next claim is day 3 (not reset)
        clock += 4 * Day;
        CheckinDto d3 = checkin.Claim(acc, hero.characterId, null);
        Assert.Equal(3, d3.claimedDays);
        Assert.Equal(800 + 200 + 300 + 500, d3.gold); // day 3: starter chest = 500 Gold
    }

    [Fact]
    public void Day_seven_needs_a_valid_choice_then_a_new_cycle_starts_next_day()
    {
        for (int d = 1; d <= 6; d++) { checkin.Claim(acc, hero.characterId, null); clock += Day; }
        Assert.Equal("choice_required", Assert.Throws<GameError>(() => checkin.Claim(acc, hero.characterId, null)).Code);
        Assert.Equal("choice_required", Assert.Throws<GameError>(() => checkin.Claim(acc, hero.characterId, new CheckinClaimDto { choiceItemId = "set_crystal_zephyr" })).Code);
        CheckinDto d7 = checkin.Claim(acc, hero.characterId, new CheckinClaimDto { choiceItemId = "head_lotus_crown" });
        Assert.Equal(7, d7.claimedDays);
        Assert.Contains(d7.granted, g => g.label == "Vòng Hoa Sen");
        Assert.Equal(3, d7.diamonds); // day 4 (+1) and day 6 (+2)
        Assert.Equal("already_claimed_today", Assert.Throws<GameError>(() => checkin.Claim(acc, hero.characterId, null)).Code);
        clock += Day;
        CheckinDto next = checkin.Claim(acc, hero.characterId, null);
        Assert.Equal(2, next.cycle);
        Assert.Equal(1, next.claimedDays);
    }

    [Fact]
    public void Concurrent_claims_pay_once()
    {
        int ok = 0;
        Parallel.For(0, 8, _ =>
        {
            try { checkin.Claim(acc, hero.characterId, null); Interlocked.Increment(ref ok); }
            catch (GameError) { }
        });
        Assert.Equal(1, ok);
        using var c = t.Db.Open();
        Assert.Equal(1000, Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", hero.characterId)));
        Assert.Equal(1, Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM checkin_claims"));
    }

    [Fact]
    public void Another_account_cannot_claim_with_my_character()
    {
        string other = accounts.Register("checkin02", "checkin-pass-02").AccountId;
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => checkin.Claim(other, hero.characterId, null)).Code);
    }
}
