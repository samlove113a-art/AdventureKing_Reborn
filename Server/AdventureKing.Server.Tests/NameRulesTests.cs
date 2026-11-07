using AdventureKing.Server.Domain;

namespace AdventureKing.Server.Tests;

public class NameRulesTests
{
    private readonly NameRules rules = new();

    [Theory]
    [InlineData("Hùng Dũng")]
    [InlineData("Nguyễn Văn An")]
    [InlineData("Đặng Thảo")]
    [InlineData("Lớn")]          // innocent word that shares a skeleton with a profanity
    [InlineData("Grape")]        // contains "rape" as a substring
    [InlineData("Pigment")]      // contains "gm" in the middle
    [InlineData("Clara")]        // starts with short slang "cl"
    [InlineData("Modric")]       // starts with "mod"
    [InlineData("Devin")]
    [InlineData("Kiếm Sĩ 99")]
    [InlineData("Ánh Tuyết")]
    public void Accepts_Legitimate_Vietnamese_And_Latin_Names(string name)
    {
        NameCheck r = rules.Check(name);
        Assert.True(r.Ok, $"{name}: {r.Code} {r.Message}");
    }

    [Theory]
    [InlineData("ab", "length")]
    [InlineData("Một Hai Ba Bốn Năm", "length")]   // 18 characters
    [InlineData("9Lives", "start")]
    [InlineData("Hùng  Dũng", "spaces")]
    [InlineData("Hu​ng", "invalid_char")]       // zero-width space
    [InlineData("Ｈｕｎｇ", "script")]              // fullwidth letters
    [InlineData("Нung", "script")]                  // Cyrillic Н homoglyph
    [InlineData("Ηung", "script")]                  // Greek Eta homoglyph
    [InlineData("Hú̀̃ng", "marks")]  // zalgo-style stacked marks
    [InlineData("Hùng★", "script")]
    public void Rejects_Malformed_Or_Lookalike_Names(string name, string code)
    {
        NameCheck r = rules.Check(name);
        Assert.False(r.Ok, name);
        Assert.Equal(code, r.Code);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Adm1n")]
    [InlineData("Vip 4dmin")]
    [InlineData("Ádmin")]
    [InlineData("GM Hùng")]
    [InlineData("GMHung")]
    [InlineData("HungGM")]
    [InlineData("Hùng Gm")]
    [InlineData("Mod Linh")]
    [InlineData("Quản Trị Viên")]
    [InlineData("Nhà Phát Hành")]
    [InlineData("AdventureKing")]
    [InlineData("Hỗ Trợ Game")]
    public void Rejects_Staff_Impersonation(string name)
    {
        NameCheck r = rules.Check(name);
        Assert.False(r.Ok, name);
        Assert.Equal("reserved", r.Code);
    }

    [Theory]
    [InlineData("Fuck")]
    [InlineData("Fuuuuck")]
    [InlineData("F u c k")]
    [InlineData("Sh1t Lord")]
    [InlineData("Hitler")]
    [InlineData("H1tl3r")]
    [InlineData("N1gger")]
    [InlineData("Đ1tme")]
    [InlineData("Ditmemay")]
    [InlineData("Thằng Lồn")]
    [InlineData("Cặc")]
    [InlineData("Vcl")]
    [InlineData("Dmm Hùng")]
    [InlineData("Rape")]
    public void Rejects_Profanity_And_Hate(string name)
    {
        NameCheck r = rules.Check(name);
        Assert.False(r.Ok, name);
        Assert.Equal("offensive", r.Code);
    }

    [Fact]
    public void Unique_Key_Folds_Lookalikes_But_Keeps_Vietnamese_Tones()
    {
        Assert.Equal(NameRules.UniqueKey("Boss"), NameRules.UniqueKey("B0ss"));
        Assert.Equal(NameRules.UniqueKey("Linh Chi"), NameRules.UniqueKey("LinhChi"));
        Assert.Equal(NameRules.UniqueKey("Linh"), NameRules.UniqueKey("1inh"));
        Assert.NotEqual(NameRules.UniqueKey("Hùng"), NameRules.UniqueKey("Hưng"));
        // Decomposed input (e.g. from a different keyboard) maps to the same key as precomposed.
        Assert.Equal(NameRules.UniqueKey("Hùng"), NameRules.UniqueKey("Hùng"));
    }

    [Fact]
    public void Extra_Blocklist_File_Extends_Filter()
    {
        string path = Path.Combine(Path.GetTempPath(), "ak_block_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllLines(path, new[] { "# owner terms", "xauxa" });
        try
        {
            Assert.False(new NameRules(path).Check("Xấu Xa").Ok);
            Assert.True(new NameRules().Check("Xấu Xa").Ok);
        }
        finally { File.Delete(path); }
    }
}
