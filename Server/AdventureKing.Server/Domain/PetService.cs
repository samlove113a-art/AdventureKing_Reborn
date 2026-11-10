using System.Text.Json;
using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class PetDef
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string tier { get; set; } = "normal"; // normal | vip
    public string currency { get; set; } = "gold"; // gold | diamonds
    public long price { get; set; }
    public string desc { get; set; } = "";
}
public sealed class AutoPotionConfig { public float cooldownSeconds { get; set; } = 8f; public int minPercent { get; set; } = 10; public int maxPercent { get; set; } = 90; }
public sealed class PetConfig
{
    public int version { get; set; }
    public int maxSatietyMinutes { get; set; } = 60;
    public float pickupReach { get; set; } = 8f;
    public AutoPotionConfig autoPotion { get; set; } = new();
    public PetDef[] pets { get; set; } = Array.Empty<PetDef>();
    public long MaxSatietyMs => maxSatietyMinutes * 60_000L;

    public static PetConfig Load(string path)
    {
        var cfg = JsonSerializer.Deserialize<PetConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty pet config");
        if (cfg.maxSatietyMinutes < 1 || cfg.pickupReach <= 0) throw new InvalidDataException("bad pet config");
        if (cfg.autoPotion.cooldownSeconds < 1f || cfg.autoPotion.minPercent < 1 || cfg.autoPotion.maxPercent > 99 || cfg.autoPotion.minPercent > cfg.autoPotion.maxPercent)
            throw new InvalidDataException("bad pet autoPotion config");
        foreach (PetDef p in cfg.pets)
            if (p.price <= 0 || p.currency is not ("gold" or "diamonds") || p.tier is not ("normal" or "vip")) throw new InvalidDataException(p.id + ": bad pet");
        return cfg;
    }
}

public sealed class OwnedPetDto { public string petId { get; set; } = ""; public string name { get; set; } = ""; public string tier { get; set; } = ""; public bool summoned { get; set; } public long satietyMs { get; set; } public bool hungry { get; set; } }
public sealed class PetStateDto
{
    public PetDef[] catalog { get; set; } = Array.Empty<PetDef>();
    public OwnedPetDto[] owned { get; set; } = Array.Empty<OwnedPetDto>();
    public string summoned { get; set; } = "";
    public long maxSatietyMs { get; set; }
    public float pickupReach { get; set; }
    public long gold { get; set; }
    public long diamonds { get; set; }
    public AutoPotionDto autoPotion { get; set; } = new();
}
/// <summary>Player's auto-potion settings (+ the server's limits, for the UI).</summary>
public sealed class AutoPotionDto
{
    public bool hpEnabled { get; set; }
    public int hpPercent { get; set; } = 50;
    public bool mpEnabled { get; set; }
    public int mpPercent { get; set; } = 30;
    public float cooldownSeconds { get; set; }
    public int minPercent { get; set; }
    public int maxPercent { get; set; }
}
/// <summary>What the pet did on this heartbeat (the client animates it).</summary>
public sealed class PetDrinkDto { public string itemId { get; set; } = ""; public string itemName { get; set; } = ""; public int hp { get; set; } public int mp { get; set; } public int left { get; set; } }
public sealed class PetBuyDto { public string? petId { get; set; } public string? requestId { get; set; } }
public sealed class PetIdDto { public string? petId { get; set; } }
public sealed class PetFeedDto { public string? petId { get; set; } public int slot { get; set; } }

/// <summary>
/// Pets. Normal pets cost Gold (character wallet), VIP pets Diamonds (account wallet, server-verified only); each pet
/// can be owned once per character (primary key). One pet may be summoned at a time. Satiety is time: 60 minutes of
/// summoned activity (the hero present on a farm map — offline or idle time does not count), then the pet is hungry and
/// stops collecting. Food from the bag adds minutes, never above the maximum (a full pet refuses food, nothing is used).
/// A fed, summoned pet collects only its owner's drops, within reach of the owner (FarmService checks; same drop, paid
/// once — a pet never creates Gold).
/// </summary>
public sealed class PetService
{
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<long> now;
    private readonly object gate = new();
    private readonly Dictionary<string, long> lastTick = new();
    public PetConfig Config { get; }
    public const long MaxTickGapMs = 5000; // longer silences (offline, AFK, loading) do not drain the pet

    public PetService(Database db, PetConfig config, InventoryService inv, Func<long>? clock = null)
    {
        this.db = db;
        Config = config;
        this.inv = inv;
        now = clock ?? Database.NowMs;
    }

    private static void Own(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId)
    {
        if (Database.Scalar<string>(c, tx, "SELECT id FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId)) == null)
            throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
    }

    private PetStateDto Snapshot(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId)
    {
        var owned = new List<OwnedPetDto>();
        using (var cmd = Database.Command(c, tx, "SELECT pet_id, summoned, satiety_ms FROM pets_owned WHERE character_id=$c ORDER BY bought_ms", ("$c", characterId)))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                PetDef? d = Config.pets.FirstOrDefault(p => p.id == r.GetString(0));
                long s = r.GetInt64(2);
                owned.Add(new OwnedPetDto { petId = r.GetString(0), name = d?.name ?? r.GetString(0), tier = d?.tier ?? "", summoned = r.GetInt64(1) != 0, satietyMs = s, hungry = s <= 0 });
            }
        return new PetStateDto
        {
            catalog = Config.pets, owned = owned.ToArray(), summoned = owned.FirstOrDefault(o => o.summoned)?.petId ?? "",
            maxSatietyMs = Config.MaxSatietyMs, pickupReach = Config.pickupReach,
            gold = Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c", ("$c", characterId)),
            diamonds = Database.Scalar<long>(c, tx, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", accountId)),
            autoPotion = AutoSettings(c, tx, characterId),
        };
    }

    private AutoPotionDto AutoSettings(SqliteConnection c, SqliteTransaction? tx, string characterId)
    {
        var a = new AutoPotionDto { cooldownSeconds = Config.autoPotion.cooldownSeconds, minPercent = Config.autoPotion.minPercent, maxPercent = Config.autoPotion.maxPercent };
        using var cmd = Database.Command(c, tx, "SELECT hp_enabled, hp_percent, mp_enabled, mp_percent FROM pet_auto_potion WHERE character_id=$c", ("$c", characterId));
        using var r = cmd.ExecuteReader();
        if (r.Read()) { a.hpEnabled = r.GetInt64(0) != 0; a.hpPercent = r.GetInt32(1); a.mpEnabled = r.GetInt64(2) != 0; a.mpPercent = r.GetInt32(3); }
        return a;
    }

    /// <summary>Saves the player's auto-potion settings (thresholds clamped to the server's limits).</summary>
    public PetStateDto SetAutoPotion(string accountId, string characterId, AutoPotionDto? req) => db.InTransaction((c, tx) =>
    {
        Own(c, tx, accountId, characterId);
        if (req == null) throw new GameError("bad_request", "Thiếu dữ liệu.");
        int Clamp(int p) => Math.Clamp(p, Config.autoPotion.minPercent, Config.autoPotion.maxPercent);
        Database.Exec(c, tx, """
            INSERT INTO pet_auto_potion(character_id, hp_enabled, hp_percent, mp_enabled, mp_percent) VALUES($c,$he,$hp,$me,$mp)
            ON CONFLICT(character_id) DO UPDATE SET hp_enabled=excluded.hp_enabled, hp_percent=excluded.hp_percent, mp_enabled=excluded.mp_enabled, mp_percent=excluded.mp_percent
            """, ("$c", characterId), ("$he", req.hpEnabled ? 1 : 0), ("$hp", Clamp(req.hpPercent)), ("$me", req.mpEnabled ? 1 : 0), ("$mp", Clamp(req.mpPercent)));
        return Snapshot(c, tx, accountId, characterId);
    });

    private readonly Dictionary<string, long> hpReady = new(), mpReady = new();
    private const int BarHp = 0, BarMp = 1;

    /// <summary>
    /// Called by the farm after each heartbeat with the hero's server HP/MP. If the player enabled it, a summoned pet
    /// with satiety left gives ONE potion from the bag for a bar under its threshold, once per cooldown per bar. The
    /// potion that best fits the missing amount is chosen (largest that does not overshoot, else the smallest).
    /// Removing the potion and restoring happen in one request; the client cannot trigger or fake a drink.
    /// </summary>
    public PetDrinkDto? AutoPotion(string accountId, string characterId, HeroCombatDto hero, Func<int, int, (int hp, int mp, HeroCombatDto hero)> restore)
    {
        if (hero.dead || hero.maxHp <= 0) return null;
        long t = now();
        return db.InTransaction<PetDrinkDto?>((c, tx) =>
        {
            if (Database.Scalar<long?>(c, tx, "SELECT satiety_ms FROM pets_owned WHERE character_id=$c AND summoned=1", ("$c", characterId)) is not > 0) return null;
            AutoPotionDto a = AutoSettings(c, tx, characterId);
            foreach (int bar in new[] { BarHp, BarMp })
            {
                bool on = bar == BarHp ? a.hpEnabled : a.mpEnabled && hero.maxMp > 0;
                if (!on) continue;
                int cur = bar == BarHp ? hero.hp : hero.mp, max = bar == BarHp ? hero.maxHp : hero.maxMp;
                if (cur * 100 >= (bar == BarHp ? a.hpPercent : a.mpPercent) * max) continue;
                var ready = bar == BarHp ? hpReady : mpReady;
                lock (gate) if (ready.TryGetValue(characterId, out long r) && t < r) continue;
                int missing = max - cur;
                var choices = inv.Cells(c, tx, characterId)
                    .Select(kv => (slot: kv.Key, kv.Value.item, kv.Value.qty, def: inv.Config.Get(kv.Value.item)))
                    .Where(x => x.def != null && x.def.kind == "consumable" && (bar == BarHp ? x.def.hp > 0 && x.def.mp == 0 : x.def.mp > 0 && x.def.hp == 0))
                    .Select(x => (x.slot, x.item, x.qty, amount: bar == BarHp ? x.def!.hp : x.def!.mp, x.def!.name))
                    .ToList();
                if (choices.Count == 0) continue;
                var fit = choices.Where(x => x.amount <= missing).OrderByDescending(x => x.amount).ThenBy(x => x.slot).FirstOrDefault();
                var pick = fit.item != null ? fit : choices.OrderBy(x => x.amount).ThenBy(x => x.slot).First();
                inv.RemoveFromSlot(c, tx, characterId, pick.slot, pick.item, 1);
                var (hp, mp, after) = restore(bar == BarHp ? pick.amount : 0, bar == BarMp ? pick.amount : 0);
                lock (gate) ready[characterId] = t + (long)(Config.autoPotion.cooldownSeconds * 1000f);
                hero.hp = after.hp; hero.mp = after.mp;
                int left = inv.Cells(c, tx, characterId).Values.Where(v => v.item == pick.item).Sum(v => v.qty);
                return new PetDrinkDto { itemId = pick.item, itemName = pick.name, hp = hp, mp = mp, left = left };
            }
            return null;
        });
    }

    public PetStateDto Get(string accountId, string characterId) => db.InTransaction((c, tx) => { Own(c, tx, accountId, characterId); return Snapshot(c, tx, accountId, characterId); });

    public PetStateDto Buy(string accountId, string characterId, PetBuyDto? req)
    {
        PetDef d = Config.pets.FirstOrDefault(p => p.id == req?.petId) ?? throw new GameError("pet_unknown", "Thú cưng không tồn tại.", 404);
        if (string.IsNullOrWhiteSpace(req!.requestId) || req.requestId.Length > 80) throw new GameError("bad_request", "Thiếu mã giao dịch.");
        return db.InTransaction((c, tx) =>
        {
            Own(c, tx, accountId, characterId);
            if (Database.Scalar<string>(c, tx, "SELECT pet_id FROM pets_owned WHERE character_id=$c AND pet_id=$p", ("$c", characterId), ("$p", d.id)) != null)
                throw new GameError("pet_owned", "Bạn đã có thú cưng này.", 409);
            LedgerResult r = d.currency == "gold"
                ? Wallet.Gold(c, tx, accountId, characterId, -d.price, "pet:" + d.id, "pet:" + characterId + ":" + req.requestId)
                : Wallet.Diamonds(c, tx, accountId, -d.price, "pet:" + d.id, "pet:" + accountId + ":" + req.requestId);
            if (!r.Applied) return Snapshot(c, tx, accountId, characterId); // replay: nothing charged twice
            Database.Exec(c, tx, "INSERT INTO pets_owned(character_id, pet_id, summoned, satiety_ms, bought_ms) VALUES($c,$p,0,$s,$t)",
                ("$c", characterId), ("$p", d.id), ("$s", Config.MaxSatietyMs), ("$t", now()));
            AccountService.Audit(c, tx, accountId, "pet_buy", $"{characterId} {d.id} -{d.price}{d.currency}");
            return Snapshot(c, tx, accountId, characterId);
        });
    }

    public PetStateDto Summon(string accountId, string characterId, PetIdDto? req) => db.InTransaction((c, tx) =>
    {
        Own(c, tx, accountId, characterId);
        if (Database.Scalar<string>(c, tx, "SELECT pet_id FROM pets_owned WHERE character_id=$c AND pet_id=$p", ("$c", characterId), ("$p", req?.petId ?? "")) == null)
            throw new GameError("pet_not_owned", "Bạn chưa có thú cưng này.", 409);
        Database.Exec(c, tx, "UPDATE pets_owned SET summoned = CASE WHEN pet_id=$p THEN 1 ELSE 0 END WHERE character_id=$c", ("$p", req!.petId), ("$c", characterId));
        lock (gate) lastTick.Remove(characterId);
        return Snapshot(c, tx, accountId, characterId);
    });

    public PetStateDto Dismiss(string accountId, string characterId) => db.InTransaction((c, tx) =>
    {
        Own(c, tx, accountId, characterId);
        Database.Exec(c, tx, "UPDATE pets_owned SET summoned=0 WHERE character_id=$c", ("$c", characterId));
        lock (gate) lastTick.Remove(characterId);
        return Snapshot(c, tx, accountId, characterId);
    });

    public PetStateDto Feed(string accountId, string characterId, PetFeedDto? req) => db.InTransaction((c, tx) =>
    {
        Own(c, tx, accountId, characterId);
        long? sat = Database.Scalar<long?>(c, tx, "SELECT satiety_ms FROM pets_owned WHERE character_id=$c AND pet_id=$p", ("$c", characterId), ("$p", req?.petId ?? ""));
        if (sat == null) throw new GameError("pet_not_owned", "Bạn chưa có thú cưng này.", 409);
        var cell = inv.Cell(c, tx, characterId, req!.slot) ?? throw new GameError("slot_empty", "Ô này không có vật phẩm.", 409);
        ItemDef food = inv.Config.Get(cell.item) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
        if (food.kind != "petfood" || food.satietyMinutes <= 0) throw new GameError("not_food", "Đây không phải thức ăn thú cưng.", 409);
        if (sat.Value >= Config.MaxSatietyMs) throw new GameError("pet_full", "Thú cưng đã no.", 409); // nothing wasted
        inv.RemoveFromSlot(c, tx, characterId, req.slot, food.id, 1);
        long next = Math.Min(Config.MaxSatietyMs, sat.Value + food.satietyMinutes * 60_000L);
        Database.Exec(c, tx, "UPDATE pets_owned SET satiety_ms=$s WHERE character_id=$c AND pet_id=$p", ("$s", next), ("$c", characterId), ("$p", req.petId));
        return Snapshot(c, tx, accountId, characterId);
    });

    /// <summary>Called on every farm-map heartbeat: the summoned pet loses satiety for the active time since the
    /// previous heartbeat (gaps above 5 s — offline, loading, AFK — do not count).</summary>
    public void Tick(string characterId)
    {
        long t = now(), dt;
        lock (gate)
        {
            dt = lastTick.TryGetValue(characterId, out long last) ? Math.Clamp(t - last, 0, MaxTickGapMs) : 0;
            lastTick[characterId] = t;
        }
        if (dt <= 0) return;
        using var c = db.Open();
        Database.Exec(c, null, "UPDATE pets_owned SET satiety_ms = MAX(0, satiety_ms - $d) WHERE character_id=$c AND summoned=1", ("$d", dt), ("$c", characterId));
    }

    /// <summary>A summoned pet with satiety left can collect for this character.</summary>
    public bool CanCollect(string characterId)
    {
        using var c = db.Open();
        return Database.Scalar<long?>(c, null, "SELECT satiety_ms FROM pets_owned WHERE character_id=$c AND summoned=1", ("$c", characterId)) is > 0;
    }
}
