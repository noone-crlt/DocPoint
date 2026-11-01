using DocPoint.Application;

namespace DocPoint.App.Services.Fakes;

public sealed class FakeLibraryService : ILibraryService
{
    public Task<int> CountDocumentsAsync(CancellationToken ct = default) => Task.FromResult(2418);
}
