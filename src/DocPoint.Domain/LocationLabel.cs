namespace DocPoint.Domain;

/// <summary>Tạo nhãn vị trí đọc được từ chuỗi node cha, ví dụ "Điểm a, khoản 2, Điều 5".</summary>
public static class LocationLabel
{
    /// <param name="chain">Từ node gốc xuống node lá.</param>
    /// <param name="page">Trang (nếu biết), thêm vào cuối nhãn khi chuỗi chưa có node trang.</param>
    public static string Build(IReadOnlyList<LabelPart> chain, int? page = null)
    {
        var parts = new List<string>(chain.Count + 1);
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var text = chain[i].Text;
            // Đơn vị nhỏ (khoản, điểm, đoạn...) viết thường khi đứng sau một đơn vị khác.
            parts.Add(parts.Count > 0 && chain[i].Kind.IsMinor() ? Lower(text) : text);
        }

        if (page is { } p && !chain.Any(c => c.Kind == NodeKind.Page))
            parts.Add($"trang {p}");

        return string.Join(", ", parts);
    }

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>Một mắt xích của nhãn: loại và chữ hiện ra ("Điều 5", "a", "Trang 7").</summary>
public readonly record struct LabelPart(NodeKind Kind, string Text);

public static class NodeLabelExtensions
{
    /// <summary>Mắt xích nhãn của riêng node này: "Điều 5", "Mục 2.3"; tiêu đề không có số thì dùng chính tiêu đề.</summary>
    public static LabelPart ToLabelPart(this Node node)
    {
        if (node.Kind == NodeKind.Heading && node.Number.Length == 0)
            return new(node.Kind, node.Title.Length > 60 ? node.Title[..60] + "…" : node.Title);

        return new(node.Kind, $"{node.Kind.DisplayName()} {node.Number}");
    }
}
