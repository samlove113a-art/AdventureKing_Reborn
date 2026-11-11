using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Server-authoritative farm: HP, kills, 60 s respawn, Gold drops, pickup rules, no duplication.</summary>
public sealed class FarmServiceTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly string acc;
    private readonly CharacterDto sword; // Kiếm Sĩ: 18 dmg, 0.85 s, 1.6 m
    private long clock = 1_000_000;

    public static string MonstersPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "monsters.json");

    public FarmServiceTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 800) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(MonstersPath), () => clock, new Random(7));
        acc = accounts.Register("farmer01", "farmer-pass-01").AccountId;
        sword = chars.Create(acc, "Nông Dân", 0, 0);
        chars.Travel(acc, sword.characterId, "hub_east_meadow", 66f, 0f); // into the meadow (the hub is a safe zone)
    }

    public void Dispose() => t.Dispose();

    private long Gold() { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", sword.characterId)); }
    private AttackResultDto Swing(string spawn, float x, float y = 0f) { clock += 900; return farm.Attack(acc, sword.characterId, new AttackRequestDto { spawnId = spawn, x = x, y = y }); }

    /// <summary>Walks the reported position over in small steps (as the real client does while running).</summary>
    private void WalkTo(float from, float to) { for (float x = from; Math.Abs(x - to) > 0.01f; x += Math.Clamp(to - x, -5f, 5f)) { clock += 500; farm.State(acc, sword.characterId); Report(x); } Report(to); }
    private void Report(float x) { try { farm.Presence(acc, sword.characterId, new PresenceRequestDto { x = x, y = 0 }); } catch (GameError) { } }
    private HeroCombatDto Beat(float x, long ms) { clock += ms; return farm.Presence(acc, sword.characterId, new PresenceRequestDto { x = x, y = 0 }); }

    [Fact]
    public void Hit_monster_fights_back_on_the_server_timer()
    {
        WalkTo(-36f, -21f);
        AttackResultDto r = Swing("meadow_s1", -21f);        // aggro: Slime Bông 9 atk vs Kiếm Sĩ def 12 -> 3 per hit
        Assert.Equal(140, r.hero.hp);
        HeroCombatDto h = Beat(-21f, 1600);                   // one wind-up later: first strike
        Assert.Equal(137, h.hp);
        Assert.Equal(3, h.damageTaken);
        Assert.Contains("meadow_s1", h.hitBy);
        Assert.Equal(131, Beat(-21f, 3200).hp);               // two more strikes
        // going quiet does not dodge: 16 s without any request = 10 strikes at the last known position
        clock += 16_000;
        Assert.Equal(101, farm.State(acc, sword.characterId).hero.hp);
    }

    [Fact]
    public void Monster_cannot_hit_outside_its_leash_and_hero_regenerates()
    {
        WalkTo(-36f, -21f);
        Swing("meadow_s1", -21f);
        Assert.Equal(137, Beat(-21f, 1600).hp);
        // run 9 m away: beyond spawn (-22) + leash 3.5 + reach 1.3 -> no more hits
        Beat(-26f, 600); Beat(-30f, 600);
        Assert.Equal(137, Beat(-30f, 4000).hp);
        // out of combat > 6 s: regenerates 3 %/s of 140
        Assert.Equal(140, Beat(-30f, 10_000).hp);
    }

    [Fact]
    public void Fallen_hero_cannot_act_until_revived_at_the_map_point()
    {
        string a = accounts.Register("farmer09", "farmer-pass-09").AccountId;
        var mage = chars.Create(a, "Pháp Sư Yếu", 1, 0);       // 75 HP, def 3 -> 8 per hit
        chars.Travel(a, mage.characterId, "hub_east_meadow", 66f, 0f);
        for (float x = -36f; x < -21f; x += 4f) { clock += 500; farm.Presence(a, mage.characterId, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500;
        farm.Presence(a, mage.characterId, new PresenceRequestDto { x = -21f, y = 0 });
        clock += 2000;
        farm.Attack(a, mage.characterId, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 }); // 34 dmg, slime survives, aggro
        clock += 20_000;
        HeroCombatDto down = farm.State(a, mage.characterId).hero;
        Assert.True(down.dead);
        Assert.Equal(0, down.hp);
        Assert.Equal("hero_dead", Assert.Throws<GameError>(() => farm.Attack(a, mage.characterId, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 })).Code);
        HeroCombatDto up = farm.Revive(a, mage.characterId);
        Assert.False(up.dead);
        Assert.Equal(75, up.hp);
        Assert.Equal("not_dead", Assert.Throws<GameError>(() => farm.Revive(a, mage.characterId)).Code);
        clock += 300;
        Assert.Equal(75, farm.Presence(a, mage.characterId, new PresenceRequestDto { x = -35.5f, y = 1 }).hp); // at the respawn point, no "teleport" refusal
    }

    [Fact]
    public void Real_config_is_valid_and_keeps_the_hub_safe()
    {
        FarmConfig cfg = FarmConfig.Load(MonstersPath);
        Assert.NotEmpty(cfg.spawns);
        // farm maps only (Đồng Cỏ Gió + Chương 1); never the hub or the hall interiors
        Assert.All(cfg.spawns, s => Assert.Contains(s.mapId, new[] { "meadow_breeze", "ch1_mushroom_village", "ch1_green_meadow" }));
        Assert.All(cfg.types.Where(ty => ty.id.EndsWith("_slime")), ty => Assert.Equal(60, ty.respawnSeconds));
        var bad = new FarmConfig { types = cfg.types, spawns = new[] { new SpawnPoint { id = "x", mapId = "village_wind_bell", type = cfg.types[0].id } } };
        Assert.Throws<InvalidDataException>(bad.Validate);
    }

    [Fact]
    public void Kill_drops_server_rolled_gold_and_respawns_after_60_seconds()
    {
        // meadow_s1: Slime Bông, 60 HP at x=-22; the hero arrives at x=-36 and walks over
        WalkTo(-36f, -21f);
        int swings = 0;
        AttackResultDto r;
        do { r = Swing("meadow_s1", -21f); swings++; } while (!r.killed && swings < 10);
        long killedAt = clock;
        Assert.True(r.killed);
        Assert.Equal(4, swings); // 18 dmg x 4 >= 60: damage comes from the server class table, not the client
        Assert.NotNull(r.drop);
        Assert.InRange(r.drop!.amount, 5, 11);
        Assert.False(r.monster.alive);
        Assert.Equal(60_000, r.monster.respawnInMs);
        Assert.Equal("monster_dead", Assert.Throws<GameError>(() => Swing("meadow_s1", -21f)).Code);

        clock = killedAt + 59_000; // 59 s after the kill: still down
        Assert.False(farm.State(acc, sword.characterId).monsters.Single(m => m.id == "meadow_s1").alive);
        clock = killedAt + 60_000;
        MonsterDto back = farm.State(acc, sword.characterId).monsters.Single(m => m.id == "meadow_s1");
        Assert.True(back.alive);
        Assert.Equal(back.maxHp, back.hp);
    }

    [Fact]
    public void Pickup_pays_exactly_once_to_the_killer_in_reach()
    {
        WalkTo(-36f, -21f);
        AttackResultDto r;
        do r = Swing("meadow_s1", -21f); while (!r.killed);
        long before = Gold();
        string drop = r.drop!.id;

        // too far
        clock += 300;
        Assert.Equal("too_far", Assert.Throws<GameError>(() => farm.Pickup(acc, sword.characterId, new PickupRequestDto { dropId = drop, x = -18f, y = 0 })).Code);
        // another character cannot take it
        string acc2 = accounts.Register("farmer02", "farmer-pass-02").AccountId;
        var other = chars.Create(acc2, "Hàng Xóm", 3, 0);
        clock += 2000;
        chars.Travel(acc2, other.characterId, "hub_east_meadow", 66f, 0f);
        Assert.Equal("not_your_drop", Assert.Throws<GameError>(() => farm.Pickup(acc2, other.characterId, new PickupRequestDto { dropId = drop, x = -22f, y = 0 })).Code);

        clock += 300;
        PickupResultDto p = farm.Pickup(acc, sword.characterId, new PickupRequestDto { dropId = drop, x = -21.5f, y = 0 });
        Assert.Equal(r.drop.amount, p.amount);
        Assert.Equal(before + r.drop.amount, p.gold);
        Assert.Equal(before + r.drop.amount, Gold());
        // replay / double click: gone, nothing more paid
        clock += 100;
        Assert.Equal("drop_gone", Assert.Throws<GameError>(() => farm.Pickup(acc, sword.characterId, new PickupRequestDto { dropId = drop, x = -21.5f, y = 0 })).Code);
        Assert.Equal(before + r.drop.amount, Gold());
    }

    [Fact]
    public void Concurrent_pickups_of_one_drop_pay_once()
    {
        WalkTo(-36f, -21f);
        AttackResultDto r;
        do r = Swing("meadow_s1", -21f); while (!r.killed);
        long before = Gold();
        clock += 300;
        int ok = 0;
        Parallel.For(0, 8, _ =>
        {
            try { farm.Pickup(acc, sword.characterId, new PickupRequestDto { dropId = r.drop!.id, x = -21.5f, y = 0 }); Interlocked.Increment(ref ok); }
            catch (GameError) { }
        });
        Assert.Equal(1, ok);
        Assert.Equal(before + r.drop!.amount, Gold());
    }

    [Fact]
    public void Forged_positions_range_and_speed_are_refused()
    {
        WalkTo(-36f, -30f);
        // teleport next to a far monster: refused (can't run 50 m in 0.9 s)
        Assert.Equal("moved_too_fast", Assert.Throws<GameError>(() => Swing("meadow_s7", 62f)).Code);
        // out of the sword's reach
        Assert.Equal("out_of_range", Assert.Throws<GameError>(() => Swing("meadow_s1", -26f)).Code);
        // a monster on another map / unknown
        Assert.Equal("monster_unknown", Assert.Throws<GameError>(() => Swing("nope", -30f)).Code);
    }

    [Fact]
    public void Swings_faster_than_the_class_allows_are_refused()
    {
        WalkTo(-36f, -21f);
        Swing("meadow_s1", -21f);
        clock += 200; // Kiếm Sĩ needs ~0.85 s between swings
        Assert.Equal("too_fast", Assert.Throws<GameError>(() => farm.Attack(acc, sword.characterId, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 })).Code);
    }

    [Fact]
    public void Hub_has_no_monsters_to_fight()
    {
        string acc3 = accounts.Register("farmer03", "farmer-pass-03").AccountId;
        var stay = chars.Create(acc3, "Ở Nhà", 2, 0); // still in the village
        Assert.Empty(farm.State(acc3, stay.characterId).monsters);
        Assert.Equal("wrong_map", Assert.Throws<GameError>(() => farm.Attack(acc3, stay.characterId, new AttackRequestDto { spawnId = "meadow_s1", x = -22f, y = 0 })).Code);
    }
}
