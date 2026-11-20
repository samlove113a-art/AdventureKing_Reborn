using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Item drops (junk / equipment), the 15 s ground life, bag capacity (40 → 99 bought with Gold) and "Bán Nhanh Đồ Rác".</summary>
public sealed class DropInventoryTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private readonly ShopService shop;
    private long clock = 1_767_225_600_000;

    public DropInventoryTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(5), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        shop = new ShopService(t.Db, inv, () => clock);
        chars.Arrived = farm.Arrived;
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Rơi Đồ " + login[^2..], 0, 0).characterId);
    }

    [Fact]
    public void Owned_Name_Frames_Appear_In_The_Bag_And_Can_Be_Worn_Or_Removed_Without_Taking_A_Cell()
    {
        var (acc, id) = Hero("frame01");
        var titles = new TitleService(t.Db, () => clock);
        InventoryDto before = inv.Get(acc, id);
        Assert.DoesNotContain(before.slots, s => s.kind == "frame");
        titles.Grant(id, "VIP_15", "test");
        titles.Grant(id, "VIP_01", "test");
        titles.Grant(id, "BossHunter_10", "test");
        InventoryDto d = inv.Get(acc, id);
        BagSlotDto[] frames = d.slots.Where(s => s.kind == "frame").ToArray();
        Assert.Equal(new[] { "VIP_01", "VIP_15", "BossHunter_10" }, frames.Select(f => f.itemId));          // VIP 1..15 first, then Boss Hunter
        Assert.Equal("Khung VIP 15", frames[1].name);
        Assert.All(frames, f => Assert.True(f.index >= d.capacity, "frames never use a real cell"));
        Assert.Equal("white", frames[0].rarity); Assert.Equal("red", frames[1].rarity);                      // the higher the level, the rarer the colour
        Assert.All(frames, f => Assert.False(f.worn));
        Assert.Equal(before.slots.Length, d.slots.Length - 3);                                              // the real bag is untouched
        titles.Equip(acc, id, new TitleIdDto { titleId = "VIP_15" });
        Assert.True(inv.Get(acc, id).slots.Single(s => s.itemId == "VIP_15").worn);
        Assert.Single(inv.Get(acc, id).slots, s => s.worn);
        titles.Equip(acc, id, new TitleIdDto { titleId = null });                                           // take it off: back in the bag, not worn
        Assert.DoesNotContain(inv.Get(acc, id).slots, s => s.worn);
        Assert.Contains(inv.Get(acc, id).slots, s => s.itemId == "VIP_15");
    }

    private long Gold(string id) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", id)); }
    private void SetGold(string id, long g) { using var c = t.Db.Open(); Database.Exec(c, null, "UPDATE characters SET gold=$g WHERE id=$c", ("$g", g), ("$c", id)); }
    private int Count(InventoryDto d, string item) => d.slots.Where(s => s.itemId == item).Sum(s => s.qty);

    private void Add(string id, string item, int qty) => t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, item, qty); return 0; });

    private void Walk(string acc, string id, float fromX, float toX)
    {
        float step = Math.Sign(toX - fromX) * 4f;
        for (float x = fromX; Math.Abs(toX - x) > 0.01f; x = Math.Abs(toX - x) <= 4f ? toX : x + step)
        {
            clock += 500;
            farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0f });
        }
        clock += 500;
        farm.Presence(acc, id, new PresenceRequestDto { x = toX, y = 0f });
    }

    /// <summary>Kills the meadow slime at x 9 until an item drop (junk) lies on the ground; returns it.</summary>
    private DropDto KillUntilItemDrop(string acc, string id)
    {
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        clock += 12000;
        Walk(acc, id, -36f, 9f);
        for (int kill = 0; kill < 40; kill++)
        {
            for (int swing = 0; swing < 60; swing++)
            {
                clock += 900;
                if (farm.Attack(acc, id, new AttackRequestDto { spawnId = "meadow_s3", x = 9f, y = 0f }).killed) break;
            }
            DropDto? item = farm.State(acc, id).drops.FirstOrDefault(d => d.itemId.Length > 0);
            if (item != null) return item;
            clock += 70000; // respawn
            farm.Presence(acc, id, new PresenceRequestDto { x = 9f, y = 0f });
        }
        throw new Exception("no item drop in 40 kills");
    }

    [Fact]
    public void Config_is_consistent_junk_sells_equipment_does_not_stack_and_drops_name_real_items()
    {
        FarmConfig cfg = FarmConfig.Load(FarmServiceTests.MonstersPath);
        Assert.Equal(15, cfg.dropLifetimeSeconds);
        foreach (MonsterItemDrop d in cfg.types.SelectMany(x => x.drops))
        {
            ItemDef item = inv.Config.Get(d.itemId) ?? throw new Exception("unknown drop item " + d.itemId);
            Assert.InRange(d.chance, 0.0, 1.0);
            Assert.True(item.kind is "junk" or "equipment" or "material", d.itemId);
        }
        Assert.All(inv.Config.items.Where(i => i.kind == "junk"), i => { Assert.True(i.priceGold > 0); Assert.True(inv.Config.Stackable(i)); });
        Assert.All(inv.Config.items.Where(i => i.kind == "equipment"), i => Assert.False(inv.Config.Stackable(i)));
        // junk is the frequent drop, equipment the rare one
        MonsterType nam = cfg.types.Single(x => x.id == "young_mushroom");
        Assert.True(nam.drops.Single(x => x.itemId == "junk_mushroom_cap").chance > nam.drops.Single(x => x.itemId == "gear_nam_non_ring").chance * 5);
    }

    [Fact]
    public void Item_drop_is_picked_once_lands_in_the_bag_and_vanishes_after_15_seconds_if_left()
    {
        var (acc, id) = Hero("drop01");
        DropDto item = KillUntilItemDrop(acc, id);
        Assert.True(item.qty >= 1 && item.expiresInMs <= 15000, "15 s life");
        farm.Presence(acc, id, new PresenceRequestDto { x = item.x, y = 0f });
        int before = Count(inv.Get(acc, id), item.itemId);
        PickupResultDto got = farm.Pickup(acc, id, new PickupRequestDto { dropId = item.id, x = item.x, y = 0f });
        Assert.Equal(item.itemId, got.itemId);
        Assert.Equal(before + item.qty, Count(inv.Get(acc, id), item.itemId));
        Assert.Equal("drop_gone", Assert.Throws<GameError>(() => farm.Pickup(acc, id, new PickupRequestDto { dropId = item.id, x = item.x, y = 0f })).Code); // never twice
        // anything left on the ground is gone after 15 s
        clock += 16000;
        farm.Presence(acc, id, new PresenceRequestDto { x = item.x, y = 0f });
        Assert.Empty(farm.State(acc, id).drops);
    }

    [Fact]
    public void Full_bag_refuses_the_pickup_and_the_drop_stays_until_a_bought_cell_makes_room()
    {
        var (acc, id) = Hero("drop02");
        inv.Get(acc, id); // starter kit
        // equipment never stacks: one cell each, the 40 cells fill up quickly
        while (inv.Get(acc, id).slots.Length < inv.Config.bagSlots) Add(id, "gear_cloth_vest", 1);
        DropDto item = KillUntilItemDrop(acc, id);
        // every cell holds a different stack now; make sure the junk cannot top up an existing stack
        farm.Presence(acc, id, new PresenceRequestDto { x = item.x, y = 0f });
        Assert.Equal("bag_full", Assert.Throws<GameError>(() => farm.Pickup(acc, id, new PickupRequestDto { dropId = item.id, x = item.x, y = 0f })).Code);
        Assert.Contains(farm.State(acc, id).drops, d => d.id == item.id); // still there
        InventoryDto full = inv.Get(acc, id);
        Assert.Equal(40, full.capacity);
        ExpandResultDto ex = inv.Expand(acc, id);
        Assert.Equal(41, ex.inventory.capacity);
        Assert.Equal(500, ex.priceGold);
        farm.Pickup(acc, id, new PickupRequestDto { dropId = item.id, x = item.x, y = 0f });
        Assert.True(Count(inv.Get(acc, id), item.itemId) >= item.qty);
    }

    [Fact]
    public void Bag_expands_one_cell_at_a_time_for_Gold_up_to_99_never_more_and_never_twice_for_one_cell()
    {
        var (acc, id) = Hero("exp001");
        Assert.Equal(40, inv.Get(acc, id).capacity);
        Assert.Equal(99, inv.Get(acc, id).maxCapacity);
        Assert.Equal(500, inv.Get(acc, id).nextExpandGold);
        SetGold(id, 5);
        Assert.Equal("insufficient_gold", Assert.Throws<GameError>(() => inv.Expand(acc, id)).Code);
        Assert.Equal(40, inv.Get(acc, id).capacity);
        SetGold(id, 1_000_000);
        long paid = 0;
        for (int n = 0; n < 59; n++) paid += inv.Expand(acc, id).priceGold;
        Assert.Equal(500L * 59 + 50L * (59 * 58 / 2), paid); // price rises by 50 with every cell
        Assert.Equal(1_000_000 - paid, Gold(id));
        InventoryDto done = inv.Get(acc, id);
        Assert.Equal(99, done.capacity);
        Assert.Equal(0, done.nextExpandGold);
        Assert.Equal("bag_max", Assert.Throws<GameError>(() => inv.Expand(acc, id)).Code);
        Assert.Equal(1_000_000 - paid, Gold(id));
        // the extra cells are really usable
        Add(id, "gear_cloth_vest", 99 - inv.Get(acc, id).slots.Length);
        Assert.Equal(99, inv.Get(acc, id).slots.Length);
        Assert.Equal("bag_full", Assert.Throws<GameError>(() => Add(id, "gear_cloth_vest", 1)).Code);
    }

    [Fact]
    public void Quick_sell_junk_pays_once_keeps_everything_else_and_needs_a_shop_map()
    {
        var (acc, id) = Hero("sell01");
        inv.Get(acc, id);
        Add(id, "junk_mushroom_cap", 120);   // 99 + 21: two stacks
        Add(id, "junk_pollen_dust", 5);
        Add(id, "gear_cloth_vest", 1);
        long gold = Gold(id);
        SellJunkResultDto r = shop.SellJunk(acc, id);
        Assert.Equal(3, r.stacksSold);
        Assert.Equal(125, r.itemsSold);
        Assert.Equal(7L * 120 + 8L * 5, r.totalGold);
        Assert.Equal(gold + r.totalGold, Gold(id));
        Assert.Equal(0, Count(r.inventory, "junk_mushroom_cap"));
        Assert.True(Count(r.inventory, "gear_cloth_vest") >= 2, "equipment and potions are never sold");
        Assert.True(Count(r.inventory, "potion_hp_s") >= 10);
        // again: nothing left, nothing paid
        Assert.Equal("no_junk", Assert.Throws<GameError>(() => shop.SellJunk(acc, id)).Code);
        Assert.Equal(gold + r.totalGold, Gold(id));
        // away from the shop NPC: refused, junk kept
        Add(id, "junk_slime_jelly", 3);
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        Assert.Equal("shop_far", Assert.Throws<GameError>(() => shop.SellJunk(acc, id)).Code);
        Assert.Equal(3, Count(inv.Get(acc, id), "junk_slime_jelly"));
    }
}
