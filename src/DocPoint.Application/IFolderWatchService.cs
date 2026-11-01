using DocPoint.Domain;

namespace DocPoint.Application;

public interface IFolderWatchService
{
    /// <summary>Đang có file được đọc trong các thư mục theo dõi.</summary>
    bool IsReading { get; }

    /// <summary>Tiến độ của thư mục đang đọc; null khi không đọc gì.</summary>
    ScanProgress? Progress { get; }

    /// <summary>Phát khi <see cref="IsReading"/> hoặc <see cref="Progress"/> đổi; có thể gọi từ luồng nền.</summary>
    event EventHandler? StatusChanged;

    Task<IReadOnlyList<WatchedFolder>> GetFoldersAsync(CancellationToken ct = default);

    /// <exception cref="FolderException">Thư mục không có thật, hoặc trùng/lồng với thư mục đã theo dõi.</exception>
    Task<WatchedFolder> AddFolderAsync(string path, CancellationToken ct = default);

    Task RemoveFolderAsync(long folderId, CancellationToken ct = default);

    /// <summary>Đối chiếu mọi thư mục theo dõi với chỉ mục, chạy nền. Gọi khi mở app.</summary>
    void ScanAll();

    void Pause();

    void Resume();
}
