using DocPoint.Domain;

namespace DocPoint.Application;

/// <summary>Đọc một loại file thành chuỗi khối chữ. Mỗi định dạng có một bản cài đặt.</summary>
public interface IDocumentParser
{
    /// <param name="extension">Đuôi viết thường, có dấu chấm: ".docx".</param>
    bool CanParse(string extension);

    /// <exception cref="DocumentReadException">File không đọc được; có trạng thái và câu báo lỗi cho người dùng.</exception>
    Task<IReadOnlyList<RawBlock>> ReadAsync(string path, CancellationToken ct = default);
}

/// <summary>Lỗi khi đọc file. <see cref="Exception.Message"/> là câu tiếng Việt nói rõ vấn đề và cách xử lý.</summary>
public sealed class DocumentReadException(DocumentStatus status, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public DocumentStatus Status { get; } = status;
}
