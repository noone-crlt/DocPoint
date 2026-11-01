using System.Text.RegularExpressions;
using System.Threading.Channels;
using DocPoint.Domain;

namespace DocPoint.Application;

/// <param name="MaxFileSizeBytes">File lớn hơn không được đọc, chỉ ghi nhận là "quá lớn".</param>
/// <param name="MaxParallelism">Số luồng đọc và tách; luôn chỉ có một luồng ghi.</param>
public sealed record ScanOptions(long MaxFileSizeBytes = 100L * 1024 * 1024, int? MaxParallelism = null)
{
    public int Parallelism => MaxParallelism ?? Math.Max(1, Environment.ProcessorCount - 1);
}

/// <summary>
/// Đối chiếu một thư mục với chỉ mục: file mới thì đọc, file đổi (ngày sửa hoặc kích thước) thì đọc lại,
/// file không còn thì xoá khỏi chỉ mục. Nhiều luồng đọc ở mức ưu tiên thấp, một luồng ghi gom theo lô.
/// Dừng giữa chừng rồi quét lại sẽ làm tiếp phần còn thiếu, vì mỗi lô đã ghi là xong.
/// </summary>
public sealed class FolderScanner(DocumentIndexer indexer, IDocumentRepository repository, ScanOptions options) : IFolderScanner
{
    private const int BatchSize = 50;

    public async Task ScanAsync(WatchedFolder folder, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        var existing = (await repository.ListByFolderAsync(folder.Id, ct))
            .ToDictionary(d => d.Path, StringComparer.OrdinalIgnoreCase);

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var work = new List<(FileInfo File, Document? Existing)>();
        var tooLarge = new List<(FileInfo File, Document? Existing)>();

        foreach (var file in Enumerate(folder))
        {
            ct.ThrowIfCancellationRequested();
            found.Add(file.FullName);
            existing.TryGetValue(file.FullName, out var known);

            var unchanged = known is not null && known.Size == file.Length && known.ModifiedAt == file.LastWriteTimeUtc
                && known.Status != DocumentStatus.Pending;
            if (unchanged)
                continue;

            (file.Length > options.MaxFileSizeBytes ? tooLarge : work).Add((file, known));
        }

        foreach (var gone in existing.Values.Where(d => !found.Contains(d.Path)))
            await repository.DeleteAsync(gone.Id, ct);

        if (tooLarge.Count > 0)
        {
            await repository.ReplaceBatchAsync(tooLarge.Select(t => (TooLarge(folder, t.File, t.Existing), (IReadOnlyList<Node>)[])).ToList(), ct);
        }

        await ReadAll(folder, work, progress, ct);
    }

    private async Task ReadAll(WatchedFolder folder, List<(FileInfo File, Document? Existing)> work, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new ScanProgress(0, work.Count, null));
        if (work.Count == 0)
            return;

        var channel = Channel.CreateBounded<(Document Document, IReadOnlyList<Node> Nodes)>(BatchSize);

        // Luồng ghi không nhận CancellationToken: phần đã đọc xong vẫn được ghi trước khi dừng.
        var writer = Task.Run(async () =>
        {
            var batch = new List<(Document, IReadOnlyList<Node>)>();
            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                batch.Add(item);
                if (batch.Count >= BatchSize)
                {
                    await repository.ReplaceBatchAsync(batch.ToList());
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                await repository.ReplaceBatchAsync(batch);
        });

        var done = 0;
        try
        {
            await Parallel.ForEachAsync(work,
                new ParallelOptions { MaxDegreeOfParallelism = options.Parallelism, CancellationToken = ct },
                async (item, token) =>
                {
                    // Ưu tiên thấp để giao diện không bị giật; luồng của thread pool tự về mức thường sau mỗi việc.
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    if (!indexer.CanIndex(item.File.FullName))
                        return;

                    var prepared = await indexer.PrepareAsync(folder.Id, item.File.FullName, item.Existing, token);
                    await channel.Writer.WriteAsync(prepared, token);
                    progress?.Report(new ScanProgress(Interlocked.Increment(ref done), work.Count, item.File.Name));
                });
        }
        finally
        {
            channel.Writer.Complete();
            await writer;
        }
    }

    private IEnumerable<FileInfo> Enumerate(WatchedFolder folder)
    {
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = folder.IncludeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        var ignore = folder.IgnorePatterns.Select(Wildcard).ToList();
        var root = new DirectoryInfo(folder.Path);

        foreach (var file in root.EnumerateFiles("*", enumeration))
        {
            if (file.Name.StartsWith("~$", StringComparison.Ordinal) || file.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!indexer.CanIndex(file.FullName))
                continue;

            var relative = Path.GetRelativePath(folder.Path, file.FullName);
            if (ignore.Count > 0 && relative.Split(Path.DirectorySeparatorChar).Any(part => ignore.Any(rx => rx.IsMatch(part))))
                continue;

            yield return file;
        }
    }

    private Document TooLarge(WatchedFolder folder, FileInfo file, Document? existing)
    {
        var document = existing ?? new Document { Path = file.FullName };
        document.FolderId = folder.Id;
        document.FileName = file.Name;
        document.Extension = file.Extension.ToLowerInvariant();
        document.ModifiedAt = file.LastWriteTimeUtc;
        document.Size = file.Length;
        document.Status = DocumentStatus.TooLarge;
        document.Error = $"\"{file.Name}\" lớn hơn {options.MaxFileSizeBytes / (1024 * 1024)} MB nên chưa được đọc. Có thể tăng giới hạn trong Cài đặt.";
        return document;
    }

    private static Regex Wildcard(string pattern) =>
        new("^" + Regex.Escape(pattern.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
