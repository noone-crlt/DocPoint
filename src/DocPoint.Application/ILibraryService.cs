namespace DocPoint.Application;

public interface ILibraryService
{
    Task<int> CountDocumentsAsync(CancellationToken ct = default);
}
