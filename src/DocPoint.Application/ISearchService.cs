using DocPoint.Domain;

namespace DocPoint.Application;

public interface ISearchService
{
    /// <summary>Gợi ý cho ô tìm kiếm: câu đã tìm gần đây, tên tài liệu.</summary>
    Task<IReadOnlyList<string>> GetSuggestionsAsync(CancellationToken ct = default);

    /// <summary>Gợi ý khi đang gõ: tên tài liệu và tiêu đề khớp phần đã gõ.</summary>
    Task<IReadOnlyList<string>> SuggestAsync(string prefix, CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct = default);
}
