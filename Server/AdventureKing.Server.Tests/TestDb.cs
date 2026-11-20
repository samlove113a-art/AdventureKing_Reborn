using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Tests;

/// <summary>A real on-disk SQLite database per test (same engine and schema as the server), deleted afterwards.</summary>
public sealed class TestDb : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "ak_test_" + Guid.NewGuid().ToString("N"));
    public string Path_ => Path.Combine(Dir, "game.db");
    public Database Db { get; }

    public TestDb()
    {
        Directory.CreateDirectory(Dir);
        Db = new Database(Path_);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Dir, true); } catch (IOException) { }
    }
}
