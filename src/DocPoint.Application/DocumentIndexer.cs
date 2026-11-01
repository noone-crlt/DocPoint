using System.Security.Cryptography;
using DocPoint.Domain;

namespace DocPoint.Application;

/// <summary>
/// Đọc một file và tách cấu trúc (<see cref="PrepareAsync"/>), rồi ghi vào chỉ mục (<see cref="IndexAsync"/>).
/// File lỗi vẫn có kết quả: tài liệu không có node, kèm trạng thái và câu báo lỗi tiếng Việt,
/// để người dùng thấy file nào chưa đọc được.
/// </summary>
public sealed class DocumentIndexer(IEnumerable<IDocumentParser> parsers, IDocumentRepository repository)
{
    private readonly IReadOnlyList<IDocumentParser> _parsers = parsers.ToList();

    public bool CanIndex(string path) => FindParser(path) is not null;

    public async Task<Document> IndexAsync(long folderId, string path, CancellationToken ct = default)
    {
        var existing = await repository.GetByPathAsync(path, ct);
        var (document, nodes) = await PrepareAsync(folderId, path, existing, ct);
        return await repository.ReplaceAsync(document, nodes, ct);
    }

    /// <summary>Chỉ đọc và tách, chưa ghi: để nhiều luồng đọc song song rồi một luồng ghi.</summary>
    public async Task<(Document Document, IReadOnlyList<Node> Nodes)> PrepareAsync(
        long folderId, string path, Document? existing = null, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        var document = existing ?? new Document { Path = path };
        document.FolderId = folderId;
        document.FileName = info.Name;
        document.Extension = info.Extension.ToLowerInvariant();
        document.ModifiedAt = info.LastWriteTimeUtc;
        document.Size = info.Length;
        document.Error = null;

        IReadOnlyList<Node> nodes = [];
        try
        {
            document.Hash = await HashAsync(path, ct);
            var parser = FindParser(path) ?? throw new DocumentReadException(DocumentStatus.Corrupt,
                $"DocPoint chưa đọc được định dạng \"{document.Extension}\".");

            var blocks = await parser.ReadAsync(path, ct);
            nodes = StructureDetector.Detect(blocks);
            document.Status = nodes.Any(n => n.NeedsReview) ? DocumentStatus.NeedsReview : DocumentStatus.Indexed;
        }
        catch (DocumentReadException ex)
        {
            document.Status = ex.Status;
            document.Error = ex.Message;
        }
        catch (IOException)
        {
            document.Status = DocumentStatus.Locked;
            document.Error = $"\"{info.Name}\" đang được chương trình khác mở. DocPoint sẽ tự thử lại sau.";
        }

        return (document, nodes);
    }

    private IDocumentParser? FindParser(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return _parsers.FirstOrDefault(p => p.CanParse(ext));
    }

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }
}
