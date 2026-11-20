using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Forward portals stay shut until the map's quest is claimed (config/portal_gates.json, DRAFT); back ways and halls are never gated.</summary>
public sealed class PortalGateTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly QuestService quests;
    private readonly PortalGates gates;
    private long clock = 1_767_225_600_000;

    private static string GatesPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "portal_gates.json");

    public PortalGateTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        var farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(3), SkillConfig.Load(SkillServiceTests.SkillsPath));
        var inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        quests = new QuestService(t.Db, QuestConfig.Load(ShopQuestTests.QuestsPath, inv.Config), inv, () => clock);
        gates = PortalGates.Load(GatesPath, quests);
        chars.PortalGate = gates.Check;
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Cổng Thử " + login[^2..], 0, 0).characterId);
    }

    private void Place(string id, string map) { using var c = t.Db.Open(); Database.Exec(c, null, "UPDATE characters SET map_id=$m, has_position=0 WHERE id=$c", ("$m", map), ("$c", id)); }

    private void Claim(string id, string questId)
    {
        using var c = t.Db.Open();
        Database.Exec(c, null, "INSERT INTO quest_progress(character_id, quest_id, period, counts_json, claimed_ms) VALUES($c,$q,'','{}',$t)",
            ("$c", id), ("$q", questId), ("$t", clock));
    }

    [Fact]
    public void Every_gate_names_a_real_portal_and_a_real_quest_and_never_gates_a_way_back()
    {
        Assert.NotEmpty(gates.Gates);
        foreach (var (portal, quest) in gates.Gates)
        {
            Assert.True(CharacterService.Portals.ContainsKey(portal), portal);
            Assert.Contains(quests.Config.quests, q => q.id == quest);
            Assert.DoesNotContain("west", portal);   // the way back is always open
        }
    }

    [Fact]
    public void A_gated_portal_is_refused_with_the_quest_title_until_the_quest_is_claimed()
    {
        var (acc, id) = Hero("gate01");
        Place(id, "ch1_mushroom_village");
        var east = CharacterService.Portals["ch1_village_east_m02"];
        GameError e = Assert.Throws<GameError>(() => chars.Travel(acc, id, east.Id, east.X, east.Y));
        Assert.Equal("portal_locked", e.Code);
        string title = quests.Config.quests.Single(q => q.id == "ch1_03").title;
        Assert.Contains(title, e.Message);
        // a refusal costs nothing: no cooldown, still on the same map
        using (var c = t.Db.Open()) Assert.Equal("ch1_mushroom_village", Database.Scalar<string>(c, null, "SELECT map_id FROM characters WHERE id=$c", ("$c", id)));

        Claim(id, "ch1_03");
        var moved = chars.Travel(acc, id, east.Id, east.X, east.Y);
        Assert.Equal("ch1_green_meadow", moved.lastMapId);
    }

    [Fact]
    public void Another_characters_progress_does_not_open_the_gate_and_the_way_back_is_always_open()
    {
        var (accA, a) = Hero("gate02");
        var (accB, b) = Hero("gate03");
        Place(a, "ch1_mushroom_village"); Place(b, "ch1_green_meadow");
        Claim(a, "ch1_03");
        var east = CharacterService.Portals["ch1_village_east_m02"];
        Place(b, "ch1_mushroom_village");
        Assert.Equal("portal_locked", Assert.Throws<GameError>(() => chars.Travel(accB, b, east.Id, east.X, east.Y)).Code);   // A's claim is not B's
        // B stands in Map 2 and walks back west: never gated
        Place(b, "ch1_green_meadow");
        var west = CharacterService.Portals["ch1_m02_west_village"];
        Assert.Equal("ch1_mushroom_village", chars.Travel(accB, b, west.Id, west.X, west.Y).lastMapId);
    }

    [Fact]
    public void The_whole_chapter_chain_needs_each_quest_in_order_to_reach_the_temple()
    {
        var (acc, id) = Hero("gate04");
        string[] forward = { "ch1_village_east_m02", "ch1_m02_east_m03", "ch1_m03_east_m04", "ch1_m04_east_m05", "ch1_m05_east_m06",
                             "ch1_m06_east_m07", "ch1_m07_east_m08", "ch1_m08_east_m09", "ch1_m09_east_m10" };
        foreach (string portalId in forward)
        {
            var p = CharacterService.Portals[portalId];
            Place(id, p.FromMap);
            Assert.Equal("portal_locked", Assert.Throws<GameError>(() => chars.Travel(acc, id, p.Id, p.X, p.Y)).Code);
            Claim(id, gates.Gates[portalId]);
            clock += 5000;   // past the travel cooldown
            Assert.Equal(p.ToMap, chars.Travel(acc, id, p.Id, p.X, p.Y).lastMapId);
        }
    }
}
