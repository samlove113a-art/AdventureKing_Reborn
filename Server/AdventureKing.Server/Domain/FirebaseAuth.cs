using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;

namespace AdventureKing.Server.Domain;

public sealed class FirebaseIdTokenDto { public string? idToken { get; set; } }

public sealed record FirebaseIdentity(string Uid, string Provider, string? Email);

/// <summary>
/// Verifies a Firebase Auth ID token (RS256 JWT) WITHOUT any extra package: signature against Google's published certificates, issuer, audience (= Firebase project id),
/// expiry and subject. Keys come from <paramref name="keys"/> (kid -> RSA), which production fills from Google's x509 metadata endpoint.
/// </summary>
public sealed class FirebaseTokenVerifier
{
    public const string CertUrl = "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";
    private readonly string projectId;
    private readonly Func<string, RSA?> keys;
    private readonly Func<long> nowSeconds;

    public FirebaseTokenVerifier(string projectId, Func<string, RSA?> keys, Func<long>? nowSeconds = null)
    { this.projectId = projectId; this.keys = keys; this.nowSeconds = nowSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds()); }

    public bool Configured => projectId.Length > 0;

    private static byte[] B64(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight((s.Length + 3) / 4 * 4, '='));

    public FirebaseIdentity Verify(string? token)
    {
        if (!Configured) throw new GameError("social_not_configured", "Đăng nhập mạng xã hội chưa được cấu hình trên máy chủ.", 501);
        GameError Bad(string why) => new("firebase_token_invalid", "Phiên đăng nhập Firebase không hợp lệ (" + why + ").", 401);
        string[] parts = (token ?? "").Split('.');
        if (parts.Length != 3) throw Bad("format");
        JsonElement header, payload;
        try { header = JsonDocument.Parse(B64(parts[0])).RootElement; payload = JsonDocument.Parse(B64(parts[1])).RootElement; }
        catch (Exception) { throw Bad("json"); }
        if (header.GetProperty("alg").GetString() != "RS256") throw Bad("alg");
        string kid = header.TryGetProperty("kid", out var k) ? k.GetString() ?? "" : "";
        RSA? rsa = keys(kid);
        if (rsa == null) throw Bad("key");
        if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), B64(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw Bad("signature");
        long now = nowSeconds();
        string? Str(string n) => payload.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        long Num(string n) => payload.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
        if (Str("iss") != "https://securetoken.google.com/" + projectId) throw Bad("issuer");
        if (Str("aud") != projectId) throw Bad("audience");
        if (Num("exp") <= now) throw Bad("expired");
        if (Num("iat") > now + 300) throw Bad("issued in the future");
        string? sub = Str("sub");
        if (string.IsNullOrEmpty(sub) || sub.Length > 128) throw Bad("subject");
        string provider = payload.TryGetProperty("firebase", out var fb) && fb.TryGetProperty("sign_in_provider", out var sp) ? sp.GetString() ?? "" : "";
        return new FirebaseIdentity(sub, provider, Str("email"));
    }

    /// <summary>Production key source: Google's current certificates, cached for an hour.</summary>
    public static Func<string, RSA?> GoogleKeys(HttpClient http)
    {
        var cache = new Dictionary<string, RSA>(); long fetched = 0; var gate = new object();
        return kid =>
        {
            lock (gate)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (!cache.ContainsKey(kid) || now - fetched > 3600)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(http.GetStringAsync(CertUrl).GetAwaiter().GetResult());
                        cache.Clear();
                        foreach (var p in doc.RootElement.EnumerateObject())
                            cache[p.Name] = X509Certificate2.CreateFromPem(p.Value.GetString()!).GetRSAPublicKey()!;
                        fetched = now;
                    }
                    catch (Exception) { /* offline: keep what we have */ }
                }
                return cache.TryGetValue(kid, out var rsa) ? rsa : null;
            }
        };
    }
}

/// <summary>Firebase uid -> game account. A new uid creates an account (random unusable password); an anonymous (guest) uid that later links Google / Facebook keeps the same uid, so all progress stays.</summary>
public sealed class FirebaseAccounts
{
    private readonly Database db;
    private readonly AccountService accounts;
    public FirebaseAccounts(Database db, AccountService accounts) { this.db = db; this.accounts = accounts; }

    public SessionInfo SignIn(FirebaseIdentity id)
    {
        string? account = null;
        using (var c = db.Open())
            account = Database.Scalar<string>(c, null, "SELECT account_id FROM firebase_links WHERE firebase_uid=$u", ("$u", id.Uid));
        if (account == null)
        {
            string login = "fb" + Crypto.Sha256(id.Uid)[..14];
            string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1";
            SessionInfo created = accounts.Register(login, password);
            account = created.AccountId;
            db.InTransaction((c, tx) => Database.Exec(c, tx, "INSERT INTO firebase_links(firebase_uid, account_id, provider, email, created_ms) VALUES($u,$a,$p,$e,$t)",
                ("$u", id.Uid), ("$a", account), ("$p", id.Provider), ("$e", id.Email), ("$t", Database.NowMs())));
        }
        else
            db.InTransaction((c, tx) => Database.Exec(c, tx, "UPDATE firebase_links SET provider=$p, email=COALESCE($e,email) WHERE firebase_uid=$u", ("$p", id.Provider), ("$e", id.Email), ("$u", id.Uid)));
        return accounts.IssueSession(account);
    }
}
