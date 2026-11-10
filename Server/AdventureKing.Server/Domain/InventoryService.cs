using System.Text.Json;
using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class ItemDef
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string kind { get; set; } = "consumable"; // consumable | equipment | material | ticket | petfood | quest
    public string icon { get; set; } = "";
    public string desc { get; set; } = "";
    public int hp { get; set; }
    public int mp { get; set; }
    public long priceGold { get; set; }
    public string equipSlot { get; set; } = "";
    public int[] classes { get; set; } = Array.Empty<int>();
    public int attack { get; set; }
    public int defense { get; set; }
    // tickets
    public string destMap { get; set; } = "";
    public float destX { get; set; }
    public float destY { get; set; }
    public int minLevel { get; set; } = 1;
    // pet food
    public int satietyMinutes { get; set; }
}

public sealed class ShopTab { public string id { get; set; } = ""; public string name { get; set; } = ""; public string[] items { get; set; } = Array.Empty<string>(); }
public sealed class ShopDef { public string id { get; set; } = ""; public string npc { get; set; } = ""; public string mapId { get; set; } = ""; public ShopTab[] tabs { get; set; } = Array.Empty<ShopTab>(); }

public sealed class KitEntry { public string itemId { get; set; } = ""; public int qty { get; set; } = 1; public int? heroClass { get; set; } }

public sealed class ItemConfig
{
    public int version { get; set; }
    public int bagSlots { get; set; } = 40;
    public int stackMax { get; set; } = 99;
    public float potionCooldownSeconds { get; set; } = 1f;
    public ItemDef[] items { get; set; } = Array.Empty<ItemDef>();
    public KitEntry[] starterKit { get; set; } = Array.Empty<KitEntry>();
    public ShopDef[] shops { get; set; } = Array.Empty<ShopDef>();

    public static ItemConfig Load(string path)
    {
        var cfg = JsonSerializer.Deserialize<ItemConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty item config");
        if (cfg.stackMax < 1 || cfg.stackMax > 99 || cfg.bagSlots < 1) throw new InvalidDataException("bad bag config");
        if (cfg.items.Select(i => i.id).Distinct().Count() != cfg.items.Length) throw new InvalidDataException("duplicate item id");
        foreach (ItemDef i in cfg.items)
        {
            if (i.kind == "equipment" && i.equipSlot is not ("weapon" or "armor" or "accessory")) throw new InvalidDataException(i.id + ": bad equip slot");
            // a ticket may only lead to a map the server really has (never to an unbuilt map)
            if (i.kind == "ticket" && !CharacterService.Maps.ContainsKey(i.destMap)) throw new InvalidDataException(i.id + ": ticket to unknown map " + i.destMap);
        }
        foreach (ShopDef s in cfg.shops)
            foreach (string id in s.tabs.SelectMany(t => t.items))
                if (cfg.items.FirstOrDefault(x => x.id == id) is not { priceGold: > 0 }) throw new InvalidDataException($"shop {s.id}: {id} unknown or unpriced");
        foreach (KitEntry k in cfg.starterKit)
            if (cfg.items.All(i => i.id != k.itemId) || k.qty < 1) throw new InvalidDataException("starter kit: unknown item " + k.itemId);
        return cfg;
    }

    public ItemDef? Get(string id) => items.FirstOrDefault(i => i.id == id);
    public bool Stackable(ItemDef d) => d.kind != "equipment";
}

public sealed class BagSlotDto
{
    public int index { get; set; }
    public string itemId { get; set; } = "";
    public string name { get; set; } = "";
    public string kind { get; set; } = "";
    public string icon { get; set; } = "";
    public string desc { get; set; } = "";
    public int qty { get; set; }
    public string equipSlot { get; set; } = "";
    public int attack { get; set; }
    public int defense { get; set; }
    public bool usableByClass { get; set; } = true;
    public string destMap { get; set; } = "";
}

public sealed class EquippedDto { public string equipSlot { get; set; } = ""; public BagSlotDto item { get; set; } = new(); }

public sealed class InventoryDto
{
    public int capacity { get; set; }
    public int stackMax { get; set; }
    public BagSlotDto[] slots { get; set; } = Array.Empty<BagSlotDto>();
    public EquippedDto[] equipped { get; set; } = Array.Empty<EquippedDto>();
    public int attackBonus { get; set; }
    public int defenseBonus { get; set; }
}

/// <summary>force: drink even with a full bar (the player confirmed; nothing is restored above the maximum).</summary>
public sealed class UseItemRequestDto { public int slot { get; set; } public bool force { get; set; } }
public sealed class UseItemResultDto
{
    public string itemId { get; set; } = "";
    public int restoredHp { get; set; }
    public int restoredMp { get; set; }
    public HeroCombatDto hero { get; set; } = new();
    public InventoryDto inventory { get; set; } = new();
}
public sealed class EquipRequestDto { public int slot { get; set; } }
public sealed class UnequipRequestDto { public string? equipSlot { get; set; } }

/// <summary>
/// Server-owned character bag (default 40 cells, up to 99 of a stackable item per cell; extra quantity spills into
/// further cells, and an add that does not fit is refused as a whole) plus basic equipment (weapon / armor / accessory,
/// whose attack/defence bonuses the farm combat reads). Every change is one SQLite transaction and the rows are the
/// only copy of an item, so disconnecting, double-clicking or two devices cannot duplicate anything.
/// Potions: the item is removed and the HP/MP restored in the same transaction; a full bar or a fallen hero is refused
/// without consuming anything; a short per-character potion cooldown stops spam macros.
/// </summary>
public sealed class InventoryService
{
    private readonly Database db;
    private readonly FarmService farm;
    private readonly Func<long> now;
    private readonly Dictionary<string, long> potionReady = new();
    private readonly object gate = new();
    public ItemConfig Config { get; }

    public InventoryService(Database db, ItemConfig config, FarmService farm, Func<long>? clock = null)
    {
        this.db = db;
        Config = config;
        this.farm = farm;
        now = clock ?? Database.NowMs;
        farm.EquipmentBonus = Bonus;
    }

    private int ClassOf(SqliteConnection c, SqliteTransaction? tx, string accountId, string characterId)
    {
        using var cmd = Database.Command(c, tx, "SELECT hero_class FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId));
        object? v = cmd.ExecuteScalar();
        if (v == null || v is DBNull) throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
        return Convert.ToInt32(v);
    }

    /// <summary>Gear bonus used by combat (no account check: the farm already verified the character).</summary>
    public (int attack, int defense) Bonus(string characterId)
    {
        using var c = db.Open();
        int atk = 0, def = 0;
        using var cmd = Database.Command(c, null, "SELECT item_id FROM equipped WHERE character_id=$c", ("$c", characterId));
        using var r = cmd.ExecuteReader();
        while (r.Read()) { ItemDef? d = Config.Get(r.GetString(0)); if (d != null) { atk += d.attack; def += d.defense; } }
        return (atk, def);
    }

    // ------------------------------------------------------------------ bag primitives (inside a transaction)
    private Dictionary<int, (string item, int qty)> Bag(SqliteConnection c, SqliteTransaction tx, string characterId)
    {
        var bag = new Dictionary<int, (string, int)>();
        using var cmd = Database.Command(c, tx, "SELECT slot_index, item_id, qty FROM bag_slots WHERE character_id=$c", ("$c", characterId));
        using var r = cmd.ExecuteReader();
        while (r.Read()) bag[r.GetInt32(0)] = (r.GetString(1), r.GetInt32(2));
        return bag;
    }

    /// <summary>Adds qty of an item: tops up existing stacks to the cap, then fills free cells. All or nothing.</summary>
    public void Add(SqliteConnection c, SqliteTransaction tx, string characterId, string itemId, int qty)
    {
        ItemDef d = Config.Get(itemId) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
        if (qty < 1) throw new GameError("bad_qty", "Số lượng không hợp lệ.");
        var bag = Bag(c, tx, characterId);
        int cap = Config.Stackable(d) ? Config.stackMax : 1, left = qty;
        foreach (var (slot, (item, have)) in bag.OrderBy(kv => kv.Key).ToList())
        {
            if (left == 0) break;
            if (item != itemId || have >= cap) continue;
            int put = Math.Min(cap - have, left);
            Database.Exec(c, tx, "UPDATE bag_slots SET qty=qty+$q WHERE character_id=$c AND slot_index=$s", ("$q", put), ("$c", characterId), ("$s", slot));
            left -= put;
        }
        for (int slot = 0; slot < Config.bagSlots && left > 0; slot++)
        {
            if (bag.ContainsKey(slot)) continue;
            int put = Math.Min(cap, left);
            Database.Exec(c, tx, "INSERT INTO bag_slots(character_id, slot_index, item_id, qty) VALUES($c,$s,$i,$q)", ("$c", characterId), ("$s", slot), ("$i", itemId), ("$q", put));
            bag[slot] = (itemId, put);
            left -= put;
        }
        if (left > 0) throw new GameError("bag_full", "Túi đồ không đủ chỗ.", 409); // the transaction rolls back: nothing added
    }

    /// <summary>Removes qty of an item from one bag cell (inside the caller's transaction). Fails if the cell does not
    /// hold that item in that quantity.</summary>
    public void RemoveFromSlot(SqliteConnection c, SqliteTransaction tx, string characterId, int slot, string itemId, int qty)
    {
        var bag = Bag(c, tx, characterId);
        if (!bag.TryGetValue(slot, out var cell) || cell.item != itemId || cell.qty < qty) throw new GameError("slot_empty", "Ô này không có vật phẩm.", 409);
        if (cell.qty > qty) Database.Exec(c, tx, "UPDATE bag_slots SET qty=qty-$n WHERE character_id=$c AND slot_index=$s AND qty=$q", ("$n", qty), ("$c", characterId), ("$s", slot), ("$q", cell.qty));
        else Database.Exec(c, tx, "DELETE FROM bag_slots WHERE character_id=$c AND slot_index=$s AND qty=$q", ("$c", characterId), ("$s", slot), ("$q", qty));
    }

    public (string item, int qty)? Cell(SqliteConnection c, SqliteTransaction tx, string characterId, int slot) =>
        Bag(c, tx, characterId).TryGetValue(slot, out var cell) ? cell : null;

    public InventoryDto SnapshotFor(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId) =>
        Snapshot(c, tx, characterId, ClassOf(c, tx, accountId, characterId));

    /// <summary>Space check without changing anything (for shops: check before taking Gold).</summary>
    public bool Fits(SqliteConnection c, SqliteTransaction tx, string characterId, string itemId, int qty)
    {
        ItemDef? d = Config.Get(itemId);
        if (d == null || qty < 1) return false;
        var bag = Bag(c, tx, characterId);
        int cap = Config.Stackable(d) ? Config.stackMax : 1;
        long room = bag.Values.Where(v => v.item == itemId).Sum(v => (long)Math.Max(0, cap - v.qty)) + (long)(Config.bagSlots - bag.Count) * cap;
        return room >= qty;
    }

    private void GrantStarterKit(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId, int heroClass)
    {
        int inserted = Database.Exec(c, tx, "INSERT OR IGNORE INTO item_grants(character_id, grant_key, granted_ms) VALUES($c,'starter_kit_v1',$t)", ("$c", characterId), ("$t", now()));
        if (inserted == 0) return; // already granted once
        foreach (KitEntry k in Config.starterKit)
            if (k.heroClass == null || k.heroClass == heroClass) Add(c, tx, characterId, k.itemId, k.qty);
        AccountService.Audit(c, tx, accountId, "starter_kit", characterId);
    }

    private InventoryDto Snapshot(SqliteConnection c, SqliteTransaction tx, string characterId, int heroClass)
    {
        BagSlotDto Dto(int index, string itemId, int qty)
        {
            ItemDef d = Config.Get(itemId) ?? new ItemDef { id = itemId, name = itemId };
            return new BagSlotDto
            {
                index = index, itemId = itemId, name = d.name, kind = d.kind, icon = d.icon, desc = d.desc, qty = qty,
                equipSlot = d.equipSlot, attack = d.attack, defense = d.defense, destMap = d.destMap,
                usableByClass = d.kind != "equipment" || d.classes.Length == 0 || d.classes.Contains(heroClass),
            };
        }
        var slots = Bag(c, tx, characterId).OrderBy(kv => kv.Key).Select(kv => Dto(kv.Key, kv.Value.item, kv.Value.qty)).ToArray();
        var eq = new List<EquippedDto>();
        using (var cmd = Database.Command(c, tx, "SELECT equip_slot, item_id FROM equipped WHERE character_id=$c ORDER BY equip_slot", ("$c", characterId)))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) eq.Add(new EquippedDto { equipSlot = r.GetString(0), item = Dto(-1, r.GetString(1), 1) });
        return new InventoryDto
        {
            capacity = Config.bagSlots, stackMax = Config.stackMax, slots = slots, equipped = eq.ToArray(),
            attackBonus = eq.Sum(e => e.item.attack), defenseBonus = eq.Sum(e => e.item.defense),
        };
    }

    // ------------------------------------------------------------------ API
    public InventoryDto Get(string accountId, string characterId) => db.InTransaction((c, tx) =>
    {
        int cls = ClassOf(c, tx, accountId, characterId);
        GrantStarterKit(c, tx, accountId, characterId, cls);
        return Snapshot(c, tx, characterId, cls);
    });

    public Action<string, string, string, int>? QuestEvent { get; set; }

    public UseItemResultDto Use(string accountId, string characterId, UseItemRequestDto? req)
    {
        UseItemResultDto r = UseCore(accountId, characterId, req);
        QuestEvent?.Invoke(accountId, characterId, "use:potion", 1);
        return r;
    }

    private UseItemResultDto UseCore(string accountId, string characterId, UseItemRequestDto? req)
    {
        if (req == null) throw new GameError("bad_request", "Thiếu dữ liệu.");
        long t = now();
        lock (gate)
        {
            if (potionReady.TryGetValue(characterId, out long ready) && t < ready) throw new GameError("too_fast", "Dùng thuốc quá nhanh.", 429);
        }
        return db.InTransaction((c, tx) =>
        {
            int cls = ClassOf(c, tx, accountId, characterId);
            var bag = Bag(c, tx, characterId);
            if (!bag.TryGetValue(req.slot, out var cell)) throw new GameError("slot_empty", "Ô này không có vật phẩm.", 409);
            ItemDef d = Config.Get(cell.item) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
            if (d.kind != "consumable" || (d.hp <= 0 && d.mp <= 0)) throw new GameError("not_usable", "Vật phẩm này không dùng trực tiếp được.", 409);
            HeroCombatDto before = farm.Peek(accountId, characterId);
            if (before.dead) throw new GameError("hero_dead", "Nhân vật đã gục ngã, hãy hồi sinh.", 409);
            bool needHp = d.hp > 0 && before.hp < before.maxHp, needMp = d.mp > 0 && before.mp < before.maxMp;
            if (!needHp && !needMp && !req.force) throw new GameError("already_full", d.hp > 0 ? "HP đã đầy." : "MP đã đầy.", 409);
            // remove exactly one, then restore; any failure rolls the removal back
            if (cell.qty > 1) Database.Exec(c, tx, "UPDATE bag_slots SET qty=qty-1 WHERE character_id=$c AND slot_index=$s AND qty=$q", ("$c", characterId), ("$s", req.slot), ("$q", cell.qty));
            else Database.Exec(c, tx, "DELETE FROM bag_slots WHERE character_id=$c AND slot_index=$s AND qty=1", ("$c", characterId), ("$s", req.slot));
            var (hp, mp, hero) = farm.Restore(accountId, characterId, d.hp, d.mp);
            lock (gate) potionReady[characterId] = t + (long)(Config.potionCooldownSeconds * 1000f);
            return new UseItemResultDto { itemId = d.id, restoredHp = hp, restoredMp = mp, hero = hero, inventory = Snapshot(c, tx, characterId, cls) };
        });
    }

    /// <summary>Equips the gear in a bag cell; whatever was in that equipment slot goes back into the same cell (swap).</summary>
    public InventoryDto Equip(string accountId, string characterId, EquipRequestDto? req) => db.InTransaction((c, tx) =>
    {
        if (req == null) throw new GameError("bad_request", "Thiếu dữ liệu.");
        int cls = ClassOf(c, tx, accountId, characterId);
        var bag = Bag(c, tx, characterId);
        if (!bag.TryGetValue(req.slot, out var cell)) throw new GameError("slot_empty", "Ô này không có vật phẩm.", 409);
        ItemDef d = Config.Get(cell.item) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
        if (d.kind != "equipment") throw new GameError("not_equipment", "Vật phẩm này không trang bị được.", 409);
        if (d.classes.Length > 0 && !d.classes.Contains(cls)) throw new GameError("wrong_class", "Nghề của bạn không dùng được trang bị này.", 409);
        string? old = Database.Scalar<string>(c, tx, "SELECT item_id FROM equipped WHERE character_id=$c AND equip_slot=$e", ("$c", characterId), ("$e", d.equipSlot));
        Database.Exec(c, tx, "DELETE FROM bag_slots WHERE character_id=$c AND slot_index=$s", ("$c", characterId), ("$s", req.slot));
        if (old != null)
            Database.Exec(c, tx, "INSERT INTO bag_slots(character_id, slot_index, item_id, qty) VALUES($c,$s,$i,1)", ("$c", characterId), ("$s", req.slot), ("$i", old));
        Database.Exec(c, tx, "INSERT INTO equipped(character_id, equip_slot, item_id) VALUES($c,$e,$i) ON CONFLICT(character_id, equip_slot) DO UPDATE SET item_id=excluded.item_id",
            ("$c", characterId), ("$e", d.equipSlot), ("$i", d.id));
        return Snapshot(c, tx, characterId, cls);
    });

    public InventoryDto Unequip(string accountId, string characterId, UnequipRequestDto? req) => db.InTransaction((c, tx) =>
    {
        int cls = ClassOf(c, tx, accountId, characterId);
        string? item = Database.Scalar<string>(c, tx, "SELECT item_id FROM equipped WHERE character_id=$c AND equip_slot=$e", ("$c", characterId), ("$e", req?.equipSlot ?? ""));
        if (item == null) throw new GameError("not_equipped", "Ô trang bị này đang trống.", 409);
        Database.Exec(c, tx, "DELETE FROM equipped WHERE character_id=$c AND equip_slot=$e", ("$c", characterId), ("$e", req!.equipSlot));
        Add(c, tx, characterId, item, 1); // bag full -> GameError -> rollback (stays equipped)
        return Snapshot(c, tx, characterId, cls);
    });
}
