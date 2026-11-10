using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Events / Phúc Lợi (DRAFT disabled in production), mail (server-created, claim once), giftcodes (checks + once per account).</summary>
public sealed class WelfareServicesTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly InventoryService inv;
    private readonly ItemConfig items;
    // 2026-11-15 10:00 VN, inside the Săn Nấm Lễ Hội dates
    private long clock = new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static string Cfg(string f) => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", f);

    public WelfareServicesTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
        var farm = new FarmService(t.Db, FarmConfig.Load(FarmServiceTests.MonstersPath), () => clock, new Random(4), SkillConfig.Load(SkillServiceTests.SkillsPath));
        items = ItemConfig.Load(InventoryServiceTests.ItemsPath);
        inv = new InventoryService(t.Db, items, farm, () => clock);
    }

    public void Dispose() => t.Dispose();

    private EventService Events(string file = "events.dev.json") => new(t.Db, EventConfig.Load(Cfg(file), items), inv, () => clock);

    private string NewAccount(string login) => accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
    private string NewHero(string acc, string name) => chars.Create(acc, name, 1, 0).characterId;
    private long Gold(string id) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", id)); }
    private long Count(string id, string item) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT COALESCE(SUM(qty),0) FROM bag_slots WHERE character_id=$c AND item_id=$i", ("$c", id), ("$i", item)); }
    private void SetLevel(string id, int level) { using var c = t.Db.Open(); Database.Exec(c, null, "UPDATE characters SET level=$l WHERE id=$c", ("$l", level), ("$c", id)); }
    private void FillBag(string id)
    {
        using var c = t.Db.Open();
        Database.Exec(c, null, "DELETE FROM bag_slots WHERE character_id=$c", ("$c", id));
        for (int i = 0; i < 40; i++) Database.Exec(c, null, "INSERT INTO bag_slots(character_id, slot_index, item_id, qty) VALUES($c,$s,'gear_cloth_vest',1)", ("$c", id), ("$s", i));
    }

    [Fact]
    public void Production_config_is_a_disabled_draft_and_a_draft_cannot_be_switched_on()
    {
        EventService ev = Events("events.json");
        Assert.False(ev.Config.enabled);
        string acc = NewAccount("wel01"), id = NewHero(acc, "Phúc Lợi Một");
        ev.Record(acc, id, "kill:any", 30);
        WelfareDto w = ev.Get(acc, id);
        Assert.False(w.rewardsEnabled);
        Assert.False(w.anyClaimable);
        Assert.All(w.tabs.SelectMany(x => x.steps), s => Assert.False(s.claimable));
        Assert.True(w.tabs.Single(x => x.id == "firsttopup").locked, "Nạp Đầu stays locked (payments off)");
        Assert.Equal("rewards_pending", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = "event:mushroom_festival:0" })).Code);
        Assert.Equal(5000, Gold(id));

        string bad = Path.Combine(Path.GetTempPath(), "ak_events_bad_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(bad, File.ReadAllText(Cfg("events.json")).Replace("\"enabled\": false", "\"enabled\": true"));
        try { Assert.Throws<InvalidDataException>(() => EventConfig.Load(bad, items)); } finally { File.Delete(bad); }
        // rewards may only be Gold + common items (gear refused)
        File.WriteAllText(bad, File.ReadAllText(Cfg("events.dev.json")).Replace("\"petfood_feast\"", "\"gear_wind_charm\""));
        try { Assert.Throws<InvalidDataException>(() => EventConfig.Load(bad, items)); } finally { File.Delete(bad); }
    }

    [Fact]
    public void Event_tasks_count_server_events_inside_the_dates_and_pay_once()
    {
        EventService ev = Events();
        string acc = NewAccount("wel02"), id = NewHero(acc, "Săn Nấm Hai");
        Assert.Equal("not_ready", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = "event:mushroom_festival:0" })).Code);
        for (int i = 0; i < 31; i++) ev.Record(acc, id, "kill:any", 1);
        ev.Record(acc, id, "kill:slime", 5); // not a task event: ignored
        RewardStepDto step = ev.Get(acc, id).tabs.Single(x => x.id == "event:mushroom_festival").steps[0];
        Assert.Equal(30, step.have);
        Assert.True(step.claimable);
        ev.Claim(acc, id, new WelfareClaimDto { key = "event:mushroom_festival:0" });
        Assert.Equal(5100, Gold(id));
        Assert.Equal(3, Count(id, "potion_hp_m"));
        Assert.Equal("already_claimed", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = "event:mushroom_festival:0" })).Code);
        Assert.Equal(5100, Gold(id));
        Assert.Equal("reward_unknown", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = "event:nope:9" })).Code);
        // another player cannot read or claim this hero's welfare
        string other = NewAccount("wel02b");
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => ev.Get(other, id)).Code);

        // after the event ends: locked, nothing counts, nothing claimable
        clock = new DateTimeOffset(2027, 1, 2, 3, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        ev.Record(acc, id, "skill:any", 100);
        WelfareTabDto tab = ev.Get(acc, id).tabs.Single(x => x.id == "event:mushroom_festival");
        Assert.True(tab.locked);
        Assert.Equal(0, tab.steps[2].have);
        Assert.False(tab.steps[1].claimable);
    }

    [Fact]
    public void Online_minutes_come_from_capped_heartbeats_and_reset_each_vn_day()
    {
        EventService ev = Events();
        string acc = NewAccount("wel03"), id = NewHero(acc, "Online Ba");
        ev.Ping(acc, id);                               // first ping: starts the clock, counts nothing
        for (int i = 0; i < 20; i++) { clock += 30_000; ev.Ping(acc, id); } // 10 minutes
        clock += 3_600_000; ev.Ping(acc, id);           // an hour away counts only the 40 s cap
        RewardStepDto[] online = ev.Get(acc, id).tabs.Single(x => x.id == "online").steps;
        Assert.Equal(10, online[0].have);
        Assert.True(online[0].claimable);
        Assert.False(online[1].claimable);
        long g = Gold(id);
        ev.Claim(acc, id, new WelfareClaimDto { key = online[0].key });
        Assert.Equal(g + 20, Gold(id));
        Assert.Equal("already_claimed", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = online[0].key })).Code);
        // next VN day: counter starts again with a new claim key
        clock += 24 * 3_600_000L;
        RewardStepDto next = ev.Get(acc, id).tabs.Single(x => x.id == "online").steps[0];
        Assert.Equal(0, next.have);
        Assert.NotEqual(online[0].key, next.key);
        Assert.False(next.claimed);
    }

    [Fact]
    public void Level_gifts_need_the_level_and_the_starter_gift_shows_as_received()
    {
        EventService ev = Events();
        string acc = NewAccount("wel04"), id = NewHero(acc, "Lên Cấp Bốn");
        Assert.False(ev.Get(acc, id).tabs.Single(x => x.id == "newbie").steps[0].claimed);
        inv.Get(acc, id); // first bag open grants the starter kit
        Assert.True(ev.Get(acc, id).tabs.Single(x => x.id == "newbie").steps[0].claimed);
        Assert.Equal("not_ready", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = "level:5" })).Code);
        SetLevel(id, 5);
        long hp = Count(id, "potion_hp_m");
        ev.Claim(acc, id, new WelfareClaimDto { key = "level:2" });
        ev.Claim(acc, id, new WelfareClaimDto { key = "level:5" });
        Assert.Equal(5000 + 50 + 150, Gold(id));
        Assert.Equal(hp + 5, Count(id, "potion_hp_m"));
        Assert.False(ev.Get(acc, id).tabs.Single(x => x.id == "levelup").steps.Single(s => s.key == "level:10").claimable);
        // full bag: refused, nothing paid, still claimable later
        SetLevel(id, 10);
        FillBag(id);
        Assert.Equal("bag_full", Assert.Throws<GameError>(() => ev.Claim(acc, id, new WelfareClaimDto { key = "level:10" })).Code);
        Assert.Equal(5200, Gold(id));
        Assert.True(ev.Get(acc, id).tabs.Single(x => x.id == "levelup").steps.Single(s => s.key == "level:10").claimable);
    }

    [Fact]
    public void Mail_welcome_once_read_and_attachments_claimed_once_by_the_owner_only()
    {
        var mail = new MailService(t.Db, inv, () => clock);
        string acc = NewAccount("wel05"), id = NewHero(acc, "Thư Năm");
        MailboxDto box = mail.List(acc, id);
        Assert.Single(box.mails);
        Assert.Equal("notice", box.mails[0].kind);
        Assert.False(box.mails[0].hasAttachment);
        Assert.Single(mail.List(acc, id).mails); // welcome only once
        Assert.Equal(1, box.unread);
        box = mail.Read(acc, id, new MailIdDto { mailId = box.mails[0].id });
        Assert.Equal(0, box.unread);
        Assert.Equal("no_attachment", Assert.Throws<GameError>(() => mail.Claim(acc, id, new MailIdDto { mailId = box.mails[0].id })).Code);

        clock += 1000;
        string mid = mail.Send(id, "reward", "Quà thử", "Thư có quà", 300, new[] { new RewardItem { itemId = "potion_mp_s", qty = 4 } });
        Assert.Throws<InvalidDataException>(() => mail.Send(id, "reward", "x", "y", 0, new[] { new RewardItem { itemId = "gear_wind_charm", qty = 1 } }));
        Assert.Equal(1, mail.List(acc, id).unclaimed);
        // another account cannot touch it
        string other = NewAccount("wel05b"), otherHero = NewHero(other, "Người Lạ");
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => mail.Claim(other, id, new MailIdDto { mailId = mid })).Code);
        Assert.Equal("mail_unknown", Assert.Throws<GameError>(() => mail.Claim(other, otherHero, new MailIdDto { mailId = mid })).Code);
        // full bag: nothing changes, mail stays unclaimed
        long mp = Count(id, "potion_mp_s");
        FillBag(id);
        Assert.Equal("bag_full", Assert.Throws<GameError>(() => mail.Claim(acc, id, new MailIdDto { mailId = mid })).Code);
        Assert.Equal(5000, Gold(id));
        Assert.Equal(1, mail.List(acc, id).unclaimed);
        using (var c = t.Db.Open()) Database.Exec(c, null, "DELETE FROM bag_slots WHERE character_id=$c", ("$c", id));
        box = mail.Claim(acc, id, new MailIdDto { mailId = mid });
        Assert.Equal(5300, Gold(id));
        Assert.Equal(4, Count(id, "potion_mp_s"));
        Assert.Equal(0, box.unclaimed);
        Assert.Equal("already_claimed", Assert.Throws<GameError>(() => mail.Claim(acc, id, new MailIdDto { mailId = mid })).Code);
        Assert.Equal(5300, Gold(id));
        _ = mp;
    }

    [Fact]
    public void Giftcodes_check_existence_expiry_uses_level_and_once_per_account()
    {
        var codes = new GiftcodeService(t.Db, inv, () => clock);
        string acc = NewAccount("wel06"), id = NewHero(acc, "Mã Quà Sáu"), id2 = chars.Create(acc, "Mã Quà Bảy", 1, 1).characterId;
        codes.Create("TANTHU2026", 200, new[] { new RewardItem { itemId = "potion_hp_s", qty = 2 } }, maxUses: 2, minLevel: 1, expiresMs: clock + 86_400_000);
        codes.Create("OLD2025", 100, Array.Empty<RewardItem>(), 10, 1, clock - 1);
        codes.Create("CAP10", 100, Array.Empty<RewardItem>(), 10, 10, clock + 86_400_000);
        Assert.Throws<InvalidDataException>(() => codes.Create("GEAR1", 0, new[] { new RewardItem { itemId = "gear_novice_blade", qty = 1 } }, 1, 1, clock + 1));

        Assert.Equal("bad_code", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "  " })).Code);
        Assert.Equal("code_unknown", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "NOPE" })).Code);
        Assert.Equal("code_expired", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "old2025" })).Code);
        Assert.Equal("level_too_low", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "CAP10" })).Code);

        GiftcodeResultDto r = codes.Redeem(acc, id, new GiftcodeDto { code = " tanthu-2026 " }); // case/space/dash-insensitive
        Assert.Equal(200, r.gold);
        Assert.Equal(5200, Gold(id));
        Assert.Equal(2, Count(id, "potion_hp_s"));
        // the same account (any character) cannot use it twice
        Assert.Equal("code_already_used", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "TANTHU2026" })).Code);
        Assert.Equal("code_already_used", Assert.Throws<GameError>(() => codes.Redeem(acc, id2, new GiftcodeDto { code = "TANTHU2026" })).Code);
        Assert.Equal(5000, Gold(id2));
        // max uses: second account ok, third refused
        string b = NewAccount("wel06b"), hb = NewHero(b, "Người Hai");
        string c3 = NewAccount("wel06c"), hc = NewHero(c3, "Người Ba");
        codes.Redeem(b, hb, new GiftcodeDto { code = "TANTHU2026" });
        Assert.Equal("code_used_up", Assert.Throws<GameError>(() => codes.Redeem(c3, hc, new GiftcodeDto { code = "TANTHU2026" })).Code);
        Assert.Equal(5000, Gold(hc));
        // someone else's character
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => codes.Redeem(c3, id, new GiftcodeDto { code = "CAP10" })).Code);
    }

    [Fact]
    public void Giftcode_guessing_is_rate_limited()
    {
        var codes = new GiftcodeService(t.Db, inv, () => clock);
        string acc = NewAccount("wel07"), id = NewHero(acc, "Đoán Mã");
        for (int i = 0; i < 20; i++) Assert.Equal("code_unknown", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "GUESS" + i })).Code);
        Assert.Equal("too_many_attempts", Assert.Throws<GameError>(() => codes.Redeem(acc, id, new GiftcodeDto { code = "GUESS99" })).Code);
    }
}
