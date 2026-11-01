using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.Infrastructure.Readers;

internal static class ReaderSupport
{
    /// <summary>Mở để đọc mà không chặn người khác: đọc được cả khi file đang mở trong Word.</summary>
    public static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);

    /// <summary>Đổi lỗi thô khi mở hoặc đọc file thành lỗi có trạng thái và câu tiếng Việt.</summary>
    public static DocumentReadException Translate(Exception ex, string path)
    {
        var name = Path.GetFileName(path);
        return ex switch
        {
            DocumentReadException known => known,
            FileNotFoundException or DirectoryNotFoundException =>
                new(DocumentStatus.Corrupt, $"Không còn thấy file \"{name}\". Có thể file đã bị xoá hoặc đổi tên.", ex),
            UnauthorizedAccessException =>
                new(DocumentStatus.Locked, $"Không có quyền đọc \"{name}\". Hãy kiểm tra quyền truy cập của file.", ex),
            IOException io when IsSharingViolation(io) =>
                new(DocumentStatus.Locked, $"\"{name}\" đang được chương trình khác mở. DocPoint sẽ tự thử lại sau.", ex),
            _ => new(DocumentStatus.Corrupt, $"Không đọc được \"{name}\". File có thể bị hỏng hoặc sai định dạng.", ex),
        };
    }

    private static bool IsSharingViolation(IOException ex)
    {
        // ERROR_SHARING_VIOLATION (32) và ERROR_LOCK_VIOLATION (33) nằm ở 16 bit thấp của HResult.
        var code = ex.HResult & 0xFFFF;
        return code is 32 or 33;
    }

    /// <summary>File Office có mật khẩu là tệp OLE chứa EncryptedPackage chứ không phải zip.</summary>
    public static bool LooksEncryptedOffice(Stream stream)
    {
        var head = new byte[8];
        stream.Position = 0;
        var read = stream.Read(head, 0, head.Length);
        stream.Position = 0;
        return read == 8 && head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0;
    }
}
