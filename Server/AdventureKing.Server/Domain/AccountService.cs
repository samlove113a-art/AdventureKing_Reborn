using System.Globalization;
using System.Text;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class GameError : Exception
{
    public string Code { get; }
    public int Status { get; }
    public GameError(string code, string message, int status = 400) : base(message)
    {
        Code = code;
        Status = status;
    }
}

public sealed record SessionInfo(string Token, string AccountId, long ExpiresMs);

/// <summary>Username/password accounts and bearer sessions. Social sign-in is NOT implemented here (fails closed).</summary>
public sealed class AccountService
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
    private readonly Database db;
    private readonly Func<long> nowMs;

    public AccountService(Database db, Func<long>? nowMs = null)
    {
        this.db = db;
        this.nowMs = nowMs ?? Database.NowMs;
    }

    /// <summary>Login names: 3–24 chars, ASCII letters/digits/._- only (keeps logins unambiguous; display names live on characters).</summary>
    public static string NormalizeLogin(string? login)
    {
        string l = (login ?? "").Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        if (l.Length < 3 || l.Length > 24 || !l.All(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch is '.' or '_' or '-'))
            throw new GameError("login_invalid", "Tên đăng nhập 3–24 ký tự, chỉ gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.");
        return l;
    }

    public static void ValidatePassword(string? password, string loginKey)
    {
        if (password == null || password.Length < 8 || password.Length > 128)
            throw new GameError("password_weak", "Mật khẩu cần 8–128 ký tự.");
        if (password.ToLowerInvariant().Contains(loginKey))
            throw new GameError("password_weak", "Mật khẩu không được chứa tên đăng nhập.");
        if (password.Distinct().Count() < 4)
            throw new GameError("password_weak", "Mật khẩu quá đơn giản.");
    }

    public SessionInfo Register(string? login, string? password)
    {
        string key = NormalizeLogin(login);
        ValidatePassword(password, key);
        string hash = Crypto.HashPassword(password!); // hash outside the write transaction (CPU-heavy)
        string accountId = Crypto.NewId("acc");
        return db.InTransaction((c, tx) =>
        {
            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM accounts WHERE login_key=$k", ("$k", key)) > 0)
                throw new GameError("login_taken", "Tên đăng nhập đã tồn tại.", 409);
            Database.Exec(c, tx,
                "INSERT INTO accounts(id, login_key, login_display, password_hash, created_ms) VALUES($id,$k,$d,$h,$t)",
                ("$id", accountId), ("$k", key), ("$d", login!.Trim()), ("$h", hash), ("$t", nowMs()));
            Audit(c, tx, accountId, "register", key);
            return NewSession(c, tx, accountId);
        });
    }

    public SessionInfo Login(string? login, string? password)
    {
        string key;
        try { key = NormalizeLogin(login); }
        catch (GameError) { throw Invalid(); }
        using var c = db.Open();
        string? id = null, hash = null;
        bool disabled = false;
        using (var cmd = Database.Command(c, null, "SELECT id, password_hash, disabled FROM accounts WHERE login_key=$k", ("$k", key)))
        using (var r = cmd.ExecuteReader())
            if (r.Read())
            {
                id = r.GetString(0);
                hash = r.GetString(1);
                disabled = r.GetInt64(2) != 0;
            }
        // Always run one PBKDF2 so unknown logins and wrong passwords take the same time.
        bool ok = Crypto.VerifyPassword(password ?? "", hash ?? DummyHash);
        if (id == null || !ok) throw Invalid();
        if (disabled) throw new GameError("account_disabled", "Tài khoản đã bị khóa.", 403);
        return db.InTransaction((c2, tx) =>
        {
            Audit(c2, tx, id, "login", "");
            return NewSession(c2, tx, id);
        });
    }

    private static readonly string DummyHash = Crypto.HashPassword("dummy-password-for-timing");
    private static GameError Invalid() => new("credentials_invalid", "Sai tên đăng nhập hoặc mật khẩu.", 401);

    /// <summary>Resolves a bearer token to its account, or throws 401.</summary>
    public string Authenticate(string? bearer)
    {
        if (string.IsNullOrWhiteSpace(bearer)) throw new GameError("unauthenticated", "Phiên đăng nhập không hợp lệ.", 401);
        using var c = db.Open();
        string? account = null;
        using (var cmd = Database.Command(c, null,
                   "SELECT account_id FROM sessions WHERE token_hash=$h AND revoked=0 AND expires_ms>$now",
                   ("$h", Crypto.Sha256(bearer)), ("$now", nowMs())))
            account = cmd.ExecuteScalar() as string;
        return account ?? throw new GameError("unauthenticated", "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.", 401);
    }

    public void Logout(string bearer) =>
        db.InTransaction((c, tx) => Database.Exec(c, tx, "UPDATE sessions SET revoked=1 WHERE token_hash=$h", ("$h", Crypto.Sha256(bearer))));

    private SessionInfo NewSession(SqliteConnection c, SqliteTransaction tx, string accountId)
    {
        string token = Crypto.NewToken();
        long now = nowMs(), expires = now + (long)SessionLifetime.TotalMilliseconds;
        Database.Exec(c, tx, "INSERT INTO sessions(token_hash, account_id, created_ms, expires_ms) VALUES($h,$a,$c,$e)",
            ("$h", Crypto.Sha256(token)), ("$a", accountId), ("$c", now), ("$e", expires));
        return new SessionInfo(token, accountId, expires);
    }

    internal static void Audit(SqliteConnection c, SqliteTransaction tx, string? accountId, string action, string detail) =>
        Database.Exec(c, tx, "INSERT INTO audit_log(account_id, action, detail, created_ms) VALUES($a,$x,$d,$t)",
            ("$a", accountId), ("$x", action), ("$d", detail), ("$t", Database.NowMs()));

    internal static string Invariant(long v) => v.ToString(CultureInfo.InvariantCulture);
}
