using System.Text.RegularExpressions;
using DocPoint.Domain;

namespace DocPoint.Infrastructure.Search;

/// <summary>
/// Câu tìm đã chuẩn hoá thành biểu thức MATCH của FTS5. Mọi từ đều ở dạng không dấu (đ thành d);
/// cụm trong ngoặc kép phải đứng liền nhau; từ cuối được khớp tiền tố để tìm khi đang gõ dở.
/// </summary>
internal sealed partial class FtsQuery
{
    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    private FtsQuery()
    {
    }

    /// <summary>Biểu thức cho nội dung và tiêu đề; null nếu câu tìm không có từ nào.</summary>
    public string? Text { get; private init; }

    /// <summary>Biểu thức cho tên file.</summary>
    public string? FileName { get; private init; }

    /// <summary>Cụm các từ rời đứng liền nhau, để cộng điểm cho chỗ khớp liền mạch; null nếu chỉ có một từ.</summary>
    public string? Phrase { get; private init; }

    /// <summary>Các từ người dùng gõ có dấu, viết thường: dùng để cộng điểm cho chỗ khớp đúng dấu.</summary>
    public IReadOnlyList<string> AccentedTerms { get; private init; } = [];

    public static FtsQuery Parse(string input)
    {
        var text = TextNormalizer.Normalize(input ?? "").Trim();
        var endsWithQuote = text.EndsWith('"');
        var items = new List<string>();
        var loose = new List<string>();
        var accented = new List<string>();

        foreach (Match m in Quoted().Matches(text))
        {
            var words = WordsOf(m.Groups[1].Value);
            if (words.Count > 0)
                items.Add("\"" + string.Join(' ', words) + "\"");
            accented.AddRange(Word().Matches(m.Groups[1].Value.ToLowerInvariant()).Select(x => x.Value));
        }

        var rest = Quoted().Replace(text, " ");
        loose.AddRange(WordsOf(rest));
        accented.AddRange(Word().Matches(rest.ToLowerInvariant()).Select(x => x.Value));

        for (var i = 0; i < loose.Count; i++)
        {
            // Từ cuối đang gõ dở thì khớp tiền tố; số thì khớp nguyên ("điều 4" không kéo theo "điều 40").
            var isLast = i == loose.Count - 1 && !endsWithQuote && char.IsLetter(loose[i][0]) && (loose[i].Length >= 2 || loose.Count > 1);
            items.Add("\"" + loose[i] + "\"" + (isLast ? "*" : ""));
        }

        if (items.Count == 0)
            return new FtsQuery();

        var expression = string.Join(" AND ", items);
        return new FtsQuery
        {
            Text = "{title content_plain} : (" + expression + ")",
            FileName = "{file_name} : (" + expression + ")",
            Phrase = loose.Count >= 2 ? "{title content_plain} : \"" + string.Join(' ', loose) + "\"" : null,
            AccentedTerms = accented.Where(a => TextNormalizer.RemoveDiacritics(a) != a).Distinct().ToList(),
        };
    }

    private static List<string> WordsOf(string s) =>
        Word().Matches(TextNormalizer.RemoveDiacritics(s).ToLowerInvariant()).Select(m => m.Value).ToList();
}
