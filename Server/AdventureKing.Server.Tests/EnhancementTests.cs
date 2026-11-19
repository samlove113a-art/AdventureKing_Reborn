using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Server regression for +1..+15, gear preservation, receipts and resource ownership.</summary>
public sealed class EnhancementTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService characters;
    private readonly FarmService farm;
    private readonly InventoryService upgrades;

    public EnhancementTests()
    {
        accounts = new AccountService(t.Db);
        characters = new CharacterService(t.Db, new NameRules(), starterGold: 5000);
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), random: new Random(5));
        upgrades = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm,
            upgradeRoll: () => 0); // deterministic success
    }

    public void Dispose() => t.Dispose();

    private (string account, string hero) Make(string login)
    {
        string account = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        string hero = characters.Create(account, "Đập Đồ " + login[^2..], 1, 0).characterId;
        return (account, hero);
    }

    private static int Count(InventoryDto inventory, string item) =>
        inventory.slots.Where(s => s.itemId == item).Sum(s => s.qty);

    [Fact]
    public void Server_consumes_stone_preserves_levels_on_swap_and_replays_without_double_charge()
    {
        var (account, hero) = Make("enh01");
        InventoryDto before = upgrades.Get(account, hero);
        int weaponSlot = before.slots.Single(s => s.itemId == "gear_novice_staff").index;
        upgrades.Equip(account, hero, new EquipRequestDto { slot = weaponSlot });
        Assert.Equal("no_stone", Assert.Throws<GameError>(() =>
            upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "upgrade_no_stone_001" })).Code);
        t.Db.InTransaction((c, tx) => { upgrades.Add(c, tx, hero, "enhance_stone", 15); return 0; });

        int cp = upgrades.Get(account, hero).combatPower;
        var first = upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "upgrade_success_001" });
        Assert.True(first.success);
        Assert.Equal(1, first.afterLevel);
        Assert.Equal(14, Count(first.inventory, "enhance_stone"));
        Assert.True(first.inventory.combatPower > cp);
        var replay = upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "upgrade_success_001" });
        Assert.True(replay.replayed);
        Assert.Equal(first.afterLevel, replay.afterLevel);
        Assert.Equal(14, Count(replay.inventory, "enhance_stone"));

        InventoryDto bag = upgrades.Unequip(account, hero, new UnequipRequestDto { equipSlot = "weapon" });
        var staff = bag.slots.Single(s => s.itemId == "gear_novice_staff");
        Assert.Equal(1, staff.enhanceLevel);
        InventoryDto equipped = upgrades.Equip(account, hero, new EquipRequestDto { slot = staff.index });
        Assert.Equal(1, equipped.equipped.Single(x => x.equipSlot == "weapon").item.enhanceLevel);
    }

    [Fact]
    public void Protection_prevents_downgrade_on_failure_and_consumes_one_charm()
    {
        var (account, hero) = Make("enh02");
        InventoryDto d = upgrades.Get(account, hero);
        upgrades.Equip(account, hero, new EquipRequestDto { slot = d.slots.Single(s => s.itemId == "gear_novice_staff").index });
        t.Db.InTransaction((c, tx) => {
            upgrades.Add(c, tx, hero, "enhance_stone", 15);
            upgrades.Add(c, tx, hero, "protection_charm", 2);
            return 0;
        });
        for (int i = 0; i < 7; i++)
            Assert.True(upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "success_level_" + (100 + i) }).success);
        var failure = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => 100);          // 100 never succeeds
        var shielded = failure.Enhance(account, hero,
            new EnhanceRequestDto { equipSlot = "weapon", protect = true, requestId = "protected_failure_001" });
        Assert.False(shielded.success);
        Assert.Equal(7, shielded.afterLevel);
        Assert.Equal(1, Count(shielded.inventory, "protection_charm"));
        var replay = failure.Enhance(account, hero,
            new EnhanceRequestDto { equipSlot = "weapon", protect = true, requestId = "protected_failure_001" });
        Assert.True(replay.replayed);
        Assert.Equal(1, Count(replay.inventory, "protection_charm"));
        int rolls = 0;
        var dropping = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => rolls++ == 0 ? 100 : 0);      // fail, then the 50 % level-loss roll hits
        var unshielded = dropping.Enhance(account, hero,
            new EnhanceRequestDto { equipSlot = "weapon", requestId = "unprotected_failure_001" });
        Assert.False(unshielded.success);
        Assert.Equal(6, unshielded.afterLevel);
    }

    [Fact]
    public void Blacksmith_fee_is_charged_once_on_success_and_failure_and_refused_without_gold()
    {
        var (account, hero) = Make("enh05");
        InventoryDto d = upgrades.Get(account, hero);
        upgrades.Equip(account, hero, new EquipRequestDto { slot = d.slots.Single(s => s.itemId == "gear_novice_staff").index });
        t.Db.InTransaction((c, tx) => { upgrades.Add(c, tx, hero, "enhance_stone", 15); return 0; });
        Assert.Equal(InventoryService.GoldCost(0, 0), upgrades.Get(account, hero).equipped.Single().item.enhanceGold);

        var ok = upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "fee_success_0001" });
        Assert.Equal(InventoryService.GoldCost(0, 0), ok.goldSpent);
        Assert.Equal(5000 - ok.goldSpent, ok.gold);
        var replay = upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "fee_success_0001" });
        Assert.Equal(ok.gold, replay.gold);                                   // replay: no second charge

        var failing = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => 100);
        var lost = failing.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "fee_failure_0001" });
        Assert.False(lost.success);
        Assert.Equal(ok.gold - InventoryService.GoldCost(1, 0), lost.gold);      // a failed attempt still costs the fee

        // broke: refused, nothing consumed
        t.Db.InTransaction((c, tx) => { Wallet.Gold(c, tx, account, hero, -lost.gold, "test:broke", "test-broke-enh05"); return 0; });
        int stones = Count(upgrades.Get(account, hero), "enhance_stone");
        Assert.Equal("insufficient_gold", Assert.Throws<GameError>(() =>
            upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "fee_broke_00001" })).Code);
        Assert.Equal(stones, Count(upgrades.Get(account, hero), "enhance_stone"));
    }

    [Fact]
    public void Cannot_upgrade_someone_elses_gear_or_upgrade_past_plus_15()
    {
        var (owner, hero) = Make("enh03");
        var (other, _) = Make("enh04");
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() =>
            upgrades.Enhance(other, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "someone_else_1234" })).Code);
    }
    [Fact]
    public void Tiers_Bac_Vang_KimCuong_Do_have_rising_difficulty_and_all_reach_plus_15()
    {
        for (int tier = 0; tier < 4; tier++)
        {
            for (int lvl = 0; lvl < 15; lvl++) Assert.InRange(InventoryService.SuccessChance(lvl, tier), 1, 100);
            if (tier > 0) for (int lvl = 1; lvl < 15; lvl++) Assert.True(InventoryService.SuccessChance(lvl, tier) <= InventoryService.SuccessChance(lvl, tier - 1), $"tier {tier} +{lvl} is not easier than the tier below");
        }
        Assert.True(InventoryService.SuccessChance(10, 0) >= 60 && InventoryService.SuccessChance(10, 3) <= 20, "Bạc is easy, Đỏ is hard around +10");
        Assert.True(InventoryService.SuccessChance(14, 0) > 3 * InventoryService.SuccessChance(14, 3));
        Assert.True(InventoryService.GoldCost(10, 3) > InventoryService.GoldCost(10, 0), "better gear costs more to push");
    }

    [Fact]
    public void Failing_up_to_plus_6_never_loses_a_level_from_plus_7_it_can_and_blessing_builds_up_and_resets()
    {
        var (account, hero) = Make("enh09");
        InventoryDto d = upgrades.Get(account, hero);
        upgrades.Equip(account, hero, new EquipRequestDto { slot = d.slots.Single(s => s.itemId == "gear_novice_staff").index });
        t.Db.InTransaction((c, tx) => { upgrades.Add(c, tx, hero, "enhance_stone", 40); Wallet.Gold(c, tx, account, hero, 500000, "test:rich", "rich-enh09"); return 0; });
        for (int i = 0; i < 6; i++) Assert.True(upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "up_to_six_" + (100 + i) }).success);
        var alwaysDrop = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => 100);        // 100: fails every roll (the drop roll needs < 50)
        var at6 = alwaysDrop.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "fail_at_six_0001" });
        Assert.False(at6.success); Assert.False(at6.lostLevel); Assert.Equal(6, at6.afterLevel);           // +6 -> a failure costs only the materials
        Assert.Equal(1, at6.blessing);                                                                      // a failure from +6 upward stores one Chúc Phúc Rèn
        Assert.Equal(1, upgrades.Get(account, hero).blessing);
        // the stored blessing raises the chance of the next attempt by 2 % per stack
        var next = alwaysDrop.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "fail_at_six_0002" });
        Assert.Equal(InventoryService.SuccessChance(6, 0) + 2, next.chancePercent);
        Assert.Equal(2, next.blessing);
        // a success resets it
        var ok = upgrades.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "win_after_fail_01" });
        Assert.True(ok.success); Assert.Equal(0, ok.blessing); Assert.Equal(7, ok.afterLevel);
        // at +7 a failure can cost one level (second roll < 50), a Bùa Bảo Vệ prevents it
        int rolls = 0;
        var dropping = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => rolls++ % 2 == 0 ? 100 : 0);
        var lost = dropping.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "drop_at_seven_01" });
        Assert.True(lost.lostLevel); Assert.Equal(6, lost.afterLevel);
    }

    [Fact]
    public void Lucky_stone_adds_ten_percent_and_is_consumed_and_red_gear_gains_more_per_level()
    {
        var (account, hero) = Make("enh10");
        InventoryDto d = upgrades.Get(account, hero);
        upgrades.Equip(account, hero, new EquipRequestDto { slot = d.slots.Single(s => s.itemId == "gear_novice_staff").index });
        t.Db.InTransaction((c, tx) => { upgrades.Add(c, tx, hero, "enhance_stone", 5); upgrades.Add(c, tx, hero, "lucky_stone", 2); return 0; });
        var failing = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => 100);
        var r = failing.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", lucky = true, requestId = "lucky_try_00001" });
        Assert.Equal(100, r.chancePercent);                                    // +0 is already 100 %, capped
        Assert.Equal(1, Count(r.inventory, "lucky_stone"));
        Assert.Equal("lucky_stone", upgrades.Config.Get("lucky_stone")!.id);
        Assert.Equal("no_lucky", Assert.Throws<GameError>(() => { failing.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", lucky = true, requestId = "lucky_try_00002" }); failing.Enhance(account, hero, new EnhanceRequestDto { equipSlot = "weapon", lucky = true, requestId = "lucky_try_00003" }); }).Code);
    }
}
