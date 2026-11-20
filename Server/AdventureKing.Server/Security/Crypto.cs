using System.Security.Cryptography;
using System.Text;

namespace AdventureKing.Server.Security;

/// <summary>PBKDF2-SHA256 password hashing (OWASP 2023 iteration guidance) and opaque bearer tokens.</summary>
public static class Crypto
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        string[] parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out int iter) || iter < 10_000)
            return false;
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>256-bit random token, URL-safe. Only its SHA-256 is stored server-side.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");
}

/// <summary>
/// Fixed-window in-memory limiter for auth endpoints (per key, e.g. IP + account).
/// Sufficient for the single-process PC server; a VPS deployment would move this to shared storage.
/// </summary>
public sealed class AttemptLimiter
{
    private readonly int maxAttempts;
    private readonly TimeSpan window;
    private readonly Dictionary<string, (DateTime start, int count)> buckets = new();
    private readonly object gate = new();
    private readonly Func<DateTime> clock;

    public AttemptLimiter(int maxAttempts, TimeSpan window, Func<DateTime>? clock = null)
    {
        this.maxAttempts = maxAttempts;
        this.window = window;
        this.clock = clock ?? (() => DateTime.UtcNow);
    }

    public bool TryAcquire(string key)
    {
        lock (gate)
        {
            DateTime now = clock();
            if (!buckets.TryGetValue(key, out var b) || now - b.start >= window)
                b = (now, 0);
            if (b.count >= maxAttempts)
            {
                buckets[key] = b;
                return false;
            }
            buckets[key] = (b.start, b.count + 1);
            if (buckets.Count > 50_000) Prune(now);
            return true;
        }
    }

    private void Prune(DateTime now)
    {
        foreach (string k in buckets.Where(kv => now - kv.Value.start >= window).Select(kv => kv.Key).ToList())
            buckets.Remove(k);
    }
}
