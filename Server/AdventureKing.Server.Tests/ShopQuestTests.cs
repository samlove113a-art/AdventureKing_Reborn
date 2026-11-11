using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Healer shop (Gold, 1–99, bag checked first, replay-safe), tickets (atomic, real maps only), login to hub, quests.</summary>
public sealed class ShopQuestTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private readonly ShopService shop;
    private readonly QuestService quests;
    private long clock = 1_767_225_600_000; // 2026-01-01 00:00 UTC = 07:00 VN

    public static string QuestsPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "quests.json");

    public ShopQuestTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(9), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        shop = new ShopService(t.Db, inv, () => clock);
        quests = new QuestService(t.Db, QuestConfig.Load(QuestsPath, inv.Config), inv, () => clock);
        Action<string, string, string, int> rec = (a, c, e, n) => quests.Record(a, c, e, n);
        farm.QuestEvent = rec; inv.QuestEvent = rec; shop.QuestEvent = rec; chars.QuestEvent = rec;
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, int heroClass = 0)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        var c = chars.Create(acc, "Thử Nghiệm " + login.Substring(login.Length - 2), heroClass, 0);
        return (acc, c.characterId);
    }

    private long Gold(string id) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", id)); }
    private int Count(InventoryDto d, string item) => d.slots.Where(s => s.itemId == item).Sum(s => s.qty);
    private BuyResultDto Buy(string acc, string id, string item, int qty, string req) => shop.Buy(acc, id, new BuyRequestDto { shopId = "healer", itemId = item, qty = qty, requestId = req });

    [Fact]
    public void Buying_charges_exactly_once_checks_qty_map_bag_and_gold_first()
    {
        var (acc, id) = Hero("shop01");
        inv.Get(acc, id); // starter kit: 10 HP small
        BuyResultDto r = Buy(acc, id, "potion_hp_s", 20, "req-1");     // 20 x 30 = 600
        Assert.Equal(600, r.totalGold);
        Assert.Equal(5000 - 600, Gold(id));
        Assert.Equal(30, Count(r.inventory, "potion_hp_s"));
        // the same request again (double click / retry): nothing charged, nothing delivered
        BuyResultDto again = Buy(acc, id, "potion_hp_s", 20, "req-1");
        Assert.True(again.replayed);
        Assert.Equal(5000 - 600, Gold(id));
        Assert.Equal(30, Count(again.inventory, "potion_hp_s"));
        Assert.Equal("bad_qty", Assert.Throws<GameError>(() => Buy(acc, id, "potion_hp_s", 100, "req-2")).Code);
        Assert.Equal("bad_qty", Assert.Throws<GameError>(() => Buy(acc, id, "potion_hp_s", 0, "req-3")).Code);
        Assert.Equal("not_sold_here", Assert.Throws<GameError>(() => Buy(acc, id, "gear_novice_blade", 1, "req-4")).Code);
        // not enough Gold: refused, no items
        Assert.Equal("insufficient_gold", Assert.Throws<GameError>(() => Buy(acc, id, "potion_hp_l", 99, "req-5")).Code); // 25 740
        Assert.Equal(5000 - 600, Gold(id));
        Assert.Equal(0, Count(inv.Get(acc, id), "potion_hp_l"));
        // away from the shop's map: refused
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        Assert.Equal("shop_far", Assert.Throws<GameError>(() => Buy(acc, id, "potion_hp_s", 1, "req-6")).Code);
    }

    [Fact]
    public void A_full_bag_refuses_before_any_gold_moves()
    {
        var (acc, id) = Hero("shop02");
        InventoryDto d = inv.Get(acc, id);
        t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, "potion_mp_l", (d.capacity - d.slots.Length) * 99); return 0; });
        long before = Gold(id);
        Assert.Equal("bag_full", Assert.Throws<GameError>(() => Buy(acc, id, "ticket_meadow", 1, "full-1")).Code);
        Assert.Equal(before, Gold(id));
        // topping up an existing stack still fits
        Buy(acc, id, "potion_hp_s", 5, "full-2");
        Assert.Equal(before - 150, Gold(id));
    }

    [Fact]
    public void Ticket_travel_removes_one_ticket_and_moves_atomically_and_failures_keep_the_ticket()
    {
        var (acc, id) = Hero("ticket01");
        inv.Get(acc, id);
        InventoryDto d = Buy(acc, id, "ticket_meadow", 2, "t-1").inventory;
        int slot = d.slots.First(s => s.itemId == "ticket_meadow").index;
        TicketResultDto r = shop.UseTicket(acc, id, new TicketRequestDto { slot = slot });
        Assert.Equal("meadow_breeze", r.character.lastMapId);
        Assert.Equal(-36f, r.character.posX);
        Assert.Equal(1, Count(r.inventory, "ticket_meadow"));
        // already there: refused, ticket kept
        Assert.Equal("already_there", Assert.Throws<GameError>(() => shop.UseTicket(acc, id, new TicketRequestDto { slot = slot })).Code);
        Assert.Equal(1, Count(inv.Get(acc, id), "ticket_meadow"));
        // a potion is not a ticket
        int potion = inv.Get(acc, id).slots.First(s => s.itemId == "potion_hp_s").index;
        Assert.Equal("not_ticket", Assert.Throws<GameError>(() => shop.UseTicket(acc, id, new TicketRequestDto { slot = potion })).Code);
        // every ticket in the catalog leads to a map the server really has
        Assert.All(inv.Config.items.Where(i => i.kind == "ticket"), i => Assert.True(CharacterService.Maps.ContainsKey(i.destMap)));
    }

    [Fact]
    public void Logging_in_again_puts_the_hero_in_the_hub_with_everything_kept()
    {
        var (acc, id) = Hero("login01");
        inv.Get(acc, id);
        Buy(acc, id, "potion_mp_s", 3, "l-1");
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        chars.SaveLocation(acc, id, "meadow_breeze", 10f, 0f);
        long gold = Gold(id);
        CharacterDto back = chars.Enter(acc, id);
        Assert.Equal(CharacterService.StartMap, back.lastMapId);
        Assert.False(back.hasPosition);                                        // the hub's safe spawn
        Assert.Equal(gold, back.gold);
        Assert.Equal(13, Count(inv.Get(acc, id), "potion_mp_s"));
        // already in the hub: the saved hub position is kept
        chars.SaveLocation(acc, id, CharacterService.StartMap, 20f, 0f);
        Assert.True(chars.Enter(acc, id).hasPosition);
    }

    [Fact]
    public void Main_chain_advances_one_quest_at_a_time_from_server_events_and_pays_once()
    {
        var (acc, id) = Hero("quest01", 1);
        QuestLogDto log = quests.Log(acc, id);
        Assert.Equal("main_01", log.main.Single().id);
        Assert.Equal(16, log.mainTotal); // 10 starter quests + 3 Chương 1 Map 1 + 3 Map 2 quests
        Assert.Equal("quest_incomplete", Assert.Throws<GameError>(() => quests.Claim(acc, id, new QuestClaimDto { questId = "main_01" })).Code);
        Assert.Equal("quest_locked", Assert.Throws<GameError>(() => quests.Claim(acc, id, new QuestClaimDto { questId = "main_02" })).Code);
        log = quests.ClientEvent(acc, id, new QuestEventDto { @event = "talk:elder" });
        Assert.True(log.main.Single().complete);
        Assert.Equal("Trưởng Làng", log.turnInNpc);                          // "?" over the Elder
        QuestClaimResultDto c1 = quests.Claim(acc, id, new QuestClaimDto { questId = "main_01" });
        Assert.Equal(20, c1.exp);
        Assert.Equal("main_02", c1.log.main.Single().id);
        Assert.Equal("already_claimed", Assert.Throws<GameError>(() => quests.Claim(acc, id, new QuestClaimDto { questId = "main_01" })).Code);
        // the client cannot report server events
        Assert.Equal("event_not_allowed", Assert.Throws<GameError>(() => quests.ClientEvent(acc, id, new QuestEventDto { @event = "kill:puff_slime" })).Code);
        // practice x3 (capped), then three skills
        for (int i = 0; i < 5; i++) quests.ClientEvent(acc, id, new QuestEventDto { @event = "practice:attack" });
        Assert.Equal(3, quests.Log(acc, id).main.Single().objectives[0].have);
        quests.Claim(acc, id, new QuestClaimDto { questId = "main_02" });
        foreach (string s in new[] { "practice:skill1", "practice:skill2", "practice:skill3" }) quests.ClientEvent(acc, id, new QuestEventDto { @event = s });
        quests.Claim(acc, id, new QuestClaimDto { questId = "main_03" });
        quests.ClientEvent(acc, id, new QuestEventDto { @event = "talk:healer" });
        quests.Claim(acc, id, new QuestClaimDto { questId = "main_04" });
        // main_05: buy + use come from the real shop / potion services
        inv.Get(acc, id);
        Buy(acc, id, "potion_hp_s", 1, "q-1");
        Assert.False(quests.Log(acc, id).main.Single().complete);
        // (drink needs a missing bar: fight once in the meadow)
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);                     // also completes "travel" later
        for (float x = -36f; x < -27f; x += 4f) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -27f, y = 0 });
        clock += 500; farm.Skill(acc, id, new SkillRequestDto { slot = 3, x = -27f, y = 0, facing = 1, spawnId = "meadow_s1" });
        clock += 1000;
        inv.Use(acc, id, new UseItemRequestDto { slot = inv.Get(acc, id).slots.First(s => s.itemId == "potion_mp_s").index });
        Assert.True(quests.Log(acc, id).main.Single().complete);
        quests.Claim(acc, id, new QuestClaimDto { questId = "main_05" });
        // main_06 travel happened before it was the current quest: the chain only counts the open quest
        Assert.False(quests.Log(acc, id).main.Single().complete);
        // (main_07 kill events: the meteor already killed s1 before main_07 was open -> not counted either)
    }

    [Fact]
    public void Daily_quests_reset_at_vietnam_midnight_and_pay_once_per_day_and_exp_levels_up()
    {
        var (acc, id) = Hero("quest02");
        for (int i = 0; i < 10; i++) quests.Record(acc, id, "pickup:gold");
        QuestDto daily = quests.Log(acc, id).daily.Single(q => q.id == "daily_gold");
        Assert.True(daily.complete);
        long g0 = Gold(id);
        quests.Claim(acc, id, new QuestClaimDto { questId = "daily_gold" });
        Assert.Equal(g0 + 30, Gold(id));
        Assert.Equal("already_claimed", Assert.Throws<GameError>(() => quests.Claim(acc, id, new QuestClaimDto { questId = "daily_gold" })).Code);
        // 16:59 UTC = 23:59 VN: still the same day
        clock += (16 * 60 + 59) * 60_000L;
        Assert.True(quests.Log(acc, id).daily.Single(q => q.id == "daily_gold").claimed);
        // 17:00 UTC = 00:00 VN next day: fresh daily
        clock += 60_000L;
        QuestDto fresh = quests.Log(acc, id).daily.Single(q => q.id == "daily_gold");
        Assert.False(fresh.claimed);
        Assert.Equal(0, fresh.objectives[0].have);
        // side quest pickup counter kept going in parallel (10 of 10)
        Assert.True(quests.Log(acc, id).side.Single(q => q.id == "side_collector").complete);
        // EXP -> level on the server curve (lv2 at 100)
        QuestClaimResultDto r = quests.Claim(acc, id, new QuestClaimDto { questId = "side_collector" }); // +60 exp (+50 daily earlier) = 110
        Assert.Equal(2, r.level);
        Assert.True(r.leveledUp);
    }
}
