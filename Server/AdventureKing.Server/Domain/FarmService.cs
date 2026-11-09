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
    public int attack { get; set; } = 6;            // damage per hit before the hero's defence
    public float attackSeconds { get; set; } = 1.6f; // time between two hits
    public float reach { get; set; } = 1.3f;          // metres from the monster's (leashed) position
    public float leash { get; set; } = 3.5f;          // how far it chases from its spawn point
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

/// <summary>The hero's server-side combat state on a farm map (HP is decided here, never by the client).</summary>
public sealed class HeroCombatDto
{
    public int hp { get; set; }
    public int maxHp { get; set; }
    public bool dead { get; set; }
    public int damageTaken { get; set; }           // since the previous answer (for hit feedback)
    public string[] hitBy { get; set; } = Array.Empty<string>(); // spawn ids that struck since the previous answer
}

public sealed class FarmStateDto
{
    public string mapId { get; set; } = "";
    public MonsterDto[] monsters { get; set; } = Array.Empty<MonsterDto>();
    public DropDto[] drops { get; set; } = Array.Empty<DropDto>();
    public HeroCombatDto hero { get; set; } = new();
}

public sealed class AttackRequestDto { public string? spawnId { get; set; } public float x { get; set; } public float y { get; set; } }
public sealed class AttackResultDto
{
    public MonsterDto monster { get; set; } = new();
    public int damage { get; set; }
    public bool killed { get; set; }
    public DropDto? drop { get; set; }
    public HeroCombatDto hero { get; set; } = new();
}
public sealed class PickupRequestDto { public string? dropId { get; set; } public float x { get; set; } public float y { get; set; } }
public sealed class PickupResultDto { public string dropId { get; set; } = ""; public long amount { get; set; } public long gold { get; set; } public HeroCombatDto hero { get; set; } = new(); }
public sealed class PresenceRequestDto { public float x { get; set; } public float y { get; set; } }

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
    // Mirrors the client's HeroClassCatalog (damage, seconds between swings, reach in metres, max HP, defence). Server copy wins.
    private static readonly (int damage, float interval, float range, int maxHp, int defense)[] ClassStats =
    {
        (18, 0.85f, 1.6f, 140, 12), // Kiếm Sĩ
        (34, 1.6f, 6f, 75, 3),      // Pháp Sư
        (13, 0.4f, 1.8f, 90, 5),    // Sát Thủ
        (22, 0.9f, 7f, 100, 6),     // Cung Thủ
    };
    /// <summary>Where a fallen hero gets back up on each farm map (the map's portal arrival point).</summary>
    public static readonly IReadOnlyDictionary<string, (float x, float y)> RespawnPoints = new Dictionary<string, (float, float)>
    {
        ["meadow_breeze"] = (-36f, 1f),
    };
    public const long RegenDelayMs = 6000;      // out of combat this long -> regenerate
    public const float RegenPerSecond = 0.03f;  // of max HP
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

    private sealed class Live { public int Hp; public long RespawnAtMs; public string? Aggro; public long NextHitMs; }
    private sealed class Hero
    {
        public int Hp, Max, Defense; public bool Dead; public long LastDamageMs, LastRegenMs; public float RegenCarry;
        public int Pending; public List<string> HitBy = new();
    }
    private readonly Dictionary<string, Hero> heroes = new();
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

    private Hero HeroOf(string characterId, int heroClass, long t)
    {
        if (!heroes.TryGetValue(characterId, out Hero? h))
        {
            var st = ClassStats[Math.Clamp(heroClass, 0, ClassStats.Length - 1)];
            heroes[characterId] = h = new Hero { Hp = st.maxHp, Max = st.maxHp, Defense = st.defense, LastDamageMs = t - RegenDelayMs, LastRegenMs = t };
        }
        return h;
    }

    /// <summary>
    /// Applies what happened since the last request: monsters aggroed on this hero strike on their own timer while
    /// the hero's last reported position is within reach of where the monster can be (spawn ± leash). Going quiet
    /// does not dodge hits (the last known position counts). Out of combat the hero regenerates. All server-side.
    /// </summary>
    private void Evaluate(string characterId, string mapId, Hero h, long t)
    {
        bool known = presence.TryGetValue(characterId + "|" + mapId, out var p);
        foreach (SpawnPoint s in Config.spawns.Where(s => s.mapId == mapId))
        {
            Live m = live[s.id];
            if (m.Aggro != characterId) continue;
            MonsterType ty = TypeOf(s);
            if (m.Hp <= 0 || h.Dead || !known || MathF.Abs(p.x - s.x) > ty.leash + 8f) { m.Aggro = null; continue; } // lost interest
            float monsterX = Math.Clamp(p.x, s.x - ty.leash, s.x + ty.leash);
            bool inReach = MathF.Abs(p.x - monsterX) <= ty.reach && MathF.Abs(p.y - s.y) <= 2f;
            long step = (long)(ty.attackSeconds * 1000f);
            if (!inReach) { m.NextHitMs = Math.Max(m.NextHitMs, t + step / 2); continue; } // must close in again (wind-up)
            while (m.NextHitMs <= t && !h.Dead)
            {
                int dmg = Math.Max(1, ty.attack - h.Defense / 2);
                h.Hp -= dmg;
                h.Pending += dmg;
                h.HitBy.Add(s.id);
                h.LastDamageMs = m.NextHitMs;
                m.NextHitMs += step;
                if (h.Hp <= 0) { h.Hp = 0; h.Dead = true; }
            }
        }
        if (h.Dead) foreach (Live m in live.Values) if (m.Aggro == characterId) m.Aggro = null;
        // regeneration out of combat
        if (!h.Dead && h.Hp < h.Max && t - h.LastDamageMs >= RegenDelayMs)
        {
            float secs = (t - Math.Max(h.LastRegenMs, h.LastDamageMs + RegenDelayMs)) / 1000f;
            h.RegenCarry += Math.Max(0f, secs) * RegenPerSecond * h.Max;
            int add = (int)h.RegenCarry;
            h.RegenCarry -= add;
            h.Hp = Math.Min(h.Max, h.Hp + add);
        }
        h.LastRegenMs = t;
    }

    private static HeroCombatDto Drain(Hero h)
    {
        var dto = new HeroCombatDto { hp = h.Hp, maxHp = h.Max, dead = h.Dead, damageTaken = h.Pending, hitBy = h.HitBy.ToArray() };
        h.Pending = 0;
        h.HitBy.Clear();
        return dto;
    }

    /// <summary>Position heartbeat (~2/s while on a farm map): moves the hero (speed-checked) and resolves monster hits.</summary>
    public HeroCombatDto Presence(string accountId, string characterId, PresenceRequestDto? req)
    {
        var (heroClass, mapId) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, t);
            if (!h.Dead) Move(characterId, mapId, req?.x ?? float.NaN, req?.y ?? float.NaN, t);
            Evaluate(characterId, mapId, h, t);
            return Drain(h);
        }
    }

    /// <summary>Gets a fallen hero back up at the map's respawn point with full HP (no Gold or item loss).</summary>
    public HeroCombatDto Revive(string accountId, string characterId)
    {
        var (heroClass, mapId) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, t);
            if (!h.Dead) throw new GameError("not_dead", "Nhân vật chưa gục ngã.", 409);
            h.Dead = false;
            h.Hp = h.Max;
            h.LastDamageMs = t - RegenDelayMs;
            var spot = RespawnPoints.TryGetValue(mapId, out var r) ? r : (0f, 1f);
            presence[characterId + "|" + mapId] = (spot.Item1, spot.Item2, t);
            return Drain(h);
        }
    }

    public FarmStateDto State(string accountId, string characterId)
    {
        var (heroClass, mapId) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            foreach (var d in drops.Values.Where(d => d.ExpiresMs <= t || d.Picked).ToList()) drops.Remove(d.Id);
            var spawns = Config.spawns.Where(s => s.mapId == mapId).ToArray();
            foreach (SpawnPoint s in spawns) Tick(s, t);
            Hero h = HeroOf(characterId, heroClass, t);
            Evaluate(characterId, mapId, h, t);
            return new FarmStateDto
            {
                mapId = mapId,
                monsters = spawns.Select(s => Dto(s, t)).ToArray(),
                drops = drops.Values.Where(d => d.Owner == characterId && d.MapId == mapId).Select(d => Dto(d, t)).ToArray(),
                hero = Drain(h),
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
            Hero h = HeroOf(characterId, heroClass, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            Move(characterId, mapId, req!.x, req.y, t);
            Evaluate(characterId, mapId, h, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
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
                m.Aggro = null;
            }
            else if (m.Aggro != characterId)
            {
                m.Aggro = characterId; // it fights back: first strike after one wind-up
                m.NextHitMs = t + (long)(TypeOf(s).attackSeconds * 1000f);
            }
            result.monster = Dto(s, t);
            result.hero = Drain(h);
            return result;
        }
    }

    public PickupResultDto Pickup(string accountId, string characterId, PickupRequestDto? req)
    {
        var (heroClass, mapId) = Character(accountId, characterId);
        long t = now();
        Drop d;
        HeroCombatDto heroDto;
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            Move(characterId, mapId, req?.x ?? float.NaN, req?.y ?? float.NaN, t);
            Evaluate(characterId, mapId, h, t);
            heroDto = Drain(h);
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
                return new PickupResultDto { dropId = d.Id, amount = r.Applied ? d.Amount : 0, gold = r.Balance, hero = heroDto };
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
