using System.Diagnostics;
using DocPoint.Application;
using DocPoint.Domain;
using DocPoint.Infrastructure;
using DocPoint.Infrastructure.Persistence;
using DocPoint.Infrastructure.Readers;
using DocPoint.Tests.Support;
using Microsoft.Data.Sqlite;

namespace DocPoint.Tests;

/// <summary>Cơ sở dữ liệu tạm cho mỗi test, đã chạy migration và có sẵn một thư mục theo dõi (id = 1).</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "docpoint-test-" + Guid.NewGuid().ToString("N"));

    public TestDatabase()
    {
        Db = new DocPointDatabase(Path.Combine(_dir, "docpoint.db"));
        Db.Initialize();
        Repository = new SqliteDocumentRepository(Db, new SystemClock());
        Exec("INSERT INTO folders (id, path) VALUES (1, 'C:\\mau')");
    }

    public DocPointDatabase Db { get; }
    public SqliteDocumentRepository Repository { get; }

    public long Count(string table)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)cmd.ExecuteScalar()!;
    }

    public void Exec(string sql)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    public static Document DocumentFor(string path, long folderId = 1) => new()
    {
        FolderId = folderId,
        Path = path,
        FileName = Path.GetFileName(path),
        Extension = Path.GetExtension(path).ToLowerInvariant(),
        ModifiedAt = new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc),
        Size = 1234,
        Hash = "abc",
        Status = DocumentStatus.Indexed,
    };
}

public class RepositoryTests
{
    private static async Task<IReadOnlyList<Node>> ParseSample(string file)
    {
        var path = Samples.File(file);
        var blocks = await new DocxReader().ReadAsync(path);
        return StructureDetector.Detect(blocks);
    }

    [Fact]
    public async Task WriteThenRead_ReturnsTheSameTree()
    {
        using var t = new TestDatabase();
        var nodes = await ParseSample("quy-che-lam-viec.docx");

        var saved = await t.Repository.ReplaceAsync(TestDatabase.DocumentFor(@"C:\mau\quy-che-lam-viec.docx"), nodes);
        var loaded = await t.Repository.GetNodesAsync(saved.Id);

        Assert.Equal(nodes.Count, loaded.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            Assert.Equal(nodes[i].Label, loaded[i].Label);
            Assert.Equal(nodes[i].Kind, loaded[i].Kind);
            Assert.Equal(nodes[i].Content, loaded[i].Content);
            Assert.Equal(nodes[i].ParentOrdinal, loaded[i].ParentOrdinal);
            Assert.Equal(nodes[i].ParentOrdinal is { } p ? loaded[p].Id : null, loaded[i].ParentId);
        }

        var doc = await t.Repository.GetByPathAsync(@"C:\mau\quy-che-lam-viec.docx");
        Assert.Equal(saved.Id, doc!.Id);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc), doc.ModifiedAt);
        Assert.Equal(DocumentStatus.Indexed, doc.Status);
    }

    [Fact]
    public async Task Overwrite_KeepsDocumentId_AndLeavesNoOrphanNodes()
    {
        using var t = new TestDatabase();
        var first = await t.Repository.ReplaceAsync(
            TestDatabase.DocumentFor(@"C:\mau\a.docx"), await ParseSample("quy-che-lam-viec.docx"));
        var firstId = first.Id;

        var second = await t.Repository.ReplaceAsync(
            TestDatabase.DocumentFor(@"C:\mau\a.docx"), await ParseSample("cong-van-so-125.docx"));

        Assert.Equal(firstId, second.Id);
        Assert.Equal(1, t.Count("documents"));
        Assert.Equal(8, t.Count("nodes"));
        Assert.Equal(8, t.Count("nodes_fts"));
        Assert.Equal(8, (await t.Repository.GetNodesAsync(firstId)).Count);
    }

    [Fact]
    public async Task Delete_RemovesNodesAndSearchRows()
    {
        using var t = new TestDatabase();
        var saved = await t.Repository.ReplaceAsync(
            TestDatabase.DocumentFor(@"C:\mau\a.docx"), await ParseSample("quy-che-lam-viec.docx"));

        await t.Repository.DeleteAsync(saved.Id);

        Assert.Equal(0, t.Count("documents"));
        Assert.Equal(0, t.Count("nodes"));
        Assert.Equal(0, t.Count("nodes_fts"));
    }

    [Fact]
    public async Task FailedBatch_RollsBackEverything()
    {
        using var t = new TestDatabase();
        var good = TestDatabase.DocumentFor(@"C:\mau\good.docx");
        var bad = TestDatabase.DocumentFor(@"C:\mau\bad.docx", folderId: 999); // thư mục không tồn tại
        var nodes = await ParseSample("cong-van-so-125.docx");

        await Assert.ThrowsAsync<SqliteException>(() =>
            t.Repository.ReplaceBatchAsync([(good, nodes), (bad, nodes)]));

        Assert.Equal(0, t.Count("documents"));
        Assert.Equal(0, t.Count("nodes"));
        Assert.Equal(0, t.Count("nodes_fts"));
    }

    [Fact]
    public async Task Rename_KeepsIdWhenDocumentIdIsGiven()
    {
        using var t = new TestDatabase();
        var saved = await t.Repository.ReplaceAsync(
            TestDatabase.DocumentFor(@"C:\mau\cu.docx"), await ParseSample("cong-van-so-125.docx"));

        var renamed = TestDatabase.DocumentFor(@"C:\mau\moi.docx");
        renamed.Id = saved.Id;
        await t.Repository.ReplaceAsync(renamed, await ParseSample("cong-van-so-125.docx"));

        Assert.Equal(1, t.Count("documents"));
        Assert.Null(await t.Repository.GetByPathAsync(@"C:\mau\cu.docx"));
        Assert.Equal(saved.Id, (await t.Repository.GetByPathAsync(@"C:\mau\moi.docx"))!.Id);
    }

    [Fact]
    public async Task Throughput_IsAtLeastTenThousandNodesPerSecond()
    {
        using var t = new TestDatabase();
        var nodes = Enumerable.Range(0, 20_000).Select(i => new Node
        {
            Ordinal = i,
            ParentOrdinal = i == 0 ? null : (i - 1) / 10,
            Kind = NodeKind.Paragraph,
            Number = i.ToString(),
            Content = $"Đoạn số {i}: nội dung thử nghiệm về hợp đồng lao động và nghỉ phép hằng năm của nhân viên.",
            Label = $"Đoạn {i}",
        }).ToList();

        var watch = Stopwatch.StartNew();
        await t.Repository.ReplaceAsync(TestDatabase.DocumentFor(@"C:\mau\lon.txt"), nodes);
        watch.Stop();

        var rate = nodes.Count / watch.Elapsed.TotalSeconds;
        Assert.True(rate >= 10_000, $"Chỉ đạt {rate:N0} node/giây");
    }
}
