using System.Text.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

/// <summary>
/// Item kinds the inventory understands. Cosmetics are owned once; consumables (pet food) and pets stack/instance
/// later — the owned_items table already carries kind + quantity so they need no schema rewrite.
/// </summary>
public static class ItemKinds
{
    public const string Cosmetic = "cosmetic", CosmeticSet = "cosmetic_set", Pet = "pet", PetFood = "pet_food";
    public static readonly string[] All = { Cosmetic, CosmeticSet, Pet, PetFood };
}

/// <summary>Appearance slots a character can dress. "set" is a premium full-body skin worn over the Gold outfit.</summary>
public static class OutfitSlots
{
    public static readonly string[] Pieces = { "hair", "head", "top", "bottom", "gloves", "shoes", "accessory", "back", "weapon" };
    public const string Set = "set";
    public static bool IsPiece(string? s) => s != null && Array.IndexOf(Pieces, s) >= 0;
}

public sealed class CatalogItem
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public string slot { get; set; } = "";
    public string kind { get; set; } = ItemKinds.Cosmetic;
    public string currency { get; set; } = "gold";
    public long price { get; set; }
    public int[] classes { get; set; } = Array.Empty<int>();
    public bool listed { get; set; }
    public string[] pieces { get; set; } = Array.Empty<string>();
    /// <summary>common / rare / epic / legendary — display only, never affects stats.</summary>
    public string rarity { get; set; } = "common";
}

public sealed class CosmeticCatalog
{
    public int version { get; set; }
    public CatalogItem[] items { get; set; } = Array.Empty<CatalogItem>();

    public static CosmeticCatalog Load(string path)
    {
        var cat = JsonSerializer.Deserialize<CosmeticCatalog>(File.ReadAllText(path),
                      new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                  ?? throw new InvalidDataException("Empty cosmetic catalog: " + path);
        cat.Validate();
        return cat;
    }

    /// <summary>Refuses a broken catalog at startup instead of selling something unbuyable or free.</summary>
    public void Validate()
    {
        var seen = new HashSet<string>();
        foreach (CatalogItem i in items)
        {
            if (string.IsNullOrWhiteSpace(i.id) || i.id.Length > 64 || !seen.Add(i.id)) throw new InvalidDataException("Bad/duplicate item id: " + i.id);
            if (Array.IndexOf(ItemKinds.All, i.kind) < 0) throw new InvalidDataException(i.id + ": unknown kind " + i.kind);
            if (i.currency is not ("gold" or "diamonds")) throw new InvalidDataException(i.id + ": currency must be gold or diamonds");
            if (i.price <= 0) throw new InvalidDataException(i.id + ": price must be positive");
            if (i.rarity is not ("common" or "rare" or "epic" or "legendary")) throw new InvalidDataException(i.id + ": rarity");
            if (i.classes.Length == 0 || i.classes.Any(c => c is < 0 or > 3)) throw new InvalidDataException(i.id + ": classes");
            if (i.kind == ItemKinds.Cosmetic && !OutfitSlots.IsPiece(i.slot)) throw new InvalidDataException(i.id + ": slot " + i.slot);
            if (i.kind == ItemKinds.CosmeticSet && (i.slot != OutfitSlots.Set || i.pieces.Length == 0)) throw new InvalidDataException(i.id + ": set needs slot 'set' and pieces");
            // Policy (owner, 2026-11-07): single pieces are Gold, premium full sets are Diamonds.
            if (i.kind == ItemKinds.Cosmetic && i.currency != "gold") throw new InvalidDataException(i.id + ": single cosmetic pieces are sold for Gold");
            if (i.kind == ItemKinds.CosmeticSet && i.currency != "diamonds") throw new InvalidDataException(i.id + ": premium sets are sold for Diamonds");
        }
    }

    public CatalogItem? Find(string? id) => id == null ? null : items.FirstOrDefault(i => i.id == id);
}

// ------------------------------------------------------------------ DTOs (field names = Unity JsonUtility models)
public sealed class OutfitSlotDto { public string slot { get; set; } = ""; public string itemId { get; set; } = ""; }

public sealed class OutfitDto
{
    public string set { get; set; } = "";
    public OutfitSlotDto[] slots { get; set; } = Array.Empty<OutfitSlotDto>();
}

public sealed class ShopItemDto
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public string slot { get; set; } = "";
    public string kind { get; set; } = "";
    public string currency { get; set; } = "";
    public long price { get; set; }
    public int[] classes { get; set; } = Array.Empty<int>();
    public bool owned { get; set; }
    public bool equipped { get; set; }
    public bool usableByCharacter { get; set; }
    public string rarity { get; set; } = "common";
}

public sealed class WardrobeDto
{
    public int catalogVersion { get; set; }
    public string characterId { get; set; } = "";
    public int heroClass { get; set; }
    public long gold { get; set; }
    public long diamonds { get; set; }
    public long serverRevision { get; set; }
    public ShopItemDto[] items { get; set; } = Array.Empty<ShopItemDto>();
    public string[] owned { get; set; } = Array.Empty<string>();
    public OutfitDto outfit { get; set; } = new();
    public string purchaseId { get; set; } = "";
    public bool replayed { get; set; }
}

public sealed class PurchaseRequestDto { public string? itemId { get; set; } public string? requestId { get; set; } public bool equip { get; set; } }

/// <summary>
/// Fashion shop + wardrobe. Every purchase is one IMMEDIATE transaction: check character ownership, catalog
/// (listed, class, currency, server price), not already owned, balance → spend through <see cref="Wallet"/> →
/// grant ownership → write purchase history (→ optionally equip). A client request id makes the whole purchase
/// idempotent: replays (double-click, retry after a timeout) return the original result and never charge twice.
/// Gold cosmetics belong to the character (Gold is per character); Diamond sets belong to the account.
/// </summary>
public sealed class CosmeticShop
{
    private readonly Database db;
    public CosmeticCatalog Catalog { get; }

    public CosmeticShop(Database db, CosmeticCatalog catalog)
    {
        this.db = db;
        Catalog = catalog;
    }

    public WardrobeDto Wardrobe(string accountId, string characterId)
    {
        using var c = db.Open();
        return Snapshot(c, null, accountId, Character(c, null, accountId, characterId));
    }

    public WardrobeDto Purchase(string accountId, string characterId, PurchaseRequestDto? req)
    {
        string requestId = (req?.requestId ?? "").Trim();
        if (requestId.Length is < 8 or > 64 || requestId.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '-')))
            throw new GameError("request_id", "Thiếu mã yêu cầu mua hợp lệ.");
        return db.InTransaction((c, tx) =>
        {
            CharRow ch = Character(c, tx, accountId, characterId);
            // Replay of the same click: same answer, nothing charged again.
            using (var q = Database.Command(c, tx, "SELECT id, item_id, character_id FROM shop_purchases WHERE account_id=$a AND request_id=$r",
                       ("$a", accountId), ("$r", requestId)))
            using (var r = q.ExecuteReader())
                if (r.Read())
                {
                    if (r.GetString(1) != req!.itemId || r.GetString(2) != characterId)
                        throw new GameError("request_reused", "Mã yêu cầu đã dùng cho giao dịch khác.", 409);
                    WardrobeDto again = Snapshot(c, tx, accountId, ch);
                    again.purchaseId = r.GetString(0);
                    again.replayed = true;
                    return again;
                }

            CatalogItem item = Catalog.Find(req!.itemId) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
            if (!item.listed) throw new GameError("item_unavailable", "Vật phẩm này chưa được mở bán.", 409);
            if (item.kind is not (ItemKinds.Cosmetic or ItemKinds.CosmeticSet)) throw new GameError("item_unavailable", "Vật phẩm này không bán ở tiệm thời trang.", 409);
            if (Array.IndexOf(item.classes, ch.HeroClass) < 0) throw new GameError("class_restricted", "Nghề của bạn không dùng được vật phẩm này.", 409);
            if (Owns(c, tx, accountId, characterId, item.id)) throw new GameError("already_owned", "Bạn đã sở hữu vật phẩm này.", 409);

            string purchaseId = Crypto.NewId("pur");
            string key = $"shop:{accountId}:{requestId}";
            if (item.currency == "gold") Wallet.Gold(c, tx, accountId, characterId, -item.price, "shop:" + item.id, key);
            else Wallet.Diamonds(c, tx, accountId, -item.price, "shop:" + item.id, key);

            long now = Database.NowMs();
            Database.Exec(c, tx, """
                INSERT INTO owned_items(id, account_id, character_id, item_id, kind, quantity, source, acquired_ms)
                VALUES($id,$a,$c,$i,$k,1,$s,$t)
                """, ("$id", Crypto.NewId("own")), ("$a", accountId), ("$c", item.currency == "gold" ? characterId : null),
                ("$i", item.id), ("$k", item.kind), ("$s", "purchase:" + purchaseId), ("$t", now));
            Database.Exec(c, tx, """
                INSERT INTO shop_purchases(id, account_id, character_id, item_id, currency, price, request_id, catalog_version, created_ms)
                VALUES($id,$a,$c,$i,$cur,$p,$r,$v,$t)
                """, ("$id", purchaseId), ("$a", accountId), ("$c", characterId), ("$i", item.id), ("$cur", item.currency),
                ("$p", item.price), ("$r", requestId), ("$v", Catalog.version), ("$t", now));
            AccountService.Audit(c, tx, accountId, "shop_purchase", $"{characterId} {item.id} -{item.price}{item.currency}");

            if (req.equip)
            {
                OutfitDto outfit = ReadOutfit(ch.AppearanceJson);
                WriteOutfit(c, tx, characterId, With(outfit, item));
            }
            WardrobeDto result = Snapshot(c, tx, accountId, Character(c, tx, accountId, characterId));
            result.purchaseId = purchaseId;
            return result;
        });
    }

    /// <summary>Replaces the worn outfit. Every item must be owned, fit its slot and the character's class.</summary>
    public WardrobeDto Equip(string accountId, string characterId, OutfitDto? wanted)
    {
        wanted ??= new OutfitDto();
        return db.InTransaction((c, tx) =>
        {
            CharRow ch = Character(c, tx, accountId, characterId);
            var clean = new OutfitDto { set = "" };
            var slots = new List<OutfitSlotDto>();
            foreach (OutfitSlotDto s in wanted.slots ?? Array.Empty<OutfitSlotDto>())
            {
                if (string.IsNullOrEmpty(s.itemId)) continue;
                if (!OutfitSlots.IsPiece(s.slot)) throw new GameError("slot_invalid", "Ô trang phục không hợp lệ.");
                if (slots.Any(x => x.slot == s.slot)) throw new GameError("slot_duplicate", "Mỗi ô chỉ mặc được một món.");
                CatalogItem item = Catalog.Find(s.itemId) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
                if (item.kind != ItemKinds.Cosmetic || item.slot != s.slot) throw new GameError("slot_mismatch", "Vật phẩm không thuộc ô này.");
                Check(c, tx, accountId, ch, item);
                slots.Add(new OutfitSlotDto { slot = s.slot, itemId = item.id });
            }
            if (!string.IsNullOrEmpty(wanted.set))
            {
                CatalogItem set = Catalog.Find(wanted.set) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
                if (set.kind != ItemKinds.CosmeticSet) throw new GameError("slot_mismatch", "Đây không phải skin nguyên bộ.");
                Check(c, tx, accountId, ch, set);
                clean.set = set.id;
            }
            clean.slots = slots.OrderBy(s => Array.IndexOf(OutfitSlots.Pieces, s.slot)).ToArray();
            WriteOutfit(c, tx, characterId, clean);
            AccountService.Audit(c, tx, accountId, "outfit_equip", characterId + " " + JsonSerializer.Serialize(clean));
            return Snapshot(c, tx, accountId, Character(c, tx, accountId, characterId));
        });
    }

    private void Check(SqliteConnection c, SqliteTransaction tx, string accountId, CharRow ch, CatalogItem item)
    {
        if (!Owns(c, tx, accountId, ch.Id, item.id)) throw new GameError("not_owned", "Bạn chưa sở hữu " + item.name + ".", 409);
        if (Array.IndexOf(item.classes, ch.HeroClass) < 0) throw new GameError("class_restricted", item.name + " không dành cho nghề này.", 409);
    }

    private static OutfitDto With(OutfitDto outfit, CatalogItem item)
    {
        if (item.kind == ItemKinds.CosmeticSet) return new OutfitDto { set = item.id, slots = outfit.slots };
        var slots = outfit.slots.Where(s => s.slot != item.slot).Append(new OutfitSlotDto { slot = item.slot, itemId = item.id })
            .OrderBy(s => Array.IndexOf(OutfitSlots.Pieces, s.slot)).ToArray();
        return new OutfitDto { set = outfit.set, slots = slots };
    }

    private static bool Owns(SqliteConnection c, SqliteTransaction? tx, string accountId, string characterId, string itemId) =>
        Database.Scalar<long>(c, tx, """
            SELECT COUNT(*) FROM owned_items WHERE account_id=$a AND item_id=$i AND (character_id IS NULL OR character_id=$c)
            """, ("$a", accountId), ("$i", itemId), ("$c", characterId)) > 0;

    private sealed record CharRow(string Id, int HeroClass, string AppearanceJson);

    private static CharRow Character(SqliteConnection c, SqliteTransaction? tx, string accountId, string? characterId)
    {
        using var cmd = Database.Command(c, tx, "SELECT id, hero_class, appearance_json FROM characters WHERE id=$c AND account_id=$a",
            ("$c", characterId ?? ""), ("$a", accountId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new GameError("character_not_found", "Không tìm thấy nhân vật thuộc tài khoản này.", 404);
        return new CharRow(r.GetString(0), r.GetInt32(1), r.GetString(2));
    }

    public static OutfitDto ReadOutfit(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return new OutfitDto();
        try { return JsonSerializer.Deserialize<OutfitDto>(json) ?? new OutfitDto(); }
        catch (JsonException) { return new OutfitDto(); }
    }

    private static void WriteOutfit(SqliteConnection c, SqliteTransaction tx, string characterId, OutfitDto outfit) =>
        Database.Exec(c, tx, "UPDATE characters SET appearance_json=$j, revision=revision+1, saved_ms=$t WHERE id=$c",
            ("$j", JsonSerializer.Serialize(outfit)), ("$t", Database.NowMs()), ("$c", characterId));

    private WardrobeDto Snapshot(SqliteConnection c, SqliteTransaction? tx, string accountId, CharRow ch)
    {
        var owned = new HashSet<string>();
        using (var q = Database.Command(c, tx, "SELECT item_id FROM owned_items WHERE account_id=$a AND (character_id IS NULL OR character_id=$c)",
                   ("$a", accountId), ("$c", ch.Id)))
        using (var r = q.ExecuteReader())
            while (r.Read()) owned.Add(r.GetString(0));
        OutfitDto outfit = ReadOutfit(ch.AppearanceJson);
        var worn = new HashSet<string>(outfit.slots.Select(s => s.itemId));
        if (!string.IsNullOrEmpty(outfit.set)) worn.Add(outfit.set);
        long gold = Database.Scalar<long>(c, tx, "SELECT gold FROM characters WHERE id=$c", ("$c", ch.Id));
        long diamonds = Database.Scalar<long>(c, tx, "SELECT diamonds FROM accounts WHERE id=$a", ("$a", accountId));
        long rev = Database.Scalar<long>(c, tx, "SELECT revision FROM characters WHERE id=$c", ("$c", ch.Id));
        return new WardrobeDto
        {
            catalogVersion = Catalog.version, characterId = ch.Id, heroClass = ch.HeroClass, gold = gold, diamonds = diamonds, serverRevision = rev,
            owned = owned.OrderBy(x => x).ToArray(), outfit = outfit,
            items = Catalog.items.Where(i => i.listed || owned.Contains(i.id)).Select(i => new ShopItemDto
            {
                id = i.id, name = i.name, description = i.description, slot = i.slot, kind = i.kind, currency = i.currency,
                price = i.price, classes = i.classes, owned = owned.Contains(i.id), equipped = worn.Contains(i.id),
                usableByCharacter = Array.IndexOf(i.classes, ch.HeroClass) >= 0, rarity = i.rarity,
            }).ToArray(),
        };
    }
}
