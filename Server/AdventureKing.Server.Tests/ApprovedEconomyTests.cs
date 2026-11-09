using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace AdventureKing.Server.Tests;

/// <summary>
/// Owner-approved economy values (2026-11-09) stay as approved: production starter Gold and the shop price list.
/// Changing them must be a deliberate edit of these expectations, not an accident.
/// </summary>
public sealed class ApprovedEconomyTests
{
    private static string ServerDir()
    {
        var d = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "AdventureKing.Server", "appsettings.json"))) d = d.Parent;
        Assert.NotNull(d);
        return Path.Combine(d!.FullName, "AdventureKing.Server");
    }

    [Fact]
    public void Production_starter_gold_is_the_approved_800()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ServerDir(), "appsettings.json")));
        Assert.Equal(800, doc.RootElement.GetProperty("AdventureKing").GetProperty("StarterGold").GetInt64());
    }

    [Theory]
    [InlineData("head_lotus_crown", "gold", 1200)]
    [InlineData("head_bell_circlet", "gold", 1800)]
    [InlineData("acc_silver_bell", "gold", 600)]
    [InlineData("back_dragonfly_wings", "gold", 2500)]
    [InlineData("head_duck_buddy", "gold", 1600)]
    [InlineData("head_cat_ears", "gold", 1000)]
    [InlineData("set_crystal_zephyr", "diamonds", 300)]
    [InlineData("set_little_dino", "diamonds", 240)]
    [InlineData("set_sweet_dream", "diamonds", 180)]
    public void Approved_prices_are_served(string id, string currency, long price)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ServerDir(), "config", "cosmetics.json")));
        var item = doc.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == id);
        Assert.Equal(currency, item.GetProperty("currency").GetString());
        Assert.Equal(price, item.GetProperty("price").GetInt64());
        Assert.True(item.GetProperty("listed").GetBoolean());
    }
}
