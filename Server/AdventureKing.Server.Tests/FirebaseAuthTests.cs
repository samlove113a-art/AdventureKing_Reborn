using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

public sealed class FirebaseAuthTests : IDisposable
{
    private const string Project = "adventureking-test";
    private readonly TestDb t = new();
    private readonly RSA key = RSA.Create(2048);
    private readonly RSA otherKey = RSA.Create(2048);
    private long now = 1_800_000_000;

    public void Dispose() => t.Dispose();

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private string Token(string uid, string provider, RSA? signer = null, string? aud = null, long? exp = null, string kid = "k1")
    {
        string h = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "RS256", kid, typ = "JWT" })));
        string p = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = "https://securetoken.google.com/" + Project, ["aud"] = aud ?? Project, ["sub"] = uid, ["iat"] = now - 10, ["exp"] = exp ?? now + 3600,
            ["firebase"] = new { sign_in_provider = provider }, ["email"] = "a@b.c",
        })));
        string sig = B64((signer ?? key).SignData(Encoding.ASCII.GetBytes(h + "." + p), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        return h + "." + p + "." + sig;
    }

    private FirebaseTokenVerifier Verifier(string project = Project) => new(project, kid => kid == "k1" ? key : null, () => now);

    [Fact]
    public void A_Valid_Token_Is_Accepted_And_Every_Forgery_Is_Refused()
    {
        var v = Verifier();
        FirebaseIdentity id = v.Verify(Token("uid-1", "google.com"));
        Assert.Equal("uid-1", id.Uid); Assert.Equal("google.com", id.Provider);
        Assert.Equal("firebase_token_invalid", Assert.Throws<GameError>(() => v.Verify(Token("uid-1", "google.com", signer: otherKey))).Code);   // wrong signature
        Assert.Equal("firebase_token_invalid", Assert.Throws<GameError>(() => v.Verify(Token("uid-1", "google.com", aud: "someone-else"))).Code);
        Assert.Equal("firebase_token_invalid", Assert.Throws<GameError>(() => v.Verify(Token("uid-1", "google.com", exp: now - 1))).Code);       // expired
        Assert.Equal("firebase_token_invalid", Assert.Throws<GameError>(() => v.Verify(Token("uid-1", "google.com", kid: "unknown"))).Code);
        Assert.Equal("firebase_token_invalid", Assert.Throws<GameError>(() => v.Verify("a.b.c")).Code);
        Assert.Equal("firebase_token_invalid", Assert.Throws<GameError>(() => v.Verify(null)).Code);
        Assert.Equal("social_not_configured", Assert.Throws<GameError>(() => Verifier("").Verify(Token("u", "google.com"))).Code);                // fails closed without a project
    }

    [Fact]
    public void Sign_In_Creates_One_Account_Per_Uid_And_A_Guest_Upgrade_Keeps_The_Same_Account()
    {
        var accounts = new AccountService(t.Db);
        var fb = new FirebaseAccounts(t.Db, accounts);
        var v = Verifier();
        SessionInfo guest = fb.SignIn(v.Verify(Token("uid-guest", "anonymous")));
        Assert.Equal(guest.AccountId, accounts.Authenticate(guest.Token));
        using (var c = t.Db.Open()) Database.Exec(c, null, "UPDATE accounts SET diamonds=77 WHERE id=$a", ("$a", guest.AccountId));
        // Firebase linkWithCredential keeps the uid: signing in again after linking Google lands on the same account with its data
        SessionInfo upgraded = fb.SignIn(v.Verify(Token("uid-guest", "google.com")));
        Assert.Equal(guest.AccountId, upgraded.AccountId);
        using (var c = t.Db.Open()) Assert.Equal(77, Database.Scalar<long>(c, null, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", upgraded.AccountId)));
        Assert.Equal("google.com", Provider(guest.AccountId));
        SessionInfo other = fb.SignIn(v.Verify(Token("uid-other", "facebook.com")));
        Assert.NotEqual(guest.AccountId, other.AccountId);
    }

    private string Provider(string account) { using var c = t.Db.Open(); return Database.Scalar<string>(c, null, "SELECT provider FROM firebase_links WHERE account_id=$a", ("$a", account))!; }
}
