namespace DocPoint.Domain;

public enum DocumentKind { Legal, Word, Pdf, Excel, Text }

public sealed record RecentDocument(
    string Name,
    string Folder,
    string Position,
    DocumentKind Kind,
    DateTime OpenedAt,
    bool IsPinned);
