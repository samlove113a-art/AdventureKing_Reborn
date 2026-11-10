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
        foreach (SpawnPoint s in spawns)
        {
            if (!FarmService.SafeZones.TryGetValue(s.mapId, out var z)) continue;
            MonsterType t = types.First(x => x.id == s.type);
            float r = t.leash + t.reach;
            if (s.x + r >= z.minX && s.x - r <= z.maxX) throw new InvalidDataException(s.id + ": reaches into the safe zone of " + s.mapId);
        }
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

// ---------------------------------------------------------------- skills (config/skills.json)
public sealed class SkillDef
{
    public int slot { get; set; }
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public float cooldownSeconds { get; set; }
    public int mpCost { get; set; }
    public float damageMultiplier { get; set; } = 1f;
    public string shape { get; set; } = "target"; // target | self | line
    public float range { get; set; }
    public float radius { get; set; }
    public int maxTargets { get; set; } = 1;
}

public sealed class ClassSkills
{
    public int heroClass { get; set; }
    public string name { get; set; } = "";
    public int maxMp { get; set; }
    public float mpRegenPerSecond { get; set; }
    public SkillDef[] skills { get; set; } = Array.Empty<SkillDef>();
}

public sealed class SkillConfig
{
    public int version { get; set; }
    public float globalCooldownSeconds { get; set; } = 0.45f;
    public float mpCostPerLevel { get; set; }
    public float maxMpPerLevel { get; set; }
    public ClassSkills[] classes { get; set; } = Array.Empty<ClassSkills>();

    public static SkillConfig Load(string path)
    {
        var cfg = JsonSerializer.Deserialize<SkillConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty skill config");
        cfg.Validate();
        return cfg;
    }

    public void Validate()
    {
        foreach (ClassSkills c in classes)
        {
            if (c.maxMp <= 0 || c.mpRegenPerSecond < 0) throw new InvalidDataException(c.name + ": bad MP");
            if (c.skills.Length != 3 || c.skills.Select(s => s.slot).OrderBy(s => s).SequenceEqual(new[] { 1, 2, 3 }) == false)
                throw new InvalidDataException(c.name + ": exactly skills 1, 2, 3");
            foreach (SkillDef s in c.skills)
                if (s.cooldownSeconds <= 0 || s.mpCost < 0 || s.damageMultiplier <= 0 || s.maxTargets < 1 || s.shape is not ("target" or "self" or "line"))
                    throw new InvalidDataException(s.id + ": bad skill");
        }
    }

    public ClassSkills? For(int heroClass) => classes.FirstOrDefault(c => c.heroClass == heroClass);
    public int MaxMp(ClassSkills c, int level) => (int)MathF.Round(c.maxMp * (1f + maxMpPerLevel * Math.Max(0, level - 1)));
    public int Cost(SkillDef s, int level) => (int)MathF.Round(s.mpCost * (1f + mpCostPerLevel * Math.Max(0, level - 1)));
}

public sealed class SkillRequestDto { public int slot { get; set; } public string? spawnId { get; set; } public float x { get; set; } public float y { get; set; } public int facing { get; set; } = 1; }
public sealed class SkillHitDto { public MonsterDto monster { get; set; } = new(); public int damage { get; set; } public bool killed { get; set; } public DropDto? drop { get; set; } }
public sealed class SkillResultDto
{
    public int slot { get; set; }
    public string skillId { get; set; } = "";
    public string name { get; set; } = "";
    public int mpSpent { get; set; }
    public float cx { get; set; }
    public float cy { get; set; }
    public SkillHitDto[] hits { get; set; } = Array.Empty<SkillHitDto>();
    public HeroCombatDto hero { get; set; } = new();
}

/// <summary>The hero's server-side combat state on a farm map (HP is decided here, never by the client).</summary>
public sealed class HeroCombatDto
{
    public int hp { get; set; }
    public int maxHp { get; set; }
    public int mp { get; set; }
    public int maxMp { get; set; }
    public long[] cooldownMs { get; set; } = new long[3]; // remaining for skills 1..3
    public bool dead { get; set; }
    public int damageTaken { get; set; }           // since the previous answer (for hit feedback)
    public string[] hitBy { get; set; } = Array.Empty<string>(); // spawn ids that struck since the previous answer
    public PetDrinkDto? petDrink { get; set; }     // the pet gave a potion on this heartbeat (server-decided)
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
    /// <summary>
    /// Lực Chiến from the same stats the server fights with: damage per second (class base + gear attack), max HP,
    /// defence (class + gear) and level. Titles / nameplate frames never add power (owner rule).
    /// </summary>
    public int CombatPower(string characterId, int heroClass, int level)
    {
        var st = ClassStats[Math.Clamp(heroClass, 0, ClassStats.Length - 1)];
        var (atk, def) = Bonus(characterId);
        double dps = (st.damage + atk) / st.interval;
        return (int)Math.Round(dps * 10 + st.maxHp + (st.defense + def) * 5 + level * 25);
    }

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
        ["ch1_mushroom_village"] = (-36f, 1f),
    };

    /// <summary>
    /// Safe zones inside farm maps (village squares, around quest NPCs and portals): no spawn may be placed so that
    /// its chase (leash) plus its reach enters the zone, so monsters can never strike a hero standing there.
    /// Enforced when the config loads. Mirrors the client's TownLayout.*.SafeZone (shown as "Vùng an toàn").
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (float minX, float maxX)> SafeZones = new Dictionary<string, (float, float)>
    {
        ["ch1_mushroom_village"] = (-46f, 30f),
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
        public int Mp, MaxMp; public float MpRegen, MpCarry; public long LastMpMs;
        public readonly long[] Ready = new long[3]; public long GlobalReady;
    }

    public SkillConfig Skills { get; }
    private readonly Dictionary<string, Hero> heroes = new();
    private sealed class Drop { public string Id = "", MapId = "", Owner = "", Account = "", Type = ""; public float X, Y; public long Amount, ExpiresMs; public bool Picked; }

    public FarmService(Database db, FarmConfig config, Func<long>? clock = null, Random? random = null, SkillConfig? skills = null)
    {
        this.db = db;
        Config = config;
        Skills = skills ?? new SkillConfig();
        now = clock ?? Database.NowMs;
        rng = random ?? new Random();
        foreach (SpawnPoint s in config.spawns) live[s.id] = new Live { Hp = TypeOf(s).maxHp };
    }

    private MonsterType TypeOf(SpawnPoint s) => Config.types.First(t => t.id == s.type);

    private (int heroClass, string mapId, int level) Character(string accountId, string characterId)
    {
        using var c = db.Open();
        using var cmd = Database.Command(c, null, "SELECT hero_class, map_id, level FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
        return (r.GetInt32(0), r.GetString(1), r.GetInt32(2));
    }

    /// <summary>Equipment stat bonus (attack, defence) of a character; wired to the inventory service.</summary>
    public Func<string, (int attack, int defense)>? EquipmentBonus { get; set; }
    private (int attack, int defense) Bonus(string characterId) => EquipmentBonus?.Invoke(characterId) ?? (0, 0);

    /// <summary>Pet hooks (wired to PetService): satiety drain per heartbeat, and whether a fed pet is out.</summary>
    public Action<string>? PetTick { get; set; }
    public Func<string, bool>? PetCanCollect { get; set; }
    public float PetReach { get; set; } = 8f;

    /// <summary>Quest progress hook (accountId, characterId, event, amount): kills, pickups, skills that hit.</summary>
    public Action<string, string, string, int>? QuestEvent { get; set; }
    private void Fire(string acc, string ch, string ev, int n = 1) { try { QuestEvent?.Invoke(acc, ch, ev, n); } catch (Exception e) { Console.Error.WriteLine("quest hook: " + e.Message); } }

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

    private Hero HeroOf(string characterId, int heroClass, int level, long t)
    {
        if (!heroes.TryGetValue(characterId, out Hero? h))
        {
            var st = ClassStats[Math.Clamp(heroClass, 0, ClassStats.Length - 1)];
            heroes[characterId] = h = new Hero { Hp = st.maxHp, Max = st.maxHp, Defense = st.defense, LastDamageMs = t - RegenDelayMs, LastRegenMs = t, LastMpMs = t };
            ClassSkills? cs = Skills.For(heroClass);
            if (cs != null) { h.MaxMp = h.Mp = Skills.MaxMp(cs, level); h.MpRegen = cs.mpRegenPerSecond; }
        }
        return h;
    }

    /// <summary>MP regenerates continuously (in and out of combat) on the server clock.</summary>
    private static void RegenMp(Hero h, long t)
    {
        if (!h.Dead && h.Mp < h.MaxMp)
        {
            h.MpCarry += Math.Max(0, t - h.LastMpMs) / 1000f * h.MpRegen;
            int add = (int)h.MpCarry;
            h.MpCarry -= add;
            h.Mp = Math.Min(h.MaxMp, h.Mp + add);
        }
        else h.MpCarry = 0f;
        h.LastMpMs = t;
    }

    /// <summary>
    /// Applies what happened since the last request: monsters aggroed on this hero strike on their own timer while
    /// the hero's last reported position is within reach of where the monster can be (spawn ± leash). Going quiet
    /// does not dodge hits (the last known position counts). Out of combat the hero regenerates. All server-side.
    /// </summary>
    private void Evaluate(string characterId, string mapId, Hero h, long t)
    {
        bool known = presence.TryGetValue(characterId + "|" + mapId, out var p);
        int defenseBonus = Bonus(characterId).defense;
        RegenMp(h, t);
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
                int dmg = Math.Max(1, ty.attack - (h.Defense + defenseBonus) / 2);
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

    private static HeroCombatDto Drain(Hero h, long t)
    {
        var dto = new HeroCombatDto
        {
            hp = h.Hp, maxHp = h.Max, mp = h.Mp, maxMp = h.MaxMp, dead = h.Dead, damageTaken = h.Pending, hitBy = h.HitBy.ToArray(),
            cooldownMs = h.Ready.Select(r => Math.Max(0, r - t)).ToArray(),
        };
        h.Pending = 0;
        h.HitBy.Clear();
        return dto;
    }

    /// <summary>Position heartbeat (~2/s while on a farm map): moves the hero (speed-checked) and resolves monster hits.</summary>
    public HeroCombatDto Presence(string accountId, string characterId, PresenceRequestDto? req)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        long t = now();
        HeroCombatDto dto;
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
            if (!h.Dead) Move(characterId, mapId, req?.x ?? float.NaN, req?.y ?? float.NaN, t);
            Evaluate(characterId, mapId, h, t);
            dto = Drain(h, t);
            if (!h.Dead) try { PetTick?.Invoke(characterId); } catch (Exception e) { Console.Error.WriteLine("pet tick: " + e.Message); }
        }
        if (!dto.dead && PetAutoPotion != null)
        {
            try
            {
                dto.petDrink = PetAutoPotion(accountId, characterId, dto, (hp, mp) => Restore(accountId, characterId, hp, mp));
            }
            catch (GameError) { } // e.g. fell in between: no drink, nothing consumed (rolled back)
        }
        return dto;
    }

    /// <summary>Pet auto-potion hook (PetService.AutoPotion), run after each heartbeat on farm maps.</summary>
    public Func<string, string, HeroCombatDto, Func<int, int, (int hp, int mp, HeroCombatDto hero)>, PetDrinkDto?>? PetAutoPotion { get; set; }

    /// <summary>Gets a fallen hero back up at the map's respawn point with full HP (no Gold or item loss).</summary>
    public HeroCombatDto Revive(string accountId, string characterId)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
            if (!h.Dead) throw new GameError("not_dead", "Nhân vật chưa gục ngã.", 409);
            h.Dead = false;
            h.Hp = h.Max;
            h.Mp = h.MaxMp;
            h.LastDamageMs = t - RegenDelayMs;
            var spot = RespawnPoints.TryGetValue(mapId, out var r) ? r : (0f, 1f);
            presence[characterId + "|" + mapId] = (spot.Item1, spot.Item2, t);
            return Drain(h, t);
        }
    }

    public FarmStateDto State(string accountId, string characterId)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            foreach (var d in drops.Values.Where(d => d.ExpiresMs <= t || d.Picked).ToList()) drops.Remove(d.Id);
            var spawns = Config.spawns.Where(s => s.mapId == mapId).ToArray();
            foreach (SpawnPoint s in spawns) Tick(s, t);
            Hero h = HeroOf(characterId, heroClass, level, t);
            Evaluate(characterId, mapId, h, t);
            return new FarmStateDto
            {
                mapId = mapId,
                monsters = spawns.Select(s => Dto(s, t)).ToArray(),
                drops = drops.Values.Where(d => d.Owner == characterId && d.MapId == mapId).Select(d => Dto(d, t)).ToArray(),
                hero = Drain(h, t),
            };
        }
    }

    public AttackResultDto Attack(string accountId, string characterId, AttackRequestDto? req)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        SpawnPoint s = Config.spawns.FirstOrDefault(p => p.id == req?.spawnId) ?? throw new GameError("monster_unknown", "Quái vật không tồn tại.", 404);
        if (s.mapId != mapId) throw new GameError("wrong_map", "Quái vật không ở bản đồ của bạn.", 409);
        var stats = ClassStats[Math.Clamp(heroClass, 0, ClassStats.Length - 1)];
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
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
            int dmg = stats.damage + Bonus(characterId).attack;
            SkillHitDto hit = Damage(s, dmg, accountId, characterId, mapId, t);
            if (hit.killed) { Fire(accountId, characterId, "kill:" + s.type); Fire(accountId, characterId, "kill:any"); }
            return new AttackResultDto { damage = dmg, killed = hit.killed, drop = hit.drop, monster = hit.monster, hero = Drain(h, t) };
        }
    }

    /// <summary>Applies server damage to a live monster: on death it rolls the Gold drop (owned by the killer, picked up
    /// once) and starts the respawn timer; otherwise the monster aggroes on the attacker.</summary>
    private SkillHitDto Damage(SpawnPoint s, int dmg, string accountId, string characterId, string mapId, long t)
    {
        Live m = live[s.id];
        m.Hp -= dmg;
        var hit = new SkillHitDto { damage = dmg };
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
            hit.killed = true;
            hit.drop = Dto(d, t);
            m.Aggro = null;
        }
        else if (m.Aggro != characterId)
        {
            m.Aggro = characterId; // it fights back: first strike after one wind-up
            m.NextHitMs = t + (long)(TypeOf(s).attackSeconds * 1000f);
        }
        hit.monster = Dto(s, t);
        return hit;
    }

    /// <summary>
    /// Casts skill 1/2/3. The server checks: the skill exists for this class, the hero is alive and on a farm map, the
    /// reported position is plausible, the skill's own cooldown and the global cooldown have passed, and there is
    /// enough MP (cost scales with level). Only then MP is spent and the cooldown starts. Damage = class base damage
    /// (+ equipment) x the skill multiplier, applied to live monsters inside the skill's shape:
    ///  - "target": a chosen monster within range, plus others within radius of it;
    ///  - "self": monsters within radius of the hero;
    ///  - "line": monsters in front of the hero (facing) within range, nearest first.
    /// A cast that would hit nothing is refused without spending MP (no wasted MP, no free cooldown spam).
    /// </summary>
    /// <summary>A skill the SERVER accepted (characterId, slot, centre x/y, main target spawn id): other players on the
    /// map replay the same skill presentation from this (PresenceService.RecordCast → SyncDto peers).</summary>
    public Action<string, int, float, float, string>? SkillCast { get; set; }

    public SkillResultDto Skill(string accountId, string characterId, SkillRequestDto? req)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        if (req == null) throw new GameError("bad_request", "Thiếu dữ liệu kỹ năng.");
        ClassSkills cs = Skills.For(heroClass) ?? throw new GameError("skill_unknown", "Nghề này chưa có kỹ năng.", 404);
        SkillDef sk = cs.skills.FirstOrDefault(k => k.slot == req.slot) ?? throw new GameError("skill_unknown", "Kỹ năng không tồn tại.", 404);
        if (!Config.spawns.Any(sp => sp.mapId == mapId)) throw new GameError("safe_zone", "Không thể dùng kỹ năng trong khu an toàn.", 409);
        var stats = ClassStats[Math.Clamp(heroClass, 0, ClassStats.Length - 1)];
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            Move(characterId, mapId, req.x, req.y, t);
            Evaluate(characterId, mapId, h, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            int i = sk.slot - 1;
            if (t < h.Ready[i]) throw new GameError("skill_cooldown", $"{sk.name} đang hồi chiêu ({(h.Ready[i] - t + 999) / 1000} giây).", 429);
            if (t < h.GlobalReady) throw new GameError("too_fast", "Thi triển quá nhanh.", 429);
            int cost = Skills.Cost(sk, level);
            if (h.Mp < cost) throw new GameError("not_enough_mp", $"Không đủ MP ({h.Mp}/{cost}).", 409);

            var alive = Config.spawns.Where(sp => sp.mapId == mapId).Where(sp => { Tick(sp, t); return live[sp.id].Hp > 0; }).ToList();
            bool InY(SpawnPoint sp, float y) => MathF.Abs(sp.y - y) <= 2.5f;
            List<SpawnPoint> targets;
            float cx = req.x, cy = req.y;
            switch (sk.shape)
            {
                case "self":
                    targets = alive.Where(sp => InY(sp, req.y) && MathF.Abs(sp.x - req.x) <= sk.radius + MonsterHalfWidth)
                        .OrderBy(sp => MathF.Abs(sp.x - req.x)).Take(sk.maxTargets).ToList();
                    break;
                case "line":
                    int dir = req.facing >= 0 ? 1 : -1;
                    targets = alive.Where(sp => InY(sp, req.y) && (sp.x - req.x) * dir >= -MonsterHalfWidth && MathF.Abs(sp.x - req.x) <= sk.range + MonsterHalfWidth)
                        .OrderBy(sp => MathF.Abs(sp.x - req.x)).Take(sk.maxTargets).ToList();
                    cx = req.x + dir * sk.range * 0.5f;
                    break;
                default: // target
                    SpawnPoint? main = alive.FirstOrDefault(sp => sp.id == req.spawnId)
                        ?? alive.Where(sp => InY(sp, req.y) && MathF.Abs(sp.x - req.x) <= sk.range + MonsterHalfWidth).OrderBy(sp => MathF.Abs(sp.x - req.x)).FirstOrDefault();
                    if (main == null || MathF.Abs(main.x - req.x) > sk.range + MonsterHalfWidth || !InY(main, req.y)) { targets = new(); break; }
                    cx = main.x; cy = main.y;
                    targets = alive.Where(sp => sp == main || (InY(sp, main.y) && MathF.Abs(sp.x - main.x) <= sk.radius + MonsterHalfWidth))
                        .OrderBy(sp => sp == main ? -1f : MathF.Abs(sp.x - main.x)).Take(sk.maxTargets).ToList();
                    break;
            }
            if (targets.Count == 0) throw new GameError("no_target", "Không có mục tiêu trong tầm kỹ năng.", 409);

            h.Mp -= cost;
            h.Ready[i] = t + (long)(sk.cooldownSeconds * 1000f);
            h.GlobalReady = t + (long)(Skills.globalCooldownSeconds * 1000f);
            int dmg = (int)MathF.Round((stats.damage + Bonus(characterId).attack) * sk.damageMultiplier);
            var hits = targets.Select(sp => Damage(sp, dmg, accountId, characterId, mapId, t)).ToArray();
            Fire(accountId, characterId, "skill:any");
            foreach (var (sp, h2) in targets.Zip(hits))
                if (h2.killed) { Fire(accountId, characterId, "kill:" + sp.type); Fire(accountId, characterId, "kill:any"); }
            try { SkillCast?.Invoke(characterId, sk.slot, cx, cy, targets[0].id); } catch (Exception e) { Console.Error.WriteLine("skill cast hook: " + e.Message); }
            return new SkillResultDto { slot = sk.slot, skillId = sk.id, name = sk.name, mpSpent = cost, cx = cx, cy = cy, hits = hits, hero = Drain(h, t) };
        }
    }

    /// <summary>Restores HP/MP from a consumed potion (the inventory service removes the item in the same request).
    /// Never above the maximum; refused for a fallen hero. Returns the amounts actually restored.</summary>
    public (int hp, int mp, HeroCombatDto hero) Restore(string accountId, string characterId, int hp, int mp)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
            Evaluate(characterId, mapId, h, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            int addHp = Math.Max(0, Math.Min(hp, h.Max - h.Hp)), addMp = Math.Max(0, Math.Min(mp, h.MaxMp - h.Mp));
            h.Hp += addHp;
            h.Mp += addMp;
            return (addHp, addMp, Drain(h, t));
        }
    }

    /// <summary>Current HP/MP (for checks before consuming a potion).</summary>
    public HeroCombatDto Peek(string accountId, string characterId)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        long t = now();
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
            Evaluate(characterId, mapId, h, t);
            var d = Drain(h, t);
            h.Pending += d.damageTaken; h.HitBy.AddRange(d.hitBy); // peek must not swallow hit feedback
            return d;
        }
    }

    /// <summary>The summoned pet collects one of the owner's drops: only while the pet is out and fed, only the owner's
    /// own drops, within the pet's reach of the owner's (speed-checked) position. Same drop, same ledger key: never twice.</summary>
    public PickupResultDto PetPickup(string accountId, string characterId, PickupRequestDto? req)
    {
        if (PetCanCollect == null || !PetCanCollect(characterId)) throw new GameError("pet_unavailable", "Không có thú cưng no bụng đang được triệu hồi.", 409);
        return Pickup(accountId, characterId, req, byPet: true);
    }

    public PickupResultDto Pickup(string accountId, string characterId, PickupRequestDto? req) => Pickup(accountId, characterId, req, byPet: false);

    private PickupResultDto Pickup(string accountId, string characterId, PickupRequestDto? req, bool byPet)
    {
        var (heroClass, mapId, level) = Character(accountId, characterId);
        long t = now();
        Drop d;
        HeroCombatDto heroDto;
        lock (gate)
        {
            Hero h = HeroOf(characterId, heroClass, level, t);
            if (h.Dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            Move(characterId, mapId, req?.x ?? float.NaN, req?.y ?? float.NaN, t);
            Evaluate(characterId, mapId, h, t);
            heroDto = Drain(h, t);
            if (req?.dropId == null || !drops.TryGetValue(req.dropId, out d!) || d.Picked || d.ExpiresMs <= t)
                throw new GameError("drop_gone", "Vàng này không còn trên mặt đất.", 409);
            if (d.Owner != characterId || d.MapId != mapId) throw new GameError("not_your_drop", "Đây không phải vàng của bạn.", 403);
            float reach = byPet ? PetReach : PickupReach;
            if (MathF.Abs(req.x - d.X) > reach || MathF.Abs(req.y - d.Y) > (byPet ? 4f : 2.5f)) throw new GameError("too_far", byPet ? "Vàng ở quá xa thú cưng." : "Hãy lại gần hơn để nhặt.", 409);
            d.Picked = true; // claimed: a second request (double click, replay) can never reach the wallet
        }
        try
        {
            PickupResultDto res = db.InTransaction((c, tx) =>
            {
                LedgerResult r = Wallet.Gold(c, tx, accountId, characterId, d.Amount, "loot:" + d.Type, "loot:" + d.Id);
                if (r.Applied) { AccountService.Audit(c, tx, accountId, "loot", $"{characterId} {d.Id} +{d.Amount}gold"); }
                return new PickupResultDto { dropId = d.Id, amount = r.Applied ? d.Amount : 0, gold = r.Balance, hero = heroDto };
            });
            if (res.amount > 0) Fire(accountId, characterId, "pickup:gold");
            return res;
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
