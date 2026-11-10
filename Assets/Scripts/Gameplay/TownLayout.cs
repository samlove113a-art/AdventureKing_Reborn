using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Gameplay geometry of the world maps, shared by the scene builders, the tests and (for portals) mirrored
    /// by the server's portal graph (Server/.../CharacterService.Portals). One source of truth per map.
    /// Coordinates: gameplay plane z = 0, x to the right, y up, ground top at y = 0.
    /// </summary>
    public static class TownLayout
    {
        public readonly struct Deck
        {
            public readonly string Name; public readonly float XMin, XMax, Top;
            public Deck(string name, float xMin, float xMax, float top) { Name = name; XMin = xMin; XMax = xMax; Top = top; }
        }

        public readonly struct Climb
        {
            public readonly string Name; public readonly float X, Bottom, Top; public readonly bool Rope;
            /// <summary>Top of the trigger: 1.3 above the deck so the player's feet clear the floor before the climb ends.</summary>
            public float VisualTop => Top - 0.6f;
            public Climb(string name, float x, float bottom, float top, bool rope) { Name = name; X = x; Bottom = bottom; Top = top; Rope = rope; }
        }

        public readonly struct PortalSpot
        {
            public readonly string Id, ToScene, DestinationName; public readonly float X;
            public PortalSpot(string id, float x, string toScene, string destinationName) { Id = id; X = x; ToScene = toScene; DestinationName = destinationName; }
        }

        // ------------------------------------------------------------ Thị trấn Chuông Gió (hub)
        public static class Hub
        {
            public const string MapId = "village_wind_bell", Scene = "AK_Village", DisplayName = "Thị trấn Chuông Gió";
            public const float MinX = -60f, MaxX = 86f; // east end widened for the Guild Hall portal (x 74) so the camera can centre on it
            public static readonly Vector2 Spawn = new Vector2(0f, 1f);
            public const float StreamX = -16f, StreamHalfWidth = 2.2f;

            public static readonly Deck[] Decks =
            {
                new Deck("BanCongThoRen", -52f, -36f, 4.5f),   // smithy balcony
                new Deck("SanChuong", -6f, 14f, 5f),          // bell terrace above the plaza
                new Deck("DaiNgamGio", 0f, 10f, 10f),         // wind lookout (top)
                new Deck("CauTreo", 14f, 32f, 5f),            // rope bridge (bridge planks)
                new Deck("HienQuanTra", 32f, 48f, 5f),        // teahouse veranda
                new Deck("DaiCoiXay", 38f, 50f, 9.5f),        // windmill lookout
            };

            public static readonly Climb[] Climbs =
            {
                new Climb("ThangThoRen", -50f, 0f, 5.8f, false),
                new Climb("DayLeoSanChuong", -4f, 0f, 6.3f, true),
                new Climb("ThangSanChuong", 12f, 0f, 6.3f, false),
                new Climb("DayLeoDaiNgamGio", 8f, 5f, 11.3f, true),
                new Climb("ThangHienQuanTra", 46f, 0f, 6.3f, false),
                new Climb("ThangDaiCoiXay", 40f, 5f, 10.8f, false),
            };

            public static readonly PortalSpot[] Portals =
            {
                new PortalSpot("hub_east_meadow", 66f, "AK_Meadow", "Đồng Cỏ Gió"),
                new PortalSpot("hub_guild_hall", 74f, GuildHall.Scene, GuildHall.DisplayName),
                new PortalSpot("hub_wedding_hall", -56f, WeddingHall.Scene, WeddingHall.DisplayName),
            };

            /// <summary>Where NPCs will stand (marked in the scene as NpcSpot_*; NPC models are not built yet).</summary>
            public static readonly (string role, float x, float y)[] NpcSpots =
            {
                ("TruongLang", 6f, 0f), ("ThoRen", -46f, 0f), ("ThuongNhan", -30f, 0f), ("ThuKho", -40f, 4.5f),
                ("NhiemVu", 38f, 0f), ("ChuQuanTra", 42f, 5f),
            };
        }

        // ------------------------------------------------------------ Đồng Cỏ Gió (first field)
        public static class Meadow
        {
            public const string MapId = "meadow_breeze", Scene = "AK_Meadow", DisplayName = "Đồng Cỏ Gió";
            public const float MinX = -46f, MaxX = 84f;
            public static readonly Vector2 Spawn = new Vector2(-36f, 1f);

            public static readonly Deck[] Decks =
            {
                new Deck("GoCo", -10f, 6f, 4f),
                new Deck("TangDa", 14f, 30f, 7f),
                new Deck("BoVuc", 40f, 56f, 4f),
            };

            public static readonly Climb[] Climbs =
            {
                new Climb("ThangGoCo", -8f, 0f, 5.3f, false),
                new Climb("DayLeoTangDa", 16f, 0f, 8.3f, true),
                new Climb("ThangBoVuc", 54f, 0f, 5.3f, false),
            };

            public static readonly PortalSpot[] Portals = { new PortalSpot("meadow_west_hub", -40f, "AK_Village", "Thị trấn Chuông Gió") };
        }

        // ------------------------------------------------------------ Sảnh Bang Hội (Phase D interior, safe zone)
        public static class GuildHall
        {
            public const string MapId = "guild_hall", Scene = "AK_GuildHall", DisplayName = "Sảnh Bang Hội";
            public const float MinX = -24f, MaxX = 24f;
            public static readonly Vector2 Spawn = new Vector2(-14f, 1f);
            public static readonly Deck[] Decks = { };
            public static readonly Climb[] Climbs = { };
            public static readonly PortalSpot[] Portals = { new PortalSpot("guild_hall_hub", -19f, Hub.Scene, Hub.DisplayName) };
        }

        // ------------------------------------------------------------ Sảnh Kết Hôn (Phase D interior, safe zone)
        public static class WeddingHall
        {
            public const string MapId = "wedding_hall", Scene = "AK_WeddingHall", DisplayName = "Sảnh Kết Hôn";
            public const float MinX = -26f, MaxX = 26f;
            public static readonly Vector2 Spawn = new Vector2(-16f, 1f);
            public static readonly Deck[] Decks = { };
            public static readonly Climb[] Climbs = { };
            public static readonly PortalSpot[] Portals = { new PortalSpot("wedding_hall_hub", -21f, Hub.Scene, Hub.DisplayName) };
        }

        /// <summary>One entry per world map (scene, name, bounds, minimap data).</summary>
        public sealed class MapInfo
        {
            public string MapId, Scene, DisplayName;
            public float MinX, MaxX;
            public Vector2 Spawn;
            public Deck[] Decks;
            public Climb[] Climbs;
            public PortalSpot[] Portals;
        }

        public static readonly MapInfo[] Maps =
        {
            new MapInfo { MapId = Hub.MapId, Scene = Hub.Scene, DisplayName = Hub.DisplayName, MinX = Hub.MinX, MaxX = Hub.MaxX, Spawn = Hub.Spawn, Decks = Hub.Decks, Climbs = Hub.Climbs, Portals = Hub.Portals },
            new MapInfo { MapId = Meadow.MapId, Scene = Meadow.Scene, DisplayName = Meadow.DisplayName, MinX = Meadow.MinX, MaxX = Meadow.MaxX, Spawn = Meadow.Spawn, Decks = Meadow.Decks, Climbs = Meadow.Climbs, Portals = Meadow.Portals },
            new MapInfo { MapId = GuildHall.MapId, Scene = GuildHall.Scene, DisplayName = GuildHall.DisplayName, MinX = GuildHall.MinX, MaxX = GuildHall.MaxX, Spawn = GuildHall.Spawn, Decks = GuildHall.Decks, Climbs = GuildHall.Climbs, Portals = GuildHall.Portals },
            new MapInfo { MapId = WeddingHall.MapId, Scene = WeddingHall.Scene, DisplayName = WeddingHall.DisplayName, MinX = WeddingHall.MinX, MaxX = WeddingHall.MaxX, Spawn = WeddingHall.Spawn, Decks = WeddingHall.Decks, Climbs = WeddingHall.Climbs, Portals = WeddingHall.Portals },
        };

        /// <summary>The map with this id (the hub when unknown).</summary>
        public static MapInfo For(string mapId)
        {
            foreach (MapInfo m in Maps) if (m.MapId == mapId) return m;
            return Maps[0];
        }
    }
}
