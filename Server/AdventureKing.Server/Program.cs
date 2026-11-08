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
builder.Services.AddSingleton(sp => new CharacterService(sp.GetRequiredService<Database>(), sp.GetRequiredService<NameRules>()));
builder.Services.AddSingleton(sp => new DiamondStore(sp.GetRequiredService<Database>(), vip));
builder.Services.AddKeyedSingleton("login", new AttemptLimiter(10, TimeSpan.FromMinutes(5)));
builder.Services.AddKeyedSingleton("register", new AttemptLimiter(5, TimeSpan.FromHours(1)));

var app = builder.Build();

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
api.MapPost("/characters/{id}/tutorial/{step:int}", (string id, int step, HttpContext ctx, AccountService accounts, CharacterService chars) =>
    chars.CompleteTutorialStep(accounts.Authenticate(Bearer(ctx)), id, step));

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
