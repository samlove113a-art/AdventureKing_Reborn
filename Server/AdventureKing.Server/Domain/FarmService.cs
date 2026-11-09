using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class MonsterType
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public int maxHp { get; set; }
    public int goldMin { get; set; }
    public int goldMax { get; set; }
    public int respawnSeconds { get; set; } = 60;
}

public sealed class SpawnPoint
{
    public string id { get; set; } = "";
    public string mapId { get; set; } = "";
    public string type { get; set; } = "";
    public float x { get; set; }
    public float y { get; set; }
}

public sealed class FarmConfig
{
    public int version { get; set; }
    public int dropLifetimeSeconds { get; set; } = 120;
    public MonsterType[] types { get; set; } = Array.Empty<MonsterType>();
    public SpawnPoint[] spawns { get; set; } = Array.Empty<SpawnPoint>();

    public static FarmConfig Load(string path)
    {
        var cfg = JsonSerializer.Deserialize<FarmConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty farm config");
        cfg.Validate();
        return cfg;
    }

    public void Validate()
    {
        foreach (MonsterType t in types)
            if (t.maxHp <= 0 || t.goldMin < 0 || t.goldMax < t.goldMin || t.respawnSeconds < 5) throw new InvalidDataException(t.id + ": bad monster type");
        foreach (SpawnPoint s in spawns)
            if (types.All(t => t.id != s.type)) throw new InvalidDataException(s.id + ": unknown type " + s.type);
        if (spawns.Select(s => s.id).Distinct().Count() != spawns.Length) throw new InvalidDataException("duplicate spawn id");
        if (spawns.Any(s => s.mapId == "village_wind_bell")) throw new InvalidDataException("no monsters in the hub (safe zone)");
    }
}

public sealed class MonsterDto
{
    public string id { get; set; } = "";
    public string type { get; set; } = "";
    public string name { get; set; } = "";
    public float x { get; set; }
    public float y { get; set; }
    public int hp { get; set; }
    public int maxHp { get; set; }
    public bool alive { get; set; }
    public long respawnInMs { get; set; }
}

public sealed class DropDto
{
    public string id { get; set; } = "";
    public float x { get; set; }
    public float y { get; set; }
    public long amount { get; set; }
    public long expiresInMs { get; set; }
}

public sealed class FarmStateDto
{
    public string mapId { get; set; } = "";
    public MonsterDto[] monsters { get; set; } = Array.Empty<MonsterDto>();
    public DropDto[] drops { get; set; } = Array.Empty<DropDto>();
}

public sealed class AttackRequestDto { public string? spawnId { get; set; } public float x { get; set; } public float y { get; set; } }
public sealed class AttackResultDto
{
    public MonsterDto monster { get; set; } = new();
    public int damage { get; set; }
    public bool killed { get; set; }
    public DropDto? drop { get; set; }
}
public sealed class PickupRequestDto { public string? dropId { get; set; } public float x { get; set; } public float y { get; set; } }
public sealed class PickupResultDto { public string dropId { get; set; } = ""; public long amount { get; set; } public long gold { get; set; } }

/// <summary>
/// Server-authoritative monster farm. The server owns monster HP, deaths, the 60 s respawn timer, Gold rolls and the
/// drops on the ground; the client only says "I swing at spawn X from position P" and "I pick up drop D from P".
/// Checks: the character belongs to the session and is on that map; P is reachable from its last reported position
/// at running speed; the target is inside the class's attack range; the class cooldown is respected; drops belong to
/// the killer, are close enough and not expired; each drop pays exactly once (ledger idempotency key "loot:{dropId}",
/// plus the in-memory picked flag). No daily cap, no diminishing reward. There is no player-vs-player damage path.
/// Movement itself is client-side (side-scroller physics); the speed check bounds what a forged position can gain.
/// </summary>
public sealed class FarmService
{
    // Mirrors the client's HeroClassCatalog (damage, seconds between swings, reach in metres). Server copy wins.
    private static readonly (int damage, float interval, float range)[] ClassStats =
    {
        (18, 0.85f, 1.6f), // Kiếm Sĩ
        (34, 1.6f, 6f),    // Pháp Sư
        (13, 0.4f, 1.8f),  // Sát Thủ
        (22, 0.9f, 7f),    // Cung Thủ
    };
    public const float MaxSpeed = 12f;        // m/s (run 6-7 m/s, dash/fall headroom)
    public const float PositionSlack = 2.5f;  // m, network jitter
    public const float MonsterHalfWidth = 0.8f;
    public const float PickupReach = 1.8f;

    private readonly Database db;
    private readonly Func<long> now;
    private readonly Random rng;
    private readonly object gate = new();
    private readonly Dictionary<string, Live> live = new();
    private readonly Dictionary<string, Drop> drops = new();
    private readonly Dictionary<string, (float x, float y, long ms)> presence = new();
    private readonly Dictionary<string, long> lastSwing = new();

    public FarmConfig Config { get; }

    private sealed class Live { public int Hp; public long RespawnAtMs; }
    private sealed class Drop { public string Id = "", MapId = "", Owner = "", Account = "", Type = ""; public float X, Y; public long Amount, ExpiresMs; public bool Picked; }

    public FarmService(Database db, FarmConfig config, Func<long>? clock = null, Random? random = null)
    {
        this.db = db;
        Config = config;
        now = clock ?? Database.NowMs;
        rng = random ?? new Random();
        foreach (SpawnPoint s in config.spawns) live[s.id] = new Live { Hp = TypeOf(s).maxHp };
    }

    private MonsterType TypeOf(SpawnPoint s) => Config.types.First(t => t.id == s.type);

    private (int heroClass, string mapId) Character(string accountId, string characterId)
    {
        using var c = db.Open();
        using var cmd = Database.Command(c, null, "SELECT hero_class, map_id FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
        return (r.GetInt32(0), r.GetString(1));
    }

    /// <summary>Revives a dead monster whose respawn time has passed (server clock).</summary>
    private void Tick(SpawnPoint s, long t)
    {
        Live m = live[s.id];
        if (m.Hp <= 0 && t >= m.RespawnAtMs) m.Hp = TypeOf(s).maxHp;
    }

    private MonsterDto Dto(SpawnPoint s, long t)
    {
        Live m = live[s.id];
        MonsterType ty = TypeOf(s);
        return new MonsterDto
        {
            id = s.id, type = s.type, name = ty.name, x = s.x, y = s.y, hp = Math.Max(0, m.Hp), maxHp = ty.maxHp,
            alive = m.Hp > 0, respawnInMs = m.Hp > 0 ? 0 : Math.Max(0, m.RespawnAtMs - t),
        };
    }

    private static DropDto Dto(Drop d, long t) => new() { id = d.Id, x = d.X, y = d.Y, amount = d.Amount, expiresInMs = Math.Max(0, d.ExpiresMs - t) };

    /// <summary>Plausibility of a reported position: reachable from the last one at running speed.</summary>
    private void Move(string characterId, string mapId, float x, float y, long t)
    {
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) throw new GameError("bad_position", "Vị trí không hợp lệ.");
        string key = characterId + "|" + mapId;
        if (presence.TryGetValue(key, out var p))
        {
            float dt = Math.Max(0f, (t - p.ms) / 1000f);
            float dist = MathF.Sqrt((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y));
            if (dist > MaxSpeed * dt + PositionSlack) throw new GameError("moved_too_fast", "Vị trí thay đổi bất thường, thao tác bị từ chối.", 409);
        }
        presence[key] = (x, y, t);
    }

    public FarmStateDto State(string accountId, string characterId)
    {
        var (_, mapId) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            foreach (var d in drops.Values.Where(d => d.ExpiresMs <= t || d.Picked).ToList()) drops.Remove(d.Id);
            var spawns = Config.spawns.Where(s => s.mapId == mapId).ToArray();
            foreach (SpawnPoint s in spawns) Tick(s, t);
            return new FarmStateDto
            {
                mapId = mapId,
                monsters = spawns.Select(s => Dto(s, t)).ToArray(),
                drops = drops.Values.Where(d => d.Owner == characterId && d.MapId == mapId).Select(d => Dto(d, t)).ToArray(),
            };
        }
    }

    public AttackResultDto Attack(string accountId, string characterId, AttackRequestDto? req)
    {
        var (heroClass, mapId) = Character(accountId, characterId);
        SpawnPoint s = Config.spawns.FirstOrDefault(p => p.id == req?.spawnId) ?? throw new GameError("monster_unknown", "Quái vật không tồn tại.", 404);
        if (s.mapId != mapId) throw new GameError("wrong_map", "Quái vật không ở bản đồ của bạn.", 409);
        var stats = ClassStats[Math.Clamp(heroClass, 0, ClassStats.Length - 1)];
        long t = now();
        lock (gate)
        {
            Move(characterId, mapId, req!.x, req.y, t);
            if (MathF.Abs(req.x - s.x) > stats.range + MonsterHalfWidth || MathF.Abs(req.y - s.y) > 2.5f)
                throw new GameError("out_of_range", "Mục tiêu ở ngoài tầm đánh.", 409);
            if (lastSwing.TryGetValue(characterId, out long last) && t - last < (long)(stats.interval * 1000f * 0.8f))
                throw new GameError("too_fast", "Đánh quá nhanh.", 429);
            Tick(s, t);
            Live m = live[s.id];
            if (m.Hp <= 0) throw new GameError("monster_dead", "Quái vật đã bị hạ, chờ hồi sinh.", 409);
            lastSwing[characterId] = t;
            m.Hp -= stats.damage;
            var result = new AttackResultDto { damage = stats.damage };
            if (m.Hp <= 0)
            {
                MonsterType ty = TypeOf(s);
                m.Hp = 0;
                m.RespawnAtMs = t + ty.respawnSeconds * 1000L;
                var d = new Drop
                {
                    Id = Crypto.NewId("drop"), MapId = mapId, Owner = characterId, Account = accountId, Type = ty.id,
                    X = s.x, Y = s.y, Amount = rng.Next(ty.goldMin, ty.goldMax + 1), ExpiresMs = t + Config.dropLifetimeSeconds * 1000L,
                };
                drops[d.Id] = d;
                result.killed = true;
                result.drop = Dto(d, t);
            }
            result.monster = Dto(s, t);
            return result;
        }
    }

    public PickupResultDto Pickup(string accountId, string characterId, PickupRequestDto? req)
    {
        var (_, mapId) = Character(accountId, characterId);
        long t = now();
        Drop d;
        lock (gate)
        {
            Move(characterId, mapId, req?.x ?? float.NaN, req?.y ?? float.NaN, t);
            if (req?.dropId == null || !drops.TryGetValue(req.dropId, out d!) || d.Picked || d.ExpiresMs <= t)
                throw new GameError("drop_gone", "Vàng này không còn trên mặt đất.", 409);
            if (d.Owner != characterId || d.MapId != mapId) throw new GameError("not_your_drop", "Đây không phải vàng của bạn.", 403);
            if (MathF.Abs(req.x - d.X) > PickupReach || MathF.Abs(req.y - d.Y) > 2.5f) throw new GameError("too_far", "Hãy lại gần hơn để nhặt.", 409);
            d.Picked = true; // claimed: a second request (double click, replay) can never reach the wallet
        }
        try
        {
            return db.InTransaction((c, tx) =>
            {
                LedgerResult r = Wallet.Gold(c, tx, accountId, characterId, d.Amount, "loot:" + d.Type, "loot:" + d.Id);
                if (r.Applied) AccountService.Audit(c, tx, accountId, "loot", $"{characterId} {d.Id} +{d.Amount}gold");
                return new PickupResultDto { dropId = d.Id, amount = r.Applied ? d.Amount : 0, gold = r.Balance };
            });
        }
        catch
        {
            lock (gate) d.Picked = false; // the wallet write failed: the coins stay on the ground
            throw;
        }
        finally
        {
            lock (gate) if (d.Picked) drops.Remove(d.Id);
        }
    }
}
