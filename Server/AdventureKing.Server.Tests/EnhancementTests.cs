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
        var failure = new InventoryService(t.Db, upgrades.Config, farm, upgradeRoll: () => 99);
        var shielded = failure.Enhance(account, hero,
            new EnhanceRequestDto { equipSlot = "weapon", protect = true, requestId = "protected_failure_001" });
        Assert.False(shielded.success);
        Assert.Equal(7, shielded.afterLevel);
        Assert.Equal(1, Count(shielded.inventory, "protection_charm"));
        var replay = failure.Enhance(account, hero,
            new EnhanceRequestDto { equipSlot = "weapon", protect = true, requestId = "protected_failure_001" });
        Assert.True(replay.replayed);
        Assert.Equal(1, Count(replay.inventory, "protection_charm"));
        var unshielded = failure.Enhance(account, hero,
            new EnhanceRequestDto { equipSlot = "weapon", requestId = "unprotected_failure_001" });
        Assert.False(unshielded.success);
        Assert.Equal(6, unshielded.afterLevel);
    }

    [Fact]
    public void Cannot_upgrade_someone_elses_gear_or_upgrade_past_plus_15()
    {
        var (owner, hero) = Make("enh03");
        var (other, _) = Make("enh04");
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() =>
            upgrades.Enhance(other, hero, new EnhanceRequestDto { equipSlot = "weapon", requestId = "someone_else_1234" })).Code);
    }
}
