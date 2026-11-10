using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Data;

/// <summary>
/// SQLite store for the local test server. Every write that touches currency, items or
/// rewards goes through <see cref="InTransaction{T}"/> so it is atomic. The schema is
/// versioned (PRAGMA user_version) so the same database can later be migrated to a VPS.
/// </summary>
public sealed class Database
{
    public string ConnectionString { get; }
    public string FilePath { get; }

    public Database(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString();
        Migrate();
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(ConnectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return c;
    }

    /// <summary>Runs <paramref name="work"/> inside one IMMEDIATE transaction (single writer, no lost updates).</summary>
    public T InTransaction<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        using var c = Open();
        using var tx = c.BeginTransaction(deferred: false);
        try
        {
            T result = work(c, tx);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void Migrate()
    {
        using var c = Open();
        using (var wal = c.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            wal.ExecuteNonQuery();
        }
        long version = Scalar<long>(c, null, "PRAGMA user_version;");
        if (version < 1)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V1);
            Exec(c, tx, "PRAGMA user_version=1;");
            tx.Commit();
            version = 1;
        }
        if (version < 2)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V2);
            Exec(c, tx, "PRAGMA user_version=2;");
            tx.Commit();
            version = 2;
        }
        if (version < 3)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V3);
            Exec(c, tx, "PRAGMA user_version=3;");
            tx.Commit();
            version = 3;
        }
        if (version < 4)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V4);
            Exec(c, tx, "PRAGMA user_version=4;");
            tx.Commit();
            version = 4;
        }
        if (version < 5)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V5);
            Exec(c, tx, "PRAGMA user_version=5;");
            tx.Commit();
            version = 5;
        }
        if (version < 6)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V6);
            Exec(c, tx, "PRAGMA user_version=6;");
            tx.Commit();
            version = 6;
        }
        if (version < 7)
        {
            using var tx = c.BeginTransaction();
            Exec(c, tx, Schema.V7);
            Exec(c, tx, "PRAGMA user_version=7;");
            tx.Commit();
        }
    }

    public static int Exec(SqliteConnection c, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    {
        using var cmd = Command(c, tx, sql, args);
        return cmd.ExecuteNonQuery();
    }

    public static T Scalar<T>(SqliteConnection c, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    {
        using var cmd = Command(c, tx, sql, args);
        object? v = cmd.ExecuteScalar();
        if (v == null || v is DBNull) return default!;
        return (T)Convert.ChangeType(v, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public static SqliteCommand Command(SqliteConnection c, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    {
        var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

internal static class Schema
{
    public const int Current = 7;

    // V7: pets owned by a character (one row per pet: buying twice is impossible), at most one summoned, satiety in
    // milliseconds of summoned activity left.
    public const string V7 = """
    CREATE TABLE pets_owned (
        character_id TEXT NOT NULL REFERENCES characters(id),
        pet_id       TEXT NOT NULL,
        summoned     INTEGER NOT NULL DEFAULT 0,
        satiety_ms   INTEGER NOT NULL CHECK (satiety_ms >= 0),
        bought_ms    INTEGER NOT NULL,
        PRIMARY KEY (character_id, pet_id)
    );
    """;

    // V6: quest progress. period = '' for main/side quests, the Vietnam calendar day for daily quests. claimed_ms set
    // once: the primary key + the claimed check make a second reward for the same (quest, period) impossible.
    public const string V6 = """
    CREATE TABLE quest_progress (
        character_id TEXT NOT NULL REFERENCES characters(id),
        quest_id     TEXT NOT NULL,
        period       TEXT NOT NULL DEFAULT '',
        counts_json  TEXT NOT NULL DEFAULT '{}',
        claimed_ms   INTEGER,
        PRIMARY KEY (character_id, quest_id, period)
    );
    """;

    // V5: character bag (stackable items, max 99 per slot) + equipped gear + one-time grants (starter kit).
    // (character_id, slot_index) is the bag cell; an item lives in exactly one row, so it cannot exist twice.
    public const string V5 = """
    CREATE TABLE bag_slots (
        character_id TEXT NOT NULL REFERENCES characters(id),
        slot_index   INTEGER NOT NULL CHECK (slot_index >= 0),
        item_id      TEXT NOT NULL,
        qty          INTEGER NOT NULL CHECK (qty BETWEEN 1 AND 99),
        PRIMARY KEY (character_id, slot_index)
    );
    CREATE TABLE equipped (
        character_id TEXT NOT NULL REFERENCES characters(id),
        equip_slot   TEXT NOT NULL,
        item_id      TEXT NOT NULL,
        PRIMARY KEY (character_id, equip_slot)
    );
    CREATE TABLE item_grants (
        character_id TEXT NOT NULL REFERENCES characters(id),
        grant_key    TEXT NOT NULL,
        granted_ms   INTEGER NOT NULL,
        PRIMARY KEY (character_id, grant_key)
    );
    """;

    // V4: 7-day check-in (account scope). One receipt per (account, cycle, day): the primary key makes a second claim
    // of the same day impossible even under concurrent requests; last_vn_day enforces one claim per VN calendar day.
    public const string V4 = """
    CREATE TABLE checkin_progress (
        account_id   TEXT PRIMARY KEY REFERENCES accounts(id),
        cycle        INTEGER NOT NULL DEFAULT 1,
        claimed_days INTEGER NOT NULL DEFAULT 0 CHECK (claimed_days BETWEEN 0 AND 7),
        last_vn_day  TEXT NOT NULL DEFAULT ''
    );
    CREATE TABLE checkin_claims (
        account_id   TEXT NOT NULL REFERENCES accounts(id),
        cycle        INTEGER NOT NULL,
        day          INTEGER NOT NULL CHECK (day BETWEEN 1 AND 7),
        character_id TEXT NOT NULL,
        vn_day       TEXT NOT NULL,
        catalog_id   TEXT NOT NULL,
        reward_json  TEXT NOT NULL,
        claimed_ms   INTEGER NOT NULL,
        PRIMARY KEY (account_id, cycle, day)
    );
    """;

    // V3: wardrobe / inventory ownership (cosmetics now; pets + pet food later: kind + quantity) and shop history.
    // character_id NULL = owned by the whole account (Diamond sets). The partial unique index makes a second
    // copy of a once-only item impossible even if application checks were bypassed.
    public const string V3 = """
    CREATE TABLE owned_items (
        id           TEXT PRIMARY KEY,
        account_id   TEXT NOT NULL REFERENCES accounts(id),
        character_id TEXT REFERENCES characters(id),
        item_id      TEXT NOT NULL,
        kind         TEXT NOT NULL CHECK (kind IN ('cosmetic','cosmetic_set','pet','pet_food')),
        quantity     INTEGER NOT NULL DEFAULT 1 CHECK (quantity > 0),
        source       TEXT NOT NULL,
        acquired_ms  INTEGER NOT NULL
    );
    CREATE UNIQUE INDEX ux_owned_once ON owned_items(account_id, IFNULL(character_id, ''), item_id)
        WHERE kind IN ('cosmetic','cosmetic_set');
    CREATE INDEX ix_owned_character ON owned_items(character_id);
    CREATE TABLE shop_purchases (
        id              TEXT PRIMARY KEY,
        account_id      TEXT NOT NULL REFERENCES accounts(id),
        character_id    TEXT NOT NULL REFERENCES characters(id),
        item_id         TEXT NOT NULL,
        currency        TEXT NOT NULL CHECK (currency IN ('gold','diamonds')),
        price           INTEGER NOT NULL CHECK (price > 0),
        request_id      TEXT NOT NULL,
        catalog_version INTEGER NOT NULL,
        created_ms      INTEGER NOT NULL,
        UNIQUE (account_id, request_id)
    );
    CREATE INDEX ix_purchases_character ON shop_purchases(character_id, created_ms);
    """;

    // V2: last saved world position inside the current map (server-validated against map bounds).
    public const string V2 = """
    ALTER TABLE characters ADD COLUMN pos_x REAL NOT NULL DEFAULT 0;
    ALTER TABLE characters ADD COLUMN pos_y REAL NOT NULL DEFAULT 0;
    ALTER TABLE characters ADD COLUMN has_position INTEGER NOT NULL DEFAULT 0;
    """;

    public const string V1 = """
    CREATE TABLE accounts (
        id              TEXT PRIMARY KEY,
        login_key       TEXT NOT NULL UNIQUE,      -- normalised login name
        login_display   TEXT NOT NULL,
        password_hash   TEXT NOT NULL,             -- PBKDF2-SHA256$iter$salt$hash, never plaintext
        created_ms      INTEGER NOT NULL,
        disabled        INTEGER NOT NULL DEFAULT 0,
        diamonds        INTEGER NOT NULL DEFAULT 0 CHECK (diamonds >= 0),   -- account-wide premium currency
        paid_total_vnd  INTEGER NOT NULL DEFAULT 0 CHECK (paid_total_vnd >= 0) -- verified paid top-ups only (VIP basis)
    );
    CREATE TABLE sessions (
        token_hash  TEXT PRIMARY KEY,              -- SHA-256 of the bearer token; raw token never stored
        account_id  TEXT NOT NULL REFERENCES accounts(id),
        created_ms  INTEGER NOT NULL,
        expires_ms  INTEGER NOT NULL,
        revoked     INTEGER NOT NULL DEFAULT 0
    );
    CREATE INDEX ix_sessions_account ON sessions(account_id);
    CREATE TABLE characters (
        id                TEXT PRIMARY KEY,
        account_id        TEXT NOT NULL REFERENCES accounts(id),
        slot_index        INTEGER NOT NULL CHECK (slot_index BETWEEN 0 AND 3),
        name              TEXT NOT NULL,           -- NFC display name
        name_key          TEXT NOT NULL UNIQUE,    -- confusable skeleton: blocks look-alike duplicates
        hero_class        INTEGER NOT NULL CHECK (hero_class BETWEEN 0 AND 3),
        level             INTEGER NOT NULL DEFAULT 1 CHECK (level >= 1),
        experience        INTEGER NOT NULL DEFAULT 0 CHECK (experience >= 0),
        skill_points      INTEGER NOT NULL DEFAULT 0 CHECK (skill_points >= 0),
        gold              INTEGER NOT NULL DEFAULT 0 CHECK (gold >= 0),
        map_id            TEXT NOT NULL DEFAULT 'village_wind_bell',
        checkpoint_id     TEXT NOT NULL DEFAULT 'village_spawn',
        appearance_json   TEXT NOT NULL DEFAULT '{}',
        tutorial_step     INTEGER NOT NULL DEFAULT 0 CHECK (tutorial_step >= 0),
        tutorial_done     INTEGER NOT NULL DEFAULT 0,
        revision          INTEGER NOT NULL DEFAULT 1,
        created_ms        INTEGER NOT NULL,
        saved_ms          INTEGER NOT NULL,
        UNIQUE (account_id, slot_index)
    );
    -- Append-only currency ledger. idempotency_key makes every grant/spend replay-safe.
    CREATE TABLE ledger (
        id               INTEGER PRIMARY KEY AUTOINCREMENT,
        account_id       TEXT NOT NULL REFERENCES accounts(id),
        character_id     TEXT,
        currency         TEXT NOT NULL CHECK (currency IN ('gold','diamonds')),
        delta            INTEGER NOT NULL,
        balance_after    INTEGER NOT NULL CHECK (balance_after >= 0),
        reason           TEXT NOT NULL,
        idempotency_key  TEXT NOT NULL UNIQUE,
        created_ms       INTEGER NOT NULL
    );
    CREATE INDEX ix_ledger_account ON ledger(account_id, id);
    -- Store orders: one row per verified provider transaction (unique => no double grant).
    CREATE TABLE store_orders (
        id                  TEXT PRIMARY KEY,
        account_id          TEXT NOT NULL REFERENCES accounts(id),
        package_id          TEXT NOT NULL,
        price_vnd           INTEGER NOT NULL,
        diamonds_granted    INTEGER NOT NULL,
        first_purchase_bonus INTEGER NOT NULL DEFAULT 0,
        provider            TEXT NOT NULL,
        provider_txn_id     TEXT NOT NULL,
        status              TEXT NOT NULL,
        created_ms          INTEGER NOT NULL,
        UNIQUE (provider, provider_txn_id)
    );
    CREATE INDEX ix_orders_account ON store_orders(account_id, package_id);
    CREATE TABLE month_cards (
        account_id      TEXT NOT NULL REFERENCES accounts(id),
        order_id        TEXT NOT NULL REFERENCES store_orders(id),
        start_day       TEXT NOT NULL,             -- yyyy-MM-dd, Vietnam time (UTC+7)
        days_total      INTEGER NOT NULL,
        daily_diamonds  INTEGER NOT NULL,
        PRIMARY KEY (account_id, order_id)
    );
    CREATE TABLE month_card_claims (
        account_id  TEXT NOT NULL,
        order_id    TEXT NOT NULL,
        claim_day   TEXT NOT NULL,
        diamonds    INTEGER NOT NULL,
        claimed_ms  INTEGER NOT NULL,
        PRIMARY KEY (account_id, order_id, claim_day)
    );
    CREATE TABLE items (
        id             TEXT PRIMARY KEY,
        character_id   TEXT NOT NULL REFERENCES characters(id),
        definition_id  TEXT NOT NULL,
        quantity       INTEGER NOT NULL CHECK (quantity > 0),
        location       TEXT NOT NULL DEFAULT 'bag' CHECK (location IN ('bag','storage','equipped','mail','auction')),
        upgrade_level  INTEGER NOT NULL DEFAULT 0,
        locked         INTEGER NOT NULL DEFAULT 0,
        source         TEXT NOT NULL,
        created_ms     INTEGER NOT NULL
    );
    CREATE INDEX ix_items_character ON items(character_id, location);
    CREATE TABLE audit_log (
        id          INTEGER PRIMARY KEY AUTOINCREMENT,
        account_id  TEXT,
        action      TEXT NOT NULL,
        detail      TEXT NOT NULL,
        created_ms  INTEGER NOT NULL
    );
    """;
}
