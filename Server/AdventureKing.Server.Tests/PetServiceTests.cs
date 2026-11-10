using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Pets: buy once (Gold / Diamonds), one summoned, satiety from active time only, food, pet pickup rules.</summary>
public sealed class PetServiceTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private readonly PetService pets;
    private long clock = 1_000_000;

    public static string PetsPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "pets.json");

    public PetServiceTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(4), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        pets = new PetService(t.Db, PetConfig.Load(PetsPath), inv, () => clock);
        farm.PetTick = pets.Tick; farm.PetCanCollect = pets.CanCollect; farm.PetReach = pets.Config.pickupReach;
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, int heroClass = 1)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Chủ Pet " + login.Substring(login.Length - 2), heroClass, 0).characterId);
    }
    private long Gold(string id) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", id)); }

    [Fact]
    public void Pets_are_bought_once_with_the_right_currency_and_never_charged_twice()
    {
        var (acc, id) = Hero("pet01");
        PetStateDto s = pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "p-1" });
        Assert.Equal(5000 - 2500, Gold(id));
        Assert.Single(s.owned);
        Assert.Equal(60 * 60_000L, s.owned[0].satietyMs);
        Assert.Equal("pet_owned", Assert.Throws<GameError>(() => pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "p-2" })).Code);
        Assert.Equal("pet_owned", Assert.Throws<GameError>(() => pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "p-1" })).Code);
        Assert.Equal(5000 - 2500, Gold(id));
        // VIP pets need Diamonds (none: payments are off) -> refused, nothing changes
        Assert.Equal("insufficient_diamonds", Assert.Throws<GameError>(() => pets.Buy(acc, id, new PetBuyDto { petId = "pet_jade_dragon", requestId = "p-3" })).Code);
        Assert.Equal("insufficient_gold", Assert.Throws<GameError>(() => pets.Buy(acc, id, new PetBuyDto { petId = "pet_cloud_cat", requestId = "p-4" })).Code); // 2500 left < 3000
        Assert.Single(pets.Get(acc, id).owned);
        Assert.Equal("pet_unknown", Assert.Throws<GameError>(() => pets.Buy(acc, id, new PetBuyDto { petId = "nope", requestId = "p-5" })).Code);
    }

    [Fact]
    public void Satiety_drains_only_while_summoned_and_active_and_food_never_overfills()
    {
        var (acc, id) = Hero("pet02");
        pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "a" });
        // not summoned: no drain
        for (int i = 0; i < 10; i++) { clock += 2000; pets.Tick(id); }
        Assert.Equal(3_600_000, pets.Get(acc, id).owned[0].satietyMs);
        pets.Summon(acc, id, new PetIdDto { petId = "pet_chick" });
        pets.Tick(id);
        for (int i = 0; i < 300; i++) { clock += 2000; pets.Tick(id); }  // 10 minutes of activity
        Assert.Equal(3_600_000 - 600_000, pets.Get(acc, id).owned[0].satietyMs);
        // offline / AFK for an hour: only the 5 s cap counts
        clock += 3_600_000; pets.Tick(id);
        Assert.Equal(3_000_000 - 5_000, pets.Get(acc, id).owned[0].satietyMs);
        // keep going until hungry: stops at 0, the pet can no longer collect
        for (int i = 0; i < 2000; i++) { clock += 2000; pets.Tick(id); }
        OwnedPetDto p = pets.Get(acc, id).owned[0];
        Assert.Equal(0, p.satietyMs);
        Assert.True(p.hungry);
        Assert.False(pets.CanCollect(id));
        // food from the bag: +15 min each, never above 60
        t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, "petfood_basic", 6); inv.Add(c, tx, id, "potion_hp_s", 1); return 0; });
        InventoryDto bag = inv.Get(acc, id);
        int food = bag.slots.First(s => s.itemId == "petfood_basic").index, potion = bag.slots.First(s => s.itemId == "potion_hp_s").index;
        Assert.Equal("not_food", Assert.Throws<GameError>(() => pets.Feed(acc, id, new PetFeedDto { petId = "pet_chick", slot = potion })).Code);
        for (int i = 0; i < 4; i++) pets.Feed(acc, id, new PetFeedDto { petId = "pet_chick", slot = food });
        Assert.Equal(3_600_000, pets.Get(acc, id).owned[0].satietyMs);
        Assert.Equal("pet_full", Assert.Throws<GameError>(() => pets.Feed(acc, id, new PetFeedDto { petId = "pet_chick", slot = food })).Code);
        Assert.Equal(2, inv.Get(acc, id).slots.Where(s => s.itemId == "petfood_basic").Sum(s => s.qty)); // 6 - 4, the refused one kept
        Assert.True(pets.CanCollect(id));
    }

    [Fact]
    public void Only_one_pet_is_summoned_and_dismiss_stops_collecting()
    {
        var (acc, id) = Hero("pet03");
        t.Db.InTransaction((c, tx) => { Wallet.Gold(c, tx, acc, id, 5000, "test", "test-gold-" + id); return 0; });
        pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "a" });
        pets.Buy(acc, id, new PetBuyDto { petId = "pet_cloud_cat", requestId = "b" });
        Assert.Equal("pet_chick", pets.Summon(acc, id, new PetIdDto { petId = "pet_chick" }).summoned);
        PetStateDto s = pets.Summon(acc, id, new PetIdDto { petId = "pet_cloud_cat" });
        Assert.Equal("pet_cloud_cat", s.summoned);
        Assert.Single(s.owned, o => o.summoned);
        Assert.True(pets.CanCollect(id));
        pets.Dismiss(acc, id);
        Assert.False(pets.CanCollect(id));
        Assert.Equal("pet_not_owned", Assert.Throws<GameError>(() => pets.Summon(acc, id, new PetIdDto { petId = "pet_fire_fox" })).Code);
    }

    [Fact]
    public void A_fed_summoned_pet_collects_only_the_owners_drop_within_reach_and_only_once()
    {
        var (acc, id) = Hero("pet04", 0);
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        for (float x = -36f; x < -21f; x += 4f) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -21f, y = 0 });
        AttackResultDto hit = null!;
        for (int i = 0; i < 6 && (hit == null || !hit.killed); i++) { clock += 900; hit = farm.Attack(acc, id, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 }); }
        Assert.True(hit.killed);
        string drop = hit.drop!.id;
        // walk 6 m away: too far to pick up by hand, but inside the pet's reach
        for (float x = -21f; x > -27f; x -= 2f) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500;
        Assert.Equal("too_far", Assert.Throws<GameError>(() => farm.Pickup(acc, id, new PickupRequestDto { dropId = drop, x = -27f, y = 0 })).Code);
        Assert.Equal("pet_unavailable", Assert.Throws<GameError>(() => farm.PetPickup(acc, id, new PickupRequestDto { dropId = drop, x = -27f, y = 0 })).Code);
        pets.Buy(acc, id, new PetBuyDto { petId = "pet_chick", requestId = "c" });
        pets.Summon(acc, id, new PetIdDto { petId = "pet_chick" });
        long before = Gold(id);
        clock += 500;
        PickupResultDto got = farm.PetPickup(acc, id, new PickupRequestDto { dropId = drop, x = -27f, y = 0 });
        Assert.Equal(before + got.amount, Gold(id));
        Assert.InRange(got.amount, 5, 11); // the slime's own roll: the pet adds nothing
        clock += 500;
        Assert.Equal("drop_gone", Assert.Throws<GameError>(() => farm.PetPickup(acc, id, new PickupRequestDto { dropId = drop, x = -27f, y = 0 })).Code);
        // another player's pet cannot take my drops
        var (acc2, other) = Hero("pet05", 0);
        Assert.Equal("pet_unavailable", Assert.Throws<GameError>(() => farm.PetPickup(acc2, other, new PickupRequestDto { dropId = drop, x = 0, y = 0 })).Code);
    }
}
