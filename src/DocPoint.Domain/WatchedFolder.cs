namespace DocPoint.Domain;

public sealed class WatchedFolder
{
    public long Id { get; set; }
    public string Path { get; set; } = "";
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>Mẫu tên bỏ qua, ví dụ "node_modules", ".git".</summary>
    public IReadOnlyList<string> IgnorePatterns { get; set; } = [];

    /// <summary>Tự đọc file mới xuất hiện.</summary>
    public bool AutoIndex { get; set; } = true;

    public DateTime? LastScanAt { get; set; }
    public string Status { get; set; } = "";
}
