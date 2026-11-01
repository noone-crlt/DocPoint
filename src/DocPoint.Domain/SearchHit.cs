namespace DocPoint.Domain;

public enum Validity { NotApplicable, InForce, Expired }

/// <summary>
/// Một chỗ khớp trong tài liệu. Snippet và Body đánh dấu chỗ khớp bằng <see cref="HighlightStart"/>
/// và <see cref="HighlightEnd"/> (cặp ký tự mà hàm highlight() của FTS5 sẽ được cấu hình để trả về).
/// MapIndex là vị trí của chỗ khớp trong văn bản, chia thành 48 phần, để vẽ bản đồ văn bản.
/// </summary>
public sealed record SearchHit(
    string Position,
    string Breadcrumb,
    string DocumentName,
    string DocumentInfo,
    string Snippet,
    IReadOnlyList<string> Body,
    Validity Validity,
    string? ValidityNote = null,
    int MapIndex = 0)
{
    public const char HighlightStart = '\u0002';
    public const char HighlightEnd = '\u0003';

    public string Citation => $"{Position} — {DocumentName}";
}
