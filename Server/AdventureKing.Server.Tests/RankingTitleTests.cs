using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Leaderboards from real data only; nameplate frames only from server grants, one equipped, no power bonus.</summary>
public sealed class RankingTitleTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly RankingService ranks;
    private readonly TitleService titles;
    private long clock = 1_000_000;

    public RankingTitleTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(4), SkillConfig.Load(SkillServiceTests.SkillsPath));
        _ = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        ranks = new RankingService(t.Db, farm);
        titles = new TitleService(t.Db, () => clock);
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, string name, int heroClass, int level, long exp = 0)
    {
        clock += 1000;
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        string id = chars.Create(acc, name, heroClass, 0).characterId;
        using var c = t.Db.Open();
        Database.Exec(c, null, "UPDATE characters SET level=$l, experience=$e WHERE id=$c", ("$l", level), ("$e", exp), ("$c", id));
        return (acc, id);
    }

    [Fact]
    public void Level_and_power_boards_rank_real_characters_and_mark_mine()
    {
        var (a1, h1) = Hero("rank01", "Hạng Một", 0, 5, 100);
        var (_, h2) = Hero("rank02", "Hạng Hai", 1, 5, 300);
        var (_, h3) = Hero("rank03", "Hạng Ba", 2, 9);
        RankBoardDto lv = ranks.Get(a1, h1, "level");
        Assert.Equal(3, lv.total);
        Assert.Equal(new[] { h3, h2, h1 }, lv.rows.Select(r => r.characterId));
        Assert.Equal(9, lv.rows[0].value);
        Assert.True(lv.rows[2].me);
        Assert.Equal(3, lv.mine!.rank);

        RankBoardDto pw = ranks.Get(a1, h1, "power");
        Assert.Equal(3, pw.rows.Length);
        Assert.All(pw.rows, r => Assert.Equal(farm.CombatPower(r.characterId, r.heroClass, r.level), r.value));
        Assert.True(pw.rows.Zip(pw.rows.Skip(1)).All(p => p.First.value >= p.Second.value), "sorted by power");
        Assert.True(farm.CombatPower(h1, 0, 10) > farm.CombatPower(h1, 0, 5), "level raises power");

        foreach (string b in new[] { "guild", "boss" })
        {
            RankBoardDto e = ranks.Get(a1, h1, b);
            Assert.Empty(e.rows);
            Assert.False(string.IsNullOrEmpty(e.emptyReason), "an empty board says why");
        }
        Assert.Equal("board_unknown", Assert.Throws<GameError>(() => ranks.Get(a1, h1, "x")).Code);
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => ranks.Get(a1, h2, "level")).Code);
    }

    [Fact]
    public void Frames_are_server_granted_only_one_equipped_and_never_add_power()
    {
        var (acc, id) = Hero("title01", "Khung Tên", 0, 3);
        TitlesDto s = titles.Get(acc, id);
        Assert.Empty(s.owned);
        Assert.Null(s.equipped);
        Assert.Contains("Chưa sở hữu", s.note);
        Assert.Equal("title_not_owned", Assert.Throws<GameError>(() => titles.Equip(acc, id, new TitleIdDto { titleId = "VIP_15" })).Code);
        int power = farm.CombatPower(id, 0, 3);

        titles.Grant(id, "BossHunter_01", "test:server-grant");
        titles.Grant(id, "BossHunter_01", "test:server-grant"); // idempotent
        titles.Grant(id, "Special_Starter", "test:server-grant");
        Assert.Equal("title_unknown", Assert.Throws<GameError>(() => titles.Grant(id, "VIP_99", "x")).Code);
        s = titles.Equip(acc, id, new TitleIdDto { titleId = "BossHunter_01" });
        Assert.Equal(2, s.owned.Length);
        Assert.Equal("BossHunter_01", s.equipped);
        Assert.Equal("Boss Hunter 1", s.owned[0].name);
        s = titles.Equip(acc, id, new TitleIdDto { titleId = "Special_Starter" });
        Assert.Equal("Special_Starter", s.equipped);
        Assert.Equal(power, farm.CombatPower(id, 0, 3));
        s = titles.Equip(acc, id, new TitleIdDto { titleId = null });
        Assert.Null(s.equipped);
        Assert.Equal(2, s.owned.Length); // unequipping keeps ownership

        var (other, _) = Hero("title02", "Người Khác", 1, 1);
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => titles.Equip(other, id, new TitleIdDto { titleId = "Special_Starter" })).Code);
    }
}
