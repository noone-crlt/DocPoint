using System.Text;
using DocPoint.Application;
using DocPoint.Domain;
using DocPoint.Infrastructure.Readers;
using DocPoint.Tests.Support;

namespace DocPoint.Tests;

public class DocxReaderTests
{
    private readonly DocxReader _reader = new();

    [Fact]
    public async Task Report_HeadingLevelsComeFromOutlineLevel_NotStyleName()
    {
        var blocks = await _reader.ReadAsync(Samples.File("bao-cao-quy-3.docx"));

        var h1 = blocks.Single(b => b.Text == "Chương 2. Kết quả theo mảng kinh doanh");
        var h2 = blocks.Single(b => b.Text == "2.3 Mảng dịch vụ bảo trì");
        Assert.Equal(BlockKind.Heading, h1.Kind);
        Assert.Equal(1, h1.HeadingLevel);
        Assert.Equal(2, h2.HeadingLevel);
        Assert.Equal(18, blocks.Count);
    }

    [Fact]
    public async Task Report_PagesComeFromRenderedBreaks_AndAreMarkedApproximate()
    {
        var blocks = await _reader.ReadAsync(Samples.File("bao-cao-quy-3.docx"));

        Assert.Equal(1, blocks.Single(b => b.Text == "2.2 Mảng doanh nghiệp").Page);
        Assert.Equal(2, blocks.Single(b => b.Text == "2.3 Mảng dịch vụ bảo trì").Page);
        Assert.All(blocks, b => Assert.True(b.PageIsApproximate));
    }

    [Fact]
    public async Task Regulations_AutomaticNumberingIsComputedFromNumberingXml()
    {
        var blocks = await _reader.ReadAsync(Samples.File("quy-che-lam-viec.docx"));

        string? NumberingOf(string startsWith) => blocks.Single(b => b.Text.StartsWith(startsWith)).Numbering;

        Assert.Equal("Điều 1.", NumberingOf("Phạm vi áp dụng"));
        Assert.Equal("Điều 3.", NumberingOf("Nghỉ hằng năm"));
        Assert.Equal("Điều 4.", NumberingOf("Nghỉ không hưởng lương"));
        Assert.Equal("a)", NumberingOf("Có việc riêng"));
        Assert.Equal("b)", NumberingOf("Nghỉ chăm sóc con ốm"));
        // Khoản đếm lại từ 1 sau mỗi Điều, và đếm tiếp sau khi các điểm a), b) kết thúc.
        Assert.Equal("1.", NumberingOf("Giờ làm việc từ"));
        Assert.Equal("2.", NumberingOf("Thời gian nghỉ không hưởng lương tối đa"));
    }

    [Fact]
    public async Task Letter_HasNoHeadingsAndNoPageInfo()
    {
        var blocks = await _reader.ReadAsync(Samples.File("cong-van-so-125.docx"));

        Assert.Equal(8, blocks.Count);
        Assert.All(blocks, b =>
        {
            Assert.Equal(BlockKind.Paragraph, b.Kind);
            Assert.Null(b.Page);
        });
    }

    [Fact]
    public async Task EncryptedOfficeFile_ReportsPasswordProtected()
    {
        using var temp = TempFile.With(".docx", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0]);

        var ex = await Assert.ThrowsAsync<DocumentReadException>(() => _reader.ReadAsync(temp.Path));

        Assert.Equal(DocumentStatus.PasswordProtected, ex.Status);
        Assert.Contains("mật khẩu", ex.Message);
    }

    [Fact]
    public async Task GarbageFile_ReportsCorrupt_WithVietnameseMessage()
    {
        using var temp = TempFile.With(".docx", Encoding.UTF8.GetBytes("đây không phải là file Word"));

        var ex = await Assert.ThrowsAsync<DocumentReadException>(() => _reader.ReadAsync(temp.Path));

        Assert.Equal(DocumentStatus.Corrupt, ex.Status);
        Assert.Contains("Không đọc được", ex.Message);
    }

    [Fact]
    public async Task FileOpenForWriting_IsStillReadable()
    {
        using var temp = TempFile.Copy(Samples.File("bao-cao-quy-3.docx"));
        // Word giữ file mở khi đang soạn: mở ghi và cho phép người khác đọc.
        using var word = new FileStream(temp.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        var blocks = await _reader.ReadAsync(temp.Path);

        Assert.NotEmpty(blocks);
    }

    [Fact]
    public async Task FileLockedExclusively_ReportsLocked()
    {
        using var temp = TempFile.Copy(Samples.File("bao-cao-quy-3.docx"));
        using var lockHolder = new FileStream(temp.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ex = await Assert.ThrowsAsync<DocumentReadException>(() => _reader.ReadAsync(temp.Path));

        Assert.Equal(DocumentStatus.Locked, ex.Status);
    }
}

public class PdfReaderTests
{
    private readonly PdfReader _reader = new();

    [Fact]
    public async Task ExportedFromWord_DetectsHeadingsByFontSize_AndDropsRepeatedHeaderFooter()
    {
        var blocks = await _reader.ReadAsync(Samples.File("bao-cao-an-toan-lao-dong.pdf"));

        Assert.DoesNotContain(blocks, b => b.Text.Contains("Công ty ABC"));
        Assert.DoesNotContain(blocks, b => b.Text.StartsWith("Trang "));

        var h1 = blocks.Single(b => b.Text == "2. Kết quả kiểm tra");
        var h2 = blocks.Single(b => b.Text == "2.1 Phòng cháy chữa cháy");
        Assert.Equal(BlockKind.Heading, h1.Kind);
        Assert.Equal(1, h1.HeadingLevel);
        Assert.Equal(2, h2.HeadingLevel);
        Assert.Equal(2, h2.Page);
        Assert.Equal(3, blocks.Single(b => b.Text == "3. Kiến nghị").Page);
        Assert.Equal(8, blocks.Count);
    }

    [Fact]
    public async Task ExportedFromWord_JoinsWrappedLinesIntoOneParagraph()
    {
        var blocks = await _reader.ReadAsync(Samples.File("bao-cao-an-toan-lao-dong.pdf"));

        Assert.Contains(blocks, b => b.Text ==
            "Đã kiểm tra 8 xưởng, phát hiện 14 vị trí chưa có biển cảnh báo và 3 bình chữa cháy hết hạn kiểm định.");
    }

    [Fact]
    public async Task Unstructured_SplitsParagraphsByLineGap_WithPages()
    {
        var blocks = await _reader.ReadAsync(Samples.File("thong-bao-di-doi-kho.pdf"));

        Assert.Equal(5, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(BlockKind.Paragraph, b.Kind));
        Assert.Equal([1, 1, 1, 2, 2], blocks.Select(b => b.Page!.Value));
        Assert.Contains("0243 555 0199", blocks[^1].Text);
    }

    [Fact]
    public async Task Scanned_ReportsNoText()
    {
        var ex = await Assert.ThrowsAsync<DocumentReadException>(() => _reader.ReadAsync(Samples.File("tai-lieu-scan.pdf")));

        Assert.Equal(DocumentStatus.NoText, ex.Status);
    }

    [Fact]
    public async Task GarbageFile_ReportsCorrupt()
    {
        using var temp = TempFile.With(".pdf", Encoding.UTF8.GetBytes("không phải pdf"));

        var ex = await Assert.ThrowsAsync<DocumentReadException>(() => _reader.ReadAsync(temp.Path));

        Assert.Equal(DocumentStatus.Corrupt, ex.Status);
    }
}

public class TextFileReaderTests
{
    private readonly TextFileReader _reader = new();

    [Fact]
    public async Task Utf8WithBom_IsDecoded()
    {
        var blocks = await _reader.ReadAsync(Samples.File("ghi-chu-hop.txt"));

        Assert.Equal(7, blocks.Count);
        Assert.Contains(blocks, b => b.Text == "- Anh Minh gửi báo giá cho trường Nguyễn Du trước thứ Sáu.");
    }

    [Fact]
    public async Task Windows1258_IsDetectedWhenNotValidUtf8()
    {
        var blocks = await _reader.ReadAsync(Samples.File("ghi-chu-cu.txt"));

        Assert.Equal(
            ["Nhắc việc cuối tuần", "- Gọi lại cho khách hàng về lịch bảo trì máy in.", "- Đặt phòng họp cho buổi đào tạo an toàn lao động."],
            blocks.Select(b => b.Text));
    }

    [Fact]
    public async Task Markdown_HashLinesBecomeHeadings()
    {
        using var temp = TempFile.With(".md", Encoding.UTF8.GetBytes("# Tổng quan\n\nNội dung.\n\n## Chi tiết\nThêm."));

        var blocks = await _reader.ReadAsync(temp.Path);

        Assert.Equal(BlockKind.Heading, blocks[0].Kind);
        Assert.Equal(1, blocks[0].HeadingLevel);
        Assert.Equal("Tổng quan", blocks[0].Text);
        Assert.Equal(2, blocks[2].HeadingLevel);
        Assert.Equal(BlockKind.Paragraph, blocks[3].Kind);
    }
}

internal sealed class TempFile : IDisposable
{
    private TempFile(string path) => Path = path;

    public string Path { get; }

    public static TempFile With(string extension, byte[] content)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "docpoint-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(path, content);
        return new TempFile(path);
    }

    public static TempFile Copy(string source) =>
        With(System.IO.Path.GetExtension(source), File.ReadAllBytes(source));

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
        }
    }
}
