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
public sealed class PetConfig
{
    public int version { get; set; }
    public int maxSatietyMinutes { get; set; } = 60;
    public float pickupReach { get; set; } = 8f;
    public PetDef[] pets { get; set; } = Array.Empty<PetDef>();
    public long MaxSatietyMs => maxSatietyMinutes * 60_000L;

    public static PetConfig Load(string path)
    {
        var cfg = JsonSerializer.Deserialize<PetConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty pet config");
        if (cfg.maxSatietyMinutes < 1 || cfg.pickupReach <= 0) throw new InvalidDataException("bad pet config");
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
}
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
        };
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
