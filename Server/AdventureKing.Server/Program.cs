using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;
using AdventureKing.Server.Security;

// Adventure King local game server (PC test environment).
//   dotnet run --project Server/AdventureKing.Server              -> http://127.0.0.1:5080 (loopback only)
//   dotnet run --project Server/AdventureKing.Server -- --backup  -> consistent online backup
//   dotnet run --project Server/AdventureKing.Server -- --restore <file> (server stopped)
//   dotnet run --project Server/AdventureKing.Server -- --verify <file>

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration.GetSection("AdventureKing");
string contentRoot = builder.Environment.ContentRootPath;
string dbPath = Path.GetFullPath(Path.Combine(contentRoot, cfg["DatabasePath"] ?? "data/adventureking.db"));
string backupDir = Path.GetFullPath(Path.Combine(contentRoot, cfg["BackupDirectory"] ?? "data/backups"));

if (args.Length > 0 && args[0] is "--backup" or "--restore" or "--verify")
{
    switch (args[0])
    {
        case "--backup":
            Console.WriteLine("Backup written: " + BackupService.Backup(new Database(dbPath), backupDir));
            return;
        case "--verify":
            Console.WriteLine($"OK, accounts={BackupService.Verify(args[1])}");
            return;
        case "--restore":
            BackupService.Restore(args[1], dbPath);
            Console.WriteLine("Restored " + args[1] + " -> " + dbPath);
            return;
    }
}

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 16 * 1024);
if (string.IsNullOrEmpty(builder.Configuration["urls"]))
    builder.WebHost.UseUrls(cfg["Url"] ?? "http://127.0.0.1:5080");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = null); // DTOs already use client field names

long[] vip = cfg.GetSection("VipThresholdsVnd").Get<long[]>() ?? Array.Empty<long>();
builder.Services.AddSingleton(_ => new Database(dbPath));
builder.Services.AddSingleton(_ => new NameRules(Path.Combine(contentRoot, "config", "name_blocklist.txt")));
builder.Services.AddSingleton(sp => new AccountService(sp.GetRequiredService<Database>()));
long starterGold = cfg.GetValue<long?>("StarterGold") ?? 5000; // PROPOSAL pending owner approval (owner declined to treat 800 as approved, 2026-11-10)
string cosmeticsPath = Path.Combine(contentRoot, cfg["CosmeticCatalogPath"] ?? "config/cosmetics.json");
builder.Services.AddSingleton(sp => new CharacterService(sp.GetRequiredService<Database>(), sp.GetRequiredService<NameRules>(), starterGold));
builder.Services.AddSingleton(sp => new CosmeticShop(sp.GetRequiredService<Database>(), CosmeticCatalog.Load(cosmeticsPath)));
builder.Services.AddSingleton(sp => new DiamondStore(sp.GetRequiredService<Database>(), vip));
string monstersPath = Path.Combine(contentRoot, cfg["MonsterConfigPath"] ?? "config/monsters.json");
string skillsPath = Path.Combine(contentRoot, cfg["SkillConfigPath"] ?? "config/skills.json");
builder.Services.AddSingleton(sp => new FarmService(sp.GetRequiredService<Database>(), FarmConfig.Load(monstersPath), skills: SkillConfig.Load(skillsPath)));
string itemsPath = Path.Combine(contentRoot, cfg["ItemConfigPath"] ?? "config/items.json");
builder.Services.AddSingleton(sp => new InventoryService(sp.GetRequiredService<Database>(), ItemConfig.Load(itemsPath), sp.GetRequiredService<FarmService>()));
builder.Services.AddSingleton(sp => new ShopService(sp.GetRequiredService<Database>(), sp.GetRequiredService<InventoryService>()));
string petsPath = Path.Combine(contentRoot, cfg["PetConfigPath"] ?? "config/pets.json");
builder.Services.AddSingleton(sp => new PetService(sp.GetRequiredService<Database>(), PetConfig.Load(petsPath), sp.GetRequiredService<InventoryService>()));
string questsPath = Path.Combine(contentRoot, cfg["QuestConfigPath"] ?? "config/quests.json");
builder.Services.AddSingleton(sp => new QuestService(sp.GetRequiredService<Database>(),
    QuestConfig.Load(questsPath, sp.GetRequiredService<InventoryService>().Config), sp.GetRequiredService<InventoryService>()));
// Production config = the owner's DRAFT (enabled=false) until rewards are approved; tests point this at welfare.dev.json.
string welfarePath = Path.Combine(contentRoot, cfg["WelfareConfigPath"] ?? "config/welfare.json");
builder.Services.AddSingleton(sp => new CheckinService(sp.GetRequiredService<Database>(), WelfareConfig.Load(welfarePath), sp.GetRequiredService<CosmeticShop>().Catalog));
// Events / Phúc Lợi: production = DRAFT disabled (claims answer "chờ duyệt"); tests point EventConfigPath at events.dev.json.
string eventsPath = Path.Combine(contentRoot, cfg["EventConfigPath"] ?? "config/events.json");
builder.Services.AddSingleton(sp => new EventService(sp.GetRequiredService<Database>(),
    EventConfig.Load(eventsPath, sp.GetRequiredService<InventoryService>().Config), sp.GetRequiredService<InventoryService>()));
builder.Services.AddSingleton(sp => new MailService(sp.GetRequiredService<Database>(), sp.GetRequiredService<InventoryService>()));
builder.Services.AddSingleton(sp => new GiftcodeService(sp.GetRequiredService<Database>(), sp.GetRequiredService<InventoryService>()));
builder.Services.AddSingleton(sp => new RankingService(sp.GetRequiredService<Database>(), sp.GetRequiredService<FarmService>()));
builder.Services.AddSingleton(sp => new TitleService(sp.GetRequiredService<Database>()));
// Social layer (Phase D): production = DRAFT (no fees charged, guild economy closed); tests use social.dev.json.
string socialPath = Path.Combine(contentRoot, cfg["SocialConfigPath"] ?? "config/social.json");
builder.Services.AddSingleton(sp => SocialConfig.Load(socialPath, sp.GetRequiredService<CosmeticShop>().Catalog));
builder.Services.AddSingleton(_ => new PresenceService());
builder.Services.AddSingleton(sp => new FriendService(sp.GetRequiredService<Database>(), sp.GetRequiredService<PresenceService>()));
builder.Services.AddSingleton(sp => new ChatService(sp.GetRequiredService<Database>(), sp.GetRequiredService<SocialConfig>(), sp.GetRequiredService<NameRules>()));
builder.Services.AddSingleton(sp => new GuildService(sp.GetRequiredService<Database>(), sp.GetRequiredService<SocialConfig>(), sp.GetRequiredService<NameRules>(), sp.GetRequiredService<PresenceService>()));
builder.Services.AddSingleton(sp => new MarriageService(sp.GetRequiredService<Database>(), sp.GetRequiredService<SocialConfig>(), sp.GetRequiredService<PresenceService>()));
builder.Services.AddSingleton(sp => new SyncService(sp.GetRequiredService<Database>(), sp.GetRequiredService<PresenceService>(), sp.GetRequiredService<ChatService>(), sp.GetRequiredService<MarriageService>()));
builder.Services.AddKeyedSingleton("login", new AttemptLimiter(10, TimeSpan.FromMinutes(5)));
builder.Services.AddKeyedSingleton("register", new AttemptLimiter(5, TimeSpan.FromHours(1)));

var app = builder.Build();
app.Services.GetRequiredService<InventoryService>(); // wires equipment bonuses into farm combat from the start
{
    // quest progress comes from server events only (plus the small client allowlist in QuestService.ClientEvent)
    var quests = app.Services.GetRequiredService<QuestService>();
    var events = app.Services.GetRequiredService<EventService>();
    var guilds = app.Services.GetRequiredService<GuildService>();
    Action<string, string, string, int> record = (a, c, e, n) =>
    {
        quests.Record(a, c, e, n);
        try { events.Record(a, c, e, n); } catch (Exception ex) { Console.Error.WriteLine("event hook: " + ex.Message); }
        try { guilds.Record(a, c, e, n); } catch (Exception ex) { Console.Error.WriteLine("guild hook: " + ex.Message); }
    };
    app.Services.GetRequiredService<RankingService>().GuildBoard = guilds.Ranking;
    app.Services.GetRequiredService<FarmService>().QuestEvent = record;
    app.Services.GetRequiredService<InventoryService>().QuestEvent = record;
    app.Services.GetRequiredService<ShopService>().QuestEvent = record;
    app.Services.GetRequiredService<CharacterService>().QuestEvent = record;
    var pets = app.Services.GetRequiredService<PetService>();
    var farmSvc = app.Services.GetRequiredService<FarmService>();
    farmSvc.PetTick = pets.Tick;
    farmSvc.PetCanCollect = pets.CanCollect;
    farmSvc.PetReach = pets.Config.pickupReach;
    farmSvc.PetAutoPotion = pets.AutoPotion;
}

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (GameError e)
    {
        ctx.Response.StatusCode = e.Status;
        await ctx.Response.WriteAsJsonAsync(new ErrorDto { code = e.Code, message = e.Message });
    }
    catch (Exception e) when (e is JsonException or BadHttpRequestException)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new ErrorDto { code = "bad_request", message = "Dữ liệu gửi lên không hợp lệ." });
    }
});

static string Bearer(HttpContext ctx)
{
    string h = ctx.Request.Headers.Authorization.ToString();
    return h.StartsWith("Bearer ", StringComparison.Ordinal) ? h[7..].Trim() : "";
}
static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
static void Limit(AttemptLimiter l, string key)
{
    if (!l.TryAcquire(key)) throw new GameError("rate_limited", "Bạn thao tác quá nhanh. Hãy thử lại sau ít phút.", 429);
}

var api = app.MapGroup("/api/v1");

api.MapGet("/health", () => new { status = "ok", schema = 1, paymentsEnabled = DiamondStore.PaymentsEnabled, socialLogin = false });

// ---------------------------------------------------------------- auth
api.MapPost("/auth/register", (CredentialsDto body, HttpContext ctx, AccountService accounts, [FromKeyedServices("register")] AttemptLimiter limiter) =>
{
    Limit(limiter, Ip(ctx));
    SessionInfo s = accounts.Register(body.account, body.password);
    return new SessionDto { token = s.Token, accountId = s.AccountId, expiresMs = s.ExpiresMs };
});
api.MapPost("/auth/login", (CredentialsDto body, HttpContext ctx, AccountService accounts, [FromKeyedServices("login")] AttemptLimiter limiter) =>
{
    Limit(limiter, Ip(ctx) + "|" + (body.account ?? "").Trim().ToLowerInvariant());
    SessionInfo s = accounts.Login(body.account, body.password);
    return new SessionDto { token = s.Token, accountId = s.AccountId, expiresMs = s.ExpiresMs };
});
api.MapPost("/auth/logout", (HttpContext ctx, AccountService accounts) =>
{
    accounts.Logout(Bearer(ctx));
    return new ErrorDto { code = "ok" };
});
// Real Google/Facebook sign-in needs the owner's OAuth client IDs and server-side ID-token verification.
// Until then these endpoints FAIL CLOSED — they never create or return a session.
api.MapPost("/auth/google", () => Results.Json(new ErrorDto { code = "social_not_configured", message = "Đăng nhập Google chưa được cấu hình trên máy chủ." }, statusCode: 501));
api.MapPost("/auth/facebook", () => Results.Json(new ErrorDto { code = "social_not_configured", message = "Đăng nhập Facebook chưa được cấu hình trên máy chủ." }, statusCode: 501));

// ---------------------------------------------------------------- characters
api.MapGet("/profile", (HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.LoadProfile(accounts.Authenticate(Bearer(ctx))));
api.MapPost("/characters", (CreateCharacterDto body, HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.Create(accounts.Authenticate(Bearer(ctx)), body.characterName, body.heroClass, body.slotIndex));
api.MapPost("/characters/name-check", (CreateCharacterDto body, NameRules names) =>
{
    NameCheck r = names.Check(body.characterName);
    return new ErrorDto { code = r.Ok ? "ok" : r.Code, message = r.Message };
});
api.MapPost("/characters/{id}/enter", (string id, HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.Enter(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/location", (string id, LocationDto body, HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.SaveLocation(accounts.Authenticate(Bearer(ctx)), id, body.mapId, body.x, body.y));
api.MapPost("/characters/{id}/travel/{portalId}", (string id, string portalId, LocationDto body, HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.Travel(accounts.Authenticate(Bearer(ctx)), id, portalId, body.x, body.y));
api.MapPost("/characters/{id}/tutorial/{step:int}", (string id, int step, HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.CompleteTutorialStep(accounts.Authenticate(Bearer(ctx)), id, step));

// ---------------------------------------------------------------- fashion shop + wardrobe (Gold pieces, Diamond sets)
api.MapGet("/characters/{id}/wardrobe", (string id, HttpContext ctx, AccountService accounts, CosmeticShop shop) =>
    shop.Wardrobe(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/wardrobe/purchase", (string id, PurchaseRequestDto body, HttpContext ctx, AccountService accounts, CosmeticShop shop) =>
    shop.Purchase(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/wardrobe/equip", (string id, OutfitDto body, HttpContext ctx, AccountService accounts, CosmeticShop shop) =>
    shop.Equip(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- farm: monsters, Gold drops (server-authoritative)
api.MapGet("/characters/{id}/farm", (string id, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.State(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/farm/attack", (string id, AttackRequestDto body, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.Attack(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/farm/pickup", (string id, PickupRequestDto body, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.Pickup(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/farm/presence", (string id, PresenceRequestDto body, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.Presence(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/farm/revive", (string id, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.Revive(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/farm/skill", (string id, SkillRequestDto body, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.Skill(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapGet("/skills", (FarmService farm) => farm.Skills); // public: names, cooldowns, MP costs (display only; the server decides)

// ---------------------------------------------------------------- inventory (server-owned bag + basic equipment)
api.MapGet("/characters/{id}/inventory", (string id, HttpContext ctx, AccountService accounts, InventoryService inv, FarmService _) =>
    inv.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/inventory/use", (string id, UseItemRequestDto body, HttpContext ctx, AccountService accounts, InventoryService inv) =>
    inv.Use(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/inventory/equip", (string id, EquipRequestDto body, HttpContext ctx, AccountService accounts, InventoryService inv) =>
    inv.Equip(accounts.Authenticate(Bearer(ctx)), id, body));
// ---------------------------------------------------------------- NPC shops + ticket travel
api.MapGet("/shops/{shopId}", (string shopId, ShopService shops) => shops.Get(shopId));
api.MapPost("/characters/{id}/shop/buy", (string id, BuyRequestDto body, HttpContext ctx, AccountService accounts, ShopService shops) =>
    shops.Buy(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/travel/ticket", (string id, TicketRequestDto body, HttpContext ctx, AccountService accounts, ShopService shops) =>
    shops.UseTicket(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- pets
api.MapGet("/characters/{id}/pets", (string id, HttpContext ctx, AccountService accounts, PetService pets) => pets.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/pets/buy", (string id, PetBuyDto body, HttpContext ctx, AccountService accounts, PetService pets) => pets.Buy(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/pets/summon", (string id, PetIdDto body, HttpContext ctx, AccountService accounts, PetService pets) => pets.Summon(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/pets/dismiss", (string id, HttpContext ctx, AccountService accounts, PetService pets) => pets.Dismiss(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/pets/feed", (string id, PetFeedDto body, HttpContext ctx, AccountService accounts, PetService pets) => pets.Feed(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/pets/auto-potion", (string id, AutoPotionDto body, HttpContext ctx, AccountService accounts, PetService pets) => pets.SetAutoPotion(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/farm/pet-pickup", (string id, PickupRequestDto body, HttpContext ctx, AccountService accounts, FarmService farm) =>
    farm.PetPickup(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- quests
api.MapGet("/characters/{id}/quests", (string id, HttpContext ctx, AccountService accounts, QuestService quests) =>
    quests.Log(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/quests/claim", (string id, QuestClaimDto body, HttpContext ctx, AccountService accounts, QuestService quests) =>
    quests.Claim(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/quests/event", (string id, QuestEventDto body, HttpContext ctx, AccountService accounts, QuestService quests) =>
    quests.ClientEvent(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/inventory/unequip", (string id, UnequipRequestDto body, HttpContext ctx, AccountService accounts, InventoryService inv) =>
    inv.Unequip(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- events / welfare, mail, giftcode
api.MapGet("/characters/{id}/welfare", (string id, HttpContext ctx, AccountService accounts, EventService ev) => ev.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/welfare/claim", (string id, WelfareClaimDto body, HttpContext ctx, AccountService accounts, EventService ev) => ev.Claim(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/online/ping", (string id, HttpContext ctx, AccountService accounts, EventService ev) =>
{
    ev.Ping(accounts.Authenticate(Bearer(ctx)), id);
    return Results.NoContent();
});
api.MapGet("/characters/{id}/mail", (string id, HttpContext ctx, AccountService accounts, MailService mail) => mail.List(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/mail/read", (string id, MailIdDto body, HttpContext ctx, AccountService accounts, MailService mail) => mail.Read(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/mail/claim", (string id, MailIdDto body, HttpContext ctx, AccountService accounts, MailService mail) => mail.Claim(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/giftcode", (string id, GiftcodeDto body, HttpContext ctx, AccountService accounts, GiftcodeService codes) => codes.Redeem(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- multiplayer heartbeat, friends, chat
api.MapPost("/characters/{id}/sync", (string id, SyncRequestDto body, HttpContext ctx, AccountService accounts, SyncService sync) => sync.Sync(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapGet("/characters/{id}/friends", (string id, HttpContext ctx, AccountService accounts, FriendService f) => f.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/friends/request", (string id, NameDto body, HttpContext ctx, AccountService accounts, FriendService f) => f.Request(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/friends/accept", (string id, IdDto body, HttpContext ctx, AccountService accounts, FriendService f) => f.Respond(accounts.Authenticate(Bearer(ctx)), id, body, true));
api.MapPost("/characters/{id}/friends/decline", (string id, IdDto body, HttpContext ctx, AccountService accounts, FriendService f) => f.Respond(accounts.Authenticate(Bearer(ctx)), id, body, false));
api.MapPost("/characters/{id}/friends/remove", (string id, IdDto body, HttpContext ctx, AccountService accounts, FriendService f) => f.Remove(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/friends/block", (string id, NameDto body, HttpContext ctx, AccountService accounts, FriendService f) => f.Block(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/friends/unblock", (string id, IdDto body, HttpContext ctx, AccountService accounts, FriendService f) => f.Unblock(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/chat", (string id, ChatSendDto body, HttpContext ctx, AccountService accounts, ChatService chat) => chat.Send(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- guilds
api.MapGet("/characters/{id}/guild", (string id, HttpContext ctx, AccountService accounts, GuildService g) => g.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/guild/create", (string id, GuildCreateDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.Create(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/apply", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.Apply(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/invite", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.Invite(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/invite/respond", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.RespondInvite(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/review", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.Review(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/leave", (string id, HttpContext ctx, AccountService accounts, GuildService g) => g.Leave(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/guild/disband", (string id, HttpContext ctx, AccountService accounts, GuildService g) => g.DisbandGuild(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/guild/kick", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.Kick(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/role", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.SetRole(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/notice", (string id, GuildNoticeDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.SetNotice(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/guild/contribute", (string id, GuildContributeDto body, HttpContext ctx, AccountService accounts, GuildService g) => g.Contribute(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- marriage + ceremony
api.MapGet("/characters/{id}/marriage", (string id, HttpContext ctx, AccountService accounts, MarriageService m) => m.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/marriage/propose", (string id, ProposeDto body, HttpContext ctx, AccountService accounts, MarriageService m) => m.Propose(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/marriage/respond", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, MarriageService m) => m.Respond(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/marriage/cancel-proposal", (string id, GuildTargetDto body, HttpContext ctx, AccountService accounts, MarriageService m) => m.CancelProposal(accounts.Authenticate(Bearer(ctx)), id, body));
api.MapPost("/characters/{id}/marriage/divorce", (string id, HttpContext ctx, AccountService accounts, MarriageService m) => m.Divorce(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/marriage/ceremony/start", (string id, HttpContext ctx, AccountService accounts, MarriageService m) => m.StartCeremony(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/marriage/ceremony/confirm", (string id, HttpContext ctx, AccountService accounts, MarriageService m) => m.ConfirmCeremony(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/marriage/ceremony/cancel", (string id, HttpContext ctx, AccountService accounts, MarriageService m) => m.CancelCeremony(accounts.Authenticate(Bearer(ctx)), id));

// ---------------------------------------------------------------- leaderboards + nameplate frames
api.MapGet("/characters/{id}/rankings/{board}", (string id, string board, HttpContext ctx, AccountService accounts, RankingService ranks) => ranks.Get(accounts.Authenticate(Bearer(ctx)), id, board));
api.MapGet("/characters/{id}/titles", (string id, HttpContext ctx, AccountService accounts, TitleService titles) => titles.Get(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/titles/equip", (string id, TitleIdDto body, HttpContext ctx, AccountService accounts, TitleService titles) => titles.Equip(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- 7-day check-in (server clock, VN calendar day)
api.MapGet("/characters/{id}/checkin", (string id, HttpContext ctx, AccountService accounts, CheckinService checkin) =>
    checkin.Status(accounts.Authenticate(Bearer(ctx)), id));
api.MapPost("/characters/{id}/checkin/claim", (string id, CheckinClaimDto body, HttpContext ctx, AccountService accounts, CheckinService checkin) =>
    checkin.Claim(accounts.Authenticate(Bearer(ctx)), id, body));

// ---------------------------------------------------------------- diamond store (catalog only; payments disabled)
api.MapGet("/store/catalog", (HttpContext ctx, AccountService accounts, DiamondStore store) =>
    new CatalogDto { paymentsEnabled = DiamondStore.PaymentsEnabled, packages = store.CatalogFor(accounts.Authenticate(Bearer(ctx))) });
api.MapPost("/store/purchase/{packageId}", (string packageId, HttpContext ctx, AccountService accounts, DiamondStore store) =>
{
    store.RequestPurchase(accounts.Authenticate(Bearer(ctx)), packageId); // always throws payments_disabled
    return Results.StatusCode(500);
});
api.MapGet("/store/history", (HttpContext ctx, AccountService accounts, DiamondStore store) => store.History(accounts.Authenticate(Bearer(ctx))));
api.MapGet("/store/month-card", (HttpContext ctx, AccountService accounts, DiamondStore store) => store.MonthCardStatus(accounts.Authenticate(Bearer(ctx))));
api.MapPost("/store/month-card/claim", (HttpContext ctx, AccountService accounts, DiamondStore store) =>
    new { diamonds = store.ClaimMonthCard(accounts.Authenticate(Bearer(ctx))).Balance });
api.MapGet("/vip", (HttpContext ctx, AccountService accounts, DiamondStore store) =>
{
    var (level, paid, configured) = store.Vip(accounts.Authenticate(Bearer(ctx)));
    return new { level, paidVnd = paid, configured };
});

app.Logger.LogInformation("Adventure King server: database {Db}", dbPath);
app.Run();

public sealed class CredentialsDto { public string? account { get; set; } public string? password { get; set; } }
public sealed class SessionDto { public string token { get; set; } = ""; public string accountId { get; set; } = ""; public long expiresMs { get; set; } }
public sealed class CreateCharacterDto { public string? characterName { get; set; } public int heroClass { get; set; } public int slotIndex { get; set; } }
public sealed class LocationDto { public string? mapId { get; set; } public float x { get; set; } public float y { get; set; } }
public sealed class ErrorDto { public string code { get; set; } = ""; public string message { get; set; } = ""; }
public sealed class CatalogDto { public bool paymentsEnabled { get; set; } public StorePackageDto[] packages { get; set; } = Array.Empty<StorePackageDto>(); }

public partial class Program { }
