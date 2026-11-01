using DocPoint.Domain;

namespace DocPoint.Application;

public interface ISearchService
{
    /// <summary>Gợi ý cho ô tìm kiếm: câu đã tìm gần đây, tên tài liệu.</summary>
    Task<IReadOnlyList<string>> GetSuggestionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct = default);
}
