using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

public sealed class NpcShopItemDto
{
    public string itemId { get; set; } = "";
    public string name { get; set; } = "";
    public string kind { get; set; } = "";
    public string icon { get; set; } = "";
    public string desc { get; set; } = "";
    public long priceGold { get; set; }
    public string destMap { get; set; } = "";
    public string destName { get; set; } = "";
    public int minLevel { get; set; }
}
public sealed class NpcShopTabDto { public string id { get; set; } = ""; public string name { get; set; } = ""; public NpcShopItemDto[] items { get; set; } = Array.Empty<NpcShopItemDto>(); }
public sealed class NpcShopDto { public string id { get; set; } = ""; public string npc { get; set; } = ""; public int maxPerPurchase { get; set; } = 99; public NpcShopTabDto[] tabs { get; set; } = Array.Empty<NpcShopTabDto>(); }
public sealed class BuyRequestDto { public string? shopId { get; set; } public string? itemId { get; set; } public int qty { get; set; } public string? requestId { get; set; } }
public sealed class BuyResultDto { public string itemId { get; set; } = ""; public int qty { get; set; } public long totalGold { get; set; } public long gold { get; set; } public bool replayed { get; set; } public InventoryDto inventory { get; set; } = new(); }
public sealed class TicketRequestDto { public int slot { get; set; } }
public sealed class TicketResultDto { public CharacterDto character { get; set; } = new(); public InventoryDto inventory { get; set; } = new(); }

/// <summary>
/// NPC shops (Dược Sư Mộc Linh: Dược Phẩm + Vé Dịch Chuyển) and ticket travel.
/// Buying: the item must be on that shop's list, 1–99 per purchase, the hero must be on the shop's map; the bag space is
/// checked BEFORE any Gold moves; Gold and items change in one transaction (ledger key "shop:{requestId}", so a
/// retried / double-clicked purchase never charges or delivers twice). Prices are data (items.json, PROPOSAL).
/// Ticket travel: the ticket must be in the hero's bag, lead to a map the server really has, the level must allow it;
/// the ticket is removed and the character moved in the same transaction — any failure leaves both unchanged.
/// </summary>
public sealed class ShopService
{
    public const int MaxPerPurchase = 99;
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<long> now;
    public Action<string, string, string, int>? QuestEvent { get; set; } // (accountId, characterId, event, amount)

    public ShopService(Database db, InventoryService inv, Func<long>? clock = null)
    {
        this.db = db;
        this.inv = inv;
        now = clock ?? Database.NowMs;
    }

    private ItemConfig Cfg => inv.Config;

    public NpcShopDto Get(string shopId)
    {
        ShopDef s = Cfg.shops.FirstOrDefault(x => x.id == shopId) ?? throw new GameError("shop_unknown", "Cửa hàng không tồn tại.", 404);
        return new NpcShopDto
        {
            id = s.id, npc = s.npc, maxPerPurchase = MaxPerPurchase,
            tabs = s.tabs.Select(t => new NpcShopTabDto
            {
                id = t.id, name = t.name,
                items = t.items.Select(id => Cfg.Get(id)!).Select(d => new NpcShopItemDto
                {
                    itemId = d.id, name = d.name, kind = d.kind, icon = d.icon, desc = d.desc, priceGold = d.priceGold, destMap = d.destMap,
                    destName = CharacterService.Maps.TryGetValue(d.destMap, out var m) ? m.Name : "", minLevel = d.minLevel,
                }).ToArray(),
            }).ToArray(),
        };
    }

    private static (string mapId, int level) CharacterRow(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId)
    {
        using var cmd = Database.Command(c, tx, "SELECT map_id, level FROM characters WHERE id=$c AND account_id=$a", ("$c", characterId), ("$a", accountId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
        return (r.GetString(0), r.GetInt32(1));
    }

    public BuyResultDto Buy(string accountId, string characterId, BuyRequestDto? req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.requestId) || req.requestId.Length > 80) throw new GameError("bad_request", "Thiếu mã giao dịch.");
        if (req.qty < 1 || req.qty > MaxPerPurchase) throw new GameError("bad_qty", $"Mỗi lần mua từ 1 đến {MaxPerPurchase} vật phẩm.");
        ShopDef s = Cfg.shops.FirstOrDefault(x => x.id == req.shopId) ?? throw new GameError("shop_unknown", "Cửa hàng không tồn tại.", 404);
        if (!s.tabs.Any(t => t.items.Contains(req.itemId))) throw new GameError("not_sold_here", "Cửa hàng này không bán vật phẩm đó.", 409);
        ItemDef d = Cfg.Get(req.itemId!)!;
        long total = d.priceGold * req.qty;
        BuyResultDto result = db.InTransaction((c, tx) =>
        {
            var (mapId, _) = CharacterRow(c, tx, accountId, characterId);
            if (mapId != s.mapId) throw new GameError("shop_far", "Bạn không ở gần cửa hàng này.", 409);
            if (!inv.Fits(c, tx, characterId, d.id, req.qty)) throw new GameError("bag_full", "Túi đồ không đủ chỗ cho số lượng này.", 409); // before any Gold moves
            LedgerResult r = Wallet.Gold(c, tx, accountId, characterId, -total, "shop:" + s.id + ":" + d.id, "shop:" + characterId + ":" + req.requestId);
            if (!r.Applied) // the same request again (retry / double click): nothing charged, nothing delivered again
                return new BuyResultDto { itemId = d.id, qty = 0, totalGold = 0, gold = r.Balance, replayed = true, inventory = inv.SnapshotFor(c, tx, accountId, characterId) };
            inv.Add(c, tx, characterId, d.id, req.qty);
            AccountService.Audit(c, tx, accountId, "shop_buy", $"{characterId} {s.id} {d.id} x{req.qty} -{total}gold");
            return new BuyResultDto { itemId = d.id, qty = req.qty, totalGold = total, gold = r.Balance, inventory = inv.SnapshotFor(c, tx, accountId, characterId) };
        });
        if (!result.replayed) QuestEvent?.Invoke(accountId, characterId, "buy:" + d.kind, req.qty);
        return result;
    }

    /// <summary>Map change hook (characterId, map, x, y): FarmService.Arrived seeds the new map's position there.</summary>
    public Action<string, string, float, float>? Arrived { get; set; }

    /// <summary>Uses a teleport ticket from the bag: removes exactly that ticket and moves the hero, atomically.</summary>
    public TicketResultDto UseTicket(string accountId, string characterId, TicketRequestDto? req)
    {
        if (req == null) throw new GameError("bad_request", "Thiếu dữ liệu.");
        TicketResultDto res = db.InTransaction((c, tx) =>
        {
            var (mapId, level) = CharacterRow(c, tx, accountId, characterId);
            var cell = inv.Cell(c, tx, characterId, req.slot) ?? throw new GameError("slot_empty", "Ô này không có vật phẩm.", 409);
            ItemDef d = Cfg.Get(cell.item) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
            if (d.kind != "ticket") throw new GameError("not_ticket", "Đây không phải vé dịch chuyển.", 409);
            if (!CharacterService.Maps.ContainsKey(d.destMap)) throw new GameError("map_unknown", "Bản đồ đích chưa tồn tại.", 409);
            if (level < d.minLevel) throw new GameError("level_too_low", $"Cần cấp {d.minLevel} để tới nơi này.", 409);
            if (mapId == d.destMap) throw new GameError("already_there", "Bạn đang ở bản đồ này rồi.", 409);
            inv.RemoveFromSlot(c, tx, characterId, req.slot, d.id, 1);
            Database.Exec(c, tx, "UPDATE characters SET map_id=$m, pos_x=$x, pos_y=$y, has_position=1, revision=revision+1, saved_ms=$t WHERE id=$id",
                ("$m", d.destMap), ("$x", d.destX), ("$y", d.destY), ("$t", now()), ("$id", characterId));
            AccountService.Audit(c, tx, accountId, "ticket_travel", $"{characterId} {mapId}->{d.destMap} {d.id}");
            return new TicketResultDto
            {
                character = CharacterService.Query(c, tx, "c.id=$id", ("$id", characterId)).Single(),
                inventory = inv.SnapshotFor(c, tx, accountId, characterId),
            };
        });
        QuestEvent?.Invoke(accountId, characterId, "travel:" + res.character.lastMapId, 1);
        Arrived?.Invoke(characterId, res.character.lastMapId, res.character.posX, res.character.posY);
        return res;
    }
}
