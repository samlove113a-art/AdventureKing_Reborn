using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

/// <summary>AK Map Forge Map 2 contract tests: online travel, spawn safety and authoritatively fixed economy.</summary>
public sealed class MapForgeTests
{
    [Fact]
    public void Map2_has_bidirectional_server_validated_portals()
    {
        var maps = CharacterService.Maps;
        Assert.Contains("ch1_mushroom_village", maps.Keys);
        Assert.Contains("ch1_green_meadow", maps.Keys);

        var east = CharacterService.Portals["ch1_village_east_m02"];
        var west = CharacterService.Portals["ch1_m02_west_village"];
        Assert.Equal("ch1_mushroom_village", east.FromMap);
        Assert.Equal("ch1_green_meadow", east.ToMap);
        Assert.Equal("ch1_green_meadow", west.FromMap);
        Assert.Equal("ch1_mushroom_village", west.ToMap);
        Assert.InRange(east.X, maps[east.FromMap].MinX, maps[east.FromMap].MaxX);
        Assert.InRange(west.X, maps[west.FromMap].MinX, maps[west.FromMap].MaxX);
        Assert.InRange(east.ArrivalX, maps[east.ToMap].MinX, maps[east.ToMap].MaxX);
        Assert.InRange(west.ArrivalX, maps[west.ToMap].MinX, maps[west.ToMap].MaxX);
    }

    [Fact]
    public void Map2_spawns_fit_authoritative_lanes_and_never_attack_west_portal_safe_zone()
    {
        var cfg = FarmConfig.Load(FarmServiceTests.MonstersPath);
        var spawns = cfg.spawns.Where(s => s.mapId == "ch1_green_meadow").ToArray();
        Assert.Equal(8, spawns.Length);
        Assert.Contains(spawns, s => s.type == "pollen_slime");
        Assert.Contains(spawns, s => s.type == "young_mushroom");
        var world = CharacterService.Maps["ch1_green_meadow"];
        var zone = FarmService.SafeZones["ch1_green_meadow"];
        foreach (var spawn in spawns)
        {
            var monster = cfg.types.Single(t => t.id == spawn.type);
            Assert.InRange(spawn.x, world.MinX, world.MaxX);
            Assert.InRange(spawn.y, world.MinY, world.MaxY);
            Assert.True(spawn.x - monster.leash - monster.reach > zone.maxX,
                "Monster " + spawn.id + " may reach the protected west entry");
        }

        // Regression: accidental decorative seed updates must never be used as server reward sources.
        var pollen = cfg.types.Single(t => t.id == "pollen_slime");
        Assert.True(pollen.maxHp > 0 && pollen.goldMin >= 0 && pollen.goldMax >= pollen.goldMin);
        Assert.Equal(-36f, FarmService.RespawnPoints["ch1_green_meadow"].x);
    }
}
