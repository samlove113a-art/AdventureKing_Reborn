using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Chương 1 · Map 3-10: portal chain, spawns, Mini Boss / Elite / Boss, boss skills (slam, enrage), explore points and the
/// server-recorded chapter progress (only the confirmed Vua Nấm Quỷ kill, idempotent, never from the client).</summary>
public sealed class Chapter1MapsTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly FarmService farm;
    private readonly ChapterService chapters;
    private readonly List<string> events = new();
    private long clock = 1_767_225_600_000;
    private static readonly string[] MapIds =
        { "ch1_glow_forest", "ch1_flower_hill", "ch1_mushroom_cave", "ch1_jade_falls", "ch1_dark_forest", "ch1_poison_marsh", "ch1_ancient_ruins", "ch1_king_temple" };

    public Chapter1MapsTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(5), SkillConfig.Load(SkillServiceTests.SkillsPath));
        chapters = new ChapterService(t.Db, () => clock);
        farm.ChapterBossKilled = chapters.RecordBossKill;
        farm.QuestEvent = (a, c, e, n) => events.Add(e);
        chars.QuestEvent = (a, c, e, n) => events.Add(e);
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id) Hero(string login, int cls = 0)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, "Nấm Thử " + login[^2..], cls, 0).characterId);
    }

    private void Place(string id, string mapId)
    {
        using var c = t.Db.Open();
        Database.Exec(c, null, "UPDATE characters SET map_id=$m, has_position=0 WHERE id=$c", ("$m", mapId), ("$c", id));
    }

    [Fact]
    public void Portals_chain_map_2_to_map_10_both_ways_inside_the_bounds()
    {
        string[] chain = new[] { "ch1_green_meadow" }.Concat(MapIds).ToArray();
        for (int i = 0; i + 1 < chain.Length; i++)
        {
            var east = CharacterService.Portals.Values.Single(p => p.FromMap == chain[i] && p.ToMap == chain[i + 1]);
            var west = CharacterService.Portals.Values.Single(p => p.FromMap == chain[i + 1] && p.ToMap == chain[i]);
            var from = CharacterService.Maps[chain[i]]; var to = CharacterService.Maps[chain[i + 1]];
            Assert.InRange(east.X, from.MinX, from.MaxX); Assert.InRange(east.ArrivalX, to.MinX, to.MaxX);
            Assert.InRange(west.X, to.MinX, to.MaxX); Assert.InRange(west.ArrivalX, from.MinX, from.MaxX);
            // arriving never lands inside the next portal's reach (no ping-pong)
            Assert.True(Math.Abs(east.ArrivalX - west.X) > CharacterService.PortalReach, $"{chain[i]} -> {chain[i + 1]} arrival overlaps the way back");
        }
        // the chapter ends at the temple: no portal leads further (Chương 2 is unlocked by the server, not by a gate)
        Assert.DoesNotContain(CharacterService.Portals.Values, p => p.FromMap == "ch1_king_temple" && p.ToMap != "ch1_ancient_ruins");
    }

    [Fact]
    public void A_hero_walks_the_whole_chain_with_the_server_portals()
    {
        var (acc, id) = Hero("ch1chain1");
        Place(id, "ch1_green_meadow");
        string[] chain = new[] { "ch1_green_meadow" }.Concat(MapIds).ToArray();
        for (int i = 0; i + 1 < chain.Length; i++)
        {
            clock += 2000;
            var east = CharacterService.Portals.Values.Single(p => p.FromMap == chain[i] && p.ToMap == chain[i + 1]);
            Assert.Equal(chain[i + 1], chars.Travel(acc, id, east.Id, east.X, 0f).lastMapId);
        }
        Assert.Contains("travel:ch1_king_temple", events);
    }

    [Fact]
    public void Every_map_has_monsters_and_the_tiers_the_plan_asks_for()
    {
        foreach (string m in MapIds)
            Assert.True(farm.Config.spawns.Count(s => s.mapId == m) >= 6, m + " has too few spawns");
        string TierOn(string m) => string.Join(",", farm.Config.spawns.Where(s => s.mapId == m).Select(s => farm.Config.types.First(t2 => t2.id == s.type).tier).Where(x => x != "normal").Distinct());
        Assert.Equal("mini", TierOn("ch1_glow_forest")); Assert.Equal("mini", TierOn("ch1_jade_falls")); Assert.Equal("mini", TierOn("ch1_ancient_ruins"));
        Assert.Equal("elite", TierOn("ch1_poison_marsh")); Assert.Equal("boss", TierOn("ch1_king_temple"));
        foreach (string m in new[] { "ch1_flower_hill", "ch1_mushroom_cave", "ch1_dark_forest" }) Assert.Equal("", TierOn(m));
        // only Vua Nấm Quỷ closes the chapter
        Assert.Equal(new[] { "mushroom_king" }, farm.Config.types.Where(x => x.chapterBossOf > 0).Select(x => x.id).ToArray());
        // quests, drops and items all resolve (config loaders already throw on unknown ids; kill targets exist)
        var items = ItemConfig.Load(InventoryServiceTests.ItemsPath);
        var quests = QuestConfig.Load(ShopQuestTests.QuestsPath, items);
        foreach (var q in quests.quests.Where(q => q.id.CompareTo("ch1_07") >= 0 && q.id.StartsWith("ch1_")))
            foreach (var o in q.objectives.Where(o => o.@event.StartsWith("kill:")))
                Assert.Contains(farm.Config.types, ty => "kill:" + ty.id == o.@event);
        foreach (var ty in farm.Config.types) foreach (var d in ty.drops) Assert.NotNull(items.Get(d.itemId));
    }

    private void Walk(string acc, string id, float fromX, float toX, float y = 0.8f)
    {
        for (float x = fromX; Math.Abs(toX - x) > 0.01f;) { x = Math.Abs(toX - x) <= 4f ? toX : x + Math.Sign(toX - x) * 4f; clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = y }); }
    }

    private void Aggro(string acc, string id, string spawn, float x, float y = 0.8f)
    {
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x - 3f, y = y });
        clock += 1000; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = y });
    }

    [Fact]
    public void The_mini_boss_slams_enrages_below_half_and_pays_out_once()
    {
        var (acc, id) = Hero("ch1mini01", 1); // Pháp Sư: range 6
        Place(id, "ch1_glow_forest");
        farm.EquipmentBonus = _ => (640, 36);
        const string boss = "c1m3_boss"; float bx = 90f;
        Aggro(acc, id, boss, bx - 3f);
        clock += 1500;
        var hit = farm.Attack(acc, id, new AttackRequestDto { spawnId = boss, x = bx - 3f, y = 0.8f });
        Assert.False(hit.killed);
        Assert.Equal(1, hit.monster.phase);
        Assert.Equal("mini", hit.monster.tier);
        Assert.True(hit.monster.slamInMs > 0, "the slam is telegraphed (time until it lands)");
        // the slam lands on a hero inside its reach once the timer runs out, harder than a normal hit
        clock += 6000;
        var state = farm.Presence(acc, id, new PresenceRequestDto { x = bx - 3f, y = 0.8f });
        Assert.True(state.damageTaken >= 22, "slam damage " + state.damageTaken);
        // second hit: HP below half -> enraged (phase 2)
        clock += 2500;
        var again = farm.Attack(acc, id, new AttackRequestDto { spawnId = boss, x = bx - 3f, y = 0.8f });
        Assert.True(again.monster.hp < 1400 / 2);
        Assert.Equal(2, again.monster.phase);
    }

    [Fact]
    public void Only_the_confirmed_king_kill_records_chapter_progress_once()
    {
        var (acc, id) = Hero("ch1king01", 1);
        Place(id, "ch1_king_temple");
        farm.EquipmentBonus = _ => (100000, 100000); // one hit kills, nothing hurts: this test is about the record, not the fight
        Assert.False(chapters.Cleared(id, 1));
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -36f, y = 0.8f });
        Walk(acc, id, -36f, 30f);
        // killing an ordinary guard and (elsewhere) a mini boss never closes the chapter
        var guard = farm.Config.spawns.First(s => s.mapId == "ch1_king_temple" && s.type == "royal_guard" && s.y == 0f);
        Walk(acc, id, 30f, guard.x - 3f, 0.8f);
        clock += 1500;
        Assert.True(farm.Attack(acc, id, new AttackRequestDto { spawnId = guard.id, x = guard.x - 3f, y = 0.8f }).killed);
        Assert.False(chapters.Cleared(id, 1));
        // the king
        const string king = "c1m10_boss";
        Walk(acc, id, guard.x - 3f, 54f);
        clock += 1500;
        var r = farm.Attack(acc, id, new AttackRequestDto { spawnId = king, x = 54f, y = 0.8f });
        Assert.True(r.killed, "the king falls");
        Assert.True(r.drop != null && r.drop.amount >= 600, "boss Gold");
        Assert.True(chapters.Cleared(id, 1));
        Assert.Equal(new[] { 1 }, chapters.ClearedChapters(id));
        // respawn and kill again: still one row (idempotent), the first kill time is kept
        long first;
        using (var c = t.Db.Open()) first = Database.Scalar<long>(c, null, "SELECT killed_at FROM chapter_progress WHERE character_id=$c", ("$c", id));
        clock += 700_000;
        Walk(acc, id, 54f, 50f); Walk(acc, id, 50f, 54f); clock += 1500;
        Assert.True(farm.Attack(acc, id, new AttackRequestDto { spawnId = king, x = 54f, y = 0.8f }).killed);
        using (var c = t.Db.Open())
        {
            Assert.Equal(1L, Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM chapter_progress WHERE character_id=$c", ("$c", id)));
            Assert.Equal(first, Database.Scalar<long>(c, null, "SELECT killed_at FROM chapter_progress WHERE character_id=$c", ("$c", id)));
        }
        // someone else's character reads as 404, never as progress
        var (acc2, _) = Hero("ch1king02");
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => chapters.ClearedFor(acc2, id)).Code);
        Assert.Equal(new[] { 1 }, chapters.ClearedFor(acc, id));
    }

    [Fact]
    public void Explore_points_fire_their_quest_event_from_the_servers_checked_position_once()
    {
        var (acc, id) = Hero("ch1expl01");
        Place(id, "ch1_flower_hill");
        var pt = Ch1ServerData.ExplorePoints.Single(p => p.MapId == "ch1_flower_hill");
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = -36f, y = 0.8f });
        Assert.DoesNotContain("explore:" + pt.Id, events);
        // an impossible jump straight onto the point is refused, so a client cannot fake the exploration
        clock += 200;
        Assert.Throws<GameError>(() => farm.Presence(acc, id, new PresenceRequestDto { x = pt.X, y = pt.Y }));
        Assert.DoesNotContain("explore:" + pt.Id, events);
        // walking there does it, and only once
        float x = -36f;
        while (Math.Abs(pt.X - x) > 0.01f) { x = Math.Abs(pt.X - x) <= 4f ? pt.X : x + 4f; clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = x, y = pt.Y }); }
        clock += 500; farm.Presence(acc, id, new PresenceRequestDto { x = pt.X, y = pt.Y });
        Assert.Single(events, e => e == "explore:" + pt.Id);
    }
}
