using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Cường Hóa Trang Bị +1…+15: rules table, costs, success / failure / drop / break, Bùa Bảo Vệ, refusals that consume nothing, the level
/// travelling with the gear, and the effect on stats and Lực Chiến. The random source is scripted so every branch is exact.</summary>
public sealed class EnhanceTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private readonly Queue<double> rolls = new();
    private readonly EnhanceService enhance;
    private long clock = 1_767_225_600_000;

    public EnhanceTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 0) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(5), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        enhance = new EnhanceService(t.Db, inv, () => rolls.Count > 0 ? rolls.Dequeue() : throw new InvalidOperationException("unscripted roll"));
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Cường Hóa " + login[^2..], 0, 0).characterId);
    }

    private void Give(string id, string item, int qty) => t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, item, qty); return 0; });
    private void SetGold(string id, long g) { using var c = t.Db.Open(); Database.Exec(c, null, "UPDATE characters SET gold=$g WHERE id=$c", ("$g", g), ("$c", id)); }
    private long Gold(string id) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", id)); }
    private void SetLevel(string id, string item, int level) { using var c = t.Db.Open(); Database.Exec(c, null, "UPDATE bag_slots SET enhance=$l WHERE character_id=$c AND item_id=$i", ("$l", level), ("$c", id), ("$i", item)); }
    private int Count(string acc, string id, string item) => inv.Get(acc, id).slots.Where(s => s.itemId == item).Sum(s => s.qty);
    private int SlotOf(string acc, string id, string item) => inv.Get(acc, id).slots.First(s => s.itemId == item).index;
    private EnhanceResultDto Hit(string acc, string id, string item, bool charm = false, params double[] script)
    {
        foreach (double r in script) rolls.Enqueue(r);
        return enhance.Enhance(acc, id, new EnhanceRequestDto { where = "bag", slot = SlotOf(acc, id, item), useCharm = charm });
    }

    /// <summary>A hero with a +level gold blade in the bag and plenty of stones / Gold / charms.</summary>
    private (string acc, string id) Smith(string login, int level, int stones = 60, int charms = 0)
    {
        var (acc, id) = Hero(login);
        Give(id, "gear_bee_charm", 1);
        if (level > 0) SetLevel(id, "gear_bee_charm", level);
        if (stones > 0) Give(id, "enhance_stone", stones);
        if (charms > 0) Give(id, "protect_charm", charms);
        SetGold(id, 1_000_000);
        return (acc, id);
    }

    [Fact]
    public void Rules_table_gets_harder_and_dearer_and_the_stat_bonus_grows_to_plus_15()
    {
        for (int l = 1; l < EnhanceRules.MaxLevel; l++)
        {
            Assert.True(EnhanceRules.SuccessPercent(l) <= EnhanceRules.SuccessPercent(l - 1), $"success rate never rises (+{l})");
            Assert.True(EnhanceRules.StonesFor(l) >= EnhanceRules.StonesFor(l - 1));
            Assert.True(EnhanceRules.GoldFor(l) > EnhanceRules.GoldFor(l - 1));
        }
        Assert.Equal(100, EnhanceRules.SuccessPercent(0)); Assert.Equal(20, EnhanceRules.SuccessPercent(14));
        Assert.True(EnhanceRules.Scaled(10, 15) >= 2 * 10, "+15 more than doubles the base");
        for (int l = 1; l <= 15; l++) Assert.True(EnhanceRules.Scaled(2, l) > EnhanceRules.Scaled(2, l - 1), "every level is a visible gain");
        Assert.Equal(0, EnhanceRules.Scaled(0, 9)); Assert.Equal(7, EnhanceRules.Scaled(7, 0));
        var items = ItemConfig.Load(InventoryServiceTests.ItemsPath);
        Assert.Equal("material", items.Get(EnhanceRules.StoneId)!.kind); Assert.Equal("purple", items.Get(EnhanceRules.CharmId)!.rarity);
        Assert.Contains(items.shops.SelectMany(s => s.tabs).Where(tb => tb.id == "enhance"), tb => tb.items.Contains(EnhanceRules.StoneId) && tb.items.Contains(EnhanceRules.CharmId));
    }

    [Fact]
    public void Success_takes_stones_and_gold_raises_the_level_and_the_stats()
    {
        var (acc, id) = Smith("enh01", 0);
        long gold0 = Gold(id); int stones0 = Count(acc, id, "enhance_stone");
        EnhanceResultDto r = Hit(acc, id, "gear_bee_charm", false, 0.0);
        Assert.Equal("success", r.outcome); Assert.Equal(0, r.levelBefore); Assert.Equal(1, r.levelAfter);
        Assert.Equal(1, r.stonesUsed); Assert.Equal(100, r.goldSpent);
        Assert.Equal(gold0 - 100, Gold(id)); Assert.Equal(r.gold, Gold(id));
        Assert.Equal(stones0 - 1, Count(acc, id, "enhance_stone"));
        var blade = r.inventory.slots.Single(s => s.itemId == "gear_bee_charm");
        Assert.Equal(1, blade.enhance);
        Assert.Equal(EnhanceRules.Scaled(4, 1), blade.attack);
        // walk up to +5: every step succeeds
        for (int i = 1; i < 5; i++) Assert.Equal("success", Hit(acc, id, "gear_bee_charm", false, 0.0).outcome);
        Assert.Equal(5, inv.Get(acc, id).slots.Single(s => s.itemId == "gear_bee_charm").enhance);
        Assert.Equal(EnhanceRules.Scaled(4, 5), inv.Get(acc, id).slots.Single(s => s.itemId == "gear_bee_charm").attack);
    }

    [Fact]
    public void Failure_up_to_plus_5_costs_only_the_materials()
    {
        var (acc, id) = Smith("enh02", 3);
        long gold0 = Gold(id); int stones0 = Count(acc, id, "enhance_stone");
        EnhanceResultDto r = Hit(acc, id, "gear_bee_charm", false, 0.99);
        Assert.Equal("fail", r.outcome); Assert.Equal(3, r.levelAfter);
        Assert.Equal(gold0 - EnhanceRules.GoldFor(3), Gold(id)); Assert.Equal(stones0 - EnhanceRules.StonesFor(3), Count(acc, id, "enhance_stone"));
        Assert.Equal(3, inv.Get(acc, id).slots.Single(s => s.itemId == "gear_bee_charm").enhance);
    }

    [Fact]
    public void From_plus_6_a_failure_drops_one_level_unless_a_charm_protects_it_and_a_charm_is_only_spent_when_it_protects()
    {
        var (acc, id) = Smith("enh03", 7, charms: 3);
        // unprotected failure: -1
        EnhanceResultDto drop = Hit(acc, id, "gear_bee_charm", false, 0.99);
        Assert.Equal("drop", drop.outcome); Assert.Equal(6, drop.levelAfter); Assert.False(drop.charmUsed);
        Assert.Equal(3, Count(acc, id, "protect_charm"));
        // protected failure: level kept, one charm gone
        EnhanceResultDto kept = Hit(acc, id, "gear_bee_charm", true, 0.99);
        Assert.Equal("protected", kept.outcome); Assert.Equal(6, kept.levelAfter); Assert.True(kept.charmUsed);
        Assert.Equal(2, Count(acc, id, "protect_charm"));
        // protected success: the charm stays in the bag
        EnhanceResultDto won = Hit(acc, id, "gear_bee_charm", true, 0.0);
        Assert.Equal("success", won.outcome); Assert.False(won.charmUsed); Assert.Equal(2, Count(acc, id, "protect_charm"));
        Assert.Equal(7, won.levelAfter);
        // below the safe line a charm is not needed and not spent
        var (acc2, id2) = Smith("enh04", 2, charms: 1);
        Assert.Equal("fail", Hit(acc2, id2, "gear_bee_charm", true, 0.99).outcome);
        Assert.Equal(1, Count(acc2, id2, "protect_charm"));
    }

    [Fact]
    public void From_plus_10_an_unprotected_failure_can_shatter_the_gear_and_a_charm_prevents_it()
    {
        var (acc, id) = Smith("enh05", 12, charms: 2);
        // fail, then the 25 % break roll hits (0.10 < 0.25): the blade is gone
        EnhanceResultDto broke = Hit(acc, id, "gear_bee_charm", false, 0.99, 0.10);
        Assert.Equal("broken", broke.outcome);
        Assert.Equal(0, Count(acc, id, "gear_bee_charm"));
        Assert.Equal(2, Count(acc, id, "protect_charm"));
        // with a charm the same two bad rolls break nothing
        var (acc2, id2) = Smith("enh06", 12, charms: 1);
        EnhanceResultDto safe = Hit(acc2, id2, "gear_bee_charm", true, 0.99);
        Assert.Equal("protected", safe.outcome); Assert.Equal(12, safe.levelAfter); Assert.Equal(1, Count(acc2, id2, "gear_bee_charm")); Assert.Equal(0, Count(acc2, id2, "protect_charm"));
        // fail and the break roll misses: only a level is lost
        var (acc3, id3) = Smith("enh07", 12);
        EnhanceResultDto lost = Hit(acc3, id3, "gear_bee_charm", false, 0.99, 0.90);
        Assert.Equal("drop", lost.outcome); Assert.Equal(11, lost.levelAfter); Assert.Equal(1, Count(acc3, id3, "gear_bee_charm"));
        Assert.Empty(rolls);
    }

    [Fact]
    public void Refusals_consume_nothing()
    {
        var (acc, id) = Smith("enh08", 0, stones: 0);
        long gold0 = Gold(id);
        // no stones
        Assert.Equal("no_stone", Assert.Throws<GameError>(() => Hit(acc, id, "gear_bee_charm", false, 0.0)).Code);
        Assert.Equal(gold0, Gold(id));
        Give(id, "enhance_stone", 5);
        // charm asked for but none in the bag
        Assert.Equal("no_charm", Assert.Throws<GameError>(() => Hit(acc, id, "gear_bee_charm", true, 0.0)).Code);
        Assert.Equal(5, Count(acc, id, "enhance_stone")); Assert.Equal(gold0, Gold(id));
        // not enough Gold: the stones stay (the whole attempt rolls back)
        SetGold(id, 50);
        Assert.NotNull(Record.Exception(() => Hit(acc, id, "gear_bee_charm", false, 0.0)));
        Assert.Equal(5, Count(acc, id, "enhance_stone")); Assert.Equal(50, Gold(id));
        rolls.Clear();
        SetGold(id, 10_000);
        // not gear / empty cell / bad target
        Assert.Equal("not_equipment", Assert.Throws<GameError>(() => enhance.Enhance(acc, id, new EnhanceRequestDto { where = "bag", slot = SlotOf(acc, id, "enhance_stone") })).Code);
        Assert.Equal("slot_empty", Assert.Throws<GameError>(() => enhance.Enhance(acc, id, new EnhanceRequestDto { where = "bag", slot = 39 })).Code);
        Assert.Equal("slot_empty", Assert.Throws<GameError>(() => enhance.Enhance(acc, id, new EnhanceRequestDto { where = "equipped", equipSlot = "accessory" })).Code);
        Assert.Equal("bad_request", Assert.Throws<GameError>(() => enhance.Enhance(acc, id, new EnhanceRequestDto { where = "sky" })).Code);
        // max level
        SetLevel(id, "gear_bee_charm", 15);
        Assert.Equal("max_level", Assert.Throws<GameError>(() => Hit(acc, id, "gear_bee_charm", false, 0.0)).Code);
        // somebody else's character
        var (acc2, _) = Hero("enh09");
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => enhance.Enhance(acc2, id, new EnhanceRequestDto { where = "bag", slot = 0 })).Code);
        Assert.Equal(5, Count(acc, id, "enhance_stone")); Assert.Equal(10_000, Gold(id));
    }

    [Fact]
    public void The_level_travels_with_the_gear_between_bag_and_equipment_and_raises_combat_power()
    {
        var (acc, id) = Smith("enh10", 0);
        for (int i = 0; i < 5; i++) Hit(acc, id, "gear_bee_charm", false, 0.0);                 // +5
        InventoryDto bag = inv.Get(acc, id);
        int cell = bag.slots.Single(s => s.itemId == "gear_bee_charm").index;
        InventoryDto worn = inv.Equip(acc, id, new EquipRequestDto { slot = cell });
        var w = worn.equipped.Single(e => e.equipSlot == "accessory").item;
        Assert.Equal(5, w.enhance); Assert.Equal(EnhanceRules.Scaled(4, 5), w.attack);
        Assert.Equal(EnhanceRules.Scaled(4, 5), worn.attackBonus);
        Assert.Equal(EnhanceRules.Scaled(1, 5), worn.defenseBonus);
        Assert.Equal(FarmService.PowerOf(0, 1, EnhanceRules.Scaled(4, 5), EnhanceRules.Scaled(1, 5)), worn.combatPower);
        Assert.Equal(farm.CombatPower(id, 0, 1), worn.combatPower);
        // enhance the worn piece in place
        foreach (double r in new[] { 0.0 }) rolls.Enqueue(r);
        EnhanceResultDto up = enhance.Enhance(acc, id, new EnhanceRequestDto { where = "equipped", equipSlot = "accessory" });
        Assert.Equal("success", up.outcome); Assert.Equal(6, up.levelAfter);
        Assert.Equal(EnhanceRules.Scaled(4, 6), up.inventory.attackBonus);
        Assert.True(up.inventory.combatPower > worn.combatPower);
        // swapping in a plain +0 copy puts the +6 one back in the bag with its level
        Give(id, "gear_bee_charm", 1);
        int fresh = inv.Get(acc, id).slots.First(s => s.itemId == "gear_bee_charm").index;
        InventoryDto swapped = inv.Equip(acc, id, new EquipRequestDto { slot = fresh });
        Assert.Equal(0, swapped.equipped.Single(e => e.equipSlot == "accessory").item.enhance);
        Assert.Equal(6, swapped.slots.Single(s => s.itemId == "gear_bee_charm").enhance);
        // taking it off keeps the level too
        InventoryDto off = inv.Unequip(acc, id, new UnequipRequestDto { equipSlot = "accessory" });
        Assert.Equal(new[] { 0, 6 }, off.slots.Where(s => s.itemId == "gear_bee_charm").Select(s => s.enhance).OrderBy(x => x).ToArray());
    }
}
