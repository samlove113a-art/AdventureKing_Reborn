using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdventureKing.Server.Data;
using AdventureKing.Server.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdventureKing.Server.Tests;

public class CosmeticShopTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly AccountService accounts;
    private readonly CharacterService chars;
    private readonly CosmeticShop shop;
    private readonly string acc;
    private readonly CharacterDto hero;

    public static string CatalogPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdventureKing.Server", "config", "cosmetics.json");

    public CosmeticShopTests()
    {
        accounts = new AccountService(t.Db);
        chars = new CharacterService(t.Db, new NameRules(), starterGold: 5000);
        shop = new CosmeticShop(t.Db, CosmeticCatalog.Load(CatalogPath));
        acc = accounts.Register("fashion01", "fashion-pass-01").AccountId;
        hero = chars.Create(acc, "Thời Trang", 1, 0); // Mage
    }

    public void Dispose() => t.Dispose();

    private static string Rid() => Guid.NewGuid().ToString("N");
    private long Count(string sql, params (string, object?)[] a) { using var c = t.Db.Open(); return Database.Scalar<long>(c, null, sql, a); }
    private void GiveDiamonds(long n) { t.Db.InTransaction((c, tx) => Wallet.Diamonds(c, tx, acc, n, "test", "test:" + Rid())); }

    [Fact]
    public void Real_Catalog_Is_Valid_And_Has_Ten_2D_Sets_Per_Class_In_Gold_Or_Diamonds()
    {
        CosmeticCatalog cat = CosmeticCatalog.Load(CatalogPath);
        // 2D pixel fashion: 10 full outfits per class, each owned by exactly one class, sold for Gold or Diamonds per item
        var sets = cat.items.Where(i => i.art == "2d" && i.kind == ItemKinds.CosmeticSet).ToList();
        Assert.Equal(40, sets.Count);
        for (int k = 0; k < 4; k++) Assert.Equal(10, sets.Count(s => s.classes.Length == 1 && s.classes[0] == k));
        Assert.All(sets, s => Assert.Equal("diamonds", s.currency));                                   // skins are sold for Diamonds only
        for (int k = 0; k < 4; k++)                                                                      // 1 KC = 1 000 VND ladder, ascending
            Assert.Equal(new long[] { 50, 80, 100, 120, 150, 200, 250, 300, 400, 500 }, sets.Where(s => s.classes[0] == k).Select(s => (long)s.price).OrderBy(p => p).ToArray());
        Assert.All(sets, s => { Assert.NotNull(s.skill); Assert.False(string.IsNullOrWhiteSpace(s.skill!.name)); Assert.True(s.skill.damagePct + s.skill.critPct + s.skill.reducePct + s.skill.moveSpeedPct > 0); });
        // layered slots: tops, bottoms, wings and mounts exist (hats and masks were removed from the game) (class-free) and mounts / some wings carry a speed bonus
        foreach (string slot in new[] { "top", "bottom", "wings", "mount" })
            Assert.Contains(cat.items, i => i.art == "2d" && i.slot == slot && i.kind == ItemKinds.Cosmetic && i.listed);
        Assert.All(cat.items.Where(i => i.art == "2d" && i.slot == "mount"), m => Assert.True(m.skill!.moveSpeedPct >= 10));
        // A catalog that would sell something for free or with an absurd effect is refused at startup.
        var bad = new CosmeticCatalog { items = new[] { new CatalogItem { id = "x", slot = "head", currency = "gold", price = 0, classes = new[] { 0 } } } };
        Assert.Throws<InvalidDataException>(bad.Validate);
        bad.items[0].price = 5; bad.items[0].skill = new StyleSkill { name = "n", damagePct = 90 };
        Assert.Throws<InvalidDataException>(bad.Validate);
    }

    [Fact]
    public void A_Worn_Set_And_Mount_Give_Capped_Passive_Effects_And_Equip_Is_Class_Checked()
    {
        CosmeticCatalog cat = CosmeticCatalog.Load(CatalogPath);
        // hero is class 1 (Pháp Sư): buy a Mage Gold set, wear it with a mount and read the effect the combat would use
        CatalogItem set = cat.items.First(i => i.art == "2d" && i.kind == ItemKinds.CosmeticSet && i.classes[0] == hero.heroClass && i.price == 50);
        CatalogItem otherClass = cat.items.First(i => i.art == "2d" && i.kind == ItemKinds.CosmeticSet && i.classes[0] != hero.heroClass);
        CatalogItem mount = cat.items.First(i => i.art == "2d" && i.slot == "mount" && i.currency == "gold");
        t.Db.InTransaction((c, tx) => Wallet.Gold(c, tx, acc, hero.characterId, 500000, "test", "test:" + Rid()));
        GiveDiamonds(1000);
        Assert.Equal("class_restricted", Assert.Throws<GameError>(() =>
            shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = otherClass.id, requestId = Rid() })).Code);
        Assert.Equal(StyleBonus.None, shop.StyleOf(hero.characterId));
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = set.id, requestId = Rid(), equip = true });
        StyleBonus sb = shop.StyleOf(hero.characterId);
        Assert.Equal(set.skill!.damagePct, sb.damagePct);
        Assert.Equal(set.skill.critPct, sb.critPct);
        // the set is worn as separate pieces (one per slot), each of which can be taken off or replaced on its own
        OutfitDto worn = shop.Wardrobe(acc, hero.characterId).outfit;
        Assert.Equal(set.id, worn.set);
        foreach (string piece in set.pieces) Assert.Equal(CosmeticShop.PieceId(set.id, piece), worn.slots.Single(s => s.slot == piece).itemId);
        OutfitDto withoutTop = new OutfitDto { set = set.id, slots = worn.slots.Where(s => s.slot != "top").ToArray() };
        shop.Equip(acc, hero.characterId, withoutTop);
        Assert.Equal(StyleBonus.None, shop.StyleOf(hero.characterId));                                   // incomplete set: no skill
        Assert.DoesNotContain(shop.Wardrobe(acc, hero.characterId).outfit.slots, s => s.slot == "top");  // the other pieces stay
        shop.Equip(acc, hero.characterId, worn);
        Assert.Equal(set.skill.critPct, shop.StyleOf(hero.characterId).critPct);                          // whole set again: skill back
        Assert.Equal("slot_mismatch", Assert.Throws<GameError>(() => shop.Equip(acc, hero.characterId,
            new OutfitDto { slots = new[] { new OutfitSlotDto { slot = "mask", itemId = CosmeticShop.PieceId(set.id, "top") } } })).Code);
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = mount.id, requestId = Rid(), equip = true });
        Assert.Equal(set.skill.moveSpeedPct + mount.skill!.moveSpeedPct, shop.StyleOf(hero.characterId).moveSpeedPct);   // set + mount add up (cap 40)
        // taking everything off removes the effects at once (no stale cache)
        shop.Equip(acc, hero.characterId, new OutfitDto());
        Assert.Equal(StyleBonus.None, shop.StyleOf(hero.characterId));
        // the wardrobe shows the skill text for the shop
        Assert.Equal(set.skill.name, shop.Wardrobe(acc, hero.characterId).items.Single(i => i.id == set.id).skillName);
    }
    [Fact]
    public void New_Character_Gets_Starter_Gold_Exactly_Once()
    {
        Assert.Equal(5000, hero.gold);
        for (int i = 0; i < 3; i++) Assert.Equal(5000, chars.Enter(acc, hero.characterId).gold);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM ledger WHERE idempotency_key=$k", ("$k", "starter_gold:" + hero.characterId)));
    }

    [Fact]
    public void Try_On_Is_Free_Purchase_Charges_Server_Price_Grants_And_Equips()
    {
        WardrobeDto before = shop.Wardrobe(acc, hero.characterId);
        ShopItemDto crown = before.items.Single(i => i.id == "head_lotus_crown");
        Assert.False(crown.owned);
        Assert.Equal(5000, before.gold);

        WardrobeDto after = shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = crown.id, requestId = Rid(), equip = true });
        Assert.Equal(5000 - crown.price, after.gold);
        Assert.Contains(crown.id, after.owned);
        Assert.Contains(after.outfit.slots, s => s.slot == "head" && s.itemId == crown.id);
        Assert.True(after.items.Single(i => i.id == crown.id).equipped);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM shop_purchases WHERE character_id=$c", ("$c", hero.characterId)));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM ledger WHERE reason='shop:head_lotus_crown' AND delta=$d", ("$d", -crown.price)));
        Assert.Equal(0, after.diamonds); // a Gold purchase never touches Diamonds
    }

    [Fact]
    public void Insufficient_Gold_Charges_Nothing_Grants_Nothing()
    {
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "back_dragonfly_wings", requestId = Rid() }); // 2500
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "back_wind_cape", requestId = Rid() });       // 2000 -> 500 left
        var e = Assert.Throws<GameError>(() => shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "head_bell_circlet", requestId = Rid() }));
        Assert.Equal("insufficient_gold", e.Code);
        WardrobeDto w = shop.Wardrobe(acc, hero.characterId);
        Assert.Equal(500, w.gold);
        Assert.DoesNotContain("head_bell_circlet", w.owned);
        Assert.Equal(2, Count("SELECT COUNT(*) FROM shop_purchases"));
    }

    [Fact]
    public void Replayed_Request_And_Rapid_Repeat_Clicks_Never_Charge_Twice()
    {
        string rid = Rid();
        var req = new PurchaseRequestDto { itemId = "gloves_jade_silk", requestId = rid };
        WardrobeDto first = shop.Purchase(acc, hero.characterId, req);
        WardrobeDto replay = shop.Purchase(acc, hero.characterId, req); // lost response, client retries
        Assert.True(replay.replayed);
        Assert.Equal(first.purchaseId, replay.purchaseId);
        Assert.Equal(first.gold, replay.gold);
        // A second click with a fresh id for the same item is refused, not charged.
        var e = Assert.Throws<GameError>(() => shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "gloves_jade_silk", requestId = Rid() }));
        Assert.Equal("already_owned", e.Code);
        // Same id reused for another item is a conflict.
        Assert.Equal("request_reused", Assert.Throws<GameError>(() =>
            shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "shoes_cloud_boots", requestId = rid })).Code);
        Assert.Equal(5000 - 700, shop.Wardrobe(acc, hero.characterId).gold);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM owned_items WHERE item_id='gloves_jade_silk'"));
    }

    [Fact]
    public async Task Concurrent_Purchases_Of_One_Item_Charge_Once()
    {
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            try { shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "shoes_ember_boots", requestId = Rid() }); return true; }
            catch (GameError e) when (e.Code == "already_owned") { return false; }
        })).ToArray();
        bool[] ok = await Task.WhenAll(tasks);
        Assert.Equal(1, ok.Count(x => x));
        Assert.Equal(5000 - 1100, shop.Wardrobe(acc, hero.characterId).gold);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM owned_items WHERE item_id='shoes_ember_boots'"));
    }

    [Fact]
    public void Equip_Validates_Ownership_Slot_And_Class_And_Unequip_Works()
    {
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "shoes_cloud_boots", requestId = Rid() });
        Assert.Equal("not_owned", Assert.Throws<GameError>(() => shop.Equip(acc, hero.characterId,
            new OutfitDto { slots = new[] { new OutfitSlotDto { slot = "head", itemId = "head_lotus_crown" } } })).Code);
        Assert.Equal("slot_mismatch", Assert.Throws<GameError>(() => shop.Equip(acc, hero.characterId,
            new OutfitDto { slots = new[] { new OutfitSlotDto { slot = "head", itemId = "shoes_cloud_boots" } } })).Code);
        Assert.Equal("slot_invalid", Assert.Throws<GameError>(() => shop.Equip(acc, hero.characterId,
            new OutfitDto { slots = new[] { new OutfitSlotDto { slot = "jetpack", itemId = "shoes_cloud_boots" } } })).Code);

        WardrobeDto on = shop.Equip(acc, hero.characterId, new OutfitDto { slots = new[] { new OutfitSlotDto { slot = "shoes", itemId = "shoes_cloud_boots" } } });
        Assert.Single(on.outfit.slots);
        WardrobeDto off = shop.Equip(acc, hero.characterId, new OutfitDto());
        Assert.Empty(off.outfit.slots);
        Assert.Contains("shoes_cloud_boots", off.owned); // unequipping never removes ownership
    }

    [Fact]
    public void Another_Account_Can_Neither_Buy_For_Nor_Dress_My_Character()
    {
        string other = accounts.Register("thief0001", "thief-pass-01").AccountId;
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() =>
            shop.Purchase(other, hero.characterId, new PurchaseRequestDto { itemId = "gloves_jade_silk", requestId = Rid() })).Code);
        Assert.Equal("character_not_found", Assert.Throws<GameError>(() => shop.Equip(other, hero.characterId, new OutfitDto())).Code);
    }

    [Fact]
    public void Diamond_Set_Spends_Only_Diamonds_Is_Account_Wide_And_Wears_Over_Gold_Outfit()
    {
        Assert.Equal("insufficient_diamonds", Assert.Throws<GameError>(() =>
            shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "set_crystal_zephyr", requestId = Rid() })).Code);
        GiveDiamonds(400);
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "gloves_jade_silk", requestId = Rid(), equip = true });
        WardrobeDto w = shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "set_crystal_zephyr", requestId = Rid(), equip = true });
        Assert.Equal(100, w.diamonds);
        Assert.Equal(5000 - 700, w.gold); // Gold untouched by the Diamond purchase
        Assert.Equal("set_crystal_zephyr", w.outfit.set);
        Assert.Contains(w.outfit.slots, s => s.itemId == "gloves_jade_silk"); // Gold outfit kept underneath
        // Take the set off -> back to the Gold outfit.
        WardrobeDto off = shop.Equip(acc, hero.characterId, new OutfitDto { set = "", slots = w.outfit.slots });
        Assert.Equal("", off.outfit.set);
        // Account-wide: a second character of the account owns it too.
        CharacterDto alt = chars.Create(acc, "Nhân Vật Phụ", 3, 1);
        Assert.Contains("set_crystal_zephyr", shop.Wardrobe(acc, alt.characterId).owned);
        Assert.DoesNotContain("gloves_jade_silk", shop.Wardrobe(acc, alt.characterId).owned); // Gold pieces are per character
    }

    [Fact]
    public void Outfit_Survives_Restart_And_Comes_Back_On_Enter()
    {
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "back_wind_cape", requestId = Rid(), equip = true });
        shop.Purchase(acc, hero.characterId, new PurchaseRequestDto { itemId = "head_lotus_crown", requestId = Rid(), equip = true });
        // "Restart": brand-new services over the same database file.
        var chars2 = new CharacterService(new Database(t.Path_), new NameRules(), 5000);
        CharacterDto back = chars2.Enter(acc, hero.characterId);
        Assert.Equal(new[] { "head", "back" }, back.outfit.slots.Select(s => s.slot).ToArray());
        Assert.Equal(5000 - 2000 - 1200, back.gold);
    }
}

public class CosmeticHttpTests : IDisposable
{
    private readonly TestDb t = new();
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient http;

    public CosmeticHttpTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("AdventureKing:DatabasePath", t.Path_));
        http = factory.CreateClient();
    }

    public void Dispose() { http.Dispose(); factory.Dispose(); t.Dispose(); }

    private sealed class Session { public string token { get; set; } = ""; }

    [Fact]
    public async Task Http_Purchase_Equip_And_Profile_Roundtrip()
    {
        var reg = await http.PostAsJsonAsync("/api/v1/auth/register", new { account = "shophttp1", password = "shop-pass-001" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await reg.Content.ReadFromJsonAsync<Session>())!.token);
        CharacterDto ch = (await (await http.PostAsJsonAsync("/api/v1/characters", new { characterName = "Mua Sắm", heroClass = 2, slotIndex = 0 }))
            .Content.ReadFromJsonAsync<CharacterDto>())!;
        Assert.Equal(5000, ch.gold); // production starter Gold (current PROPOSAL)

        var wardrobe = await http.GetFromJsonAsync<WardrobeDto>($"/api/v1/characters/{ch.characterId}/wardrobe");
        Assert.Contains(wardrobe!.items, i => i.id == "acc_silver_bell" && i.price == 600 && i.currency == "gold");

        var buy = await http.PostAsJsonAsync($"/api/v1/characters/{ch.characterId}/wardrobe/purchase",
            new { itemId = "acc_silver_bell", requestId = Guid.NewGuid().ToString("N"), equip = true });
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);
        Assert.Equal(ch.gold - 600, (await buy.Content.ReadFromJsonAsync<WardrobeDto>())!.gold);

        var forged = await http.PostAsJsonAsync($"/api/v1/characters/{ch.characterId}/wardrobe/equip",
            new { set = "", slots = new[] { new { slot = "back", itemId = "back_dragonfly_wings" } } });
        Assert.Equal(HttpStatusCode.Conflict, forged.StatusCode);

        CharacterDto entered = (await (await http.PostAsync($"/api/v1/characters/{ch.characterId}/enter", null)).Content.ReadFromJsonAsync<CharacterDto>())!;
        Assert.Equal("acc_silver_bell", Assert.Single(entered.outfit.slots).itemId);
    }
}
