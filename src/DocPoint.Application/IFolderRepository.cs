using DocPoint.Domain;

namespace DocPoint.Application;

/// <summary>Lỗi thao tác với thư mục theo dõi. <see cref="Exception.Message"/> là câu tiếng Việt cho người dùng.</summary>
public sealed class FolderException(string message) : Exception(message);

public interface IFolderRepository
{
    Task<IReadOnlyList<WatchedFolder>> ListAsync(CancellationToken ct = default);

    /// <exception cref="FolderException">Thư mục không có thật, hoặc trùng/lồng với thư mục đã theo dõi.</exception>
    Task<WatchedFolder> AddAsync(string path, bool includeSubfolders = true, CancellationToken ct = default);

    /// <summary>Bỏ theo dõi: toàn bộ tài liệu và node của thư mục này rời khỏi chỉ mục. File thật không bị đụng tới.</summary>
    Task RemoveAsync(long folderId, CancellationToken ct = default);

    Task UpdateAsync(WatchedFolder folder, CancellationToken ct = default);

    Task SetStatusAsync(long folderId, string status, DateTime? scannedAt, CancellationToken ct = default);
}
