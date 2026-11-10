using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>
/// Other players see the SAME skill a hero cast: on farm maps only from skills the server itself accepted (class,
/// slot, centre, main target), never from a client claim; in safe zones a practice cast (no effect) is shown, at
/// most one per global skill gap.
/// </summary>
public sealed class SkillSyncTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly PresenceService presence;
    private readonly SyncService sync;
    private long clock = new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    public SkillSyncTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(3), SkillConfig.Load(SkillServiceTests.SkillsPath));
        presence = new PresenceService { Clock = () => clock };
        var social = SocialConfig.Load(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "social.dev.json"),
            CosmeticCatalog.Load(CosmeticShopTests.CatalogPath));
        var chat = new ChatService(t.Db, social, new NameRules()) { Clock = () => clock };
        sync = new SyncService(t.Db, presence, chat, new MarriageService(t.Db, social, presence) { Clock = () => clock });
        // as wired in Program.cs
        farm.SkillCast = presence.RecordCast;
        presence.IsSafeMap = map => !farm.Config.spawns.Any(s => s.mapId == map);
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, string name, int cls)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, name, cls, 0).characterId);
    }

    private SyncDto Beat((string acc, string id) p, float x, int practiceSlot = 0, int practiceSeq = 0)
    {
        clock += 170;
        return sync.Sync(p.acc, p.id, new SyncRequestDto { x = x, y = 0.9f, facing = 1, anim = "idle", practiceSlot = practiceSlot, practiceSeq = practiceSeq });
    }

    [Fact]
    public void Server_accepted_farm_skill_reaches_the_other_player_with_slot_and_target()
    {
        var a = Hero("sksync01", "Pháp Hỏa", 1);
        var b = Hero("sksync02", "Người Xem", 0);
        chars.Travel(a.acc, a.id, "hub_east_meadow", 66f, 0f);
        chars.Travel(b.acc, b.id, "hub_east_meadow", 66f, 0f);
        Beat(a, -27f); Beat(b, -30f);
        Assert.Equal(0, Assert.Single(Beat(b, -30f).peers).castSeq);

        // a client cannot claim a skill on a farm map: the practice field is ignored there
        Beat(a, -27f, practiceSlot: 3, practiceSeq: 1);
        Assert.Equal(0, Assert.Single(Beat(b, -30f).peers).castSeq);

        // the server accepts Cầu Lửa (slot 1) on Slime Bông meadow_s1 → B sees it, with slot, centre and target
        clock += 500;
        farm.Presence(a.acc, a.id, new PresenceRequestDto { x = -27f, y = 0f });
        clock += 500;
        SkillResultDto r = farm.Skill(a.acc, a.id, new SkillRequestDto { slot = 1, x = -27f, y = 0f, facing = 1, spawnId = "meadow_s1" });
        PeerDto seen = Assert.Single(Beat(b, -30f).peers);
        Assert.Equal(1, seen.castSeq);
        Assert.Equal(1, seen.castSlot);
        Assert.Equal("meadow_s1", seen.castTarget);
        Assert.Equal(r.cx, seen.castX, 2);
        Assert.InRange(seen.castAgeMs, 0, PresenceService.CastShowMs);
        // A's own heartbeats do not lose it, and nothing new appears without a new cast
        Beat(a, -27f);
        Assert.Equal(1, Assert.Single(Beat(b, -30f).peers).castSeq);

        // a refused skill (cooldown) is never shown
        Assert.Throws<GameError>(() => farm.Skill(a.acc, a.id, new SkillRequestDto { slot = 1, x = -27f, y = 0f, facing = 1, spawnId = "meadow_s1" }));
        Assert.Equal(1, Assert.Single(Beat(b, -30f).peers).castSeq);
    }

    [Fact]
    public void Practice_casts_in_the_safe_hub_are_shown_once_per_counter_and_rate_limited()
    {
        var a = Hero("sksync03", "Kiếm Tập", 0);
        var b = Hero("sksync04", "Người Xem Hai", 2);
        Beat(a, 0f); Beat(b, 3f);
        Beat(a, 0f, practiceSlot: 2, practiceSeq: 1);
        PeerDto seen = Assert.Single(Beat(b, 3f).peers);
        Assert.Equal(1, seen.castSeq);
        Assert.Equal(2, seen.castSlot);
        Assert.Equal("", seen.castTarget);
        Assert.Equal(3f, seen.castX, 2); // a point ahead of the hero (facing +1)
        // the same counter again: nothing new
        Beat(a, 0f, practiceSlot: 2, practiceSeq: 1);
        Assert.Equal(1, Assert.Single(Beat(b, 3f).peers).castSeq);
        // a new counter later: accepted
        clock += 600;
        Beat(a, 0f, practiceSlot: 3, practiceSeq: 2);
        PeerDto again = Assert.Single(Beat(b, 3f).peers);
        Assert.Equal(2, again.castSeq);
        Assert.Equal(3, again.castSlot);
        // another new counter right after it (< 450 ms since that cast): ignored (spam)
        sync.Sync(a.acc, a.id, new SyncRequestDto { x = 0f, y = 0.9f, facing = 1, practiceSlot = 1, practiceSeq = 3 });
        Assert.Equal(2, Assert.Single(Beat(b, 3f).peers).castSeq);
    }
}
