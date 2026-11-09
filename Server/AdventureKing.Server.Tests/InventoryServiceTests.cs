using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Server bag: starter kit once, stacks of 99, all-or-nothing adds, potions, equipment and its combat bonus.</summary>
public sealed class InventoryServiceTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private long clock = 1_000_000;

    public static string ItemsPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "items.json");

    public InventoryServiceTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(5), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(ItemsPath), farm, () => clock);
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, int heroClass)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        var c = chars.Create(acc, "Túi Đồ " + login.Substring(login.Length - 2), heroClass, 0);
        return (acc, c.characterId);
    }

    private int Count(InventoryDto d, string item) => d.slots.Where(s => s.itemId == item).Sum(s => s.qty);
    private int Slot(InventoryDto d, string item) => d.slots.First(s => s.itemId == item).index;

    [Fact]
    public void Starter_kit_is_granted_once_with_the_class_weapon_only()
    {
        var (acc, id) = Hero("bag01", 1); // Pháp Sư
        InventoryDto a = inv.Get(acc, id);
        Assert.Equal(10, Count(a, "potion_hp_s"));
        Assert.Equal(10, Count(a, "potion_mp_s"));
        Assert.Equal(1, Count(a, "gear_novice_staff"));
        Assert.Equal(0, Count(a, "gear_novice_blade"));
        Assert.Equal(40, a.capacity);
        InventoryDto b = inv.Get(acc, id);                                 // relog / second device: no second kit
        Assert.Equal(a.slots.Length, b.slots.Length);
        Assert.Equal(10, Count(b, "potion_hp_s"));
    }

    [Fact]
    public void Stacks_cap_at_99_spill_into_new_cells_and_a_full_bag_refuses_the_whole_add()
    {
        var (acc, id) = Hero("bag02", 0);
        inv.Get(acc, id);
        t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, "potion_hp_s", 95); return 0; }); // 10 + 95 = 99 + 6
        InventoryDto d = inv.Get(acc, id);
        Assert.Equal(105, Count(d, "potion_hp_s"));
        Assert.Equal(new[] { 6, 99 }, d.slots.Where(s => s.itemId == "potion_hp_s").Select(s => s.qty).OrderBy(q => q).ToArray());
        Assert.All(d.slots, s => Assert.InRange(s.qty, 1, 99));
        // fill the rest of the bag, then an add that does not fit changes nothing
        int free = d.capacity - d.slots.Length;
        t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, "potion_mp_m", free * 99); return 0; });
        InventoryDto full = inv.Get(acc, id);
        Assert.Equal(full.capacity, full.slots.Length);
        var e = Assert.Throws<GameError>(() => t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, "potion_hp_l", 1); return 0; }));
        Assert.Equal("bag_full", e.Code);
        Assert.Equal(0, Count(inv.Get(acc, id), "potion_hp_l"));
        Assert.False(t.Db.InTransaction((c, tx) => inv.Fits(c, tx, id, "potion_hp_l", 1)));
        Assert.True(t.Db.InTransaction((c, tx) => inv.Fits(c, tx, id, "potion_hp_s", 93))); // tops up the 6-stack
    }

    [Fact]
    public void Potions_restore_up_to_the_max_consume_exactly_one_and_are_refused_when_full_or_too_fast()
    {
        var (acc, id) = Hero("bag03", 1); // Pháp Sư: 75 HP, 140 MP
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        InventoryDto d = inv.Get(acc, id);
        int hpSlot = Slot(d, "potion_hp_s"), mpSlot = Slot(d, "potion_mp_s");
        // full bars: refused, nothing consumed
        Assert.Equal("already_full", Assert.Throws<GameError>(() => inv.Use(acc, id, new UseItemRequestDto { slot = hpSlot })).Code);
        Assert.Equal(10, Count(inv.Get(acc, id), "potion_hp_s"));
        // spend MP on a skill, then drink: +40 MP but never above max
        for (float x = -36f; x < -27f; x += 4f) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -27f, y = 0 });
        clock += 500; farm.Skill(acc, id, new SkillRequestDto { slot = 3, x = -27f, y = 0, facing = 1, spawnId = "meadow_s1" }); // 140 -> 95
        clock += 1000;
        UseItemResultDto r = inv.Use(acc, id, new UseItemRequestDto { slot = mpSlot });
        Assert.Equal(40, r.restoredMp);
        Assert.Equal(9, Count(r.inventory, "potion_mp_s"));
        Assert.Equal(r.hero.mp, Math.Min(140, r.hero.mp));
        // spam: second potion inside the 1 s potion cooldown is refused and costs nothing
        clock += 200;
        Assert.Equal("too_fast", Assert.Throws<GameError>(() => inv.Use(acc, id, new UseItemRequestDto { slot = mpSlot })).Code);
        Assert.Equal(9, Count(inv.Get(acc, id), "potion_mp_s"));
        // MP regenerated back to full (3/s): another potion would be wasted -> refused, nothing consumed
        clock += 1000;
        Assert.Equal(140, farm.Peek(acc, id).mp);
        Assert.Equal("already_full", Assert.Throws<GameError>(() => inv.Use(acc, id, new UseItemRequestDto { slot = mpSlot })).Code);
        Assert.Equal(9, Count(inv.Get(acc, id), "potion_mp_s"));
        // gear is not drinkable
        Assert.Equal("not_usable", Assert.Throws<GameError>(() => inv.Use(acc, id, new UseItemRequestDto { slot = Slot(d, "gear_novice_staff") })).Code);
    }

    [Fact]
    public void Equipment_swaps_through_the_bag_checks_class_and_adds_to_server_damage()
    {
        var (acc, id) = Hero("bag04", 0); // Kiếm Sĩ: 18 dmg
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        InventoryDto d = inv.Get(acc, id);
        int blade = Slot(d, "gear_novice_blade");
        InventoryDto eq = inv.Equip(acc, id, new EquipRequestDto { slot = blade });
        Assert.Equal(4, eq.attackBonus);
        Assert.Equal("gear_novice_blade", eq.equipped.Single(e => e.equipSlot == "weapon").item.itemId);
        Assert.Equal(0, Count(eq, "gear_novice_blade"));                 // moved, not copied
        eq = inv.Equip(acc, id, new EquipRequestDto { slot = Slot(eq, "gear_cloth_vest") });
        Assert.Equal(4, eq.defenseBonus);
        // server combat uses the bonus: 18 + 4 = 22
        for (float x = -36f; x < -21f; x += 4f) { clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0 }); }
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -21f, y = 0 });
        clock += 900;
        Assert.Equal(22, farm.Attack(acc, id, new AttackRequestDto { spawnId = "meadow_s1", x = -21f, y = 0 }).damage);
        // unequip puts it back in the bag
        InventoryDto un = inv.Unequip(acc, id, new UnequipRequestDto { equipSlot = "weapon" });
        Assert.Equal(1, Count(un, "gear_novice_blade"));
        Assert.Equal(0, un.attackBonus);
        // a Pháp Sư cannot wield the Kiếm Sĩ blade; another account cannot touch this bag
        var (acc2, mage) = Hero("bag05", 1);
        t.Db.InTransaction((c, tx) => { inv.Add(c, tx, mage, "gear_novice_blade", 1); return 0; });
        InventoryDto md = inv.Get(acc2, mage);
        Assert.Equal("wrong_class", Assert.Throws<GameError>(() => inv.Equip(acc2, mage, new EquipRequestDto { slot = Slot(md, "gear_novice_blade") })).Code);
        Assert.False(md.slots.First(s => s.itemId == "gear_novice_blade").usableByClass);
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => inv.Get(acc2, id)).Code);
    }
}
