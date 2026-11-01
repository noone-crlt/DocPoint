using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.App.Services.Fakes;

public sealed class FakeFolderWatchService : IFolderWatchService
{
    public bool IsReading => true;

    public ScanProgress? Progress => new(1200, 2418, "Quy chế nhân sự 2026.docx");

    public event EventHandler? StatusChanged { add { } remove { } }

    public Task<IReadOnlyList<WatchedFolder>> GetFoldersAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<WatchedFolder>>([]);

    public Task<WatchedFolder> AddFolderAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(new WatchedFolder { Id = 1, Path = path });

    public Task RemoveFolderAsync(long folderId, CancellationToken ct = default) => Task.CompletedTask;

    public void ScanAll()
    {
    }

    public void Pause()
    {
    }

    public void Resume()
    {
    }
}
