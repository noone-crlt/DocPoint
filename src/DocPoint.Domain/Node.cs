namespace DocPoint.Domain;

public enum NodeKind
{
    /// <summary>Gốc ảo của tài liệu (không có nhãn).</summary>
    Root,
    Part,       // Phần
    Chapter,    // Chương
    Section,    // Mục
    Article,    // Điều
    Clause,     // Khoản
    Point,      // Điểm
    Heading,    // Tiêu đề thường, kể cả số nhiều tầng 1.1.1
    Paragraph,  // Đoạn
    Page,       // Trang (tài liệu không có cấu trúc)
    Sheet,      // Sheet Excel
    Row,        // Dòng trong bảng
}

public static class NodeKindExtensions
{
    /// <summary>Đơn vị nhỏ: viết thường khi đứng sau đơn vị khác trong nhãn vị trí.</summary>
    public static bool IsMinor(this NodeKind kind) =>
        kind is NodeKind.Clause or NodeKind.Point or NodeKind.Paragraph or NodeKind.Page or NodeKind.Row;

    public static string DisplayName(this NodeKind kind) => kind switch
    {
        NodeKind.Part => "Phần",
        NodeKind.Chapter => "Chương",
        NodeKind.Section => "Mục",
        NodeKind.Article => "Điều",
        NodeKind.Clause => "Khoản",
        NodeKind.Point => "Điểm",
        NodeKind.Heading => "Mục",
        NodeKind.Paragraph => "Đoạn",
        NodeKind.Page => "Trang",
        NodeKind.Sheet => "Sheet",
        NodeKind.Row => "Dòng",
        _ => "",
    };
}

/// <summary>Một chỗ trong tài liệu có thể tìm tới và chỉ ra vị trí.</summary>
public sealed class Node
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public long? ParentId { get; set; }

    /// <summary>Trước khi ghi: cha trỏ bằng <see cref="Ordinal"/> của nó (null là gốc). Sau khi đọc lại: không dùng.</summary>
    public int? ParentOrdinal { get; set; }
    public NodeKind Kind { get; set; }

    /// <summary>Thứ tự trong tài liệu, tính từ 0; dùng để dựng lại đúng thứ tự đọc.</summary>
    public int Ordinal { get; set; }

    /// <summary>Số hiệu như người viết: "5", "2.3", "II", "a".</summary>
    public string Number { get; set; } = "";

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";

    /// <summary>Nhãn vị trí đầy đủ, ví dụ "Điểm a, khoản 2, Điều 5".</summary>
    public string Label { get; set; } = "";

    public int? Page { get; set; }

    /// <summary>0 đến 1; thấp nghĩa là việc tách chỗ này có thể sai.</summary>
    public double Confidence { get; set; } = 1;

    public bool NeedsReview { get; set; }

    /// <summary>Lý do cần xem, bằng chữ cho người dùng ("Số thứ tự nhảy từ 2 sang 4").</summary>
    public string? ReviewReason { get; set; }
}
