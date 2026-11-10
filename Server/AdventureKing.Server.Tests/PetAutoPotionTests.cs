using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Pet auto-potion: player settings only; the server decides every drink (threshold, fed pet, cooldown, bag).</summary>
public sealed class PetAutoPotionTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private readonly PetService pets;
    private long clock = 1_000_000;
    private string acc = "", id = "";

    public PetAutoPotionTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(4), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        pets = new PetService(t.Db, PetConfig.Load(PetServiceTests.PetsPath), inv, () => clock);
        farm.PetTick = pets.Tick; farm.PetCanCollect = pets.CanCollect; farm.PetReach = pets.Config.pickupReach;
        farm.PetAutoPotion = pets.AutoPotion;
    }

    public void Dispose() => t.Dispose();

    private void Swordsman(string login)
    {
        acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        id = chars.Create(acc, "Kiếm Pet " + login[^2..], 0, 0).characterId;   // 140 HP, def 12: Slime Bông hits for 3
        inv.Get(acc, id);                                                       // starter kit granted now (not mid-test)
        using (var c = t.Db.Open()) Database.Exec(c, null, "DELETE FROM bag_slots WHERE character_id=$c", ("$c", id)); // empty bag: only the test's potions
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        for (float x = -36f; x < -21f; x += 4f) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -21f, y = 0 });
    }
    private HeroCombatDto Beat(long ms) { clock += ms; return farm.Presence(acc, id, new PresenceRequestDto { x = -21f, y = 0 }); }
    private void Give(string item, int qty) => t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, item, qty); return 0; });
    private int Count(string item) => inv.Get(acc, id).slots.Where(s => s.itemId == item).Sum(s => s.qty);
    private void PetReady() { pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "auto-" + id }); pets.Summon(acc, id, new PetIdDto { petId = "pet_chick" }); }

    /// <summary>Beats (1.6 s, the slime's wind-up) until HP drops under pct % or a drink happens.</summary>
    private HeroCombatDto BeatUntil(Func<HeroCombatDto, bool> stop, int max = 30)
    {
        HeroCombatDto h = Beat(1600);
        for (int i = 0; i < max && !stop(h); i++) h = Beat(1600);
        return h;
    }

    [Fact]
    public void Settings_are_off_by_default_and_clamped_to_the_server_limits()
    {
        Swordsman("auto01");
        AutoPotionDto a = pets.Get(acc, id).autoPotion;
        Assert.False(a.hpEnabled); Assert.False(a.mpEnabled);
        Assert.Equal(8f, a.cooldownSeconds);
        a = pets.SetAutoPotion(acc, id, new AutoPotionDto { hpEnabled = true, hpPercent = 250, mpEnabled = true, mpPercent = -5 }).autoPotion;
        Assert.True(a.hpEnabled && a.mpEnabled);
        Assert.Equal(90, a.hpPercent);
        Assert.Equal(10, a.mpPercent);
        string other = accounts.Register("auto01b", "Kh0ng-Trung-Ten-2026").AccountId;
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => pets.SetAutoPotion(other, id, new AutoPotionDto { hpEnabled = true })).Code);
    }

    [Fact]
    public void A_fed_pet_gives_one_bag_potion_under_the_threshold_then_waits_its_cooldown()
    {
        Swordsman("auto02");
        Give("potion_hp_s", 3);
        Give("petfood_basic", 1); Give("potion_mp_s", 1); // must never be used for HP
        pets.SetAutoPotion(acc, id, new AutoPotionDto { hpEnabled = true, hpPercent = 90 });
        farm.Attack(acc, id, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 }); // aggro: 3 dmg per strike
        // no pet yet: HP falls under 90 % and nothing is drunk
        HeroCombatDto h = BeatUntil(x => x.hp < 126);
        Assert.True(h.hp < 126);
        Assert.Null(h.petDrink);
        Assert.Equal(3, Count("potion_hp_s"));

        PetReady();
        h = Beat(100);
        Assert.NotNull(h.petDrink);
        Assert.Equal("potion_hp_s", h.petDrink!.itemId);
        Assert.Equal(140, h.hp);                                // healed to full, never above
        Assert.True(h.petDrink.hp is > 0 and < 60, "only the missing HP is restored");
        Assert.Equal(2, h.petDrink.left);
        Assert.Equal(2, Count("potion_hp_s"));

        // cooldown 8 s: hits under the threshold within it -> no second drink
        long drankAt = clock;
        h = BeatUntil(x => x.hp < 126 || x.petDrink != null);
        while (clock - drankAt < 8000 && h.petDrink == null) { Assert.Null(h.petDrink); h = Beat(500); }
        Assert.True(clock - drankAt >= 8000 || h.petDrink == null);
        h = h.petDrink != null ? h : BeatUntil(x => x.petDrink != null, 10);
        Assert.NotNull(h.petDrink);
        Assert.True(clock - drankAt >= 8000, "second drink only after the cooldown");
        Assert.Equal(1, Count("potion_hp_s"));
        // third drink empties the HP potions; after that HP stays low and nothing else is taken from the bag
        h = BeatUntil(x => x.petDrink != null, 20);
        Assert.Equal("potion_hp_s", h.petDrink?.itemId);
        Assert.Equal(0, h.petDrink!.left);
        h = BeatUntil(x => x.hp < 126);
        for (int i = 0; i < 8; i++) { Assert.Null(h.petDrink); h = Beat(1600); }
        Assert.True(h.hp < 126, "still under the threshold");
        Assert.Equal(1, Count("petfood_basic"));
        Assert.Equal(1, Count("potion_mp_s"));
    }

    [Fact]
    public void No_drink_when_disabled_hungry_dismissed_or_out_of_potions_and_mp_uses_mp_potions()
    {
        Swordsman("auto03");
        Give("potion_hp_m", 1); Give("potion_hp_l", 1); Give("potion_mp_s", 2);
        PetReady();
        farm.Attack(acc, id, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 });
        // disabled (default): nothing
        HeroCombatDto h = BeatUntil(x => x.hp < 126);
        Assert.Null(h.petDrink);
        // hungry: nothing
        using (var c = t.Db.Open()) Database.Exec(c, null, "UPDATE pets_owned SET satiety_ms=0 WHERE character_id=$c", ("$c", id));
        pets.SetAutoPotion(acc, id, new AutoPotionDto { hpEnabled = true, hpPercent = 90 });
        Assert.Null(Beat(100).petDrink);
        // dismissed: nothing
        using (var c = t.Db.Open()) Database.Exec(c, null, "UPDATE pets_owned SET satiety_ms=3600000 WHERE character_id=$c", ("$c", id));
        pets.Dismiss(acc, id);
        Assert.Null(Beat(100).petDrink);
        // summoned + fed: picks the potion that fits best (missing < 150 -> the smallest, Thuốc HP vừa, not the 400 one)
        pets.Summon(acc, id, new PetIdDto { petId = "pet_chick" });
        h = Beat(100);
        Assert.Equal("potion_hp_m", h.petDrink?.itemId);
        Assert.Equal(1, Count("potion_hp_l"));
        // MP: Thiên Kiếm costs 45 MP -> under 90 % -> an MP potion (HP potions are never used for MP)
        pets.SetAutoPotion(acc, id, new AutoPotionDto { hpEnabled = false, mpEnabled = true, mpPercent = 90 });
        clock += 1000;
        SkillResultDto sk = farm.Skill(acc, id, new SkillRequestDto { slot = 3, x = -21f, y = 0, facing = 1, spawnId = "meadow_s1" });
        Assert.True(sk.hero.mp * 100 < 90 * sk.hero.maxMp);
        h = Beat(100);
        Assert.Equal("potion_mp_s", h.petDrink?.itemId);
        Assert.True(h.petDrink!.mp > 0);
        Assert.Equal(1, Count("potion_mp_s"));
        Assert.Equal(1, Count("potion_hp_l"));
    }
}
