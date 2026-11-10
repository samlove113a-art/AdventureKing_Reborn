using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Tests;

public class BackupRestoreTests : IDisposable
{
    private readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    [Fact]
    public void Backup_Then_Restore_Brings_Back_Exact_State()
    {
        var accounts = new AccountService(t.Db);
        var chars = new CharacterService(t.Db, new NameRules());
        string acc = accounts.Register("backup01", "backup-pass-01").AccountId;
        chars.Create(acc, "Lưu Trữ", 0, 0);
        string backup = BackupService.Backup(t.Db, Path.Combine(t.Dir, "backups"));
        Assert.Equal(1, BackupService.Verify(backup));

        // Disaster: data changed / lost after the backup.
        chars.Create(acc, "Mất Tích", 1, 1);
        using (var c = t.Db.Open()) Database.Exec(c, null, "DELETE FROM characters WHERE name='Lưu Trữ'");
        Assert.Single(chars.LoadProfile(acc).characters);

        SqliteConnection.ClearAllPools();
        BackupService.Restore(backup, t.Path_);
        var restored = new CharacterService(new Database(t.Path_), new NameRules());
        CharacterDto only = Assert.Single(restored.LoadProfile(acc).characters);
        Assert.Equal("Lưu Trữ", only.characterName);
        Assert.Single(Directory.GetFiles(t.Dir, "game.db.pre-restore-*"));
    }

    [Fact]
    public void A_v10_database_migrates_to_v11_keeping_data_and_social_rows_survive_backup_restore()
    {
        var accounts = new AccountService(t.Db);
        var chars = new CharacterService(t.Db, new NameRules());
        string acc = accounts.Register("migrate01", "migrate-pass-01").AccountId;
        string id = chars.Create(acc, "Di Trú", 0, 0).characterId;
        // turn this database back into a v10 one (drop the v11 tables)
        using (var c = t.Db.Open())
        {
            foreach (string tbl in new[] { "friend_requests", "friends", "blocks", "chat_messages", "guild_quest_progress", "guild_requests", "guild_members", "guilds", "proposals", "marriages" })
                Database.Exec(c, null, $"DROP TABLE {tbl}");
            Database.Exec(c, null, "PRAGMA user_version=10;");
        }
        SqliteConnection.ClearAllPools();
        var db = new Database(t.Path_); // opening migrates
        using (var c = db.Open())
        {
            Assert.Equal(11L, Database.Scalar<long>(c, null, "PRAGMA user_version"));
            Assert.Equal("Di Trú", Database.Scalar<string>(c, null, "SELECT name FROM characters WHERE id=$c", ("$c", id)));
            Database.Exec(c, null, "INSERT INTO guilds(id, name, name_key, emblem, created_ms) VALUES('g1','Lưu Bang','luubang','bell',1)");
            Database.Exec(c, null, "INSERT INTO guild_members(character_id, guild_id, role, joined_ms) VALUES($c,'g1','leader',1)", ("$c", id));
        }
        string backup = BackupService.Backup(db, Path.Combine(t.Dir, "backups"));
        using (var c = db.Open()) Database.Exec(c, null, "DELETE FROM guild_members"); // disaster
        SqliteConnection.ClearAllPools();
        BackupService.Restore(backup, t.Path_);
        using (var c = new Database(t.Path_).Open())
            Assert.Equal("leader", Database.Scalar<string>(c, null, "SELECT role FROM guild_members WHERE character_id=$c", ("$c", id)));
    }

    [Fact]
    public void Corrupt_Backup_Is_Refused()
    {
        string bad = Path.Combine(t.Dir, "bad.db");
        File.WriteAllBytes(bad, new byte[4096]);
        Assert.ThrowsAny<Exception>(() => BackupService.Verify(bad));
        Assert.ThrowsAny<Exception>(() => BackupService.Restore(bad, t.Path_));
    }
}

public class HttpApiTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient http;

    public HttpApiTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("AdventureKing:DatabasePath", t.Path_)
             .UseSetting("AdventureKing:BackupDirectory", Path.Combine(t.Dir, "backups")));
        http = factory.CreateClient();
    }

    public void Dispose()
    {
        http.Dispose();
        factory.Dispose();
        t.Dispose();
    }

    private sealed class Session { public string token { get; set; } = ""; public string accountId { get; set; } = ""; }
    private sealed class Err { public string code { get; set; } = ""; public string message { get; set; } = ""; }

    [Fact]
    public async Task Full_Entry_Flow_Register_Create_Enter_Survives_Restart()
    {
        var reg = await http.PostAsJsonAsync("/api/v1/auth/register", new { account = "httpuser1", password = "http-pass-001" });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        Session s = (await reg.Content.ReadFromJsonAsync<Session>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.token);

        var create = await http.PostAsJsonAsync("/api/v1/characters", new { characterName = "Kiếm Khách", heroClass = 0, slotIndex = 0 });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        CharacterDto ch = (await create.Content.ReadFromJsonAsync<CharacterDto>())!;
        Assert.Equal("Kiếm Khách", ch.characterName);

        var bad = await http.PostAsJsonAsync("/api/v1/characters", new { characterName = "Adm1n", heroClass = 0, slotIndex = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("name_reserved", (await bad.Content.ReadFromJsonAsync<Err>())!.code);

        var enter = await http.PostAsync($"/api/v1/characters/{ch.characterId}/enter", null);
        Assert.Equal(HttpStatusCode.OK, enter.StatusCode);

        // Restart: a brand-new server process over the same database file still has the character.
        using var factory2 = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("AdventureKing:DatabasePath", t.Path_));
        using var http2 = factory2.CreateClient();
        var login = await http2.PostAsJsonAsync("/api/v1/auth/login", new { account = "httpuser1", password = "http-pass-001" });
        Session s2 = (await login.Content.ReadFromJsonAsync<Session>())!;
        http2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s2.token);
        ProfileDto profile = (await http2.GetFromJsonAsync<ProfileDto>("/api/v1/profile"))!;
        Assert.Equal("Kiếm Khách", Assert.Single(profile.characters).characterName);
    }

    [Fact]
    public async Task Unauthenticated_Social_And_Payment_Paths_Fail_Closed()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/profile")).StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/profile")).StatusCode);
        http.DefaultRequestHeaders.Authorization = null;

        var google = await http.PostAsync("/api/v1/auth/google", null);
        Assert.Equal(HttpStatusCode.NotImplemented, google.StatusCode);
        Assert.DoesNotContain("token", await google.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotImplemented, (await http.PostAsync("/api/v1/auth/facebook", null)).StatusCode);

        var reg = await http.PostAsJsonAsync("/api/v1/auth/register", new { account = "httpbuyer", password = "http-pass-002" });
        Session s = (await reg.Content.ReadFromJsonAsync<Session>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.token);
        var buy = await http.PostAsync("/api/v1/store/purchase/vault", null);
        Assert.Equal(HttpStatusCode.Forbidden, buy.StatusCode);
        Assert.Equal("payments_disabled", (await buy.Content.ReadFromJsonAsync<Err>())!.code);
        Assert.Empty((await http.GetFromJsonAsync<ProfileDto>("/api/v1/profile"))!.characters);
    }

    [Fact]
    public async Task Login_Brute_Force_Is_Rate_Limited()
    {
        await http.PostAsJsonAsync("/api/v1/auth/register", new { account = "target01", password = "target-pass-01" });
        HttpStatusCode last = HttpStatusCode.OK;
        for (int i = 0; i < 12; i++)
            last = (await http.PostAsJsonAsync("/api/v1/auth/login", new { account = "target01", password = "guess-" + i + "-xyz" })).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Malformed_Json_Is_A_Clean_400()
    {
        var r = await http.PostAsync("/api/v1/auth/login", new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
