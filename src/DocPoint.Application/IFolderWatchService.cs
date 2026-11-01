namespace DocPoint.Application;

public interface IFolderWatchService
{
    /// <summary>Đang có file được đọc trong các thư mục theo dõi.</summary>
    bool IsReading { get; }

    event EventHandler? StatusChanged;
}
