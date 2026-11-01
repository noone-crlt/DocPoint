using DocPoint.Domain;

namespace DocPoint.Application;

public sealed record ScanProgress(int Done, int Total, string? CurrentFile);

public interface IFolderScanner
{
    /// <summary>Đối chiếu thư mục với chỉ mục: file mới thì đọc, file đổi thì đọc lại, file mất thì xoá.</summary>
    Task ScanAsync(WatchedFolder folder, IProgress<ScanProgress>? progress = null, CancellationToken ct = default);
}

public enum FileChangeKind { Created, Changed, Deleted, Renamed }

public sealed record FileChange(long FolderId, FileChangeKind Kind, string Path, string? OldPath = null);

public interface IFileWatcher : IDisposable
{
    /// <summary>Đã gom các sự kiện dồn dập của cùng một file thành một.</summary>
    event Action<FileChange>? Changed;

    void Watch(WatchedFolder folder);

    void Stop(long folderId);
}
