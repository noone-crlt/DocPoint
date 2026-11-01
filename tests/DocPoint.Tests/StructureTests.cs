using System.Text.Json;
using DocPoint.Application;
using DocPoint.Domain;
using DocPoint.Infrastructure.Readers;
using DocPoint.Tests.Support;

namespace DocPoint.Tests;

public class StructureTests
{
    private static readonly IDocumentParser[] Parsers = [new DocxReader(), new PdfReader(), new TextFileReader()];

    private static async Task<IReadOnlyList<Node>> NodesOf(string sampleFile)
    {
        var path = Samples.File(sampleFile);
        var parser = Parsers.First(p => p.CanParse(Path.GetExtension(path)));
        return StructureDetector.Detect(await parser.ReadAsync(path));
    }

    private static string Plain(string s) => TextNormalizer.RemoveDiacritics(s).ToLowerInvariant();

    [Fact]
    public async Task Regulations_BuildsChapterArticleClausePointTree()
    {
        var nodes = await NodesOf("quy-che-lam-viec.docx");

        var point = nodes.Single(n => n.Content.StartsWith("Nghỉ chăm sóc con ốm"));
        Assert.Equal(NodeKind.Point, point.Kind);
        Assert.Equal("Điểm b, khoản 1, Điều 4", point.Label);

        var clause = nodes[point.ParentOrdinal!.Value];
        Assert.Equal(NodeKind.Clause, clause.Kind);
        var article = nodes[clause.ParentOrdinal!.Value];
        Assert.Equal(NodeKind.Article, article.Kind);
        Assert.Equal("Nghỉ không hưởng lương", article.Title);
        Assert.Equal("Điều 4", $"Điều {article.Number}");
    }

    [Fact]
    public async Task Report_NestsNumberedHeadingsUnderChapters()
    {
        var nodes = await NodesOf("bao-cao-quy-3.docx");

        var section = nodes.Single(n => n.Title == "Mảng dịch vụ bảo trì");
        Assert.Equal("Mục 2.3, Chương 2", section.Label);
        Assert.Equal(NodeKind.Chapter, nodes[section.ParentOrdinal!.Value].Kind);
        Assert.Contains("Tỷ lệ gia hạn", section.Content);
        Assert.Equal(2, section.Page);
    }

    [Fact]
    public async Task Textbook_DropsAncestorsImpliedByTheNumber()
    {
        var nodes = await NodesOf("giao-trinh-co-so-du-lieu.docx");

        Assert.Equal("Mục 1.1.2, Chương 1", nodes.Single(n => n.Title == "Hệ quản trị cơ sở dữ liệu").Label);
    }

    [Fact]
    public async Task Contract_TypedNumbersBecomeClausesOfTheirArticle()
    {
        var nodes = await NodesOf("hop-dong-thue-van-phong.docx");

        var clause = nodes.Single(n => n.Content.Contains("tiền đặt cọc"));
        Assert.Equal(NodeKind.Clause, clause.Kind);
        Assert.Equal("Khoản 3.2, Điều 3", clause.Label);
        Assert.False(clause.NeedsReview);
    }

    [Fact]
    public async Task Letter_WithoutStructureFallsBackToParagraphs()
    {
        var nodes = await NodesOf("cong-van-so-125.docx");

        Assert.Equal(8, nodes.Count);
        Assert.All(nodes, n => Assert.Equal(NodeKind.Paragraph, n.Kind));
        Assert.Equal("Đoạn 6", nodes[5].Label);
        // "1.250.000.000" không được nhầm với số thứ tự nhiều tầng.
        Assert.Contains("1.250.000.000", nodes[5].Content);
    }

    [Fact]
    public async Task UnstructuredPdf_SplitsByPageThenParagraph()
    {
        var nodes = await NodesOf("thong-bao-di-doi-kho.pdf");

        var last = nodes.Single(n => n.Content.Contains("0243 555 0199"));
        Assert.Equal("Đoạn 2, trang 2", last.Label);
        Assert.Equal(2, nodes.Count(n => n.Kind == NodeKind.Page));
    }

    [Fact]
    public async Task Pdf_HeadingLabelsCarryTheExactPage()
    {
        var nodes = await NodesOf("bao-cao-an-toan-lao-dong.pdf");

        Assert.Equal("Mục 2.1, trang 2", nodes.Single(n => n.Title == "Phòng cháy chữa cháy").Label);
    }

    [Fact]
    public async Task NumberedTextFile_UsesNumberedLinesAsSections()
    {
        var nodes = await NodesOf("ghi-chu-hop.txt");

        var section = nodes.Single(n => n.Title == "Kế hoạch tuyển dụng");
        Assert.Equal("Mục 2", section.Label);
        Assert.Contains("- Cần tuyển thêm 3 nhân viên", section.Content);
    }

    [Fact]
    public async Task NodesAreInReadingOrder_WithParentsBeforeChildren()
    {
        foreach (var file in new[] { "quy-che-lam-viec.docx", "bao-cao-quy-3.docx", "hop-dong-thue-van-phong.docx" })
        {
            var nodes = await NodesOf(file);
            for (var i = 0; i < nodes.Count; i++)
            {
                Assert.Equal(i, nodes[i].Ordinal);
                Assert.True(nodes[i].ParentOrdinal is null || nodes[i].ParentOrdinal < i);
            }
        }
    }

    [Fact]
    public async Task EveryQueryInTheSampleSet_LandsOnTheExpectedLabel()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Samples.File("queries.json")));
        var failures = new List<string>();
        var checkedCount = 0;

        foreach (var q in json.RootElement.GetProperty("queries").EnumerateArray())
        {
            var file = q.GetProperty("file").GetString()!;
            var terms = Plain(q.GetProperty("query").GetString()!).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var expected = q.GetProperty("label").GetString()!;

            var labels = (await NodesOf(file))
                .Where(n => terms.All(Plain(n.Title + " " + n.Content).Contains))
                .Select(n => n.Label)
                .ToList();

            checkedCount++;
            if (!labels.Contains(expected))
                failures.Add($"{file} / \"{string.Join(' ', terms)}\": cần \"{expected}\", có [{string.Join(" | ", labels)}]");
        }

        Assert.Equal(28, checkedCount);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void SkippedNumber_MarksNodeForReview_WithReason()
    {
        RawBlock[] blocks =
        [
            new("Điều 1. Phạm vi"),
            new("1. Khoản một."),
            new("2. Khoản hai."),
            new("4. Khoản bốn nhảy cóc."),
        ];

        var nodes = StructureDetector.Detect(blocks);

        var jumped = nodes.Single(n => n.Content.StartsWith("Khoản bốn"));
        Assert.True(jumped.NeedsReview);
        Assert.True(jumped.Confidence < StructureDetector.ReviewThreshold);
        Assert.Equal("Số thứ tự nhảy từ 2 sang 4", jumped.ReviewReason);
        Assert.All(nodes.Where(n => n != jumped), n => Assert.False(n.NeedsReview));
    }

    [Fact]
    public void TableRows_PairHeaderWithValues()
    {
        RawBlock[] blocks =
        [
            new("Danh sách", BlockKind.Heading, HeadingLevel: 1),
            new("Phần 1 mở đầu", BlockKind.Heading, HeadingLevel: 1),
            new("Họ tên | Lớp | Điểm", BlockKind.TableRow, Cells: ["Họ tên", "Lớp", "Điểm"]),
            new("Nguyễn Văn A | 12A1 | 8,5", BlockKind.TableRow, Cells: ["Nguyễn Văn A", "12A1", "8,5"]),
        ];

        var nodes = StructureDetector.Detect(blocks);

        var row = nodes.Single(n => n.Kind == NodeKind.Row);
        Assert.Equal("Họ tên: Nguyễn Văn A; Lớp: 12A1; Điểm: 8,5", row.Content);
        Assert.Equal("Dòng 1, Phần 1", row.Label);
    }

    [Fact]
    public void VeryLongNode_IsSplitIntoParagraphChildren()
    {
        var sentence = "Đây là một câu khá dài để thử việc chia nhỏ nội dung của một node quá lớn. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 200));
        RawBlock[] blocks =
        [
            new("Điều 1. Rất dài"),
            new(text),
            new("Điều 2. Ngắn"),
            new("Một dòng."),
        ];

        var nodes = StructureDetector.Detect(blocks);

        var pieces = nodes.Where(n => n.Kind == NodeKind.Paragraph).ToList();
        Assert.True(pieces.Count >= 3);
        Assert.All(pieces, p => Assert.True(p.Content.Split(' ').Length <= 600));
        Assert.StartsWith("Đoạn 1, Điều 1", pieces[0].Label);
    }
}

public class StructureTitleTests
{
    [Fact]
    public void ContentNodes_KeepTheirTextInContentOnly()
    {
        RawBlock[] blocks =
        [
            new("Điều 1. Phạm vi"),
            new("1. Khoản một."),
            new("a) Điểm a."),
            new("Điều 2. Khác"),
        ];

        var nodes = StructureDetector.Detect(blocks);

        var clause = nodes.Single(n => n.Kind == NodeKind.Clause);
        var point = nodes.Single(n => n.Kind == NodeKind.Point);
        Assert.Equal("", clause.Title);
        Assert.Equal("Khoản một.", clause.Content);
        Assert.Equal("", point.Title);
        Assert.Equal("Điểm a.", point.Content);
        Assert.Equal("Phạm vi", nodes.First(n => n.Kind == NodeKind.Article).Title);
    }
}
