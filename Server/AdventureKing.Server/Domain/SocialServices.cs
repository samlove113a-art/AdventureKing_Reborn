using System.Globalization;
using System.Text;
using System.Text.Json;
using AdventureKing.Server.Data;
using Microsoft.Data.Sqlite;

namespace AdventureKing.Server.Domain;

// ================================================================== config (DRAFT economy, see config/social.json)
public sealed class GuildQuestDef { public string id { get; set; } = ""; public string @event { get; set; } = ""; public int need { get; set; } = 1; public string text { get; set; } = ""; public long rewardExp { get; set; } public long rewardDevotion { get; set; } }
public sealed class GuildConfig
{
    public long createFeeGold { get; set; }
    public int minLevelToCreate { get; set; } = 1;
    public int maxVice { get; set; } = 2;
    public long[] levelExp { get; set; } = { 0 };
    public int[] maxMembers { get; set; } = { 20 };
    public long[] contributionGold { get; set; } = Array.Empty<long>();
    public double devotionPerGold { get; set; }
    public double expPerGold { get; set; }
    public string[] emblems { get; set; } = Array.Empty<string>();
    public GuildQuestDef[] quests { get; set; } = Array.Empty<GuildQuestDef>();
}
public sealed class CeremonyStepDef { public string id { get; set; } = ""; public int ms { get; set; } }
public sealed class CeremonyConfig { public int inviteSeconds { get; set; } = 60; public int leadInMs { get; set; } = 2500; public CeremonyStepDef[] steps { get; set; } = Array.Empty<CeremonyStepDef>(); }
public sealed class MarriageConfig
{
    public int minLevel { get; set; } = 1;
    public long ringPriceGold { get; set; }
    public bool requireFriends { get; set; } = true;
    public CeremonyConfig ceremony { get; set; } = new();
    public string[] gifts { get; set; } = Array.Empty<string>();
}
public sealed class ChatConfig { public int maxLength { get; set; } = 120; public int minIntervalMs { get; set; } = 1200; public int burstCount { get; set; } = 8; public int burstWindowMs { get; set; } = 30000; public int historyOnJoin { get; set; } = 30; }
public sealed class SocialConfig
{
    public string status { get; set; } = "DRAFT";
    public bool pricesApproved { get; set; }
    public bool economyEnabled { get; set; }
    public GuildConfig guild { get; set; } = new();
    public MarriageConfig marriage { get; set; } = new();
    public ChatConfig chat { get; set; } = new();

    public static SocialConfig Load(string path, CosmeticCatalog cosmetics)
    {
        var cfg = JsonSerializer.Deserialize<SocialConfig>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty social config");
        bool draft = cfg.status.Contains("DRAFT") && !cfg.status.Contains("DEV");
        if (draft && (cfg.pricesApproved || cfg.economyEnabled))
            throw new InvalidDataException("social: a DRAFT config may not approve prices or open the guild economy (owner approval first)");
        if (cfg.guild.levelExp.Length == 0 || cfg.guild.levelExp.Length != cfg.guild.maxMembers.Length || cfg.guild.emblems.Length == 0)
            throw new InvalidDataException("social: guild levels / member caps / emblems");
        if (cfg.marriage.ceremony.steps.Length == 0) throw new InvalidDataException("social: ceremony steps");
        foreach (string g in cfg.marriage.gifts)
            if (cosmetics.Find(g) is not { listed: false }) throw new InvalidDataException("social: wedding gift must be an unlisted cosmetic: " + g);
        return cfg;
    }
}

/// <summary>Shared lookups for the social services.</summary>
public static class SocialDb
{
    public sealed record Who(string Id, string AccountId, string Name, int HeroClass, int Level, string MapId, string AppearanceJson, float X, float Y);

    public static Who? ById(SqliteConnection c, SqliteTransaction? tx, string id) => Read(c, tx, "id=$v", id);
    public static Who? ByName(SqliteConnection c, SqliteTransaction? tx, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Read(c, tx, "name_key=$v", NameRules.UniqueKey(name.Trim()));

    private static Who? Read(SqliteConnection c, SqliteTransaction? tx, string where, string v)
    {
        using var cmd = Database.Command(c, tx, $"SELECT id, account_id, name, hero_class, level, map_id, appearance_json, pos_x, pos_y FROM characters WHERE {where}", ("$v", v));
        using var r = cmd.ExecuteReader();
        return r.Read() ? new Who(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4), r.GetString(5), r.GetString(6),
            r.IsDBNull(7) ? 0f : (float)r.GetDouble(7), r.IsDBNull(8) ? 0f : (float)r.GetDouble(8)) : null;
    }

    /// <summary>The caller's own character (ownership checked).</summary>
    public static Who Me(SqliteConnection c, SqliteTransaction? tx, string accountId, string characterId)
    {
        Who? w = ById(c, tx, characterId);
        if (w == null || w.AccountId != accountId) throw new GameError("character_not_found", "Không tìm thấy nhân vật.", 404);
        return w;
    }

    public static Who Target(SqliteConnection c, SqliteTransaction? tx, string? name, string meId)
    {
        Who t = ByName(c, tx, name) ?? throw new GameError("player_not_found", "Không tìm thấy người chơi này.", 404);
        if (t.Id == meId) throw new GameError("self", "Không thể chọn chính mình.");
        return t;
    }

    /// <summary>Either side blocked the other.</summary>
    public static bool Blocked(SqliteConnection c, SqliteTransaction? tx, string a, string b) =>
        Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM blocks WHERE (character_id=$a AND blocked_id=$b) OR (character_id=$b AND blocked_id=$a)", ("$a", a), ("$b", b)) > 0;

    public static bool Friends(SqliteConnection c, SqliteTransaction? tx, string a, string b) =>
        Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM friends WHERE a_id=$a AND b_id=$b", ("$a", a), ("$b", b)) > 0;

    public static string? GuildOf(SqliteConnection c, SqliteTransaction? tx, string characterId) =>
        Database.Scalar<string>(c, tx, "SELECT guild_id FROM guild_members WHERE character_id=$c", ("$c", characterId));

    public static int TextLength(string s) => new StringInfo(s).LengthInTextElements;
}

// ================================================================== presence (memory only: who is where, right now)
public sealed class PresenceService
{
    public sealed class Entry
    {
        public string Id = "", Map = "", Instance = "", Anim = "idle", Emote = "";
        public float X, Y;
        public int Facing = 1, EmoteSeq;
        public long Ms;
    }

    public const float MaxSpeed = 14f, Slack = 2.5f;
    public const long PeerTimeoutMs = 6000, OnlineTimeoutMs = 15000;
    public static readonly HashSet<string> Anims = new() { "idle", "run", "jump", "fall", "climb", "attack", "skill", "hit", "dead", "emote" };
    public static readonly HashSet<string> Emotes = new() { "", "Kneel", "Toast", "Cheer", "Celebrate", "Bow", "Wave" };

    private readonly Dictionary<string, Entry> entries = new();
    private readonly object gate = new();
    public Func<long> Clock { get; set; } = Database.NowMs;

    public Entry? Get(string id) { lock (gate) return entries.TryGetValue(id, out var e) ? Copy(e) : null; }
    public bool Online(string id) { lock (gate) return entries.TryGetValue(id, out var e) && Clock() - e.Ms <= OnlineTimeoutMs; }
    public bool PresentOn(string id, string map, long within = 10000)
    {
        lock (gate) return entries.TryGetValue(id, out var e) && e.Map == map && Clock() - e.Ms <= within;
    }

    private static Entry Copy(Entry e) => new() { Id = e.Id, Map = e.Map, Instance = e.Instance, Anim = e.Anim, Emote = e.Emote, X = e.X, Y = e.Y, Facing = e.Facing, EmoteSeq = e.EmoteSeq, Ms = e.Ms };

    /// <summary>Moves the character (speed-checked against its previous heartbeat on the same map).
    /// Returns the accepted position and whether the client was corrected.</summary>
    public (float x, float y, bool corrected) Update(string id, string map, string instance, float x, float y, int facing, string? anim, string? emote, int emoteSeq,
        CharacterService.WorldMap bounds, float savedX, float savedY)
    {
        long t = Clock();
        lock (gate)
        {
            entries.TryGetValue(id, out Entry? prev);
            bool valid = float.IsFinite(x) && float.IsFinite(y) && x >= bounds.MinX && x <= bounds.MaxX && y >= bounds.MinY && y <= bounds.MaxY;
            bool corrected = false;
            if (prev != null && prev.Map == map && valid)
            {
                float dt = Math.Max(0f, (t - prev.Ms) / 1000f);
                float dist = MathF.Sqrt((x - prev.X) * (x - prev.X) + (y - prev.Y) * (y - prev.Y));
                if (dist > MaxSpeed * dt + Slack) { x = prev.X; y = prev.Y; corrected = true; }
            }
            else if (!valid)
            {
                (x, y) = prev != null && prev.Map == map ? (prev.X, prev.Y) : (savedX, savedY);
                corrected = true;
            }
            var e = new Entry
            {
                Id = id, Map = map, Instance = instance, X = x, Y = y, Ms = t, Facing = facing < 0 ? -1 : 1,
                Anim = anim != null && Anims.Contains(anim) ? anim : "idle",
                Emote = emote != null && Emotes.Contains(emote) ? emote : "", EmoteSeq = Math.Clamp(emoteSeq, 0, 1_000_000),
            };
            entries[id] = e;
            return (x, y, corrected);
        }
    }

    public List<Entry> On(string map, string instance, string exceptId)
    {
        long t = Clock();
        lock (gate)
            return entries.Values.Where(e => e.Map == map && e.Instance == instance && e.Id != exceptId && t - e.Ms <= PeerTimeoutMs).Select(Copy).ToList();
    }

    public void Forget(string id) { lock (gate) entries.Remove(id); }
}

// ================================================================== friends + blocks
public sealed class FriendDto { public string id { get; set; } = ""; public string name { get; set; } = ""; public int heroClass { get; set; } public int level { get; set; } public bool online { get; set; } public string mapId { get; set; } = ""; }
public sealed class FriendsDto { public FriendDto[] friends { get; set; } = Array.Empty<FriendDto>(); public FriendDto[] incoming { get; set; } = Array.Empty<FriendDto>(); public FriendDto[] outgoing { get; set; } = Array.Empty<FriendDto>(); public FriendDto[] blocked { get; set; } = Array.Empty<FriendDto>(); }
public sealed class NameDto { public string? name { get; set; } }
public sealed class IdDto { public string? id { get; set; } }

/// <summary>
/// Friends (request → accept, both directions stored), remove, block / unblock. Blocking removes the friendship and
/// every pending request or proposal between the two, hides each from the other on the map, and refuses their chat,
/// friend requests, guild invites and proposals.
/// </summary>
public sealed class FriendService
{
    public const int MaxFriends = 100, MaxPending = 50;
    private readonly Database db;
    private readonly PresenceService presence;
    public FriendService(Database db, PresenceService presence) { this.db = db; this.presence = presence; }

    public FriendsDto Request(string accountId, string me, NameDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Who self = SocialDb.Me(c, tx, accountId, me);
        SocialDb.Who t = SocialDb.Target(c, tx, req?.name, me);
        if (SocialDb.Blocked(c, tx, me, t.Id)) throw new GameError("blocked", "Không thể kết bạn với người chơi này.", 409);
        if (SocialDb.Friends(c, tx, me, t.Id)) throw new GameError("already_friends", "Hai bạn đã là bạn bè.", 409);
        if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM friends WHERE a_id=$a", ("$a", me)) >= MaxFriends) throw new GameError("friends_full", "Danh sách bạn bè đã đầy.", 409);
        // they already asked me: accept straight away
        if (Database.Exec(c, tx, "DELETE FROM friend_requests WHERE from_id=$t AND to_id=$m", ("$t", t.Id), ("$m", me)) == 1) { Link(c, tx, me, t.Id); return List(c, tx, me); }
        if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM friend_requests WHERE from_id=$m", ("$m", me)) >= MaxPending) throw new GameError("too_many_requests", "Bạn đã gửi quá nhiều lời mời đang chờ.", 409);
        if (Database.Exec(c, tx, "INSERT OR IGNORE INTO friend_requests(from_id, to_id, created_ms) VALUES($m,$t,$n)", ("$m", me), ("$t", t.Id), ("$n", Database.NowMs())) == 0)
            throw new GameError("already_requested", "Đã gửi lời mời kết bạn, hãy chờ trả lời.", 409);
        _ = self;
        return List(c, tx, me);
    });

    private static void Link(SqliteConnection c, SqliteTransaction tx, string a, string b)
    {
        long t = Database.NowMs();
        Database.Exec(c, tx, "INSERT OR IGNORE INTO friends(a_id, b_id, since_ms) VALUES($a,$b,$t)", ("$a", a), ("$b", b), ("$t", t));
        Database.Exec(c, tx, "INSERT OR IGNORE INTO friends(a_id, b_id, since_ms) VALUES($b,$a,$t)", ("$a", a), ("$b", b), ("$t", t));
    }

    public FriendsDto Respond(string accountId, string me, IdDto? req, bool accept) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        if (Database.Exec(c, tx, "DELETE FROM friend_requests WHERE from_id=$f AND to_id=$m", ("$f", req?.id ?? ""), ("$m", me)) == 0)
            throw new GameError("request_unknown", "Lời mời không còn.", 404);
        if (accept)
        {
            if (Database.Scalar<long>(c, tx, "SELECT COUNT(*) FROM friends WHERE a_id=$a", ("$a", me)) >= MaxFriends) throw new GameError("friends_full", "Danh sách bạn bè đã đầy.", 409);
            Link(c, tx, me, req!.id!);
        }
        return List(c, tx, me);
    });

    public FriendsDto Remove(string accountId, string me, IdDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        string other = req?.id ?? "";
        Database.Exec(c, tx, "DELETE FROM friends WHERE (a_id=$m AND b_id=$o) OR (a_id=$o AND b_id=$m)", ("$m", me), ("$o", other));
        Database.Exec(c, tx, "DELETE FROM friend_requests WHERE from_id=$m AND to_id=$o", ("$m", me), ("$o", other)); // cancels my own request too
        return List(c, tx, me);
    });

    public FriendsDto Block(string accountId, string me, NameDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        SocialDb.Who t = SocialDb.Target(c, tx, req?.name, me);
        Database.Exec(c, tx, "INSERT OR IGNORE INTO blocks(character_id, blocked_id, created_ms) VALUES($m,$t,$n)", ("$m", me), ("$t", t.Id), ("$n", Database.NowMs()));
        Database.Exec(c, tx, "DELETE FROM friends WHERE (a_id=$m AND b_id=$t) OR (a_id=$t AND b_id=$m)", ("$m", me), ("$t", t.Id));
        Database.Exec(c, tx, "DELETE FROM friend_requests WHERE (from_id=$m AND to_id=$t) OR (from_id=$t AND to_id=$m)", ("$m", me), ("$t", t.Id));
        Database.Exec(c, tx, "DELETE FROM proposals WHERE (from_id=$m AND to_id=$t) OR (from_id=$t AND to_id=$m)", ("$m", me), ("$t", t.Id));
        Database.Exec(c, tx, "DELETE FROM guild_requests WHERE character_id=$m AND kind='invite' AND by_id=$t", ("$m", me), ("$t", t.Id));
        return List(c, tx, me);
    });

    public FriendsDto Unblock(string accountId, string me, IdDto? req) => db.InTransaction((c, tx) =>
    {
        SocialDb.Me(c, tx, accountId, me);
        Database.Exec(c, tx, "DELETE FROM blocks WHERE character_id=$m AND blocked_id=$o", ("$m", me), ("$o", req?.id ?? ""));
        return List(c, tx, me);
    });

    public FriendsDto Get(string accountId, string me) => db.InTransaction((c, tx) => { SocialDb.Me(c, tx, accountId, me); return List(c, tx, me); });

    private FriendsDto List(SqliteConnection c, SqliteTransaction tx, string me)
    {
        FriendDto[] Q(string sql)
        {
            var l = new List<FriendDto>();
            using var cmd = Database.Command(c, tx, sql, ("$m", me));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                string id = r.GetString(0);
                bool on = presence.Online(id);
                l.Add(new FriendDto { id = id, name = r.GetString(1), heroClass = r.GetInt32(2), level = r.GetInt32(3), online = on, mapId = on ? r.GetString(4) : "" });
            }
            return l.OrderByDescending(f => f.online).ThenBy(f => f.name, StringComparer.Ordinal).ToArray();
        }
        const string cols = "c.id, c.name, c.hero_class, c.level, c.map_id";
        return new FriendsDto
        {
            friends = Q($"SELECT {cols} FROM friends f JOIN characters c ON c.id=f.b_id WHERE f.a_id=$m"),
            incoming = Q($"SELECT {cols} FROM friend_requests q JOIN characters c ON c.id=q.from_id WHERE q.to_id=$m"),
            outgoing = Q($"SELECT {cols} FROM friend_requests q JOIN characters c ON c.id=q.to_id WHERE q.from_id=$m"),
            blocked = Q($"SELECT {cols} FROM blocks b JOIN characters c ON c.id=b.blocked_id WHERE b.character_id=$m"),
        };
    }
}

// ================================================================== chat
public sealed class ChatMessageDto { public long id { get; set; } public string channel { get; set; } = ""; public string senderId { get; set; } = ""; public string sender { get; set; } = ""; public string toId { get; set; } = ""; public string toName { get; set; } = ""; public string text { get; set; } = ""; public long ms { get; set; } }
public sealed class ChatSendDto { public string? channel { get; set; } public string? to { get; set; } public string? text { get; set; } }

/// <summary>
/// Chat: Thế Giới (everyone), Bang Hội (own guild only), Riêng Tư (one player). Server-side: length limit, profanity
/// masking (NameRules), per-character rate limit (min interval + burst window), blocks both ways, guild membership.
/// The sender name is the server's, never the client's (no impersonation).
/// </summary>
public sealed class ChatService
{
    private readonly Database db;
    private readonly ChatConfig cfg;
    private readonly NameRules names;
    private readonly Dictionary<string, Queue<long>> sent = new();
    private readonly object gate = new();
    public Func<long> Clock { get; set; } = Database.NowMs;

    public ChatService(Database db, SocialConfig cfg, NameRules names) { this.db = db; this.cfg = cfg.chat; this.names = names; }

    public ChatMessageDto Send(string accountId, string me, ChatSendDto? req)
    {
        string channel = req?.channel ?? "";
        if (channel is not ("world" or "guild" or "private")) throw new GameError("chat_channel", "Kênh chat không hợp lệ.");
        string text = names.MaskChat(req?.text).Trim();
        int len = SocialDb.TextLength(text);
        if (len == 0) throw new GameError("chat_empty", "Hãy nhập nội dung.");
        if (len > cfg.maxLength) throw new GameError("chat_too_long", $"Tin nhắn tối đa {cfg.maxLength} ký tự.");
        long t = Clock();
        lock (gate)
        {
            if (!sent.TryGetValue(me, out var q)) sent[me] = q = new Queue<long>();
            while (q.Count > 0 && t - q.Peek() > cfg.burstWindowMs) q.Dequeue();
            if (q.Count > 0 && t - q.Last() < cfg.minIntervalMs) throw new GameError("chat_too_fast", "Bạn nhắn quá nhanh, chờ chút nhé.", 429);
            if (q.Count >= cfg.burstCount) throw new GameError("chat_spam", "Bạn nhắn quá nhiều, hãy chờ ít giây.", 429);
            q.Enqueue(t);
        }
        return db.InTransaction((c, tx) =>
        {
            SocialDb.Who self = SocialDb.Me(c, tx, accountId, me);
            string ch = "world"; string? toId = null; string toName = "";
            if (channel == "guild")
            {
                string g = SocialDb.GuildOf(c, tx, me) ?? throw new GameError("not_in_guild", "Bạn chưa vào bang hội.", 409);
                ch = "guild:" + g;
            }
            else if (channel == "private")
            {
                SocialDb.Who to = SocialDb.Target(c, tx, req?.to, me);
                if (SocialDb.Blocked(c, tx, me, to.Id)) throw new GameError("chat_blocked", "Không thể nhắn tin cho người chơi này.", 409);
                ch = "dm"; toId = to.Id; toName = to.Name;
            }
            Database.Exec(c, tx, "INSERT INTO chat_messages(channel, sender_id, sender_name, to_id, text, created_ms) VALUES($c,$s,$n,$t,$x,$m)",
                ("$c", ch), ("$s", me), ("$n", self.Name), ("$t", toId), ("$x", text), ("$m", t));
            long id = Database.Scalar<long>(c, tx, "SELECT last_insert_rowid()");
            return new ChatMessageDto { id = id, channel = ch.StartsWith("guild:") ? "guild" : ch == "dm" ? "private" : "world", senderId = me, sender = self.Name, toId = toId ?? "", toName = toName, text = text, ms = t };
        });
    }

    /// <summary>Messages this character may read after the cursor (cursor ≤ 0: the latest few as history).</summary>
    public (ChatMessageDto[] msgs, long cursor) Since(SqliteConnection c, SqliteTransaction? tx, string me, long cursor)
    {
        string g = "guild:" + (SocialDb.GuildOf(c, tx, me) ?? "-");
        const string visible = "(m.channel='world' OR m.channel=$g OR (m.channel='dm' AND (m.sender_id=$me OR m.to_id=$me))) " +
                               "AND NOT EXISTS (SELECT 1 FROM blocks b WHERE b.character_id=$me AND b.blocked_id=m.sender_id)";
        string sql = cursor <= 0
            ? $"SELECT * FROM (SELECT m.id, m.channel, m.sender_id, m.sender_name, m.to_id, m.text, m.created_ms, (SELECT name FROM characters WHERE id=m.to_id) FROM chat_messages m WHERE {visible} ORDER BY m.id DESC LIMIT {cfg.historyOnJoin}) ORDER BY 1"
            : $"SELECT m.id, m.channel, m.sender_id, m.sender_name, m.to_id, m.text, m.created_ms, (SELECT name FROM characters WHERE id=m.to_id) FROM chat_messages m WHERE m.id > $cur AND {visible} ORDER BY m.id LIMIT 60";
        var list = new List<ChatMessageDto>();
        using (var cmd = Database.Command(c, tx, sql, ("$g", g), ("$me", me), ("$cur", cursor)))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                string ch = r.GetString(1);
                list.Add(new ChatMessageDto
                {
                    id = r.GetInt64(0), channel = ch.StartsWith("guild:") ? "guild" : ch == "dm" ? "private" : "world", senderId = r.GetString(2), sender = r.GetString(3),
                    toId = r.IsDBNull(4) ? "" : r.GetString(4), text = r.GetString(5), ms = r.GetInt64(6), toName = r.IsDBNull(7) ? "" : r.GetString(7),
                });
            }
        long next = list.Count > 0 ? list[^1].id : Math.Max(cursor, Database.Scalar<long>(c, tx, "SELECT COALESCE(MAX(id),0) FROM chat_messages"));
        return (list.ToArray(), next);
    }
}

// ================================================================== the multiplayer heartbeat
public sealed class PeerDto
{
    public string id { get; set; } = ""; public string name { get; set; } = ""; public int heroClass { get; set; } public int level { get; set; }
    public float x { get; set; } public float y { get; set; } public int facing { get; set; } = 1; public string anim { get; set; } = "idle";
    public string emote { get; set; } = ""; public int emoteSeq { get; set; }
    public OutfitDto outfit { get; set; } = new(); public string guild { get; set; } = ""; public string guildEmblem { get; set; } = "";
    public string partner { get; set; } = ""; public string title { get; set; } = ""; public bool friend { get; set; }
}
public sealed class BadgesDto { public int friendRequests { get; set; } public int guildInvites { get; set; } public int guildApplications { get; set; } public int proposals { get; set; } }
public sealed class SyncRequestDto { public float x { get; set; } public float y { get; set; } public int facing { get; set; } = 1; public string? anim { get; set; } public string? emote { get; set; } public int emoteSeq { get; set; } public long chatAfter { get; set; } }
public sealed class SyncDto
{
    public long serverMs { get; set; } public string mapId { get; set; } = ""; public bool corrected { get; set; } public float x { get; set; } public float y { get; set; }
    public PeerDto[] peers { get; set; } = Array.Empty<PeerDto>(); public ChatMessageDto[] chat { get; set; } = Array.Empty<ChatMessageDto>(); public long chatCursor { get; set; }
    public BadgesDto badges { get; set; } = new(); public CeremonyDto? ceremony { get; set; }
}

/// <summary>
/// One request ~6 times a second from every client in the world: the server validates and stores where the hero is
/// (speed / bounds checked, map from the database — a client cannot move itself to another map), and answers with
/// the other players on the same map (same guild instance inside the Guild Hall; blocked players hidden), new chat,
/// social badges and the wedding ceremony state for the Wedding Hall.
/// </summary>
public sealed class SyncService
{
    private readonly Database db;
    private readonly PresenceService presence;
    private readonly ChatService chat;
    private readonly MarriageService marriage;
    public SyncService(Database db, PresenceService presence, ChatService chat, MarriageService marriage) { this.db = db; this.presence = presence; this.chat = chat; this.marriage = marriage; }

    public const string GuildHall = "guild_hall", WeddingHall = "wedding_hall";

    public SyncDto Sync(string accountId, string me, SyncRequestDto? req)
    {
        req ??= new SyncRequestDto();
        using var c = db.Open();
        SocialDb.Who self = SocialDb.Me(c, null, accountId, me);
        if (!CharacterService.Maps.TryGetValue(self.MapId, out var bounds)) throw new GameError("map_unknown", "Bản đồ không hợp lệ.");
        string? guild = SocialDb.GuildOf(c, null, me);
        string instance = self.MapId == GuildHall ? guild ?? "" : "";
        var (x, y, corrected) = presence.Update(me, self.MapId, instance, req.x, req.y, req.facing, req.anim, req.emote, req.emoteSeq, bounds, self.X, self.Y);

        var peers = new List<PeerDto>();
        foreach (var e in presence.On(self.MapId, instance, me))
        {
            if (SocialDb.Blocked(c, null, me, e.Id)) continue;
            using var cmd = Database.Command(c, null, """
                SELECT c.name, c.hero_class, c.level, c.appearance_json, g.name, g.emblem, t.title_id,
                       (SELECT p.name FROM marriages m JOIN characters p ON p.id = CASE WHEN m.a_id=c.id THEN m.b_id ELSE m.a_id END
                        WHERE (m.a_id=c.id OR m.b_id=c.id) AND m.status='married')
                FROM characters c LEFT JOIN guild_members gm ON gm.character_id=c.id LEFT JOIN guilds g ON g.id=gm.guild_id
                LEFT JOIN title_equipped t ON t.character_id=c.id WHERE c.id=$id
                """, ("$id", e.Id));
            using var r = cmd.ExecuteReader();
            if (!r.Read()) continue;
            peers.Add(new PeerDto
            {
                id = e.Id, name = r.GetString(0), heroClass = r.GetInt32(1), level = r.GetInt32(2), outfit = CosmeticShop.ReadOutfit(r.GetString(3)),
                guild = r.IsDBNull(4) ? "" : r.GetString(4), guildEmblem = r.IsDBNull(5) ? "" : r.GetString(5), title = r.IsDBNull(6) ? "" : r.GetString(6),
                partner = r.IsDBNull(7) ? "" : r.GetString(7), friend = SocialDb.Friends(c, null, me, e.Id),
                x = e.X, y = e.Y, facing = e.Facing, anim = e.Anim, emote = e.Emote, emoteSeq = e.EmoteSeq,
            });
        }
        var (msgs, cursor) = chat.Since(c, null, me, req.chatAfter);
        bool officer = guild != null && Database.Scalar<string>(c, null, "SELECT role FROM guild_members WHERE character_id=$m", ("$m", me)) is "leader" or "vice";
        var badges = new BadgesDto
        {
            friendRequests = (int)Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM friend_requests WHERE to_id=$m", ("$m", me)),
            guildInvites = (int)Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM guild_requests WHERE character_id=$m AND kind='invite'", ("$m", me)),
            guildApplications = officer ? (int)Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM guild_requests WHERE guild_id=$g AND kind='apply'", ("$g", guild)) : 0,
            proposals = (int)Database.Scalar<long>(c, null, "SELECT COUNT(*) FROM proposals WHERE to_id=$m", ("$m", me)),
        };
        CeremonyDto? cer = self.MapId == WeddingHall ? marriage.CeremonyView() : null;
        return new SyncDto { serverMs = presence.Clock(), mapId = self.MapId, corrected = corrected, x = x, y = y, peers = peers.ToArray(), chat = msgs, chatCursor = cursor, badges = badges, ceremony = cer };
    }
}
