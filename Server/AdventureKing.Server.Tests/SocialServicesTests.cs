using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Phase D social layer with real accounts on one server database: presence/sync, friends, blocks, chat,
/// guilds (lifecycle + DRAFT economy), marriage + server-timed ceremony, and persistence across a "restart".</summary>
public sealed class SocialServicesTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly CosmeticCatalog catalog;
    private long clock = new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    private SocialConfig cfg;
    private PresenceService presence = null!;
    private FriendService friends = null!;
    private ChatService chat = null!;
    private GuildService guilds = null!;
    private MarriageService marriage = null!;
    private SyncService sync = null!;

    private static string Cfg(string f) => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", f);

    public SocialServicesTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 50000) { Clock = () => clock };
        catalog = CosmeticCatalog.Load(CosmeticShopTests.CatalogPath);
        cfg = SocialConfig.Load(Cfg("social.dev.json"), catalog);
        Services();
    }

    /// <summary>(Re)creates every service on the same database — a fresh server process, i.e. a "restart".</summary>
    private void Services()
    {
        presence = new PresenceService { Clock = () => clock };
        friends = new FriendService(t.Db, presence);
        chat = new ChatService(t.Db, cfg, new NameRules()) { Clock = () => clock };
        guilds = new GuildService(t.Db, cfg, new NameRules(), presence) { Clock = () => clock };
        marriage = new MarriageService(t.Db, cfg, presence) { Clock = () => clock };
        sync = new SyncService(t.Db, presence, chat, marriage);
    }

    public void Dispose() => t.Dispose();

    private (string acc, string id, string name) Player(string login, string name, int cls = 0)
    {
        string acc = accounts.Register(login, "Kh0ng-Trung-Ten-2026").AccountId;
        return (acc, chars.Create(acc, name, cls, 0).characterId, name);
    }
    private SyncDto Beat((string acc, string id, string name) p, float x, float y = 0.9f, long after = 0, string anim = "run")
    {
        clock += 200;
        return sync.Sync(p.acc, p.id, new SyncRequestDto { x = x, y = y, facing = 1, anim = anim, chatAfter = after });
    }
    private long Gold(string id) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, "SELECT gold FROM characters WHERE id=$c", ("$c", id)); }

    [Fact]
    public void Draft_config_cannot_open_prices_or_economy()
    {
        SocialConfig prod = SocialConfig.Load(Cfg("social.json"), catalog);
        Assert.False(prod.pricesApproved);
        Assert.False(prod.economyEnabled);
        string bad = Path.Combine(Path.GetTempPath(), "ak_social_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(bad, File.ReadAllText(Cfg("social.json")).Replace("\"economyEnabled\": false", "\"economyEnabled\": true"));
        try { Assert.Throws<InvalidDataException>(() => SocialConfig.Load(bad, catalog)); } finally { File.Delete(bad); }
    }

    [Fact]
    public void Two_players_on_one_map_see_each_other_with_server_checked_positions()
    {
        var a = Player("soc01", "Lan Anh");
        var b = Player("soc02", "Minh Khoa", 1);
        Beat(a, 0f); Beat(b, 4f);
        SyncDto sa = Beat(a, 0.5f);
        PeerDto pb = Assert.Single(sa.peers);
        Assert.Equal("Minh Khoa", pb.name);
        Assert.Equal(1, pb.heroClass);
        Assert.Equal(4f, pb.x, 2);
        // a teleport (far beyond run speed) is refused: position corrected back
        SyncDto jump = Beat(b, 60f);
        Assert.True(jump.corrected);
        Assert.Equal(4f, jump.x, 2);
        Assert.Equal(4f, Beat(a, 0.6f).peers[0].x, 2);
        // anim names are whitelisted
        Assert.Equal("idle", Beat(b, 4.2f, anim: "<script>").peers.Length == 0 ? "idle" : Beat(a, 0.7f).peers[0].anim);
        // other map: not visible
        clock += 2000; chars.Travel(b.acc, b.id, "hub_east_meadow", 66f, 0f);
        Beat(b, -36f);
        Assert.Empty(Beat(a, 0.8f).peers);
        // gone quiet: drops off after the timeout; online status follows
        Assert.True(presence.Online(b.id));
        clock += 20000;
        Assert.False(presence.Online(b.id));
    }

    [Fact]
    public void Friends_requests_block_and_the_blocked_disappear()
    {
        var a = Player("soc03", "Bảo Ngọc");
        var b = Player("soc04", "Gia Huy");
        friends.Request(a.acc, a.id, new NameDto { name = "Gia Huy" });
        Assert.Equal("already_requested", Assert.Throws<GameError>(() => friends.Request(a.acc, a.id, new NameDto { name = "gia huy" })).Code);
        Assert.Equal(1, Beat(b, 2f).badges.friendRequests);
        FriendsDto fb = friends.Respond(b.acc, b.id, new IdDto { id = a.id }, true);
        Assert.Equal("Bảo Ngọc", Assert.Single(fb.friends).name);
        Assert.Single(friends.Get(a.acc, a.id).friends);
        Beat(a, 0f);
        Assert.True(Beat(b, 2.2f).peers.Single().friend);
        // block: friendship gone, hidden on the map, chat refused both ways
        friends.Block(b.acc, b.id, new NameDto { name = "Bảo Ngọc" });
        Assert.Empty(friends.Get(a.acc, a.id).friends);
        Assert.Empty(Beat(a, 0.2f).peers);
        Assert.Empty(Beat(b, 2.4f).peers);
        Assert.Equal("blocked", Assert.Throws<GameError>(() => friends.Request(a.acc, a.id, new NameDto { name = "Gia Huy" })).Code);
        clock += 2000;
        Assert.Equal("chat_blocked", Assert.Throws<GameError>(() => chat.Send(a.acc, a.id, new ChatSendDto { channel = "private", to = "Gia Huy", text = "chào" })).Code);
        friends.Unblock(b.acc, b.id, new IdDto { id = a.id });
        Assert.Single(Beat(b, 2.6f).peers);
        // someone else's character cannot be used
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => friends.Get(a.acc, b.id)).Code);
    }

    [Fact]
    public void Chat_world_private_guild_with_masking_spam_limits_and_server_names()
    {
        var a = Player("soc05", "Thu Trang");
        var b = Player("soc06", "Quốc Bảo");
        var c = Player("soc07", "Hải Đăng");
        long cb = Beat(b, 1f).chatCursor, cc = Beat(c, 2f).chatCursor;
        ChatMessageDto m = chat.Send(a.acc, a.id, new ChatSendDto { channel = "world", text = "Xin chào đồ shit cả nhà" });
        Assert.Equal("Thu Trang", m.sender);
        Assert.DoesNotContain("shit", m.text);
        clock += 300;
        Assert.Equal("chat_too_fast", Assert.Throws<GameError>(() => chat.Send(a.acc, a.id, new ChatSendDto { channel = "world", text = "spam" })).Code);
        clock += 1500;
        chat.Send(a.acc, a.id, new ChatSendDto { channel = "private", to = "Quốc Bảo", text = "Hẹn gặp ở Sảnh Kết Hôn nhé" });
        Assert.Equal("chat_too_long", Assert.Throws<GameError>(() => { clock += 1500; return chat.Send(a.acc, a.id, new ChatSendDto { channel = "world", text = new string('a', 121) }); }).Code);
        SyncDto sb = Beat(b, 1.1f, after: cb), sc = Beat(c, 2.1f, after: cc);
        Assert.Equal(2, sb.chat.Length);                                     // world + the private message
        Assert.Equal("private", sb.chat[1].channel);
        Assert.Single(sc.chat);                                              // the third player sees only world chat
        Assert.Equal("not_in_guild", Assert.Throws<GameError>(() => { clock += 1500; return chat.Send(a.acc, a.id, new ChatSendDto { channel = "guild", text = "x" }); }).Code);
        // burst limit: 8 messages / 30 s
        for (int i = 0; i < 5; i++) { clock += 1300; chat.Send(c.acc, c.id, new ChatSendDto { channel = "world", text = "msg " + i }); }
        for (int i = 0; i < 3; i++) { clock += 1300; chat.Send(c.acc, c.id, new ChatSendDto { channel = "world", text = "more " + i }); }
        clock += 1300;
        Assert.Equal("chat_spam", Assert.Throws<GameError>(() => chat.Send(c.acc, c.id, new ChatSendDto { channel = "world", text = "one too many" })).Code);
    }

    [Fact]
    public void Guild_lifecycle_roles_requests_quests_ranking_and_private_hall_instance()
    {
        var lead = Player("soc08", "Bang Chủ An");
        var vice = Player("soc09", "Phó Bang Bình");
        var mem = Player("soc10", "Thành Viên Chi");
        var other = Player("soc11", "Khách Dũng");
        long g0 = Gold(lead.id);
        GuildViewDto v = guilds.Create(lead.acc, lead.id, new GuildCreateDto { name = "Gió Ngàn", emblem = "bell", requestId = "create-giongan" });
        Assert.Equal("leader", v.guild!.myRole);
        Assert.Equal(g0 - cfg.guild.createFeeGold, Gold(lead.id)); // dev config: fee approved
        Assert.Equal("guild_name_taken", Assert.Throws<GameError>(() => guilds.Create(other.acc, other.id, new GuildCreateDto { name = "gió ngàn", emblem = "bell", requestId = "create-dup00" })).Code);
        Assert.Equal("guild_emblem", Assert.Throws<GameError>(() => guilds.Create(other.acc, other.id, new GuildCreateDto { name = "Hoa Đào", emblem = "skull", requestId = "create-bad000" })).Code);
        string gid = v.guild.id;
        // apply → approve; invite → accept
        guilds.Apply(vice.acc, vice.id, new GuildTargetDto { id = gid });
        Assert.Single(guilds.Get(lead.acc, lead.id).guild!.applications);
        guilds.Review(lead.acc, lead.id, new GuildTargetDto { id = vice.id, accept = true });
        guilds.Invite(lead.acc, lead.id, new GuildTargetDto { name = "Thành Viên Chi" });
        Assert.Single(guilds.Get(mem.acc, mem.id).invites);
        guilds.RespondInvite(mem.acc, mem.id, new GuildTargetDto { id = gid, accept = true });
        Assert.Equal(3, guilds.Get(lead.acc, lead.id).guild!.members.Length);
        // roles: vice can kick members only; members cannot invite
        guilds.SetRole(lead.acc, lead.id, new GuildTargetDto { id = vice.id, role = "vice" });
        Assert.Equal("guild_rights", Assert.Throws<GameError>(() => guilds.Invite(mem.acc, mem.id, new GuildTargetDto { name = "Khách Dũng" })).Code);
        Assert.Equal("guild_rights", Assert.Throws<GameError>(() => guilds.Kick(vice.acc, vice.id, new GuildTargetDto { id = lead.id })).Code);
        // contributions: exact tier only, replay-safe, devotion + fund + exp
        long gm = Gold(mem.id);
        guilds.Contribute(mem.acc, mem.id, new GuildContributeDto { amount = 5000, requestId = "fund-req-0001" });
        GuildViewDto again = guilds.Contribute(mem.acc, mem.id, new GuildContributeDto { amount = 5000, requestId = "fund-req-0001" });
        Assert.Equal(gm - 5000, Gold(mem.id));
        Assert.Equal(5000, again.guild!.fundGold);
        Assert.Equal(500, again.guild.myDevotion);
        Assert.Equal("contribution_amount", Assert.Throws<GameError>(() => guilds.Contribute(mem.acc, mem.id, new GuildContributeDto { amount = 1234, requestId = "fund-req-0002" })).Code);
        // guild quest from server events: counted once per guild per day, rewards every member when complete
        for (int i = 0; i < 25; i++) guilds.Record(mem.acc, mem.id, "kill:any", 8);
        GuildDto gd = guilds.Get(lead.acc, lead.id).guild!;
        Assert.True(gd.quests.Single(q => q.id == "gq_hunt").done);
        Assert.Equal(500 + 300, gd.exp);
        Assert.Equal(30, gd.members.Single(x => x.id == lead.id).devotion);
        // guild chat reaches members only
        long cv = Beat(vice, 1f).chatCursor, co = Beat(other, 2f).chatCursor;
        chat.Send(mem.acc, mem.id, new ChatSendDto { channel = "guild", text = "Tối nay đi săn nhé" });
        Assert.Single(Beat(vice, 1.1f, after: cv).chat);
        Assert.Empty(Beat(other, 2.1f, after: co).chat);
        // ranking
        Assert.Equal("Gió Ngàn", guilds.Ranking(other.id).rows.Single().name);
        // Guild Hall: members share a private instance; outsiders do not see them
        foreach (var p in new[] { lead, mem, other })
        {
            clock += 2000; chars.Travel(p.acc, p.id, "hub_guild_hall", 74f, 0f);
        }
        Beat(lead, -14f); Beat(other, -14f);
        Assert.Contains(Beat(mem, -13f).peers, x => x.id == lead.id);
        Assert.DoesNotContain(Beat(mem, -13.2f).peers, x => x.id == other.id);
        Assert.Empty(Beat(other, -14.1f).peers);
        // leader cannot leave while others remain; transfer, then leave
        Assert.Equal("leader_must_transfer", Assert.Throws<GameError>(() => guilds.Leave(lead.acc, lead.id)).Code);
        guilds.SetRole(lead.acc, lead.id, new GuildTargetDto { id = vice.id, role = "leader" });
        Assert.Null(guilds.Leave(lead.acc, lead.id).guild);
        Assert.Equal("leader", guilds.Get(vice.acc, vice.id).guild!.myRole);
        guilds.Kick(vice.acc, vice.id, new GuildTargetDto { id = mem.id });
        Assert.Null(guilds.Get(mem.acc, mem.id).guild);
        guilds.DisbandGuild(vice.acc, vice.id);
        Assert.Empty(guilds.Ranking(other.id).rows);
    }

    [Fact]
    public void Production_draft_charges_nothing_and_keeps_the_guild_economy_closed()
    {
        cfg = SocialConfig.Load(Cfg("social.json"), catalog);
        Services();
        var a = Player("soc12", "Tiết Kiệm");
        long g0 = Gold(a.id);
        guilds.Create(a.acc, a.id, new GuildCreateDto { name = "Chờ Duyệt", emblem = "lotus" });
        Assert.Equal(g0, Gold(a.id)); // fee is a proposal: not charged
        Assert.Equal("economy_pending", Assert.Throws<GameError>(() => guilds.Contribute(a.acc, a.id, new GuildContributeDto { amount = 1000, requestId = "fund-req-0003" })).Code);
        for (int i = 0; i < 30; i++) guilds.Record(a.acc, a.id, "kill:any", 10);
        Assert.Equal(0, guilds.Get(a.acc, a.id).guild!.exp); // progress counts, rewards wait for approval
    }

    [Fact]
    public void Proposal_engagement_two_player_ceremony_marriage_gifts_and_persistence()
    {
        var a = Player("soc13", "Chú Rể Long");
        var b = Player("soc14", "Cô Dâu Mai", 1);
        var c = Player("soc15", "Khách Mời Hoa", 2);
        Assert.Equal("not_friends", Assert.Throws<GameError>(() => marriage.Propose(a.acc, a.id, new ProposeDto { name = "Cô Dâu Mai" })).Code);
        friends.Request(a.acc, a.id, new NameDto { name = "Cô Dâu Mai" });
        friends.Respond(b.acc, b.id, new IdDto { id = a.id }, true);
        long ga = Gold(a.id);
        marriage.Propose(a.acc, a.id, new ProposeDto { name = "Cô Dâu Mai" });
        Assert.Equal(ga - cfg.marriage.ringPriceGold, Gold(a.id)); // dev config: the ring is paid
        Assert.Equal(1, Beat(b, 1f).badges.proposals);
        // a declined proposal is refunded
        marriage.Respond(b.acc, b.id, new GuildTargetDto { id = a.id, accept = false });
        Assert.Equal(ga, Gold(a.id));
        clock += 1000;
        marriage.Propose(a.acc, a.id, new ProposeDto { name = "Cô Dâu Mai" });
        MarriageDto mb = marriage.Respond(b.acc, b.id, new GuildTargetDto { id = a.id, accept = true });
        Assert.Equal("engaged", mb.status);
        Assert.Equal("Chú Rể Long", mb.partnerName);
        Assert.Equal("partner_bound", Assert.Throws<GameError>(() => marriage.Propose(c.acc, c.id, new ProposeDto { name = "Cô Dâu Mai" })).Code);
        // the ceremony needs both in the Wedding Hall
        Assert.Equal("partner_absent", Assert.Throws<GameError>(() => marriage.StartCeremony(a.acc, a.id)).Code);
        foreach (var p in new[] { a, b, c }) { clock += 2000; chars.Travel(p.acc, p.id, "hub_wedding_hall", -56f, 0f); }
        Beat(a, -16f); Beat(b, -15f); Beat(c, -14f);
        marriage.StartCeremony(a.acc, a.id);
        Assert.Equal("ceremony_wait", Assert.Throws<GameError>(() => marriage.ConfirmCeremony(a.acc, a.id)).Code);
        Assert.Equal("invited", Beat(b, -15.1f).ceremony!.state);
        marriage.ConfirmCeremony(b.acc, b.id);
        // a guest sees the same server timeline
        CeremonyDto seen = Beat(c, -14.1f).ceremony!;
        Assert.Equal("running", seen.state);
        Assert.Equal(new[] { "intro", "vows", "rings", "cake", "toast", "kiss", "finale" }, seen.steps.Select(s => s.id));
        Assert.Equal(seen.endMs, seen.steps[^1].startMs + seen.steps[^1].ms);
        // everyone keeps heartbeating through the ceremony
        while (clock < seen.endMs + 500) { Beat(a, -16f); Beat(b, -15f); Beat(c, -14f); clock += 1000; }
        Assert.Equal("done", Beat(c, -14.2f).ceremony!.state);
        Assert.Equal("married", marriage.Get(a.acc, a.id).status);
        // wedding gifts: owned by both, once
        var shop = new CosmeticShop(t.Db, catalog);
        foreach (var p in new[] { a, b })
            Assert.All(cfg.marriage.gifts, gift => Assert.Contains(gift, shop.Wardrobe(p.acc, p.id).owned));
        // the partner shows on the map
        Assert.Equal("Cô Dâu Mai", Beat(c, -14.3f).peers.Single(x => x.id == a.id).partner);
        // persistence: a new server process on the same database still knows everything
        Services();
        Assert.Equal("married", marriage.Get(b.acc, b.id).status);
        Assert.Single(friends.Get(a.acc, a.id).friends);
        // divorce ends it; gifts are kept
        marriage.Divorce(a.acc, a.id);
        Assert.Equal("none", marriage.Get(b.acc, b.id).status);
        Assert.Contains(cfg.marriage.gifts[0], new CosmeticShop(t.Db, catalog).Wardrobe(a.acc, a.id).owned);
    }

    [Fact]
    public void A_partner_leaving_the_hall_cancels_the_ceremony_and_they_stay_engaged()
    {
        var a = Player("soc16", "Rời Sảnh Tâm");
        var b = Player("soc17", "Ở Lại Vy");
        friends.Request(a.acc, a.id, new NameDto { name = "Ở Lại Vy" });
        friends.Respond(b.acc, b.id, new IdDto { id = a.id }, true);
        marriage.Propose(a.acc, a.id, new ProposeDto { name = "Ở Lại Vy" });
        marriage.Respond(b.acc, b.id, new GuildTargetDto { id = a.id, accept = true });
        foreach (var p in new[] { a, b }) { clock += 2000; chars.Travel(p.acc, p.id, "hub_wedding_hall", -56f, 0f); }
        Beat(a, -16f); Beat(b, -15f);
        marriage.StartCeremony(b.acc, b.id);
        marriage.ConfirmCeremony(a.acc, a.id);
        clock += 3000; Beat(b, -15f);
        clock += 12000; // a went quiet (left / disconnected)
        Assert.Equal("cancelled", Beat(b, -15.1f).ceremony!.state);
        Assert.Equal("engaged", marriage.Get(a.acc, a.id).status);
    }
}
