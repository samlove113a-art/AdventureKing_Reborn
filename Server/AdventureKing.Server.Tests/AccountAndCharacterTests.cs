using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;
using AdventureKing.Server.Security;

namespace AdventureKing.Server.Tests;

public class AccountAndCharacterTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;

    public AccountAndCharacterTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules());
    }

    public void Dispose() => t.Dispose();

    [Fact]
    public void Register_Login_And_Session_Round_Trip()
    {
        SessionInfo reg = accounts.Register("Hero.One", "dragon-sword-77");
        Assert.Equal(reg.AccountId, accounts.Authenticate(reg.Token));
        SessionInfo login = accounts.Login("hero.one", "dragon-sword-77");
        Assert.Equal(reg.AccountId, login.AccountId);
        Assert.NotEqual(reg.Token, login.Token);

        accounts.Logout(login.Token);
        Assert.Equal(401, Assert.Throws<GameError>(() => accounts.Authenticate(login.Token)).Status);
        Assert.Equal(reg.AccountId, accounts.Authenticate(reg.Token)); // other sessions unaffected
    }

    [Fact]
    public void Passwords_And_Tokens_Are_Never_Stored_In_Plaintext()
    {
        SessionInfo s = accounts.Register("secretkeeper", "very-secret-pass-1");
        using var c = t.Db.Open();
        string hash = Database.Scalar<string>(c, null, "SELECT password_hash FROM accounts");
        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.DoesNotContain("very-secret-pass-1", hash);
        Assert.Equal(0, Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM sessions WHERE token_hash=$t", ("$t", s.Token)));
        Assert.Equal(1, Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM sessions WHERE token_hash=$t", ("$t", Crypto.Sha256(s.Token))));
        foreach (string f in new[] { t.Path_, t.Path_ + "-wal" }.Where(File.Exists))
        {
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            string raw = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            Assert.DoesNotContain("very-secret-pass-1", raw);
            Assert.DoesNotContain(s.Token, raw);
        }
    }

    [Fact]
    public void Wrong_Password_Unknown_User_And_Duplicates_Fail()
    {
        accounts.Register("player01", "correct-horse-9");
        Assert.Equal("credentials_invalid", Assert.Throws<GameError>(() => accounts.Login("player01", "wrong-horse-9")).Code);
        Assert.Equal("credentials_invalid", Assert.Throws<GameError>(() => accounts.Login("nobody01", "correct-horse-9")).Code);
        Assert.Equal("login_taken", Assert.Throws<GameError>(() => accounts.Register("PLAYER01", "another-pass-9")).Code);
        Assert.Equal("password_weak", Assert.Throws<GameError>(() => accounts.Register("player02", "short")).Code);
        Assert.Equal("password_weak", Assert.Throws<GameError>(() => accounts.Register("player03", "player03-pass")).Code);
        Assert.Equal("login_invalid", Assert.Throws<GameError>(() => accounts.Register("người chơi", "correct-horse-9")).Code);
        Assert.Equal("login_invalid", Assert.Throws<GameError>(() => accounts.Register("a@b@c.vn", "correct-horse-9")).Code);
        Assert.Equal("login_invalid", Assert.Throws<GameError>(() => accounts.Register("player@nodot", "correct-horse-9")).Code);
        string mail = accounts.Register("Hero.Mail+ak@Example.com", "mail-pass-777").AccountId;
        Assert.Equal(mail, accounts.Login("hero.mail+ak@example.com", "mail-pass-777").AccountId);
    }

    [Fact]
    public void Expired_Session_Is_Rejected()
    {
        long now = Database.NowMs();
        var clocked = new AccountService(t.Db, () => now);
        SessionInfo s = clocked.Register("timetraveler", "tick-tock-clock-1");
        now += (long)AccountService.SessionLifetime.TotalMilliseconds + 1;
        Assert.Equal(401, Assert.Throws<GameError>(() => clocked.Authenticate(s.Token)).Status);
    }

    [Fact]
    public void Four_Slots_Unique_Names_And_Ownership()
    {
        string a = accounts.Register("owner001", "owner-pass-001").AccountId;
        string b = accounts.Register("owner002", "owner-pass-002").AccountId;

        CharacterDto c0 = chars.Create(a, "Hùng Dũng", 0, 0);
        Assert.Equal("Hùng Dũng", c0.characterName);
        Assert.Equal(1, c0.level);
        Assert.Equal(0, c0.gold);
        chars.Create(a, "Mai Linh", 1, 1);
        chars.Create(a, "Bóng Đêm", 2, 2);
        chars.Create(a, "Mũi Tên", 3, 3);
        Assert.Equal(4, chars.LoadProfile(a).characters.Length);
        Assert.Equal("slot_invalid", Assert.Throws<GameError>(() => chars.Create(a, "Thứ Năm", 0, 4)).Code);
        Assert.Equal("slot_taken", Assert.Throws<GameError>(() => chars.Create(a, "Thứ Năm", 0, 0)).Code);

        Assert.Equal("name_taken", Assert.Throws<GameError>(() => chars.Create(b, "hùng dũng", 0, 0)).Code);
        Assert.Equal("name_taken", Assert.Throws<GameError>(() => chars.Create(b, "HùngDũng", 0, 0)).Code);
        Assert.Equal("name_reserved", Assert.Throws<GameError>(() => chars.Create(b, "GM Tèo", 0, 0)).Code);
        Assert.Equal("class_invalid", Assert.Throws<GameError>(() => chars.Create(b, "Tèo Em", 7, 0)).Code);

        // B cannot enter or progress A's character.
        Assert.Equal(404, Assert.Throws<GameError>(() => chars.Enter(b, c0.characterId)).Status);
        Assert.Equal(404, Assert.Throws<GameError>(() => chars.CompleteTutorialStep(b, c0.characterId, 0)).Status);
        Assert.Equal(c0.characterId, chars.Enter(a, c0.characterId).characterId);
    }

    [Fact]
    public void Location_Is_Validated_Owned_And_Survives_Restart()
    {
        string a = accounts.Register("walker01", "walker-pass-1").AccountId;
        string b = accounts.Register("walker02", "walker-pass-2").AccountId;
        string id = chars.Create(a, "Lữ Khách", 3, 0).characterId;
        Assert.False(chars.Enter(a, id).hasPosition);

        CharacterDto saved = chars.SaveLocation(a, id, "village_wind_bell", 12.5f, 4.25f);
        Assert.True(saved.hasPosition);
        Assert.Equal("map_unknown", Assert.Throws<GameError>(() => chars.SaveLocation(a, id, "boss_room", 0, 0)).Code);
        Assert.Equal("position_invalid", Assert.Throws<GameError>(() => chars.SaveLocation(a, id, "village_wind_bell", 9999f, 0)).Code);
        Assert.Equal("position_invalid", Assert.Throws<GameError>(() => chars.SaveLocation(a, id, "village_wind_bell", float.NaN, 0)).Code);
        Assert.Equal(404, Assert.Throws<GameError>(() => chars.SaveLocation(b, id, "village_wind_bell", 1, 1)).Status);

        CharacterDto after = new CharacterService(new Database(t.Path_), new NameRules()).Enter(a, id);
        Assert.Equal("village_wind_bell", after.lastMapId);
        Assert.Equal(12.5f, after.posX);
        Assert.Equal(4.25f, after.posY);
        Assert.Equal(0, after.gold); // a position save never changes economy fields
    }

    [Fact]
    public void Maps_Change_Only_Through_A_Nearby_Portal_On_The_Current_Map()
    {
        string a = accounts.Register("traveler1", "travel-pass-1").AccountId;
        string id = chars.Create(a, "Phượt Thủ", 0, 0).characterId;
        Assert.Equal(CharacterService.StartMap, chars.Enter(a, id).lastMapId);

        // Saving a position on another map is not a way to teleport.
        Assert.Equal("map_mismatch", Assert.Throws<GameError>(() => chars.SaveLocation(a, id, "meadow_breeze", 0, 1)).Code);
        // Portal must exist, be on the current map and be within reach.
        Assert.Equal(404, Assert.Throws<GameError>(() => chars.Travel(a, id, "nowhere", 66, 0)).Status);
        Assert.Equal("portal_too_far", Assert.Throws<GameError>(() => chars.Travel(a, id, "hub_east_meadow", 20, 0)).Code);
        Assert.Equal("portal_wrong_map", Assert.Throws<GameError>(() => chars.Travel(a, id, "meadow_west_hub", -40, 0)).Code);

        long clock = 1_000_000;
        chars.Clock = () => clock;
        CharacterDto there = chars.Travel(a, id, "hub_east_meadow", 65.2f, 0.9f);
        // An immediate second jump (double-click, lag retry, spam) is refused even from a valid spot.
        clock += 300;
        Assert.Equal("portal_cooldown", Assert.Throws<GameError>(() => chars.Travel(a, id, "meadow_west_hub", -39f, 0.9f)).Code);
        Assert.Equal("meadow_breeze", chars.LoadProfile(a).characters.Single(c => c.characterId == id).lastMapId); // (Enter = login: back to the hub)
        clock += 2000;
        CharacterService.Portal p = CharacterService.Portals["hub_east_meadow"];
        Assert.Equal("meadow_breeze", there.lastMapId);
        Assert.Equal(p.ArrivalX, there.posX);
        Assert.True(there.hasPosition);
        chars.SaveLocation(a, id, "meadow_breeze", -10f, 0.9f);

        CharacterDto back = chars.Travel(a, id, "meadow_west_hub", -39f, 0.9f);
        Assert.Equal("village_wind_bell", back.lastMapId);
        Assert.Equal(CharacterService.Portals["meadow_west_hub"].ArrivalX, back.posX);
        // Every portal lands inside its destination map.
        foreach (CharacterService.Portal portal in CharacterService.Portals.Values)
        {
            CharacterService.WorldMap to = CharacterService.Maps[portal.ToMap], from = CharacterService.Maps[portal.FromMap];
            Assert.InRange(portal.ArrivalX, to.MinX, to.MaxX);
            Assert.InRange(portal.X, from.MinX, from.MaxX);
        }
    }

    [Fact]
    public void Tutorial_Progress_Is_Sequential_Persistent_And_Never_Resets()
    {
        string a = accounts.Register("learner01", "learner-pass-1").AccountId;
        string id = chars.Create(a, "Tân Thủ", 0, 0).characterId;
        Assert.Equal("tutorial_order", Assert.Throws<GameError>(() => chars.CompleteTutorialStep(a, id, 3)).Code);
        for (int s = 0; s < CharacterService.TutorialSteps.Length; s++)
            chars.CompleteTutorialStep(a, id, s);
        CharacterDto done = chars.Enter(a, id);
        Assert.True(done.tutorialCompleted);
        // Replaying an old step (e.g. after reconnect) does not restart the tutorial.
        Assert.True(chars.CompleteTutorialStep(a, id, 0).tutorialCompleted);
        // A fresh service over the same DB file (server restart) sees the same state.
        var restarted = new CharacterService(new Database(t.Path_), new NameRules());
        Assert.True(restarted.Enter(a, id).tutorialCompleted);
    }
}
