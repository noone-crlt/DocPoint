using DocPoint.Domain;

namespace DocPoint.Application;

public interface IDocumentRepository
{
    /// <summary>
    /// Thêm tài liệu, hoặc thay toàn bộ node của tài liệu đã có cùng đường dẫn, trong một transaction.
    /// Giữ nguyên id của tài liệu cũ để ghim và lịch sử không mất. Node đi theo thứ tự đọc,
    /// cha trỏ bằng <see cref="Node.ParentOrdinal"/>.
    /// </summary>
    Task<Document> ReplaceAsync(Document document, IReadOnlyList<Node> nodes, CancellationToken ct = default);

    /// <summary>Ghi nhiều tài liệu trong một transaction (quét lần đầu).</summary>
    Task ReplaceBatchAsync(IReadOnlyList<(Document Document, IReadOnlyList<Node> Nodes)> items, CancellationToken ct = default);

    Task<Document?> GetByPathAsync(string path, CancellationToken ct = default);

    Task<IReadOnlyList<Document>> ListByFolderAsync(long folderId, CancellationToken ct = default);

    /// <summary>Đọc lại cây node theo đúng thứ tự đọc.</summary>
    Task<IReadOnlyList<Node>> GetNodesAsync(long documentId, CancellationToken ct = default);

    /// <summary>Xoá tài liệu cùng toàn bộ node của nó.</summary>
    Task DeleteAsync(long documentId, CancellationToken ct = default);
}
