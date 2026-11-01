using DocPoint.Domain;

namespace DocPoint.Application;

/// <param name="Text">Câu người dùng gõ; cụm trong ngoặc kép phải đứng liền nhau.</param>
/// <param name="Extensions">Chỉ lấy các loại file này (".docx"); null là mọi loại.</param>
public sealed record SearchQuery(
    string Text,
    long? FolderId = null,
    IReadOnlyList<string>? Extensions = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null,
    int Limit = 200);

public interface ISearchIndex
{
    Task<IReadOnlyList<NodeMatch>> SearchAsync(SearchQuery query, CancellationToken ct = default);

    /// <summary>Tên tài liệu và tiêu đề khớp tiền tố, tối đa <paramref name="max"/> gợi ý.</summary>
    Task<IReadOnlyList<string>> SuggestAsync(string prefix, int max = 8, CancellationToken ct = default);
}
