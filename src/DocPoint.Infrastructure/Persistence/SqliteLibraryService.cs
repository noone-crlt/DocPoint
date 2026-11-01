using DocPoint.Application;

namespace DocPoint.Infrastructure.Persistence;

/// <summary>Bản thật của <see cref="ILibraryService"/>. Mới có phần đếm; duyệt, sắp xếp và lọc làm ở F9.</summary>
public sealed class SqliteLibraryService(DocPointDatabase db) : ILibraryService
{
    public Task<int> CountDocumentsAsync(CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            // Chỉ tính tài liệu đã đọc được: file lỗi nằm ở mục "Cần bạn xem", không nằm trong thư viện.
            cmd.CommandText = "SELECT COUNT(*) FROM documents WHERE status IN (1, 7)";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }, ct);
}
