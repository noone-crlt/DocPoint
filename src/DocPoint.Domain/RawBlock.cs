namespace DocPoint.Domain;

public enum BlockKind { Paragraph, Heading, TableRow }

/// <summary>
/// Một khối chữ do reader đọc ra, chưa hiểu cấu trúc. Đầu ra chung của mọi định dạng file.
/// </summary>
/// <param name="HeadingLevel">Cấp tiêu đề thật (1 là cao nhất) nếu file nói rõ; null nếu không biết.</param>
/// <param name="Numbering">Chữ đánh số đầu dòng ("1.", "a)", "Điều 3.") khi nó không nằm trong <paramref name="Text"/>, như đánh số tự động của Word.</param>
/// <param name="Cells">Với dòng bảng: nội dung từng ô, để F2 ghép với tiêu đề cột.</param>
/// <param name="Page">Số trang nếu biết; <paramref name="PageIsApproximate"/> bật khi chỉ là ước lượng (DOCX).</param>
public sealed record RawBlock(
    string Text,
    BlockKind Kind = BlockKind.Paragraph,
    int? HeadingLevel = null,
    string? Numbering = null,
    int? Page = null,
    bool PageIsApproximate = false,
    bool IsBold = false,
    IReadOnlyList<string>? Cells = null);
