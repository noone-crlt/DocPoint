using DocPoint.Domain;

namespace DocPoint.Application;

/// <summary>
/// Điều phối việc đọc nền: thêm, bỏ thư mục theo dõi và quét đối chiếu từng thư mục lần lượt.
/// Theo dõi thay đổi theo thời gian thực (FileSystemWatcher) là việc của F6.
/// </summary>
public sealed class FolderWatchService(IFolderRepository folders, IFolderScanner scanner, IClock clock) : IFolderWatchService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task _running = Task.CompletedTask;
    private bool _rescanRequested;
    private bool _active;

    public bool IsReading { get; private set; }

    public ScanProgress? Progress { get; private set; }

    public event EventHandler? StatusChanged;

    public Task<IReadOnlyList<WatchedFolder>> GetFoldersAsync(CancellationToken ct = default) => folders.ListAsync(ct);

    public async Task<WatchedFolder> AddFolderAsync(string path, CancellationToken ct = default)
    {
        var folder = await folders.AddAsync(path, ct: ct);
        if (folder.AutoIndex)
            ScanAll();
        return folder;
    }

    public async Task RemoveFolderAsync(long folderId, CancellationToken ct = default)
    {
        // Dừng việc đang đọc để không ghi thêm vào thư mục sắp bỏ, xoá xong thì đọc tiếp các thư mục còn lại.
        Task running;
        lock (_gate)
        {
            _cts?.Cancel();
            running = _running;
        }

        await running.ConfigureAwait(false);
        await folders.RemoveAsync(folderId, ct);
        ScanAll();
    }

    public void ScanAll()
    {
        lock (_gate)
        {
            if (_active)
            {
                // Đang đọc: thư mục vừa thêm sẽ được đọc ở vòng sau thay vì bị bỏ sót.
                _rescanRequested = true;
                return;
            }

            _active = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetReading(true);
            _running = Task.Run(() => RunAsync(token));
        }
    }

    public void Pause()
    {
        lock (_gate)
            _cts?.Cancel();
    }

    public void Resume() => ScanAll();

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            do
            {
                foreach (var folder in await folders.ListAsync(ct))
                {
                    if (!folder.AutoIndex)
                        continue;

                    await folders.SetStatusAsync(folder.Id, "Đang đọc", null);
                    try
                    {
                        await scanner.ScanAsync(folder, new Reporter(this), ct);
                        await folders.SetStatusAsync(folder.Id, "", clock.Now);
                    }
                    catch (OperationCanceledException)
                    {
                        await folders.SetStatusAsync(folder.Id, "Đã tạm dừng", null);
                        throw;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        await folders.SetStatusAsync(folder.Id, "Không đọc được thư mục. Hãy kiểm tra ổ đĩa hoặc quyền truy cập.", null);
                    }
                }
            }
            while (TakeRescanRequest());
        }
        catch (OperationCanceledException)
        {
            // Người dùng tạm dừng: không phải lỗi. Bản quét kế tiếp (Resume) bắt đầu từ chỗ đã ghi.
            EndRun();
        }
        catch
        {
            EndRun();
            throw;
        }
        finally
        {
            Progress = null;
            SetReading(false);
        }
    }

    private void EndRun()
    {
        lock (_gate)
        {
            _active = false;
            _rescanRequested = false;
        }
    }

    // Quyết định tiếp tục hay dừng nằm trong cùng một khoá với cờ _active, nên không có khe hở để một yêu cầu quét bị rơi mất.
    private bool TakeRescanRequest()
    {
        lock (_gate)
        {
            if (_rescanRequested)
            {
                _rescanRequested = false;
                return true;
            }

            _active = false;
            return false;
        }
    }

    private void SetReading(bool value)
    {
        IsReading = value;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Reporter(FolderWatchService owner) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value)
        {
            owner.Progress = value;
            // Báo thưa thôi: mỗi 10 file, đầu và cuối.
            if (value.Done % 10 == 0 || value.Done == value.Total)
                owner.StatusChanged?.Invoke(owner, EventArgs.Empty);
        }
    }
}
