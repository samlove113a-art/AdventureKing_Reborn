using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Data;

/// <summary>
/// Online, consistent SQLite backups (SQLite backup API — safe while the server is running) plus a verified restore.
/// Restore refuses files that fail PRAGMA integrity_check or are from a newer schema.
/// </summary>
public static class BackupService
{
    public static string Backup(Database db, string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"adventureking_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.db");
        using (var src = db.Open())
        using (var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            dst.Open();
            src.BackupDatabase(dst);
        }
        Verify(path);
        return path;
    }

    /// <summary>Checks integrity and schema version; returns the number of accounts as a sanity figure.</summary>
    public static long Verify(string backupPath)
    {
        if (!File.Exists(backupPath)) throw new FileNotFoundException("Backup not found", backupPath);
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        c.Open();
        string integrity = Database.Scalar<string>(c, null, "PRAGMA integrity_check;");
        if (integrity != "ok") throw new InvalidDataException("Backup integrity check failed: " + integrity);
        long version = Database.Scalar<long>(c, null, "PRAGMA user_version;");
        if (version < 1 || version > 1) throw new InvalidDataException("Unsupported schema version " + version);
        return Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM accounts;");
    }

    /// <summary>
    /// Restores <paramref name="backupPath"/> into the live database file. The server must be stopped
    /// (CLI: --restore). The current database is first saved next to it as *.pre-restore.
    /// </summary>
    public static void Restore(string backupPath, string liveDbPath)
    {
        Verify(backupPath);
        SqliteConnection.ClearAllPools();
        if (File.Exists(liveDbPath))
        {
            string keep = liveDbPath + $".pre-restore-{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            using (var live = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = liveDbPath, Pooling = false }.ToString()))
            using (var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = keep, Pooling = false }.ToString()))
            {
                live.Open();
                copy.Open();
                live.BackupDatabase(copy);
            }
        }
        using (var src = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        using (var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = liveDbPath, Pooling = false }.ToString()))
        {
            src.Open();
            dst.Open();
            src.BackupDatabase(dst);
        }
        Verify(liveDbPath);
    }
}
