namespace DocPoint.Domain;

/// <summary>
/// Kết quả tìm: một node cùng điểm, đoạn trích và các vị trí khớp. (Tên "SearchHit" trong kế hoạch đã
/// được lớp DTO của giao diện dùng, nên lớp này tên NodeMatch.)
/// </summary>
/// <param name="TitleMatches">Đoạn khớp trong <see cref="Node.Title"/>.</param>
/// <param name="Matches">Các đoạn [Start, Start+Length) trong <see cref="Node.Content"/> cần tô vàng.</param>
public sealed record NodeMatch(
    Node Node,
    Document Document,
    double Score,
    string Snippet,
    IReadOnlyList<TextRange> Matches,
    IReadOnlyList<TextRange>? TitleMatches = null);

public readonly record struct TextRange(int Start, int Length);
