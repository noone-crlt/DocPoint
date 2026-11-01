using System.Diagnostics;
using DocPoint.Application;
using DocPoint.Domain;
using DocPoint.Infrastructure;
using DocPoint.Infrastructure.Persistence;
using DocPoint.Infrastructure.Readers;
using DocPoint.Infrastructure.Search;
using DocPoint.Tests.Support;

namespace DocPoint.Tests;

/// <summary>Cơ sở dữ liệu tạm cùng một thư mục file thật để quét; xoá hết khi xong.</summary>
public sealed class ScanHarness : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "docpoint-scan-" + Guid.NewGuid().ToString("N"));

    public ScanHarness(ScanOptions? options = null)
    {
        Directory.CreateDirectory(Files);
        Database = new TestDatabase();
        Database.Exec("DELETE FROM folders"); // bỏ thư mục mẫu mà TestDatabase tạo sẵn
        Folders = new SqliteFolderRepository(Database.Db);
        var indexer = new DocumentIndexer([new DocxReader(), new PdfReader(), new TextFileReader()], Database.Repository);
        Scanner = new FolderScanner(indexer, Database.Repository, options ?? new ScanOptions());
        Index = new SqliteSearchIndex(Database.Db);
    }

    public string Files => Path.Combine(_root, "files");
    public TestDatabase Database { get; }
    public SqliteFolderRepository Folders { get; }
    public FolderScanner Scanner { get; }
    public SqliteSearchIndex Index { get; }

    public async Task<WatchedFolder> AddFolder(string? path = null)
    {
        Directory.CreateDirectory(path ?? Files);
        return await Folders.AddAsync(path ?? Files);
    }

    public string Write(string relative, string text)
    {
        var path = Path.Combine(Files, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public void CopySample(string name, string? relative = null)
    {
        var target = Path.Combine(Files, relative ?? name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Samples.File(name), target, overwrite: true);
    }

    public Task Scan(WatchedFolder folder, IProgress<ScanProgress>? progress = null) => Scanner.ScanAsync(folder, progress);

    public void Dispose()
    {
        Database.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

public class FolderRepositoryTests
{
    [Fact]
    public async Task Add_RejectsMissingDuplicateNestedAndParentFolders_WithVietnameseMessages()
    {
        using var h = new ScanHarness();
        var parent = await h.AddFolder();
        Directory.CreateDirectory(Path.Combine(h.Files, "con"));

        var missing = await Assert.ThrowsAsync<FolderException>(() => h.Folders.AddAsync(Path.Combine(h.Files, "khong-co")));
        Assert.Contains("Không thấy thư mục", missing.Message);

        var duplicate = await Assert.ThrowsAsync<FolderException>(() => h.Folders.AddAsync(h.Files + Path.DirectorySeparatorChar));
        Assert.Contains("đã được theo dõi", duplicate.Message);

        var nested = await Assert.ThrowsAsync<FolderException>(() => h.Folders.AddAsync(Path.Combine(h.Files, "con")));
        Assert.Contains("nằm trong", nested.Message);

        await h.Folders.RemoveAsync(parent.Id);
        var child = await h.Folders.AddAsync(Path.Combine(h.Files, "con"));
        var container = await Assert.ThrowsAsync<FolderException>(() => h.Folders.AddAsync(h.Files));
        Assert.Contains("chứa", container.Message);
        Assert.NotEqual(0, child.Id);
    }

    [Fact]
    public async Task Remove_DropsItsDocumentsFromTheIndex_ButNotTheFiles()
    {
        using var h = new ScanHarness();
        var folder = await h.AddFolder();
        h.CopySample("quy-che-lam-viec.docx");
        await h.Scan(folder);
        Assert.Equal(1, h.Database.Count("documents"));

        await h.Folders.RemoveAsync(folder.Id);

        Assert.Equal(0, h.Database.Count("documents"));
        Assert.Equal(0, h.Database.Count("nodes"));
        Assert.Equal(0, h.Database.Count("nodes_fts"));
        Assert.True(File.Exists(Path.Combine(h.Files, "quy-che-lam-viec.docx")));
    }
}

public class ScannerTests
{
    [Fact]
    public async Task FirstScan_ReadsSupportedFilesInSubfolders_AndSkipsTheRest()
    {
        using var h = new ScanHarness();
        var folder = await h.AddFolder();
        h.CopySample("quy-che-lam-viec.docx");
        h.CopySample("thong-bao-di-doi-kho.pdf", @"con\thong-bao.pdf");
        h.Write("ghi-chu.txt", "Dòng một\nDòng hai");
        h.Write("~$quy-che.docx", "tệp tạm của Word");
        h.Write("du-lieu.tmp", "tạm");
        h.Write("anh.png", "không hỗ trợ");

        await h.Scan(folder);

        var docs = await h.Database.Repository.ListByFolderAsync(folder.Id);
        Assert.Equal(["ghi-chu.txt", "quy-che-lam-viec.docx", "thong-bao.pdf"], docs.Select(d => d.FileName).OrderBy(n => n));
        Assert.All(docs, d => Assert.Equal(DocumentStatus.Indexed, d.Status));
    }

    [Fact]
    public async Task IgnorePatterns_SkipMatchingFoldersAndFiles()
    {
        using var h = new ScanHarness();
        var folder = await h.AddFolder();
        folder.IgnorePatterns = ["node_modules", "nhap-*"];
        h.Write(@"node_modules\a.txt", "bỏ qua");
        h.Write("nhap-1.txt", "bỏ qua");
        h.Write("giu.txt", "giữ lại");

        await h.Scan(folder);

        var docs = await h.Database.Repository.ListByFolderAsync(folder.Id);
        Assert.Equal(["giu.txt"], docs.Select(d => d.FileName));
    }

    [Fact]
    public async Task SecondScan_ReadsOnlyChangedFiles_AndRemovesDeletedOnes()
    {
        using var h = new ScanHarness();
        var folder = await h.AddFolder();
        var kept = h.Write("giu.txt", "nội dung cũ về hợp đồng");
        var deleted = h.Write("xoa.txt", "sắp bị xoá");
        h.Write("khong-doi.txt", "không đổi");
        await h.Scan(folder);
        var idBefore = (await h.Database.Repository.GetByPathAsync(kept))!.Id;

        File.WriteAllText(kept, "nội dung mới về nghỉ phép");
        File.SetLastWriteTimeUtc(kept, DateTime.UtcNow.AddMinutes(5));
        File.Delete(deleted);
        h.Write("moi.txt", "file mới");

        ScanProgress? last = null;
        await h.Scan(folder, new Progress<ScanProgress>(p => last = p));
        await Task.Delay(50);

        var docs = (await h.Database.Repository.ListByFolderAsync(folder.Id)).Select(d => d.FileName).OrderBy(n => n);
        Assert.Equal(["giu.txt", "khong-doi.txt", "moi.txt"], docs);
        Assert.Equal(idBefore, (await h.Database.Repository.GetByPathAsync(kept))!.Id);
        Assert.Equal(2, last!.Total);
        Assert.Empty(await h.Index.SearchAsync(new SearchQuery("hợp đồng")));
        Assert.NotEmpty(await h.Index.SearchAsync(new SearchQuery("nghỉ phép")));
    }

    [Fact]
    public async Task TooLargeFile_IsRecordedWithAMessage_NotRead()
    {
        using var h = new ScanHarness(new ScanOptions(MaxFileSizeBytes: 10));
        var folder = await h.AddFolder();
        h.Write("lon.txt", "một đoạn văn dài hơn mười byte");

        await h.Scan(folder);

        var doc = (await h.Database.Repository.ListByFolderAsync(folder.Id)).Single();
        Assert.Equal(DocumentStatus.TooLarge, doc.Status);
        Assert.Contains("lớn hơn", doc.Error);
        Assert.Equal(0, h.Database.Count("nodes"));
    }

    [Fact]
    public async Task UnreadableFiles_AreRecordedWithTheirStatus_WithoutStoppingTheScan()
    {
        using var h = new ScanHarness();
        var folder = await h.AddFolder();
        h.CopySample("tai-lieu-scan.pdf");
        h.Write("hong.docx", "không phải file Word");
        h.CopySample("quy-che-lam-viec.docx");

        await h.Scan(folder);

        var docs = (await h.Database.Repository.ListByFolderAsync(folder.Id)).ToDictionary(d => d.FileName);
        Assert.Equal(DocumentStatus.NoText, docs["tai-lieu-scan.pdf"].Status);
        Assert.Equal(DocumentStatus.Corrupt, docs["hong.docx"].Status);
        Assert.Equal(DocumentStatus.Indexed, docs["quy-che-lam-viec.docx"].Status);
    }

    [Fact]
    public async Task CancelledScan_KeepsWhatWasWritten_AndTheNextScanFinishesTheRest()
    {
        using var h = new ScanHarness(new ScanOptions(MaxParallelism: 1));
        var folder = await h.AddFolder();
        for (var i = 0; i < 300; i++)
            h.Write($"f{i:000}.txt", $"Tài liệu số {i} nói về hợp đồng.");

        using var cts = new CancellationTokenSource();
        var progress = new Progress<ScanProgress>(p =>
        {
            if (p.Done >= 120)
                cts.Cancel();
        });

        try
        {
            await h.Scanner.ScanAsync(folder, progress, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        var afterCancel = (await h.Database.Repository.ListByFolderAsync(folder.Id)).Count;
        Assert.InRange(afterCancel, 1, 299);

        await h.Scan(folder);

        Assert.Equal(300, (await h.Database.Repository.ListByFolderAsync(folder.Id)).Count);
    }

    [Fact]
    public async Task FiveThousandFiles_AreReadWithoutErrorsOrHangs()
    {
        using var h = new ScanHarness();
        var folder = await h.AddFolder();
        for (var i = 0; i < 5000; i++)
            h.Write($@"nhom{i % 20:00}\ban-ghi-{i:0000}.txt", $"Bản ghi số {i}\n1. Nội dung\n- Hợp đồng lao động số {i} được ký ngày {i % 28 + 1}/10/2026.");

        var watch = Stopwatch.StartNew();
        await h.Scan(folder);
        watch.Stop();

        Assert.Equal(5000, h.Database.Count("documents"));
        Assert.Equal(0, h.Database.Count("documents WHERE status <> 1"));
        var hits = await h.Index.SearchAsync(new SearchQuery("hợp đồng lao động số 4999"));
        Assert.Equal("ban-ghi-4999.txt", hits[0].Document.FileName);

        // Quét lại khi không có gì đổi phải rất nhanh.
        var again = Stopwatch.StartNew();
        await h.Scan(folder);
        again.Stop();
        Assert.True(again.Elapsed < TimeSpan.FromSeconds(10), $"Quét đối chiếu mất {again.Elapsed.TotalSeconds:F1} giây");
        Assert.True(watch.Elapsed < TimeSpan.FromMinutes(2), $"Quét lần đầu mất {watch.Elapsed.TotalSeconds:F1} giây");
    }
}

public class FolderWatchServiceTests
{
    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 20000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(watch.ElapsedMilliseconds < timeoutMs, "Hết thời gian chờ.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task AddFolder_StartsReadingInTheBackground_AndReportsStatus()
    {
        using var h = new ScanHarness();
        h.CopySample("quy-che-lam-viec.docx");
        h.CopySample("bao-cao-quy-3.docx");
        var service = new FolderWatchService(h.Folders, h.Scanner, new SystemClock());
        var changes = 0;
        service.StatusChanged += (_, _) => Interlocked.Increment(ref changes);

        var folder = await service.AddFolderAsync(h.Files);
        await WaitUntil(() => h.Database.Count("documents") == 2 && !service.IsReading);

        Assert.True(changes >= 2);
        Assert.Null(service.Progress);
        Assert.Single(await service.GetFoldersAsync());
        Assert.NotNull((await service.GetFoldersAsync())[0].LastScanAt);
        Assert.Equal(folder.Id, (await service.GetFoldersAsync())[0].Id);
    }

    [Fact]
    public async Task FolderAddedWhileReading_IsNotMissed()
    {
        using var h = new ScanHarness();
        var other = Path.Combine(Path.GetDirectoryName(h.Files)!, "khac");
        Directory.CreateDirectory(other);
        for (var i = 0; i < 400; i++)
            h.Write($"a{i:000}.txt", $"Tài liệu {i} về hợp đồng.");
        File.WriteAllText(Path.Combine(other, "b.txt"), "Tài liệu ở thư mục thứ hai.");
        var service = new FolderWatchService(h.Folders, h.Scanner, new SystemClock());

        await service.AddFolderAsync(h.Files);
        await service.AddFolderAsync(other); // thêm khi thư mục đầu còn đang đọc
        await WaitUntil(() => h.Database.Count("documents") == 401 && !service.IsReading);

        Assert.Equal(2, (await service.GetFoldersAsync()).Count);
    }

    [Fact]
    public async Task PauseThenResume_FinishesTheWork()
    {
        using var h = new ScanHarness(new ScanOptions(MaxParallelism: 1));
        for (var i = 0; i < 600; i++)
            h.Write($"p{i:000}.txt", $"Tài liệu {i} về nghỉ phép.");
        var service = new FolderWatchService(h.Folders, h.Scanner, new SystemClock());

        await service.AddFolderAsync(h.Files);
        await WaitUntil(() => h.Database.Count("documents") > 0);
        service.Pause();
        await WaitUntil(() => !service.IsReading);
        var paused = h.Database.Count("documents");

        service.Resume();
        await WaitUntil(() => h.Database.Count("documents") == 600 && !service.IsReading);

        Assert.InRange(paused, 1, 600);
        Assert.Equal("", (await service.GetFoldersAsync())[0].Status);
    }

    [Fact]
    public async Task RemoveFolder_StopsReading_AndClearsItsDocuments()
    {
        using var h = new ScanHarness();
        for (var i = 0; i < 200; i++)
            h.Write($"r{i:000}.txt", $"Tài liệu {i}.");
        var service = new FolderWatchService(h.Folders, h.Scanner, new SystemClock());
        var folder = await service.AddFolderAsync(h.Files);

        await service.RemoveFolderAsync(folder.Id);
        await WaitUntil(() => !service.IsReading);

        Assert.Empty(await service.GetFoldersAsync());
        Assert.Equal(0, h.Database.Count("documents"));
        Assert.Equal(0, h.Database.Count("nodes_fts"));
    }
}
