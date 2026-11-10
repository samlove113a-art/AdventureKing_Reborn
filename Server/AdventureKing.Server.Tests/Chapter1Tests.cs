using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Chương 1 — Map 1 Làng Nấm: portal from the east of Đồng Cỏ Gió, Nấm Non farm, safe zone, chapter quests.</summary>
public sealed class Chapter1Tests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly InventoryService inv;
    private readonly QuestService quests;
    private long clock = 1_767_225_600_000;

    public Chapter1Tests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(11), SkillConfig.Load(SkillServiceTests.SkillsPath));
        inv = new InventoryService(t.Db, ItemConfig.Load(InventoryServiceTests.ItemsPath), farm, () => clock);
        quests = new QuestService(t.Db, QuestConfig.Load(ShopQuestTests.QuestsPath, inv.Config), inv, () => clock);
        Action<string, string, string, int> rec = (a, c, e, n) => quests.Record(a, c, e, n);
        farm.QuestEvent = rec; inv.QuestEvent = rec; chars.QuestEvent = rec;
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Nấm Thử " + login[^2..], 0, 0).characterId);
    }

    /// <summary>Marks the starter chain (main_01..main_10) as already claimed so the chapter quest is the open one.</summary>
    private void FinishStarterChain(string id)
    {
        using var c = t.Db.Open();
        foreach (QuestDef q in quests.Config.quests.Where(q => q.type == "main" && q.id.StartsWith("main_")))
            Database.Exec(c, null, "INSERT INTO quest_progress(character_id, quest_id, period, counts_json, claimed_ms) VALUES($c,$q,'','{}',1)", ("$c", id), ("$q", q.id));
    }

    private void Walk(string acc, string id, float fromX, float toX, float y = 0f)
    {
        float step = Math.Sign(toX - fromX) * 4f;
        for (float x = fromX; Math.Abs(toX - x) > 0.01f; x = Math.Abs(toX - x) <= 4f ? toX : x + step)
        {
            clock += 500;
            farm.Presence(acc, id, new PresenceRequestDto { x = x, y = y });
        }
        clock += 500;
        farm.Presence(acc, id, new PresenceRequestDto { x = toX, y = y });
    }

    [Fact]
    public void Hub_to_meadow_to_mushroom_village_and_back_through_server_portals()
    {
        var (acc, id) = Hero("ch1trv01");
        Assert.Equal("meadow_breeze", chars.Travel(acc, id, "hub_east_meadow", 66f, 0f).lastMapId);
        clock += 2000;
        // the chapter gate is at the east end of the meadow: standing at the west arrival is too far
        Assert.Equal("portal_too_far", Assert.Throws<GameError>(() => chars.Travel(acc, id, "meadow_east_ch1", -36f, 0f)).Code);
        CharacterDto v = chars.Travel(acc, id, "meadow_east_ch1", 78f, 0f);
        Assert.Equal("ch1_mushroom_village", v.lastMapId);
        Assert.Equal(-36f, v.posX, 1);
        // the position autosave works inside Làng Nấm (bounds) and is refused outside
        chars.SaveLocation(acc, id, "ch1_mushroom_village", 10f, 0.9f);
        Assert.Equal("position_invalid", Assert.Throws<GameError>(() => chars.SaveLocation(acc, id, "ch1_mushroom_village", 140f, 0.9f)).Code);
        clock += 2000;
        CharacterDto back = chars.Travel(acc, id, "ch1_village_west_meadow", -40f, 0f);
        Assert.Equal("meadow_breeze", back.lastMapId);
        Assert.Equal(74f, back.posX, 1);
    }

    [Fact]
    public void Mushroom_village_spawns_only_outside_the_safe_zone_and_the_rule_is_enforced()
    {
        FarmConfig cfg = FarmConfig.Load(FarmServiceTests.MonstersPath);
        var spawns = cfg.spawns.Where(s => s.mapId == "ch1_mushroom_village").ToList();
        Assert.True(spawns.Count >= 5);
        Assert.All(spawns, s => Assert.Equal("young_mushroom", s.type));
        var zone = FarmService.SafeZones["ch1_mushroom_village"];
        MonsterType ty = cfg.types.Single(x => x.id == "young_mushroom");
        Assert.All(spawns, s => Assert.True(s.x - ty.leash - ty.reach > zone.maxX, s.id));
        // a config that puts a monster next to the elder's square is rejected at load
        cfg.spawns = cfg.spawns.Append(new SpawnPoint { id = "bad", mapId = "ch1_mushroom_village", type = "young_mushroom", x = 2f }).ToArray();
        Assert.Throws<InvalidDataException>(() => cfg.Validate());
    }

    [Fact]
    public void Chapter_quests_travel_talk_kill_and_pay_once()
    {
        var (acc, id) = Hero("ch1qst01");
        FinishStarterChain(id);
        QuestLogDto log = quests.Log(acc, id);
        Assert.Equal("ch1_01", log.main.Single().id);
        // talking to the mushroom elder before the travel quest is done counts nothing
        quests.ClientEvent(acc, id, new QuestEventDto { @event = "talk:mushroom_elder" });
        Assert.False(quests.Log(acc, id).main.Single().complete);

        chars.Travel(acc, id, "hub_east_meadow", 66f, 0f);
        clock += 2000;
        chars.Travel(acc, id, "meadow_east_ch1", 78f, 0f);
        Assert.True(quests.Log(acc, id).main.Single().complete);
        quests.Claim(acc, id, new QuestClaimDto { questId = "ch1_01" });

        log = quests.ClientEvent(acc, id, new QuestEventDto { @event = "talk:mushroom_elder" });
        Assert.Equal("ch1_02", log.main.Single().id);
        Assert.True(log.main.Single().complete);
        Assert.Equal("Trưởng Làng Nấm", log.turnInNpc);
        quests.Claim(acc, id, new QuestClaimDto { questId = "ch1_02" });
        Assert.Equal("ch1_03", quests.Log(acc, id).main.Single().id);

        // walk out of the safe zone to the first Nấm Non and fight it on the server (Kiếm Sĩ, 18 dmg / 0.85 s)
        Walk(acc, id, -36f, 39f);
        int kills = 0;
        string[] targets = { "c1m1_n1", "c1m1_n2", "c1m1_n4", "c1m1_n6" };
        float[] at = { 39f, 49f, 65f, 83f };
        float here = 39f;
        for (int k = 0; k < targets.Length; k++)
        {
            if (Math.Abs(here - at[k]) > 0.1f) { Walk(acc, id, here, at[k]); here = at[k]; }
            for (int swing = 0; swing < 30; swing++)
            {
                clock += 900;
                AttackResultDto r = farm.Attack(acc, id, new AttackRequestDto { spawnId = targets[k], x = here, y = 0f });
                if (r.killed) { kills++; break; }
            }
            clock += 15000; // catch breath: out-of-combat regeneration (server)
            farm.Presence(acc, id, new PresenceRequestDto { x = here, y = 0f });
        }
        Assert.Equal(4, kills);
        Assert.False(quests.Log(acc, id).main.Single().complete);
        Assert.Equal(4, quests.Log(acc, id).main.Single().objectives[0].have);
        // the first monster has respawned (45 s) by now: kill it again for the fifth
        Walk(acc, id, here, 39f);
        for (int swing = 0; swing < 30; swing++)
        {
            clock += 900;
            if (farm.Attack(acc, id, new AttackRequestDto { spawnId = "c1m1_n1", x = 39f, y = 0f }).killed) break;
        }
        log = quests.Log(acc, id);
        Assert.True(log.main.Single().complete);
        Assert.Equal("Trưởng Làng Nấm", log.turnInNpc);
        QuestClaimResultDto paid = quests.Claim(acc, id, new QuestClaimDto { questId = "ch1_03" });
        Assert.Equal(150, paid.exp);
        Assert.Equal("already_claimed", Assert.Throws<GameError>(() => quests.Claim(acc, id, new QuestClaimDto { questId = "ch1_03" })).Code);
        Assert.True(Count(inv.Get(acc, id), "potion_hp_m") >= 2);
    }

    private static int Count(InventoryDto d, string item) => d.slots.Where(s => s.itemId == item).Sum(s => s.qty);
}
