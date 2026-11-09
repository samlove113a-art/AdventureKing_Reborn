using AdventureKing.Server.Data;
using AdventureKing.Server.Security;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

// Field names match the Unity client's JsonUtility models (AdventurePlayerSaveContracts.cs).
public sealed class CharacterDto
{
    public string characterId { get; set; } = "";
    public string characterName { get; set; } = "";
    public int slotIndex { get; set; }
    public int heroClass { get; set; }
    public int level { get; set; }
    public long experience { get; set; }
    public int unspentSkillPoints { get; set; }
    public long gold { get; set; }
    public long diamonds { get; set; }
    public string lastMapId { get; set; } = "";
    public string lastSafeCheckpointId { get; set; } = "";
    public float posX { get; set; }
    public float posY { get; set; }
    public bool hasPosition { get; set; }
    public int tutorialStep { get; set; }
    public bool tutorialCompleted { get; set; }
    public long serverRevision { get; set; }
    public long lastSavedUnixMilliseconds { get; set; }
    /// <summary>Worn outfit (server-validated wardrobe loadout).</summary>
    public OutfitDto outfit { get; set; } = new();
}

public sealed class ProfileDto
{
    public string accountId { get; set; } = "";
    public CharacterDto[] characters { get; set; } = Array.Empty<CharacterDto>();
}

/// <summary>Character slots (max 4 per account), creation with server name rules, entry and tutorial progress.</summary>
public sealed class CharacterService
{
    public const int MaxSlots = 4;
    private readonly Database db;
    private readonly NameRules names;

    /// <summary>
    /// One-time welcome Gold per character (server rule, ledger key starter_gold:&lt;id&gt; so it can never be paid twice).
    /// Amount approved by the owner 2026-11-09 (800); configured by AdventureKing:StarterGold.
    /// </summary>
    public long StarterGold { get; }

    public CharacterService(Database db, NameRules names, long starterGold = 0)
    {
        this.db = db;
        this.names = names;
        StarterGold = Math.Max(0, starterGold);
    }

    private void GrantStarterGold(SqliteConnection c, SqliteTransaction tx, string accountId, string characterId)
    {
        if (StarterGold > 0) Wallet.Gold(c, tx, accountId, characterId, StarterGold, "starter_gold", "starter_gold:" + characterId);
    }

    public ProfileDto LoadProfile(string accountId)
    {
        using var c = db.Open();
        return new ProfileDto { accountId = accountId, characters = Query(c, null, "c.account_id=$a ORDER BY c.slot_index", ("$a", accountId)).ToArray() };
    }

    public CharacterDto Create(string accountId, string? displayName, int heroClass, int slotIndex)
    {
        if (heroClass is < 0 or > 3) throw new GameError("class_invalid", "Nghề không hợp lệ.");
        if (slotIndex is < 0 or >= MaxSlots) throw new GameError("slot_invalid", "Ô nhân vật không hợp lệ.");
        NameCheck check = names.Check(displayName);
        if (!check.Ok) throw new GameError("name_" + check.Code, check.Message);

        string id = Crypto.NewId("chr");
        long now = Database.NowMs();
        return db.InTransaction((c, tx) =>
        {
            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM characters WHERE account_id=$a AND slot_index=$s",
                    ("$a", accountId), ("$s", slotIndex)) > 0)
                throw new GameError("slot_taken", "Ô này đã có nhân vật.", 409);
            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM characters WHERE account_id=$a", ("$a", accountId)) >= MaxSlots)
                throw new GameError("slots_full", "Tài khoản đã đủ 4 nhân vật.", 409);
            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM characters WHERE name_key=$k", ("$k", check.UniqueKey)) > 0)
                throw new GameError("name_taken", "Tên này đã có người sử dụng (hoặc quá giống một tên đã có).", 409);
            try
            {
                Database.Exec(c, tx, """
                    INSERT INTO characters(id, account_id, slot_index, name, name_key, hero_class, created_ms, saved_ms)
                    VALUES($id,$a,$s,$n,$k,$h,$t,$t)
                    """, ("$id", id), ("$a", accountId), ("$s", slotIndex), ("$n", check.Normalized),
                    ("$k", check.UniqueKey), ("$h", heroClass), ("$t", now));
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE race
            {
                throw new GameError("name_taken", "Tên này đã có người sử dụng.", 409);
            }
            AccountService.Audit(c, tx, accountId, "character_create", id + " " + check.Normalized);
            GrantStarterGold(c, tx, accountId, id);
            return Query(c, tx, "c.id=$id", ("$id", id)).Single();
        });
    }

    /// <summary>Re-authorises ownership on every entry and returns the authoritative snapshot.</summary>
    public CharacterDto Enter(string accountId, string? characterId)
    {
        return db.InTransaction((c, tx) =>
        {
            CharacterDto ch = Query(c, tx, "c.id=$id AND c.account_id=$a", ("$id", characterId ?? ""), ("$a", accountId)).SingleOrDefault()
                              ?? throw new GameError("character_not_found", "Không tìm thấy nhân vật thuộc tài khoản này.", 404);
            // Characters created before the welcome Gold existed receive it once (idempotent ledger key).
            GrantStarterGold(c, tx, accountId, ch.characterId);
            return Query(c, tx, "c.id=$id", ("$id", ch.characterId)).Single();
        });
    }

    // ------------------------------------------------------------- world position
    public sealed record WorldMap(string Id, string Name, float MinX, float MaxX, float MinY, float MaxY);

    /// <summary>Maps the server knows. A client can only be saved inside one of these, inside its bounds.</summary>
    public static readonly IReadOnlyDictionary<string, WorldMap> Maps = new Dictionary<string, WorldMap>
    {
        ["village_wind_bell"] = new("village_wind_bell", "Thị trấn Chuông Gió", -70f, 100f, -8f, 40f),
        ["meadow_breeze"] = new("meadow_breeze", "Đồng Cỏ Gió", -50f, 90f, -8f, 30f),
    };

    public const string StartMap = "village_wind_bell";

    public sealed record Portal(string Id, string FromMap, float X, float Y, string ToMap, float ArrivalX, float ArrivalY);

    /// <summary>
    /// The portal graph is server data: a client can only change map by using a portal that exists on its
    /// current map and that it stands next to. Coordinates must match the scenes (Unity builders use the same values).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Portal> Portals = new Dictionary<string, Portal>
    {
        ["hub_east_meadow"] = new("hub_east_meadow", "village_wind_bell", 66f, 0f, "meadow_breeze", -36f, 0.9f),
        ["meadow_west_hub"] = new("meadow_west_hub", "meadow_breeze", -40f, 0f, "village_wind_bell", 62f, 0.9f),
    };

    public const float PortalReach = 3.5f;
    /// <summary>Minimum time between two portal uses by one character (blocks double-submits and portal spam).</summary>
    public static readonly TimeSpan TravelCooldown = TimeSpan.FromSeconds(1.5);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> lastTravelMs = new();
    public Func<long> Clock { get; set; } = Database.NowMs;

    /// <summary>Uses a portal: validated against the character's saved map and its current position.</summary>
    public CharacterDto Travel(string accountId, string characterId, string? portalId, float x, float y)
    {
        if (portalId == null || !Portals.TryGetValue(portalId, out Portal? p))
            throw new GameError("portal_unknown", "Cổng dịch chuyển không tồn tại.", 404);
        if (!float.IsFinite(x) || !float.IsFinite(y) || MathF.Abs(x - p.X) > PortalReach || MathF.Abs(y - p.Y) > PortalReach)
            throw new GameError("portal_too_far", "Bạn đứng quá xa cổng dịch chuyển.", 409);
        return db.InTransaction((c, tx) =>
        {
            CharacterDto ch = Query(c, tx, "c.id=$id AND c.account_id=$a", ("$id", characterId), ("$a", accountId)).SingleOrDefault()
                              ?? throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
            if (ch.lastMapId != p.FromMap)
                throw new GameError("portal_wrong_map", "Cổng này không ở bản đồ hiện tại của bạn.", 409);
            long now = Clock();
            if (lastTravelMs.TryGetValue(characterId, out long last) && now - last < (long)TravelCooldown.TotalMilliseconds)
                throw new GameError("portal_cooldown", "Cổng đang hồi năng lượng, hãy thử lại sau giây lát.", 429);
            lastTravelMs[characterId] = now;
            Database.Exec(c, tx, """
                UPDATE characters SET map_id=$m, pos_x=$x, pos_y=$y, has_position=1, revision=revision+1, saved_ms=$t WHERE id=$id
                """, ("$m", p.ToMap), ("$x", p.ArrivalX), ("$y", p.ArrivalY), ("$t", Database.NowMs()), ("$id", characterId));
            AccountService.Audit(c, tx, accountId, "travel", $"{characterId} {p.Id}");
            return Query(c, tx, "c.id=$id", ("$id", characterId)).Single();
        });
    }

    /// <summary>
    /// Saves where the character stands (autosave / leaving the map). Position is not an economic value, but it
    /// is still validated: known map, finite numbers, inside the map bounds. Never touches level, gold or items.
    /// </summary>
    public CharacterDto SaveLocation(string accountId, string characterId, string? mapId, float x, float y)
    {
        if (mapId == null || !Maps.TryGetValue(mapId, out WorldMap? map))
            throw new GameError("map_unknown", "Bản đồ không hợp lệ.");
        if (!float.IsFinite(x) || !float.IsFinite(y) || x < map.MinX || x > map.MaxX || y < map.MinY || y > map.MaxY)
            throw new GameError("position_invalid", "Vị trí nằm ngoài bản đồ.");
        return db.InTransaction((c, tx) =>
        {
            CharacterDto ch = Query(c, tx, "c.id=$id AND c.account_id=$a", ("$id", characterId), ("$a", accountId)).SingleOrDefault()
                              ?? throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
            // Saving a position never changes map: only a validated portal (Travel) can do that.
            if (ch.lastMapId != mapId)
                throw new GameError("map_mismatch", "Vị trí không thuộc bản đồ hiện tại của nhân vật.", 409);
            Database.Exec(c, tx, """
                UPDATE characters SET pos_x=$x, pos_y=$y, has_position=1, revision=revision+1, saved_ms=$t WHERE id=$id
                """, ("$x", x), ("$y", y), ("$t", Database.NowMs()), ("$id", characterId));
            return Query(c, tx, "c.id=$id", ("$id", characterId)).Single();
        });
    }

    // ------------------------------------------------------------- tutorial (levels 1–15 chain)
    public static readonly string[] TutorialSteps =
    {
        "move", "jump", "climb", "attack", "skill", "loot", "inventory", "quest", "shop_npc", "explore",
    };

    /// <summary>
    /// Advances the tutorial strictly one step at a time; replaying an already completed step is a no-op
    /// (so a reconnect cannot restart or skip the tutorial). Grants no currency.
    /// </summary>
    public CharacterDto CompleteTutorialStep(string accountId, string characterId, int step)
    {
        return db.InTransaction((c, tx) =>
        {
            CharacterDto ch = Query(c, tx, "c.id=$id AND c.account_id=$a", ("$id", characterId), ("$a", accountId)).SingleOrDefault()
                              ?? throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
            if (ch.tutorialCompleted || step < ch.tutorialStep) return ch;
            if (step != ch.tutorialStep) throw new GameError("tutorial_order", "Bước hướng dẫn chưa được mở.", 409);
            int next = step + 1;
            bool done = next >= TutorialSteps.Length;
            Database.Exec(c, tx, "UPDATE characters SET tutorial_step=$n, tutorial_done=$d, revision=revision+1, saved_ms=$t WHERE id=$id",
                ("$n", next), ("$d", done ? 1 : 0), ("$t", Database.NowMs()), ("$id", characterId));
            return Query(c, tx, "c.id=$id", ("$id", characterId)).Single();
        });
    }

    internal static IEnumerable<CharacterDto> Query(SqliteConnection c, SqliteTransaction? tx, string where, params (string, object?)[] args)
    {
        using var cmd = Database.Command(c, tx, $"""
            SELECT c.id, c.name, c.slot_index, c.hero_class, c.level, c.experience, c.skill_points, c.gold, a.diamonds,
                   c.map_id, c.checkpoint_id, c.tutorial_step, c.tutorial_done, c.revision, c.saved_ms,
                   c.pos_x, c.pos_y, c.has_position, c.appearance_json
            FROM characters c JOIN accounts a ON a.id = c.account_id WHERE {where}
            """, args);
        using var r = cmd.ExecuteReader();
        var list = new List<CharacterDto>();
        while (r.Read())
            list.Add(new CharacterDto
            {
                characterId = r.GetString(0), characterName = r.GetString(1), slotIndex = r.GetInt32(2), heroClass = r.GetInt32(3),
                level = r.GetInt32(4), experience = r.GetInt64(5), unspentSkillPoints = r.GetInt32(6), gold = r.GetInt64(7),
                diamonds = r.GetInt64(8), lastMapId = r.GetString(9), lastSafeCheckpointId = r.GetString(10),
                tutorialStep = r.GetInt32(11), tutorialCompleted = r.GetInt64(12) != 0, serverRevision = r.GetInt64(13),
                lastSavedUnixMilliseconds = r.GetInt64(14),
                posX = (float)r.GetDouble(15), posY = (float)r.GetDouble(16), hasPosition = r.GetInt64(17) != 0,
                outfit = CosmeticShop.ReadOutfit(r.GetString(18)),
            });
        return list;
    }
}
