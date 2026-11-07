using System.Globalization;
using System.Text;

namespace AdventureKing.Server.Domain;

public sealed record NameCheck(bool Ok, string Code, string Message, string Normalized, string UniqueKey);

/// <summary>
/// Server-side character name policy (Part 5 of the spec).
///  - NFC; Vietnamese/Latin letters, digits and single spaces only (any other script is rejected,
///    which removes Cyrillic/Greek/fullwidth homoglyph attacks outright).
///  - 3–16 visible characters, must start with a letter, at most 2 combining marks per letter
///    (Vietnamese never needs more), no zero-width / control characters.
///  - Reserved: GM / Admin / staff / publisher impersonation, checked on a folded "skeleton" that
///    strips diacritics, case, spaces, leetspeak (0→o, 1/l/|→i, 3→e, 4→a, 5→s, 7→t) and repeats.
///  - Profanity / hate: unambiguous terms on the skeleton; Vietnamese words whose plain-ASCII
///    spelling is also an innocent word (e.g. "lồn" vs "lớn") are matched with their diacritics.
///  - Uniqueness key keeps Vietnamese diacritics (Hùng ≠ Hưng) but folds case, spaces and leet look-alikes
///    so "B0ss" cannot sit next to "Boss".
/// The default lists can be extended with Server/config/name_blocklist.txt (one term per line).
/// </summary>
public sealed class NameRules
{
    public const int MinLength = 3, MaxLength = 16;

    // Combining marks used by Vietnamese (and common Latin accents): grave, acute, circumflex, tilde, breve, hook, horn, dot below.
    private static readonly HashSet<char> AllowedMarks = new() { '̀', '́', '̂', '̃', '̆', '̉', '̛', '̣' };

    private static readonly string[] ReservedContains = FoldAll(
        "admin", "moderator", "gamemaster", "quantri", "banquantri",
        "nhaphathanh", "adventureking", "chamsockhachhang", "hotrokhachhang", "official", "chinhthuc",
        "nhanvien", "developer", "staff", "support", "hethong", "system");
    // Reserved prefixes/suffixes/words (so "Pigment" is fine, "GMHung" / "Hung GM" are not).
    private static readonly string[] ReservedAffix = FoldAll("gm", "bqt", "cskh", "hotro", "npc");
    // Reserved as a whole word only ("Modric", "Devin" stay allowed; "Mod Linh", "Dev" are not).
    private static readonly string[] ReservedWord = FoldAll("mod", "dev", "sys", "op", "gamemod");

    // Matched against the folded skeleton (substring). Terms are folded with the same Skeleton() at startup.
    private static readonly string[] ProfanityContains = FoldAll(
        "fuck", "fuk", "shit", "bitch", "cunt", "nigger", "nigga", "faggot", "whore", "slut",
        "hitler", "nazi", "porn", "pussy", "asshole", "dick", "penis", "vagina", "sex",
        "ditme", "ditcon", "dume", "duma", "dumay", "occho", "occac", "lonme", "cacme", "buoime",
        "dauboi", "matday", "chetme", "thangcho", "khonnan", "ngulon", "phandong", "giethet", "tusat");
    // Ambiguous terms that would hit innocent names as substrings (e.g. "Grape"): prefix, suffix or whole word.
    private static readonly string[] ProfanityAffix = FoldAll("concho", "clgt", "dmcs");
    // Short/ambiguous slang: only as a whole word or the whole name (uncollapsed skeleton, so "dmm" stays "dmm";
    // "Grape" / "Caveman" stay allowed).
    private static readonly string[] ProfanityWord =
        new[] { "dm", "dmm", "dcm", "dkm", "vcl", "vkl", "vcc", "cl", "clm", "cc", "kkk", "đm", "đmm", "đcm", "rape", "cave", "isis" }
            .Select(t => Skeleton(t, collapseRepeats: false)).Distinct().ToArray();

    private static string[] FoldAll(params string[] terms) =>
        terms.Select(t => Skeleton(t, collapseRepeats: true)).Distinct().ToArray();

    private static bool HitsAffix(IEnumerable<string> terms, string skeleton, string[] tokens) =>
        terms.Any(r => skeleton.StartsWith(r) || skeleton.EndsWith(r) || tokens.Contains(r));
    // Vietnamese terms matched WITH diacritics (lower-case NFC, spaces removed) to avoid hitting innocent words.
    private static readonly string[] ProfanityAccented = { "lồn", "cặc", "buồi", "địt", "đụ", "đéo", "đĩ", "đếch", "cứt", "dái", "nứng", "đần" };

    private readonly List<string> extraSkeleton = new();

    public NameRules(string? extraBlocklistPath = null)
    {
        if (extraBlocklistPath != null && File.Exists(extraBlocklistPath))
            foreach (string line in File.ReadAllLines(extraBlocklistPath, Encoding.UTF8))
            {
                string t = line.Trim();
                if (t.Length >= 2 && !t.StartsWith('#')) extraSkeleton.Add(Skeleton(t, collapseRepeats: true));
            }
    }

    public NameCheck Check(string? raw)
    {
        string name = (raw ?? "").Normalize(NormalizationForm.FormC).Trim();
        NameCheck Fail(string code, string msg) => new(false, code, msg, name, "");

        if (name.Length == 0) return Fail("empty", "Hãy nhập tên nhân vật.");
        foreach (char ch in name)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned || (char.IsWhiteSpace(ch) && ch != ' '))
                return Fail("invalid_char", "Tên chứa ký tự ẩn hoặc không hợp lệ.");
        }
        if (name.Contains("  ")) return Fail("spaces", "Không dùng nhiều khoảng trắng liên tiếp.");

        int visible = new StringInfo(name).LengthInTextElements;
        if (visible < MinLength || visible > MaxLength)
            return Fail("length", $"Tên phải từ {MinLength} đến {MaxLength} ký tự.");

        // Script check on the decomposed form: base must be ASCII letter/digit/space or đ/Đ, marks from the Vietnamese set.
        string decomposed = name.Normalize(NormalizationForm.FormD);
        int marksOnCurrent = 0;
        foreach (char ch in decomposed)
        {
            if (AllowedMarks.Contains(ch))
            {
                if (++marksOnCurrent > 2) return Fail("marks", "Tên có quá nhiều dấu chồng lên nhau.");
                continue;
            }
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                return Fail("script", "Chỉ dùng chữ cái tiếng Việt, chữ số và khoảng trắng.");
            marksOnCurrent = 0;
            bool ok = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == ' ' || ch == 'đ' || ch == 'Đ';
            if (!ok) return Fail("script", "Chỉ dùng chữ cái tiếng Việt, chữ số và khoảng trắng.");
        }
        if (!char.IsLetter(name[0])) return Fail("start", "Tên phải bắt đầu bằng chữ cái.");
        if (name.Count(char.IsDigit) > 6) return Fail("digits", "Tên có quá nhiều chữ số.");

        string skeleton = Skeleton(name, collapseRepeats: true);
        string[] tokens = name.Split(' ').Select(t => Skeleton(t, collapseRepeats: true)).Where(t => t.Length > 0).ToArray();
        string rawSkeleton = Skeleton(name, collapseRepeats: false);
        string[] rawTokens = name.Split(' ').Select(t => Skeleton(t, collapseRepeats: false)).Where(t => t.Length > 0).ToArray();

        if (ReservedContains.Any(skeleton.Contains) || HitsAffix(ReservedAffix, skeleton, tokens) ||
            ReservedWord.Any(w => tokens.Contains(w) || skeleton == w))
            return Fail("reserved", "Tên không được giả mạo GM, Admin, nhân viên hoặc nhà phát hành.");

        string accented = name.ToLowerInvariant().Replace(" ", "");
        if (ProfanityContains.Any(skeleton.Contains) || extraSkeleton.Any(skeleton.Contains) ||
            HitsAffix(ProfanityAffix, skeleton, tokens) ||
            ProfanityWord.Any(w => rawTokens.Contains(w) || rawSkeleton == w) ||
            ProfanityAccented.Any(accented.Contains))
            return Fail("offensive", "Tên chứa từ ngữ không phù hợp. Hãy chọn tên khác.");

        return new NameCheck(true, "ok", "", name, UniqueKey(name));
    }

    /// <summary>Uniqueness key: diacritics kept, case/spaces/leet look-alikes folded.</summary>
    public static string UniqueKey(string name)
    {
        var sb = new StringBuilder();
        foreach (char ch in name.Normalize(NormalizationForm.FormC).ToLowerInvariant())
        {
            if (ch == ' ') continue;
            sb.Append(FoldLeet(ch));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Aggressive fold for filter matching: no diacritics, đ→d, leet folded, rn→m, vv→w, optional repeat collapse.</summary>
    public static string Skeleton(string text, bool collapseRepeats)
    {
        var sb = new StringBuilder();
        foreach (char ch in text.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark || ch == ' ') continue;
            sb.Append(ch == 'đ' ? 'd' : FoldLeet(ch));
        }
        string s = sb.ToString().Replace("rn", "m").Replace("vv", "w");
        if (!collapseRepeats) return s;
        var c = new StringBuilder();
        foreach (char ch in s)
            if (c.Length == 0 || c[^1] != ch) c.Append(ch);
        return c.ToString();
    }

    private static char FoldLeet(char ch) => ch switch
    {
        '0' => 'o',
        '1' or 'l' or '|' => 'i',
        '3' => 'e',
        '4' => 'a',
        '5' => 's',
        '7' => 't',
        '8' => 'b',
        _ => ch,
    };
}
