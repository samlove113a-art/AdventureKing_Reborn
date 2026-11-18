using AdventureKing.Server.Data;

namespace AdventureKing.Server.Domain;

/// <summary>
/// Chapter progress: the chapter boss kill the SERVER confirmed (FarmService.ChapterBossKilled, after the hit that took the boss
/// to 0 HP). One row per character and chapter, idempotent (a second kill never rewrites it). A client can never set it; the next
/// chapter is "unlocked" for a character only when Cleared(chapter) is true.
/// </summary>
public sealed class ChapterService
{
    private readonly Database db;
    private readonly Func<long> now;
    public ChapterService(Database db, Func<long>? clock = null) { this.db = db; now = clock ?? Database.NowMs; }

    public void RecordBossKill(string accountId, string characterId, int chapter, string bossType)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        int n = Database.Exec(c, tx, "INSERT OR IGNORE INTO chapter_progress(character_id, chapter, boss_type, killed_at) VALUES($c,$ch,$b,$t)",
            ("$c", characterId), ("$ch", chapter), ("$b", bossType), ("$t", now()));
        if (n > 0) AccountService.Audit(c, tx, accountId, "chapter_cleared", $"{characterId} chapter {chapter} boss {bossType}");
        tx.Commit();
    }

    public bool Cleared(string characterId, int chapter)
    {
        using var c = db.Open();
        return Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM chapter_progress WHERE character_id=$c AND chapter=$ch", ("$c", characterId), ("$ch", chapter)) > 0;
    }

    /// <summary>Cleared chapters of one of THIS account's characters (404 for anyone else's).</summary>
    public int[] ClearedFor(string accountId, string characterId)
    {
        using (var c = db.Open())
            if (Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId)) == 0)
                throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
        return ClearedChapters(characterId);
    }

    public int[] ClearedChapters(string characterId)
    {
        using var c = db.Open();
        using var cmd = Database.Command(c, null, "SELECT chapter FROM chapter_progress WHERE character_id=$c ORDER BY chapter", ("$c", characterId));
        using var r = cmd.ExecuteReader();
        var list = new List<int>();
        while (r.Read()) list.Add(r.GetInt32(0));
        return list.ToArray();
    }
}
