using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Item rarity (white &lt; blue &lt; purple &lt; orange &lt; red) in the item data, the bag snapshot and the drop DTO, and Lực Chiến in the bag
/// snapshot: the same number as the ranking, changing with equipment.</summary>
public sealed class LootPowerTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private long clock = 1_767_225_600_000;
    private static readonly string[] Order = { "white", "blue", "purple", "orange", "red" };

    public LootPowerTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(5), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, int cls = 0)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Lực Chiến " + login[^2..], cls, 0).characterId);
    }

    [Fact]
    public void Every_item_has_a_rarity_and_the_top_tiers_are_bosses_and_elites()
    {
        var cfg = ItemConfig.Load(InventoryServiceTests.ItemsPath);
        Assert.All(cfg.items, i => Assert.Contains(i.rarity, Order));
        Assert.Equal("red", cfg.Get("gear_king_crown_charm")!.rarity);       // Boss Vua Nấm Quỷ
        Assert.Equal("orange", cfg.Get("gear_dark_charm")!.rarity);           // Mini Boss Nấm Hắc Ám
        Assert.Equal("orange", cfg.Get("gear_toxic_mask")!.rarity);           // Elite
        Assert.Equal("purple", cfg.Get("gear_chief_armor")!.rarity);          // Mini Boss
        Assert.Equal("white", cfg.Get("gear_novice_blade")!.rarity);
        // junk is always white; no ordinary monster's drop beats blue
        Assert.All(cfg.items.Where(i => i.kind == "junk"), i => Assert.Equal("white", i.rarity));
        var farmCfg = FarmConfig.Load(FarmServiceTests.MonstersPath);
        foreach (var ty in farmCfg.types.Where(x => x.tier == "normal"))
            foreach (var d in ty.drops)
                Assert.True(Array.IndexOf(Order, cfg.Get(d.itemId)!.rarity) <= 2, $"{ty.id} drops {d.itemId} above purple");
        // a bad rarity is refused when the config loads
        string bad = Path.Combine(Path.GetTempPath(), "items_bad_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(bad, File.ReadAllText(InventoryServiceTests.ItemsPath).Replace("\"rarity\": \"red\"", "\"rarity\": \"rainbow\""));
        Assert.Throws<InvalidDataException>(() => ItemConfig.Load(bad));
        File.Delete(bad);
    }

    [Fact]
    public void Bag_snapshot_carries_rarity_and_a_combat_power_that_matches_the_ranking_and_follows_equipment()
    {
        var (acc, id) = Hero("lootpw01", 0);
        InventoryDto start = inv.Get(acc, id);
        int level = 1;
        Assert.Equal(farm.CombatPower(id, 0, level), start.combatPower);
        Assert.True(start.combatPower > 0);

        t.Db.InTransaction((c, tx) => { inv.Add(c, tx, id, "gear_dark_charm", 1); return 0; });
        InventoryDto withBag = inv.Get(acc, id);
        Assert.Equal(start.combatPower, withBag.combatPower);                  // carrying is not wearing
        var slot = withBag.slots.Single(s => s.itemId == "gear_dark_charm");
        Assert.Equal("orange", slot.rarity);

        InventoryDto worn = inv.Equip(acc, id, new EquipRequestDto { slot = slot.index });
        int gain = FarmService.PowerOf(0, level, 7, 5) - FarmService.PowerOf(0, level, 0, 0);
        Assert.Equal(start.combatPower + gain, worn.combatPower);
        Assert.Equal(farm.CombatPower(id, 0, level), worn.combatPower);        // one formula everywhere
        Assert.Equal("orange", worn.equipped.Single(e => e.item.itemId == "gear_dark_charm").item.rarity);

        InventoryDto off = inv.Unequip(acc, id, new UnequipRequestDto { equipSlot = "accessory" });
        Assert.Equal(start.combatPower, off.combatPower);
    }

    [Fact]
    public void Drops_tell_the_client_the_rarity_so_it_can_light_the_pillar()
    {
        var farmCfg = FarmConfig.Load(FarmServiceTests.MonstersPath);
        var (acc, id) = Hero("lootpw02", 1);
        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        // the king's guaranteed drop: kill it with a one-shot hero and read the drop list
        t.Db.InTransaction((c, tx) => 0);
        farm.EquipmentBonus = _ => (100000, 100000);
        using (var c = t.Db.Open()) Database.Exec(c, null, "UPDATE characters SET map_id='ch1_king_temple', has_position=0 WHERE id=$c", ("$c", id));
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -36f, y = 0.8f });
        for (float x = -36f; x < 54f;) { x = Math.Min(54f, x + 4f); clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = 0.8f }); }
        clock += 1500;
        Assert.True(farm.Attack(acc, id, new AttackRequestDto { spawnId = "c1m10_boss", x = 54f, y = 0.8f }).killed);
        var drops = farm.State(acc, id).drops;
        var crown = drops.Single(d => d.itemId == "gear_king_crown_charm");
        Assert.Equal("red", crown.itemRarity);
        Assert.Equal("equipment", crown.itemKind);
        Assert.All(drops.Where(d => d.itemId.StartsWith("junk_")), d => Assert.Equal("white", d.itemRarity));
        Assert.All(drops.Where(d => d.itemId.Length == 0), d => Assert.Equal("", d.itemRarity));   // Gold has none
    }
}
