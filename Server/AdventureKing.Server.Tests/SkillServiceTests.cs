using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Server-decided skills: damage, MP cost (level scaled), per-skill + global cooldown, shapes, refusals.</summary>
public sealed class SkillServiceTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private long clock = 1_000_000;

    public static string SkillsPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "skills.json");

    public SkillServiceTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
    }

    public void Dispose() => t.Dispose();

    private FarmService Farm(Action<SkillConfig>? tweak = null)
    {
        SkillConfig cfg = SkillConfig.Load(SkillsPath);
        tweak?.Invoke(cfg);
        return new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(3), cfg);
    }

    private (string acc, string id) Hero(string login, int heroClass, bool toMeadow = true)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        var c = chars.Create(acc, "Anh Hùng " + login.Substring(login.Length - 2), heroClass, 0);
        if (toMeadow) chars.Travel(acc, c.characterId, "hub_east_meadow", 66f, 0f);
        return (acc, c.characterId);
    }

    private void WalkTo(FarmService farm, string acc, string id, float from, float to)
    {
        for (float x = from; Math.Abs(x - to) > 0.01f; x += Math.Clamp(to - x, -5f, 5f)) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500;
        farm.Presence(acc, id, new PresenceRequestDto { x = to, y = 0 });
    }

    private SkillResultDto Cast(FarmService farm, string acc, string id, int slot, float x, int facing = -1, string? spawn = null, long wait = 500)
    {
        clock += wait;
        return farm.Skill(acc, id, new SkillRequestDto { slot = slot, x = x, y = 0, facing = facing, spawnId = spawn });
    }

    [Fact]
    public void Skills_deal_server_damage_spend_mp_and_start_cooldowns()
    {
        FarmService farm = Farm();
        var (acc, id) = Hero("skill01", 0); // Kiếm Sĩ: base 18, MP 80, +2 MP/s
        WalkTo(farm, acc, id, -36f, -21f);
        SkillResultDto s1 = Cast(farm, acc, id, 1, -21f);               // Chém Gió (line, facing the slime) x1.8 = 32
        Assert.Equal("Chém Gió", s1.name);
        Assert.Single(s1.hits);
        Assert.Equal(32, s1.hits[0].damage);
        Assert.Equal(28, s1.hits[0].monster.hp);
        Assert.Equal(10, s1.mpSpent);
        Assert.Equal(70, s1.hero.mp);                                   // was full: no regen above the cap
        Assert.Equal(5000, s1.hero.cooldownMs[0]);
        // the same skill again before 5 s: refused, nothing spent, cooldown unchanged
        var e = Assert.Throws<GameError>(() => Cast(farm, acc, id, 1, -21f));
        Assert.Equal("skill_cooldown", e.Code);
        HeroCombatDto now = farm.Peek(acc, id);
        Assert.Equal(71, now.mp);                                       // only 0.5 s of regen (+1)
        Assert.Equal(4500, now.cooldownMs[0]);
        // Kiếm Khí Xoáy (self, radius 2.6) finishes it: x2.4 = 43 > 28
        SkillResultDto s2 = Cast(farm, acc, id, 2, -21f);
        Assert.True(s2.hits[0].killed);
        Assert.NotNull(s2.hits[0].drop);                                // Gold drops on the ground (picked up separately)
        Assert.Equal(72 - 25, s2.hero.mp);
        // after 5 s Chém Gió is ready again
        clock += 4000;
        Assert.Equal(0, farm.Peek(acc, id).cooldownMs[0]);
    }

    [Fact]
    public void Target_skill_hits_the_chosen_monster_and_its_neighbours_up_to_max_targets()
    {
        FarmService farm = Farm();
        var (acc, id) = Hero("skill06", 1); // Pháp Sư base 34
        WalkTo(farm, acc, id, -36f, -27f);
        SkillResultDto meteor = Cast(farm, acc, id, 3, -27f, facing: 1, spawn: "meadow_s1"); // x3.6 = 122, radius 3
        Assert.Equal(122, meteor.hits[0].damage);
        Assert.Equal("meadow_s1", meteor.hits[0].monster.id);
        Assert.True(meteor.hits[0].killed);
        Assert.Single(meteor.hits);                                     // s2 is 8 m away: outside the 3 m radius
        Assert.Equal(-22f, meteor.cx);
    }

    [Fact]
    public void Global_cooldown_no_target_and_unknown_slot_are_refused_without_spending()
    {
        FarmService farm = Farm();
        var (acc, id) = Hero("skill02", 0);
        WalkTo(farm, acc, id, -36f, -21f);
        Cast(farm, acc, id, 1, -21f);
        var tooFast = Assert.Throws<GameError>(() => Cast(farm, acc, id, 2, -21f, wait: 100)); // inside the 0.45 s global cooldown
        Assert.Equal("too_fast", tooFast.Code);
        var none = Assert.Throws<GameError>(() => Cast(farm, acc, id, 3, -21f, spawn: "meadow_s3")); // 30 m away
        Assert.Equal("no_target", none.Code);
        HeroCombatDto h = farm.Peek(acc, id);
        Assert.Equal(0, h.cooldownMs[2]);                                // no free cooldown burn, no MP lost
        Assert.Equal(71, h.mp);
        Assert.Equal("skill_unknown", Assert.Throws<GameError>(() => Cast(farm, acc, id, 4, -21f)).Code);
    }

    [Fact]
    public void Not_enough_mp_refuses_the_cast()
    {
        FarmService farm = Farm(cfg => cfg.classes.First(c => c.heroClass == 0).maxMp = 30); // pool smaller than Thiên Kiếm
        var (acc, id) = Hero("skill07", 0);
        WalkTo(farm, acc, id, -36f, -21f);
        var e = Assert.Throws<GameError>(() => Cast(farm, acc, id, 3, -21f, spawn: "meadow_s1"));
        Assert.Equal("not_enough_mp", e.Code);
        HeroCombatDto h = farm.Peek(acc, id);
        Assert.Equal(30, h.mp);
        Assert.Equal(0, h.cooldownMs[2]);
        Assert.Equal(60, farm.State(acc, id).monsters.First(m => m.id == "meadow_s1").hp); // no damage
    }

    [Fact]
    public void Mana_regenerates_on_the_server_clock()
    {
        FarmService farm = Farm();
        var (acc, id) = Hero("skill03", 1); // Pháp Sư: 140 MP, 3/s
        WalkTo(farm, acc, id, -36f, -27f);
        Assert.Equal(95, Cast(farm, acc, id, 3, -27f, facing: 1, spawn: "meadow_s1").hero.mp);
        clock += 5000;
        Assert.Equal(110, farm.Peek(acc, id).mp);
        clock += 60_000;
        Assert.Equal(140, farm.Peek(acc, id).mp);                         // capped
    }

    [Fact]
    public void Skills_are_refused_in_the_safe_hub()
    {
        FarmService farm = Farm();
        var (acc, id) = Hero("skill04", 0, toMeadow: false);
        Assert.Equal("safe_zone", Assert.Throws<GameError>(() => Cast(farm, acc, id, 1, 0f)).Code);
    }

    [Fact]
    public void Mp_pool_and_costs_scale_with_level()
    {
        FarmService farm = Farm();
        var (acc, id) = Hero("skill05", 0);
        using (var c = t.Db.Open()) Database.Exec(c, null, "UPDATE characters SET level=11 WHERE id=$c", ("$c", id));
        WalkTo(farm, acc, id, -36f, -21f);
        Assert.Equal(120, farm.Peek(acc, id).maxMp);                     // 80 x (1 + 0.05 x 10)
        Assert.Equal(13, Cast(farm, acc, id, 1, -21f).mpSpent);          // 10 x (1 + 0.03 x 10)
    }

    [Fact]
    public void Every_class_has_exactly_three_skills_with_the_directive_cooldowns_and_costs()
    {
        SkillConfig cfg = SkillConfig.Load(SkillsPath);
        Assert.Equal(4, cfg.classes.Length);
        var names = new Dictionary<int, string[]>
        {
            [0] = new[] { "Chém Gió", "Kiếm Khí Xoáy", "Thiên Kiếm" },
            [1] = new[] { "Cầu Lửa", "Bão Băng", "Thiên Thạch" },
            [2] = new[] { "Phi Dao", "Ảnh Phân Thân", "Ám Ảnh Liên Trảm" },
            [3] = new[] { "Tên Xuyên", "Mưa Tên", "Phượng Hoàng Tiễn" },
        };
        foreach (ClassSkills c in cfg.classes)
        {
            Assert.Equal(names[c.heroClass], c.skills.OrderBy(s => s.slot).Select(s => s.name).ToArray());
            Assert.Equal(new[] { 5f, 15f, 20f }, c.skills.OrderBy(s => s.slot).Select(s => s.cooldownSeconds).ToArray());
            Assert.Equal(new[] { 10, 25, 45 }, c.skills.OrderBy(s => s.slot).Select(s => s.mpCost).ToArray());
        }
    }
}
