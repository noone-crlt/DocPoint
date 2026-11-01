namespace DocPoint.Domain;

public enum DocumentStatus
{
    Pending,
    Indexed,
    /// <summary>File đang bị chương trình khác khoá; sẽ thử lại.</summary>
    Locked,
    PasswordProtected,
    /// <summary>PDF scan: không có chữ để đọc.</summary>
    NoText,
    Corrupt,
    TooLarge,
    NeedsReview,
}

public sealed class Document
{
    public long Id { get; set; }
    public long FolderId { get; set; }
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";

    /// <summary>Đuôi file viết thường, có dấu chấm: ".docx".</summary>
    public string Extension { get; set; } = "";

    public DateTime ModifiedAt { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; } = "";
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>Câu tiếng Việt cho người dùng, không phải stack trace.</summary>
    public string? Error { get; set; }

    public DateTime? IndexedAt { get; set; }
}
