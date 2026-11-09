using System.Text.Json;
using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class QuestObjective { public string @event { get; set; } = ""; public int need { get; set; } = 1; public string text { get; set; } = ""; }
public sealed class QuestItemReward { public string itemId { get; set; } = ""; public int qty { get; set; } = 1; }
public sealed class QuestDef
{
    public string id { get; set; } = "";
    public string type { get; set; } = "main"; // main | side | daily
    public string title { get; set; } = "";
    public string desc { get; set; } = "";
    public string map { get; set; } = "";
    public string npc { get; set; } = "";
    public QuestObjective[] objectives { get; set; } = Array.Empty<QuestObjective>();
    public long rewardExp { get; set; }
    public long rewardGold { get; set; }
    public QuestItemReward[] rewardItems { get; set; } = Array.Empty<QuestItemReward>();
}
public sealed class QuestConfig
{
    public int version { get; set; }
    public long expPerLevelStep { get; set; } = 100;
    public QuestDef[] quests { get; set; } = Array.Empty<QuestDef>();

    public static QuestConfig Load(string path, ItemConfig items)
    {
        var cfg = JsonSerializer.Deserialize<QuestConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty quest config");
        if (cfg.quests.Select(q => q.id).Distinct().Count() != cfg.quests.Length) throw new InvalidDataException("duplicate quest id");
        foreach (QuestDef q in cfg.quests)
        {
            if (q.type is not ("main" or "side" or "daily") || q.objectives.Length == 0 || q.objectives.Any(o => o.need < 1 || o.@event == ""))
                throw new InvalidDataException(q.id + ": bad quest");
            if (q.rewardExp < 0 || q.rewardGold < 0 || q.rewardItems.Any(i => items.Get(i.itemId) == null || i.qty < 1)) throw new InvalidDataException(q.id + ": bad reward");
        }
        return cfg;
    }

    /// <summary>Total EXP needed to reach a level (lv1 = 0, lv2 = 100, lv3 = 300, ... step x (level-1) each).</summary>
    public long ExpForLevel(int level) => expPerLevelStep * (long)(level - 1) * level / 2;
    public int LevelFor(long exp) { int l = 1; while (exp >= ExpForLevel(l + 1) && l < 200) l++; return l; }
}

public sealed class QuestObjectiveDto { public string text { get; set; } = ""; public int have { get; set; } public int need { get; set; } }
public sealed class QuestDto
{
    public string id { get; set; } = "";
    public string type { get; set; } = "";
    public string title { get; set; } = "";
    public string desc { get; set; } = "";
    public string mapId { get; set; } = "";
    public string mapName { get; set; } = "";
    public string npc { get; set; } = "";
    public QuestObjectiveDto[] objectives { get; set; } = Array.Empty<QuestObjectiveDto>();
    public long rewardExp { get; set; }
    public long rewardGold { get; set; }
    public string rewardItems { get; set; } = "";
    public bool complete { get; set; }
    public bool claimed { get; set; }
    public int step { get; set; }   // main chain: 1..N
}
public sealed class QuestLogDto
{
    public QuestDto[] main { get; set; } = Array.Empty<QuestDto>();
    public QuestDto[] side { get; set; } = Array.Empty<QuestDto>();
    public QuestDto[] daily { get; set; } = Array.Empty<QuestDto>();
    public int mainTotal { get; set; }
    public int level { get; set; }
    public long exp { get; set; }
    public long expLevelStart { get; set; }
    public long expNextLevel { get; set; }
    public string dailyDay { get; set; } = "";
    public string turnInNpc { get; set; } = ""; // an NPC with a finished quest to hand in ("?" over its head)
}
public sealed class QuestClaimDto { public string? questId { get; set; } }
public sealed class QuestEventDto { public string? @event { get; set; } }
public sealed class QuestClaimResultDto { public string questId { get; set; } = ""; public long exp { get; set; } public long gold { get; set; } public int level { get; set; } public bool leveledUp { get; set; } public QuestLogDto log { get; set; } = new(); }

/// <summary>
/// Main / side / daily quests, all server-side. Progress is fed by server events (kills, pickups, skills that hit,
/// purchases, potions used, map travel); the client may only report talking to an NPC and practising in town, and
/// only for an objective an active quest has right now (anything else is ignored). The main chain advances one quest
/// at a time; daily quests restart every Vietnam calendar day (00:00 UTC+7, server clock). A reward (EXP, Gold, items)
/// is paid once per (quest, period) in one transaction: ledger key "quest:{character}:{quest}:{period}" + the claimed
/// row; items must fit in the bag first. EXP raises the level on the server's curve.
/// </summary>
public sealed class QuestService
{
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<long> now;
    public QuestConfig Config { get; }

    public static readonly string[] ClientEvents = { "talk:elder", "talk:healer", "talk:tailor", "practice:attack", "practice:skill1", "practice:skill2", "practice:skill3" };

    public QuestService(Database db, QuestConfig config, InventoryService inv, Func<long>? clock = null)
    {
        this.db = db;
        Config = config;
        this.inv = inv;
        now = clock ?? Database.NowMs;
    }

    public string VnDay() => DateTimeOffset.FromUnixTimeMilliseconds(now()).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
    private string Period(QuestDef q) => q.type == "daily" ? VnDay() : "";

    private static void Own(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId)
    {
        if (Database.Scalar<string>(c, tx, "SELECT id FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId)) == null)
            throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
    }

    private (Dictionary<string, int> counts, bool claimed) Row(SqliteConnection c, SqliteTransaction tx, string characterId, QuestDef q)
    {
        using var cmd = Database.Command(c, tx, "SELECT counts_json, claimed_ms FROM quest_progress WHERE character_id=$c AND quest_id=$q AND period=$p",
            ("$c", characterId), ("$q", q.id), ("$p", Period(q)));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return (new Dictionary<string, int>(), false);
        return (JsonSerializer.Deserialize<Dictionary<string, int>>(r.GetString(0)) ?? new(), !r.IsDBNull(1));
    }

    private static bool Done(QuestDef q, Dictionary<string, int> counts) => q.objectives.All(o => counts.GetValueOrDefault(o.@event) >= o.need);

    /// <summary>The main quest currently open (first one not yet claimed), or null when the chain is finished.</summary>
    private QuestDef? CurrentMain(SqliteConnection c, SqliteTransaction tx, string characterId) =>
        Config.quests.Where(q => q.type == "main").FirstOrDefault(q => !Row(c, tx, characterId, q).claimed);

    private IEnumerable<QuestDef> Active(SqliteConnection c, SqliteTransaction tx, string characterId)
    {
        QuestDef? main = CurrentMain(c, tx, characterId);
        if (main != null) yield return main;
        foreach (QuestDef q in Config.quests.Where(q => q.type != "main")) if (!Row(c, tx, characterId, q).claimed) yield return q;
    }

    /// <summary>Counts an event for every active quest objective that wants it (capped at the objective's need).</summary>
    public void Record(string accountId, string characterId, string ev, int amount = 1)
    {
        if (amount <= 0 || string.IsNullOrEmpty(ev)) return;
        db.InTransaction((c, tx) =>
        {
            foreach (QuestDef q in Active(c, tx, characterId).ToList())
            {
                var (counts, _) = Row(c, tx, characterId, q);
                bool changed = false;
                foreach (QuestObjective o in q.objectives.Where(o => o.@event == ev))
                {
                    int have = counts.GetValueOrDefault(o.@event);
                    if (have >= o.need) continue;
                    counts[o.@event] = Math.Min(o.need, have + amount);
                    changed = true;
                }
                if (!changed) continue;
                Database.Exec(c, tx, """
                    INSERT INTO quest_progress(character_id, quest_id, period, counts_json) VALUES($c,$q,$p,$j)
                    ON CONFLICT(character_id, quest_id, period) DO UPDATE SET counts_json=excluded.counts_json
                    """, ("$c", characterId), ("$q", q.id), ("$p", Period(q)), ("$j", JsonSerializer.Serialize(counts)));
            }
            return 0;
        });
    }

    /// <summary>Client-reported event: only talking to an NPC / practising, and only if an active quest needs it now.</summary>
    public QuestLogDto ClientEvent(string accountId, string characterId, QuestEventDto? req)
    {
        string ev = req?.@event ?? "";
        if (!ClientEvents.Contains(ev)) throw new GameError("event_not_allowed", "Sự kiện không hợp lệ.", 400);
        db.InTransaction((c, tx) => { Own(c, tx, accountId, characterId); return 0; });
        Record(accountId, characterId, ev, 1);
        return Log(accountId, characterId);
    }

    public QuestLogDto Log(string accountId, string characterId) => db.InTransaction((c, tx) =>
    {
        Own(c, tx, accountId, characterId);
        QuestDto Dto(QuestDef q, int step = 0)
        {
            var (counts, claimed) = Row(c, tx, characterId, q);
            return new QuestDto
            {
                id = q.id, type = q.type, title = q.title, desc = q.desc, mapId = q.map, npc = q.npc, step = step,
                mapName = CharacterService.Maps.TryGetValue(q.map, out var m) ? m.Name : "",
                objectives = q.objectives.Select(o => new QuestObjectiveDto { text = o.text, have = Math.Min(o.need, counts.GetValueOrDefault(o.@event)), need = o.need }).ToArray(),
                rewardExp = q.rewardExp, rewardGold = q.rewardGold,
                rewardItems = string.Join(", ", q.rewardItems.Select(i => $"{inv.Config.Get(i.itemId)?.name} x{i.qty}")),
                complete = Done(q, counts), claimed = claimed,
            };
        }
        var mains = Config.quests.Where(q => q.type == "main").ToList();
        QuestDef? cur = CurrentMain(c, tx, characterId);
        long exp = Database.Scalar<long>(c, tx, "SELECT experience FROM characters WHERE id=$c", ("$c", characterId));
        int level = Database.Scalar<int>(c, tx, "SELECT level FROM characters WHERE id=$c", ("$c", characterId));
        var log = new QuestLogDto
        {
            main = cur == null ? Array.Empty<QuestDto>() : new[] { Dto(cur, mains.IndexOf(cur) + 1) },
            mainTotal = mains.Count,
            side = Config.quests.Where(q => q.type == "side").Select(q => Dto(q)).Where(d => !d.claimed).ToArray(),
            daily = Config.quests.Where(q => q.type == "daily").Select(q => Dto(q)).ToArray(),
            level = level, exp = exp, expLevelStart = Config.ExpForLevel(level), expNextLevel = Config.ExpForLevel(level + 1),
            dailyDay = VnDay(),
        };
        log.turnInNpc = log.main.Concat(log.side).FirstOrDefault(d => d.complete && !d.claimed && d.npc != "")?.npc ?? "";
        return log;
    });

    public QuestClaimResultDto Claim(string accountId, string characterId, QuestClaimDto? req)
    {
        QuestDef q = Config.quests.FirstOrDefault(x => x.id == req?.questId) ?? throw new GameError("quest_unknown", "Nhiệm vụ không tồn tại.", 404);
        var res = db.InTransaction((c, tx) =>
        {
            Own(c, tx, accountId, characterId);
            var (counts, claimed) = Row(c, tx, characterId, q);
            if (claimed) throw new GameError("already_claimed", "Đã nhận thưởng nhiệm vụ này.", 409);
            if (q.type == "main" && CurrentMain(c, tx, characterId)?.id != q.id) throw new GameError("quest_locked", "Nhiệm vụ này chưa mở.", 409);
            if (!Done(q, counts)) throw new GameError("quest_incomplete", "Nhiệm vụ chưa hoàn thành.", 409);
            string period = Period(q);
            foreach (QuestItemReward it in q.rewardItems)
                if (!inv.Fits(c, tx, characterId, it.itemId, it.qty)) throw new GameError("bag_full", "Túi đồ không đủ chỗ cho phần thưởng.", 409);
            int marked = Database.Exec(c, tx, """
                INSERT INTO quest_progress(character_id, quest_id, period, counts_json, claimed_ms) VALUES($c,$q,$p,$j,$t)
                ON CONFLICT(character_id, quest_id, period) DO UPDATE SET claimed_ms=excluded.claimed_ms WHERE quest_progress.claimed_ms IS NULL
                """, ("$c", characterId), ("$q", q.id), ("$p", period), ("$j", JsonSerializer.Serialize(counts)), ("$t", now()));
            if (marked == 0) throw new GameError("already_claimed", "Đã nhận thưởng nhiệm vụ này.", 409);
            if (q.rewardGold > 0) Wallet.Gold(c, tx, accountId, characterId, q.rewardGold, "quest:" + q.id, $"quest:{characterId}:{q.id}:{period}");
            foreach (QuestItemReward it in q.rewardItems) inv.Add(c, tx, characterId, it.itemId, it.qty);
            int before = Database.Scalar<int>(c, tx, "SELECT level FROM characters WHERE id=$c", ("$c", characterId));
            long exp = Database.Scalar<long>(c, tx, "SELECT experience FROM characters WHERE id=$c", ("$c", characterId)) + q.rewardExp;
            int level = Math.Max(before, Config.LevelFor(exp));
            Database.Exec(c, tx, "UPDATE characters SET experience=$e, level=$l, revision=revision+1, saved_ms=$t WHERE id=$c",
                ("$e", exp), ("$l", level), ("$t", now()), ("$c", characterId));
            AccountService.Audit(c, tx, accountId, "quest_claim", $"{characterId} {q.id} {period} +{q.rewardExp}exp +{q.rewardGold}gold");
            return new QuestClaimResultDto { questId = q.id, exp = q.rewardExp, gold = q.rewardGold, level = level, leveledUp = level > before };
        });
        res.log = Log(accountId, characterId);
        return res;
    }
}
