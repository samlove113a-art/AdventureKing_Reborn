using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

/// <summary>Cường Hóa Trang Bị rules. All numbers are a DRAFT balance proposal pending owner approval; they live here (one table) so they can be tuned in one place.</summary>
public static class EnhanceRules
{
    public const int MaxLevel = 15;
    /// <summary>Up to this level a failure costs only the materials (nothing is lost).</summary>
    public const int SafeLevel = 5;
    /// <summary>From this level on, an unprotected failure can also shatter the item.</summary>
    public const int BreakFromLevel = 10;
    public const int BreakPercent = 25;
    public const string StoneId = "enhance_stone", CharmId = "protect_charm";
    /// <summary>On top of +1 per level, gear gains this share of its base attack / defence per level.</summary>
    public const double BonusPerLevel = 0.12;

    // index = current level (0..14) -> the attempt to reach level + 1
    private static readonly int[] Success = { 100, 95, 90, 85, 80, 70, 65, 60, 55, 50, 40, 35, 30, 25, 20 };
    private static readonly int[] Stones = { 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 6, 7, 8, 9, 10 };

    /// <summary>The whole table for the client (it only previews; the server decides).</summary>
    public static EnhanceRulesDto Describe() => new()
    {
        maxLevel = MaxLevel, safeLevel = SafeLevel, breakFromLevel = BreakFromLevel, breakPercent = BreakPercent,
        success = Success.ToArray(), stones = Stones.ToArray(), gold = Enumerable.Range(0, MaxLevel).Select(GoldFor).ToArray(),
        stoneItemId = StoneId, charmItemId = CharmId,
    };

    public static int SuccessPercent(int level) => Success[Math.Clamp(level, 0, MaxLevel - 1)];
    public static int StonesFor(int level) => Stones[Math.Clamp(level, 0, MaxLevel - 1)];
    public static long GoldFor(int level) => 100L * (level + 1) * (level + 1);

    /// <summary>Base stat after enhancement: base + 1 per level + 12 % of base per level (rounded), so every single level is a visible gain even on small bases. A stat of 0 stays 0.</summary>
    public static int Scaled(int baseValue, int level) =>
        baseValue <= 0 ? baseValue : baseValue + level + (int)Math.Round(baseValue * BonusPerLevel * level);
}

public sealed class EnhanceRulesDto
{
    public int maxLevel { get; set; }
    public int safeLevel { get; set; }
    public int breakFromLevel { get; set; }
    public int breakPercent { get; set; }
    public int[] success { get; set; } = Array.Empty<int>();
    public int[] stones { get; set; } = Array.Empty<int>();
    public long[] gold { get; set; } = Array.Empty<long>();
    public string stoneItemId { get; set; } = "";
    public string charmItemId { get; set; } = "";
}

public sealed class EnhanceRequestDto
{
    /// <summary>"bag" (use <see cref="slot"/>) or "equipped" (use <see cref="equipSlot"/>).</summary>
    public string where { get; set; } = "bag";
    public int slot { get; set; }
    public string? equipSlot { get; set; }
    /// <summary>Put a Bùa Bảo Vệ in the protection slot: a failure at +6 or above then keeps the level and the item (the charm is used up).
    /// A charm is only consumed when it actually protects something.</summary>
    public bool useCharm { get; set; }
}

public sealed class EnhanceResultDto
{
    /// <summary>"success" | "fail" (nothing lost) | "protected" (failed, charm used up, level and item kept) | "drop" (level −1) | "broken" (item destroyed).</summary>
    public string outcome { get; set; } = "";
    public string itemId { get; set; } = "";
    public int levelBefore { get; set; }
    public int levelAfter { get; set; }
    public int successPercent { get; set; }
    public int stonesUsed { get; set; }
    public long goldSpent { get; set; }
    public bool charmUsed { get; set; }
    public long gold { get; set; }
    public InventoryDto inventory { get; set; } = new();
}

/// <summary>
/// Enhancing gear +1 … +15 with Đá Cường Hóa and Gold. Server-authoritative: the materials and Gold are taken and the roll is made in ONE
/// transaction on the server's own random source, so the client cannot pick the outcome, retry for free, or keep the stone after a refusal.
/// The level belongs to the piece of gear (bag cell or equipment slot) and moves with it. Failure rules: +0…+5 nothing lost; +6…+9 the level
/// drops by one; +10…+14 the level drops by one and the item shatters 25 % of the time; a Bùa Bảo Vệ turns any of those into "protected".
/// </summary>
public sealed class EnhanceService
{
    private readonly Database db;
    private readonly InventoryService inv;
    private readonly Func<double> roll;       // [0,1): injectable for tests
    private readonly object gate = new();

    public EnhanceService(Database db, InventoryService inv, Func<double>? random = null)
    {
        this.db = db; this.inv = inv;
        var rng = new Random();
        roll = random ?? (() => { lock (rng) return rng.NextDouble(); });
    }

    public EnhanceResultDto Enhance(string accountId, string characterId, EnhanceRequestDto? req)
    {
        if (req == null) throw new GameError("bad_request", "Thiếu dữ liệu.");
        bool worn = req.where == "equipped";
        if (!worn && req.where != "bag") throw new GameError("bad_request", "Vị trí trang bị không hợp lệ.");
        lock (gate)   // one attempt at a time per server: no interleaving between the stone count and the roll
            return db.InTransaction((c, tx) =>
            {
                int cls = inv.OwnerClass(c, tx, accountId, characterId);
                string? itemId; int level;
                if (worn) (itemId, level) = Read(c, tx, "SELECT item_id, enhance FROM equipped WHERE character_id=$c AND equip_slot=$k", characterId, req.equipSlot ?? "");
                else (itemId, level) = Read(c, tx, "SELECT item_id, enhance FROM bag_slots WHERE character_id=$c AND slot_index=$k", characterId, req.slot);
                if (itemId == null) throw new GameError("slot_empty", "Không có trang bị ở vị trí này.", 409);
                ItemDef d = inv.Config.Get(itemId) ?? throw new GameError("item_unknown", "Vật phẩm không tồn tại.", 404);
                if (d.kind != "equipment") throw new GameError("not_equipment", "Chỉ cường hóa được trang bị.", 409);
                if (level >= EnhanceRules.MaxLevel) throw new GameError("max_level", "Trang bị đã đạt +15.", 409);

                int stones = EnhanceRules.StonesFor(level), success = EnhanceRules.SuccessPercent(level);
                long gold = EnhanceRules.GoldFor(level);
                var cells = inv.Cells(c, tx, characterId);
                if (cells.Where(kv => kv.Value.item == EnhanceRules.StoneId).Sum(kv => (long)kv.Value.qty) < stones)
                    throw new GameError("no_stone", $"Cần {stones} Đá Cường Hóa.", 409);
                int charmCell = req.useCharm ? cells.Where(kv => kv.Value.item == EnhanceRules.CharmId).Select(kv => kv.Key).DefaultIfEmpty(-1).Min() : -1;
                if (req.useCharm && charmCell < 0) throw new GameError("no_charm", "Không có Bùa Bảo Vệ trong túi.", 409);

                // pay first (an unaffordable attempt is refused before anything is consumed; the transaction rolls everything back on any error)
                LedgerResult paid = Wallet.Gold(c, tx, accountId, characterId, -gold, "enhance", $"enhance:{characterId}:{Crypto.NewId("enh")}");
                int left = stones;
                foreach (var (slot, (item, qty)) in cells.Where(kv => kv.Value.item == EnhanceRules.StoneId).OrderBy(kv => kv.Key).ToList())
                {
                    if (left == 0) break;
                    int take = Math.Min(left, qty);
                    inv.RemoveFromSlot(c, tx, characterId, slot, item, take);
                    left -= take;
                }

                string outcome; int after = level; bool charmUsed = false;
                if (roll() * 100.0 < success) { outcome = "success"; after = level + 1; }
                else if (level < EnhanceRules.SafeLevel) outcome = "fail";
                else if (charmCell >= 0) { outcome = "protected"; charmUsed = true; inv.RemoveFromSlot(c, tx, characterId, charmCell, EnhanceRules.CharmId, 1); }
                else if (level >= EnhanceRules.BreakFromLevel && roll() * 100.0 < EnhanceRules.BreakPercent) { outcome = "broken"; after = 0; }
                else { outcome = "drop"; after = level - 1; }

                if (outcome == "broken")
                {
                    if (worn) Database.Exec(c, tx, "DELETE FROM equipped WHERE character_id=$c AND equip_slot=$k", ("$c", characterId), ("$k", req.equipSlot!));
                    else Database.Exec(c, tx, "DELETE FROM bag_slots WHERE character_id=$c AND slot_index=$k", ("$c", characterId), ("$k", req.slot));
                }
                else if (after != level)
                {
                    if (worn) Database.Exec(c, tx, "UPDATE equipped SET enhance=$e WHERE character_id=$c AND equip_slot=$k", ("$e", after), ("$c", characterId), ("$k", req.equipSlot!));
                    else Database.Exec(c, tx, "UPDATE bag_slots SET enhance=$e WHERE character_id=$c AND slot_index=$k", ("$e", after), ("$c", characterId), ("$k", req.slot));
                }
                AccountService.Audit(c, tx, accountId, "enhance", $"{characterId} {itemId} +{level}->+{after} {outcome} -{stones}stone -{gold}gold{(charmUsed ? " -charm" : "")}");
                return new EnhanceResultDto
                {
                    outcome = outcome, itemId = itemId, levelBefore = level, levelAfter = after, successPercent = success, stonesUsed = stones, goldSpent = gold,
                    charmUsed = charmUsed, gold = paid.Balance, inventory = inv.SnapshotFor(c, tx, accountId, characterId),
                };
            });
    }

    private static (string? item, int level) Read(SqliteConnection c, SqliteTransaction tx, string sql, string characterId, object key)
    {
        using var cmd = Database.Command(c, tx, sql, ("$c", characterId), ("$k", key));
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetInt32(1)) : (null, 0);
    }
}
