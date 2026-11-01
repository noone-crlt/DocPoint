using DocPoint.Application;

namespace DocPoint.App.Services.Fakes;

public sealed class FakeFolderWatchService : IFolderWatchService
{
    public bool IsReading => true;

    public event EventHandler? StatusChanged { add { } remove { } }
}
