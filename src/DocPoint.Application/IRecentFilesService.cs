using DocPoint.Domain;

namespace DocPoint.Application;

public interface IRecentFilesService
{
    Task<IReadOnlyList<RecentDocument>> GetRecentAsync(CancellationToken ct = default);
}
