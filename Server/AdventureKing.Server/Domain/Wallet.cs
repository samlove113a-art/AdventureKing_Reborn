using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed record LedgerResult(bool Applied, long Balance);

/// <summary>
/// The only code path that changes Gold (per character) or Diamonds (per account). Must be called inside a
/// transaction. Every change carries an idempotency key: replaying the same key returns the original
/// result and changes nothing, so retries and duplicate callbacks can never double-grant or double-spend.
/// </summary>
public static class Wallet
{
    public static LedgerResult Diamonds(SqliteConnection c, SqliteTransaction tx, string accountId, long delta, string reason, string idempotencyKey) =>
        Apply(c, tx, accountId, null, "diamonds", delta, reason, idempotencyKey);

    public static LedgerResult Gold(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId, long delta, string reason, string idempotencyKey) =>
        Apply(c, tx, accountId, characterId, "gold", delta, reason, idempotencyKey);

    private static LedgerResult Apply(SqliteConnection c, SqliteTransaction tx, string accountId, string? characterId,
        string currency, long delta, string reason, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) throw new GameError("idempotency_key", "Thiếu mã giao dịch.");
        using (var cmd = Database.Command(c, tx, "SELECT account_id, currency, delta, balance_after FROM ledger WHERE idempotency_key=$k", ("$k", key)))
        using (var r = cmd.ExecuteReader())
            if (r.Read())
            {
                if (r.GetString(0) != accountId || r.GetString(1) != currency || r.GetInt64(2) != delta)
                    throw new GameError("idempotency_conflict", "Mã giao dịch đã được dùng cho thao tác khác.", 409);
                return new LedgerResult(false, r.GetInt64(3));
            }

        long balance = currency == "diamonds"
            ? Database.Scalar<long>(c, tx, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", accountId))
            : Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId));
        long after = checked(balance + delta);
        if (after < 0)
            throw new GameError("insufficient_" + currency, currency == "gold" ? "Không đủ Gold." : "Không đủ Kim Cương.", 409);

        if (currency == "diamonds")
            Database.Exec(c, tx, "UPDATE accounts SET diamonds=$v WHERE id=$a", ("$v", after), ("$a", accountId));
        else
            Database.Exec(c, tx, "UPDATE characters SET gold=$v, revision=revision+1, saved_ms=$t WHERE id=$c",
                ("$v", after), ("$t", Database.NowMs()), ("$c", characterId));

        Database.Exec(c, tx, """
            INSERT INTO ledger(account_id, character_id, currency, delta, balance_after, reason, idempotency_key, created_ms)
            VALUES($a,$c,$cur,$d,$b,$r,$k,$t)
            """, ("$a", accountId), ("$c", characterId), ("$cur", currency), ("$d", delta), ("$b", after),
            ("$r", reason), ("$k", key), ("$t", Database.NowMs()));
        return new LedgerResult(true, after);
    }
}
