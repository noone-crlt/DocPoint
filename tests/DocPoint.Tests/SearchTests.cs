using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DocPoint.Application;
using DocPoint.Domain;
using DocPoint.Infrastructure;
using DocPoint.Infrastructure.Readers;
using DocPoint.Infrastructure.Search;
using DocPoint.Tests.Support;

namespace DocPoint.Tests;

/// <summary>Chỉ mục dựng từ thư mục samples/ trong một cơ sở dữ liệu tạm, dùng chung cho các test tìm kiếm.</summary>
public sealed class SampleIndexFixture : IDisposable
{
    public SampleIndexFixture()
    {
        Database = new TestDatabase();
        var indexer = new DocumentIndexer([new DocxReader(), new PdfReader(), new TextFileReader()], Database.Repository);
        foreach (var file in Directory.GetFiles(Samples.Dir).OrderBy(f => f))
        {
            if (indexer.CanIndex(file) && Path.GetFileName(file) != "results.md")
                indexer.IndexAsync(1, file).GetAwaiter().GetResult();
        }

        Index = new SqliteSearchIndex(Database.Db);
        Service = new SqliteSearchService(Index, Database.Repository);
    }

    public TestDatabase Database { get; }
    public SqliteSearchIndex Index { get; }
    public SqliteSearchService Service { get; }

    public void Dispose() => Database.Dispose();
}

public class SearchTests(SampleIndexFixture fixture) : IClassFixture<SampleIndexFixture>
{
    private async Task<IReadOnlyList<NodeMatch>> Search(string text, SearchQuery? template = null) =>
        await fixture.Index.SearchAsync((template ?? new SearchQuery(text)) with { Text = text });

    [Fact]
    public async Task IndexesEverySampleFile_AndMarksTheScanAsUnreadable()
    {
        var docs = await fixture.Database.Repository.ListByFolderAsync(1);

        Assert.Equal(10, docs.Count);
        var scan = docs.Single(d => d.FileName == "tai-lieu-scan.pdf");
        Assert.Equal(DocumentStatus.NoText, scan.Status);
        Assert.Contains("bản scan", scan.Error);
        Assert.All(docs.Where(d => d != scan), d => Assert.True(d.Status is DocumentStatus.Indexed or DocumentStatus.NeedsReview));
    }

    [Fact]
    public async Task QueryWithoutDiacritics_FindsTextWithDiacritics_IncludingLetterD()
    {
        var hits = await Search("nghi khong huong luong");
        Assert.Equal("Điều 4", hits[0].Node.Label.Split(", ")[^1]);

        var withD = await Search("dieu 4 nghi");
        Assert.NotEmpty(withD);
    }

    [Fact]
    public async Task ExactDiacriticsRankAboveStrippedMatches()
    {
        // "phép" (nghỉ phép) và "phép" ở nơi khác: bản có dấu phải đứng trước bản chỉ khớp sau khi bỏ dấu.
        var accented = await Search("nghỉ hằng năm");
        var plain = await Search("nghi hang nam");

        Assert.Equal(accented[0].Node.Id, plain[0].Node.Id);
        Assert.True(accented[0].Score >= plain[0].Score);
    }

    [Fact]
    public async Task QuotedPhrase_MustBeAdjacent()
    {
        var phrase = await Search("\"nghỉ hằng năm\"");
        var scattered = await Search("\"hằng nghỉ năm\"");

        Assert.NotEmpty(phrase);
        Assert.Empty(scattered);
    }

    [Fact]
    public async Task HighlightRanges_PointAtTheMatchedTextInTheOriginal()
    {
        var hit = (await Search("đặt cọc")).First(h => h.Document.FileName == "hop-dong-thue-van-phong.docx");

        var matched = string.Concat(hit.Matches.Select(m => hit.Node.Content.Substring(m.Start, m.Length)));
        Assert.Equal("đặtcọc", matched.Replace(" ", "").ToLowerInvariant());
        Assert.Contains("\u0002đặt\u0003", hit.Snippet);
    }

    [Fact]
    public async Task LastWordMatchesAsPrefix_WhileTyping()
    {
        var hits = await Search("chấm dứt hợp đ");

        Assert.NotEmpty(hits);
    }

    [Fact]
    public async Task FileNameMatch_ReturnsTheDocument_EvenWhenNoTextMatches()
    {
        var hits = await Search("giao trinh co so du lieu");

        Assert.Contains(hits, h => h.Document.FileName == "giao-trinh-co-so-du-lieu.docx");
    }

    [Fact]
    public async Task Filters_NarrowByExtensionAndFolder()
    {
        var pdfOnly = await Search("an toan", new SearchQuery("", Extensions: [".pdf"]));
        Assert.NotEmpty(pdfOnly);
        Assert.All(pdfOnly, h => Assert.Equal(".pdf", h.Document.Extension));

        var otherFolder = await Search("an toan", new SearchQuery("", FolderId: 42));
        Assert.Empty(otherFolder);

        var future = await Search("an toan", new SearchQuery("", ModifiedFrom: DateTime.UtcNow.AddYears(5)));
        Assert.Empty(future);
    }

    [Fact]
    public async Task EmptyAndPunctuationQueries_ReturnNothing_WithoutThrowing()
    {
        Assert.Empty(await Search(""));
        Assert.Empty(await Search("   "));
        Assert.Empty(await Search("\"\" ()*:-"));
        Assert.NotNull(await Search("a OR NOT \"b"));
    }

    [Fact]
    public async Task Suggestions_ComeFromTitlesAndFileNames_UpToEight()
    {
        var suggestions = await fixture.Index.SuggestAsync("nghi");

        Assert.Contains("Nghỉ hằng năm", suggestions);
        Assert.True(suggestions.Count <= 8);

        Assert.Contains(await fixture.Index.SuggestAsync("quy che"), s => s.StartsWith("quy-che", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Service_BuildsTheHitTheResultsScreenNeeds()
    {
        var hits = await fixture.Service.SearchAsync("chăm sóc con ốm");

        var hit = hits[0];
        Assert.Equal("Điểm b, khoản 1, Điều 4", hit.Position);
        Assert.Equal("quy-che-lam-viec.docx › Chương II › Điều 4 › Khoản 1 › Điểm b", hit.Breadcrumb);
        Assert.Equal("quy-che-lam-viec.docx", hit.DocumentName);
        Assert.Contains("\u0002chăm sóc con ốm\u0003", hit.Snippet);
        Assert.Equal("Điểm b, khoản 1, Điều 4 — quy-che-lam-viec.docx", hit.Citation);
        Assert.InRange(hit.MapIndex, 0, 47);
    }

    [Fact]
    public async Task Service_SuggestionsForTheSearchBox()
    {
        Assert.NotEmpty(await fixture.Service.SuggestAsync("doanh"));
    }

    // ----- Bộ 30 câu thử -----

    [Fact]
    public async Task SampleQueries_Top5Is80PercentOrBetter_AndEachUnder200ms()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Samples.File("queries.json")));
        var report = new StringBuilder();
        var top1 = 0;
        var top5 = 0;
        var total = 0;
        var slowest = TimeSpan.Zero;

        foreach (var q in json.RootElement.GetProperty("queries").EnumerateArray())
        {
            var query = q.GetProperty("query").GetString()!;
            var file = q.GetProperty("file").GetString()!;
            var label = q.GetProperty("label").GetString()!;

            var watch = Stopwatch.StartNew();
            var hits = await Search(query);
            watch.Stop();
            slowest = watch.Elapsed > slowest ? watch.Elapsed : slowest;

            var rank = hits.ToList().FindIndex(h => h.Document.FileName == file && h.Node.Label == label) + 1;
            total++;
            if (rank == 1) top1++;
            if (rank is >= 1 and <= 5) top5++;
            report.AppendLine($"| {query} | {file} | {label} | {(rank == 0 ? "không thấy" : rank.ToString())} | {watch.Elapsed.TotalMilliseconds:F0} ms |");
        }

        WriteResults(report.ToString(), top1, top5, total, slowest);

        Assert.Equal(28, total);
        Assert.True(top5 >= total * 0.8, $"Top 5 chỉ đạt {top5}/{total}");
        Assert.True(slowest < TimeSpan.FromMilliseconds(200), $"Câu chậm nhất mất {slowest.TotalMilliseconds:F0} ms");
    }

    // Đặt DOCPOINT_WRITE_RESULTS=1 để ghi kết quả vào samples/results.md (quy ước: sau mỗi chức năng).
    private static void WriteResults(string rows, int top1, int top5, int total, TimeSpan slowest)
    {
        if (Environment.GetEnvironmentVariable("DOCPOINT_WRITE_RESULTS") != "1")
            return;

        var md = new StringBuilder()
            .AppendLine("# Kết quả bộ câu thử")
            .AppendLine()
            .AppendLine($"Chức năng: F4 (tìm theo chữ). Ngày chạy: {DateTime.Now:dd/MM/yyyy}.")
            .AppendLine()
            .AppendLine($"- Top 1 đúng: {top1}/{total} ({100.0 * top1 / total:F0}%)")
            .AppendLine($"- Top 5 đúng: {top5}/{total} ({100.0 * top5 / total:F0}%)")
            .AppendLine($"- Câu chậm nhất: {slowest.TotalMilliseconds:F0} ms")
            .AppendLine()
            .AppendLine("| Câu tìm | File | Nhãn mong đợi | Hạng | Thời gian |")
            .AppendLine("| --- | --- | --- | --- | --- |")
            .Append(rows);
        File.WriteAllText(Samples.File("results.md"), md.ToString());
    }
}
