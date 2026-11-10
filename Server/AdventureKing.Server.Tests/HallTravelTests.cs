using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>Phase D: the town's portals to Sảnh Bang Hội and Sảnh Kết Hôn and back (server portal graph).</summary>
public sealed class HallTravelTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private long clock = 1_000_000;

    public HallTravelTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000) { Clock = () => clock };
    }

    public void Dispose() => t.Dispose();

    [Theory]
    [InlineData("hub_guild_hall", 74f, "guild_hall", -14f, "guild_hall_hub", -19f, 71f)]
    [InlineData("hub_wedding_hall", -56f, "wedding_hall", -16f, "wedding_hall_hub", -21f, -53f)]
    public void Hall_portals_lead_in_and_back_out(string inPortal, float inX, string hall, float arriveX, string outPortal, float outX, float backX)
    {
        string acc = accounts.Register("hall" + hall[..4], "Kh0ng-Trung-Ten-2026").AccountId;
        string id = chars.Create(acc, "Khách " + hall[..4], 0, 0).characterId;
        // standing far from the portal: refused, still in town
        Assert.Equal("portal_too_far", Assert.Throws<GameError>(() => chars.Travel(acc, id, inPortal, 0f, 0f)).Code);
        CharacterDto inside = chars.Travel(acc, id, inPortal, inX, 0f);
        Assert.Equal(hall, inside.lastMapId);
        Assert.Equal(arriveX, inside.posX, 1);
        // the hall's portal does not work from the town, and the town's does not work from the hall
        clock += 2000;
        Assert.ThrowsAny<GameError>(() => chars.Travel(acc, id, inPortal, inX, 0f));
        clock += 2000;
        CharacterDto back = chars.Travel(acc, id, outPortal, outX, 0f);
        Assert.Equal("village_wind_bell", back.lastMapId);
        Assert.Equal(backX, back.posX, 1);
    }

    [Fact]
    public void Every_portal_lands_inside_its_destination_map()
    {
        foreach (var p in CharacterService.Portals.Values)
        {
            Assert.True(CharacterService.Maps.ContainsKey(p.FromMap), p.Id);
            var to = CharacterService.Maps[p.ToMap];
            Assert.InRange(p.ArrivalX, to.MinX, to.MaxX);
            Assert.InRange(p.ArrivalY, to.MinY, to.MaxY);
            // a return portal exists for every portal
            Assert.Contains(CharacterService.Portals.Values, q => q.FromMap == p.ToMap && q.ToMap == p.FromMap);
        }
    }
}
